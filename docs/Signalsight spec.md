# Signalsight 仕様書

視覚を持たず、能動的な測距だけで世界を知覚する探索ゲーム。

## 0. 概要

プレイヤーは視覚を持たない。自機・敵機・ビーコンが全方位へレイを撃つ LiDAR 方式の能動測距だけが、世界を知る手段である。各センサのレイが当たった点を「測距点」として記録し、その点群としてのみ世界は姿を現す。世界のジオメトリそのものは（通常）描画されない。各測距点は「波が届くまで」の出現遅延を持ち、走査の瞬間から距離に応じて時間差で点が灯っていく。点群は出現後 1 秒で減衰するため、プレイヤーは断続的な観測と記憶で像を組み立てる。視点は 1 人称（既定）と俯瞰オルソを Z キーで循環、いずれもプレイヤー基準で水平方向に回転可能。

ゲームの構造は **「探索 × 拠点進化 × 古代遺構収集」** の 3 軸。プレイヤーはシームレスな 1 つの暗黒マップ（Field）を歩き、地上に点在する未起動ビーコンや素材・遺構を拾って自らの拠点（コアビーコン）を起点に視界網を広げていく。素材を組み合わせた合成で特性違いのビーコンを生み出し、視覚拡張軸（広角・高所）と敵干渉軸（囮）を組み合わせて未踏領域に踏み込む。古代遺構を 5〜7 個収集することで進行が満たされてクリアとなる。死亡はコア周辺へのリスポーンで、所持品も点群も失わない。コア隣接時に手動セーブが可能で、再起動後はタイトル「続きから」で復帰する。

このため Stage を進めて完結する旧 Stage1/Stage2 式とは別の構造を取る。旧ステージは開発用テストマップとして残置されており、タイトルからの遷移リストには含めない。

## 1. 設計哲学

世界を直接は描かない。プレイヤー（および敵機、協調センサであるビーコン）が能動的に撃ったレイの当たった点だけが観測情報として与えられる。遮蔽された面は観測されない。位置・形状・地形はすべて、断続的な測距点の集積から立ち上がるべきもので、決して直接の描画ではない。「見ること」自体が能動的でコストを伴う行為であり、それをゲームの中心に置く。

## 2. 知覚モデル

### 2.1 測距スキャン

各センサは「スラブ」と呼ぶ水平断面を複数持ち、各スラブで全方位にレイを撃つ。レイは黄金角分布で角度的に均等に散らばる。レイが World レイヤのコライダーに当たれば、最初のヒット点を 1 つの測距点（`Measurement`）として記録する。経路長や反射電力ではなく、当たった点の座標そのものが情報である。

レイは `RaycastCommand` + Jobs でバッチ照射する（ワーカースレッド並列）。

### 2.2 走査プロファイル（ScanProfile）

センサ種ごとに走査ジオメトリを `ScanProfile` で定義する：

- **slabCount** … スラブ枚数。
- **slabSpacing** … スラブの垂直間隔 [m]。
- **elevationStepDeg** … 中心スラブから 1 段ごとに付く仰角 [度]。0 なら全スラブ水平。
- **emitterRadius** … 発射体の半径 [m]。レイ原点をこのぶん外へずらし、自己コライダーへの自己ヒットを防ぐ。

スラブはセンサのローカル上方向に積まれ、中心スラブを基準に上下へ展開する。`elevationStepDeg` が 0 でなければ、中心から離れたスラブのレイは仰角を持ち、扇型の放射になる。

スラブ面・レイ方向はセンサの回転に従う。センサを傾ければ走査面も傾き、たとえば下向きに設置したビーコンは、水平走査では拾えない床面を捉えられる。

プレイヤー・ビーコン・敵は同じ 10 枚 / 0.2 m 間隔の基本形を共有し、扇の開き角は `elevationStepDeg` でセンサごとにインスペクタ調整する。

### 2.3 遮蔽

レイは最初に当たったコライダーで止まり、その奥は観測されない＝遮蔽。これにより「壁の裏は見えない」「別の位置のセンサが死角を埋める」という構図が生まれる。

### 2.4 縦方向

