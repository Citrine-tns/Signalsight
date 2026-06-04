# Signalsight プロジェクト規約

コードとシーンを増やすときに守ると、後から苦しまずに済む取り決め。

## 名前の中央定数化

Unity Editor で定義する**シーン名 / レイヤ名 / シェーダ名**をコードから参照するときは、
必ず `Signalsight.SensorWorld.SignalsightNames` 経由にする。直接ハードコード禁止。

### 何をどこに置くか

| 種別 | 置き場所 | 例 |
|---|---|---|
| シーン名（Build Settings に登録するもの） | `SignalsightNames.Scenes` | `SignalsightNames.Scenes.Core` |
| レイヤ名（Tags & Layers で定義するもの） | `SignalsightNames.Layers` | `SignalsightNames.Layers.World` |
| シェーダ名（`.shader` ファイル内で `Shader "..."` 宣言した名前） | `SignalsightNames.Shaders` | `SignalsightNames.Shaders.RadarPoint` |

### なぜか

`LayerMask.NameToLayer("World")` のように文字列をハードコードすると：

- Unity Editor 側で「World」を「Environment」に rename した瞬間にコードが silent に壊れる（コンパイル通る、実行時に -1 が返るだけ）
- どこで参照されているか grep でも完全には拾えない
- typo に気付けない（`"Wrold"` でもコンパイル通る）

中央定数経由なら：

- 1 行書き換えれば全コードが追従
- IDE の "Find All References" で全使用箇所を確実に列挙できる
- typo は即コンパイルエラー

### レイヤの解決は `TryGetLayer` を使う

```csharp
if (SignalsightNames.TryGetLayer(SignalsightNames.Layers.World, out int worldLayer))
{
    // worldLayer を使う
}
```

未定義レイヤを参照したときに警告ログを出す挙動が統一される。直接 `LayerMask.NameToLayer(...)` を呼ばない。

---

## 入力は `SignalsightInput` 経由

キーボード / マウス / ゲームパッドの直叩き（`Keyboard.current.wKey.isPressed` 等）禁止。
すべての入力は `Signalsight.SensorWorld.SignalsightInput.Player.<Action>` 経由で取る。

```csharp
if (SignalsightInput.Player.Jump.WasPressedThisFrame()) Jump();
Vector2 move = SignalsightInput.Player.Move.ReadValue<Vector2>();
```

`SignalsightActions` は [SignalsightActions.inputactions](../Assets/Scripts/SensorWorld/SignalsightActions.inputactions)
から Unity が自動生成する partial class。新しいアクションは Inspector で `.inputactions` に
追加すればコード生成が走り、`SignalsightInput.Player.Xxx` でアクセスできるようになる。

### なぜか

- リバインドや VR コントローラ等への拡張時に各 MonoBehaviour を書き換えなくて済む。
- Keyboard/Mouse/Gamepad の分岐がコードから完全に消える。仕様書 §8 のキーマウ列とパッド列の
  対応はすべて `.inputactions` のバインディングで表現される。
- マウス感度・スティック速度・キー速度の式が散らばらない（`CameraController` に集約）。

---

## オブジェクト参照は `SignalsightRefs` 経由

`Camera.main` の散在と、`GetComponent<PlayerActor>()` / `== PlayerActor.Instance.gameObject` の
散在を防ぐため、Camera と Player 本体の参照は `Signalsight.SensorWorld.SignalsightRefs` 1 箇所
に集約する。

| 用途 | 取り方 |
|---|---|
| カメラ参照 | `SignalsightRefs.Camera` |
| プレイヤー GameObject | `SignalsightRefs.PlayerGameObject` |
| プレイヤー Transform | `SignalsightRefs.PlayerTransform` |
| Collider がプレイヤー本体かの判定 | `SignalsightRefs.IsPlayer(Collider)` |

書き込みは `CameraController.Awake` と `PlayerActor.Awake` が publish、`OnDestroy` で自分が
登録中なら null 化、の片務的なライフサイクル。新しいシングルトン的参照を足したくなったら、
`SignalsightRefs` を拡張するのが規約（Reconstruction → TruthWorld の依存禁止を維持するため
下位アセンブリ SensorWorld に置いている）。

### `PlayerActor.Instance` と `SignalsightRefs.PlayerTransform` の使い分け

- **位置・回転だけ欲しいとき**：`SignalsightRefs.PlayerTransform` を使う。中央集約規約に揃い、
  Reconstruction など PlayerActor を知らないアセンブリでも同じ書き方で読める。
