# Infrastructure

JSON、CSV、設定ファイル、ログなどの永続化実装を置きます。

参加者表の読込みとExcel出力も、このプロジェクトのC#実装で扱います。

## 実装済み

`Json/ProjectJsonSerializer.cs` がversion 1 JSONとCoreモデルを相互変換します。

- 未知のJSONプロパティを拒否
- 読込み後にCoreの意味検証を実行
- 評価結果を任意で書出し
- セル、参加者、タグなどを安定した順序で出力
- `combinedSpaceId`（合体スペース）を保持
