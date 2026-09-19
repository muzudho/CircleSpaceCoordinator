2026-09-08: Reusable controls, viewport and text editing now come from the StationeryUI 0.1.0 package: https://github.com/muzudho/StationeryUI. This project is a compatibility reference point and contains no copied control implementation. Windows input and rasterization use StationeryUI.Windows 0.1.0. The initial packages are in LocalPackages/StationeryUI; NuGet.Config enables clean restoration without a sibling repository.
# StationeryUI library

## 変更タグコンポーネント（2026-09-19）

`Controls/ChangeTagEditor.cs` と `Controls/ChangeTagEditorView.cs` は、このプロジェクトに追加した再利用可能な入力状態・下線／バッジ表示部品です。`StationeryUI.Controls` 名前空間で公開し、OS・MonoGame・会場モデルには依存しません。既存パッケージの `UnderlineTextEditor` を使用し、検証・描画・文字幅計測・保存は利用側から接続します。

ジャンル網掛け・ブロック色の編集が最初の利用先です。独立リポジトリーへの移設・NuGet 公開は未実施です。[変更タグエディターの設計と実装状況](../Docs/Dev/開発/文房具UI/変更タグエディター.md) を参照してください。

## リングメニューの共通ライブラリー利用

2026-09-13: リング配置計算は StationeryUI リポジトリーの `src/StationeryUI/Controls/RingMenuLayout.cs` に集約しました。現在は同梱パッケージ `StationeryUI` 0.1.1 の `StationeryUI.Controls.RingMenuLayout.Create(anchor, width, height, count)` を使用します。円形の等間隔配置・正方形の寸法・画面端への補正を担当し、MonoGame・Windows・会場モデルには依存しません。返された `Center`・`Radius`・`Buttons` は囲碁などの別アプリからも利用できます。ローカルの重複実装は削除しました。

デスクトップ側の `VenueEditorGame.ToolRing.cs` は机・柱の操作定義を共通の入力・描画処理へ渡します。操作、ラベル、説明を定義し、キャンセルは操作なしの項目として扱います。暗幕・入力遮断・入力解放待ち・撮影例外・下部説明はホスト側の責任です。将来のパッケージ化では、入力状態と帯の描画も抽出し、アイコン描画と操作実行はホストのコールバックとして接続する方針です。

GUI部品を再利用するためのクラスライブラリーです。計算用のCoreやApplicationには依存しません。

MonoGame上のテキスト入力、IME合成表示、ボタン、ダイアログ、選択などを置きます。

`KifuwarabeGo2026.StationeryUI` から移植するときは、囲碁アプリ固有の名前空間とサービスを外し、会場アプリから独立したUIライブラリーにします。WindowsのIME実装はデスクトップ側へ置き、UI側は `ITextCompositionService` のような抽象だけを参照します。

最初の境界として `Text/ITextCompositionService.cs` を追加済みです。

`Controls/ModalDialogModel.cs` は画面内の通知・確認・時間指定・進捗の状態を扱います。ゲーム側の `VenueEditorGame.Dialogs.cs` が描画と入力、最適化処理を接続します。[移行進捗](../Docs/Dev/開発/文房具UI/移行進捗.md) に完了・残作業を記録しています。

`Controls/StationeryButtonRenderer.cs` は囲碁側の文房具ボタンの外観を移植した共通描画です。背景・枠・影を描画コールバックへ渡し、アイコンやラベルの描画を利用側に任せます。[調査・実装記録](../Docs/Dev/開発/文房具UI/README.md) を参照してください。移植元のライセンスは `ThirdParty/KifuwarabeGo2026-LICENSE.txt` にあります。

`Canvas/GridViewport.cs` は、グリッドと画面座標の変換、パン、アンカー位置を維持するズームを提供します。MonoGame型や計算DLLには依存しません。

`Controls/IconButtonModel.cs` は、文房具UIの方針を参考に、位置、アクセシブル名、有効・選択・ホバー・押下状態、クリック判定を所有します。描画はホスト側へ委譲し、MonoGameには依存しません。

共通リングの定義には CloseAfterAction 設定があります（既定は true）。机・柱は実行後に閉じ、会場サイズは false で開いたまま連続実行します。キャンセル項目と Esc はこの設定に関係なく閉じます。