各スラブは異なる高さの水平断面なので、縦の構造はスラブ群の高さ方向の集積から立ち上がる。スキャンはセンサ自身の高さ座標を基準に行われるため、ジャンプや階段で高い位置から撃てば、より高い断面を観測できる。測距点は真の 3D 世界座標を持つ。

### 2.5 レーダ波の伝播（波面追従）

`RadarSimulator` には見かけの伝播速度 `propagationSpeed`（既定 60 m/s）がある。スキャン時に各レイは 1 度だけ撃たれ、ヒット情報（コライダ・ローカル座標・センサ原点・scan 時刻）が保留キューに積まれる。`RadarSimulator` は毎フレーム、各保留ヒットについて「波の球面半径（経過時間 × propagationSpeed）」と「センサ原点から surface 現在位置までの距離」を比較し、波が surface に追いついた瞬間にその surface の**現在位置**で測距点を発行する。

このため 1 回のスキャンが「波が外側に広がりながら点を灯していく」アニメーションになる。動く surface についても、波が当たった瞬間の位置が点として現れる（タイミングと位置が同じ物理事象を指す）。近距離が先に解像し遠距離は遅れて届くという観測体験そのものが、能動測距の知覚モデルと一致する。

## 3. 情報アーキテクチャ

3 つのアセンブリに分離し、責務を分ける：

- **TruthWorld** … 実座標・コライダー・レイ照射。`RadarSimulator`、各センサ（`PlayerActor` / `Beacon` / `EnemyAI`）、プレイヤー操作、ステージ進行（`StageManager` ほか）、UI トリガー類（`TutorialHintTrigger`、`EnemyActivator` 等）。
- **SensorWorld** … 測距点 `Measurement` の列を保持する `SensorBus` と共有定数。
- **Reconstruction** … `SensorBus` を読み、点群として描画する。**TruthWorld には依存しない。**

`Measurement` が保持する量：ヒット点の世界 XZ、ヒットの世界高さ Y、センサ ID、発行時刻 `timestamp`（＝波が surface に到達した時刻）。

LiDAR である以上、測距点は当たった座標そのものを含む。アセンブリ分割の意義は「描画パイプライン（Reconstruction）が物理（TruthWorld）に一切依存しない」ことを保証し、両者を `SensorBus` だけで疎結合に保つ点にある。

### 3.1 センサ ID 帯

センサ ID は次の帯で運用する。同じ kind のインスタンスが複数あっても同じ ID を共有し、点群上は「色＝種類」が成立する：

| 範囲 | 用途 | 色相帯 |
|---|---|---|
| `0` | プレイヤー | シアン固定 |
| `1..15` | ビーコン種類 (kind 別固定) | シアン〜紫 |
| `16..23` | 敵種類 (kind 別固定) | 赤〜オレンジ |
| `24..31` | 予備 | - |

shader 側の `_SensorColors` uniform 配列は 32 色対応。動的 ID プール（Acquire / Release）は持たず、SO 定義の固定値で運用する。

## 4. スキャンパイプライン

1 スキャンの処理（`RadarSimulator.Scan`）：

1. `ScanProfile` に従い、スラブごとに全方位レイ（`RaycastCommand`）を構築。各レイ原点は `emitterRadius` ぶん外へずらす。
2. `RaycastCommand.ScheduleBatch` でバッチを非同期スケジュール。main thread はここでブロックしない。
3. `LateUpdate` で完了済みバッチを引き取り、各ヒットを保留キューに積む（collider・ローカル座標・センサ原点・scan 時刻を保持）。無ヒットは捨てる。
4. 同じく `LateUpdate` で、保留中の各ヒットが波面到達条件（波の半径 ≥ センサ原点と surface 現在位置の距離）を満たした瞬間に `Measurement` として `SensorBus` に発行する。surface の現在位置を使うので、動く対象でも正しい場所に出る。

センサ ID は 0 = プレイヤー、1 以降 = ビーコン・敵。

## 5. 再構成と描画

### 5.1 SensorBus

発行された `Measurement` を保持する。`LateUpdate` で `timestamp < now − T_decay` の測距点を破棄する。発行＝波の到達時刻なので、まだ波が届いていない点はそもそも `SensorBus` には来ない（`RadarSimulator` 側の保留キューに留まる）。

