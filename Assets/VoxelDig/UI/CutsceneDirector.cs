using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using UnityEngine;

namespace SleepingAnimals
{
    /// <summary>
    /// 「暗転 → 人を配置 → 明転」の一連の演出をまとめて行う。
    ///
    /// 入力は暗転の前に止める。
    /// フェード中に動けてしまうと、明転したときに位置がズレて見えるため。
    /// </summary>
    public class CutsceneDirector : MonoBehaviour
    {
        /// <summary>暗転中に動かすオブジェクト1つ分。</summary>
        [Serializable]
        public class Placement
        {
            [Tooltip("動かす対象")]
            public Transform target;

            [Tooltip("移動先。位置と向きを両方使う")]
            public Transform destination;

            [Tooltip("暗転中にアクティブにする")]
            public bool activateOnPlace = true;

            [Tooltip("向きも合わせる")]
            public bool applyRotation = true;
        }

        [Header("参照")]
        //[SerializeField] private ScreenFader fader;
        [SerializeField] private RuleTransition fader;
        [SerializeField] private PlayerInputLock inputLock;

        [Header("配置する人")]
        [SerializeField] private Placement[] placements;

        [Header("間")]
        [Tooltip("暗転しきってから配置するまでの待ち（秒）")]
        [SerializeField] private float delayBeforePlace = 0.1f;

        [Tooltip("配置してから明転するまでの待ち（秒）")]
        [SerializeField] private float delayAfterPlace = 0.25f;

        // 二重に走らないようにするフラグ
        private bool isPlaying;

        /// <summary>演出中かどうか</summary>
        public bool IsPlaying => isPlaying;

        private void Awake()
        {
            if (fader == null) fader = FindAnyObjectByType<RuleTransition>();
            if (inputLock == null) inputLock = PlayerInputLock.Instance;
        }

        /// <summary>
        /// 暗転 → 配置 → 明転 を順に行う。
        /// 追加でやりたい処理があれば duringBlackout に渡す。
        /// </summary>
        public async UniTask PlayAsync(
            Func<CancellationToken, UniTask> duringBlackout = null,
            CancellationToken token = default)
        {
            if (isPlaying) return;
            isPlaying = true;

            // 暗転用の画面がシーンに置いてある場合、シーンが変わると消えているので探し直す
            if (fader == null) fader = FindAnyObjectByType<RuleTransition>();

            // オブジェクトが壊れたら自動で中断するトークンと束ねる
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                token, destroyCancellationToken);

            CancellationToken ct = linked.Token;

            // 入力は先に止める。
            // フェード中に動けると、明転したとき位置がズレて見える
            // シーンをまたいで使うと、前のシーンの PlayerInputLock は消えているので、毎回今のシーンのものを探し直す
            PlayerInputLock lockTarget = GetInputLock();
            if (lockTarget != null) lockTarget.Lock();

            try
            {
                // 1. 暗転
                if (fader != null) await fader.FadeOutAsync(ct);

                if (delayBeforePlace > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delayBeforePlace),
                                        DelayType.UnscaledDeltaTime, cancellationToken: ct);
                }

                // 2. 配置
                ApplyPlacements();

                // 追加の処理（カメラの切り替え、BGMの変更など）
                if (duringBlackout != null) await duringBlackout(ct);

                if (delayAfterPlace > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delayAfterPlace),
                                        DelayType.UnscaledDeltaTime, cancellationToken: ct);
                }

                // 3. 明転
                if (fader != null) await fader.FadeInAsync(ct);
            }
            finally
            {
                // 途中で中断されても、必ず操作を戻す。
                // ここを finally にしないと、操作不能のまま詰む
                if (lockTarget != null) lockTarget.Unlock();
                isPlaying = false;
            }
        }

        /// <summary>
        /// 明転だけを行う。
        /// 前のシーンで暗転したまま、このシーンに来たときに使う（シーンの Start などから呼ぶ）。
        /// beforeFadeIn を渡すと、明転する前（まだ暗いうち）にその処理を済ませてから明転する。
        /// </summary>
        /// <example>
        /// // 明転するだけ
        /// await cutsceneDirector.PlayFadeInAsync();
        ///
        /// // 暗いうちに準備してから明転する
        /// await cutsceneDirector.PlayFadeInAsync(ct => { SetupStage(); return UniTask.CompletedTask; });
        /// </example>
        public async UniTask PlayFadeInAsync(
            Func<CancellationToken, UniTask> beforeFadeIn = null,
            CancellationToken token = default)
        {
            if (isPlaying) return;
            isPlaying = true;

            // 暗転用の画面がシーンに置いてある場合、シーンが変わると消えているので探し直す
            if (fader == null) fader = FindAnyObjectByType<RuleTransition>();

            // オブジェクトが壊れたら自動で中断するトークンと束ねる
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                token, destroyCancellationToken);

            CancellationToken ct = linked.Token;

            // 明転しきるまでは操作できないようにする
            // シーンをまたいで使うと、前のシーンの PlayerInputLock は消えているので、毎回今のシーンのものを探し直す
            PlayerInputLock lockTarget = GetInputLock();
            if (lockTarget != null) lockTarget.Lock();

            try
            {
                // 1. 暗いうちにやっておく処理
                if (beforeFadeIn != null) await beforeFadeIn(ct);

                // 2. 明転
                if (fader != null) await fader.FadeInAsync(ct);
            }
            finally
            {
                // 途中で中断されても、必ず操作を戻す
                if (lockTarget != null) lockTarget.Unlock();
                isPlaying = false;
            }
        }

        /// <summary>
        /// 今のシーンの PlayerInputLock を返す。
        /// DontDestroyOnLoad で CutsceneDirector がシーンをまたいでも、
        /// 前のシーンの（もう消えた）PlayerInputLock を使わないようにする。
        /// </summary>
        private PlayerInputLock GetInputLock()
        {
            // 消えたオブジェクトは null と同じ扱いになるので、そのときだけ探し直す
            if (inputLock == null) inputLock = PlayerInputLock.Instance;
            return inputLock;
        }

        /// <summary>登録されたオブジェクトを移動先へ運ぶ。</summary>
        private void ApplyPlacements()
        {
            if (placements == null) return;

            foreach (Placement placement in placements)
            {
                if (placement?.target == null || placement.destination == null) continue;

                if (placement.applyRotation)
                {
                    placement.target.SetPositionAndRotation(
                        placement.destination.position, placement.destination.rotation);
                }
                else
                {
                    placement.target.position = placement.destination.position;
                }

                if (placement.activateOnPlace) placement.target.gameObject.SetActive(true);
            }
        }

        [Button("演出を再生")]
        private void PlayFromInspector()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("再生中のみ実行できます。", this);
                return;
            }

            PlayAsync().Forget();
        }
    }
}
