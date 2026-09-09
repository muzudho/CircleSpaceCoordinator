# エディター GUI・エディターエンジン・思考エンジンの分離

2026-09-09 に合意した設計方針と、最初の実装範囲を記録します。

## 目的と用語

プログラムは GUI がなくても実行できます。本アプリも、描画やマウス入力を必要としない「ヘッドレス」で編集・計算を実行できる構成を目指します。GUI の開発者と計算部分の開発者が、それぞれ独立して開発・検証でき、AI や CLI、将来の別の GUI からも同じ機能を使えることを目的とします。

「GUI エンジン」という呼び方は使わず、次の３つに分けます。

| 名前 | 責務 | 持たせない責務 |
| --- | --- | --- |
| エディター GUI | 描画、入力、ドラッグ中のプレビュー、操作結果・進捗の表示 | 編集の正本、探索アルゴリズム |
| エディターエンジン | プロジェクトの読込み・書出し、編集、Undo/Redo、配置案の管理、思考への依頼と結果の反映 | ウィンドウ、描画、OS 固有の入力 |
| スペース配置の思考エンジン | 会場・参加者・条件から配置を探索し、配置案と評価を返す | GUI、エディターのセッションや編集履歴 |

エディターエンジンは独立したコンソールプログラムです。起動して gRPC 要求を待ち受けます。操作コマンドを送る CLI は別のクライアントであり、エンジンの起動に GUI は必要ありません。

## 依存と通信

```text
エディター GUI       CLI / AI によるコマンド実行
        ＼           ／
             gRPC
               ↓
       エディターエンジン
               ↓ gRPC
   スペース配置の思考エンジン
```

矢印は呼出し方向です。結果の返信は逆向きの実装参照ではありません。エディターエンジンは GUI を参照せず、思考エンジンはエディターエンジンを参照しません。思考エンジンは別のクライアントから直接利用することもできます。

通信契約は [`engines.proto`](../../../CircleSpaceCoordinator.Engine.Contracts/Protos/engines.proto) に置きます。契約プロジェクトは Core、Application、GUI、各サーバー実装を参照しません。各クライアントは生成した gRPC クライアントから呼び出します。将来の別言語のクライアントもこの `.proto` を利用します。

現在のサーバー内部では、既存の Core / Application / Infrastructure を再利用します。ThinkingEngine は既存の OptimizationEngine を呼び出します。共通モデル・配置の検証・評価を GUI から独立して保持します。

## 最初の実装

| プロジェクト | 内容 |
| --- | --- |
| `CircleSpaceCoordinator.Engine.Contracts` | `circlespace.engines.v1` のメッセージと２サービスの契約 |
| `CircleSpaceCoordinator.EditorEngine` | ヘッドレスの編集 gRPC サーバー |
| `CircleSpaceCoordinator.ThinkingEngine` | ヘッドレスの探索 gRPC サーバー |
| `tools/CircleSpaceCoordinator.EditorCli` | エンジン実装を参照しない gRPC クライアント |
| `tests/CircleSpaceCoordinator.Engine.Tests` | 実際の HTTP/2 接続を使う統合テスト |

Editor は Open / Get / Edit / Optimize / Close を公開します。Edit は配置案名の変更と Undo/Redo に対応します。Optimize は思考エンジンから得た結果を新しい配置案として追加し、その追加も Undo できます。Thinking は状態を保持しない Optimize を公開します。

初版のプロジェクトデータは既存の `schemaVersion` 付き JSON 交換形式を gRPC メッセージに格納します。操作名・ID・版・計算条件は protobuf の型付きフィールドです。画面オブジェクトやサーバーのファイルパスは送りません。CLI がローカルファイルを読み、エンジンが JSON を検証・モデルへ変換します。書出しではエンジンが JSON を生成し、CLI がローカルファイルへ保存します。CLI は既存の出力ファイルを上書きしません。

セッションは Open で作られ、Close またはサーバー終了で失われます。必要なデータは終了前に export します。自動保存・セッションの永続化・有効期限は未実装です。

### 版と失敗時の扱い

- セッションの版 `revision` は 1 から始まり、編集・Undo/Redo・探索結果の反映で増加します。Undo でも版は巻き戻しません。
- 変更要求は `expected_revision` を必須の条件として扱います。不一致は gRPC `Aborted` です。Get で最新状態を取得し、利用者が結果を確認してから再試行します。
- 探索はスナップショットを送ってロックの外で実行します。完了時にも版を確認し、その間に編集・Close されたセッションに結果を反映しません。
- 通信失敗やキャンセルでは探索結果を反映しません。クライアントのキャンセルと期限は思考側の RPC に伝播します。キャンセル時の途中最良案の取得は初版に含めません。
- 不正な入力は `InvalidArgument`、存在しないセッション・案は `NotFound`、Undo 不可などの状態は `FailedPrecondition` です。
- 更新応答を受け取れなかった場合には、変更が適用された可能性があります。Get で状態を確認してください。要求 ID による重複排除は今後の課題です。

