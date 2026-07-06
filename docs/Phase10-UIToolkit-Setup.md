# Phase 10 — UI Toolkit セットアップ手順

Phase 10（IMGUI → UI Toolkit 全面移行）の **初回 Unity Editor 手作業** をまとめる。
このドキュメントは PoC (RelicCounter) の動作確認後に他 UI（HudController / CraftingMenu /
TitleController / GameOverController / PingGauge）も同じパターンで増やしていく前提。

---

## 1. 初回だけ: PanelSettings asset を作る

UI Toolkit の全 UIDocument は **1 つの PanelSettings asset** を共有する。これは Unity Editor の
メニューからしか作れない。

1. Project ウィンドウで `Assets/UI/` を開く
2. 右クリック → `Create > UI Toolkit > Panel Settings Asset`
3. ファイル名を **`SignalsightPanelSettings`** にする
4. 生成された asset を選択し、Inspector で以下を設定（推奨値）：

| フィールド | 値 |
|---|---|
| Theme Style Sheet | **`UnityDefaultRuntimeTheme` のまま**（理由は注記参照） |
| Scale Mode | Constant Pixel Size（既定） |
| Reference Resolution | 1920 × 1080（任意） |
| Match | 0.5 |
| Sort Order | 0 |

### 注記: Theme 欄を `SignalsightTheme.uss` に差し替えようとしても**ドラッグできない**

PanelSettings の `Theme Style Sheet` 欄は Unity 2022 以降 **`.tss` (Theme Style Sheet asset)
しか受け付けない**仕様。`.uss` ファイルは type が違うので drop しても反映されない。

代わりに **各 UXML が個別に Theme.uss を `<Style>` で参照する** 形を取る：

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="project://database/Assets/UI/Theme/SignalsightTheme.uss"/>
    <Style src="project://database/Assets/UI/MyComponent/MyComponent.uss"/>
    ...
</ui:UXML>
```

新規 UI を増やすときは Theme.uss の `<Style>` 行を **必ず** 1 行目に入れる。これで `.sig-*` class
が使えるようになる。

---

## 2. RelicCounter を UI Toolkit に切り替える

### 2a. 既存 RelicCounter GameObject を Field シーンで探す

Hierarchy で `RelicCounter` を持つ GameObject を選択（Core シーンか Field シーンに配置済み）。

### 2b. 必要なコンポーネントを揃える

`RelicCounter.cs` を `[RequireComponent(typeof(UIDocument))]` にしたので、Inspector で
`Add Component` を押せば UIDocument が自動付与される。または Inspector に出ている黄色
警告から「Add UIDocument」を選ぶ。

### 2c. UIDocument を設定する

UIDocument コンポーネントの Inspector：

| フィールド | 値 |
|---|---|
| Panel Settings | `Assets/UI/SignalsightPanelSettings` を drag & drop |
| Source Asset | `Assets/UI/RelicCounter/RelicCounter.uxml` を drag & drop |
| Sort Order | 0 |

### 2d. Play する

- 遺構を 0/5 の状態で右上に `遺構: 0 / 5` が出ること
- 拾うたびに数字が更新されること
- 5 個目を拾った瞬間に画面中央に `ALL CLEAR` が出ること

文字色が白でなくシアンや、サイズが想定と違ったら `Theme/SignalsightTheme.uss` の
`--sig-font-xl` 等 CSS 変数を編集する。コードに触らず uss だけで見た目調整できる。

---

## 3. トラブルシューティング

### 3a. 「Theme Style Sheet が反映されない」

UIDocument が rootVisualElement を組み立てる時に PanelSettings の theme を読む。
**Panel Settings が空のままだと theme は適用されない**。手順 1 を完了させて asset を
作り直す。

### 3b. 「`遺構: 0 / 0` が出ない、空白だけ」

UXML の `<Style src="...">` で uss を参照しているが、UXML が読み込めていない可能性。
Console に `Asset not found` 系のエラーが出ているか確認。`Source Asset` を再 drag & drop
する。

### 3c. 「右上ではなく中央に出る」

`RelicCounter.uss` の `.rc-status { position: absolute; right: 20px; top: 20px }` が
効いていない場合、親要素（`.sig-overlay`）の `position: absolute` が無いと子の絶対位置が
解釈されない。**Theme.uss が読み込まれていない可能性**。手順 1 で Panel Settings の
Theme Style Sheet が空でないか再確認。

---

## 4. 他 UI を同じパターンで移行する手順（PoC 検証後の継続作業）

1. `Assets/UI/<ComponentName>/<ComponentName>.uxml` を作る（HTML 相当の構造）
2. `Assets/UI/<ComponentName>/<ComponentName>.uss` を作る（個別レイアウト、色は Theme 任せ）
3. 対応する C# を `[RequireComponent(typeof(UIDocument))]` + `OnEnable` で要素キャッシュ + 状態変化時に `RefreshUI` する形に書き換える
4. シーンの当該 GameObject に UIDocument を追加して uxml と PanelSettings を assign

### 移行候補と優先度

| UI | 規模 | 備考 |
|---|---|---|
| **RelicCounter** | 小 | 本ドキュメントの PoC |
| **PingGauge** | 中 | FirstPerson は screen-fixed、Ortho は player 追従。後者は `style.left = ...` の per-frame 更新が必要 |
| **HudController** | 中 | Inventory.Slot のリストを ListView または手動 Add で表示 |
| **CraftingMenu** | 中 | open/close を `style.display` で切替、Recipe ごとに Button |
| **GameOverController** | 小 | 半透明オーバーレイ + テキスト |
| **TitleController** | 中 | 3 タブ（新規 / 続きから / オプション）+ スロット選択 |
| **PauseController** | 中 | 新規。Pause メニュー + Save / Crafting / Options へのエントリ |

---

## 5. 完了条件

- 6 UI 全てが UI Toolkit 化
- `OnGUI` 残置が `RadarSimulator` 等のデバッグ表示以外でゼロ
- Conventions.md の「UI 描画の方針」を「UI Toolkit を採用」に書き直し
