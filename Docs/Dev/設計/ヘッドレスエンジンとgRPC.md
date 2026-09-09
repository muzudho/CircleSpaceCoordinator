# エディター GUI・エディターエンジン・思考エンジンの分離

2026-09-09 に合意した設計方針と、GUI の gRPC 移行後の実装を記録します。

## 現在の実装（GUI の移行まで完了）

計画した５項目（編集 API、探索ジョブ、GUI の接続切替、参照の禁止、起動終了・保存・同時実行制限・互換性検査・同梱）を実装しました。以下の「初版」の記述は開発の経緯です。

GUI は EditorClient を通してエディターエンジンを呼び出します。エディターエンジンが編集・選択・Undo/Redo の正本を所有し、GUI は表示用スナップショットをキャッシュします。描画ごとの RPC や評価計算は行いません。ドラッグ途中のプレビュー、カメラ、ホバー、ダイアログは GUI が所有します。

### プロジェクト境界

| プロジェクト | 役割 |
| --- | --- |
| EditorClient | gRPC クライアント、表示用キャッシュ、子エンジンの起動終了 |
| Engine.Model | 許可された編集操作、表示用データ、履歴保存用データの契約 |
| Core | 会場・配置・座標・評価結果のデータ型 |
| Calculations | 検証、評価、トポロジー解析、配置の射影。GUI は参照しない |
| Application | エディターの操作と履歴 |
| TableIO | Excel・CSV のファイル入出力アダプター |

互換性のため一部の型の名前空間は維持していますが、アセンブリは分離しました。GUI に残る TableIO はファイルの入出力を担当し、参加者一覧の更新・検証・評価はエディターエンジンが担当します。

思考側が使用する参加者配置・交換の共通処理も Calculations に置きました。ThinkingEngine / OptimizationEngine は Application、EditorEngine、GUI を参照せず、この方向もビルドで検査します。

`Directory.Build.targets` は GUI と EditorClient の解決済み参照を検査し、Application / Calculations / OptimizationEngine / Infrastructure / 各サーバー実装への参照が混入したらビルドを失敗させます。同梱用の `ReferenceOutputAssembly=false` は実装を呼び出す参照ではありません。

### 編集 API と CLI

Editor に Execute / Select / Load / ConvertDocument / Describe を追加しました。Execute は机の追加・移動・回転・削除、席名、会場サイズ、柱、島・対面の定義、参加者の配置・交換・仮置き、配置案の作成・複製・削除・名前変更・紐付け、参加者一覧更新、ジャンル表示設定を扱います。

[`EditorOperations.cs`](../../../CircleSpaceCoordinator.Engine.Model/EditorOperations.cs) の許可された型を、`operation` 判別子付きの JSON として `operation_json` に格納します。任意の型やメソッドを実行する API ではありません。`selected_plan_edit=true` は未使用の机配置を含むキャンバス編集用です。配置一覧の作成削除・紐付けや参加者一覧更新には false を使います。

Get と変更応答の `view_json` は、プロジェクト、選択、評価、ランキング、トポロジー、結合関係を含みます。保存形式への変換・検証・評価付き JSON の生成は ConvertDocument が行い、ローカルファイルの読書きは GUI / CLI が担当します。

CLI の追加コマンドは次のとおりです。

```text
describe
execute <workspace> <revision> <operation.json> [--project]
select-plan|select-desk <workspace> <revision> <layout-id>
load <workspace> <revision> <input.json>
start <workspace> <revision> <source-plan> <new-plan> <name> <time-ms> <iterations>
watch|stop|release-job <job-id>
```

例えば案名変更の操作ファイルは次の形式です。

```json
{"operation":"PlanCatalogService.RenamePlan","planId":"plan-1","newPlanName":"変更後の名前"}
```

選択と Load でも版が増えます。Load は Undo/Redo をリセットします。編集 RPC は同期で応答を待つため、エンジンが応答しない場合は最大15秒待つことがあります。重い探索は非同期です。

### 探索ジョブと停止

