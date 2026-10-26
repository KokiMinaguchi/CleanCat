using R3;
using UnityEngine;

namespace SleepingAnimals
{
    [RequireComponent(typeof(CustomButton))]
    public class SettingButton : BaseButton
    {
        //[SerializeField]
        //private GameObject settingPanel;

        [SerializeField]
        private SettingsMenuUI settingsMenuUI;

        protected override void Start()
        {
            base.Start();

            _button.OnButtonPressed.AsObservable()
                .Subscribe(_ =>
                {
                    Debug.Log("İ’èƒ{ƒ^ƒ“‚ğ‰Ÿ‚µ‚½");
                    //settingPanel.SetActive(true);
                    settingsMenuUI.Open();
                }).AddTo(this);
        }
    }
}