### 5.2 点群レンダリング

`RadarImageRenderer` が毎フレーム、`SensorBus` の有効な測距点を 1 つの動的メッシュに流し込む。各測距点は、当たった世界座標に置いたカメラ正対のソフトな円ビルボード（加算合成）として描かれる。

- **色** … センサ ID ごとの色（`SensorPalette`）。プレイヤーはシアン、ビーコン・敵は色相環上に分散。
- **明るさ** … 鋸歯減衰 `1 − (now − timestamp) / T_decay`。発行直後が最も明るく、`T_decay` で 0。点の重なりは加算でより明るくなる。
- 描画は真の 3D 世界座標。スラブ・テクスチャ・累積グリッド・高さビン詰めは持たない。

### 5.3 カメラ

`CameraController` が以下の 2 モードを循環で切り替える（既定 FirstPerson、Z キーまたは Select / View ボタンで切替）：

- **TopDownOrtho** … オルソ斜め俯瞰。両モード共通の `_yawDeg` / `_pitchDeg` をカメラ Euler の Y / X 値として直接適用し、その forward に `-orthoDistance` 離した位置に camera を置くことでプレイヤーを視野中心に捉える（公転と注視が同時に成立）。位置は Player を追従する。突入時の姿勢は Inspector の `orthoEntryAzimuthDeg` / `orthoEntryElevationDeg` / `orthoDistance`（既定 45° / 45° / 10m）で決まり、Z 切替・ステージ入場のたびにこの値へリセットされる（前モード・前ステージで貯めた yaw/pitch は持ち越さない）。
- **FirstPerson** … 透視投影の 1 人称。プレイヤー位置 + `firstPersonHeight`（既定 0）をカメラ位置とする＝ Player ピボットに一致し、`PlayerActor` の ping 発射原点と完全に揃う（「見えているもの＝撃ったもの」）。首振り（yaw/pitch）でルックを変え、Capsule レンダラは非表示。Z 切替・ステージ入場のたびに「yaw = プレイヤー体の向き、pitch = 0」へリセットされる（ステージ設計側は StageSpawn の rotation で「最初に何を見せるか」を制御できる）。

両モード共通：

- yaw/pitch ともに左右矢印・上下矢印・右スティック・マウスで入力。pitch はモードごとに別レンジでクランプ。
- カリングマスクで World ジオメトリを除外する（地面・壁・敵・未起動ビーコンは通常描かれない）。
- 描画するのは点群（`RadarImage` レイヤ）とマーカー（`Marker` レイヤ）のみ。背景は黒。
- ステージクリア演出時のみ、カリングマスクに World を一時的に追加し、実地形を「答え合わせ」として表示する。

### 5.4 レイヤ

- **World** … 散乱体（壁・床・階段・敵・未拾得 `FieldPickup`）。レイの対象だが、通常はカメラに映らない。
- **RadarImage** … 点群メッシュ。
- **Marker** … プレイヤー、設置済みビーコン（`PlacedBeacon` / `CoreBeacon`）、爆発エフェクト。常にカメラに映る。

## 6. センサと敵

### 6.1 プレイヤー

- 身長 1.7 m
- ping 入力で自分を中心に 1 スキャン。クールタイムあり。
- 走査は扇型：既定 10 スラブ / 20 cm 間隔 / 1°/段（インスペクタ調整可）。
- `CharacterController` で移動。壁・敵に当たって止まる。ジャンプ可。
- `CameraYaw` でカメラを水平旋回。WASD はカメラ前方を基準に解釈される。
- 移動中はプレイヤー本体が進行方向を向く（停止中は最後の向きを維持）。これにより FirstPerson の Z 切替リセット時の初期視線が「直近の進行方向」に揃う。
- ping クールダウンはプレイヤー脇の縦バー（`PingGauge`）で可視化される。

### 6.2 ビーコン

プレイヤーが拾って置く能動センサ。Field では「アイテム」と「設置物」の両面を持つ。`BeaconKind` SO で挙動が定義され、`ItemKind` と 1:1 対応する。

#### 6.2.1 共通仕様

