# SAC にブロックされた場合

「Smart App Control がブロックしました」「アプリケーション制御ポリシーによってこのファイルがブロックされました」と表示された場合の手順です。

## 対処手順

1. [公式リリース](https://github.com/muzudho/CircleSpaceCoordinator/releases/latest)から取得したアプリ用 ZIP であることを確認し、全体を展開します。
2. **SAC をオンのまま使いたい場合は、** 表示されたファイル名、アプリの版、Windows のバージョンを開発者へ知らせてください。個人名や個人用フォルダー名は伏せてください。
3. **アプリを信頼し、SAC を一時的にオフにして使う場合は、** Windows Update を適用し、再びオンにできる条件を設定画面と [Microsoft の説明](https://support.microsoft.com/ja-jp/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions)で確認します。戻せない旨が表示される場合は、この切替手順を進めないでください。
4. ［Windows セキュリティ］→［アプリとブラウザー コントロール］→［スマート アプリ コントロールの設定］で［オフ］を選び、アプリを起動します。
5. 使い終わったらアプリを終了し、同じ画面で［オン］に戻ったことを確認します。

SAC をオフにすると、このアプリ以外にも SAC の保護がかからなくなります。Microsoft Defender のリアルタイム保護はオンのまま使ってください。このアプリだけを SAC の例外にする機能はありません。[Microsoft の FAQ](https://support.microsoft.com/ja-jp/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions)

SAC をオフにしても起動しない場合は、[ほかの起動トラブル](../../GetStarted/Installation.md)を確認してください。

[トップへ戻る](../../../../README.md)
