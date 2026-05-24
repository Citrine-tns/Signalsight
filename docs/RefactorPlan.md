# リファクタ計画メモ

全ファイル精読の結果見つかった改善候補。会話の脱線で計画が流れないよう、ここに保存しておく。
進行中は対応した項目に `[x]` をつけ、不要と判断したものは取り消し線でログを残す。

各項目はファイル単独で読めるよう、根拠の行番号と背景を入れている。

---

## A. バグの温床 / 正しさに関わる問題

### [x] A-1. EnemyAI のスキャン周期：剰余を捨てている
- 場所：[EnemyAI.cs:110-113](../Assets/Scripts/TruthWorld/EnemyAI.cs#L110-L113)
- 現状：`_detectTimer >= detectInterval` のあと `_detectTimer = 0f;` で剰余を切り捨てている。
- 問題：Beacon は `_timer -= pulseInterval;` で剰余保持しており挙動が不一致。低 fps 時にスキャン頻度が実効で遅くなる。
- 修正：`_detectTimer -= detectInterval;` に置き換える（1 行）。

### [x] A-2. EnemyAI.Update の Vector3.Distance 二重計算
- 場所：[EnemyAI.cs:127-129, 216](../Assets/Scripts/TruthWorld/EnemyAI.cs#L127-L129)
- 現状：attackRange 判定と blastRadius 判定で `Vector3.Distance(transform.position, player.position)` を 2 回呼ぶ。
- 問題：sqrt が 2 回走る。StageGoal/Beacon は sqrMagnitude を使っており不揃い。
- 修正：`(player.position - transform.position).sqrMagnitude` を 1 回計算し、`attackRange*attackRange` / `blastRadius*blastRadius` と比較。

### [x] A-3. worldMask = ~0 デフォルトの脆さ
- 場所：[EnemyAI.cs:23](../Assets/Scripts/TruthWorld/EnemyAI.cs#L23), [RadarSimulator.cs:31](../Assets/Scripts/TruthWorld/RadarSimulator.cs#L31)
- 現状：デフォルトが「全レイヤ」。Tooltip は「World レイヤだけを含めること」と書いてあるのに矛盾。
- 問題：Inspector で設定し忘れた瞬間に自己ヒットや誤検知の原因になる。
- 修正案：Awake で `LayerMask.NameToLayer("World")` を読み安全側へ寄せる、または未設定時に警告ログ。

### [x] A-4. StageManager._busy の戻り忘れリスク
- 場所：[StageManager.cs:121-156](../Assets/Scripts/TruthWorld/StageManager.cs#L121-L156)
- 問題：`LoadSceneAsync` が null を返す（シーン未登録など）と while ループが素通りし、`_busy` は false に戻るがロード失敗が検出されない。
- 修正：`if (load == null) { Debug.LogError(...); _busy = false; yield break; }` を入れる。

### [x] A-5. GameOverController のシングルトンが規約と乖離
- 場所：[GameOverController.cs:14-42](../Assets/Scripts/TruthWorld/GameOverController.cs#L14-L42)
- 問題：Conventions.md の `Instance` プロパティ + Awake 登録パターンに従っていない。Trigger() で lazy 生成のため、誤って Scene に手置きすると二重生成され GUI が二重描画される。
- 修正案：Awake を足して規約に揃える、もしくは Trigger() 冒頭で `FindFirstObjectByType<GameOverController>()` フォールバックを 1 回入れる。

### [x] A-6. Beacon.SetLayerRecursive が子要素を一律巻き込む
- 場所：[Beacon.cs:80-85](../Assets/Scripts/TruthWorld/Beacon.cs#L80-L85)
- 問題：Beacon 下に意図的に別レイヤの子（エフェクト等）を置きたくなった瞬間に破綻する。
- 優先度：低（現状破綻していない、将来のための予防）。

### [x] A-7. PlayerActor & Beacon の ScanProfile.emitterRadius がデフォルト 0
- 場所：[PlayerActor.cs:15-20](../Assets/Scripts/TruthWorld/PlayerActor.cs#L15-L20), [Beacon.cs:18-23](../Assets/Scripts/TruthWorld/Beacon.cs#L18-L23)
- 問題：`emitterRadius` 初期化漏れで 0 になり、自己ヒットで点群が歪む潜在リスク。EnemyAI のみ 0.3f を持つので片手落ち。
- 修正：両方の `ScanProfile` 初期化に `emitterRadius = 0.3f`（か適切な値）を追加。Inspector でセット済みの可能性が高いが、シリアライズ値の確認も。

### [x] A-8. RadarSimulator.MaxRaysPerSlab = 4096 の stackalloc サイズ過大
- 場所：[RadarSimulator.cs:46, 121-122](../Assets/Scripts/TruthWorld/RadarSimulator.cs#L46)
- 問題：最大 32KB のスタック消費。実害は無いがデフォルト 240 本に対して過剰。
- 修正：上限を 1024 程度に絞るか、超えそうな値を inspector で受け取った時点で warning。

### [x] A-9. RadarSimulator.OnDestroy と ClearPending のロジック重複
- 場所：[RadarSimulator.cs:79-103](../Assets/Scripts/TruthWorld/RadarSimulator.cs#L79-L103)
- 問題：in-flight drain & dispose が 2 箇所に重複。
- 修正：`OnDestroy` から `ClearPending()` を呼ぶ。

### [x] A-10. EnemyAI.DetectPlayer の近距離 distance ≤ 0 で誤判定
- 場所：[EnemyAI.cs:188-199](../Assets/Scripts/TruthWorld/EnemyAI.cs#L188-L199)
- 現状：
  ```csharp
  if (d < 1e-3f) return true;
  Vector3 dir = to / d;
  float r = scanProfile.emitterRadius;
  return !Physics.Raycast(transform.position + dir * r, dir, d - r, ...);
  ```
- 問題：`d ∈ (1e-3f, r]`（プレイヤーが敵のすぐ手前。`r` はデフォルト 0.3）のとき、Raycast の distance 引数が `d - r ≤ 0` になる。`Physics.Raycast` は distance ≤ 0 を「無効」扱いするため、**近接時にむしろ遮蔽されたと判定されうる**。攻撃距離 `attackRange = 2f` より内側 0.3 m 帯で検知抜けが起き、敵が爆発トリガを取り損ねる温床。
- 修正：早期 return の閾値を `if (d <= r) return true;` に緩める（emitter 半径より内側は遮蔽考慮不要）。

### [x] A-11. CameraController.CaptureOrthoBase 失敗時のサイレント固定
- 場所：[CameraController.cs:97-115, 202-224](../Assets/Scripts/TruthWorld/CameraController.cs#L97-L115)
- 問題：`PlayerActor.Instance == null` か Player と Camera がほぼ同位置だと `_orthoBaseCaptured = false` のまま。`LateUpdate` の TopDownOrtho 分岐は break するだけ。Debug.LogError は出るが、ゲームは「カメラが Scene 初期姿勢のまま、yaw/pitch 入力に一切反応しない」状態で進行する。
- 修正案：`_orthoDistance` のフォールバック既定値（例：シリアライズ可能な `defaultDistance = 10f`）を持ち、capture 失敗時はそれで初期化して動かす。または `Start` で再試行のキューに回す。

### [ ] A-12. RadarSimulator._inFlight に上限が無い
- 場所：[RadarSimulator.cs:70-71, 161-170](../Assets/Scripts/TruthWorld/RadarSimulator.cs#L70-L71)
- 問題：`Scan` は無条件に積み、`ProcessCompletedBatches` で完了したものだけ回収。worker が詰まった場合や、ステージ切替直前で大量に Scan が呼ばれた場合、`NativeArray<RaycastCommand>` がアンロード前に蓄積する可能性。`ClearPending` でカバーできるが、シーン側の Scan 暴走（敵 100 体配置など）には未防御。
- 修正案：`_inFlight.Count >= MaxInFlight` のとき新規 Scan を warn してスキップ、または最古を完了待ち drop。

---

## B. 設計・保守性の問題

### [ ] B-1. PlayerActor を GetComponent で判別する箇所が散在
- 場所：[TutorialHintTrigger.cs:66](../Assets/Scripts/TruthWorld/TutorialHintTrigger.cs#L66), [EnemyActivator.cs:23](../Assets/Scripts/TruthWorld/EnemyActivator.cs#L23)
- 改善案：`PlayerActor.IsPlayer(Collider)` 静的ヘルパに集約し、`other.gameObject == PlayerActor.Instance.gameObject` での比較に統一。

### [ ] B-2. Camera.main の散在
- 場所：[PingGauge.cs:42](../Assets/Scripts/TruthWorld/PingGauge.cs#L42), [StageManager.cs:104](../Assets/Scripts/TruthWorld/StageManager.cs#L104), [PlayerController.cs:23](../Assets/Scripts/TruthWorld/PlayerController.cs#L23), [RadarImageRenderer.cs:33](../Assets/Scripts/Reconstruction/RadarImageRenderer.cs#L33)
- 問題：PingGauge.OnGUI は毎フレーム複数回呼ばれる。
- 修正：フィールドキャッシュ、または `CameraRefs` 系シングルトンに集約。

### [ ] B-3. OnGUI 系の重さ
- 場所：PingGauge / TutorialHintTrigger / StageManager / GameOverController の 4 箇所。
- 問題：IMGUI は 1 フレ複数回呼ばれアロケが出やすい。PingGauge は Texture2D を OnGUI で lazy 生成しており GC リスク（[PingGauge.cs:62-67](../Assets/Scripts/TruthWorld/PingGauge.cs#L62-L67)）。
- 修正：PingGauge の `_white` 生成を Awake へ。長期的には uGUI / UIElements 移行検討。

### [ ] B-4. RadarImageRenderer が毎フレーム `_material.SetFloat("_Brightness", ...)`
- 場所：[RadarImageRenderer.cs:66](../Assets/Scripts/Reconstruction/RadarImageRenderer.cs#L66)
- 修正：Start 時 1 回 + `OnValidate` でセット。

### [x] B-5. Mesh の毎フレーム全再構築（最大の最適化機会）
- 場所：[RadarImageRenderer.cs:74-109](../Assets/Scripts/Reconstruction/RadarImageRenderer.cs#L74-L109)
- 問題：最大 160,000 頂点を毎フレーム List に積み直し → Mesh.SetVertices で内部コピー。
- 修正案 (a)：`Mesh.AllocateWritableMeshData()` + `NativeArray<Vertex>` で GC アロケ 0、コピー 1 回。
- 修正案 (b)：billboard 展開と fade を GPU 側へ（Geometry/Compute Shader、または Quad mesh + instancing + ComputeBuffer）。CPU は `(hitPos, height, sensorId, timestamp)` を ComputeBuffer に積むだけになる。

### [x] B-6. SensorBus.LateUpdate の compaction が O(N)
- 場所：[SensorBus.cs:42-46](../Assets/Scripts/SensorWorld/SensorBus.cs#L42-L46)
- 問題：`_live` は timestamp 単調増加なのに毎フレーム全要素を走査。
- 修正：
  ```csharp
  int drop = 0;
  while (drop < _live.Count && _live[drop].timestamp < cutoff) drop++;
  if (drop > 0) _live.RemoveRange(0, drop);
  ```
  で O(drop) に。

### [x] B-7. SensorBus.Live が IReadOnlyList で interface dispatch
- 場所：[SensorBus.cs:15](../Assets/Scripts/SensorWorld/SensorBus.cs#L15)
- 問題：RadarImageRenderer から毎フレーム数万回 indexer 呼び出し、仮想呼び出しになる。
- 採用した修正：`LiveCount` + `LiveAt(int)` のペアで具象 `List<T>` indexer を直接公開。
- 不採用：`CollectionsMarshal.AsSpan` 案は Unity 6 のデフォルト API 互換レベル（.NET Standard 2.1）に `CollectionsMarshal` 型が存在しないためコンパイル不可。`apiCompatibilityLevel` を CoreCLR 系に上げるか、`allowUnsafeCode` を有効にして独自実装する選択肢はある。当面は具象 indexer で interface vtable は消える。

### [ ] B-8. シーンに対するセットアップ漏れがサイレント
- 場所：複数。`EnemyAI` の CapsuleCollider 前提、`StageManager` の Spawn/Goal/NavMeshSurface 前提。
- 修正案：`[RequireComponent(typeof(CapsuleCollider))]` 追加、`StageManager.LoadStage` で spawn==null 時に `Debug.LogError`。

### [x] B-9. シェーダ配置の不揃い
- 場所：[Assets/Scripts/Reconstruction/RadarPoint.shader](../Assets/Scripts/Reconstruction/RadarPoint.shader), [Assets/Scripts/TruthWorld/Explosion.shader](../Assets/Scripts/TruthWorld/Explosion.shader)
- 問題：Scripts/ 直下に置かれ、assembly 跨ぎで配置。
- 修正案：`Assets/Shaders/` 等に集約。

### [ ] B-10. テスト 0 件
- `com.unity.test-framework` 入っているのに 1 件も無い。
- 候補：`RadarSimulator.EmitArrivedWaves` の波面到達判定、`SensorBus` の compaction。

### [x] B-11. TutorialHintTrigger の Update が永続ループする経路
- 場所：[TutorialHintTrigger.cs:82-86](../Assets/Scripts/TruthWorld/TutorialHintTrigger.cs#L82-L86)
- 問題：Fire しない場合は毎 Update で `_cachedBeacons` 全体ループが続く。仕様通りだがコメント不足。
- 優先度：低。

### [x] B-12. EnemyAI._lastKnownPos の暗黙の順序依存
- 場所：[EnemyAI.cs:117-122, 133-161](../Assets/Scripts/TruthWorld/EnemyAI.cs#L117-L122)
- 修正案：`EnterChasing(Vector3 target)` のように引数で渡すと意図が明確。

### [ ] B-13. RadarSimulator.Scan が毎フレーム QueryParameters を new
- 場所：[RadarSimulator.cs:114](../Assets/Scripts/TruthWorld/RadarSimulator.cs#L114)
- 問題：struct なので GC アロケはないが、Awake で 1 回作って使い回す方が綺麗。

### [x] B-14. ScanProfile のデフォルト値リテラルが 3 箇所に重複
- 場所：[PlayerActor.cs:15-21](../Assets/Scripts/TruthWorld/PlayerActor.cs#L15-L21), [Beacon.cs:18-24](../Assets/Scripts/TruthWorld/Beacon.cs#L18-L24), [EnemyAI.cs:29-35](../Assets/Scripts/TruthWorld/EnemyAI.cs#L29-L35)
- 問題：仕様書（§2.2）にも「プレイヤー・ビーコン・敵は同じ 10 枚 / 0.2 m 間隔の基本形を共有」と明文化されているのに、コードでも同じリテラル `{10, 0.20f, 1f, 0.3f}` が 3 箇所に複写。共通既定を変えるとき 3 箇所同期が必要、片方の忘れで不整合になる。
- 修正案：
  - 軽い対応：[ScanProfile.cs](../Assets/Scripts/TruthWorld/ScanProfile.cs) に `public static ScanProfile Default => new() { slabCount = 10, ... };` を生やし、各センサで `scanProfile = ScanProfile.Default;` 初期化。
  - 仕様に沿う対応：`ScriptableObject` 化してプロジェクト 1 つの asset を Inspector で参照させる（プリセット差し替えがランタイム共有可能になる）。

### [x] B-15. CameraController のモード循環がマジックナンバー
- 場所：[CameraController.cs:121](../Assets/Scripts/TruthWorld/CameraController.cs#L121)
- 現状：`_mode = (Mode)(((int)_mode + 1) % 2);`
- 問題：`Mode` enum に 3 つ目（外部俯瞰、自由視点等）を足した瞬間サイレントに壊れる。`Enum.GetValues(...).Length` は毎呼びアロケ。
- 修正：`const int ModeCount = 2;` を定義して `% ModeCount`、enum 変更時にここを更新する規約をクラス先頭に明示。

### [x] B-16. ExplosionEffect が爆発のたびに Material を new
- 場所：[EnemyAI.cs:216-218](../Assets/Scripts/TruthWorld/EnemyAI.cs#L216-L218), [ExplosionEffect.cs:21,33](../Assets/Scripts/TruthWorld/ExplosionEffect.cs#L21-L33)
- 問題：`new Material(_explosionShader)` を爆発ごとに作って終了時 Destroy。GPU リソースなので地味に重い。さらに `_mat.SetColor("_Color", ...)` が文字列キー指定でハッシュ lookup が走る（per-frame）。
- 修正案：`EnemyAI` 側に `static Material _sharedExplosionMat` を 1 個作り、`MeshRenderer.sharedMaterial` で使い回しつつ、色は `MaterialPropertyBlock` で per-instance。Property ID は `static readonly int _IdColor = Shader.PropertyToID("_Color");` でキャッシュ。

### [ ] B-17. SignalsightNames.TryGetLayer の警告が連発しうる
- 場所：[SignalsightNames.cs:34-43](../Assets/Scripts/SensorWorld/SignalsightNames.cs#L34-L43)
- 問題：`EnemyAI.Explode` が爆発ごと、`StageManager.RevealWorld` がクリア演出ごとに `TryGetLayer` を呼ぶ。Editor 設定漏れ時に Console が同じ warning で溢れる。
- 修正案：`TryGetLayer` 内に `static HashSet<string> _warned` を持って 1 度警告したらサプレス。

### [ ] B-18. 入力分岐ヘルパが各 MonoBehaviour にローカル定義で散在【規模：大】
- 場所：[PlayerActor.cs:54-69](../Assets/Scripts/TruthWorld/PlayerActor.cs#L54-L69), [Beacon.cs:94-102](../Assets/Scripts/TruthWorld/Beacon.cs#L94-L102), [PlayerController.cs:67-74](../Assets/Scripts/TruthWorld/PlayerController.cs#L67-L74), [CameraController.cs:245-252](../Assets/Scripts/TruthWorld/CameraController.cs#L245-L252)
- 問題：`static bool XxxPressed()` の Keyboard/Gamepad/Mouse 分岐が 4 箇所で同じ形に書かれている。リバインド対応や VR コントローラ等の追加で全部書き直す羽目になる。
- **強い動機**：[Assets/InputSystem_Actions.inputactions](../Assets/InputSystem_Actions.inputactions) が既にプロジェクトに存在するのに、コードは低レベル API（`Keyboard.current.wKey.isPressed` 等）を直叩きしている。**InputSystem のモダンパターン未移行が最大の構造的負債**。Stage2 で新アクションが必要になった瞬間、また各所にコピペが増える。
- 修正の段階：
  1. （軽）薄い `SignalsightInput` 静的ラッパで 4 箇所のパターンを 1 箇所に集約。
  2. （本筋）`.inputactions` から C# クラス生成 → 各 MonoBehaviour で `InputAction` を保持。Keyboard/Mouse/Gamepad 分岐がコードから消える。リバインド可能性が手に入る。
- 規模感：影響範囲 4 ファイル（PlayerController / PlayerActor / Beacon / CameraController）。マウス感度の数式など既存挙動の再現に時間を取られる可能性あり。

### [x] B-19. 仕様書 / コミットメッセージ / コードデフォルトの値が三者不一致
- 場所：[Signalsight spec.md L228](Signalsight%20spec.md), commit `f8e0433` "Bump ... stage goal reach to 1 m", [StageGoal.cs:13](../Assets/Scripts/TruthWorld/StageGoal.cs#L13)
- 問題：spec の表は「到達判定距離 0.5 m」のまま、コミットメッセージは「1 m」、コードのデフォルトは `2f`。実 Inspector 値が支配的なのでバグではないが、仕様書を実コードに追従させていない。
- 修正：spec の §9 パラメータ表を `f8e0433` 以降のデフォルトに合わせて更新。今後はパラメータ変更コミットで spec.md も同 PR で更新する規約に。

### [ ] B-20. RadarImageRenderer の毎フレ uniform 書き込みに不変項目が混ざる
- 場所：[RadarImageRenderer.cs:116-120](../Assets/Scripts/Reconstruction/RadarImageRenderer.cs#L116-L120)
- 問題：B-4（Brightness）と同根。`_PointSize` も Inspector で変更しない限り起動後不変なのに毎 LateUpdate で `SetFloat`。`_CamRight/_CamUp/_Now` だけ毎フレ必要。
- 修正：Start + `OnValidate` で `_PointSize` `_Brightness` を 1 回設定、LateUpdate からは外す。

---

## C. アーキテクチャ全体の所感

action item ではなく俯瞰メモ。今後の設計判断のときに参照する用。

### 良い点（壊さないように）

- アセンブリ分割（TruthWorld / SensorWorld / Reconstruction）が綺麗に保たれている。Reconstruction が TruthWorld に依存しない原則が生きている。
- `SignalsightNames` による文字列の中央定数化、`TryGetLayer` ヘルパは堅実。Conventions.md の「直接 LayerMask.NameToLayer を呼ばない」が守られている。
- `RadarSimulator` の波面追従が `RaycastCommand` + Jobs で実装されており、main thread をブロックしない設計は的確。
- シングルトン規約と pause 耐性（`unscaledDeltaTime`）の規約がドキュメント化されている。

### 懸念点（中長期で効いてくる構造的な弱さ）

- **PlayerActor.Instance への依存が広がりすぎている**。シングルトンを「プレイヤー判定」と「位置参照」の両方に使っており、テスト時にモックを差し込みにくい。せめてプレイヤー判定は静的ヘルパに集約しておくと、後で疎結合化しやすい。→ B-1 と関連。
- **OnGUI が 4 箇所に散在**（GameOverController / StageManager / PingGauge / TutorialHintTrigger）。短期では問題ないが、UI 拡張時に uGUI / UIElements への移行が必要になる。今のうちに `UIRoot` 系コンポーネントに集約しておくと後が楽。→ B-3 と関連。
- **エラーパスのサイレント失敗が多い**。Shader.Find 失敗、layer 未定義、scene ロード失敗、いずれも警告/エラーログは出すがゲームは止まらず「なんとなく動く」状態になりがち。Editor 専用フックで「致命的セットアップ漏れは Play 開始直後に大きく見える（該当 GameObject を Selection.activeObject に）」のような仕組みを 1 個入れる価値あり。→ A-4, A-11, A-12, B-8 と関連。
- **「中央集約パターン」が半端**。`SignalsightNames` での文字列集約は完成度が高い一方で、Input（B-18）／PlayerActor 判定（B-1）／Camera.main（B-2）／ScanProfile デフォルト（B-14）はそれぞれ各所散在のまま。`SignalsightNames` を成功例として、`SignalsightInput` / `SignalsightRefs`（Camera/Player の弱参照キャッシュ）／`ScanProfile.Default` 等を増設すると「中央集約規約」が一貫し、新規 MonoBehaviour 追加時の判断コストも下がる。
- **テストゼロの代償が今後効いてくる**。`RadarSimulator.EmitArrivedWaves` の wave-radius 判定、`SensorBus` の compaction、`SensorPalette.ColorOf` の sensorId→color 写像はすべて純関数または timestamp 入力で決定的なので Edit Mode Test として書きやすい。B-10 の初手として最適。

---

## 修正優先度（私見）

| 優先 | 項目 | 理由 |
|---|---|---|
| 高 | A-10 (DetectPlayer 近距離 distance≤0) | 攻撃距離内 0.3 m 帯で検知抜け、1 行修正で済む |
| 高 | A-4 (LoadSceneAsync null チェック) | Stage2 追加時に必ず踏む。サイレント失敗の代表 |
| 高 | B-14 (ScanProfile デフォルト重複) | 3 ファイル横断 DRY、軽い修正で将来の不整合を予防 |
| 中 | B-5 (Mesh 構築の最適化) | 描画ボトルネックの本丸（GPU 化済みなら B-7/B-20 が次） |
| 中 | B-7 (SensorBus.Live Span 化) | 40,000 件の indexer 仮想呼び出しが消える |
| 中 | B-6 (SensorBus compaction) | 軽い変更で大きい効果 |
| 中 | A-2 (Vector3.Distance 二重計算) | A-10 と同じ箇所なので同時修正 |
| 中 | B-16 (Explosion Material 共有) | 爆発多発シーンで効く、API 整理も兼ねる |
| 中 | A-11 (CaptureOrthoBase silent fail) | A-3 系の silent failure テーマと併せて対処 |
| 低 | A-9, B-3, B-4, B-20 | コード品質・微最適化 |
| 低 | B-15, B-17, B-18, B-19 | DRY / 規約 / ドキュメント整合 |
| 低 | A-12, B-8 | 防御的コーディング、安心料 |
| 低 | B-9, B-10 | 中長期の保守性 |

---

## D. Stage2 着手前の foundation 仕上げ計画

> 方針：Stage1 しかまだ無いタイミングで、コードベースを「これ以上いじりたくない完成度」まで磨いておく。以後の Stage2 / Stage3 はこの綺麗な土台に沿わせて書く。
>
> この観点では「per-frame アロケ削減」「微最適化」よりも、**統一感（中央集約）の半端解消**と **将来の Scene 追加で踏む地雷の事前撤去** の優先度が上がる。

### 核心テーマ：「中央集約パターンが半端」

`SignalsightNames`（文字列の中央集約）と `ScanProfile.Default`（センサ既定の中央集約）は完成しているが、**同思想で集約されるべきパターンが 3 つ未完成**：

| 集約すべき対象 | 現状 | 対応 ID |
|---|---|---|
| Player 判定 | 2 ファイルで `GetComponent<PlayerActor>()` または `== PlayerActor.Instance.gameObject` を直接判定 | **B-1** |
| 入力 | 4 ファイルで `Keyboard.current` 直叩き＋デバイス分岐 | **B-18 大** |
| Camera 参照 | 4 ファイルで `Camera.main` を per-frame 取得 | **B-2** |

これらを潰すと、新 MonoBehaviour 追加時の判断コストが消え、「中央集約規約」が`SignalsightNames` レベルの完成度に揃う。

### Stage2 前 foundation 仕上げ — 推奨実行順

軽い掃除で勢いをつけて、最後に最大の B-18 大に取り組む順。

**フェーズ 1: 細かい掃除（リスク 0、合計 ~60 分）**

| 順 | ID | 概要 |
|---|---|---|
| 1 | A-9 | RadarSimulator.OnDestroy → ClearPending を呼ぶ（重複削除） |
| 2 | A-6 | Beacon.SetLayerRecursive に子レイヤ override 除外 |
| 3 | B-9 | シェーダ 2 本を `Assets/Shaders/` へ |
| 4 | B-12 | EnemyAI.EnterChasing(Vector3 target) で引数明示 |
| 5 | B-15 | CameraController モード循環の `% 2` を `const int ModeCount = 2;` に |
| 6 | B-19 | 仕様書 §9 パラメータ表をコード実値に同期 |
| 7 | B-11 | TutorialHintTrigger の永続ループにコメント追記 |

**フェーズ 2: 防御的セットアップ（Stage2 追加前に必須）**

| 順 | ID | 概要 |
|---|---|---|
| 8 | B-8 | StageSpawn / StageGoal / NavMeshSurface 欠落を Play 開始時にエラー。Stage2 作成時のセットアップミスを 0 秒で発見 |
| 9 | B-17 | `SignalsightNames.TryGetLayer` の警告を 1 回サプレス（中央 API がスパムしないように） |

**フェーズ 3: 中央集約パターン完成（核心）**

| 順 | ID | 概要 |
|---|---|---|
| 10 | B-2 | `SignalsightRefs.Camera`（弱参照キャッシュ）or `CameraController.Instance.Camera` で集約。`Camera.main` を全コードから消す |
| 11 | B-1 | `SignalsightPlayer.IsPlayer(Collider)` 等の静的ヘルパで Player 判定を 1 箇所に |
| 12 | **B-18 大** | `InputSystem_Actions.inputactions` から C# 生成 → `InputAction` ベースに全置換。Keyboard/Mouse/Gamepad の分岐がコードから消える |

### Stage2 前にはやらないと決めるもの

- **B-3 OnGUI 移行（UI Toolkit）** — 全面リプレイス級。Stage2 で UI 拡張が必要になってから判断
- **B-4 / B-13 / B-20** — per-frame アロケ微最適化。プロファイラで実際に困ったら
- **A-12** — in-flight 上限。今の Scan 頻度では溢れない
- **B-10** — テスト。手動プレイテストで運用する方針

### 完成定義（Stage2 着手の合図）

D 計画のフェーズ 1〜3 がすべて [x] になった時点で「foundation 完成」とみなし、Stage2 設計に進む。以降の新規コードはこの綺麗な規約に沿わせて書き、規約から外れる必要が出たらまずこのプランに項目を追加してから着手する。
