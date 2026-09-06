# StationeryUI library

GUI部品を再利用するためのクラスライブラリーです。計算用のCoreやApplicationには依存しません。

MonoGame上のテキスト入力、IME合成表示、ボタン、ダイアログ、選択などを置きます。

`KifuwarabeGo2026.StationeryUI` から移植するときは、囲碁アプリ固有の名前空間とサービスを外し、会場アプリから独立したUIライブラリーにします。WindowsのIME実装はデスクトップ側へ置き、UI側は `ITextCompositionService` のような抽象だけを参照します。

最初の境界として `Text/ITextCompositionService.cs` を追加済みです。

`Controls/ModalDialogModel.cs` は画面内の通知・確認・時間指定・進捗の状態を扱います。ゲーム側の `VenueEditorGame.Dialogs.cs` が描画と入力、最適化処理を接続します。[移行進捗](../Docs/Dev/開発/文房具UI/移行進捗.md) に完了・残作業を記録しています。

`Controls/StationeryButtonRenderer.cs` は囲碁側の文房具ボタンの外観を移植した共通描画です。背景・枠・影を描画コールバックへ渡し、アイコンやラベルの描画を利用側に任せます。[調査・実装記録](../Docs/Dev/開発/文房具UI/README.md) を参照してください。移植元のライセンスは `ThirdParty/KifuwarabeGo2026-LICENSE.txt` にあります。

`Canvas/GridViewport.cs` は、グリッドと画面座標の変換、パン、アンカー位置を維持するズームを提供します。MonoGame型や計算DLLには依存しません。

`Controls/IconButtonModel.cs` は、文房具UIの方針を参考に、位置、アクセシブル名、有効・選択・ホバー・押下状態、クリック判定を所有します。描画はホスト側へ委譲し、MonoGameには依存しません。
