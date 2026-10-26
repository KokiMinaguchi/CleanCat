using UnityEngine;
using R3;

namespace SleepingAnimals
{
    [RequireComponent(typeof(CustomButton))]
    public class CloseButton : BaseButton
    {
        [SerializeField]
        private Canvas canvas;

        protected override void Start()
        {
            base.Start();

            _button.OnButtonPressed.AsObservable()
                .Subscribe(_ =>
                {
                    canvas.gameObject.SetActive(false);
                    Debug.Log($"{canvas.gameObject.name}‚Ì•Â‚¶‚éƒ{ƒ^ƒ“‚ð‰Ÿ‚µ‚½");
                }).AddTo(this);
        }
    }
}
