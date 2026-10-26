using System.Collections.Generic;
using R3;
using UnityEngine;

namespace VoxelDig
{
    /// <summary>
    /// 掘ったときの破片パーティクルと効果音
    /// パーティクルはコードで作るので、Inspector では素材（マテリアル・音）を入れるだけで良い
    /// 破片はボクセルと同じ頂点カラーのマテリアルを使い、壊れたマスの色の小さなキューブを飛ばす
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class DigEffects : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private DigBlock block;

        [Tooltip("破片に使うマテリアル（ボクセルと同じ VoxelVertexColorLit で OK）")]
        [SerializeField] private Material debrisMaterial;

        [Header("破片")]
        [Tooltip("破片1個の大きさ（1ボクセルの大きさ × この値）。ブロックの大きさが変わっても見た目が揃う")]
        [SerializeField] private float debrisSizeRatio = 0.8f;

        [Tooltip("壊れたマス1個あたりに出す破片の数")]
        [SerializeField] private int debrisPerVoxel = 2;

        [Tooltip("1回掘ったときに出す破片の上限（ハンマーで大量に壊れたとき用）")]
        [SerializeField] private int maxDebrisPerDig = 40;

        [Tooltip("クリア演出で1回に出す破片の上限")]
        [SerializeField] private int maxDebrisPerReveal = 80;

        [SerializeField] private float debrisSpeed = 2.5f;
        [SerializeField] private float debrisGravity = 1.5f;

        [Header("効果音（未設定でも動く）")]
        [SerializeField] private AudioClip digClip;      // 土が崩れた
        [SerializeField] private AudioClip hardHitClip;  // 石を叩いた（まだ壊れない）
        [SerializeField] private AudioClip treasureHitClip; // お宝を叩いてしまった
        [SerializeField] private AudioClip revealClip;   // クリア演出の崩れる音
        [SerializeField] private AudioClip clearClip;
        [SerializeField] private AudioClip failClip;

        [Tooltip("毎回少しピッチを変えて単調にならないようにする幅")]
        [SerializeField] private float pitchRandom = 0.1f;

        [Tooltip("同じ音を続けて鳴らすときの最短の間隔（秒）。ドリルのように速く掘る道具で、音が重なってうるさくならないように")]
        [SerializeField] private float minClipInterval = 0.07f;

        [Header("ドリルの音（押している間ずっと鳴る音）")]
        [Tooltip("鳴り始め・止まるときのフェードの時間（秒）。プツッと切れないように")]
        [SerializeField] private float loopFadeTime = 0.08f;

        [Range(0f, 1f)]
        [SerializeField] private float loopVolume = 0.6f;

        private ParticleSystem debris;
        private AudioSource audioSource;
        private AudioSource loopSource; // ドリルの「ギュイーン」専用（ループ再生）
        private bool loopShouldPlay;

        // 音ごとに、最後に鳴らした時刻（短い間隔で何度も鳴らさないため）
        private readonly Dictionary<AudioClip, float> lastPlayedTimes = new Dictionary<AudioClip, float>();

        // 今のブロックへの購読（ブロックを差し替えたときにまとめて解除する）
        private CompositeDisposable bindings;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            debris = CreateDebrisParticleSystem();

