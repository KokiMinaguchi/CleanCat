using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using SleepingAnimals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace VoxelDig
{
    /// <summary>
    /// 探索シーン（ダンジョンなど）の進行を管理するクラス
    ///
    /// 流れ：
    ///   1. 発掘ポイント（DigSite）をクリックで選ぶ → ブロックの横に選択カードが出る
    ///   2. 「掘る」ボタン → 掘る CSV を DigSceneData に入れて、CutsceneDirector で暗転
    ///      → 暗転中に発掘シーンを読み込む
    ///   3. 発掘シーンから戻ってくると、クリア済みのポイントは DigSite 側で発掘済みの見た目になる
    /// </summary>
    public class ExploreSceneController : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private DigSiteSelector selector;

        //[Header("1シーン構成")]
        //[Tooltip("発掘ステージを同じシーンに置くときに設定する。設定するとシーンを読み込まず、SingleSceneDigFlow が切り替えを行う")]
        //[SerializeField] private SingleSceneDigFlow singleSceneFlow;

        [Header("シーン")]
        [Tooltip("発掘シーンの名前（Build Profiles の Scene List に登録しておく）")]
        [SerializeField] private string digSceneName = "Test";

        [Header("UI：選択カード")]
        [Tooltip("発掘ポイントを選んだときにブロックの横に出るカード（Tools > VoxelDig > 選択カードを作成 で自動生成できる）")]
        [SerializeField] private DigSiteCard card;

        [Header("画面切り替え")]
        [Tooltip("暗転 → シーン読み込み を行う演出（設定されていればこちらを使う）")]
        [SerializeField] private CutsceneDirector cutsceneDirector;

        [Tooltip("ON なら、シーンに入ったときに CutsceneDirector で明転する（発掘シーンから戻ってきたとき用）")]
        [SerializeField] private bool fadeInOnStart = true;

        [Tooltip("CutsceneDirector が無いときの予備の暗転用。シーンに入ったときの明転にも使う（無くても動く）")]
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private float fadeDuration = 0.4f;

        private bool isLoading; // シーン移動中（ボタンの連打を防ぐ）

        private void Start()
        {
            // シーンをまたいで残っている CutsceneDirector（PersistentTransitionRoot）があれば、そちらを使う
            // （Inspector でこのシーンに置いたものを設定していても、それは重複として消えるため）
            cutsceneDirector = PersistentTransitionRoot.Resolve(cutsceneDirector);

            // 発掘ポイントの選択が変わったら、カードを出したり消したりする
            selector.Selected
                .Subscribe(OnSiteSelected)
                .AddTo(this);

            // 「掘る」：カードに表示中の発掘ポイントを掘りに行く
            // （ボタンを押したクリックで選択が外れても、確実に掘りに行けるようにカード側の情報を使う）
            card.DigButton.onClick.AddListener(() =>
                GoToDigSceneAsync(card.Site, destroyCancellationToken).Forget());

            // 「×」：選択を解除する（カードも消える）
            if (card.CloseButton != null)
            {
                card.CloseButton.onClick.AddListener(() =>
                {
                    if (!isLoading) selector.ClearSelection();
                });
            }

            // シーンに入ったら明転する
            if (cutsceneDirector != null)
            {
                // 発掘シーンから暗転したまま戻ってくるので、CutsceneDirector で明転する
                // 予備の黒い画面は使わないので、最初から消しておく（二重に暗くならないように）
                if (fadeOverlay != null) fadeOverlay.SetVisible(false, 0f);
                if (fadeInOnStart) cutsceneDirector.PlayFadeInAsync(token: destroyCancellationToken).Forget();
            }
            else if (fadeOverlay != null)
            {
                // CutsceneDirector が無いときは、黒い画面から明るくする
                fadeOverlay.SetVisible(true, 0f);
                fadeOverlay.SetVisible(false, fadeDuration).Forget();
            }
        }

        /// <summary>Esc キー：選択を解除する（PlayerInput の Cancel から呼ばれる）</summary>
        public void OnCancel(InputAction.CallbackContext context)
        {
            if (context.performed && !isLoading) selector.ClearSelection();
        }

        private void OnSiteSelected(DigSite site)
        {
            if (isLoading) return;

            if (site != null) card.Show(site);
            else card.Hide();
        }

        /// <summary>
        /// 発掘シーンへ移動する
        /// </summary>
        private async UniTaskVoid GoToDigSceneAsync(DigSite site, CancellationToken token)
        {
            if (isLoading) return;
            if (site == null)
            {
                Debug.LogWarning("[ExploreSceneController] 発掘ポイントが選ばれていません", this);
                return;
            }
            // 発掘済みは、もう一度掘れる設定のときだけ掘りに行ける
            if (site.IsCompleted && !site.AllowRedig) return;
            if (site.VoxelCsv == null)
            {
                Debug.LogError($"[ExploreSceneController] {site.name} に Voxel Csv が設定されていません", site);
                return;
            }
            // ---- 1シーン構成：同じシーンの発掘ステージへ切り替える ----
            //if (singleSceneFlow != null)
            //{
            //    // 切り替えの中で SuspendForDig / ResumeFromDig が呼ばれる
            //    await singleSceneFlow.EnterDigAsync(site, token);
            //    return;
            //}

            // 発掘シーンが読み込めるか（Build Profiles の Scene List に入っているか）を先に確認する
            if (!Application.CanStreamedLevelBeLoaded(digSceneName))
            {
                Debug.LogError($"[ExploreSceneController] シーン「{digSceneName}」を読み込めません。" +
                               "Dig Scene Name がシーン名と一致しているか、Build Profiles の Scene List に入っているか確認してください", this);
                return;
            }
            // 別の演出が再生中だと PlayAsync は何もせずに終わってしまうので、先に確認する
            if (cutsceneDirector != null && cutsceneDirector.IsPlaying)
            {
                Debug.LogWarning("[ExploreSceneController] 演出の再生中なので、発掘シーンへ移動できません", this);
                return;
            }

            isLoading = true;
            Debug.Log($"[ExploreSceneController] {site.DisplayName} を掘りに「{digSceneName}」へ移動します");

            // ---- 暗転する前にやること ----
            // 操作を止めて、カードを閉じる（暗転中にクリックされないように、暗転より先に行う）
            selector.enabled = false;
            card.Hide();

            // 発掘シーンに渡す情報を入れておく（戻り先は今のシーン）
            DigSceneData.SetRequest(site.VoxelCsv, site.SiteId, site.DisplayName, SceneManager.GetActiveScene().name);

            // ---- 暗転 → 暗転中にシーンを読み込む ----
            if (cutsceneDirector != null)
            {
                // CutsceneDirector：暗転 → Placements → 暗転中の処理（ここで渡すシーン読み込み）→ 明転
                // シーンの読み込みが終わるとこのシーン（CutsceneDirector も含む）は消えるので、
                // 明転は CutsceneDirector ではなく、読み込んだ先のシーン（DigSceneController）が行う
                await cutsceneDirector.PlayAsync(LoadDigSceneAsync, token);
            }
            else
            {
                // CutsceneDirector が無いときは、今までどおり黒い画面で暗転してから読み込む
                if (fadeOverlay != null)
                {
                    // 万が一フェードが終わらなくても先へ進めるよう、フェード時間 + 1秒で待つのをやめる
                    await UniTask.WhenAny(
                        fadeOverlay.SetVisible(true, fadeDuration),
                        UniTask.Delay(System.TimeSpan.FromSeconds(fadeDuration + 1f), ignoreTimeScale: true, cancellationToken: token));
                }
                await LoadDigSceneAsync(token);
            }
        }

        // ---------------------------------------------------------------
        // 1シーン構成用（SingleSceneDigFlow から呼ばれる）
        // ---------------------------------------------------------------

        /// <summary>発掘ステージへ行く前に、ダンジョンの操作を止めてカードを閉じる</summary>
        public void SuspendForDig()
        {
            isLoading = true;
            selector.ClearSelection(); // 選択中のふわふわ拡大も止める
            selector.enabled = false;
            card.Hide();
        }

        /// <summary>発掘ステージから戻ったら、ダンジョンの操作を再開する</summary>
        public void ResumeFromDig()
        {
            isLoading = false;
            selector.enabled = true;
        }

        /// <summary>
        /// 発掘シーンを読み込む（CutsceneDirector の暗転中に呼ばれる）
        /// </summary>
        private async UniTask LoadDigSceneAsync(CancellationToken ct)
        {
            // シーンの読み込み（UniTask を使うと AsyncOperation をそのまま await できる）
            // 読み込みが終わるとこのオブジェクトは消えるので、この後ろには何も書かない
            await SceneManager.LoadSceneAsync(digSceneName);
        }
    }
}
