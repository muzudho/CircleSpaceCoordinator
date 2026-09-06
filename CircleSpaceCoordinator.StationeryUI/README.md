# StationeryUI library

GUI部品を再利用するためのクラスライブラリーです。計算用のCoreやApplicationには依存しません。

MonoGame上のテキスト入力、IME合成表示、ボタン、ダイアログ、選択などを置きます。

`KifuwarabeGo2026.StationeryUI` から移植するときは、囲碁アプリ固有の名前空間とサービスを外し、会場アプリから独立したUIライブラリーにします。WindowsのIME実装はデスクトップ側へ置き、UI側は `ITextCompositionService` のような抽象だけを参照します。

最初の境界として `Text/ITextCompositionService.cs` を追加済みです。

`Canvas/GridViewport.cs` は、グリッドと画面座標の変換、パン、アンカー位置を維持するズームを提供します。MonoGame型や計算DLLには依存しません。

`Controls/IconButtonModel.cs` は、文房具UIの方針を参考に、位置、アクセシブル名、有効・選択・ホバー・押下状態、クリック判定を所有します。描画はホスト側へ委譲し、MonoGameには依存しません。
