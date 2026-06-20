using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Signalsight.SensorWorld;

namespace Signalsight.TruthWorld
{
    /// <summary>
    /// Core シーンに常駐し、Title↔Field の Additive 遷移を司る。
    /// 旧 StageManager の縮退版で、Stage 進行 (Stage1→Stage2) やクリア演出・入場 freeze は持たない。
    /// Stage1/2 を試したい場合は StageManager GameObject を再有効化して stageScenes に投入する開発手順を取る。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class BootManager : MonoBehaviour
    {
        public static BootManager Instance { get; private set; }

        public enum AppScene { Unknown, Title, Field }

        [Tooltip("起動時に最初にロードするシーン（通常は Title）。")]
        [SerializeField] string initialScene = SignalsightNames.Scenes.Title;

        string _loadedScene;
        bool _busy;

        /// <summary>現在ロード中のメインシーンの種別。</summary>
        public static AppScene Current { get; private set; } = AppScene.Unknown;

        /// <summary>Title から「続きから」が選ばれた場合に true。SwapScene が消費する。</summary>
        public static bool PendingLoad { get; set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            StartCoroutine(Boot());
        }

        /// <summary>
        /// 起動時：Core 以外に既にロードされている Title/Field を剥がしてから initialScene を読み込む。
        /// Editor で Title.unity / Field.unity を直接開いたまま Play されたケースの保護を兼ねる。
        /// </summary>
        IEnumerator Boot()
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name == SignalsightNames.Scenes.Core) continue;
                if (s.name != SignalsightNames.Scenes.Title && s.name != SignalsightNames.Scenes.Field) continue;
                var op = SceneManager.UnloadSceneAsync(s);
                while (op != null && !op.isDone) yield return null;
            }
            yield return SwapScene(initialScene);
        }

        /// <summary>Title から呼び出して Field シーンへ遷移する。</summary>
        public void EnterField()
        {
            if (_busy) return;
            StartCoroutine(SwapScene(SignalsightNames.Scenes.Field));
        }

        /// <summary>Field から Title に戻る（Pause メニューなどから）。</summary>
        public void ReturnToTitle()
        {
            if (_busy) return;
            StartCoroutine(SwapScene(SignalsightNames.Scenes.Title));
        }

        /// <summary>
        /// シーンを入れ替える：旧 unload → bus clear → 新 load → validate → player teleport。
        /// 旧 StageManager.SwapStageScene の縮退版（celebration / freeze / banner なし）。
        /// 遷移中は入力をロックし、Field 完了後に解除する。Title 遷移時はロック維持。
        /// </summary>
        IEnumerator SwapScene(string scene)
        {
            _busy = true;
            // 遷移中は入力封じ。プレイヤー ping や移動入力が load 完了前に走るのを防ぐ。
            SignalsightInput.Locked = true;

            if (!string.IsNullOrEmpty(_loadedScene))
            {
                var unload = SceneManager.UnloadSceneAsync(_loadedScene);
                while (unload != null && !unload.isDone) yield return null;
            }

            // 前シーンの保留中ヒットと測距点を持ち越さない。
            if (RadarSimulator.Instance != null) RadarSimulator.Instance.ClearPending();
            if (SensorBus.Instance != null) SensorBus.Instance.Clear();

            // Field 遷移かつ PendingLoad が立っていれば、シーンロード前に Inventory + ProgressFlags を
            // 復元する。FieldPickup.Awake がフラグを見て自己 Destroy するため、必ず先に状態を作る。
            SaveData pendingData = null;
            if (PendingLoad && scene == SignalsightNames.Scenes.Field)
            {
                PendingLoad = false;
                if (SaveSystem.TryLoad(out pendingData))
                    SaveService.RestorePreSceneLoad(pendingData);
                else
                {
                    Debug.LogWarning("[BootManager] セーブ読込失敗。新規ゲームとして進行します。");
                    pendingData = null;
                }
            }

            var load = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"[BootManager] Scene '{scene}' のロードに失敗。Build Settings に登録されているか確認してください。", this);
                _busy = false;
                yield break;
            }
            while (!load.isDone) yield return null;

            _loadedScene = scene;
            UpdateCurrentFromSceneName(scene);

            ValidateScene(scene);

            // ロード経路の場合、テレポート前に PlacedBeacons（コア含む）を再生成しておく。
            // これで RespawnService がコアを発見できるようになる。
            if (pendingData != null)
                SaveService.RestorePostSceneLoad(pendingData);

            // Field ではコア周辺/StageSpawn を判断する RespawnService.Respawn() で復帰。
            // Title では StageSpawn を直接使う（コア概念がないため）。
            if (scene == SignalsightNames.Scenes.Field)
                RespawnService.Respawn();
            else
                TeleportPlayerToSpawn();

            // Title では Locked=true 維持（演出中 ping/移動を封じ TitleController の ENTER だけ通す）。
            // Field では Locked=false で通常プレイへ。
            if (scene != SignalsightNames.Scenes.Title)
                SignalsightInput.Locked = false;

            _busy = false;
        }

        void TeleportPlayerToSpawn()
        {
            var spawn = FindFirstObjectByType<StageSpawn>();
            var playerGo = SignalsightRefs.PlayerGameObject;
            if (spawn == null || playerGo == null) return;

            var cc = playerGo.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerGo.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
            if (cc != null) cc.enabled = true;

            if (CameraController.Instance != null) CameraController.Instance.ResetLook();
        }

        void ValidateScene(string scene)
        {
            bool isField = scene == SignalsightNames.Scenes.Field;
            bool isTitle = scene == SignalsightNames.Scenes.Title;

            if (FindFirstObjectByType<StageSpawn>() == null)
                Debug.LogError($"[BootManager] '{scene}' に StageSpawn が無い。プレイヤーの開始位置が決まりません。", this);

            if (isTitle && FindFirstObjectByType<TitleController>() == null)
                Debug.LogError($"[BootManager] Title '{scene}' に TitleController が無い。Enter で Field に遷移できなくなります。", this);

            if (isField && NavMesh.CalculateTriangulation().vertices.Length == 0)
                Debug.LogError($"[BootManager] Field '{scene}' に NavMesh データが無い。敵が動きません。", this);
        }

        static void UpdateCurrentFromSceneName(string scene)
        {
            if (scene == SignalsightNames.Scenes.Title) Current = AppScene.Title;
            else if (scene == SignalsightNames.Scenes.Field) Current = AppScene.Field;
            else Current = AppScene.Unknown;
        }
    }
}