- 身長 1.8 m
- 設置すると即座に Marker レイヤの可視マーカーになり、`FieldClock` の共通 tick で自動的に全方位スキャン開始。Stage1/2 時代の「起動入力」概念はなく、出現＝稼働。
- 走査は既定 10 スラブ / 20 cm 間隔 / 1°/段。センサ回転に追従するので、傾けて設置すれば斜め走査になる。
- 拾い直しは「狙って回収（aim-based recovery）」：プレイヤーが照準を向け、`PickupOrInteract` 入力で `BeaconRecoveryController` が Physics.RaycastAll で回収する。
- 設置済みビーコン同士には最小間隔距離があり、`BeaconPlacementController.CanPlace` が `FieldManager.AllPlaced` を参照して判定する。

#### 6.2.2 BeaconKind の区分

`BeaconKind` SO に 2 つのフラグを持たせて派生挙動を分ける（フラグ運用、enum 化はしない）：

| フラグ | 意味 | 派生挙動 |
|---|---|---|
| `IsCore` | コアビーコン | リスポーン基点になる。`FieldManager.Core` に登録、1 個限定。設置時は専用 prefab、回収可だが死亡時は直近セーブ/StageSpawn フォールバック |
| `IsLure` | 囮ビーコン | `EnemyAI` が `FindClosestVisibleLure` で優先追尾。`FieldManager.AllLures` サブ登録簿で O(N_lure) の Tick 走査 |

両フラグが立つ kind は想定しない。新規 kind を増やすときは `SensorId` を 1〜15 から空き番号で選ぶ。

#### 6.2.3 同期スキャンと「ずらし高頻度化」抑止

`PlacedBeacon` は自前タイマーを持たず、`FieldClock.OnTick(tickIndex)` を購読する。各 kind は `tickMultiplier`（既定 1）を持ち、`tickIndex % tickMultiplier == 0` の tick だけ発火する。同 kind を 2 個並列に置いても**同じ tick に合流する**ので、ずらし配置で実効頻度を 2 倍にする抜け道がない。

プレイヤー ping と `CoreBeacon.BurstScan`（リスポーン直後の 1 発）はこの同期の対象外。押した瞬間に独立して `RadarSimulator.Scan` を呼ぶ。

### 6.3 アイテムと合成

`ItemKind` SO で 3 カテゴリ：

- **Beacon** … 拾うと `Inventory` に積まれ、設置入力で `PlacedBeacon` として Field に出現
- **Material** … 合成の入力に使う素材
- **Relic** … 古代遺構。拾うと `RelicCounter` が「relic_<id>」フラグを集計し、目標数（既定 5）でクリア演出

`CraftRecipe` SO は入力 `Ingredient[]` → 出力 `ItemKind` の関係を持つ。合成画面（`CraftingMenu`、C キー開閉）で `CraftingService.TryCraft` が在庫を見て可否判定 + 消費 + 追加を実行する。レシピは「素材+通常ビーコン → 広角/高所/囮等の特性ビーコン」「素材+ビーコン+遺構 → 進行キービーコン」を想定。同じ ItemKind を入力配列の複数行に書いたレシピは `CanCraft` が reject する（部分消費バグ防止）。

### 6.3 敵

円柱オブジェクトを敵として運用する。敵もセンサであり、同じレイ／遮蔽モデルで世界を見る。

- 身長 1.8 m
- スキャン間隔ごとに全方位スキャン（既定 10 スラブ / 20 cm 間隔 / 1°/段）。結果は `SensorBus` に発行され、敵の色で点群に映る（プレイヤーは敵の接近を点群で読める）。
- プレイヤー検知は遮蔽判定（敵 → プレイヤーの LOS）で行う。間に壁があれば非検知、検知距離を超えても非検知。全方位スキャンのレイがプレイヤーに到達する条件と等価。
- 移動は `NavMeshAgent` による経路探索（ステージ Scene に `NavMeshSurface` をベイクしておく）。3 状態の状態機械で動く：
  - **Idle** … 初期位置で停止。
  - **Chasing** … 直近の `lastKnownPos`（LKP）へ経路探索で接近。検知のたびに目標を更新。検知がロストしても LKP に到達するまで歩き続ける（途中ロストでは諦めない）。LKP に到達したあと、最初の失敗スキャンを引いた瞬間に Returning へ。
  - **Returning** … 初期位置へ経路探索で帰還。途中で再検知すれば Chasing へ戻る。
