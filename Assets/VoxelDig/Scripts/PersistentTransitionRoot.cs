using UnityEngine;

namespace SleepingAnimals
{
    /// <summary>
    /// 画面切り替えの演出（CutsceneDirector と RuleTransition）を、シーンをまたいで1つだけ残すためのコンポーネント
    ///
    /// 使い方：
    ///   1. 空の GameObject「TransitionSystem」を作り、このコンポーネントを付ける
    ///   2. その GameObject（または子）に CutsceneDirector と RuleTransition（暗転用の Canvas）を入れる
    ///   3. プレハブにして、タイトル・ダンジョン・Test など全部のシーンに置く
    ///
    /// 動き：
    ///   ・最初に読み込まれたシーンの TransitionSystem だけが残り（DontDestroyOnLoad）、シーンを切り替えても消えない
    ///   ・後から読み込んだシーンの TransitionSystem は、GameObject ごと自動で消える（2つに増えない）
    ///   → どのシーンから再生しても動き、シーンを行き来しても1つのまま
    ///
    /// ほかのスクリプトからは PersistentTransitionRoot.Director で、残っている CutsceneDirector を使える
    /// </summary>
    // ほかのスクリプトより先に動かして、重複を Awake の時点で片付ける
    [DefaultExecutionOrder(-10000)]
    public class PersistentTransitionRoot : SingletonMonoBehaviour<PersistentTransitionRoot>
    {
        [Tooltip("残しておく CutsceneDirector（未設定なら、この GameObject と子から自動で探す）")]
        [SerializeField] private CutsceneDirector director;

        // シーンをまたいで残っている本物（重複して消える方はここに入らない）
        private static PersistentTransitionRoot current;

        /// <summary>
        /// シーンをまたいで残っている CutsceneDirector（まだ無ければ null）
        /// </summary>
        public static CutsceneDirector Director => current != null ? current.director : null;

        /// <summary>
        /// 残っている CutsceneDirector があればそれを、無ければ fallback を返す
        /// （シーンに置いた CutsceneDirector を Inspector で設定しているスクリプトでも、
        ///   重複して消える方ではなく、残っている方を使うようにするため）
        /// </summary>
        public static CutsceneDirector Resolve(CutsceneDirector fallback)
        {
            return Director != null ? Director : fallback;
        }

        protected override void Awake()
        {
            // SingletonMonoBehaviour の仕組みで、すでに本物があるか調べる
            // （重複していたら、ベースクラスがこのコンポーネントを消して false を返す）
            if (!CheckInstance())
            {
                // コンポーネントだけでなく、GameObject ごと消す
                // （暗転用の Canvas や CutsceneDirector が2つに増えないように）
                // 先に非表示にして、同じ GameObject と子の Awake / Start（RuleTransition の初期化など）を動かさないようにする
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            if (director == null) director = GetComponentInChildren<CutsceneDirector>(true);

            // DontDestroyOnLoad は一番上の階層のオブジェクトにしか効かないので、親から外す
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            current = this;
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
