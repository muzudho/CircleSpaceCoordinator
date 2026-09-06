# JSON交換形式 version 1 設計

`circleLayouts[].temporaryPlacements` は通路などへの仮置き位置を保持する省略可能な配列です。各要素は `participantId`、`occupiedCells`、`scoringPosition`、任意の `combinedSpaceId` を持ちます。省略時は空配列として読み込みます。座標は会場外の仮置きも扱うため負数を含む整数を許可します。正式な `assignments` とは参加者・セルが重複せず、机・柱とも重ならないことを検証します。仮置きは評価・席番号出力の対象には含めません。合体IDは仮置き中も保持しますが、同じ机を使用する制約は両者が正式配置に戻った時点で検証します。

## 目的

C#アプリケーションとPython製Excelブリッジの間で、会場、参加者、配置案、評価設定を交換する。

正式なスキーマ:

- [`schemas/circle-space-project-v1.schema.json`](../../../schemas/circle-space-project-v1.schema.json)
- [匿名サンプル](../../../examples/circle-space-project-v1.example.json)

## JSONを正規形式にする理由

CSVは参加者一覧のような一枚の表には向いているが、次の情報を一つの交換単位として表現しにくい。

- 二次元会場
- 複数種類の机と占有セル
- 複数の配置案
- 参加者と机の割当て
- 評価項目別の重みマップ

参加者一覧だけをCSVで入出力することは許容する。CSVから取り込んだ後は、このJSONモデルへ変換する。

## バージョン

ルートの `schemaVersion` は必須で、version 1では文字列 `"1.0"` とする。

破壊的変更ではmajorを上げる。項目の任意追加など、既存readerが安全に拒否または無視できる変更ではminorを上げる。ただしversion 1のスキーマは `additionalProperties: false` を基本とするため、readerとschemaを同時に更新する。

## 座標系

- 原点は会場グリッドの左上 `(0, 0)`。
- `x` は右へ増える。
- `y` は下へ増える。
- 座標は0以上の整数。
- 有効範囲は `0 <= x < venue.width`、`0 <= y < venue.height`。

JSON Schemaだけでは、座標が会場内かどうかを完全には検証できない。C# CoreとPython bridgeの双方で意味検証を行う。

Excelとの対応はbridgeだけが持つ。例えばExcelの `C2` をJSONの `(0, 0)` とするかどうかは、`VenueArea` の取込み規則で決める。Coreへ `C2` のようなExcel座標を持ち込まない。

## 会場

`venue.width` と `venue.height` がグリッド全体を定める。グリッド内は原則として配置可能で、`blockedCells` にあるセルは配置禁止とする。

`zones` は入口側、壁側、本部周辺などの論理的な領域を表す。version 1では分類・表示・将来の制約指定に使い、評価式へ暗黙には影響させない。

## 机

`deskTypes` は机の形を相対座標の集合 `footprint` で表現する。

標準の2セル机:

```json
{
  "id": "standard-desk",
  "footprint": [
    { "x": 0, "y": 0 },
    { "x": 1, "y": 0 }
  ],
}
```

`deskPlacements.anchor` を基準にfootprintを置き、`orientation` に従って90度単位で回転する。回転規則は次のとおり。

```text
north: ( x,  y)
east:  (-y,  x)
south: (-x, -y)
west:  ( y, -x)
```

回転後に負方向へ広がる机も表現できる。占有セルが会場外、`blockedCells`、別の机と重なる場合は配置違反。

## 参加者

公開形式では個人情報を必須にしない。交換上の識別には `id` を使い、画面表示には `displayName` を使う。

`genreId` はイベント側のジャンル識別子を保持し、ジャンル色の表示と一般参加者評価に使用する。省略可能であり、値がない参加者はジャンル集計に含めない。

トップレベルの任意配列 `genreStyles` は、ジャンルごとの表示設定を保持する。各要素は `genreId`、`primaryColor`、`secondaryColor`、`pattern` を持つ。設定がないジャンルにはアプリの既定色・既定網掛けを適用する。

`features` は評価項目IDをキーとする数値辞書。評価項目を増やしてもparticipantの構造を変更しなくてよい。

```json
"features": {
  "shop-items": 12,
  "paper-book": 1
}
```

値が存在しない評価項目は `0` として扱う。真偽値もJSONの `true` / `false` ではなく `1` / `0` に正規化する。

`requiredCellCount` は参加者が必要とするスペースセル数。1スペース申込みなら1、2スペース申込みなら2となる。割当てられた `occupiedCells` の数と一致しなければ配置違反。

## 配置案

`plans` の各要素が旧 `XPlan0`、`XPlan1` などに対応する。

