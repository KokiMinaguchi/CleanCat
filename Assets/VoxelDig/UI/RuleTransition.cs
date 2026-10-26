using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SleepingAnimals
{
    /// <summary>
    /// ルール画像を使った画面転換。
    ///
    /// ScreenFader と同じメソッド名にしてあるので、
    /// CutsceneDirector の参照をこちらに差し替えればそのまま動く。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class RuleTransition : MonoBehaviour
    {
        [Tooltip("覆うまでの時間（秒）")]
        [SerializeField] private float fadeOutDuration = 0.6f;

        [Tooltip("開くまでの時間（秒）")]
        [SerializeField] private float fadeInDuration = 0.7f;

        [Tooltip("進み方。終わりを緩やかにすると自然")]
        [SerializeField]
        private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("見た目")]
        [Tooltip("順番を決めるグレースケール画像")]
        [SerializeField] private Texture ruleTexture;

        [Tooltip("境界のぼかし。小さいほど硬い切り替わり")]
        [Range(0.001f, 0.5f)]
        [SerializeField] private float softness = 0.05f;

        [Tooltip("開くときはルールを反転させる。閉じた順と逆に開く")]
        [SerializeField] private bool invertOnFadeIn = true;

        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int RuleTexId = Shader.PropertyToID("_RuleTex");
        private static readonly int InvertId = Shader.PropertyToID("_InvertRule");
        private const string InvertKeyword = "_INVERT_RULE";

        private Image image;
        private Material instanceMaterial;

        /// <summary>今、画面が覆われているか</summary>
        public bool IsCovered => instanceMaterial != null
                                 && instanceMaterial.GetFloat(CutoffId) >= 0.999f;

        private void Awake()
        {
            image = GetComponent<Image>();

            // material を呼ぶと複製されるので、他の UI に影響しない
            instanceMaterial = image.material = new Material(image.material);

            if (ruleTexture != null) instanceMaterial.SetTexture(RuleTexId, ruleTexture);
            instanceMaterial.SetFloat(SoftnessId, softness);

            SetImmediate(0f);
        }

        private void OnDestroy()
        {
            // 複製したマテリアルは自分で片付ける
            if (instanceMaterial != null) Destroy(instanceMaterial);
        }

        /// <summary>覆う。</summary>
        public UniTask FadeOutAsync(CancellationToken token)
        {
            image.raycastTarget = true;
            SetInvert(false);
            return AnimateAsync(0f, 1f, fadeOutDuration, token);
        }

        /// <summary>開く。</summary>
        public async UniTask FadeInAsync(CancellationToken token)
        {
            // 反転すると、閉じた順と逆の順で開く。往復が自然に見える
            SetInvert(invertOnFadeIn);

            await AnimateAsync(1f, 0f, fadeInDuration, token);
            image.raycastTarget = false;
        }

        /// <summary>即座に指定の状態にする。0で全開、1で全閉。</summary>
        public void SetImmediate(float cutoff)
        {
            float value = Mathf.Clamp01(cutoff);
            instanceMaterial.SetFloat(CutoffId, value);
            image.raycastTarget = value > 0.001f;
        }

        /// <summary>ルール画像を差し替える。場面ごとに変えたいとき。</summary>
        public void SetRuleTexture(Texture texture)
        {
            ruleTexture = texture;
            if (instanceMaterial != null) instanceMaterial.SetTexture(RuleTexId, texture);
        }

        private void SetInvert(bool invert)
        {
            instanceMaterial.SetFloat(InvertId, invert ? 1f : 0f);

            // shader_feature はキーワードで切り替わるので、両方設定する
            if (invert) instanceMaterial.EnableKeyword(InvertKeyword);
            else instanceMaterial.DisableKeyword(InvertKeyword);
        }

        private async UniTask AnimateAsync(float from, float to, float duration,
                                           CancellationToken token)
        {
            if (duration <= 0.001f)
            {
                instanceMaterial.SetFloat(CutoffId, to);
                return;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();

                // 演出中に時間が止まっていても進むようにする
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                instanceMaterial.SetFloat(CutoffId, Mathf.Lerp(from, to, curve.Evaluate(t)));

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            instanceMaterial.SetFloat(CutoffId, to);
        }
    }
}