            // ループ音用の AudioSource を、同じ GameObject にもう1つ作る
            // （PlayOneShot の効果音と分けておくと、ループだけを止めたり音量を変えたりできる）
            // 出力先（AudioMixer のグループ）や 3D 設定は、元の AudioSource と同じにする
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.loop = true;
            loopSource.volume = 0f;
            loopSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            loopSource.spatialBlend = audioSource.spatialBlend;
        }

        private void Update()
        {
            // ループ音の音量を、鳴らす / 止める に向けて少しずつ変える（フェード）
            if (loopSource == null || loopSource.clip == null) return;

            float target = loopShouldPlay ? loopVolume : 0f;
            float speed = loopFadeTime > 0f ? loopVolume / loopFadeTime : float.MaxValue;
            loopSource.volume = Mathf.MoveTowards(loopSource.volume, target, speed * Time.unscaledDeltaTime);

            if (loopShouldPlay && !loopSource.isPlaying) loopSource.Play();
            else if (!loopShouldPlay && loopSource.isPlaying && loopSource.volume <= 0f) loopSource.Stop();
        }

        /// <summary>
        /// 押している間ずっと鳴る道具の音（ドリルなど）を鳴らす / 止める
        /// DigController から毎フレーム呼ばれる
        /// </summary>
        /// <param name="clip">鳴らす音（null なら止める）</param>
        /// <param name="play">true で鳴らす、false で止める</param>
        public void SetToolLoop(AudioClip clip, bool play)
        {
            if (loopSource == null) return;

            // 道具を持ち替えて音が変わったら、すぐに切り替える
            if (clip != null && loopSource.clip != clip)
            {
                loopSource.Stop();
                loopSource.clip = clip;
                loopSource.volume = 0f;
            }

            loopShouldPlay = play && clip != null;
        }

        private void Start()
        {
            // Inspector でブロックが設定されていれば、それを使う
            if (block != null && bindings == null) Bind(block);
        }

        private void OnDestroy()
        {
            bindings?.Dispose();
        }

        /// <summary>
        /// エフェクトを出す対象のブロックを差し替える（DigSessionManager から呼ばれる）
        /// </summary>
        public void Bind(DigBlock newBlock)
        {
            // 前のブロックの購読を解除
            bindings?.Dispose();
            bindings = new CompositeDisposable();

            block = newBlock;
            if (block == null) return;

            // クリア演出で土が崩れたら破片を出す
            block.RevealBroken
                .Subscribe(list =>
                {
                    EmitDebris(list, maxDebrisPerReveal, true);
                    PlayClip(revealClip, 0.5f);
                })
                .AddTo(bindings);

            // クリア・失敗の音（Skip(1) で最初の現在値の通知は無視する）
            block.State
                .Skip(1)
                .Subscribe(state =>
                {
                    if (state == DigState.Cleared) PlayClip(clearClip);
                    else if (state == DigState.Failed) PlayClip(failClip);
                })
                .AddTo(bindings);
        }

        /// <summary>掘った結果に応じてエフェクトと音を出す</summary>
        /// <param name="overrideDigClip">道具ごとの「掘った音」（null なら Dig Clip を使う）</param>
        public void PlayDig(DigResult result, AudioClip overrideDigClip = null)
        {
            if (result.broken.Count > 0)
            {
                EmitDebris(result.broken, maxDebrisPerDig, false);
                PlayClip(overrideDigClip != null ? overrideDigClip : digClip);
            }

            if (result.hitTreasure) PlayClip(treasureHitClip);
            else if (result.hitHard && result.broken.Count == 0) PlayClip(hardHitClip);
        }

        // ---------------------------------------------------------------
        // パーティクル
        // ---------------------------------------------------------------

        /// <summary>
        /// 壊れたボクセルの位置から、その色の破片を飛ばす
        /// </summary>
        private void EmitDebris(IReadOnlyList<BrokenVoxel> list, int maxCount, bool spreadOutward)
        {
            if (list.Count == 0) return;

            int total = Mathf.Min(list.Count * debrisPerVoxel, maxCount);
            // 数が多いときは、間引いて均等に選ぶ
            float step = list.Count * debrisPerVoxel / (float)total;

            // 破片の大きさはボクセルの大きさに合わせる
            float debrisSize = (block != null ? block.VoxelSize : 0.1f) * debrisSizeRatio;

            var emitParams = new ParticleSystem.EmitParams();
            for (int n = 0; n < total; n++)
            {
                BrokenVoxel v = list[Mathf.Min(list.Count - 1, (int)(n * step) / debrisPerVoxel)];

                Vector3 velocity = Random.insideUnitSphere * debrisSpeed + Vector3.up * debrisSpeed * 0.8f;
                if (spreadOutward && block != null)
                {
                    // クリア演出ではブロックの外側へはじけるように飛ばす
                    velocity += (v.worldPosition - block.WorldCenter).normalized * debrisSpeed;
                }

                emitParams.position = v.worldPosition + Random.insideUnitSphere * debrisSize;
                emitParams.velocity = velocity;
                emitParams.startColor = v.color;
                emitParams.startSize = debrisSize * Random.Range(0.6f, 1.2f);
                emitParams.rotation3D = Random.rotation.eulerAngles;
                emitParams.startLifetime = Random.Range(0.5f, 1.0f);
                debris.Emit(emitParams, 1);
            }
        }

        /// <summary>
        /// 破片用のパーティクルシステムをコードで作る
        /// </summary>
        private ParticleSystem CreateDebrisParticleSystem()
        {
            var go = new GameObject("DebrisParticles");
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // 発生後はワールドに残る
            main.gravityModifier = debrisGravity;
            main.maxParticles = 2000;
            main.startRotation3D = true;

            // 自動では出さず、Emit で出したいときだけ出す
            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = false;

            // だんだん小さくなって消える
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // くるくる回る
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.separateAxes = true;
            rotationOverLifetime.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rotationOverLifetime.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            // 見た目はキューブのメッシュ（色はパーティクルの頂点カラーで付く）
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.alignment = ParticleSystemRenderSpace.World;
            if (debrisMaterial != null) renderer.sharedMaterial = debrisMaterial;

            ps.Play();
            return ps;
        }

        // ---------------------------------------------------------------
        // 効果音
        // ---------------------------------------------------------------

        private void PlayClip(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;

            // 直前に同じ音を鳴らしたばかりなら鳴らさない（ドリルで1秒に20回掘っても、音が重なりすぎない）
            float now = Time.unscaledTime;
            if (lastPlayedTimes.TryGetValue(clip, out float last) && now - last < minClipInterval) return;
            lastPlayedTimes[clip] = now;

            audioSource.pitch = 1f + Random.Range(-pitchRandom, pitchRandom);
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
