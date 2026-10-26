using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SleepingAnimals;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelDig
{
    /// <summary>
    /// 発掘シーンの進行を管理するクラス
    ///
    /// 流れ：
    ///   1. シーンに入ったら、探索シーンから渡された CSV でブロックを生成
    ///      （発掘シーンを単体で再生したときは DigBlock に設定された CSV を使う）
    ///   2. 画面が明るくなりながらカメラがブロックに寄る → 掘れるようになる
    ///      （CutsceneDirector が設定されていれば、明転は CutsceneDirector が行う）
    ///   3. 「戻る」/ Esc → クリアしていれば記録して、CutsceneDirector で暗転 → 探索シーンへ戻る
    ///   4. 「リトライ」→ 暗転 →（暗転中）カメラを最初の位置に戻してブロックを作り直す
    ///      → 明転 → カメラが斜めの位置へ動く（シーンに入ったときと同じ演出）
    /// </summary>
    public class DigSceneController : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("DigBlock の Generate On Start は OFF にしておく（このクラスが生成する）")]
        [SerializeField] private DigBlock block;
        [SerializeField] private DigController controller;
        [SerializeField] private OrbitCamera orbitCamera;

        //[Header("1シーン構成")]
        //[Tooltip("ダンジョンと同じシーンに発掘ステージを置くときに設定する。設定するとシーンを読み込まず、SingleSceneDigFlow が切り替えを行う")]
        //[SerializeField] private SingleSceneDigFlow singleSceneFlow;

        [Header("シーン")]
        [Tooltip("探索シーンを通らずに再生したときの戻り先（空なら戻らずにブロックを作り直す）")]
        [SerializeField] private string fallbackReturnSceneName = "";

        [Header("UI")]
        [Tooltip("発掘中に表示するパネル（DigHud の UI をこの下にまとめる）")]
        [SerializeField] private CanvasGroup digPanel;

        [Tooltip("発掘ポイントの名前を出すテキスト（無くても動く）")]
        [SerializeField] private TMP_Text siteNameText;

        [Tooltip("探索シーンへ戻るボタン（HUD の「戻る」、リザルトの「戻る」など複数登録できる）")]
        [SerializeField] private Button[] exitButtons;

        [Tooltip("強化パネル（Esc で、シーンから戻る代わりにパネルを閉じる。無くても動く）")]
        [SerializeField] private UpgradePanel upgradePanel;

        [Tooltip("もう一度掘り直すボタン（リザルトの「リトライ」など。DigHud の Retry Button に入れたものは自動でこちらの演出になるので、ここに入れなくてよい）")]
        [SerializeField] private Button[] retryButtons;

        [Header("画面切り替え")]
        [Tooltip("明転・暗転を行う演出（設定されていればこちらを使う。このシーンにも CutsceneDirector を置いておく）")]
        [SerializeField] private CutsceneDirector cutsceneDirector;

        [Tooltip("CutsceneDirector が無いときの予備の暗転・明転用（画面全体を覆う黒い Image の CanvasGroup。無くても動く）")]
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private float fadeDuration = 0.4f;

        [Header("入ったときの演出")]
        [Tooltip("ON：明転が終わってから、カメラが斜めの位置へ動く ／ OFF：明転と同時に動く")]
        [SerializeField] private bool moveCameraAfterFade = true;

        [Tooltip("明転が終わってから、カメラが動き出すまでの間（秒）")]
        [SerializeField] private float cameraMoveDelay = 0.2f;

        private bool isReady;   // 掘れる状態になったか
        private bool isLeaving; // シーン移動中（ボタンの連打を防ぐ）

        /// <summary>掘れる状態か（カメラの移動が終わって、操作できるようになったか）</summary>
        public bool IsReady => isReady;

        private void Start()
        {
            // シーンをまたいで残っている CutsceneDirector（PersistentTransitionRoot）があれば、そちらを使う
            // （Inspector でこのシーンに置いたものを設定していても、それは重複として消えるため）
            cutsceneDirector = PersistentTransitionRoot.Resolve(cutsceneDirector);

            // 準備ができるまでは掘れないようにしておく
            controller.enabled = false;
            if (!isReady) digPanel.SetVisible(false, 0f);

            foreach (var button in exitButtons)
            {
                button.onClick.AddListener(() => ExitAsync(destroyCancellationToken).Forget());
            }

            foreach (var button in retryButtons)
            {
                button.onClick.AddListener(Retry);
            }

            // 1シーン構成では、SingleSceneDigFlow が PrepareForEnter / PlayEnterCameraAsync を呼ぶので、ここでは何もしない
            //if (singleSceneFlow != null)
            //{
            //    if (fadeOverlay != null) fadeOverlay.SetVisible(false, 0f);
            //    return;
            //}

            // CutsceneDirector で明転するときは、予備の黒い画面は最初から消しておく（二重に暗くならないように）
            // 使わないときは、黒い画面から明転する
            if (fadeOverlay != null) fadeOverlay.SetVisible(cutsceneDirector == null, 0f);

            EnterAsync(destroyCancellationToken).Forget();
        }

        /// <summary>Esc キー：探索シーンへ戻る（PlayerInput の Cancel から呼ばれる）</summary>
        public void OnCancel(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            // 強化パネルが開いていたら、まずパネルを閉じる（いきなりシーンから戻らないように）
            if (upgradePanel != null && upgradePanel.IsOpen)
            {
                upgradePanel.Close();
                return;
            }

            ExitAsync(destroyCancellationToken).Forget();
        }

        // ---------------------------------------------------------------
        // シーンに入ったとき
        // ---------------------------------------------------------------

        /// <summary>
        /// 【1シーン構成用】暗転中に呼ぶ：ブロックを作って、掘る準備をする（まだ操作はできない）
        /// </summary>
        /// <param name="csv">埋めるモデルの CSV（null なら DigBlock に設定された CSV）</param>
        /// <param name="siteName">UI に出す名前</param>
        public void PrepareForEnter(TextAsset csv, string siteName)
        {
            isReady = false;
            isLeaving = false;
            controller.enabled = false;
            digPanel.SetVisible(false, 0f);

            SetupBlock(csv, siteName);
        }

        /// <summary>
        /// 【1シーン構成用】明転したあとに呼ぶ：カメラを斜めの位置へ動かして、掘れるようにする
        /// </summary>
        public async UniTask PlayEnterCameraAsync(CancellationToken token)
        {
            if (cameraMoveDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(cameraMoveDelay), DelayType.UnscaledDeltaTime, cancellationToken: token);
            }
            await orbitCamera.EnterAsync(block, token);
            StartDigging();
        }

        /// <summary>CSV でブロックを作り、名前を表示する</summary>
        private void SetupBlock(TextAsset csv, string siteName)
        {
            // CSV が渡されていれば、それを埋める（無ければ DigBlock の設定のまま）
            if (csv != null)
            {
                block.Setup(csv, 0f); // 0 = 大きさは DigBlock の voxelSize のまま
            }
            block.Generate();

            if (siteNameText != null)
            {
                siteNameText.SetText(string.IsNullOrEmpty(siteName) ? block.name : siteName);
            }
        }

        /// <summary>掘れるようにする（UI を出して、操作を有効にする）</summary>
        private void StartDigging()
        {
            digPanel.SetVisible(true).Forget();
            controller.enabled = true;
            isReady = true;
        }

        private async UniTaskVoid EnterAsync(CancellationToken token)
        {
            // 探索シーンから CSV が渡されていれば、それを埋める（無ければ DigBlock の設定のまま）
            SetupBlock(DigSceneData.HasRequest ? DigSceneData.VoxelCsv : null,
                       DigSceneData.HasRequest ? DigSceneData.SiteName : null);

            // 画面を明るくするのと、カメラがブロックに寄るのを同時に行い、両方終わるまで待つ
            UniTask fade;
            if (cutsceneDirector != null)
            {
                // 前のシーンで暗転したままなので、CutsceneDirector で明転する
                fade = cutsceneDirector.PlayFadeInAsync(token: token);
            }
            else
            {
                fade = fadeOverlay != null ? fadeOverlay.SetVisible(false, fadeDuration) : UniTask.CompletedTask;
            }
            if (moveCameraAfterFade)
            {
                // 1. 明転が終わるまで待つ（その間カメラはシーンに置いた位置のまま）
                await fade;

                // 2. 少し間を置く
                if (cameraMoveDelay > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(cameraMoveDelay), DelayType.UnscaledDeltaTime, cancellationToken: token);
                }

                // 3. カメラが斜めの位置（ブロックを見下ろす角度）へ動く
                await orbitCamera.EnterAsync(block, token);
            }
            else
            {
                // 明転とカメラの移動を同時に行い、両方終わるまで待つ
                UniTask cameraMove = orbitCamera.EnterAsync(block, token);
                await UniTask.WhenAll(fade, cameraMove);
            }

            // 掘れるようにする
            StartDigging();
        }

        // ---------------------------------------------------------------
        // リトライ（もう一度掘る）
        // ---------------------------------------------------------------

        /// <summary>
        /// もう一度掘り直す（リザルトの「リトライ」ボタンなどから呼ぶ）
        /// シーンに入ったときと同じように、暗転 → 作り直し → 明転 → カメラが斜めへ動く
        /// </summary>
        public void Retry()
        {
            RetryAsync(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid RetryAsync(CancellationToken token)
        {
            // 掘れる状態のときだけ受け付ける（連打や、戻る途中のリトライを防ぐ）
            if (!isReady || isLeaving) return;
            if (cutsceneDirector != null && cutsceneDirector.IsPlaying) return;

            isLeaving = true; // 「戻る」ボタンなども、リトライの演出中は受け付けない
            isReady = false;
            if (upgradePanel != null) upgradePanel.Close();
            controller.enabled = false;
            digPanel.SetVisible(false).Forget();

            try
            {
                // 1. 暗転 →（暗転中）カメラとブロックを最初の状態に戻す → 明転
                if (cutsceneDirector != null)
                {
                    await cutsceneDirector.PlayAsync(ResetForRetryAsync, token);
                }
                else if (fadeOverlay != null)
                {
                    // CutsceneDirector が無いときは、予備の黒い画面で暗転・明転する
                    await fadeOverlay.SetVisible(true, fadeDuration);
                    await ResetForRetryAsync(token);
                    await fadeOverlay.SetVisible(false, fadeDuration);
                }
                else
                {
                    await ResetForRetryAsync(token);
                }

                // 2. 明転が終わってから、カメラが斜めの位置へ動く → 掘れるようになる
                //    （シーンに入ったときと同じ動き）
                await PlayEnterCameraAsync(token);
            }
            finally
            {
                isLeaving = false;
            }
        }

        /// <summary>
        /// 暗転中に行う、リトライの準備
        /// </summary>
        private UniTask ResetForRetryAsync(CancellationToken token)
        {
            // カメラを、シーンに入る前に置いてあった位置へ一瞬で戻す
            // （EnterAsync を呼んだときに覚えた位置。ここから、また斜めの位置へ動き直す）
            orbitCamera.ExitImmediate();

            // 同じ CSV でブロックを作り直す（状態が Digging に戻るので、リザルト画面も消える）
            block.Generate();

            return UniTask.CompletedTask;
        }

        // ---------------------------------------------------------------
        // 探索シーンへ戻るとき
        // ---------------------------------------------------------------

        private async UniTaskVoid ExitAsync(CancellationToken token)
        {
            if (!isReady || isLeaving) return;

            // クリア演出中もクリア扱い
            DigState state = block.State.CurrentValue;
            bool cleared = state == DigState.Cleared || state == DigState.Revealing;

            // ---- 1シーン構成：シーンを読み込まず、SingleSceneDigFlow にダンジョンへ戻してもらう ----
            //if (singleSceneFlow != null)
            //{
            //    isLeaving = true;
            //    controller.enabled = false;
            //    digPanel.SetVisible(false).Forget();

            //    bool left = false;
            //    try
            //    {
            //        left = await singleSceneFlow.ExitDigAsync(cleared, token);
            //    }
            //    finally
            //    {
            //        isLeaving = false;
            //        if (left)
            //        {
            //            isReady = false;
            //        }
            //        else
            //        {
            //            // 戻れなかった（別の演出の再生中など）ときは、掘っている状態に戻す
            //            controller.enabled = true;
            //            digPanel.SetVisible(true).Forget();
            //        }
            //    }
            //    return;
            //}

            string returnScene = DigSceneData.HasRequest ? DigSceneData.ReturnSceneName : fallbackReturnSceneName;

            // 戻り先が無い（発掘シーンを単体でテストしている）ときは、ブロックを作り直すだけ
            if (string.IsNullOrEmpty(returnScene))
            {
                Debug.Log("[DigSceneController] 戻り先のシーンが無いので、ブロックを作り直します");
                block.Generate();
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(returnScene))
            {
                Debug.LogError($"[DigSceneController] 戻り先のシーン「{returnScene}」を読み込めません。" +
                               "Build Profiles の Scene List に入っているか確認してください", this);
                return;
            }

            isLeaving = true;
            controller.enabled = false;
            digPanel.SetVisible(false).Forget();

            // クリアしていたら「この発掘ポイントは発掘済み」と記録する
            if (cleared) DigSceneData.MarkCompleted(DigSceneData.SiteId);
            DigSceneData.ClearRequest();

            if (cutsceneDirector != null)
            {
                // CutsceneDirector で暗転して、暗転中に探索シーンを読み込む
                // （明転は、読み込んだ先の探索シーンの CutsceneDirector が行う）
                await cutsceneDirector.PlayAsync(ct => LoadSceneAsync(returnScene), token);
                return;
            }

            // CutsceneDirector が無いときは、黒い画面で暗転してから読み込む
            if (fadeOverlay != null)
            {
                // 万が一フェードが終わらなくても先へ進めるよう、フェード時間 + 1秒で待つのをやめる
                await UniTask.WhenAny(
                    fadeOverlay.SetVisible(true, fadeDuration),
                    UniTask.Delay(System.TimeSpan.FromSeconds(fadeDuration + 1f), ignoreTimeScale: true, cancellationToken: token));
            }
            await LoadSceneAsync(returnScene);
        }

        /// <summary>
        /// シーンを読み込む
        /// 読み込みが終わるとこのオブジェクトは消えるので、この後ろには何も書かない
        /// </summary>
        private static async UniTask LoadSceneAsync(string sceneName)
        {
            await SceneManager.LoadSceneAsync(sceneName);
        }
    }
}