- **PlayerActor 固有の API**（`PingCooldown` / `LastPingTime` / `CharacterController` 経由の
  テレポート等）が必要なとき**だけ** `PlayerActor.Instance` を直接参照する。固有 API のために
  `Instance` を握っているスコープでも、**位置参照は `Refs.PlayerTransform` を使う**（grep で
  「位置依存」を 1 系統に揃え、固有 API 依存と区別する）。
- 新規 MonoBehaviour で「念のため PlayerActor を握っておく」のは禁止。後で位置参照だけと
  分かったら Refs に書き直す前提で進める。

### Start で 1 回キャッシュ（per-frame 読みは禁止）

以下はすべて寿命を通して不変：

- `SignalsightRefs.Camera` / `PlayerGameObject` / `PlayerTransform`（`Awake` で publish、`OnDestroy` で null 化）
- `.Instance` シングルトン：`RadarSimulator` / `SensorBus` / `PlayerActor` / `CameraController` / `BootManager` / `FieldManager` / `FieldClock` / `Inventory` / `ProgressFlags` / `RelicCounter` / `SaveRegistry` / `GameOverController` / `StageManager`（Legacy）
  （`Awake` でセット、`OnDestroy` で null 化）

**per-frame メソッド（`Update` / `LateUpdate` / `OnGUI` 等、およびそこから呼ばれる関数）で使う場合は
`Start` で 1 度ローカルフィールドにキャッシュ**し、以降はそのフィールドを参照する。

```csharp
Camera _cam;
Transform _playerT;
RadarSimulator _simulator;

void Start()
{
    _cam = SignalsightRefs.Camera;
    _playerT = SignalsightRefs.PlayerTransform;
    _simulator = RadarSimulator.Instance;
}

void Update()
{
    if (_simulator == null || _playerT == null) return;
    ...
}
```

per-frame で `SignalsightRefs.X` や `.Instance` を読み続けるコードは「中央参照が動的に差し替わる」
設計と誤読される。寿命固定であることをコードで明示する意味でも Start キャッシュに揃える。

破棄されたオブジェクトへの参照は Unity の overloaded `==` で `null` 扱いされるので、cached フィールドの
null チェックでそのまま捌ける。

**例外**：one-shot な読み（コルーチン内・OnTriggerEnter・到達時 1 回のみのイベント等）は per-frame
ではないのでキャッシュ不要、`.Instance` / `Refs.X` を直接読んで OK。判断基準は「同じ参照を同じ
オブジェクト寿命中に**繰り返し**読むか」。

### なぜか

- `Camera.main` は内部で `FindGameObjectsWithTag` を毎回呼ぶため per-frame パスでは無視できない。
- Player 判定が `GetComponent` 連打になると、Inspector で Player の構造を変えた瞬間に各所で
  挙動が不揃いになる。

---

## センサ既定は `ScanProfile.Default`

プレイヤー・ビーコン・敵が共有する「基本形」（10 スラブ / 0.2 m 間隔 / 1°/段 / emitterRadius 0.3）は
[ScanProfile.cs](../Assets/Scripts/TruthWorld/ScanProfile.cs) の `ScanProfile.Default` を `=` 初期化子で
参照する。**リテラル `{10, 0.20f, 1f, 0.3f}` を直書きしない**。仕様書 §2.2 の「同じ基本形を共有」を
コードでも 1 箇所に集約することで、共通既定の更新時に全センサが追従する。

```csharp
[SerializeField] ScanProfile scanProfile = ScanProfile.Default;
```

各センサで個別に値を変えたいときは Inspector で上書き。

---

## シングルトン MonoBehaviour

`SensorBus` / `RadarSimulator` / `PlayerActor` / `BootManager` / `FieldManager` / `FieldClock` / `Inventory` / `ProgressFlags` / `RelicCounter` / `SaveRegistry` / `GameOverController` / Legacy `StageManager` が以下のパターンを共有：

```csharp
public static MyClass Instance { get; private set; }

void Awake()
{
    if (Instance != null && Instance != this) { Destroy(this); return; }
    Instance = this;
}

void OnDestroy()
{
    if (Instance == this) Instance = null;
}
```

新しいシングルトンを足すときも同じ書き方で。Awake で先に登録、OnDestroy で必ず null 化。

---

## `TutorialHintTrigger` の GameObject 命名

`TutorialHintTrigger` は「シーン名 / GameObject 名」をキーに**過去のプレイで発火済みか**を判定する（`static HashSet<string>`）。

