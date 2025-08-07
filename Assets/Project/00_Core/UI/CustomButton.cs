using System;
using R3;
using R3.Triggers;
using UnityEngine;

namespace SleepingAnimals
{
    /// <summary>
    /// 汎用ボタン
    /// </summary>
    [RequireComponent(typeof(ObservableEventTrigger))]
    public class CustomButton : MonoBehaviour
    {
        #region Property

        /// <summary>
        /// ボタンのアクティブ状態を保持するReactiveProperty
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
        /// ボタンクリック時
        /// </summary>
        public Observable<Unit> OnButtonClicked => _observableEventTrigger
            .OnPointerClickAsObservable().AsUnitObservable().Where(_ => _isActiveRP.CurrentValue);

        /// <summary>
        /// ボタンを押した時
        /// </summary>
        public Observable<Unit> OnButtonPressed => _observableEventTrigger
            .OnPointerDownAsObservable().AsUnitObservable().Where(_ => _isActiveRP.CurrentValue);

        /// <summary>
        /// ボタンを離した時
        /// </summary>
        public Observable<Unit> OnButtonReleased => _observableEventTrigger
            .OnPointerUpAsObservable().AsUnitObservable().Where(_ => _isActiveRP.CurrentValue);

        /// <summary>
        /// ボタンの領域にカーソルが入った時
        /// </summary>
        public Observable<Unit> OnButtonEntered => _observableEventTrigger
            .OnPointerEnterAsObservable().AsUnitObservable().Where(_ => _isActiveRP.CurrentValue);

        /// <summary>
        /// ボタンの領域からカーソルが出た時
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
        /// ボタンのアクティブ状態を取得する
        /// </summary>
        public bool GetIsActive() => _isActiveRP.CurrentValue;
        public bool GetIsSelect() => _isSelectedRP.CurrentValue;

        /// <summary>
        /// アクティブ状態を変更する
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