- 段差・回り込みは NavMesh が解決するため、起伏地形でも追跡可能。プレイヤーがジャンプ前提の高い段差を越えた場合、敵は同じ経路を辿れず代替路を探すか、見失って帰還する。
- プレイヤーが攻撃距離以内に入ると範囲爆発（半径 `blastRadius`、膨張＋フェードの加算エフェクト）。爆風がプレイヤーを捉えればゲームオーバー。外していればクールダウン後に行動を再開する。
- NavMeshAgent の半径・高さ・足元オフセットは起動時に CapsuleCollider の値から自動算出される（スケールの掛かったメッシュにフィットする）。

設計意図：遮蔽で隠れれば検知されない＝レーダの遮蔽が生存に直結する。敵は「検知した時点の座標」へ向かうので、動き続ければ的を外せる。検知が途切れれば敵は元の位置に戻るので、引きつけて引き離す・回り込んで通過する、といった戦術が成立する。

## 7. ゲームデザイン

### 7.1 シーン構成と起動フロー

3 シーンの加算ロード構成：

- **Core** … 起動から終了まで常駐。Player、Main Camera、`RadarSimulator`、`SensorBus`、`RadarImageRenderer`、`BootManager`、`Inventory`、`ProgressFlags`、`SaveRegistry`、`CraftingMenu`、`HudController`、`RelicCounter`。
- **Title** … 入口シーン。`TitleController` の ENTER 押下で `SaveSystem.HasSave()` を見て新規/続きから分岐する。
- **Field** … プレイ本編のシームレス 1 マップ。`FieldManager`、`FieldClock`、`StageSpawn`、地形、`FieldPickup`、`EnemyAI`、`EnemyActivator`、ベイク済み `NavMeshSurface`。

`BootManager` がシーン遷移を統括する：

- 起動時に Core + Title を起動
- Title の ENTER で：セーブが無ければ Field を素のまま追加ロード、セーブが有れば `PendingLoad = true` で Field 追加ロード後に `RestorePreSceneLoad` → `RestorePostSceneLoad` の順で復元
- Field 内では `StageManager` は使わず、進行は遺構収集数で判定する（`RelicCounter`）

### 7.2 拠点（コアビーコン）と探索

プレイヤーは Field を歩き回り、地上に点在する `FieldPickup`（拾える Beacon / Material / Relic）を回収する。拠点となる**コアビーコン**は通常ビーコンと素材から合成して 1 個だけ作り、Field に設置する。コアは：

- 1 個限定（既存コア在中は新規設置を拒否、移設は回収 → 再設置）
- 死亡時のリスポーン基点
- 設置時に `BurstScan` を 1 発撃ち、周囲を即座に照らす（リスポーン直後の真っ暗緩和）

通常ビーコンは「拾って置いて視界網を広げる」遊びの中核で、設置のたびに `FieldClock` 同期スキャンが追加される。種類によって視覚拡張軸（広角・高所・床向き）と敵干渉軸（囮）の役割を担う。

### 7.3 合成（クラフト）

`CraftRecipe` SO を `CraftingMenu` の Inspector 配列に登録しておく。C キーで合成 UI を開き、在庫が条件を満たすレシピの行が Craft 可能で表示される。`CraftingService.TryCraft` は：

- 在庫充足判定（`CanCraft`）
- 重複 ItemKind を含むレシピを reject（部分消費バグ防止）
- Inputs を順に `Inventory.Remove`、Output を `Inventory.Add`

合成はどこでも実行可能（拠点要件なし）。最小レシピ例：「通常ビーコン + 素材 A → 広角ビーコン」「通常ビーコン + 素材 B → 囮ビーコン（IsLure）」「通常ビーコン + 遺構 + 素材 C → 進行キービーコン」。

### 7.4 ギミック方針