- **シーン内で必ず一意な GameObject 名を付ける**こと（例：`Hint_Ping`、`Hint_Jump`、`Hint_BeaconScanner`）
- Unity が `(1)` を自動付与するコピペ複製は OK だが、**手動で同名 GameObject を複数作らない**
- 同名衝突するとリスタート跨ぎの再発火抑止が誤動作する

---

## シーン構成

3 シーンの加算ロード構成：

| シーン | 役割 | 常駐コンポーネント |
|---|---|---|
| **Core** | 起動から終了まで常駐 | Player、Main Camera、`RadarSimulator`、`SensorBus`、`RadarImageRenderer`、`BootManager`、`Inventory`、`ProgressFlags`、`SaveRegistry`、`CraftingMenu`、`HudController`、`RelicCounter` |
| **Title** | 起動直後の入口 | `TitleController`（ENTER で SaveSystem.HasSave() を見て新規/続きから分岐） |
| **Field** | プレイ本編のシームレス 1 マップ | `FieldManager`、`FieldClock`、`StageSpawn`、床・壁・拠点候補地、`FieldPickup`、`EnemyActivator`、ベイク済み `NavMeshSurface` |

`BootManager` が Title↔Field の切替を担う。`s_restartStageIndex` のような前 stage index 静的変数は持たず、SaveSystem の有無で「新規/続きから」を分岐する。

旧 Stage1/Stage2 シーンは Legacy 用に残置されている（Title からの遷移リストには含めない）。Legacy シーンを開発で触る場合は `Legacy/StageManager` を併用するが、新規実装はすべて Field シーンに対して行う。

---

## Field シーンのひな型

Field シーンが必ず持つべきもの：

| 必須コンポーネント | 役割 |
|---|---|
| `FieldManager` を持つ GameObject | Field 常駐の中央レジストリ。CoreBeacon 追跡、PlacedBeacon 登録簿、StageSpawn キャッシュ |
| `FieldClock` を持つ GameObject | PlacedBeacon と Enemy が共有する同期 tick（既定 0.5 s） |
| `StageSpawn` を持つ GameObject | プレイヤー初期位置 + Core 未配置時のリスポーン先 |
| `NavMeshSurface`（AI Navigation パッケージ）を持つ GameObject | 敵の経路探索用、ベイクしておくこと |
| 床・壁・階段などのジオメトリ | レイの的、World レイヤに配置 |

任意配置：

- `FieldPickup` を持つ GameObject … 拾える Beacon / Material / Relic。`id` フィールドを必ず一意に設定（永続化キー） |
- `EnemyAI` を持つ GameObject … 敵本体（NavMeshAgent 自動付与、CapsuleCollider は手動配置）、World レイヤ |
- `EnemyActivator` を持つ GameObject … 敵を遅延起動。`id` フィールドを一意に設定（永続化キー） |

Legacy/Stage シーン（Stage1/2 用）は別構成：旧 `StageGoal` / 旧 `Beacon` / 旧 `TutorialHintTrigger` を使い、Field シーンの規約は適用されない。

---

## FieldManager 中央登録簿

Field シーンに常駐する `FieldManager` が「Field 内で何度も探したくなる参照」を集約する。**ホットパスから `FindObjectsByType` / `FindFirstObjectByType` を排除する**のがこの仕組みの目的。

| プロパティ | 出席タイミング | 退席タイミング | 用途 |
|---|---|---|---|
| `Core` | `CoreBeacon.OnEnable` | `CoreBeacon.OnDisable` | リスポーン基点、セーブ可否判定 |
| `AllPlaced` | `PlacedBeacon.OnEnable` | `PlacedBeacon.OnDisable` | SaveService.Capture、BeaconPlacementController の距離チェック |
| `AllLures` | 同上、ただし `Kind.IsLure == true` のみ | 同上 | EnemyAI.FindClosestVisibleLure（Tick ごと走査） |
| `Spawn` | `FieldManager.Awake` で 1 回 `FindFirstObjectByType<StageSpawn>()` | - | RespawnService の Core 不在時フォールバック |

### 規約

- 新しい Field 内常駐物（コア候補、巡回ポイント等）を増やすときは `FindObjectsByType` 直叩きせず `FieldManager` に登録簿を追加する
- 「Lure だけ走査したい」のような頻出フィルタは **サブ登録簿として持つ**（全件走査して IsLure を見るのは ホットパス的に NG）
- 登録は `OnEnable`、解除は `OnDisable` の対で。`OnDestroy` ではなく `OnDisable` 側にあるのは、シーンアンロードや一時 disable に対応するため
- `FieldManager.Instance == null` のときは Field 外（Stage1/2、Title）。書き込みは null ガード必須

