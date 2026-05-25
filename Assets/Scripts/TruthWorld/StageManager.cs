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
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        [Tooltip("ステージ Scene 名（プレイ順）。")]
        [SerializeField] string[] stageScenes;
        [Tooltip("ステージクリア演出（メッセージ表示＋実地形の答え合わせ）の長さ [s]。この間は無敵。")]
        [SerializeField] float celebrationDuration = 3f;

        int _current = -1;
        string _loadedScene;
        bool _allClear;
        bool _showClear;
        bool _busy;
        GUIStyle _style;

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
        /// ステージを剥がしてから、定義順の先頭ステージを読み込む。
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
            yield return StartCoroutine(LoadStage(0));
        }

        /// <summary>現ステージのクリア。クリア演出のあと次のステージへ進む（無ければ全クリア）。</summary>
        public void StageCleared()
        {
            if (_busy || _allClear || _showClear) return;
            StartCoroutine(ClearSequence());
        }

        /// <summary>
        /// クリア演出 → 次ステージ遷移（または全クリア）。演出中は無敵で、
        /// クリアしたステージの実地形をカメラに表示して「答え合わせ」を見せる。
        /// </summary>
        IEnumerator ClearSequence()
        {
            _showClear = true;
            GameOverController.SetInvincible(true);
            RevealWorld(true);

            float t = 0f;
            while (t < celebrationDuration) { t += Time.deltaTime; yield return null; }

            int next = _current + 1;
            if (next < stageScenes.Length)
            {
                yield return StartCoroutine(LoadStage(next));
                RevealWorld(false);
                GameOverController.SetInvincible(false);
            }
            else
            {
                // 最終ステージ。実地形を見せたまま ALL CLEAR を表示し、無敵を維持する。
                _allClear = true;
            }
            _showClear = false;
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
        /// 欠けていれば LogError で報告する（Play 自体は止めない）。Stage 作成時の
        /// セットアップ漏れがサイレントな「動かない」現象になるのを防ぐ。
        /// </summary>
        void ValidateStageSetup(string sceneName)
        {
            if (FindFirstObjectByType<StageSpawn>() == null)
                Debug.LogError($"[StageManager] Stage '{sceneName}' に StageSpawn が無い。プレイヤーが正しい開始位置に移動しません。", this);

            if (FindFirstObjectByType<StageGoal>() == null)
                Debug.LogError($"[StageManager] Stage '{sceneName}' に StageGoal が無い。ステージクリアできません。", this);

            // NavMeshSurface コンポーネントの有無ではなく実際に三角形分割データが
            // あるかを見る（コンポーネントだけ置いて Bake し忘れたケースも検出できる）。
            if (NavMesh.CalculateTriangulation().vertices.Length == 0)
                Debug.LogError($"[StageManager] Stage '{sceneName}' に NavMesh データが無い。NavMeshSurface をベイクしてください。敵が動きません。", this);
        }

        IEnumerator LoadStage(int index)
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
            }

            _busy = false;
        }

        void OnGUI()
        {
            if (!_showClear && !_allClear) return;

            if (_style == null)
            {
                _style = new GUIStyle
                {
                    fontSize = 56,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _style.normal.textColor = Color.cyan;
            }

            string msg = _allClear ? "ALL CLEAR" : "STAGE CLEAR";
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), msg, _style);
        }
    }
}
