using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HPView : MonoBehaviour
{
    /// <summary>
    /// HPゲージ
    /// </summary>
    private Slider _hpGuage;

    [SerializeField]
    private TextMeshProUGUI _hpText;

    private void Start()
    {
        _hpGuage = this.GetComponent<Slider>();
        //_hpText = this.GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// HPゲージの設定
    /// </summary>
    /// <param name="maxHP">最大HP</param>
    /// <param name="hp">残っているHP</param>
    public void SetGuage(int maxHP, float hp)
    {
        _hpGuage.value = hp / maxHP;
    }

    public void SetText(int maxHP, int hp)
    {
        _hpText.text = hp.ToString() + '/' + maxHP.ToString();
    }
}
