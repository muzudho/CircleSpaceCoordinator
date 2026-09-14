# Windows.Forms 依存の棚卸し

2026-09-14、ソース内の `System.Windows.Forms` と呼出し元を確認。以下のファイルは `CircleSpaceCoordinator.Desktop.Windows/` 内。ファイル選択そのものは移行対象外とする。

## 画面から使われている UI

| 機能 | 実装 | 文房具 UI への移行内容 |
| --- | --- | --- |
| 番号・番地の編集 | `DeskNumberDialog.cs`、`VenueEditorGame.cs`、`VenueEditorGame.NumberChannels.cs` | 下線入力と一括上書き確認。空欄による削除に対応する必要がある |
| フレーム配置の選択・紐付け変更 | `LayoutBindingDialog.cs` | 配置一覧の選択 |
| 色んなコピー | `PlanCopyDialog.cs` | コピー元・先・方法の選択と検証 |
| サークル ID の正規表現 | `CircleLabelDisplayDialog.cs`（`VenueEditorGame.CircleChannels.cs` から使用） | 正規表現・置換文字列の入力と検証 |
| チャンネル名・列対応・セルの重み | `ChannelDialog.cs`、`VenueEditorGame.Channels.cs` | 下線入力、列選択、0～1 の数値編集、削除確認 |
| 参加者データのセル全文表示 | `VenueEditorGame.ParticipantData.cs` の `ShowParticipantCell` | 選択・コピーと縦横スクロールができる閲覧表示 |
| 通知・確認 | 上記フォームと `VenueEditorGame.cs` などの `MessageBox.Show` | 既存の画面内モーダルに接続 |
| 起動時のエラー | `Program.cs` | ゲーム画面が未起動でも表示できる仕組みが必要 |

## 入出力画面に混在している UI

ファイル選択と、選択後の設定画面は分けて移行できる。

| 機能 | 実装 | 移行対象 |
| --- | --- | --- |
| 参加サークル一覧の取込み | `ParticipantImportForm.cs` | 文字コード・シート・列対応の選択、プレビュー表、確認・エラー |
| Excel への書出し設定 | `CircleSeatExportForm.cs` | シート・列対応の選択、結果通知 |
| 書出す配置の選択 | `ExportPlanDialog.cs` | 配置一覧の選択 |
| 読込み・書出し中の表示 | `LoadingSpinnerDialog.cs` | 非同期処理と画面内進捗の接続 |

`Persistence/WindowsProjectFileDialog.cs` および取込み・書出しフォーム内の `OpenFileDialog` / `SaveFileDialog` は対象外。

## 呼出しが見つからない旧実装

`SeatNameDialog.cs`、`BulkSeatNameDialog.cs`、`TextPromptDialog.cs` は定義のみで、現在のソースに呼出しが見つからない。現在使われている画面とは区別する。ビルド対象には残っている。

## 今回の移行

配置の追加、フレーム配置の複製、配置案の複製を `OpenUnderlineInput` に接続し、`PlanNameDialog.cs` を削除した。100 文字制限、空白名の拒否、前後空白の除去、確定・取消は既存の下線入力を利用する。作成・複製は確定後に実行し、例外は画面内に表示する。呼出し直後の戻り値は実行完了ではなく `dialog_opened` を示す。

手動確認対象は、各入口での確定・取消、空白名、IME 確定、作成・複製先への選択移動、Undo。GUI の実機確認は未実施。

検証：Windows アプリの Release ビルド成功（警告・エラー 0）。既存の StationeryUI.Tests 14 件、Desktop.Tests 60 件が成功。これらは入力部品・共通処理の検証であり、変更した画面の実操作を検証したものではない。

## クロスプラットフォーム化との境界

直接の Windows.Forms 参照は Windows 実行プロジェクトに集まっている。同プロジェクトは `net10.0-windows` / `UseWindowsForms=true` のまま。また、文房具 UI の入力には `StationeryUI.Windows` の `WindowsTextInputService` を使用している。フォームを置き換える作業と、IME・クリップボード・文字描画などの OS 接続を別ホストへ移す作業は、両方必要になる。今回の変更だけで他 OS で起動できるわけではない。

[移行記録の目次](README.md)
