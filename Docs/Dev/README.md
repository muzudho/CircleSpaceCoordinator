# Circle Space Coordinator 開発者向けガイド

開発・保守・配布を行う人向けのガイドです。アプリは C# / .NET 10 と MonoGame を使用します。

## ビルドと実行

Windows と .NET 10 SDK が必要です。リポジトリーのルートで PowerShell を開き、次のコマンドを実行します。

```powershell
dotnet build CircleSpaceCoordinator.slnx -c Release -p:SmartAppControlSigningEnabled=false
dotnet run --project CircleSpaceCoordinator.Desktop.Windows -c Release -p:SmartAppControlSigningEnabled=false
```

`SmartAppControlSigningEnabled=false` はプロジェクト独自のビルド後署名処理を止める指定です。Windows の Smart App Control を無効にする指定ではありません。利用環境での制限は [ソース配布と Smart App Control](配布/ソース配布とSmart%20App%20Control.md) を参照してください。

テストの実行方法は [テスト手順](../../tests/README.md) にまとめています。

## リポジトリーの構成

以下のパスはリポジトリーのルートからの相対パスです。

| パス | 役割 |
| --- | --- |
| `CircleSpaceCoordinator.Core/` | 会場・机・参加者のモデルと評価 |
| `CircleSpaceCoordinator.Application/` | 配置編集、案の管理、Undo/Redo |
| `CircleSpaceCoordinator.Infrastructure/` | JSON、参加者表の読み込み、Excel 出力 |
| `CircleSpaceCoordinator.OptimizationEngine/` | 自動配置 |
| `CircleSpaceCoordinator.Engine.Contracts/` | gRPC の API 契約 |
| `CircleSpaceCoordinator.EditorEngine/` | ヘッドレスの編集 gRPC サーバー |
| `CircleSpaceCoordinator.ThinkingEngine/` | ヘッドレスの探索 gRPC サーバー |
| `CircleSpaceCoordinator.StationeryUI/` | UI 部品 |
| [`CircleSpaceCoordinator.Desktop.Core/`](../../CircleSpaceCoordinator.Desktop.Core/README.md) | Windows API に依存しないデスクトップ共通処理 |
| `CircleSpaceCoordinator.Desktop.Windows/` | Windows 用の実行・画面処理 |
| `tests/` | C# のテスト |
| `tools/CircleSpaceCoordinator.ProjectCli/` | コマンドラインツール |
| `tools/CircleSpaceCoordinator.EditorCli/` | エディターエンジン用 gRPC CLI |
| `examples/`, `schemas/` | 架空サンプルと JSON スキーマ |
| `scripts/ForSmartAppControl/` | 署名・配布用スクリプト |

## 署名・リリース

現在の方針と手順は [配布に関する知見](配布/README.md) を参照してください。開発用証明書の作成スクリプトは、実行した PC の証明書ストアと信頼設定を変更します。自己署名の検証に成功しても、一般配布先での実行を保証するものではありません。

## 公開リポジトリーでの作業

このリポジトリーは、非公開リポジトリーから C# 版を独立した履歴で公開用に整理したものです。実際の参加者データ、ローカル設定、操作ログ、業務画面のスクリーンショットをコミットしないでください。サンプルには架空のデータを使用します。

## 設計・開発資料

| 資料 | 内容 |
| --- | --- |
| [配布](配布/README.md) | 署名、リリース、ソース配布の知見 |
| [運用](運用/) | 運用作業の手順 |
| [最新の開発日誌（2026年9月）](Log/2026/09.md) | 月ごとの開発の経緯、改善点、確認結果 |
| [開発](開発/) | 引き継ぎ、実装計画 |
| [文房具 UI](開発/文房具UI/README.md) | 画面共有に向けた調査、移行計画、ボタンの試験導入 |
| [設計](設計/) | 各機能とデータ形式の設計 |
| [素材](素材/) | 開発に関する素材 |
| [Architecture](architecture.md) | アプリケーションの構成と責務 |
| [ヘッドレスエンジンと gRPC](設計/ヘッドレスエンジンとgRPC.md) | ３つの責務、API、起動例、GUI の移行計画 |
| [情報点検と残作業](public-release-review.md) | 公開に向けた点検記録 |

過去の資料には旧リポジトリーや当時の構成に関する記録も含まれます。現在の実装・運用と照合して参照してください。

[トップへ戻る](../../README.md) ／ [制作物の説明](../User/Products/README.md) ／ [最新版をダウンロードする](https://github.com/muzudho/CircleSpaceCoordinator/releases/latest) ／ [ZIP 版が起動しないとき](../User/GetStarted/Installation.md)
