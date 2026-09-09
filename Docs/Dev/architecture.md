# Architecture

## ヘッドレス化の方針（2026-09-09）

エディター GUI、エディターエンジン、スペース配置の思考エンジンを分離し、GUI → エディターエンジン → 思考エンジンを gRPC で接続します。エディターエンジンは GUI なしで起動できる独立したプログラムです。CLI も同じ API を利用します。

[ヘッドレスエンジンと gRPC](設計/ヘッドレスエンジンとgRPC.md) に現在の責務・契約・起動方法をまとめています。GUI の gRPC 接続への切替を完了し、編集・計算の実装への参照はビルドで禁止しています。Core は共有データ型、Calculations は検証・評価、EditorClient は通信を担当します。以下の構成説明は分離前の経緯を含み、現在の境界は上記設計書を正とします。

## 方針

新しいアプリケーションの本体はC#で統一し、PythonはExcelアダプターとして残します。

Excelは提出形式として有用ですが、会場モデルや評価式がExcelのセル座標へ直接依存すると、画面編集、自動配置、テストが難しくなります。そこで、内部データとExcel表現を分離します。

```text
MonoGame Desktop
      |
Application use cases
      |
Core domain and evaluator
      |
JSON / CSV exchange contract
      |
Python openpyxl bridge
      |
Input .xlsm / submitted workbook
```

## Project responsibilities

### Core

- 会場グリッドと利用可能セル
- 1セル机、2セル机、机の向き
- 参加者と属性値
- 配置案と参加者の割り当て
- 配置制約と違反内容
- 位置別重みマップ
- 項目別評価値と総合評価値

CoreではMonoGameの `Point` や `Rectangle` を使わず、独自の整数座標を使います。これにより、GUIなしで評価計算をテストできます。

`CircleSpaceCoordinator.Core.csproj` はクラスライブラリーとして `CircleSpaceCoordinator.Core.dll` を生成します。GUIプロジェクトへの参照を持ちません。

### Application

- 配置案の作成、複製、削除
- 机の配置、移動、回転
- 参加者の割り当てと交換
- 評価の再計算と案の比較
- Import / Exportの調停
- Undo / Redo対象となる編集コマンド

### StationeryUI

- テキスト編集状態と描画
- IME未確定文字列の表示
- ボタン、ダイアログ、リスト、ツールチップ
- フォーカスとポインター操作

Windows固有のIMEフックはここへ入れず、DesktopのWindows実装から抽象サービスとして注入します。

`CircleSpaceCoordinator.StationeryUI.csproj` も独立したクラスライブラリーとしてDLLを生成し、CoreやApplicationには依存しません。

### Desktop.Windows

`CircleSpaceCoordinator.Desktop.Windows` は `net10.0-windows` を対象とする Windows 専用の実行プロジェクトです。Windows API に依存しない操作・保存・ログなどは、`net10.0` の [Desktop.Core](../../CircleSpaceCoordinator.Desktop.Core/README.md) に分離しています。参照方向は `Desktop.Windows` → `Desktop.Core` で、逆向きの参照はありません。Windows 固有の画面処理と、それに結びついたゲームループ・描画処理は Windows 側に残っています。他の OS 用の実行プロジェクトはまだありません。

- MonoGameの起動とゲームループ
- 会場キャンバスとカメラ
- ドラッグ＆ドロップ
- ヒートマップ、評価内訳、案の比較画面
- Windows版IME、クリップボード、ファイルダイアログの組み立て

`CircleSpaceCoordinator.Desktop.Windows.csproj` はGUI実行プロジェクトです。参照方向はDesktopからApplication、Infrastructure、StationeryUIへの一方向とし、Core側からGUIを参照しません。

### Excel bridge

- `Data`、`SpaceConfig`、`Config`、`YPredict`など既存シートの読み書き
- 名前付き範囲 `VenueArea` とセル座標の変換
- VBAを保持した保存
- C#が扱う交換形式への変換
- 提出用ワークブックの生成

Pythonに評価式を複製せず、C#が出した評価結果を書き込むだけにします。

## Canonical exchange format

通常の保存形式にはJSONを推奨します。CSVは参加者一覧などの表には適していますが、会場、机、複数案、重みマップを一つのファイルで表現しにくいためです。

```text
project.json
participants.csv       任意。表計算ソフトとの受け渡し用
evaluation-result.json
```

JSONには `schemaVersion` を必ず持たせ、ExcelブリッジとC#アプリが同じ契約を共有します。

## Migration sequence

1. 現在のPythonと匿名化したExcelから、入力と期待評価値を記録する。
2. Coreに同じ評価式を実装し、Pythonとの一致テストを作る。
3. ExcelからJSONへ変換するbridgeのimport処理を作る。
4. MonoGameで会場と配置案を編集できるようにする。
5. C#の結果をbridgeが提出用Excelへ書き戻す。
6. 一致テストが揃ってから、既存Pythonの評価処理を廃止する。

この順序なら、提出業務を止めずにExcel依存を外せます。

## Repository data policy

- 実データ入り `.xlsm`、出力ブック、ログをコミットしない。
- テスト用ブックは架空の参加者と最小限のシートだけで作る。
- 元のブック名や業務固有列名が公開可能か、追加前に確認する。
- ローカルファイルの存在を前提にせず、パスは設定またはコマンド引数で渡す。
