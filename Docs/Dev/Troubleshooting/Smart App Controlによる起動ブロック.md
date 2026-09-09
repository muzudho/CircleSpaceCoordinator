# Smart App Control による起動ブロック

記録日：2026-09-09

## 今どうやり過ごしているか

**既存の開発用自己署名と Debug ビルド後の自動署名を維持し、Visual Studio を再起動したところ、本人から F5 で起動できたとの報告がありました。現在はこの状態で開発を継続します。**

再起動後の起動成功は確認報告として残しますが、再起動が SAC の判定を変えたのか、自己署名が効いたのかは特定できていません。再起動を SAC の確実な解除方法とは扱いません。今回の対応では新しい証明書の作成、Windows の保護設定の変更、有料署名の導入は行っていません。

## 症状と経緯

対象は Windows 上の Visual Studio で開発中の `CircleSpaceCoordinator.Desktop.Windows` です。発生時の Windows・Visual Studio の詳細バージョンと対象コミットは、既存記録にはありません。

| 順序 | 確認・報告内容 |
| --- | --- |
| 1 | 本人から「F5 キーで何も起動しない」と報告 |
| 2 | 通常ユーザー環境で Debug ビルドと自動署名は成功。しかし `CircleSpaceCoordinator.EditorClient.dll` の読み込みが `0x800711C7` で拒否された |
| 3 | Code Integrity イベント 3033 / 3077 にポリシー ID `{0283ac0f-fff1-49ae-ada1-8a933130cad6}` を確認 |
| 4 | 拒否された DLL の Authenticode は `Valid`。開発用証明書は通常ユーザーの `My`、`Root`、`TrustedPublisher` に登録済み。同梱エンジンの EXE / DLL も署名検証は `Valid` |
| 5 | 本人が Visual Studio を再起動し、その後「F5 キーで起動した」と報告 |

手順 2～4 の詳細は [コード署名の調査記録](../配布/コード署名.md#2026-09-09-f5-起動失敗と既存の開発用署名の照合) を参照してください。手順 5 は本人の報告であり、再起動前後のバイナリ・SAC 設定・ログを比較した検証はしていません。PC 自体を再起動して解決したという記録でもありません。

比較対象に挙がったコンピューター囲碁プログラムについても、本人から「警告が出ないだけで、何も対策していない」と訂正がありました。別プロジェクトで自己署名による SAC 対策が成功した事例としては扱いません。

## 現在の署名処理

[Desktop.Windows のプロジェクト設定](../../../CircleSpaceCoordinator.Desktop.Windows/CircleSpaceCoordinator.Desktop.Windows.csproj) は、Windows の Debug ビルドで `SmartAppControlSigningEnabled` が未指定なら `true` にし、ビルド後に [Sign-BuildOutput.ps1](../../../scripts/ForSmartAppControl/Sign-BuildOutput.ps1) を実行します。このスクリプトは現在のユーザーの証明書ストアから、秘密鍵を持つ有効な `CN=CircleSpaceCoordinator Development Code Signing` 証明書を選び、出力先を署名処理に渡します。

今回の調査では、署名が `Valid` の DLL も拒否されました。Microsoft の説明でも、SAC が署名判定で考慮する証明書は信頼されたプロバイダーが発行したものです。ローカルでの自己署名の検証成功だけでは、SAC の許可は保証できません。[Microsoft：SAC の署名要件](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control)

### ビルド時に「証明書がない」と出る場合

既存調査では、隔離環境から通常ユーザーの証明書ストアが見えず、署名処理が失敗しました。これは実行時の DLL ブロックとは別の問題です。通常ユーザー側に証明書がないとは限りません。

署名を行わずビルドだけ確認する場合は、リポジトリーのルートで次を実行します。

```powershell
dotnet build CircleSpaceCoordinator.slnx -c Release -p:SmartAppControlSigningEnabled=false
```

`SmartAppControlSigningEnabled=false` はプロジェクト独自の署名処理を止める指定です。Windows の SAC を無効にする指定ではなく、生成した EXE / DLL の起動許可も保証しません。

## 再発したとき

1. 発生時刻、対象コミット、Debug / Release、警告に表示されたファイル名とエラーを控えます。
2. ビルド・署名の段階で失敗したのか、成功後の起動・DLL 読み込みで拒否されたのかを切り分けます。
3. 未保存の作業を保存し、Visual Studio を終了して開き直し、同じソリューションで F5 起動を試します。今回はこの操作後に起動成功の報告がありました。
4. 起動できなければ、Windows セキュリティの「アプリとブラウザー コントロール」で SAC の現在の状態を確認します。イベント ビューアーの「アプリケーションとサービス ログ → Microsoft → Windows → CodeIntegrity → Operational」で、発生時刻付近のイベント 3033 / 3077、対象ファイル、ポリシー名・ID を確認します。
5. 拒否された実ファイルの署名を確認し、結果を追記します。`Valid` でも許可されたとは判断しません。

署名確認の例です。パスは警告・イベントに記録された対象ファイルに置き換えてください。

```powershell
Get-AuthenticodeSignature -LiteralPath '.\CircleSpaceCoordinator.Desktop.Windows\bin\Debug\net10.0-windows\CircleSpaceCoordinator.EditorClient.dll' |
    Format-List Path, Status, StatusMessage, SignerCertificate
```

起動できた場合も、試した操作と確認範囲を残します。再発時には Windows・Visual Studio のバージョン、再起動前後の SAC 状態、対象ファイルの SHA-256 も記録すると比較できます。

## 配布での扱いと未解決事項

v1.0.0 の ZIP は、本人からダウンロード・展開後に起動できた報告があります。これは今回の Debug / F5 起動とは別の事例です。[当時の設定・署名・ログの記録](../配布/v1.0.0のZIP起動とSmart%20App%20Control.md) を参照してください。

SAC はクラウドによる安全性判定も利用するため、未署名なら必ずブロックされるわけではありません。今回も ZIP の事例も、許可された直接の理由は未特定です。[Microsoft：SAC FAQ](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions)

有料署名を採用せず、ソースと Windows x64 用 ZIP を提供する現行方針を続けます。利用者自身のビルドも SAC の許可を保証する方法ではありません。詳細は [配布方針](../配布/README.md) と [ソース配布と SAC の制限](../配布/ソース配布とSmart%20App%20Control.md) にまとめています。

今後確認が必要なのは、再起動後に起動できた理由、再ビルド時の再発有無、他の PC や配布版での動作です。現状の扱いは「起動復旧の報告あり・根本原因未特定」です。

[トラブルシューティング一覧へ戻る](README.md) ／ [開発者向けガイド](../README.md)