---

## FieldClock 同期 tick

`FieldClock` が Field 常駐の唯一の Tick 発火元。`OnTick(int tickIndex)` イベントを `PlacedBeacon` と `EnemyAI` が購読し、全センサが同フレームでスキャンを発火する。

### なぜ同期するか

センサごとに自前タイマーを持つと、同種ビーコンを 0.25 s ずらして 2 個置けば実効頻度が 2 倍になる「ずらし高頻度化」の抜け道ができる。`FieldClock` を共有 tick 源にして `tickIndex % kindMultiplier == 0` を条件にすることで、同種類を何個置いても周期は変わらない。

### 規約

- 周期発火が必要な新規センサは `FieldClock.Instance.OnTick += HandleTick` を購読する（自前タイマー禁止）
- 購読/解除は `OnEnable` / `OnDisable` の対で。`FieldClock.Instance == null` チェック必須（Stage1/2 シーンには無いため）
- **プレイヤー ping は FieldClock の対象外**。`PlayerActor.Ping()` は押した瞬間に独立して `RadarSimulator.Scan` を呼ぶ
- `CoreBeacon.BurstScan`（リスポーン直後 1 発）も Tick 対象外
- Pause（`Time.timeScale == 0`）中は HandleTick 内で early return（FieldClock 自体は scaled time で進むので自動的に止まる）

---

## ProgressFlags による永続化パターン

「拾った」「敵を起こした」のような **一度起きたら覚えておきたい状態** は、`ProgressFlags` の string フラグで表現する。Phase 9 のセーブが内部状態としてこのフラグ集合を丸ごと書き出すので、シリアライズコードを書かずに永続化が完了する。

### 書き方の慣用句

```csharp
[SerializeField] string id;  // シーン内で一意な永続化キー

void Awake()
{
    if (string.IsNullOrEmpty(id)) return;
    if (ProgressFlags.Instance != null && ProgressFlags.Instance.Has("<prefix>_" + id))
        Destroy(gameObject); // または 既起動状態へジャンプ
}

void OnSomeEvent()
{
    if (!string.IsNullOrEmpty(id) && ProgressFlags.Instance != null)
        ProgressFlags.Instance.Set("<prefix>_" + id);
}
```

### prefix 名前空間

| prefix | 意味 | 使用箇所 |
|---|---|---|
| `pickup_` | FieldPickup を拾った | `FieldPickup.cs` |
| `relic_` | 遺構を拾った（RelicCounter が集計） | `FieldPickup.cs` + `RelicCounter.cs` |
| `activator_` | EnemyActivator が発火済み | `EnemyActivator.cs` |

新しい永続化対象を増やすときは：

- 既存 prefix を流用するか、衝突しない新 prefix を選ぶ
- `id` フィールドをシリアライズ可能にし、Inspector で必ず一意な値を設定
- `RelicCounter` のように **集計が必要なフラグ群** は prefix を独自に持ち、`StartsWith(prefix)` で列挙する

---

## Save System

Phase 9 で実装。4 ファイル構成：

| ファイル | 責務 |
|---|---|
| [SaveData.cs](../Assets/Scripts/TruthWorld/Save/SaveData.cs) | `[Serializable]` DTO のみ。`progressFlags` / `inventory` / `placedBeacons` / `selectedBeaconItemId` |
| [SaveSystem.cs](../Assets/Scripts/TruthWorld/Save/SaveSystem.cs) | static ファイル IO。`JsonUtility` + tempfile + atomic rename、スロット番号で複数セーブ対応 |
| [SaveRegistry.cs](../Assets/Scripts/TruthWorld/Save/SaveRegistry.cs) | Core 常駐 singleton。`ItemKind` / `BeaconKind` SO を `Dictionary` で ID 逆引き |
| [SaveService.cs](../Assets/Scripts/TruthWorld/Save/SaveService.cs) | static。`Capture` で world → SaveData、`Restore` で SaveData → world |

### Capture / Restore の二段化

シーン依存度で `Core` 常駐分と `Field` 常駐分に分け、それぞれを個別に Capture / Restore する：

