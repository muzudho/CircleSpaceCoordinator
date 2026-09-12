# 配布に関する知見

署名、リリース、利用者への配布方法に関する調査と手順をここにまとめます。

## 現在の方針（2026-09-11）

- 無料で配布するオープンソースとして、継続的な有料署名契約や将来の値上げに依存する方式は採用しません。
- 短時間の修正・再配布を妨げる、外部担当者のリリース審査待ちを前提にしません。
- **他人が作った DLL・EXE には自己署名を付けません。** Debug / F5、Release、publish のすべてに適用します。外部ライブラリは提供元の署名・未署名状態を維持し、署名対象はこのリポジトリで作成するファイルの明示的な許可リストに限定します。[署名対象の方針](../Troubleshooting/SmartAppControl/%E3%82%B3%E3%83%BC%E3%83%89%E7%BD%B2%E5%90%8D.md#署名対象の方針) を参照してください。
- ソースコードと、必要な DLL・ランタイムを含む Windows x64 用 ZIP を配布します。v1.0.1 の実行ファイル名は `CircleSpaceCoordinator.Desktop.Windows.exe` です（v1.0.0 は `CircleSpaceCoordinator.Desktop.exe`）。
- SAC に関する利用者の案内は [ユーザー向け手順](../../User/Troubleshooting/SmartAppControl/README.md)、開発時の運用・調査は [開発者向け資料](../Troubleshooting/SmartAppControl/README.md) を参照してください。

## 文書一覧

gRPC 移行後の GUI は `engines/editor/` と `engines/thinking/` も必要です。通常の publish で２つのエンジンを同梱します。ランタイム同梱版の生成例と起動テストは [ヘッドレスエンジンと gRPC](../設計/ヘッドレスエンジンとgRPC.md) に記載しています。利用者のセッション保存先は配布物に含めません。

| 文書 | 内容 |
| --- | --- |
| [v1.0.0 の ZIP 起動と Smart App Control](../Troubleshooting/SmartAppControl/v1.0.0%E3%81%AEZIP%E8%B5%B7%E5%8B%95%E3%81%A8Smart%20App%20Control.md) | 起動成功の報告、設定・ログの確認結果、未署名でも許可され得る理由 |
| [バージョン管理](バージョン管理.md) | 共通リリース番号の更新と確認、タグ・ソース配布 |
| [ソース配布とSmart App Control](../Troubleshooting/SmartAppControl/%E3%82%BD%E3%83%BC%E3%82%B9%E9%85%8D%E5%B8%83%E3%81%A8Smart%20App%20Control.md) | ソース配布で解決できること・できないこと、ビルド手順 |
| [署名サービスの比較](../Troubleshooting/SmartAppControl/%E7%BD%B2%E5%90%8D%E3%82%B5%E3%83%BC%E3%83%93%E3%82%B9%E3%81%AE%E6%AF%94%E8%BC%83.md) | SignPath Foundation と有料方式の調査・採用判断 |
| [コード署名](../Troubleshooting/SmartAppControl/%E3%82%B3%E3%83%BC%E3%83%89%E7%BD%B2%E5%90%8D.md) | 現在の自己署名処理と証明書の扱い |
| [リリース手順](リリース手順.md) | 現在のローカル署名・ZIP 作成・更新手順 |

調査を追記するときは、確認日、公式資料、実機で確認した事実と未検証事項を区別して記録してください。サービスの条件や価格は確認日時点の情報です。
