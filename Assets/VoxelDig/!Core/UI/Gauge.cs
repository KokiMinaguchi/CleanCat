using UnityEngine;
using UnityEngine.UI;
using R3;

namespace SleepingAnimals
{
    /// <summary>
    /// 汎用ゲージ（縦横、円形に対応）
    /// </summary>
    public class Gauge : MonoBehaviour
    {
        [SerializeField]
        private Image _hpBar;  // UI (fillAmountで操作)

        [SerializeField]
        [Header("最大HP")]
        [Range(1f, 100f)]
        private float maxHP = 100f;


        // 現在のHP
        public ReadOnlyReactiveProperty<float> CurrentHP;

        private ReactiveProperty<float> _currentHP = new(0);

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            // HPが変化したらUI更新
            _currentHP
                .Select(hp => 1f - hp / maxHP) // 0.0～1.0に正規化
                .Subscribe(rate =>
                {
                    _hpBar.fillAmount = rate;
                    //Debug.Log(_currentHP.CurrentValue);
                })
                .AddTo(this);

            // 初期値セット
            _currentHP.Value = maxHP;
        }

        /// <summary>
        /// ダメージ処理
        /// </summary>
        /// <param name="damage">ダメージ量</param>
        public void TakeDamage(float damage)
        {
            _currentHP.Value = Mathf.Max(_currentHP.Value - damage, 0);
        }

        /// <summary>
        /// 回復処理
        /// </summary>
        /// <param name="heal">回復量</param>
        public void Heal(float heal)
        {
            _currentHP.Value = Mathf.Min(_currentHP.Value + heal, maxHP);
        }
    }
}