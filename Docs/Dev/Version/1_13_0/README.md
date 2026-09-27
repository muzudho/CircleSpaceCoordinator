# v1.13.0 の開発・設計資料

このフォルダーは、`Directory.Build.props` が 1.13.0 の現行作業ツリーを基準に整理しました。公開タグ v1.13.0 より後の変更を含みます。各文書に残る古い案・日付付き調査は当時の記録です。

## ビルドと実行

Windows と .NET 10 SDK が必要です。リポジトリーのルートで PowerShell を開いてください。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build\Build.ps1 -Configuration Release
dotnet run --project CircleSpaceCoordinator.Desktop.Windows -c Release --no-build
```

テストは[リポジトリーのテスト手順](../../../../tests/README.md)を参照してください。

## 文書を選ぶ

- [設計](設計/README.md)
- [開発・引き継ぎ](開発/README.md)
- [配布](配布/README.md)
- [運用](運用/README.md)
- [スタイル設定の実装](StyleSettings/README.md)
- [トラブルシューティング](Troubleshooting/README.md)
- [アプリの構成](architecture.md)
- [一般公開時の情報点検](public-release-review.md)
