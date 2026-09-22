# イベント一覧のスタイル設定

開発用の Debug ビルドでは、イベント一覧ページの下部にある「スタイル設定のオートリロード：有効／無効」をクリックすると切り替わります。Tab でボタンを選び、Enter または Space でも操作できます。初期値は有効です。Release ビルドは埋め込み設定を使うため、このボタンは開発時専用と表示して無効にします。

設定はユーザー設定フォルダーの `application-settings.json` の `styleAutoReload` に保存します。再起動後も維持し、保存に失敗した場合は切り替えずエラーを表示します。

## 編集するファイル

スタイル設定の原本はリポジトリー内に置き、Git で管理します：

```text
App_Doc/events.style-settings.md
```

拡張子は `.style-settings.md` ですが、中身は文房具 UI が読み込む JSON です。Markdown の見出しやコードフェンスは付けないでください。テキストエディターで編集できます。文房具 UI のエディターを使う場合は、そのファイル選択がこの拡張子を扱えることを確認してください。

Debug ではビルド時に記録した原本の絶対パスを使用します。起動時のカレントディレクトリーに依存せず、画面下部にも実際のパスを表示します。起動時には無効設定でも一度読み込みます。有効中は約500ms間隔で監視し、同じ内容を2回読めたら反映します。無効中の変更は、有効へ戻した後に反映します。ファイルを新規作成・上書きする処理はありません。

Release では原本をアセンブリーのリソースへ埋め込み、外部ファイルを読まずに使用します。スタイルファイルの同梱やユーザーフォルダーへの展開は不要です。設定変更をリリースへ反映するには、原本を編集して Release を再ビルドします。

オートリロードの選択は従来どおり `%LOCALAPPDATA%\CircleSpaceCoordinator\application-settings.json` の `styleAutoReload` に保存します。以前の実装がユーザー設定フォルダーに作成した `events.stationery-style.json` は読み込みません。そこで調整した内容があれば、必要な変更を原本へ移してください。

JSON の解析や必須モデルの検証に失敗した場合は、直前の正常な配置を維持し、ページ下部へエラーを表示します。初回から不正な場合は埋め込みの既定配置を使います。壊れたファイル自体は書き換えません。

## 調整できる項目

形式は文房具 UI の `models` / `layouts` / `bindings` です。最初は `layouts` だけを編集してください。

| layout の完全パス | 調整対象 |
| --- | --- |
| `regularPanel` / `compactPanel` | ページ外周の余白 |
| `regularPanel.page` / `compactPanel.page` | 見出し・説明・本文・リロードボタン・状態表示・下部案内の高さと間隔 |
| `regularPanel.page.body` / `compactPanel.page.body` | 一覧と操作欄の幅、両者の間隔 |
| `regularPanel.page.body.actions` / `compactPanel.page.body.actions` | 操作ボタンの寸法、行・列の間隔 |
| `eventRow` | 一覧の行高と行間（2行とも px 指定。先頭は1px以上） |
| `eventRow.padding` / `eventRow.padding.text` | 一覧行内部の余白と、イベント名・パスの配置 |

例えば通常表示の操作欄の幅は、`regularPanel` → `children` の `page` → `children` の `body` にある `column-definitions` の末尾の `224px` を変更します。外周の左余白は `regularPanel.padding.left` です。

StationeryUI v0.2.0 の `children` でレイアウトをネストしています。トップレベルは通常表示・コンパクト表示・行ひな型の3本です。ボックスの子は余白の内側を使い、グリッドの子は自身の `row` / `col` で親のセルを指定します。`page` の本文は4行目、本文の操作欄は2列目です（いずれも0始まり）。行ひな型の `padding` は `eventRow` の0行目を使います。

`bindings.layout` はドット区切りの完全パスを指定します。同じレイアウトツリーの binding は `parentModel` を揃え、通常表示なら `/events/regular`、コンパクト表示なら `/events/compact` を基準に、`body/actions/open` などの子孫へ接続します。子レイアウトが使うセルにモデルも重ねて配置するとエラーになります。`body`・`actions`・行の `item` の領域は、アプリが対応する子レイアウトの計算結果から取得します。

通常配置の操作欄の高さが固定行の合計に足りない場合、`compact` の配置を使用します。両方の配置を編集してください。`rowTemplate` は一覧行のひな型で、C# が表示可能な行数だけ繰り返して配置します。

モデルのルート `/events`、`regular` / `compact` 配下の各役割、`rowTemplate` の階層と型、必須部品のセル割当、行ひな型の layout Id `eventRow` はアプリとの契約です。削除・改名すると検証エラーになります。未接続モデルを追加しただけで新しいボタンが生成されるわけではありません。

## C# に残している処理

上部の作業者バーは既存の32pxを除外して、その下へ配置します。色、フォントサイズ、表示文言、ボタン内部の文字余白、一覧データ、スクロール・選択、クリック処理、ページ遷移、起動中のスピナー描画は C# に残しています。ウィンドウ内の配置はピクセル座標で、会場のズームとは独立しています。

描画と入力判定は同じ配置を使い、設定変更だけではイベント一覧データや選択・キーボードフォーカスを作り直しません。リサイズや再配置時には押下中のボタンを解除し、移動後の誤クリックを防ぎます。

## 実装・配布

原本は `App_Doc/events.style-settings.md`。`CircleSpaceCoordinator.Desktop.Core.csproj` がこれを `CircleSpaceCoordinator.EventListStyle.json` というリソース名で埋め込みます。Debug でも読み込み失敗時のフォールバック用に埋め込みます。外部スタイルファイルは build / publish の出力にコピーしません。Release には開発マシンの原本パスを示す AssemblyMetadata も含めません。

配置計算には文房具 UI の `StationeryStyleSettings` と `StationeryLayoutEngine` を使用します。監視は `EventListStyle` が担当し、既存のアプリケーション設定を唯一の有効／無効設定として参照します。文房具 UI の `StationeryStyleFile` が読む別の `*.stationery-config.json` は作成しません。画面操作で即座に監視方針を反映し、二重管理を避けています。

StationeryUI / MonoGame / Windows は NuGet.org 公開版 `0.2.0` を使用します。F12 で文房具 UI 開発者ウィンドウを開き、モデルの Id・完全パス・実画面の座標を確認できます。[開発者ウィンドウの説明](Dev/StyleSettings/developer-window.md)を参照してください。