机そのものは案ごとに位置が異なる可能性があるため、`deskPlacements` は各planの内側に置く。標準机は2セルを占有でき、その左右のセルへ別々の参加者を割り当てることもできる。

参加者は `assignments.occupiedCells` により、一つ以上のスペースセルへ割り当てる。全てのoccupied cellは、配置済み机のfootprintに含まれていなければならない。

`scoringPosition` は、旧ExcelでサークルIDが入力されているセルに対応する主セル。必須項目であり、必ずその参加者の `occupiedCells` に含まれる。

配置案は任意の `islandConnectors` と `facingRegions` を持てる。前者は島をつなぐ2台の机ID、後者は向かい合わせ通路領域の対角2セルを保存する。省略時は空配列として扱う。

前回終了時のUI作業状態は、イベントの業務データではなくアプリケーション設定 `schemaVersion: 1.1` の `projectWorkingStates` にプロジェクト別で保存する。選択配置案、上段の編集モード、分析スイッチ、ズーム、表示原点を含む。終了時に自動保存し、次回起動時に存在する配置案と認識可能なモードだけを復元する。

2スペース参加者は2セルを占有するが、属性値はサークルIDが入っている主セルの位置で一度だけ採点する。Excel bridgeは、サークルIDを読んだセルを `scoringPosition` として出力する。

### 合体スペース

「合体スペース」は、2サークルが同じ物理机を合同で使う配置を意味する。該当する2つのassignmentへ同じ `combinedSpaceId` を設定する。

```json
{
  "participantId": "circle-a",
  "occupiedCells": [{ "x": 10, "y": 4 }],
  "scoringPosition": { "x": 10, "y": 4 },
  "combinedSpaceId": "combined-01"
}
```

同じ `combinedSpaceId` は、同一plan内でちょうど2サークルが共有する。両者のoccupied cellを合わせた全セルが、一つの `deskPlacement` のfootprintに含まれなければならない。隣接していても別々の机に属する場合は配置違反とする。

## 評価項目と重みマップ

`evaluation.features` が評価項目を定義する。

- `scale`: 項目別の生の総和へ掛ける係数
- `offset`: scale適用後に加える値
- `overallWeight`: 項目別評価を総合評価へ加える際の係数

`weightMaps` は評価項目ごとの位置別重み。記載のないセルには `defaultWeight` を使う。

評価式:

```text
rawSum(feature, plan)
  = Σ participant.features[feature]
      × weightMap[feature, assignment.scoringPosition]

adjustedScore
  = rawSum × scale + offset

weightedScore
  = adjustedScore × overallWeight

totalScore(plan)
  = Σ weightedScore
```

この式の `adjustedScore` までは既存Pythonの `sum * scale + offset` に対応する。`overallWeight` と `totalScore` は、総合比較をC#側で明示するために追加した。

## 派生結果

`evaluationResults` は任意。キャッシュおよびExcel export用の派生データであり、正本ではない。

readerは次の場合に結果を破棄して再計算する。

- 配置案が変更された。
- 参加者属性が変更された。
- 重みマップ、scale、offset、overallWeightが変更された。
- 結果と再計算値が一致しない。

## Schemaで検証できない規則

C# CoreとPython bridgeで次を検証する。

- IDが種類ごとに一意である。
- 参照先IDが存在する。
- featureとweight mapが対応する。
- 座標が会場内にある。
- blocked cellへ机を置いていない。
- 机同士が重なっていない。
- 参加者の必要セル数とoccupied cell数が一致する。
- occupied cellが配置済み机の占有セルに含まれる。
- 同じセルを複数参加者へ重複割当てしていない。
- `scoringPosition` が参加者のoccupied cellに含まれる。
- 同じcombined space IDをちょうど2参加者が共有している。
- 合体スペースの全セルが同じ物理机に含まれる。
- 数値がNaNまたはInfinityではない。

## セキュリティーと公開データ

- JSONへ氏名、メールアドレス、電話番号、住所を標準項目として設けない。
- 公開サンプルは架空データだけを使う。
- 業務用変換結果は `data/private/` または `data/exchange/` へ出力する。
- import時に未知のプロパティを黙って保持しない。
- 読込みサイズ、会場寸法、配列件数には実装側でも上限を設ける。

## version 1で未決の事項

次は既存Excelと実際の運用を確認してから決める。

1. 机の向きが評価値に影響するか。
2. 壁、入口、柱、非常口などをzone以外の固定オブジェクトとして持つか。
3. ジャンル隣接、参加者同士の距離、通路混雑など、セル単体ではない評価をどう追加するか。
4. Excelの総合評価式に既存の係数があるか。

これらはCoreの初期モデルを拡張するときに順次確定する。まず既存5項目の位置別加重和を再現する。