- **暗黒空間** … 地形メッシュなしの Cube ベース。プレイヤーの浅い扇では遠くの壁は見えず、設置ビーコンが空間を「節点として」照らす
- **拠点視界網** … コアからの自動スキャン + 通常ビーコンの分散設置で、自分が安心して動ける半径を能動的に作る
- **囮ビーコン** … `IsLure` フラグ持ち。`EnemyAI` が `FindClosestVisibleLure` で優先追尾するため、敵の経路を意図的に逸らす戦術が成立する
- **高所/床向き** … ビーコンを傾けて設置する自由度で、隠れた穴・上層の構造を可視化する
- **遺構** … マップ上に分散配置。拾うたびに `RelicCounter` が `relic_<id>` フラグを集計し、5〜7 個でクリア演出

### 7.5 死亡条件とリスポーン

- **敵の爆発**：攻撃距離に踏み込まれた瞬間 `Explode`。爆発半径内にプレイヤーがいればゲームオーバー
- **落下死**：プレイヤーの y 座標が `killY`（既定 −10 m）を下回ると `FallDeath` がゲームオーバーをトリガー
- **リスポーン処理**（`RespawnService.Respawn`）：
  1. `FieldManager.Core` があればコア中心の半径 1.5〜4 m 内でランダム位置を 16 回までサンプリング、地面 + Y 差 + 壁重なり + LOS の 4 条件で適格判定。全失敗時はコア位置にフォールバック
  2. コア未配置時は `FieldManager.Spawn`（StageSpawn）にフォールバック
  3. リスポーン時に `SensorBus.Clear()` + `RadarSimulator.ClearPending()` で点群をリセットして「再始動感」を出す
  4. CharacterController は `enabled = false → 位置直書き → enabled = true` のパターンで安全テレポート、`PlayerController.ResetMotion()` で落下慣性を破棄
- **所持品・進行フラグは失わない**。点群は復元しないが、コアの `BurstScan` で周辺は即座に再形成される

### 7.6 セーブ/ロード

Phase 9 で実装。原則は：

- **手動セーブのみ**：プレイヤーが**コアビーコン隣接**で `SaveAtCore` 入力を押した瞬間にスロットへ書き出し（`SaveInputHandler`）
- **複数スロット**：`SaveSystem` がスロット番号で複数セーブを保持
- **原子性**：`JsonUtility` で tempfile を書いてから rename。書込中クラッシュで前世代を壊さない
- **対象**：`ProgressFlags`、`Inventory` の中身、`SelectedBeaconKind`、設置済み `PlacedBeacons`（コア含む）
- **対象外**：プレイヤー位置（コア周辺リスポーン）、点群（コアの BurstScan で再形成）、敵の現位置（再 spawn）

タイトルの「続きから」は最新スロットを `BootManager.PendingLoad = true` で読み込み、Field シーン onLoaded callback で `RestorePreSceneLoad`（Inventory + Flags）→ Field のシーンロード → `RestorePostSceneLoad`（PlacedBeacons の Instantiate）→ RespawnService.Respawn の順に進む。`FieldPickup.Awake` と `EnemyActivator.Awake` が ProgressFlags を見て自己 Destroy / 起動状態にジャンプする慣用句で、シーン状態が自動的にセーブ時と一致する。

### 7.7 進行とクリア

- `RelicCounter` が `relic_*` フラグの個数を集計し、`targetCount`（既定 5）を超えた瞬間に `IsAllClear = true`
- `IsAllClear` 時は画面中央に `ALL CLEAR` 演出
- 旧 Stage 式の `STAGE CLEAR` 演出と「次ステージ自動遷移」は持たない（シームレス 1 マップなので）
- 旧 `s_restartStageIndex` の static 復帰機構は廃止。`SaveSystem.HasSave()` が進行状態の唯一の真実

## 8. 入力

Unity Input System を採用、キーボード+マウスとゲームパッドの両対応。

