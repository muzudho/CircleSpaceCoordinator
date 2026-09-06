# ソース配布と Smart App Control

確認日：2026-09-06。公式資料とリポジトリーの設定に基づく調査であり、利用者の PC での動作検証は未実施です。

## 結論

ソースコードを公開して利用者にビルドしてもらう方法なら、開発者が有料のコードサイニング証明書を契約せずに配布できます。ただし、**ローカルでビルドしたという理由だけで Smart App Control（SAC）の警告・ブロックをなくすことは保証できません。**

SAC はアプリやバイナリの安全性・署名を評価します。ダウンロードした EXE だけを対象にする仕組みではありません。安全性を判断できず、信頼された署名もないファイルはブロックされ得ます。[Microsoft：SAC の概要](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/overview)

## このアプリで残る問題

利用者がソースを取得してビルドすると、その PC に EXE・DLL が生成されます。実行時にはこれらが SAC の判定対象になり得ます。さらに、NuGet から取得する MonoGame や SDL2 などの外部バイナリは、自作部分をビルドしても署名済みにはなりません。

起動に成功しても、後から読み込む DLL で問題が出る可能性があるため、実際に使う機能まで確認する必要があります。[Microsoft：SAC でのテスト](https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/develop/smart-app-control/test-your-app-with-smart-app-control.md)

| SAC の状態 | ソースからビルドしたアプリへの影響 |
| --- | --- |
| オン（強制モード） | 信頼を確認できなければブロックされる可能性がある |
| 評価モード | SAC はブロックしない。後でオンになったときの動作は保証しない |
| オフ | SAC によるブロックはない。Defender などの検査は別に存在する |

Microsoft は開発作業と SAC の相性が悪い場合があると説明していますが、開発ツールを入れれば必ず自動的にオフになるとは案内できません。個別アプリだけを許可する例外機能も、確認した FAQ では提供されていません。本書は SAC の無効化を必須手順にはしません。[Microsoft：SAC FAQ](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions)

## ビルド・起動方法

Windows と .NET 10 SDK を用意し、取得したソースのルート（`CircleSpaceCoordinator.slnx` があるフォルダー）で PowerShell を開きます。初回は NuGet パッケージの取得に通信が必要です。

```powershell
dotnet build CircleSpaceCoordinator.slnx -c Release -p:SmartAppControlSigningEnabled=false
dotnet run --project CircleSpaceCoordinator.Desktop -c Release -p:SmartAppControlSigningEnabled=false
```

`SmartAppControlSigningEnabled=false` は、このプロジェクト独自のビルド後署名処理を無効にします。**Windows の SAC を無効にする指定ではありません。**

現在の Desktop プロジェクトは Windows の Debug ビルドで開発用証明書を使った署名処理を自動実行します。証明書がない環境では、その署名処理自体がエラーになります。上記はその処理を実行せずビルドする手順であり、SAC 通過を保証する手順ではありません。

更新時はアプリを終了し、公開されたタグやコミットを指定してソースを取得し直してから、同じ手順でビルドします。利用者がソースを編集している場合は、その変更を退避・管理してから更新します。イベントデータや利用設定をソースの更新で失わないようにしてください。

## 自己署名・SmartScreen との違い

開発用証明書を PC に信頼登録して `Get-AuthenticodeSignature` が `Valid` を返しても、SAC の許可を保証しません。SAC の署名による許可には信頼された提供元の証明書が必要で、公式資料では RSA 署名が必要とされています。[Microsoft：SAC の署名要件](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control)

SmartScreen のダウンロードに関する警告と、SAC の実行制御は別です。ソース配布に変えてダウンロード済み EXE の扱いが変わっても、SAC の問題まで解消したとは判断しません。また、購入した証明書で署名しても SmartScreen の警告が必ず消えるわけではなく、EV 証明書による即時の警告回避も現在は保証されません。[Microsoft：SmartScreen の評判](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)

## 利用者への説明例

> 本アプリはソースコードからビルドして利用できます。Windows と .NET 10 SDK が必要です。Smart App Control が有効な環境では、自分でビルドした場合もアプリや依存 DLL がブロックされることがあります。すべての PC で警告なく実行できることは保証していません。

今後の実機確認では、Windows のバージョン、SAC のモード、対象コミット、実行した機能、ブロックされたファイルを記録します。実データや個人情報を含むログ・画面画像は公開文書へそのまま添付しません。
