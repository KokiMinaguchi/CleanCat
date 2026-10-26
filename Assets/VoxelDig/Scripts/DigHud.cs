using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelDig
{
    /// <summary>
    /// 画面の UI（uGUI + TextMeshPro）
    /// ・発掘率とダメージのゲージ
    /// ・今の道具
    /// ・クリア／失敗のリザルト画面
    /// DigBlock と DigController の ReactiveProperty を購読して、変化したときだけ表示を更新する
    /// </summary>
    public class DigHud : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private DigBlock block;
        [SerializeField] private DigController controller;

        [Tooltip("リトライのときに暗転・カメラの演出をしてもらう（未設定ならシーンから自動で探す。見つからなければ演出なしで作り直す）")]
        [SerializeField] private DigSceneController sceneController;

        [Header("発掘率")]
        [SerializeField] private TMP_Text progressText;
        [Tooltip("Image Type = Filled にした Image")]
        [SerializeField] private Image progressFill;

        [Header("ダメージ")]
        [SerializeField] private TMP_Text damageText;
        [Tooltip("Image Type = Filled にした Image")]
        [SerializeField] private Image damageFill;
        [SerializeField] private Color damageSafeColor = new Color(0.4f, 0.85f, 0.4f);
        [SerializeField] private Color damageDangerColor = new Color(0.95f, 0.25f, 0.2f);

        [Header("道具")]
        [SerializeField] private TMP_Text toolText;
        [Tooltip("道具を選ぶボタン（tools と同じ順番。無くても動く）")]
        [SerializeField] private Button[] toolButtons;

        [Header("リザルト")]
        [SerializeField] private CanvasGroup resultPanel;
        [SerializeField] private TMP_Text resultTitleText;
        [SerializeField] private TMP_Text resultDetailText;
        [SerializeField] private Button retryButton;

        private Tween damagePunch;

        // 今のブロックへの購読（ブロックを差し替えたときにまとめて解除する）
        private CompositeDisposable bindings;

        private void Start()
        {
            HideResultImmediate();

            // ---- 道具（DigController は差し替わらないので Start で1回だけ購読）----
            controller.CurrentToolIndex
                .Subscribe(OnToolChanged)
                .AddTo(this);

            for (int i = 0; i < toolButtons.Length; i++)
            {
                int index = i; // ラムダの中で使うためにコピー（ループ変数をそのまま使うと全部同じ値になる）
                toolButtons[i].onClick.AddListener(() => controller.SelectTool(index));
            }

            if (sceneController == null) sceneController = FindAnyObjectByType<DigSceneController>();

            // リトライ：DigSceneController があれば、シーンに入ったときと同じ演出（暗転 → 作り直し → 明転 → カメラ移動）
            // 無ければ、今までどおりその場でブロックを作り直す
            // （block はブロックが差し替わっても、その時点のものを使う）
            if (retryButton != null) retryButton.onClick.AddListener(() =>
            {
                if (sceneController != null) sceneController.Retry();
                else if (block != null) block.Generate();
            });

            // Inspector でブロックが設定されていれば、それを使う
            if (block != null && bindings == null) Bind(block);
        }

        /// <summary>
        /// 表示する対象のブロックを差し替える（DigSessionManager から呼ばれる）
        /// </summary>
        public void Bind(DigBlock newBlock)
        {
            // 前のブロックの購読を解除
            bindings?.Dispose();
            bindings = new CompositeDisposable();

            block = newBlock;
            HideResultImmediate();
            if (block == null) return;

            // ---- 発掘率 ----
            block.Progress
                .Subscribe(p =>
                {
                    if (progressText != null) progressText.SetText("発掘率 {0:0}%", p * 100f);
                    if (progressFill != null) progressFill.fillAmount = p;
                })
                .AddTo(bindings);

            // ---- ダメージ ----
            block.Damage
                .Subscribe(OnDamageChanged)
                .AddTo(bindings);

            // ---- 状態（クリア・失敗）----
            block.State
                .Subscribe(OnStateChanged)
                .AddTo(bindings);
        }

        private void OnDestroy()
        {
            bindings?.Dispose();
            damagePunch?.Kill();
            // UnityEngine.Object には ?. を使わず != null で判定する
            if (resultPanel != null)
            {
                resultPanel.DOKill();
                resultPanel.transform.DOKill();
            }
        }

        private void OnDamageChanged(int damage)
        {
            float ratio = damage / (float)Mathf.Max(1, block.MaxDamage);

            if (damageText != null) damageText.SetText("ダメージ {0:0}%", ratio * 100f);
            if (damageFill != null)
            {
                damageFill.fillAmount = ratio;
                damageFill.color = Color.Lerp(damageSafeColor, damageDangerColor, ratio);

                // ダメージが増えたらゲージをぷるっと揺らす
                if (damage > 0)
                {
                    damagePunch?.Kill(true);
                    damagePunch = damageFill.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 8);
                }
            }
        }

        private void OnToolChanged(int index)
        {
            if (toolText != null) toolText.SetText(controller.Tools[index].displayName);

            // 選ばれている道具のボタンだけ押せない見た目にして、選択中であることを示す
            for (int i = 0; i < toolButtons.Length; i++)
            {
                toolButtons[i].interactable = i != index;
            }
        }

        private void OnStateChanged(DigState state)
        {
            switch (state)
            {
                case DigState.Digging:
                    HideResultImmediate();
                    break;

                case DigState.Cleared:
                    // 成功率 ＝ 100% − ダメージの割合（お宝を傷つけなかったほど高い）
                    // 整数で計算して、小数の誤差で 95% が 94% になるようなズレを防ぐ。
                    // ダメージの割合は切り上げるので、傷が少しでもあれば 100% にはならない
                    int maxDamage = Mathf.Max(1, block.MaxDamage);
                    int damagePercent = (block.Damage.CurrentValue * 100 + maxDamage - 1) / maxDamage;
                    int successPercent = Mathf.Clamp(100 - damagePercent, 0, 100);
                    ShowResult("発掘成功！", $"ランク {block.GetRank()}\n成功率 {successPercent}%");
                    break;

                case DigState.Failed:
                    ShowResult("発掘失敗…", "お宝を傷つけすぎてしまった");
                    break;
            }
        }

        private void ShowResult(string title, string detail)
        {
            if (resultPanel == null) return;

            if (resultTitleText != null) resultTitleText.SetText(title);
            if (resultDetailText != null) resultDetailText.SetText(detail);

            resultPanel.gameObject.SetActive(true);
            resultPanel.interactable = true;
            resultPanel.blocksRaycasts = true;

            // フェードイン + 少し拡大しながら出てくる
            resultPanel.DOKill();
            resultPanel.transform.DOKill();
            resultPanel.alpha = 0f;
            resultPanel.transform.localScale = Vector3.one * 0.85f;
            resultPanel.DOFade(1f, 0.35f);
            resultPanel.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
        }

        private void HideResultImmediate()
        {
            if (resultPanel == null) return;
            resultPanel.DOKill();
            resultPanel.transform.DOKill();
            resultPanel.alpha = 0f;
            resultPanel.interactable = false;
            resultPanel.blocksRaycasts = false;
            resultPanel.gameObject.SetActive(false);
        }
    }
}