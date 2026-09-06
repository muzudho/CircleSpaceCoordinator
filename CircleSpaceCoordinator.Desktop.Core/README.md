# Desktop.Core

Windows の画面 API に依存しない、デスクトップアプリの共通処理です。`Directory.Build.props` の `net10.0` を使用するクラスライブラリーで、WinForms、System.Drawing、MonoGame、Windows 実行プロジェクトを参照しません。

| 処理 | ファイル・フォルダー |
| --- | --- |
| ワークスペースの読込み、起動時のプロジェクト選択 | `DesktopApplication.cs`、`DesktopStartup.cs` |
| 会場座標とキャンバス座標の変換 | `VenueCanvasMapper.cs` |
| ドラッグ、参加者配置、編集コマンド、画面外の方向判定、出力データの組み立て | `Interaction/` |
| 設定・イベント一覧・プロジェクトの保存と読込み | `Persistence/` |
| 操作ログの整形と書込み | `Logging/` |
| スクリーンショットの保存パス生成 | `Screenshots/ScreenshotPath.cs` |

参照方向は `Desktop.Windows` → `Desktop.Core` → `Application` / `Infrastructure` / `OptimizationEngine` / `StationeryUI` です。会場・配置の基本ルールは既存の `CircleSpaceCoordinator.Core` が担当します。

Windows のダイアログ、文字のラスタライズ、音声再生、実行開始処理は `Desktop.Windows` に残しています。`VenueEditorGame` や描画処理も Windows 実装と結びついているため、現段階ではそちらに残しています。

既存の `tests/CircleSpaceCoordinator.Desktop.Tests` は本プロジェクトを参照し、`net10.0` で共通処理を検証します。

```sh
dotnet build CircleSpaceCoordinator.Desktop.Core -c Release
dotnet run --project tests/CircleSpaceCoordinator.Desktop.Tests -c Release
```

この分離だけでアプリ全体が他の OS で動くようになるわけではありません。現在の検証環境は Windows です。他の OS では、ファイルパスの比較、既定の保存先、ファイルシステムの権限を含めた実行確認が必要です。

[開発者向けガイド](../Docs/Dev/README.md)
