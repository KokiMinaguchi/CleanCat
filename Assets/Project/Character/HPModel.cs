using R3;
using UnityEngine;

public class HPModel : MonoBehaviour
{
    /// <summary>
    /// �ő�HP
    /// </summary>
    public readonly int maxHP = 100;

    /// <summary>
    /// �_���[�W��
    /// </summary>
    [SerializeField]
    private uint _damege;

    //public ReadOnlyReactiveProperty<float> InvincibleTime => _invincibleTime;

    //private readonly ReactiveProperty<float> _invincibleTime = new ReactiveProperty<float>();

    /// <summary>
    /// �c���Ă���HP
    /// </summary>
    public ReadOnlyReactiveProperty<int> HP => _hp;

    private readonly ReactiveProperty<int> _hp = new ReactiveProperty<int>();

    private void Start()
    {
        _hp.Value = maxHP;
        //_invincibleTime.Value = 0f;
    }
    /// <summary>
    /// �_���[�W���󂯂��Ƃ��̏���
    /// </summary>
    public void GetDamage()
    {
        //if (_hp.CurrentValue <= 0) return;
        var hp = _hp.Value -= (int)_damege;
        _hp.Value = Mathf.Clamp(hp, 0, maxHP);
        //_hp.Value -= (int)_damege;
        Debug.Log("Damage");
    }

    private void OnDestroy()
    {
        _hp.Dispose();
        //_invincibleTime.Dispose();
    }

    //public void SetInvincibleTime(float invincibleTime)
    //{
    //    _invincibleTime.Value = invincibleTime;
    //}

    //public void UpdateInvincibleTime()
    //{
    //    if(_invincibleTime.CurrentValue > 0f)
    //    {
    //        _invincibleTime.Value -= Time.deltaTime;
    //    }
    //}
}