| アクション | キーマウ | ゲームパッド | 用途 |
|---|---|---|---|
| 移動 | WASD | 左スティック | プレイヤー移動 |
| ジャンプ | Space | 北ボタン (Y) | ジャンプ |
| Ping | 左クリック | 右トリガー | プレイヤーの即時スキャン |
| カメラ水平回転 (yaw) | ← / → ／ マウス X 移動 | 右スティック X | カメラ yaw |
| カメラ垂直回転 (pitch) | ↑ / ↓ ／ マウス Y 移動 | 右スティック Y | カメラ pitch |
| カメラモード切替 | Z | Select / View ボタン | FirstPerson / TopDownOrtho 循環 |
| PlaceBeacon | 右クリック | 右ボタン (B) | 単押し=即設置、長押し=視野内カーソル設置 |
| CycleBeacon | Q | 左ボタン (X) | 選択中ビーコン kind の巡回 |
| PickupOrInteract | E | 南ボタン (A) | 照準先のビーコン回収・拾える物との接触インタラクト |
| OpenInventory | Tab | Select | インベントリ表示（Phase 10 で UI Toolkit 化予定） |
| OpenCrafting | C | - | 合成メニュー開閉 |
| Pause | Esc | Start | 一時停止 |
| SaveAtCore | K | - | コア隣接時のみ手動セーブ（Mac F5 = Dictation 衝突回避で K キー採用） |
| リスタート（ゲームオーバー時） | R | - | ゲームオーバー画面で押すと Field 再ロード |

移動方向はカメラの向きを水平面に投影した基準で解釈する。

旧 Stage1/2 で使った Enter（ビーコン起動）は廃止。Field のビーコンは設置と同時に稼働するため、起動キーの概念がない。

## 9. パラメータ一覧

代表値（多くはインスペクタで調整可）：

| カテゴリ | パラメータ | 値 |
|---|---|---|
| 共通 | レイ本数／スラブ | 240 |
| 共通 | レイ角度分布 | 黄金角 |
| 共通 | 最大レイ距離 | 60 m |
| 共通 | レーダ伝播速度 | 60 m/s（インスペクタ調整可） |
| 共通 | 残像減衰 T_decay | 1.0 s（鋸歯、出現時刻からカウント） |
| プレイヤー走査 | スラブ | 10 枚 / 20 cm 間隔 |
| プレイヤー走査 | 仰角ステップ | 1°/段（既定、可変） |
| プレイヤー | ping クールタイム | 既定 0.3 s（可変） |
| プレイヤー | 移動速度 / ジャンプ高 | 既定 4 m/s / 1.2 m |
| プレイヤー | 落下死しきい値 | y < −10 m（可変） |
| カメラ回転 | キー／スティック速度 | yaw 60°/s, pitch 90°/s |
| カメラ回転 | マウス感度 | yaw / pitch 各 0.2°/px |
| カメラ pitch クランプ | 1 人称時 | −80° 〜 +80° |
| カメラ pitch クランプ | オルソ時の絶対仰角 | 5° 〜 89° |
| カメラ 1 人称 | 視点高さ / FOV | Player ピボット一致（既定 0 m）/ 90° |
| ビーコン走査 | スラブ | 10 枚 / 20 cm 間隔 |
| ビーコン走査 | 仰角ステップ | 1°/段（既定、可変） |
| ビーコン | FieldClock パルス間隔 | 既定 0.5 s（共通 tick、kind 別 `tickMultiplier` で間引き） |
| ビーコン | 最小設置間隔 | `BeaconKind` ごとに Inspector 調整 |
| 敵走査 | スラブ | 10 枚 / 20 cm 間隔 |
| 敵走査 | 仰角ステップ | 1°/段（既定、可変） |
| 敵 | スキャン間隔 / 検知距離 | 既定 1 s（FieldClock 同期）/ 30 m |
| 敵 | 移動速度 | 既定 2 m/s |
| 敵 | 攻撃距離 / 爆発半径 / 再攻撃間隔 | 既定 2 m / 3 m / 2 s |
| 敵 | ロスト判定 | LKP に到達後、次のスキャンで検知できなければ帰還 |
| 敵 | 到達判定距離 | 既定 0.5 m |
| リスポーン | コア周辺半径 | 最小 1.5 m 〜 最大 4 m |
| リスポーン | 高低差ガード | コアとの Y 差 ≤ 2 m |
| リスポーン | サンプリング試行回数 | 最大 16 回 |
| 進行 | クリア遺構数 | `RelicCounter.targetCount` 既定 5（プランの 5〜7 想定） |
| 描画 | 点サイズ / 上限点数 | 既定 0.12 m / 40000 |
| カメラ | 種別 | TopDownOrtho（オルソ斜め俯瞰、位置追従）／ FirstPerson（透視 1 人称）の 2 モードを Z キー or Select ボタンで循環 |
| カメラ | 世界描画 | 通常なし（点群・マーカーのみ）。旧 Stage 演出の World 一時公開は Field では使用しない |

