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

`CircleSpaceCoordinator.Desktop.Tests` は `Desktop.Core` を参照する `net10.0` のテストです。Windows 実行プロジェクトや Windows Desktop Runtime を必要とせず、共通処理を検証できます。現在の実行確認は Windows で行っています。
