# Excel提出手順

## 前提

- 元の`.xlsm`、配置案`.xlsx`、重み`.xlsx`は非公開領域に置く。
- 中間JSONと出力ブックはGit管理外の`data/private/`を使う。
- 元の`.xlsm`と出力先には必ず別のパスを指定する。

## 1. ExcelからJSONへ取り込む

最初に値を表示しないdry-runで構造を確認する。

```powershell
python tools\excel-bridge\import_excel.py `
  --master path\to\deployment-v5.xlsm `
  --plan path\to\deployment-v5-XPlan5.xlsx `
  --weight path\to\deployment-v5-weight.xlsx `
  --dry-run
```

成功後にJSONを作る。

```powershell
python tools\excel-bridge\import_excel.py `
  --master path\to\deployment-v5.xlsm `
  --plan path\to\deployment-v5-XPlan5.xlsx `
  --weight path\to\deployment-v5-weight.xlsx `
  --output data\private\project-imported.json
```

実データの揃ったブックでは`--allow-missing-participants`を使わない。参加者の欠落を誤ってゼロ評価にしないためである。
匿名化検証で使用する場合は、配置案ブックに5つの`xFeature`シートが揃い、属性が復元されたことを変換後の検証結果で確認する。

## 2. C#で評価する

```powershell
dotnet run --project tools\CircleSpaceCoordinator.ProjectCli -- `
  evaluate data\private\project-imported.json data\private\project-evaluated.json
```

出力JSONにはC#を正本として計算した`evaluationResults`が入る。既存出力は上書きしないため、再実行時は別名にするか、不要であることを確認してから古い中間ファイルを退避する。

## 3. 別名のExcelへ書き戻す

まずdry-runする。

```powershell
python tools\excel-bridge\export_excel.py `
  --project data\private\project-evaluated.json `
  --workbook path\to\deployment-v5.xlsm `
  --output data\private\deployment-v5-evaluated.xlsm `
  --dry-run
```

成功後に`--dry-run`を外して出力する。既存出力を意図して置き換える場合だけ`--force`を付ける。

## 4. Excelで最終確認する

1. 元ブックではなく、出力した`deployment-v5-evaluated.xlsm`をExcelで開く。
2. マクロを利用する運用では、信頼できる元ブックから作った出力であることを確認してから有効化する。
3. 数式が再計算されたことを確認する。必要なら「数式」から完全再計算を行う。
4. `YPredict`の対象plan列に5つの評価結果が入っていることを確認する。
5. 旧Python版との移行期間中は、5項目すべてが旧評価値と一致することを確認する。
6. 別名のまま保存し、元ブックの更新日時や内容が変わっていないことを確認する。

## 停止条件

次の場合は提出せず、入力ファイルと列・行対応を確認する。

- Dataに存在しない参加者IDがある。
- `YPredict`に対象plan列または5つのfeature行がない。
- 重みブックなしで旧評価値との一致確認をしようとしている。
- 旧Python版とC#版の評価値が一致しない。
- VBAを含む入力なのに出力に`.xlsm`以外を指定した。
- Excelで開いた際に修復メッセージやマクロエラーが出る。
