# Smart App Control 対応の廃止記録

2026-09-26、Windows 11 の Smart App Control をオフにする運用へ変更した。これに伴い、開発用自己署名、ビルド後の自動署名、署名付き ZIP の作成、ビルド・publish 前の全 `bin`・`obj` 削除を廃止した。

- [旧リリース手順](リリース手順.md)は当時の操作記録。現在は実行しない。
- 旧スクリプトは [`scripts/Archive/RetiredSmartAppControl/`](../../../../scripts/Archive/RetiredSmartAppControl/README.md) に保存した。
- 当時の SAC の調査結果は [Smart App Control 資料](../../Troubleshooting/SmartAppControl/README.md) に残している。
- 現行の配布方法は[リリース手順](../../配布/リリース手順.md)を参照する。

この変更はリポジトリーのビルド・配布処理に関するもの。PC の Smart App Control 設定や証明書ストアをスクリプトから変更しない。
