# SAC にブロックされた場合

現在は Smart App Control をオフにして利用する運用です。アプリ側で Windows の設定を変更する処理はありません。

「Smart App Control がブロックしました」「アプリケーション制御ポリシーによってこのファイルがブロックされました」と表示された場合の手順です。

## 対処手順

1. [公式リリース](https://github.com/muzudho/CircleSpaceCoordinator/releases/latest)から取得したアプリ用 ZIP を、新しい空フォルダーへ全体展開します。
2. ［Windows セキュリティ］→［アプリとブラウザー コントロール］→［スマート アプリ コントロールの設定］で［オフ］を確認します。
3. 配布フォルダーの EXE を起動します。

SAC のオフは PC 全体の設定です。アプリだけに適用する設定ではありません。[Microsoft の FAQ](https://support.microsoft.com/ja-jp/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions)

SAC をオフにしても起動しない場合は、[ほかの起動トラブル](../../GetStarted/Installation.md)を確認してください。

[トップへ戻る](../../../../README.md)
