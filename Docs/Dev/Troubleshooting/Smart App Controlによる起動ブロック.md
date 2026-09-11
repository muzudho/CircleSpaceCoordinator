# Smart App Control による起動ブロック

記録日：2026-09-09／更新日：2026-09-12

## 2026-09-12 EditorClient.dll の再発調査

**復旧報告：開発者がソリューションをクリーンし、ビルドし直して起動したところ、Smart App Control にブロックされず起動できた。** 以下に記載する調査時点では拒否が続いていたが、その後に起動成功の報告を受けた。

今回成功した手順は次のとおり。

1. ソリューション全体をクリーンする。
2. ソリューションをビルドし直す。
3. アプリを起動する。

こちらで行った GUI プロジェクトの Debug Rebuild と、開発者が行ったソリューション全体のクリーン→ビルドは区別する。起動成功は開発者からの報告であり、古いビルド出力の残存が原因だったか、SAC の判定が変化したかまでは確定していない。再発時には、今回成功した手順としてソリューション全体のクリーン→ビルド→起動を試し、結果を記録する。外部 DLL に自己署名を付けない方針は継続する。

開発者から起動時のブロック報告があり、02:12:56～57 の Code Integrity イベント 3033 / 3077 で、Debug の `CircleSpaceCoordinator.Desktop.Windows.exe` が `CircleSpaceCoordinator.EditorClient.dll` を読み込む際に拒否されたことを確認した。外部 DLL ではなく、このリポジトリで作成する gRPC クライアント DLL である。ポリシーは `VerifiedAndReputableDesktop`、GUID は `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`。

今回の比較では Windows の保護設定・証明書ストア・外部 DLL の署名を変更していない。

| 試行 | 結果 |
| --- | --- |
| 報告時の Debug（開発用自己署名あり） | EditorClient.dll を拒否 |
| EditorClient プロジェクトの署名前の Debug DLL に差替え、同じ GUI EXE を直接起動 | 同じ DLL を `0x800711C7` で拒否 |
| 通常の Debug Rebuild（自作ファイルのみ自動署名）後に直接起動 | ビルドは警告・エラー0件。起動では同じ DLL を拒否 |
| 同じソースを Release ビルド（自動署名なし）して直接起動 | ビルドは成功。今回は GUI EXE 自体をアプリ制御が拒否 |
| その後、開発者がソリューション全体をクリーン→ビルド→起動 | SAC にブロックされず起動成功との報告 |

署名あり／なしの両方で拒否されているため、この事例を「署名を外せば直る」「自己署名を付ければ直る」とは結論できない。Debug 出力は比較後、通常の Rebuild による署名付きの状態へ戻した。Visual Studio 自体の F5 操作は自動実行しておらず、再試行は同じ出力 EXE の直接起動である。

診断データはローカルの `artifacts/debug-startup-check/diagnostic-20260912-022150.json` に保存した。`F5Result` は `NotRun` とし、開発者の報告と EXE の直接起動を区別した。この診断を保存した時点では未解消だったが、その後、上記のクリーン→ビルドによる起動成功が報告された。コンパイル成功や Authenticode の `Valid` を起動成功として扱わない。

Microsoft の [Smart App Control 向けコード署名](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control) は、SAC が信頼する発行元の証明書を対象としている。開発用自己署名だけで安定した許可を保証するものではない。自動署名の対象を闇雲に増やしたり、外部 DLL に自己署名を付けたりして対処しない。

## 2026-09-11 外部 DLL への自己署名を廃止

署名付き Debug ビルド後に EXE を直接起動したところ、`MonoGame.Framework.dll` が `0x800711C7` で拒否されました。.NET Runtime の例外と Code Integrity イベント 3033 / 3077 で同じファイルのブロックを確認しました。

