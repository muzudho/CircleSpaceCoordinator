# イベント一覧のスタイル設定

イベント一覧ページの下部にある「スタイル設定のオートリロード：有効／無効」をクリックすると切り替わります。Tab でボタンを選び、Enter または Space でも操作できます。初期値は有効です。

設定はユーザー設定フォルダーの `application-settings.json` の `styleAutoReload` に保存します。再起動後も維持し、保存に失敗した場合は切り替えずエラーを表示します。

## 編集するファイル

Windows の標準保存先：

```text
%LOCALAPPDATA%\CircleSpaceCoordinator\events.stationery-style.json
%LOCALAPPDATA%\CircleSpaceCoordinator\application-settings.json
```

画面下部にも実際のスタイルファイルのパスを表示します。スタイル設定エディターでこの JSON を開いて編集してください。アプリケーション設定ファイルの保存先を指定して起動する場合は、その隣にスタイルファイルを作成します。

起動時、スタイルファイルが存在しない場合だけ埋め込みの既定設定を作成します。既存の編集内容は再起動・再ビルド・配布更新で上書きしません。起動時には無効設定でも一度読み込みます。有効中は約500ms間隔で監視し、同じ内容を2回読めたら反映します。無効中の変更は、有効へ戻した後に反映します。

JSON の解析や必須モデルの検証に失敗した場合は、直前の正常な配置を維持し、ページ下部へエラーを表示します。初回から不正な場合は埋め込みの既定配置を使います。壊れたファイル自体は書き換えません。

## 調整できる項目

形式は文房具 UI の `models` / `layouts` / `bindings` です。最初は `layouts` だけを編集してください。

| layout の Id | 調整対象 |
| --- | --- |
| `regularPanel` / `compactPanel` | ページ外周の余白 |
| `regularPage` / `compactPage` | 見出し・説明・本文・リロードボタン・状態表示・下部案内の高さと間隔 |
| `regularBody` / `compactBody` | 一覧と操作欄の幅、両者の間隔 |
| `regularActions` / `compactActions` | 操作ボタンの寸法、行・列の間隔 |
| `eventRow` | 一覧の行高と行間（2行とも px 指定。先頭は1px以上） |
| `eventRowPadding` / `eventRowText` | 一覧行内部の余白と、イベント名・パスの配置 |

例えば通常表示の操作欄の幅は、`regularBody` の `column-definitions` の末尾にある `224px` を変更します。外周の左余白は `regularPanel.padding.left` です。

通常配置の操作欄の高さが固定行の合計に足りない場合、`compact` の配置を使用します。両方の配置を編集してください。`rowTemplate` は一覧行のひな型で、C# が表示可能な行数だけ繰り返して配置します。

モデルのルート `/events`、`regular` / `compact` 配下の各役割、`rowTemplate` の階層と型、必須部品のセル割当、行ひな型の layout Id `eventRow` はアプリとの契約です。削除・改名すると検証エラーになります。未接続モデルを追加しただけで新しいボタンが生成されるわけではありません。

## C# に残している処理

上部の作業者バーは既存の32pxを除外して、その下へ配置します。色、フォントサイズ、表示文言、ボタン内部の文字余白、一覧データ、スクロール・選択、クリック処理、ページ遷移、起動中のスピナー描画は C# に残しています。ウィンドウ内の配置はピクセル座標で、会場のズームとは独立しています。

描画と入力判定は同じ配置を使い、設定変更だけではイベント一覧データや選択・キーボードフォーカスを作り直しません。リサイズや再配置時には押下中のボタンを解除し、移動後の誤クリックを防ぎます。

## 実装・配布

既定設定は `CircleSpaceCoordinator.Desktop.Core/Styles/events.stationery-style.json`。埋め込みリソースと build / publish の `Styles` 配下に含めます。実行中に編集するのはユーザー設定フォルダー側です。

配置計算には文房具 UI の `StationeryStyleSettings` と `StationeryLayoutEngine` を使用します。監視は `EventListStyle` が担当し、既存のアプリケーション設定を唯一の有効／無効設定として参照します。文房具 UI の `StationeryStyleFile` が読む別の `*.stationery-config.json` は作成しません。画面操作で即座に監視方針を反映し、二重管理を避けています。

コアの StationeryUI はコミット `5130fff6a5cf18aa2559f33388bcc104b3aa1d05` から作ったローカルパッケージ `0.1.2-csc.5130fff` に固定しています。MonoGame / Windows の既存ホストは移行対象でないため0.1.1のままです。
