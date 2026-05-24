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

`SensorBus`・`RadarSimulator`・`PlayerActor`・`StageManager`・`GameOverController` が以下のパターンを共有：

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

## ステージ Scene のひな型

各ステージ Scene が必ず持つべきもの：

| 必須コンポーネント | 役割 |
|---|---|
| `StageSpawn` を持つ GameObject | プレイヤーの開始位置 |
| `StageGoal` を持つ GameObject | クリア判定（プレイヤーが reachRange 以内に来るとクリア） |
| `NavMeshSurface`（AI Navigation パッケージ）を持つ GameObject | 敵の経路探索用、ベイクしておくこと |
| 床・壁・階段などのジオメトリ | レイの的、World レイヤに配置 |

敵を置く場合は：

- `EnemyAI` が `[RequireComponent(typeof(NavMeshAgent))]` なので NavMeshAgent が自動付与される
- `CapsuleCollider` を別途配置（`MatchAgentToBody` が寸法の参照元として使う）
- World レイヤに配置

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

## アセンブリ依存

`TruthWorld` → `SensorWorld` ← `Reconstruction` の片方向参照を保つ。

- `Reconstruction` は `TruthWorld` に依存してはいけない（点群描画が物理を知らない原則）
- 共通の定数・型は `SensorWorld` に置く
- `SignalsightNames` が `SensorWorld` に居るのもこのため
