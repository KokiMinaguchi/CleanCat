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

        // HPテキスト初期化
        _hpView.SetText(_hPModel.maxHP, _hPModel.HP.CurrentValue);

        // 敵に当たった時の処理
        _player.OnTriggerEnter2DAsObservable()
            .Select(hit => hit.gameObject.tag)
            .Where(tag => tag == "Enemy")
            .Subscribe(_ =>
            {
                if (_player.GetComponent<SpriteAlphaBlinker>()._isInc == false)
                {
                    // ダメージ処理
                    _hPModel.GetDamage();
                    // 点滅＆無敵時間を設定
                    _player.GetComponent<SpriteAlphaBlinker>().BlinkOnHit();
                }
            }).AddTo(this);

        // HPModel内のHPが減ったことをViewへ知らせる
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
