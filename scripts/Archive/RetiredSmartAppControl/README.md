# 廃止したビルド・署名スクリプト

2026-09-26 に、Smart App Control をオフにする運用へ変更したため、開発用自己署名、Debug ビルド後の自動署名、Release の署名、ビルド・publish 前に全プロジェクトの `bin`・`obj` を削除する処理を廃止した。元のスクリプトを調査記録としてこのフォルダーに保存する。**現行のビルド・配布手順からは呼び出さない。**

現行のビルドは `scripts/Build/Build.ps1`、署名なしの配布は `scripts/Packaging/New-ReleaseZip.ps1` を使う。配布内容検査は `scripts/Packaging/Test-PublicReleaseContent.ps1` に移した。

ここには、証明書作成、署名、旧 ZIP 作成、SAC 診断、署名方針テスト、全 `bin`・`obj` 削除の旧スクリプトを置く。旧スクリプトは互いに以前のパスを参照する場合があり、そのまま実行する用途はない。
