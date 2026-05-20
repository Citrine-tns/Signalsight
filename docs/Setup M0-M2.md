# M0〜M2 シーン構築手順

生成済みコードを動かすための Unity エディタ操作手順。スクリプトは
`Assets/Scripts/` 配下に 3 つのアセンブリ（情報の関所 §4）として分割済み：

- `Signalsight.SensorWorld` … `Measurement` / `SensorBus` / 定数（参照なし）
- `Signalsight.TruthWorld` … `RadarSimulator` / `Beacon` / プレイヤー（SensorWorld + InputSystem を参照）
- `Signalsight.Reconstruction` … 累積グリッド・描画（SensorWorld のみ参照 ＝ TruthWorld には触れない）

---

## 0. プロジェクト設定の確認

- **Project Settings > Player > Active Input Handling** が `Input System Package` または `Both` であること。
- Unity がスクリプトをコンパイルし、エラーが出ていないことをコンソールで確認。

## 1. レイヤを 3 つ作る

`Project Settings > Tags and Layers` で User Layer に追加：

- `World` … 散乱体（壁・床・人体）。レーダ像にのみ現れ、カメラには描かれない。
- `RadarImage` … スラブクアッド（自動生成）。
- `Marker` … プレイヤー／ビーコンの 3D マーカー。

## 2. テストシーン（§8）

新規シーン、または SampleScene を流用。Directional Light は残してよい。

| オブジェクト | 作り方 | レイヤ | 備考 |
|---|---|---|---|
| Ground | Plane（Scale 4 程度） | `World` | 床はスラブと平行で「ほぼ映らない」のが正しい挙動 |
| Wall ×3〜4 | Cube を壁状にスケール | `World` | 高さは 2m 程度（スラブ範囲をまたぐ） |
| Human | Cylinder（径 0.4 / 高さ 1.8） | `World` | 人型 |
| Player | **Capsule**（高さ 1.7） | `Marker` | コライダーは付いたままで可（World 外なので自己ヒットしない） |
| Beacon1 / Beacon2 | Sphere か Cube（小さめ） | `Marker` | プレイヤーから 8〜15m 離して 2 個配置 |

シーン全体を**原点から概ね ±20m 以内**に収めること（像が 48m 四方の固定領域のため）。

色コード（§6.4）を合わせるなら、マテリアル色を手動で：Player=シアン / Beacon1=オレンジ / Beacon2=緑。

## 3. マネージャ用オブジェクト

空の GameObject を 3 つ作る：

- **RadarSimulator** … `RadarSimulator` をアタッチ。インスペクタの `World Mask` を **`World` だけ**に設定。
- **SensorBus** … `SensorBus` をアタッチ。
- **RadarImage** … 原点（0,0,0）に置き、`RadarImageRenderer` をアタッチ。起動時にスラブ 10 枚を子として自動生成する。

## 4. コンポーネントのアタッチと参照配線

- **Player** に `PlayerController` と `PlayerActor` をアタッチ。
  - `PlayerActor.Simulator` ← RadarSimulator オブジェクトをドラッグ。
- **Beacon1** に `Beacon` をアタッチ。
  - `Simulator` ← RadarSimulator / `Receiver` ← Player / `Tx Id` = `1`。
- **Beacon2** も同様。`Tx Id` = `2`。位相オフセット実験をするなら `Start Delay` = `0.5`。

## 5. カメラ（§6.3）

Main Camera を選択：

- `Projection` = **Orthographic**、`Size` = 26 程度。
- `Transform Rotation` = `(40, 45, 0)` あたり（斜め俯瞰）。
- `Culling Mask` = **`RadarImage` と `Marker` のみ**（`World` と `Default` を外す → 世界ジオメトリは描かれない）。
- `Environment > Background Type` = Solid Color、色は黒。
- `CameraFollow` をアタッチし、`Target` ← Player。

## 6. 再生して確認（マイルストーン完了条件）

- **M0**: 何も光らないが、黒画面にプレイヤー／ビーコンのマーカーが見え、WASD で移動できる。
- **M1**: 左クリック / スペース / RT で ping → 自機中心に 10 段の円が浮かび約 1 秒で減衰。
- **M2**: ビーコンが 1 Hz で心拍を打ち、楕円群が常時更新。ping の円と楕円の交差で物体位置が輝点として立ち上がる。

---

## チューニングどころ（インスペクタ）

`RadarImageRenderer` の「交点の強調」（再生中に変更可。これが見え方の主役）：

- `Curve Brightness` … 曲線層（Tx 色の円）の明るさ。0 で円が消え交点だけに。
- `Base Gain` … 円・楕円そのものの明るさ。小さく保つと円が沈む。
- `Cross Gain` … 異なる Tx の軌跡が交わる「交点」の明るさ。大きくすると交点が強く立つ。

その他：

- `RadarImageRenderer.Band Sigma Meters` … 円のボケ幅（距離分解能）。
- `RadarImageRenderer.Fade Tau` … 残像の長さ。
- `RadarImageRenderer.Slab Visual Spacing` … スラブ積層の見やすさ。
- `RadarSimulator.Rays Per Slab` … 1 スラブのレイ本数（既定 240。重ければ下げる）。
- `RadarSimulator.Path Length Sigma` … 測定ジッタ（M3 で SNR 由来 σ に置換予定）。

## 既知の制約（M0〜M2 の割り切り）

- 残像減衰は厳密な鋸歯ではなく指数減衰で近似（§6.2 コメント参照。M4 で調整）。
- 散乱は簡易モデル。鏡面/拡散の分離・レーダ方程式・マルチパス・GDOP は M3。
- プレイヤーは壁をすり抜ける（コライダー押し戻し未実装）。
- ビルド時にスラブが黒/ピンクなら `RadarSlab` シェーダを Always Included Shaders に追加。