| 段階 | 対象 | 呼び出しタイミング |
|---|---|---|
| `CaptureCore` | ProgressFlags、Inventory、SelectedBeaconKind | セーブ時に常に |
| `CaptureField` | PlacedBeacons | セーブ時に FieldManager 在のときのみ |
| `RestorePreSceneLoad` | ProgressFlags、Inventory | Field 読込**前**（FieldPickup.Awake がフラグを見て自己 Destroy するので必須順序） |
| `RestorePostSceneLoad` | PlacedBeacons | Field 読込**後**（PlacedBeacon を Instantiate するためシーンが居る） |

### 規約

- 新規セーブ項目を足すときは「Core 常駐 か Field 常駐 か」で `CaptureCore` / `CaptureField` のどちらに行を足すか決める
- 新規 `ItemKind` / `BeaconKind` SO を作ったら **必ず** `SaveRegistry` の Inspector 配列に drag & drop（忘れるとロード時に warning ログ + 静かに消失）
- `SaveSystem.HasSave()` の戻り値で Title の「新規/続きから」を分岐する（前 stage index の static 変数は持たない）

---

## sensorId 帯設計

`SensorPalette` の hue 帯と shader 配列 (`MaxSensorColors=32`) を以下で分割：

| 範囲 | 用途 | 色相帯 |
|---|---|---|
| `0` | プレイヤー | シアン固定 |
| `1..15` | ビーコン種類 (kind) | シアン〜紫 |
| `16..23` | 敵種類 (kind) | 赤〜オレンジ |
| `24..31` | 予備 | - |

同じ kind のインスタンスが複数あっても**同じ ID を共有**する（=「色＝種類」が成立、敵 2 体は同色）。動的 ID プール（Acquire/Release）は持たない。

### 規約

- 新しいビーコン kind を増やすときは `BeaconKind.SensorId` を 1〜15 から空き番号で選ぶ
- 新しい敵 kind を増やすときは 16〜23 から空き番号で選ぶ
- `MaxSensorColors` を超えそうになったら（kind 種類が 32 を超えたら）`RadarImageRenderer` の uniform 配列サイズと shader を同時拡張する

---

## Pause（`Time.timeScale = 0`）への耐性

GameOver は `Time.timeScale = 0` で全停止する。これに以下が影響する：

- 止まる（`timeScale` の影響を受ける）：`Time.deltaTime`、`Time.time`、`Time.timeAsDouble`
  （`timeAsDouble` は `time` の double 版なので scale の影響を同じく受ける）
- 動き続ける（`timeScale` を無視）：`Time.unscaledDeltaTime`、`Time.unscaledTime`、`Time.unscaledTimeAsDouble`

**pause 中も進ませたい演出**（爆発フェード等）には `unscaledDeltaTime` を使う。
**pause で止めたい挙動**（プレイヤー操作・スキャン発行・測距点の減衰判定）は `deltaTime` /
`timeAsDouble` ベースで自然に止まる。

### マウス delta は時間軸を持たないので別途ガードが要る

`Time.deltaTime` を掛ける入力（キー・スティック）は pause で自然に 0 になるが、マウスの
per-frame delta は時間軸を持たないので pause 中も流れる。マウスで視点を回す系の Update は
`if (Time.timeScale == 0f) return;` で明示的にガードする（`CameraController.Update` 参照）。

---

## 実行順（DefaultExecutionOrder）

複数の `[DefaultExecutionOrder]` 付き singleton が **Awake と LateUpdate の両方** で順序保証されている。

### Awake 実行順（依存先 → 依存元）

| 優先度 | コンポーネント | 役割 |
|---:|---|---|
| **-300** | `FieldClock` | tick イベントの発火元として最先に確立 |
| **-250** | `FieldManager` | PlacedBeacon/EnemyAI が OnEnable で Register するため先に Instance を確立 |
| **-200** | `BootManager` | シーン遷移統括、その他 singleton より先 |
| **-100** | `RadarSimulator` | 各センサが Start で Instance を読むため Awake 時点で確立 |

### LateUpdate 実行順（センサパイプライン）

`RadarSimulator` → `SensorBus` → `RadarImageRenderer` の 3 段は同フレーム内で**この順で動く必要がある**（生レイ回収 → compaction → GPU 転送）。

| 優先度 | コンポーネント | LateUpdate の仕事 |
|---:|---|---|
| **-100** | `RadarSimulator` | 完了 batch を回収し、波面到達済みの測距点を `SensorBus._live` に publish |
| **0**（既定） | `SensorBus` | `_live` から有効期限切れを drop（compaction） |
| **100** | `RadarImageRenderer` | 整理済み `_live` を読んで GPU バッファへ転送 |