StartJob がジョブ ID を返し、WatchJob が進捗と最終結果を配信します。再接続すると現在の状態から購読できます。GUI もこの経路を使用し、エディターは Thinking の Run / Stop を呼び出します。

StopJob は「現在までの最高案を返して終了する」要求です。結果を新しい案として追加し、Undo できます。GUI は開始時より改善した場合だけ追加します。単発 Optimize の通信キャンセルは結果を反映せず中断するため、用途が異なります。

進捗は最新値を優先し、遅い購読者のために全イベントを蓄積しません。エディターは同時に１ジョブ、思考エンジンは単発 API を含め同時に１計算です。完了ジョブは ReleaseJob で解放でき、未解放の完了ジョブも最終アクセスから10分後に次の開始要求で整理します。最大128ジョブです。購読を切断しても計算は継続します。

### セッション保存・復旧・解放

`--state-directory <directory>` を指定すると、現在のプロジェクト、選択、版、Undo/Redo 履歴を操作ごとに保存します。GUI が起動するエンジンは `%LOCALAPPDATA%/CircleSpaceCoordinator/EngineSessions` を使用します。単独サーバーは省略時にメモリーのみです。

一時ファイルからの置換で保存し、保存失敗時はメモリー上の編集も確定しません。同じ保存先を使うサーバー間でも保存時の版を検査します。サーバー再起動後は以前の workspace ID を Get に渡すと履歴ごと復元できます。保存先の `<workspace ID>.json` が復旧対象です。実行中の探索ジョブは再起動をまたいで再開しません。

GUI の通常終了ではセッションを Close し、子エンジンを終了します。Close は保存済みセッションも削除するので、必要なプロジェクトは通常の保存操作でファイルへ書き出します。異常終了時の保存済みセッションは、CLI の Get / export で取り出せます。GUI が古いセッションを自動的に上書き復元することはありません。稼働中のジョブがあるセッションは停止・完了してから Close します。

メモリー上は最大128セッションです。30分以上使われていない非稼働セッションは、新規セッションの受入れ時にメモリーから解放します。永続化している場合は Get で復元できます。保存ファイルは Close まで保持し、自動削除しません。

### 起動・配布・互換性

GUI は同梱の２つのエンジンを非表示で自動起動し、動的なループバックポートに接続します。手動でのサーバー起動は不要です。親 GUI の終了を子エンジンも監視します。

通常の GUI ビルドでは `engines/editor/` と `engines/thinking/` に必要なファイルをコピーし、publish では２つのエンジンも publish します。このサブフォルダーも一緒に配布してください。

```powershell
dotnet publish CircleSpaceCoordinator.Desktop.Windows -c Release -r win-x64 --self-contained true -p:SmartAppControlSigningEnabled=false -p:DebugType=None -p:DebugSymbols=false -o artifacts/headless-win-x64
```

起動時の Describe による版確認に加え、[`engine-api-v1-baseline.json`](../../../schemas/engine-api-v1-baseline.json) とテストで RPC・フィールド番号・操作の既存プロパティを検査します。破壊的変更は API の版を分けます。検査を通すためだけに基準ファイルを書き換えません。

GUI を表示せずに、同梱したエンジンの自動起動・編集・終了を確認するコマンドです。

```powershell
dotnet run --project tests/CircleSpaceCoordinator.Engine.Tests -c Release -- --runtime artifacts/headless-win-x64
```

配布前は `scripts/ForSmartAppControl/Test-PublicReleaseContent.ps1 -Path <publishフォルダー>` も実行します。２つのエンジンの必須ファイルを確認し、設定・セッション・ログ・デバッグシンボルなどの混入を拒否します。GUI の起動確認で生成された設定とログは、配布用フォルダーから取り除いてください。

ネットワーク公開用の認証・TLS、ジョブの再起動後再開、更新要求 ID による重複排除は、ローカルアプリ向けの今回の移行とは別の拡張事項です。

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

## 初版の実装（履歴）

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

## 初版で立てた移行計画（上記の実装で完了）

初版ではヘッドレスの縦断経路を追加し、Desktop.Windows / Desktop.Core の直接参照は残していました。その後、以下の計画を実装しました。

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
