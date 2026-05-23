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

- `Time.deltaTime`、`Time.time` は止まる → これらに依存するロジックも止まる
- `Time.unscaledDeltaTime`、`Time.unscaledTime`、`Time.timeAsDouble`（注：これは Time.timeScale の影響を受ける）は動き続ける

**pause 中も進ませたい演出**（爆発フェード等）には `unscaledDeltaTime` を使う。
**pause で止めたい挙動**（プレイヤー操作等）は `deltaTime` ベースで自然に止まる。

---

## アセンブリ依存

`TruthWorld` → `SensorWorld` ← `Reconstruction` の片方向参照を保つ。

- `Reconstruction` は `TruthWorld` に依存してはいけない（点群描画が物理を知らない原則）
- 共通の定数・型は `SensorWorld` に置く
- `SignalsightNames` が `SensorWorld` に居るのもこのため
