using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelDig
{
    /// <summary>
    /// ブロックのまわりをぐるっと回るカメラ
    /// ・右ドラッグで回転
    /// ・ホイールでズーム
    /// ・クリア後は自動でゆっくり回転
    ///
    /// 探索モードでは何もせず、EnterAsync で発掘モードに入ると
    /// 「今のカメラ位置 → ブロックを見る位置」へなめらかに移動してから操作できるようになる
    /// ExitAsync で元のカメラ位置に戻る
    ///
    /// PlayerInput（Behavior = Invoke Unity Events）の各イベントに
    /// OnOrbit / OnLook / OnZoom を登録して使う
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        [Tooltip("注視するブロック（DigSessionManager から自動で設定される。単体で使うときだけ Inspector で設定）")]
        [SerializeField] private DigBlock block;

        [Tooltip("ON なら Start で発掘モードに入る（DigSessionManager を使わず単体で動かすとき用）")]
        [SerializeField] private bool enterOnStart = false;

        [Header("回転")]
        [Tooltip("マウス移動量に対する回転の速さ")]
        [SerializeField] private float rotateSpeed = 0.25f;

        [Tooltip("上下の角度の制限")]
        [SerializeField] private float minPitch = -60f;
        [SerializeField] private float maxPitch = 80f;

        [Tooltip("発掘モードに入ったときの上下の角度（大きいほど上から見下ろす）")]
        [SerializeField] private float startPitch = 25f;

        [Tooltip("発掘モードに入ったときの横の角度を、今のカメラの向きからどれだけ回すか（度）。斜めから見せたいなら 45 など")]
        [SerializeField] private float startYawOffset = 0f;

        [Tooltip("クリア後の自動回転の速さ（度/秒）")]
        [SerializeField] private float autoRotateSpeed = 20f;

        [Header("ズーム（ブロックの大きさに対する倍率）")]
        [Tooltip("発掘モードに入ったときの距離（ブロックの大きさ × この値）")]
        [SerializeField] private float startDistanceRatio = 1.6f;
        [SerializeField] private float minDistanceRatio = 0.6f;
        [SerializeField] private float maxDistanceRatio = 3.5f;

        [Tooltip("ホイール1目盛りで近づく・離れる量（ブロックの大きさ × この値）")]
        [SerializeField] private float zoomStepRatio = 0.12f;

        [Tooltip("回転・ズームの追従のなめらかさ（大きいほどキビキビ）")]
        [SerializeField] private float smoothing = 12f;

        [Header("切り替え演出")]
        [Tooltip("探索 ⇔ 発掘 のカメラ移動にかける時間（秒）")]
        [SerializeField] private float transitionDuration = 0.8f;
        [SerializeField] private Ease transitionEase = Ease.InOutCubic;

        // 目標値（入力で直接変わる）と、実際の表示値（なめらかに追いかける）を分けて持つ
        private float targetYaw, targetPitch, targetDistance;
        private float yaw, pitch, distance;

        private bool isActive;        // 発掘モードで操作中か（移動演出中は false）
        private bool isOrbiting;      // 右ボタンを押しているか
        private Vector2 lookDelta;    // このフレームのマウス移動量
        private bool autoRotate;      // クリア後の自動回転中か

        private Vector3 shakeOffset;  // 揺れによるズレ（DOTween で動かす）
        private Tween shakeTween;
        private Sequence transitionTween;

        // 発掘モードに入る前のカメラの位置と向き（戻るときに使う）
        private Vector3 savedPosition;
        private Quaternion savedRotation;

        private CompositeDisposable bindings; // ブロックの状態の購読

        private void Start()
        {
            if (enterOnStart && block != null)
            {
                EnterAsync(block, destroyCancellationToken).Forget();
            }
        }

        private void OnDestroy()
        {
            shakeTween?.Kill();
            transitionTween?.Kill();
            bindings?.Dispose();
        }

        // ---------------------------------------------------------------
        // 発掘モードの出入り
        // ---------------------------------------------------------------

        /// <summary>
        /// 発掘モードに入る：今のカメラ位置を覚えてから、ブロックを見る位置へ移動する
        /// </summary>
        public async UniTask EnterAsync(DigBlock target, CancellationToken token)
        {
            SetTarget(target);

            // 戻るときのために、今の位置と向きを覚えておく
            savedPosition = transform.position;
            savedRotation = transform.rotation;

            // 今カメラがいる方向（横の角度）からブロックを見るようにすると、移動が自然になる
            Vector3 center = block.WorldCenter;
            Vector3 fromCenter = transform.position - center;
            fromCenter.y = 0f;
            float startYaw = fromCenter.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(-fromCenter.x, -fromCenter.z) * Mathf.Rad2Deg // 下の GetPose と逆の計算
                : transform.eulerAngles.y;

            float size = block.WorldSize;
            startYaw += startYawOffset; // 斜めから見せたいときは、ここで横に回す
            targetYaw = yaw = startYaw;
            targetPitch = pitch = startPitch;
            targetDistance = distance = size * startDistanceRatio;
            lookDelta = Vector2.zero;
            isOrbiting = false;

            // 移動先の位置と向きを計算して、そこまで DOTween で動かす
            GetPose(center, yaw, pitch, distance, out Vector3 pos, out Quaternion rot);
            await PlayTransition(pos, rot, token);

            isActive = true;
        }

        /// <summary>
        /// 発掘モードから出る：覚えておいた元の位置へ戻る
        /// </summary>
        public async UniTask ExitAsync(CancellationToken token)
        {
            isActive = false;
            autoRotate = false;
            shakeTween?.Kill();

            await PlayTransition(savedPosition, savedRotation, token);

            SetTarget(null);
        }

        /// <summary>
        /// 発掘モードから出て、アニメーションせずに一瞬で元の位置へ戻す
        /// （暗転中に戻すとき用。画面が隠れているので動きを見せる必要がない）
        /// </summary>
        public void ExitImmediate()
        {
            // 操作・自動回転・揺れ・移動の演出をすべて止める
            isActive = false;
            autoRotate = false;
            isOrbiting = false;
            lookDelta = Vector2.zero;
            shakeTween?.Kill();
            transitionTween?.Kill();

            // EnterAsync のときに覚えておいた「シーンに置いてあった位置と向き」へ一瞬で戻す
            transform.SetPositionAndRotation(savedPosition, savedRotation);
            SetTarget(null);
        }

        /// <summary>注視するブロックを差し替える</summary>
        public void SetTarget(DigBlock target)
        {
            bindings?.Dispose();
            bindings = new CompositeDisposable();

            block = target;
            autoRotate = false;
            if (block == null) return;

            // クリアしたら自動回転を始め、それ以外では止める
            block.State
                .Subscribe(s => autoRotate = s == DigState.Revealing || s == DigState.Cleared)
                .AddTo(bindings);
        }

        private async UniTask PlayTransition(Vector3 position, Quaternion rotation, CancellationToken token)
        {
            transitionTween?.Kill();
            transitionTween = DOTween.Sequence()
                .Join(transform.DOMove(position, transitionDuration))
                .Join(transform.DORotateQuaternion(rotation, transitionDuration))
                .SetEase(transitionEase);

            // カメラの移動が終わるまで待つ（TweenAwaitExtensions.WaitForKillAsync）
            await transitionTween.WaitForKillAsync(token);
        }

        /// <summary>
        /// 角度と距離から、カメラの位置と向きを計算する
        /// 注視点から「向いている方向の逆向きに distance」離れた位置に置く
        /// </summary>
        private static void GetPose(Vector3 center, float yaw, float pitch, float distance,
            out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(pitch, yaw, 0f);
            position = center + rotation * new Vector3(0f, 0f, -distance);
        }

        // ---------------------------------------------------------------
        // 入力（PlayerInput の Unity Events から呼ばれる）
        // ---------------------------------------------------------------

        /// <summary>回転ボタン（マウス右ボタン）</summary>
        public void OnOrbit(InputAction.CallbackContext context)
        {
            if (context.performed) isOrbiting = isActive;
            else if (context.canceled) isOrbiting = false;
        }

        /// <summary>マウスの移動量（Delta）</summary>
        public void OnLook(InputAction.CallbackContext context)
        {
            // 1フレーム中に複数回呼ばれることがあるので足し合わせる
            if (context.performed && isActive) lookDelta += context.ReadValue<Vector2>();
        }

        /// <summary>ホイール</summary>
        public void OnZoom(InputAction.CallbackContext context)
        {
            if (!context.performed || !isActive || block == null) return;

            float scroll = context.ReadValue<Vector2>().y;
            if (Mathf.Approximately(scroll, 0f)) return;

            // 環境によってホイール1目盛りの値（1 や 120）が違うので、向きだけを使う
            float size = block.WorldSize;
            targetDistance = Mathf.Clamp(
                targetDistance - Mathf.Sign(scroll) * zoomStepRatio * size,
                minDistanceRatio * size,
                maxDistanceRatio * size);
        }

        // ---------------------------------------------------------------
        // 更新
        // ---------------------------------------------------------------

        private void LateUpdate()
        {
            // 探索モード中・移動演出中はカメラを動かさない
            if (!isActive || block == null) return;

            if (isOrbiting)
            {
                targetYaw += lookDelta.x * rotateSpeed;
                targetPitch = Mathf.Clamp(targetPitch - lookDelta.y * rotateSpeed, minPitch, maxPitch);
            }
            else if (autoRotate)
            {
                targetYaw += autoRotateSpeed * Time.deltaTime;
            }
            lookDelta = Vector2.zero; // 使い終わったらリセット

            // 目標値になめらかに近づける（フレームレートに依存しない書き方）
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, targetYaw, t);
            pitch = Mathf.Lerp(pitch, targetPitch, t);
            distance = Mathf.Lerp(distance, targetDistance, t);

            GetPose(block.WorldCenter, yaw, pitch, distance, out Vector3 pos, out Quaternion rot);
            transform.SetPositionAndRotation(pos + shakeOffset, rot);
        }

        /// <summary>
        /// カメラを揺らす（掘ったときの手応え用）
        /// </summary>
        public void Shake(float strength, float duration = 0.15f)
        {
            if (strength <= 0f || !isActive) return;

            shakeTween?.Kill();
            shakeOffset = Vector3.zero;
            // DOTween の汎用 Shake で shakeOffset を揺らし、LateUpdate で位置に足す
            shakeTween = DOTween.Shake(() => shakeOffset, v => shakeOffset = v, duration, strength, 20, 90f, false)
                .OnKill(() => shakeOffset = Vector3.zero);
        }
    }
}