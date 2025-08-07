using R3;
using R3.Triggers;
using System;
using UnityEngine;

public class HPPresenter : MonoBehaviour
{
    [SerializeField] private GameObject _player;
    private HPModel _hPModel;
    private HPView _hpView;
    //private IGetHitEventProvider _getHitEventProvider;

    void Start()
    {
        _hPModel = this.GetComponent<HPModel>();
        _hpView = this.GetComponent<HPView>();

        // HP�e�L�X�g������
        _hpView.SetText(_hPModel.maxHP, _hPModel.HP.CurrentValue);

        // �G�ɓ����������̏���
        _player.OnTriggerEnter2DAsObservable()
            .Select(hit => hit.gameObject.tag)
            .Where(tag => tag == "Enemy")
            .Subscribe(_ =>
            {
                if (_player.GetComponent<SpriteAlphaBlinker>()._isInc == false)
                {
                    // �_���[�W����
                    _hPModel.GetDamage();
                    // �_�Ł����G���Ԃ�ݒ�
                    _player.GetComponent<SpriteAlphaBlinker>().BlinkOnHit();
                }
            }).AddTo(this);

        // HPModel����HP�����������Ƃ�View�֒m�点��
        _hPModel.HP
            .Subscribe(hp =>
            {
                _hpView.SetGuage(_hPModel.maxHP, hp);
                _hpView.SetText(_hPModel.maxHP, _hPModel.HP.CurrentValue);
            }).AddTo(this);
    }

    void Update()
    {
        //_hPModel.UpdateInvincibleTime();
        //if(_hPModel.InvincibleTime.CurrentValue <= 0f)
        //{
        //    //_player.GetComponent<SpriteAlphaBlinker>().BlinkOnHitEnd();
        //}
    }
}
