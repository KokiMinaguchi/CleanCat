# VoxelDig セットアップ手順

CSV のボクセルモデル（kinoko.csv）を土の塊に埋めて、クリックで掘り出す 3D ゲームです。
Unity 6 / URP / New Input System / R3 / UniTask / DOTween Pro / uGUI + TextMeshPro 前提。

## 遊び方

| 操作 | 内容 |
|---|---|
| 左クリック（長押しで連続） | 掘る |
| 右ドラッグ | カメラ回転 |
| ホイール | ズーム |
| 1 / 2 | ハンマー / ノミ 切り替え |
| R | やり直し（毎回ちがう形のブロック） |

- **ハンマー**：広く削れるが力は弱い（石は3回）。お宝に当てるとダメージ大
- **ノミ**：1マスだけ。石も1回で壊せる。お宝に当ててもダメージ小
- お宝に**接している土を全部取り除く**とクリア → 残りの土が崩れ落ちてお宝が出てくる
- お宝を叩くとダメージが溜まり（叩いたマスは暗くなる）、上限に達すると失敗
- まだ土に埋まっているお宝は土が守ってくれるので、ダメージを受けるのは「直接叩いたマス」と「露出しているマス」だけ
- カーソルが赤いときは、お宝を直接狙っている合図

## ファイル構成

```
VoxelDig/
├─ Scripts/
│  ├─ VoxelCsvLoader.cs  CSV 読み込み
│  ├─ VoxelGrid.cs       ボクセルデータと土ブロックの生成
│  ├─ VoxelChunk.cs      チャンク単位のメッシュ生成
│  ├─ DigBlock.cs        掘る処理・クリア/失敗判定・クリア演出
│  ├─ DigTool.cs         道具の性能
│  ├─ DigController.cs   入力・Raycast・カーソル
│  ├─ OrbitCamera.cs     カメラ（回転・ズーム・揺れ）
│  ├─ DigEffects.cs      破片パーティクルと効果音
│  └─ DigHud.cs          UI
├─ Shaders/VoxelVertexColorLit.shader  頂点カラー用 URP シェーダー
├─ Input/VoxelDigControls.inputactions
└─ kinoko.csv
```

## セットアップ

### 1. インポート
フォルダごと `Assets/VoxelDig/` に入れる。`.csv` は自動で TextAsset になります。

### 2. マテリアル
- `Create > Material` で `VoxelMat` を作り、Shader を **VoxelDig/VertexColorLit** にする
- カーソル用に `CursorMat` を作る：Shader = `Universal Render Pipeline/Unlit`、Surface Type = **Transparent**

### 3. ブロック
1. 空の GameObject `DigBlock` を (0,0,0) に置き、**DigBlock** を追加
2. `Voxel Csv` に kinoko.csv、`Voxel Material` に VoxelMat を設定
3. レイヤー `DigBlock` を作って設定（子のチャンクにも自動で同じレイヤーが付きます）

### 4. カメラとライト
- Main Camera に **OrbitCamera** を追加し、`Block` に DigBlock を設定
- Directional Light は Shadow Type を Soft Shadows に（影が付いて凹凸が見やすくなります）

### 5. カーソル
- `3D Object > Cube` を作って `DigCursor` に改名、CursorMat を設定（コライダーはスクリプトが自動で無効化します）

### 6. ゲームシステム
空の GameObject `GameSystems` を作り、以下を追加：

- **DigController**：Block / Target Camera / Orbit Camera / Effects / Dig Cursor を設定。`Block Layer` は DigBlock レイヤーのみ
- **DigEffects**：Block と Debris Material（VoxelMat で OK）、効果音を設定（音は無くても動きます）
- **PlayerInput**：
  - Actions = `VoxelDigControls`、Default Map = `Dig`
  - Behavior = **Invoke Unity Events**
  - Events > Dig に以下を登録

| Action | 登録先 |
|---|---|
| Point | DigController.OnPoint |
| Dig | DigController.OnDig |
| SelectTool1 | DigController.OnSelectTool1 |
| SelectTool2 | DigController.OnSelectTool2 |
| Restart | DigController.OnRestart |
| Orbit | OrbitCamera.OnOrbit |
| Look | OrbitCamera.OnLook |
| Zoom | OrbitCamera.OnZoom |

### 7. UI（uGUI + TextMeshPro）
Canvas を作り（EventSystem は **InputSystemUIInputModule** にする）、**DigHud** を追加して以下を作って設定：

- 発掘率：TMP テキスト + Image（Image Type = Filled, Horizontal）
- ダメージ：TMP テキスト + Image（同上）
- 道具名：TMP テキスト、道具ボタン ×2（ハンマー・ノミの順）
- リザルト：CanvasGroup を付けたパネル（タイトル TMP・詳細 TMP・リトライボタン）

※ 日本語を表示するには、日本語フォントの TMP Font Asset が必要です。

## 調整ポイント

| 場所 | 項目 | 効果 |
|---|---|---|
| DigBlock > Generation | padding / minCover | 土の厚さ |
| | stoneThreshold | 下げると石が増える（難しくなる） |
| | shapeNoiseScale | ブロックの外形のデコボコ |
| DigBlock | maxDamage | 失敗までの余裕 |
| | voxelSize | 1マスの大きさ（変えたらカメラ距離も調整） |
| DigController > Tools | radius / power / treasureDamage | 道具の性能。要素を増やせば道具も増やせる |

## 別の CSV を使うとき
`# VoxelCSV v1` 形式（`size,X,Y,Z` 行 + `x,y,z,RRGGBBAA` 行）なら DigBlock の `Voxel Csv` を差し替えるだけで使えます。Y 軸が上向きの前提です。
