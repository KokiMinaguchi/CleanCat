using R3;
using UnityEngine;

namespace SleepingAnimals
{
    [RequireComponent(typeof(CustomButton))]
    public class ContinueButton : BaseButton
    {
        protected override void Start()
        {
            base.Start();

            _button.OnButtonPressed.AsObservable()
                .Subscribe(_ =>
                {
                    Debug.Log("‚Â‚Ã‚«‚©‚çƒ{ƒ^ƒ“‚ð‰Ÿ‚µ‚½");
                }).AddTo(this);
        }
    }
}
