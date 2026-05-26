using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// Core シーンに常駐し、ステージ Scene を順に追加ロードする。
    /// StageGoal からクリア通知を受けて次のステージへ進める。
    /// 各ステージ Scene と Core は Build Settings に登録しておくこと。
    ///
    /// ステージ入場 (EnterStage) の流れ：
    ///   入力ロック → SwapStageScene (旧 unload + 新 load + player teleport)
    ///   → ShowStageBanner (シーン名を 2 秒間表示) → 入力ロック解除
    /// Title シーンだけは特例で、入場後ロックを解除せず TitleController に制御を委ねる
    /// （TitleController が Enter 待機を完了して StageCleared を呼ぶサイクルで解除される）。
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        [Tooltip("ステージ Scene 名（プレイ順）。stageScenes[0] にタイトル画面を置く想定。")]
        [SerializeField] string[] stageScenes;
        [Tooltip("ステージクリア演出（メッセージ表示＋実地形の答え合わせ）の長さ [s]。この間は無敵。" +
                 "Title からの遷移時はメッセージは出ないが、答え合わせの時間として共用される。")]
        [SerializeField] float celebrationDuration = 3f;
        [Tooltip("ステージ入場時のシーン名バナー表示時間 [s]。末尾 0.5 秒で fadeout。Title では出さない。")]
        [SerializeField] float bannerDuration = 2f;

        const float BannerFadeDuration = 0.5f;

        int _current = -1;
        string _loadedScene;
        bool _allClear;
        bool _showClear;
        bool _busy;

        // バナー表示状態。OnGUI が読む。
        bool _showBanner;
        double _bannerStartTime;
        string _bannerText;

        GUIStyle _clearStyle;
        GUIStyle _bannerStyle;

        // 中央レジストリから Start で 1 回キャッシュ（Conventions.md「Start で 1 回キャッシュ」）。
        Camera _cam;

        int _cachedCullingMask;
        bool _maskCached;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _cam = SignalsightRefs.Camera;
            if (stageScenes != null && stageScenes.Length > 0)
                StartCoroutine(Boot());
        }

        /// <summary>
        /// 起動シーケンス。エディタで stageScenes のどれかを開いたまま Play すると、
        /// 後の追加ロードと重複してしまう。StageManager の管理外で先に読み込まれている
        /// ステージを剥がしてから、定義順の先頭ステージに EnterStage で入場する。
        /// </summary>
        IEnumerator Boot()
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (System.Array.IndexOf(stageScenes, s.name) < 0) continue;
                var op = SceneManager.UnloadSceneAsync(s);
                while (op != null && !op.isDone) yield return null;
            }
            yield return EnterStage(0);
        }

        /// <summary>
        /// 現ステージのクリア。クリア演出（celebrationDuration 中の World 公開）のあと
        /// 次ステージへ EnterStage で入場する（無ければ ALL CLEAR）。
        /// </summary>
        /// <param name="showText">
        /// 「STAGE CLEAR」「ALL CLEAR」テキストを表示するか。Title からの遷移時は false にする
        /// （文字表示は出さないが、World 公開＋celebrationDuration 待機＋次ステージロードは通常通り行う）。
        /// </param>
        public void StageCleared(bool showText = true)
        {
            if (_busy || _allClear || _showClear) return;
            StartCoroutine(ClearSequence(showText));
        }

        /// <summary>
        /// クリア演出 → 次ステージ遷移（または全クリア）。演出中は無敵 + World 公開で、
        /// 入力ロックや時間停止はしない（プレイヤー徘徊・敵活動・ping 全て継続可能）。
        /// Title のように事前に Locked=true で入ってきた場合は、ClearSequence が触らないことで
        /// その状態がそのまま維持される（Title 答え合わせは locked のまま、という意図的非対称）。
        /// </summary>
        IEnumerator ClearSequence(bool showText)
        {
            BeginClearCelebration(showText);

            float t = 0f;
            while (t < celebrationDuration) { t += Time.deltaTime; yield return null; }

            int next = _current + 1;
            if (next < stageScenes.Length)
            {
                EndClearCelebration();
                yield return EnterStage(next);
            }
            else
            {
                // 最終ステージ：World 公開・無敵を維持し ALL CLEAR テキストへ切替。
                // プレイヤーも敵も自由に動ける状態が永続する＝「クリア時と同じ状態の永続」。
                FinalizeAllClear(showText);
            }
        }

        /// <summary>クリア演出開始：STAGE CLEAR テキスト + 無敵 + World 公開を同時に立てる。</summary>
        void BeginClearCelebration(bool showText)
        {
            _showClear = showText;
            GameOverController.SetInvincible(true);
            RevealWorld(true);
        }

        /// <summary>クリア演出終了：テキスト消し + World 隠し + 無敵解除を同時に行う。次ステージへの遷移直前に呼ぶ。</summary>
        void EndClearCelebration()
        {
            _showClear = false;
            RevealWorld(false);
            GameOverController.SetInvincible(false);
        }

        /// <summary>
        /// 最終ステージクリア時：STAGE CLEAR テキストを消し ALL CLEAR テキストへ切替。
        /// World 公開と無敵は維持したまま永続状態に移行する（EndClearCelebration は呼ばない）。
        /// </summary>
        void FinalizeAllClear(bool showText)
        {
            _showClear = false;
            _allClear = showText;
        }

        /// <summary>
        /// ステージ入場処理：入力ロック → 世界時間停止 → scene swap → 入場バナー → 解除を順に行う。
        /// 時間停止は scene swap 開始から banner 終了まで通しで効かせる（banner は「フリーズ期間の
        /// 可視部分」という位置づけ）。これがないと scene load 完了フレームで新ステージの敵 Update が
        /// timeScale=1 のまま走り、teleport 前の旧プレイヤー位置に当たって Explode する事故が起きる。
        /// Title シーンは banner を出さず、Locked / timeScale も維持しない（TitleController が制御を引取り、
        /// Beacon の自動 scan が動く必要があるため時間は流す）。
        /// </summary>
        IEnumerator EnterStage(int index)
        {
            string scene = stageScenes[index];
            bool isTitle = scene == SignalsightNames.Scenes.Title;
            float savedTimeScale = Time.timeScale;

            BeginEntryFreeze(freezeTime: !isTitle);
            yield return SwapStageScene(index);

            if (!isTitle)
            {
                yield return ShowStageBanner(scene);
                EndEntryFreeze(savedTimeScale);
            }
            // Title: フリーズ維持で return。TitleController が引取り、
            // 後に StageCleared(showText=false) → ClearSequence → 次の EnterStage(Stage1) という
            // チェーンの末尾で解除される。
        }

        /// <summary>
        /// ステージ入場フリーズ開始：入力ロック ON、必要なら時間停止も ON。
        /// `freezeTime=true` は通常ステージ（banner 中も世界が完全停止）、
        /// `freezeTime=false` は Title（Beacon の自動 scan が動く必要があるため時間は流す）。
        /// </summary>
        void BeginEntryFreeze(bool freezeTime)
        {
            SignalsightInput.Locked = true;
            if (freezeTime) Time.timeScale = 0f;
        }

        /// <summary>ステージ入場フリーズ終了：時間 restore + 入力ロック解除．banner 表示後に呼ぶ。</summary>
        void EndEntryFreeze(float restoreTimeScale)
        {
            Time.timeScale = restoreTimeScale;
            SignalsightInput.Locked = false;
        }

        /// <summary>シーンを入れ替える：旧 unload → bus clear → 新 load → validate → player teleport。</summary>
        IEnumerator SwapStageScene(int index)
        {
            _busy = true;

            // 旧ステージをアンロード。
            if (!string.IsNullOrEmpty(_loadedScene))
            {
                var unload = SceneManager.UnloadSceneAsync(_loadedScene);
                while (unload != null && !unload.isDone) yield return null;
            }

            // 前ステージの保留中ヒットと測距点を持ち越さない。
            if (RadarSimulator.Instance != null) RadarSimulator.Instance.ClearPending();
            if (SensorBus.Instance != null) SensorBus.Instance.Clear();

            // 新ステージを追加ロード。
            string scene = stageScenes[index];
            var load = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            if (load == null)
            {
                // Build Settings 未登録 / Scene 名タイポなどで Unity が即 null を返す。
                // ここで弾かないと _loadedScene/_current が「ロード成功した体」で進み、
                // 次クリア時の unload で二重失敗、サイレント詰みになる。
                Debug.LogError($"[StageManager] Scene '{scene}' のロードに失敗。Build Settings に登録されているか確認してください。", this);
                _busy = false;
                yield break;
            }
            while (!load.isDone) yield return null;

            _current = index;
            _loadedScene = scene;

            // 必須コンポーネントの存在確認。Stage 新規作成時の貼り忘れを即座に大きく報告し、
            // 「動かない理由が分からない」型のサイレント失敗を防ぐ。
            ValidateStageSetup(scene);

            // プレイヤーをステージのスポーン地点へ移動。GameObject アクセスのみなので
            // PlayerActor.Instance ではなく中央レジストリの PlayerGameObject を使う。
            var spawn = FindFirstObjectByType<StageSpawn>();
            var playerGo = SignalsightRefs.PlayerGameObject;
            if (spawn != null && playerGo != null)
            {
                var cc = playerGo.GetComponent<CharacterController>();
                // CharacterController は有効なまま位置を直書きすると不安定なので一旦無効化。
                if (cc != null) cc.enabled = false;
                playerGo.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
                if (cc != null) cc.enabled = true;

                // カメラを「そのモードの突入時初期姿勢」へリセット（FirstPerson は spawn.rotation.y、
                // TopDownOrtho は orthoEntryAzimuth/Elevation）。前ステージの yaw/pitch を持ち越さない。
                if (CameraController.Instance != null)
                    CameraController.Instance.ResetLook();
            }

            _busy = false;
        }

        /// <summary>
        /// シーン名バナーを画面中央に表示する（bannerDuration 秒、末尾 BannerFadeDuration 秒で fade）。
        /// 時間停止 (Time.timeScale=0) は呼び出し元 EnterStage が所有しているので、ここでは
        /// 視覚表示と待機だけを担当する。EnterStage が timeScale=0 にしている以上、進行時間は
        /// unscaledTimeAsDouble で計測しないと自分が止めた時間に巻き込まれる。
        /// </summary>
        IEnumerator ShowStageBanner(string text)
        {
            _bannerText = text;
            _bannerStartTime = Time.unscaledTimeAsDouble;
            _showBanner = true;

            while (Time.unscaledTimeAsDouble - _bannerStartTime < bannerDuration)
                yield return null;

            _showBanner = false;
        }

        /// <summary>カメラのカリングマスクを切り替え、World ジオメトリの表示／非表示を行う。</summary>
        void RevealWorld(bool reveal)
        {
            if (_cam == null) return;
            if (!SignalsightNames.TryGetLayer(SignalsightNames.Layers.World, out int worldLayer)) return;

            if (reveal)
            {
                if (!_maskCached) { _cachedCullingMask = _cam.cullingMask; _maskCached = true; }
                _cam.cullingMask |= 1 << worldLayer;
            }
            else if (_maskCached)
            {
                _cam.cullingMask = _cachedCullingMask;
                _maskCached = false;
            }
        }

        /// <summary>
        /// 新しくロードした Stage に必須のコンポーネント類が揃っているかを確認し、
        /// 欠けていれば LogError で報告する（Play 自体は止めない）。Title は StageGoal / NavMesh の
        /// 代わりに TitleController を要求する。
        /// </summary>
        void ValidateStageSetup(string sceneName)
        {
            if (FindFirstObjectByType<StageSpawn>() == null)
                Debug.LogError($"[StageManager] Stage '{sceneName}' に StageSpawn が無い。プレイヤーが正しい開始位置に移動しません。", this);

            bool isTitle = sceneName == SignalsightNames.Scenes.Title;
            if (isTitle)
            {
                // Title は StageGoal を持たず TitleController が遷移を管理する。
                if (FindFirstObjectByType<TitleController>() == null)
                    Debug.LogError($"[StageManager] Title scene '{sceneName}' に TitleController が無い。Enter で Stage1 に遷移できなくなります。", this);
                // Title は敵を置かない想定なので NavMesh ベイクは不要。
                return;
            }

            if (FindFirstObjectByType<StageGoal>() == null)
                Debug.LogError($"[StageManager] Stage '{sceneName}' に StageGoal が無い。ステージクリアできません。", this);

            // NavMeshSurface コンポーネントの有無ではなく実際に三角形分割データが
            // あるかを見る（コンポーネントだけ置いて Bake し忘れたケースも検出できる）。
            if (NavMesh.CalculateTriangulation().vertices.Length == 0)
                Debug.LogError($"[StageManager] Stage '{sceneName}' に NavMesh データが無い。NavMeshSurface をベイクしてください。敵が動きません。", this);
        }

        void OnGUI()
        {
            if (_showClear || _allClear)
            {
                EnsureClearStyle();
                string msg = _allClear ? "ALL CLEAR" : "STAGE CLEAR";
                GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), msg, _clearStyle);
            }

            if (_showBanner)
            {
                EnsureBannerStyle();
                // 寿命の末尾 BannerFadeDuration を線形フェード。
                // banner 中は Time.timeScale=0 なので scaled は進まない。ShowStageBanner と同じく unscaled で読む。
                float elapsed = (float)(Time.unscaledTimeAsDouble - _bannerStartTime);
                float alpha = elapsed > bannerDuration - BannerFadeDuration
                    ? Mathf.Max(0f, (bannerDuration - elapsed) / BannerFadeDuration)
                    : 1f;

                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), _bannerText, _bannerStyle);
                GUI.color = old;
            }
        }

        void EnsureClearStyle()
        {
            if (_clearStyle != null) return;
            _clearStyle = new GUIStyle
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _clearStyle.normal.textColor = Color.cyan;
        }

        void EnsureBannerStyle()
        {
            if (_bannerStyle != null) return;
            _bannerStyle = new GUIStyle
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _bannerStyle.normal.textColor = Color.white;
        }
    }
}
