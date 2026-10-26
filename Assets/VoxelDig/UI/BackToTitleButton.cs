using Cysharp.Threading.Tasks;
using R3;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VoxelDig;

namespace SleepingAnimals
{
    [RequireComponent(typeof(CustomButton))]
    public class BackToTitleButton : BaseButton
    {
        [SerializeField]
        private CanvasGroup titleCanvasGroup;

        [SerializeField]
        private CanvasGroup inGameCanvasGroup;

        [SerializeField]
        private CutsceneDirector cutsceneDirector;

        [Tooltip("タイトルに戻るときに選択を解除する（無くても動く）")]
        [SerializeField]
        private DigSiteSelector selector;

        [SerializeField]
        private Volume volume;

        protected override void Start()
        {
            base.Start();

            _button.OnButtonPressed.AsObservable()
                .Subscribe(_ => OnPressed())
                .AddTo(this);
        }

        private void OnPressed()
        {
            // 参照が無いと暗転したまま戻れなくなるので、先に確認する
            if (cutsceneDirector == null || titleCanvasGroup == null)
            {
                Debug.LogError("[BackToTitleButton] Cutscene Director か Title Canvas Group が設定されていません", this);
                return;
            }

            // 別の演出の再生中は何もしない（押しても反応しない理由がわかるようにログを出す）
            if (cutsceneDirector.IsPlaying)
            {
                Debug.Log("[BackToTitleButton] 演出の再生中なので、タイトルに戻れません", this);
                return;
            }

            Debug.Log("タイトルに戻るボタンを押した");
            cutsceneDirector.PlayAsync(BackToTitleTask).Forget();
        }

        // 暗転中にタイトルを表示（CutsceneDirector にそのまま渡せる形）
        private UniTask BackToTitleTask(CancellationToken ct)
        {
            // ブロックの選択を解除して、選択カードも閉じる
            if (selector != null) selector.ClearSelection();

            // HideWhenReturnedFromDig で GameObject ごと非表示になっている場合があるので、先に表示する
            titleCanvasGroup.gameObject.SetActive(true);

            if (volume.profile.TryGet(out DepthOfField depthOfField))
            {
                depthOfField.active = true;
            }
            inGameCanvasGroup.gameObject.SetActive(false);
            titleCanvasGroup.alpha = 1.0f;
            titleCanvasGroup.interactable = true;   // タイトルのボタンを押せるようにする
            titleCanvasGroup.blocksRaycasts = true; // タイトルの UI がクリックを受け取るようにする（後ろのブロックは選べなくなる）
            return UniTask.CompletedTask;
        }
    }
}