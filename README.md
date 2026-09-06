# Circle Space Coordinator

イベント会場の机・席・参加サークルを配置し、複数の配置案を編集・比較する Windows デスクトップアプリです。C# / .NET 10 と MonoGame を使用します。

## 構成

- `CircleSpaceCoordinator.Core/`: 会場・机・参加者のモデルと評価
- `CircleSpaceCoordinator.Application/`: 配置編集、案の管理、Undo/Redo
- `CircleSpaceCoordinator.Infrastructure/`: JSON、参加者表の読込み、Excel出力
- `CircleSpaceCoordinator.OptimizationEngine/`: 自動配置
- `CircleSpaceCoordinator.StationeryUI/`: UI部品
- `CircleSpaceCoordinator.Desktop/`: デスクトップアプリ（[操作説明](CircleSpaceCoordinator.Desktop/README.md)）
- `tests/`: C#のテスト
- `tools/CircleSpaceCoordinator.ProjectCli/`: コマンドラインツール
- `examples/`, `schemas/`: 架空サンプルとJSONスキーマ
- `scripts/ForSmartAppControl/`: 署名・配布用スクリプト

## ビルドと実行

Windows と .NET 10 SDK が必要です。

```powershell
dotnet build CircleSpaceCoordinator.slnx -c Release -p:SmartAppControlSigningEnabled=false
dotnet run --project CircleSpaceCoordinator.Desktop -c Release -p:SmartAppControlSigningEnabled=false
```

テスト手順は [tests/README.md](tests/README.md) を参照してください。
署名・リリース・ソース配布の方針と制限は [配布に関する知見](Docs/Dev/配布/README.md) にまとめています。
配布用の署名スクリプトは `scripts/ForSmartAppControl/` にあります。開発用証明書の作成スクリプトは、実行したPCの証明書ストアと信頼設定を変更します。

このリポジトリーはC#版を独立した履歴で収録しています。Python版・実データ・個人用メモ・ローカル設定は含めていません。実際の参加者データや操作ログ、スクリーンショットをコミットしないでください。

## ドキュメント

- [利用者向け](Docs/User/README.md)
- [開発者向け](Docs/Dev/README.md)

## ライセンス

[MIT License](LICENSE.txt)
