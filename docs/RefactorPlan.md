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

### [ ] A-2. EnemyAI.Update の Vector3.Distance 二重計算
- 場所：[EnemyAI.cs:127-129, 216](../Assets/Scripts/TruthWorld/EnemyAI.cs#L127-L129)
- 現状：attackRange 判定と blastRadius 判定で `Vector3.Distance(transform.position, player.position)` を 2 回呼ぶ。
- 問題：sqrt が 2 回走る。StageGoal/Beacon は sqrMagnitude を使っており不揃い。
- 修正：`(player.position - transform.position).sqrMagnitude` を 1 回計算し、`attackRange*attackRange` / `blastRadius*blastRadius` と比較。

### [ ] A-3. worldMask = ~0 デフォルトの脆さ
- 場所：[EnemyAI.cs:23](../Assets/Scripts/TruthWorld/EnemyAI.cs#L23), [RadarSimulator.cs:31](../Assets/Scripts/TruthWorld/RadarSimulator.cs#L31)
- 現状：デフォルトが「全レイヤ」。Tooltip は「World レイヤだけを含めること」と書いてあるのに矛盾。
- 問題：Inspector で設定し忘れた瞬間に自己ヒットや誤検知の原因になる。
- 修正案：Awake で `LayerMask.NameToLayer("World")` を読み安全側へ寄せる、または未設定時に警告ログ。

### [ ] A-4. StageManager._busy の戻り忘れリスク
- 場所：[StageManager.cs:121-156](../Assets/Scripts/TruthWorld/StageManager.cs#L121-L156)
- 問題：`LoadSceneAsync` が null を返す（シーン未登録など）と while ループが素通りし、`_busy` は false に戻るがロード失敗が検出されない。
- 修正：`if (load == null) { Debug.LogError(...); _busy = false; yield break; }` を入れる。

### [ ] A-5. GameOverController のシングルトンが規約と乖離
- 場所：[GameOverController.cs:14-42](../Assets/Scripts/TruthWorld/GameOverController.cs#L14-L42)
- 問題：Conventions.md の `Instance` プロパティ + Awake 登録パターンに従っていない。Trigger() で lazy 生成のため、誤って Scene に手置きすると二重生成され GUI が二重描画される。
- 修正案：Awake を足して規約に揃える、もしくは Trigger() 冒頭で `FindFirstObjectByType<GameOverController>()` フォールバックを 1 回入れる。

### [ ] A-6. Beacon.SetLayerRecursive が子要素を一律巻き込む
- 場所：[Beacon.cs:80-85](../Assets/Scripts/TruthWorld/Beacon.cs#L80-L85)
- 問題：Beacon 下に意図的に別レイヤの子（エフェクト等）を置きたくなった瞬間に破綻する。
- 優先度：低（現状破綻していない、将来のための予防）。

### [ ] A-7. PlayerActor & Beacon の ScanProfile.emitterRadius がデフォルト 0
- 場所：[PlayerActor.cs:15-20](../Assets/Scripts/TruthWorld/PlayerActor.cs#L15-L20), [Beacon.cs:18-23](../Assets/Scripts/TruthWorld/Beacon.cs#L18-L23)
- 問題：`emitterRadius` 初期化漏れで 0 になり、自己ヒットで点群が歪む潜在リスク。EnemyAI のみ 0.3f を持つので片手落ち。
- 修正：両方の `ScanProfile` 初期化に `emitterRadius = 0.3f`（か適切な値）を追加。Inspector でセット済みの可能性が高いが、シリアライズ値の確認も。

### [ ] A-8. RadarSimulator.MaxRaysPerSlab = 4096 の stackalloc サイズ過大
- 場所：[RadarSimulator.cs:46, 121-122](../Assets/Scripts/TruthWorld/RadarSimulator.cs#L46)
- 問題：最大 32KB のスタック消費。実害は無いがデフォルト 240 本に対して過剰。
- 修正：上限を 1024 程度に絞るか、超えそうな値を inspector で受け取った時点で warning。

### [ ] A-9. RadarSimulator.OnDestroy と ClearPending のロジック重複
- 場所：[RadarSimulator.cs:79-103](../Assets/Scripts/TruthWorld/RadarSimulator.cs#L79-L103)
- 問題：in-flight drain & dispose が 2 箇所に重複。
- 修正：`OnDestroy` から `ClearPending()` を呼ぶ。

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

### [ ] B-5. Mesh の毎フレーム全再構築（最大の最適化機会）
- 場所：[RadarImageRenderer.cs:74-109](../Assets/Scripts/Reconstruction/RadarImageRenderer.cs#L74-L109)
- 問題：最大 160,000 頂点を毎フレーム List に積み直し → Mesh.SetVertices で内部コピー。
- 修正案 (a)：`Mesh.AllocateWritableMeshData()` + `NativeArray<Vertex>` で GC アロケ 0、コピー 1 回。
- 修正案 (b)：billboard 展開と fade を GPU 側へ（Geometry/Compute Shader、または Quad mesh + instancing + ComputeBuffer）。CPU は `(hitPos, height, sensorId, timestamp)` を ComputeBuffer に積むだけになる。

### [ ] B-6. SensorBus.LateUpdate の compaction が O(N)
- 場所：[SensorBus.cs:42-46](../Assets/Scripts/SensorWorld/SensorBus.cs#L42-L46)
- 問題：`_live` は timestamp 単調増加なのに毎フレーム全要素を走査。
- 修正：
  ```csharp
  int drop = 0;
  while (drop < _live.Count && _live[drop].timestamp < cutoff) drop++;
  if (drop > 0) _live.RemoveRange(0, drop);
  ```
  で O(drop) に。

### [ ] B-7. SensorBus.Live が IReadOnlyList で interface dispatch
- 場所：[SensorBus.cs:15](../Assets/Scripts/SensorWorld/SensorBus.cs#L15)
- 問題：RadarImageRenderer から毎フレーム数万回 indexer 呼び出し、仮想呼び出しになる。
- 修正案：`ReadOnlySpan<Measurement> GetLiveSpan()`（`CollectionsMarshal.AsSpan(_live)` 使用）を提供。

### [ ] B-8. シーンに対するセットアップ漏れがサイレント
- 場所：複数。`EnemyAI` の CapsuleCollider 前提、`StageManager` の Spawn/Goal/NavMeshSurface 前提。
- 修正案：`[RequireComponent(typeof(CapsuleCollider))]` 追加、`StageManager.LoadStage` で spawn==null 時に `Debug.LogError`。

### [ ] B-9. シェーダ配置の不揃い
- 場所：[Assets/Scripts/Reconstruction/RadarPoint.shader](../Assets/Scripts/Reconstruction/RadarPoint.shader), [Assets/Scripts/TruthWorld/Explosion.shader](../Assets/Scripts/TruthWorld/Explosion.shader)
- 問題：Scripts/ 直下に置かれ、assembly 跨ぎで配置。
- 修正案：`Assets/Shaders/` 等に集約。

### [ ] B-10. テスト 0 件
- `com.unity.test-framework` 入っているのに 1 件も無い。
- 候補：`RadarSimulator.EmitArrivedWaves` の波面到達判定、`SensorBus` の compaction。

### [ ] B-11. TutorialHintTrigger の Update が永続ループする経路
- 場所：[TutorialHintTrigger.cs:82-86](../Assets/Scripts/TruthWorld/TutorialHintTrigger.cs#L82-L86)
- 問題：Fire しない場合は毎 Update で `_cachedBeacons` 全体ループが続く。仕様通りだがコメント不足。
- 優先度：低。

### [ ] B-12. EnemyAI._lastKnownPos の暗黙の順序依存
- 場所：[EnemyAI.cs:117-122, 133-161](../Assets/Scripts/TruthWorld/EnemyAI.cs#L117-L122)
- 修正案：`EnterChasing(Vector3 target)` のように引数で渡すと意図が明確。

### [ ] B-13. RadarSimulator.Scan が毎フレーム QueryParameters を new
- 場所：[RadarSimulator.cs:114](../Assets/Scripts/TruthWorld/RadarSimulator.cs#L114)
- 問題：struct なので GC アロケはないが、Awake で 1 回作って使い回す方が綺麗。

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
- **エラーパスのサイレント失敗が多い**。Shader.Find 失敗、layer 未定義、scene ロード失敗、いずれも警告/エラーログは出すがゲームは止まらず「なんとなく動く」状態になりがち。Editor 専用フックで「致命的セットアップ漏れは Play 開始直後に大きく見える（該当 GameObject を Selection.activeObject に）」のような仕組みを 1 個入れる価値あり。→ A-4, B-8 と関連。

---

## 修正優先度（私見）

| 優先 | 項目 | 理由 |
|---|---|---|
| 高 | A-1 (detectTimer 剰余切り捨て) | 1 行修正、挙動の正確さに直結 |
| 高 | A-3 (worldMask デフォルト) | サイレントなレイヤ事故の温床 |
| 高 | A-7 (emitterRadius デフォルト 0) | 自己ヒットでスキャンが歪む可能性 |
| 中 | B-5 (Mesh 構築の最適化) | 描画ボトルネックの本丸 |
| 中 | B-6 (SensorBus compaction) | 軽い変更で大きい効果 |
| 中 | A-5 (GameOverController 規約統一) | 将来の事故予防 |
| 中 | A-8 (stackalloc サイズ) | 過剰確保の縮小 |
| 低 | A-2, B-3, B-4, A-9 | コード品質・微最適化 |
| 低 | B-9, B-10 | 中長期の保守性 |
