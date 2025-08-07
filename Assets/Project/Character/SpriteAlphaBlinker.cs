using UnityEngine;
using DG.Tweening;
using R3;
using R3.Triggers;

/// <summary>
/// 無敵時間の間、点滅させるだけにする
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAlphaBlinker : MonoBehaviour
{
    [Header("点滅する合計時間（秒）")]
    [SerializeField]
    private float _blinkDuration = 1.0f;

    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;

    private bool _isBlinking = false;
    private Sequence blinkSequence;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalColor = _spriteRenderer.color;
    }

    [Header("Blink Settings")]
    [Header("点滅時のダメージカラー")]
    [SerializeField]
    private Color _damageColor = Color.red;

    [Header("点滅回数（透明⇔表示）")]
    [SerializeField]
    private int _blinkCount = 4;

    public bool _isInc = false;

    void Start()
    {
        // 敵に当たった時の処理
        this.OnTriggerEnter2DAsObservable()
            .Select(hit => hit.gameObject.tag)
            .Where(tag => tag == "Enemy")
            .Subscribe(_ =>
            {
                //BlinkOnHit();
                
            }).AddTo(this);
    }

    public void BlinkOnHit()
    {
        if (_isBlinking) return;
        _isInc = true;
        _isBlinking = true;
        blinkSequence?.Kill(); // 途中キャンセル対策

        float singleBlinkTime = _blinkDuration / (_blinkCount * 2f); // 1フェーズの時間

        blinkSequence = DOTween.Sequence();

        // 1. 赤くなる
        blinkSequence.Append(_spriteRenderer.DOColor(_damageColor, 0.05f));

        // 2. 少し赤いまま維持
        //blinkSequence.AppendInterval(colorTime);
        blinkSequence.Append(_spriteRenderer.DOColor(Color.white, 0.05f));

        // 3. 点滅（透明⇔赤）を繰り返す
        for (int i = 0; i < _blinkCount; ++i)
        {
            blinkSequence.Append(_spriteRenderer.DOColor(Color.clear, singleBlinkTime));
            blinkSequence.Append(_spriteRenderer.DOColor(Color.white, singleBlinkTime));
        }

        // 4. 元の色に戻す
        blinkSequence.OnComplete(() =>
        {
            _spriteRenderer.color = _originalColor;
            _isBlinking = false;
            _isInc = false;
        });
    }
}