使用中の MonoGame 3.8.5.1 の元の DLL は未署名でしたが、従来のスクリプトは外部 DLL も含む全 EXE/DLL に開発用自己署名を付けていました。開発者の指示により、署名対象をこのリポジトリで作成するファイルの明示的な許可リストに限定しました。**他人が作った DLL・EXE には自己署名を付けません。** 詳細は [署名対象の方針](../配布/コード署名.md#署名対象の方針) に記載しています。

変更後の再ビルドは成功し、Debug 出力の外部ファイルに開発用自己署名が残っていないことと、`MonoGame.Framework.dll` の SHA-256 が元の NuGet パッケージと一致することを確認しました。外部ファイルを変更しない回帰テストも成功しました。その後、開発者から「起動した」との報告がありました。

起動成功は開発者からの報告です。自作 DLL の自己署名は継続しており、すべてを未署名にした比較ではありません。この結果だけで、他の環境での SAC の許可や、自己署名による拒否の内部判定理由までは確定しません。

以下は 2026-09-09 時点の調査内容です。今後も外部 DLL への自己署名禁止の方針を守って調査します。

## 調査対象

Windows 上の Visual Studio で開発中の `CircleSpaceCoordinator.Desktop.Windows` で、Debug ビルド後の F5 起動時に DLL が Smart App Control（SAC）に拒否される事象を調査します。

今回の問いは **開発用自己署名を行わないと F5 起動できないか** です。Visual Studio の再起動、PC の再起動、SAC の設定変更、証明書の再作成は比較対象にしません。

既存の確認では、`CircleSpaceCoordinator.EditorClient.dll` の読み込みが `0x800711C7` で拒否され、Code Integrity イベント 3033 / 3077 のポリシー ID は `{0283ac0f-fff1-49ae-ada1-8a933130cad6}` でした。拒否された DLL は Authenticode `Valid` で、開発用証明書も現在のユーザーの `My`、`Root`、`TrustedPublisher` に登録済みでした。[コード署名の調査記録](../配布/コード署名.md#2026-09-09-f5-起動失敗と既存の開発用署名の照合) を参照してください。

自己署名の `Valid` はローカルの署名検証結果です。SAC が自己署名を信頼することは保証しません。

## 自動署名の切替

`CircleSpaceCoordinator.Desktop.Windows` は Windows の Debug ビルドで `SmartAppControlSigningEnabled` が未指定なら `true` になります。ビルド出力に次のメッセージが表示され、実験条件を確認できます。

```text
Smart App Control diagnostic: automatic signing is true.
```

署名ありの条件では通常の Debug ビルドを使用します。署名なしの条件では、リポジトリーのルートで次を実行してください。

```powershell
dotnet build CircleSpaceCoordinator.Desktop.Windows/CircleSpaceCoordinator.Desktop.Windows.csproj -c Debug -p:SmartAppControlSigningEnabled=false
```

`SmartAppControlSigningEnabled=false` はプロジェクト独自の署名処理だけを止めます。Windows の SAC を無効にする指定ではありません。

## 切り分け手順

同じ Windows ユーザー、同じコミット、同じ Debug 構成、同じ SAC 設定で、次の二つの試行を行います。試行の途中で Visual Studio や PC を再起動せず、保護設定と証明書ストアを変更しません。

1. **署名あり**: 通常の Debug ビルド後、Visual Studio から F5 を一度実行します。
2. **署名なし**: 上記の `SmartAppControlSigningEnabled=false` を付けて Debug ビルドし直した後、同じ Visual Studio セッションから F5 を一度実行します。

各 F5 の直後に、実行結果を指定して診断を保存します。`-Since` にはビルド開始時刻より前の時刻を指定します。`-FailureDetails` にはブロックされたファイル名、HRESULT、警告文を記録します。

```powershell
.\scripts\ForSmartAppControl\Save-SmartAppControlDiagnostic.ps1 `
  -Path '.\CircleSpaceCoordinator.Desktop.Windows\bin\Debug\net10.0-windows' `
  -ReportDirectory '.\Docs\Dev\Troubleshooting\SmartAppControl' `
  -Signing Enabled `
  -F5Result Succeeded `
  -Attempt '署名あり-1' `
  -Since '2026-09-09T10:00:00'
```

`-Signing` は `Enabled` または `Disabled`、`-F5Result` は `Succeeded`、`Blocked`、`NotRun` を指定します。スクリプトは出力 EXE/DLL の SHA-256、Authenticode 状態、Windows と SAC の観測可能な設定値、Visual Studio プロセス、該当時刻以降の Code Integrity イベント 3033 / 3077 を時刻付き JSON に保存します。証明書の秘密鍵やバイナリ本体は保存しません。

## 判定と横展開

`Docs/Dev/Troubleshooting/SmartAppControl/結果テンプレート.md` に試行結果を転記します。署名ありで成功し、同一条件の署名なしで同じファイルが SAC によりブロックされた場合、その環境・そのコミットでは **自動署名が F5 起動に必要だった** と記録できます。

両条件で成功、または両条件で失敗した場合は、自動署名の必要性は判定できません。条件が異なる、対象バイナリのハッシュが異なる、Code Integrity イベントを取得できない場合も判定不能です。別の PC、コミット、Windows 更新後では同じ手順を繰り返し、結果を一般化しません。

## 配布との区別

この調査は開発時の Debug / F5 起動だけを対象にします。v1.0.0 の ZIP の起動報告は別の事例であり、自己署名の効果を示すものではありません。[v1.0.0 の ZIP 起動と Smart App Control](../配布/v1.0.0のZIP起動とSmart%20App%20Control.md) を参照してください。

[トラブルシューティング一覧へ戻る](README.md) ／ [開発者向けガイド](../README.md)