新しいセンサ系コンポーネントを足すときはこの表を見て、どこに挟むか決める。挟む変更を入れたら同 PR でこの表を更新すること（コードの `[DefaultExecutionOrder]` だけが更新されて表が古くなる状況を作らない）。

---

## アセンブリ依存

`TruthWorld` → `SensorWorld` ← `Reconstruction` の片方向参照を保つ。

- `Reconstruction` は `TruthWorld` に依存してはいけない（点群描画が物理を知らない原則）
- 共通の定数・型は `SensorWorld` に置く
- `SignalsightNames` が `SensorWorld` に居るのもこのため

---

## コメントの書き方

コメントは「**なぜ**」を書く。「何を」「どう動くか」はコードが自分で説明する。

### 書かないコメント

- **Attribute / 規約の再説明**：`[RequireComponent(typeof(CapsuleCollider))]` の直下に「CapsuleCollider は RequireComponent で保証済み」と書かない。Attribute が文書。
- **同一文の複数現場コピー**：規約は Conventions.md に書く。各コール元で繰り返し説明しない（Conventions.md と冗長になる時点で削除）。
- **直前のリファクタ履歴**：「以前は X だった」「Y から移行した」型の archeology はコミットメッセージに書く。コード上に残さない。
- **却下した設計の口頭歴史**：「ここではプロパティを公開しない（理由）」型の検討メモはコミットメッセージや PR description が適切な置き場所。

### 書くコメント

- **why（設計判断の根拠）**：「`sqrMagnitude` を使うのは sqrt 1 回ぶんの節約 + 他箇所と揃えるため」
- **非自明な制約**：「`Awake` で寸法確定するのは、`enabled=false` でも床に立たせたいから」
- **仕様書・規約への参照**：「仕様書 §2.2 の共通基本形を共有」
- **将来踏みうる罠**：「`elev` を ±89° にクランプするのは LookRotation の極で崩れるのを防ぐため」
- **パフォーマンス上の選択**：「`stackalloc` で GC アロケなし」「具象 indexer 経由で仮想呼び出し回避」

### 削除タイミング

リファクタで根拠が消えた瞬間にコメントも消す。古いコメントは新規読者を誤導する。

---

## テスト方針

**手動プレイテストで運用する**。`com.unity.test-framework` は入っているが Edit Mode Test / Play Mode Test は当面書かない。

Stage1 規模では：
- ロジックの大半は Unity API（`NavMeshAgent`, `RaycastCommand`, `Physics.Raycast`）に依存しており、純粋関数として切り出せる部分が少ない
- 手動プレイでカバーできる動作範囲が狭い（ステージ 1 つぶん）
- テスト基盤（Assembly Definition 追加、モック準備）の整備コストが、得られる安全網に見合わない

将来テストを足すなら最初の候補は：
- `RadarSimulator.EmitArrivedWaves` の波面到達判定（時刻入力で決定的）
- `SensorBus` の compaction
- `SensorPalette.ColorOf` の sensorId→color 写像

これらは Stage2 以降で手動プレイテストが追いつかなくなった瞬間に判断する。

---

## UI 描画の方針

現状すべての画面 UI は **IMGUI（`OnGUI`）** で書かれている：

| ファイル | 役割 |
|---|---|
| `HudController` | インベントリのスロットを画面端に表示 |
| `CraftingMenu` | C キーで開閉する合成 UI |
| `TitleController` | タイトル画面（新規/続きから/オプション） |
| `GameOverController` | 死亡時オーバーレイ |
| `RelicCounter` | 「遺構: N / M」表示と「ALL CLEAR」演出 |
| `PingGauge` | ping クールダウンの縦バー |
| Legacy: `TutorialHintTrigger`、`StageManager` | Stage1/2 専用 UI |

IMGUI は 1 フレに複数回呼ばれアロケが出やすいため恒久解ではない。**Phase 10 で UI Toolkit への全面移行**を予定している。それまでは：

- 新規 UI は `OnGUI` で追加してよい（既存と揃える）
- per-OnGUI のアロケは可能な限り避ける：
  - `GUIStyle` は初回 lazy または `Start` で初期化
  - `Color` リテラルは `static readonly` でキャッシュ（`new Color(...)` を `OnGUI` 内に置かない）
  - 文字列補間 (`$"..."`) は値が変化したときだけ作り直してフィールドにキャッシュ（`RelicCounter._statusCache` 参照）
- マウスホバー / フォーカス / アニメーション等が必要になったら UI Toolkit 移行のトリガーとする
