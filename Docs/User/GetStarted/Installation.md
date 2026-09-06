# ［入手］から～［ウィンドウを開く］まで

このページでは、**ソースコードを取得し、自分の Windows PC でアプリを作って（ビルドして）起動する方法**を、準備から説明します。プログラムの編集や、有料の署名サービスへの契約は不要です。

**始める前に：自分でビルドしても、Smart App Control にアプリや依存 DLL がブロックされる場合があります。** この手順は署名費用をかけずに利用を試すためのもので、ブロックを解消する方法ではありません。仕組みは [ソース配布と Smart App Control](../../Dev/配布/ソース配布とSmart%20App%20Control.md) を参照してください。

## 1. 必要なものを準備する

- Windows PC。本手順は Intel / AMD の64ビット PC（x64）を想定しています。Arm64 PC での動作は未検証です。
- インターネット接続。ソースコード、SDK、必要なライブラリーを取得します。
- .NET 10 SDK。ソースコードをアプリに変換するための無料のツールです。

次の順で SDK をインストールします。

1. [Microsoft の .NET 10 ダウンロードページ](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) を開きます。
2. **Build apps - SDK** の Windows **x64** インストーラーを選び、ダウンロードします。ビルドには SDK が必要なので、Runtime だけを選ばないでください。
3. インストーラーを実行し、画面に従ってインストールします。
4. 開いている PowerShell やターミナルがあれば閉じます。次の手順で開き直します。

この手順では Visual Studio と Git のインストールは不要です。

## 2. ソースコードをダウンロードして展開する

1. [CircleSpaceCoordinator の GitHub ページ](https://github.com/muzudho/CircleSpaceCoordinator) を開きます。
2. **Code → Download ZIP** を選びます。これは実行ファイルの配布 ZIP ではなく、ソースコードを取得するための ZIP です。
3. ダウンロードした ZIP を右クリックし、**すべて展開**で、書き込み可能な作業フォルダーへ展開します。
4. 展開したフォルダーを開き、`CircleSpaceCoordinator.slnx` と `CircleSpaceCoordinator.Desktop` フォルダーがある階層まで進みます。

以降、この階層を「ソースのルート」と呼びます。ZIP の中を直接開いた状態では作業しないでください。作成されるアプリと利用設定を引き続き使うため、展開先は後で削除しない場所にしてください。

取得方法の詳細は [GitHub のソースコードダウンロード案内](https://docs.github.com/en/repositories/working-with-files/using-files/downloading-source-code-archives) を参照してください。

## 3. ソースのルートで PowerShell を開く

エクスプローラーでソースのルートを開き、アドレスバーに `powershell` と入力して Enter を押します。管理者として起動する必要はありません。

開いた画面へ次のコマンドを貼り付け、Enter を押します。

```powershell
dotnet --list-sdks
```

一覧に `10.0.xxx` で始まる行があれば .NET 10 SDK が見つかっています。続けて、作業場所を確認します。

```powershell
Test-Path .\CircleSpaceCoordinator.slnx
```

`True` と表示されれば、正しいフォルダーです。`False` の場合は PowerShell を閉じ、手順2のソースのルートから開き直してください。

## 4. アプリをビルドする

次のコマンドを実行します。

```powershell
dotnet build .\CircleSpaceCoordinator.Desktop\CircleSpaceCoordinator.Desktop.csproj -c Release -p:SmartAppControlSigningEnabled=false
```

初回は必要なライブラリー（NuGet パッケージ）が自動的にダウンロードされます。完了まで待ち、ビルド成功と表示されたことを確認してください。失敗した場合は、下の「うまくいかないとき」を確認します。

末尾の `SmartAppControlSigningEnabled=false` は、このプロジェクト独自の自己署名処理を止める指定です。Windows の Smart App Control の設定は変更しません。

## 5. アプリを起動する

同じ PowerShell で、次を実行します。

```powershell
dotnet run --project .\CircleSpaceCoordinator.Desktop\CircleSpaceCoordinator.Desktop.csproj -c Release --no-build
```

イベントプロジェクト選択画面が表示されたら起動成功です。アプリが終了するまで PowerShell は開いたままにしてください。

次回以降は、同じソースのルートで PowerShell を開き、この起動コマンドを実行します。ソースを変更・更新していなければ、手順4のビルドを繰り返す必要はありません。

ウィンドウが開いたら、[配置の決定稿を作る手順](Start.md) へ進んでください。

## 新しい版へ更新する

1. 作業中のプロジェクトを保存し、アプリを終了します。
2. 新しいソースコードをダウンロードして、今までの版とは別のフォルダーへ展開します。
3. 新しいソースのルートで、手順3〜5を実行します。SDK の再インストールは通常不要です。
4. プロジェクト選択画面の［既存ファイルを登録］から、これまで使っていたイベント JSON を登録して開きます。

イベント JSON は、ソースのフォルダーとは別の保存先で管理してください。以前のアプリで保存先を確認してから更新すると安心です。新しい版の動作を確認するまでは、古いソースと作成済みアプリも残します。

## うまくいかないとき

| 状況 | 確認すること |
| --- | --- |
| `dotnet` が見つからない | SDK のインストール完了後、PowerShell を開き直してください。Runtime だけをインストールしていないかも確認します。 |
| SDK 一覧に `10.0.xxx` がない | 手順1で .NET 10 SDK をインストールしてください。 |
| プロジェクトファイルが見つからない | `Test-Path .\CircleSpaceCoordinator.slnx` が `True` になる場所でコマンドを実行してください。 |
| NuGet パッケージの取得に失敗する | インターネット接続を確認し、手順4を実行し直してください。 |
| 開発用証明書が見つからない | 手順4のコマンドを、省略せずに実行してください。証明書の作成は本手順には不要です。 |
| Smart App Control が EXE・DLL をブロックする | ローカルビルドでも発生し得る制限です。ビルド成功や自己署名だけでは解消を保証できません。表示されたファイル名を記録し、[説明](../../Dev/配布/ソース配布とSmart%20App%20Control.md) を確認してください。 |
| その他のビルドエラーや起動失敗 | エラー文、Windows のバージョン、`dotnet --list-sdks` の結果、取得したソースの版を開発者へ知らせてください。共有前に個人名・パス・業務データを伏せてください。 |

この案内はリポジトリーの設定に基づいています。新規利用者の PC で、SDK のインストールから起動までの一連の手順は未検証です。

[制作物の説明](../Products/README.md) ／ [トップへ戻る](../../../README.md) ／ [開発者向けガイド](../../Dev/README.md)