## 起動と CLI での動作確認

.NET 10 SDK を使用します。以下はリポジトリールートでの PowerShell の例です。

```powershell
dotnet build CircleSpaceCoordinator.slnx -c Release -p:SmartAppControlSigningEnabled=false
```

２つのターミナルで、それぞれ起動します。

```powershell
dotnet run --project CircleSpaceCoordinator.ThinkingEngine -c Release --no-build -- --port 5072
```

```powershell
dotnet run --project CircleSpaceCoordinator.EditorEngine -c Release --no-build -- --port 5071 --thinking-address http://127.0.0.1:5072
```

別の PowerShell で実行します。

```powershell
$editorCli = 'tools/CircleSpaceCoordinator.EditorCli/bin/Release/net10.0/CircleSpaceCoordinator.EditorCli.dll'
$editorAddress = 'http://127.0.0.1:5071'
$state = dotnet $editorCli $editorAddress open examples/circle-space-project-v1.example.json | ConvertFrom-Json
$workspaceId = $state.workspaceId
$project = $state.projectJson | ConvertFrom-Json
$planId = $project.circleLayouts[0].id
$state = dotnet $editorCli $editorAddress rename $workspaceId $state.revision $planId 'CLIで名前変更' | ConvertFrom-Json
$state = dotnet $editorCli $editorAddress undo $workspaceId $state.revision | ConvertFrom-Json
$state = dotnet $editorCli $editorAddress redo $workspaceId $state.revision | ConvertFrom-Json
$state = dotnet $editorCli $editorAddress optimize $workspaceId $state.revision $planId 'cli-result' 'CLI探索結果' 1000 100 | ConvertFrom-Json
dotnet $editorCli $editorAddress export $workspaceId "$env:TEMP/circle-space-headless-result.json"
dotnet $editorCli $editorAddress close $workspaceId
```

CLI の標準出力は protobuf JSON 形式です。`revision` は 64 bit 整数なので JSON では文字列です。既定値の false などは省略されます。エラーは標準エラーへ出し、終了コードは成功 0、操作失敗 1、使い方の誤り 2 です。

初版は `127.0.0.1` の HTTP/2 専用ポートを使用し、メッセージ上限は送受信それぞれ 32 MiB です。認証・TLS は実装していないため、信頼できるローカル環境で開発するための構成です。別マシンへの公開は認証・TLS・実行資源の制限を整えてから対応します。gRPC の HTTP/2 要件は [Microsoft の説明](https://learn.microsoft.com/ja-jp/aspnet/core/grpc/aspnetcore?view=aspnetcore-10.0) に基づきます。

## 移行の残作業

今回の実装はヘッドレスの縦断経路です。既存の Desktop.Windows / Desktop.Core はまだ Application / Infrastructure / OptimizationEngine を直接参照しており、既存 GUI の分離完了を意味しません。

1. 机の編集・参加者割当て・案の作成削除・選択・表示用スナップショットなど、残りの操作を契約へ追加する。
2. 探索の進捗ストリーム、ジョブ ID、停止時の途中結果取得を設計する。初版の探索応答は完了時のみ。
3. GUI の操作を gRPC クライアントへ切り替え、エディターエンジンへ状態と履歴を集約する。描画・ドラッグの一時表示は GUI に残す。
4. GUI から計算実装への直接参照を削除し、依存方向をビルド時に検査する。
5. エンジン起動終了の管理、セッション解放・永続化、同時探索数の制限、API の互換性検証、配布を整備する。

`.proto` は既存フィールドの番号を再利用せず、破壊的変更では API の版を分けます。共有 JSON のスキーマ変更も契約変更として扱います。

## 検証

```powershell
dotnet run --project tests/CircleSpaceCoordinator.Engine.Tests -c Release
```

テストは GUI を起動せず、動的ポートの２つの gRPC ホストに実接続します。読込み、不正入力、案名変更、Undo/Redo、版競合、セッション分離、２段の gRPC による探索と Undo、Close、思考側停止時の状態保全を確認します。応答を待機させるテスト用の思考サービスを使い、探索中の編集との競合とキャンセルの伝播も確認します。
