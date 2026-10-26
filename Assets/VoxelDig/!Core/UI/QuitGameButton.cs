using DG.Tweening;
using R3;
using UnityEngine;

namespace SleepingAnimals
{
    [RequireComponent(typeof(CustomButton))]
    public class QuitGameButton : BaseButton
    {
        protected override void Start()
        {
            base.Start();

            _button.OnButtonPressed.AsObservable()
                .Subscribe(_ =>
                {
                    //　ゲーム終了ボタンを押したら実行する
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBPLAYER
		Application.OpenURL("http://www.yahoo.co.jp/");
#else
		Application.Quit();
#endif
                }).AddTo(this);
        }
    }
}

