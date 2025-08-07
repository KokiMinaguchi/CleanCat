using System;
using R3;
using R3.Triggers;
using UnityEngine;

namespace SleepingAnimals
{
    /// <summary>
    /// �ėp�{�^��
    /// </summary>
    [RequireComponent(typeof(ObservableEventTrigger))]
    public class CustomButton : MonoBehaviour
    {
        #region Property

        /// <summary>
        /// �{�^���̃A�N�e�B�u��Ԃ�ێ�����ReactiveProperty
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsActiveRP => _isActiveRP;
        public ReadOnlyReactiveProperty<bool> IsSelectedRP => _isSelectedRP;

        #endregion

        #region Field

        private readonly ReactiveProperty<bool> _isActiveRP = new(true);
        private readonly ReactiveProperty<bool> _isSelectedRP = new(false);
        private ObservableEventTrigger _observableEventTrigger;

        #endregion

        #region Unity

        /// <summary>
        /// �{�^���N���b�N��
        /// </summary>
        public Observable<Unit> OnButtonClicked => _observableEventTrigger
            .OnPointerClickAsObservable().AsUnitObservable().Where(_ => _isActiveRP.CurrentValue);

        /// <summary>
        /// �{�^������������
        /// </summary>
        public Observable<Unit> OnButtonPressed => _observableEventTrigger
            .OnPointerDownAsObservable().AsUnitObservable().Where(_ => _isActiveRP.CurrentValue);

        /// <summary>
        /// �{�^���𗣂�����
        /// </summary>
        public Observable<Unit> OnButtonReleased => _observableEventTrigger
            .OnPointerUpAsObservable().AsUnitObservable().Where(_ => _isActiveRP.CurrentValue);

        /// <summary>
        /// �{�^���̗̈�ɃJ�[�\������������
        /// </summary>
        public Observable<Unit> OnButtonEntered => _observableEventTrigger
            .OnPointerEnterAsObservable().AsUnitObservable().Where(_ => _isActiveRP.CurrentValue);

        /// <summary>
        /// �{�^���̗̈悩��J�[�\�����o����
        /// </summary>
        public Observable<Unit> OnButtonExited => _observableEventTrigger
            .OnPointerExitAsObservable().AsUnitObservable().Where(_ => _isActiveRP.CurrentValue);

        public Observable<Unit> OnButtonSelected => _observableEventTrigger
            .OnSelectAsObservable().AsUnitObservable().Where(_ => _isSelectedRP.CurrentValue);


        protected virtual void OnDestroy()
        {
            _isActiveRP.Dispose();
            _isSelectedRP.Dispose();
        }

        protected virtual void Awake()
        {
            _observableEventTrigger = GetComponent<ObservableEventTrigger>();
        }

        #endregion

        #region Method

        /// <summary>
        /// �{�^���̃A�N�e�B�u��Ԃ��擾����
        /// </summary>
        public bool GetIsActive() => _isActiveRP.CurrentValue;
        public bool GetIsSelect() => _isSelectedRP.CurrentValue;

        /// <summary>
        /// �A�N�e�B�u��Ԃ�ύX����
        /// </summary>
        public void SetActive(bool isActive)
        {
            _isActiveRP.Value = isActive;
        }
        public void SetSelected(bool isSelected)
        {
            _isSelectedRP.Value = isSelected;
        }

        #endregion
    }
}