## 10. 現状と今後

Field シームレス 1 マップへの移行は Phase 単位で進められている。Phase 1〜9 まで実装完了、Phase 10〜11 が残タスク。

### 実装済み（Phase 1〜9）

- **基盤**
  - 全方位測距スキャン（プレイヤー / ビーコン / 敵で共通形状の扇）、走査プロファイル、センサ回転追従
  - TruthWorld / SensorWorld / Reconstruction のアセンブリ分離（一方向依存）
  - 3D 点群レンダリング、レーダ波の出現遅延、鋸歯減衰、センサ別配色
  - SensorId 帯設計：プレイヤー=0、ビーコン kind=1..15、敵 kind=16..23、shader uniform 32 色対応
- **シーン**
  - Title / Core / Field の 3 シーン構成、`BootManager` による遷移統括
  - Title「新規」「続きから」の `SaveSystem.HasSave()` ベース分岐
- **Field の中核**
  - `FieldManager` 中央登録簿（Core / AllPlaced / AllLures / Spawn キャッシュ）
  - `FieldClock` 同期 tick による「ずらし高頻度化」抑止
  - `RespawnService` のコア周辺ランダム + LOS 通過判定
- **ビーコン**
  - `BeaconKind` SO + `PlacedBeacon` + `CoreBeacon` + `BeaconPlacementController`（単押し即設置 + 長押しカーソル設置 + 最小間隔チェック）
  - 照準ベースのビーコン回収
- **アイテム・合成**
  - `ItemKind` SO + `Inventory`（`Dictionary<ItemKind, Slot>` で O(1) lookup）+ `FieldPickup`（永続化 id pattern）
  - `CraftRecipe` SO + `CraftingService`（CanCraft 重複検出付き）+ `CraftingMenu`
- **進行・遺構**
  - `ProgressFlags`（HashSet + event）+ `RelicCounter`（`relic_*` 集計、`targetCount` で ALL CLEAR）
  - prefix 名前空間：`pickup_` / `relic_` / `activator_`
- **敵**
  - 既存 `EnemyAI` を `FieldClock` 同期に切替、`FindClosestVisibleLure` で Lure 優先追尾
  - `EnemyActivator` を `activator_<id>` フラグで永続化
- **セーブ/ロード**
  - 4 ファイル構成：`SaveData` / `SaveSystem` / `SaveRegistry` / `SaveService`
  - Capture を CaptureCore + CaptureField に分割、Restore を Pre/Post に分割
  - tempfile + atomic rename、複数スロット対応
- **死亡条件**
  - 敵爆発、落下死、リスポーンでコア周辺復帰（所持品・進行は喪失しない）

### 残タスク

- **Phase 10 — UI Toolkit 移行**: 現状すべての UI（HudController / CraftingMenu / TitleController / GameOverController / RelicCounter / PingGauge）が IMGUI。HUD、インベントリ、合成、タイトル、ポーズメニューを UI Toolkit で組み直す
- **Phase 11 — マップ・コンテンツ**: 暗黒 Cube マップへの地形配置、遺構 5〜7 個の配置、敵バリエーション、レシピ拡充、サウンド
- **将来検討**
  - 補助軸・環境干渉軸のビーコン特性追加（`BeaconKind` SO に空フィールドは確保済み）
  - オートセーブ拡張（`SaveSlot` 抽象で AutoSlot / ManualSlot 差し込み可能に）
  - Title 演出ビーコン（SIGNALSIGHT 文字浮上）の `PlacedBeacon` 派生化
  - 演出磨き（点サイズ・明るさ・伝播速度・残像時間・スキャン本数、爆発エフェクト）
