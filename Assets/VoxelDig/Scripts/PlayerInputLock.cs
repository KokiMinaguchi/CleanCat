using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SleepingAnimals
{
    /// <summary>
    /// プレイヤーの操作を止める。
    ///
    /// 複数の演出が同時に止めようとしても壊れないよう、
    /// 「止めた数」を数えて、全部が解除されたときだけ再開する。
    /// </summary>
    public class PlayerInputLock : MonoBehaviour
    {
        /// <summary>他のスクリプトから参照するための入り口。</summary>
        public static PlayerInputLock Instance { get; private set; }

        [Tooltip("止める対象。未設定なら同じオブジェクトから探す")]
        [SerializeField] private PlayerInput playerInput;

        // 今いくつの処理がロックを掛けているか
        private readonly ReactiveProperty<int> lockCount = new ReactiveProperty<int>(0);

        /// <summary>操作が止まっているか。UI の表示切り替えなどから購読できる</summary>
        public ReadOnlyReactiveProperty<int> LockCount => lockCount;

        /// <summary>今、操作が止まっているか</summary>
        public bool IsLocked => lockCount.Value > 0;

        private void Awake()
        {
            Instance = this;

            if (playerInput == null) playerInput = GetComponent<PlayerInput>();

            // ロック数が 0 と 1以上 を行き来したときだけ、実際の停止・再開を行う
            lockCount
                .Select(count => count > 0)
                .DistinctUntilChanged()
                .Subscribe(this, (locked, self) => self.ApplyLock(locked))
                .AddTo(this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            lockCount.Dispose();
        }

        /// <summary>操作を止める。解除するまで止まったまま。</summary>
        public void Lock()
        {
            lockCount.Value++;
        }

        /// <summary>止めていたぶんを1つ解除する。</summary>
        public void Unlock()
        {
            lockCount.Value = Mathf.Max(0, lockCount.Value - 1);
        }

        /// <summary>何が起きても確実に解除する。演出が例外で落ちたときの保険。</summary>
        public void ForceUnlockAll()
        {
            lockCount.Value = 0;
        }

        private void ApplyLock(bool locked)
        {
            if (playerInput == null) return;

            if (locked)
            {
                // 押しっぱなしの入力が残らないよう、いったん無効化する
                playerInput.DeactivateInput();
            }
            else
            {
                playerInput.ActivateInput();
            }
        }
    }
}
