# F12 開発者ウィンドウの接続

アプリがアクティブな状態で **F12** を押すと、文房具 UI が提供する開発者ウィンドウを別ウィンドウで表示します。Debug / Release の両方で使用できます。

文房具 Id と完全パスのツリー、種類・名前・表示状態・位置・寸法の詳細、パスのコピー、左右の仕切りは、ライブラリーの既存 `StationeryDeveloperView` を使用しています。このアプリ独自の一覧 UI は作っていません。

## 操作と表示範囲

- 本体で F12：開く。開いていれば同じウィンドウを再表示する。
- 開発者ウィンドウで F12 / Esc：隠す。本体の F12 で再表示できる。
- 開発者ウィンドウの閉じるボタン：子プロセスを閉じる。次の F12 で再作成する。
- 本体終了：所有している開発者ウィンドウも終了する。

現在の登録対象はイベント一覧のスタイル設定にある `/events` 配下のモデルです。表示中の通常／コンパクト配置には、描画と入力が使う内容矩形を画面座標で渡します。非選択の配置、行のひな型、未接続の追加モデルには実画面の矩形を付けません。別ページに移った場合はイベント一覧のモデルを非表示として報告します。

`rowTemplate` は一覧行の設計ひな型で、繰り返した個々のイベント行の Id ではありません。イベント名やファイルパスなどの業務データを今回のスナップショットには入れていません。会場編集やダイアログなどの独自描画部品も自動登録されません。別ページをスタイル設定へ移すときは、そのページの検査スナップショットも追加してください。

約0.25秒ごとに、本体のゲームスレッドで登録済みモデルの情報を更新します。Debug のスタイルリロードやウィンドウのリサイズも反映されます。検査画面の選択や折り畳み状態は文房具 UI が管理します。

## 文房具 UI のドキュメントで確認できたこと

StationeryUI リポジトリーの以下のドキュメントに組み込み方法が記載されています。

- `docs/user/developer-window.md`：操作、Id とパス、スナップショット、独自描画部品はアプリ側で登録すること。
- `docs/dev/developer-window.md`：`StationeryDeveloperWindow.Show` / `Update` / `Dispose`、別プロセス、`--stationery-inspector <パイプ名>` の入口とホストの必要性。
- `samples/StationeryUI.Demo/InspectorGame.cs`：MonoGame のウィンドウと通信の接続例。

確認したコミットは `5130fff6a5cf18aa2559f33388bcc104b3aa1d05` です。両ドキュメントの末尾にある開発者ウィンドウ自身のスタイル設定の説明には文字化けがありました。基本的な起動・通信の説明は読める状態で、スタイル設定については `StationeryDeveloperStyle.cs` とデモのソースを確認しました。

## このアプリ側の責務

| ファイル | 接続内容 |
| --- | --- |
| [Program.cs](../../../CircleSpaceCoordinator.Desktop.Windows/Program.cs) | 検査用引数を通常起動より先に処理。検査モードではイベントやエンジンを開かない |
| [StationeryInspectorGame.cs](../../../CircleSpaceCoordinator.Desktop.Windows/StationeryInspectorGame.cs) | デモを基にした薄いホスト。パイプで受け取り、既存 View の Refresh / Update / Draw を呼ぶ |
| [VenueEditorGame.DeveloperWindow.cs](../../../CircleSpaceCoordinator.Desktop.Windows/VenueEditorGame.DeveloperWindow.cs) | F12 の押下判定、Show / Update、接続診断のログ記録 |
| [EventListStyle.cs](../../../CircleSpaceCoordinator.Desktop.Core/Interaction/EventListStyle.cs) | スタイルのモデル階層を検査情報に変換。実際の配置結果と表示状態を渡す |

`StationeryDeveloperWindow` は実行中のプログラム自身を検査モードで再起動します。`Show` だけを追加して引数の分岐を忘れると、通常アプリがもう一つ起動し、パイプ接続にも失敗します。同じプロセスへ2つ目の Game を追加しないでください。

ホストは StationeryUI の MIT ライセンスのデモから派生し、著作権表示は `CircleSpaceCoordinator.Desktop.Windows/ThirdParty/StationeryUI-LICENSE.txt` に保持して配布に含めています。MonoGame / Windows パッケージもコアと同じ固定版へ更新しています。

表示状態は、そのモデルを画面に配置しているかを表します。モーダルや別ウィンドウに覆われていること、操作が無効なこととは別です。

この版のライブラリーは、開発者ウィンドウの通常終了でもパイプ切断を LastError に記録する場合があります。本体では診断を `developer_window_connection` として操作ログへ記録し、閉じる操作のたびにエラーダイアログを出すことは避けています。起動できない場合もこのログとライブラリーの Trace を確認してください。
