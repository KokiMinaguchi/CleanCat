using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SleepingAnimals
{
    [RequireComponent(typeof(CustomButton))]
    public class NewGameButton : BaseButton
    {
        [SerializeField]
        private CanvasGroup titleCanvasGroup;

        [SerializeField]
        private CanvasGroup inGameCanvasGroup;

        [SerializeField]
        private CutsceneDirector cutsceneDirector;

        [SerializeField]
        private Volume globalVolume;

        // 取得した Depth Of Field を保持しておく
        private DepthOfField depthOfField;

        protected override void Start()
        {
            base.Start();

            _button.OnButtonReleased.AsObservable()
                .Subscribe(_ =>
                {
                    Debug.Log("はじめからボタンを押した");

                    cutsceneDirector.PlayAsync(FadeOutTask).Forget();

                }).AddTo(this);
        }

        // 暗転中にタイトルを消す（CutsceneDirector にそのまま渡せる形）
        private UniTask FadeOutTask(CancellationToken ct)
        {
            inGameCanvasGroup.gameObject.SetActive(true);
            // profile から Depth Of Field を探して取り出す
            // 見つからなかったら false が返るので、警告を出しておく
            if (!globalVolume.profile.TryGet(out depthOfField))
            {
                Debug.LogWarning("Volume に Depth Of Field が追加されていません");
            }
            depthOfField.active = false;
            titleCanvasGroup.alpha = 0.0f;
            titleCanvasGroup.interactable = false;   // 見えないボタンを押せないようにする
            titleCanvasGroup.blocksRaycasts = false; // 見えない UI がクリックを吸わないようにする
            return UniTask.CompletedTask;
        }

        public void DisableDepthOfField()
        {
            if (depthOfField == null) return;
            depthOfField.active = false;
        }
    }
}