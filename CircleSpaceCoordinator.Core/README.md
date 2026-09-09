# Core

UI、Excel、ファイル形式に依存しないドメインモデルと評価結果の型を置きます。検証・評価・トポロジー解析の実装は `CircleSpaceCoordinator.Calculations` に分離しています。GUI は計算実装を参照せず、gRPC でエディターエンジンを利用します。

想定する主な型は `Venue`、`GridCell`、`Desk`、`Participant`、`Plan`、`WeightMap`、`EvaluationResult` です。
