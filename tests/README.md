# Tests

Windows と .NET 10 SDK で、リポジトリールートから実行します。テストは実行可能なC#プロジェクトです。失敗時には終了コードが非ゼロになります。

```powershell
Get-ChildItem tests -Filter *.csproj -Recurse | ForEach-Object {
    dotnet run --project $_.FullName -c Release -p:SmartAppControlSigningEnabled=false
    if ($LASTEXITCODE -ne 0) { throw "Test failed: $($_.Name)" }
}
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/ForSmartAppControl/Test-PublicReleaseContent.Tests.ps1
```

モデルの評価・配置編集・Undo/Redo・JSONとExcelの入出力・UIの座標変換・操作ログ・配布ファイルの検査を検証します。

`CircleSpaceCoordinator.Engine.Tests` は GUI なしで編集・思考の gRPC ホストを動的なループバックポートで起動し、編集履歴・版競合・探索・通信失敗を実通信で検証します。[構成と手動確認手順](../Docs/Dev/設計/ヘッドレスエンジンとgRPC.md) を参照してください。

GUI が使う RemoteWorkspace / コントローラー経由の編集、停止時の最高案の取得、セッションと Undo 履歴の再起動後復元、v1 API の互換性も検証します。`-- --runtime <GUIのpublishフォルダー>` を渡すと、同梱エンジンを別プロセスで起動する検査を行います。Desktop.Tests のファイル入出力も実際のエディターサービスで検証します。

`CircleSpaceCoordinator.Desktop.Tests` は `Desktop.Core` を参照する `net10.0` のテストです。Windows 実行プロジェクトや Windows Desktop Runtime を必要とせず、共通処理を検証できます。現在の実行確認は Windows で行っています。
