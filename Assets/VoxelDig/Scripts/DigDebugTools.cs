using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VoxelDig
{
    /// <summary>
    /// 【デバッグ用】発掘シーンで使うデバッグ機能
    /// ・指定したキー（初期値 F9）で、すべての土を一発で壊してクリアにする
    /// ・ボタンを設定すれば、画面のボタンからも同じことができる
    ///
    /// エディタと Development Build でだけ動く（製品版では自動で無効になり、ボタンも隠れる）
    /// GameSystems などに付けて、Block に DigBlock を設定するだけで使える
    /// </summary>
    public class DigDebugTools : MonoBehaviour
    {
        [Tooltip("対象のブロック")]
        [SerializeField] private DigBlock block;

        [Tooltip("全部壊すキー")]
        [SerializeField] private Key breakAllKey = Key.F9;

        [Tooltip("全部壊すボタン（無くても動く。製品版では自動で非表示になる）")]
        [SerializeField] private Button breakAllButton;

        [Tooltip("ON にすると製品版（Development Build ではないビルド）でも動く。通常は OFF のまま")]
        [SerializeField] private bool enableInReleaseBuild = false;

        private void Awake()
        {
            // Debug.isDebugBuild はエディタと Development Build のとき true になる
            bool isDebugAvailable = Debug.isDebugBuild || enableInReleaseBuild;

            if (!isDebugAvailable)
            {
                // 製品版ではボタンを隠して、このコンポーネント自体も止める
                if (breakAllButton != null) breakAllButton.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            if (breakAllButton != null) breakAllButton.onClick.AddListener(BreakAll);
        }

        private void Update()
        {
            // デバッグ機能なので、PlayerInput を通さず Input System のキーボードを直接見る
            // （Input Actions やイベント登録を増やさずに済む）
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[breakAllKey].wasPressedThisFrame) BreakAll();
        }

        /// <summary>すべての土を壊す</summary>
        public void BreakAll()
        {
            if (block == null)
            {
                Debug.LogWarning("[DigDebugTools] Block が設定されていません", this);
                return;
            }
            block.DebugBreakAll();
        }
    }
}
