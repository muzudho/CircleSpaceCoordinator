# 入力遅延・スクリーンショット遅延の調査

アプリ起動時に、EXEと同じフォルダーの `logs/performance-yyyyMMdd-HHmmss-プロセスID.jsonl` を自動作成する。約2秒ごとに数値診断を記録する。入力本文・ジャンル名・変更コメント・プロジェクト内容は記録しない。従来の `ui-operations-*.jsonl` とは別ファイル。

## 再現時の手順

1. 更新版を起動して［ジャンル］の変更コメントを入力する。
2. 遅くなったおおよその時刻を控える。Ctrl+Pを押した場合は、その時刻も控える。
3. 回復したらアプリを通常終了する。回復しない場合も、ログは別スレッドで逐次保存するため、終了前のファイルを確認できる。
4. その起動に対応する `performance-*.jsonl` を調べる。`.previous` があれば一緒に確認する。入力や撮影要求の直前・直後の行を比較する。

## 読み方

各行の `timestampUtc` はUTC時刻（日本時間は9時間を加算）。`context` が `genre_text` ならジャンル画面のテキストダイアログ。

| 項目 | 意味 |
| --- | --- |
| `uiHeartbeatAgeMs` | 最後にUIのUpdateが始まってからの時間。数千ms以上が続く場合、画面更新が止まっている可能性がある。非アクティブ・最小化時の抑制も考慮する |
| `phase`, `phaseMs` | 採取時に実行中の計測区間と経過時間。UIスレッドが止まっていても採取する。`idle` は計測区間外 |
| `timings` | 直前の採取以後に完了した区間の `Count`（回数）、`TotalMs`（合計ms）、`MaxMs`（最大ms）。区間は入れ子なので合計同士を足さない |
| `cpuPercent` | このアプリのCPU使用率、全論理CPUに対する割合。エンジンの別プロセスは含まない |
| `workingSetBytes`, `privateBytes` | アプリの常駐メモリ・プライベートメモリ。GPU専用メモリやエンジン別プロセスは含まない |
| `managedBytes`, `allocatedBytesTotal` | 現在のマネージド使用量と起動後の累積割当量。後者の増分が大きく、GCも多ければ一時オブジェクトを大量生成している可能性がある |
| `gen0`, `gen1`, `gen2` | 世代別の累積GC回数。前後の行の差分を見る |
| `gcHeapBytes`, `gcFragmentedBytes` | 直近のGCが報告したヒープ量・断片化量 |
| `gcMemoryLoadBytes`, `gcHighMemoryThresholdBytes`, `gcAvailableMemoryBytes` | GCが報告するメモリ負荷・高負荷の閾値・利用可能な総量。直近のGCに基づく情報で、現在の物理メモリ空き容量ではない |

主な計測区間：

- `update` / `draw`：画面更新全体／描画全体。
- `genre_autosave_check`：ジャンルの変更・自動保存判定。現在は表のJSON化を繰り返す箇所がある。
- `genre_save` / `project_save`：保存処理（エンジン呼び出しやファイル処理を含む）。
- `genre_draw`：背面のジャンル画面描画。
- `text_input_update` / `text_input_draw`：テキスト入力処理／テキスト欄描画。
- `text_rasterize_cache_miss`：キャッシュにない文字列のPNG生成回数・時間。入力がないときも大量に発生するならキャッシュの入替えを疑う。GPU転送時間は含まない。
- `draw_submit`：描画命令の送信。
- `text_input_batch`：アプリがIME・文字入力のバッチを取得した回数。
- `text_input_to_draw`：そのバッチ取得から描画命令送信まで。物理キー入力からの時間や画面の表示完了時刻ではない。
- `screenshot_requested` / `screenshot_completed` / `screenshot_failed`：撮影要求受付・保存成功・失敗の回数。要求がない場合は、入力未検出・非アクティブなども調べる。
- `screenshot_request_to_capture`：撮影要求受付から描画後の撮影開始まで。
- `screenshot_gpu_readback` / `screenshot_texture_upload` / `screenshot_png_write`：画面データ取得／テクスチャ転送／PNG書込み。
- `screenshot_total`：撮影全体。

メモリ不足、文字キャッシュの頻繁な入替え、UIスレッド上の保存などは候補であり、実際の遅延原因は再現時のログで判断する。

## 記録の負荷と制限

UI側は時刻とカウンターを更新するだけでファイルI/Oを行わない。監視タイマーと非同期ライターを使用し、書込み待ちキューは256件まで。満杯時は待たずに採取を捨て、`droppedSamples` に累積する。ディスクへの記録失敗でUI処理を止めない。

各起動のログは約8 MiBで前の区間を `.previous` に移し、現行・直前の約16 MiBを保持する。過去の起動のログは自動削除しない。通常終了時に最終採取を行い、書込み待ちは最大2秒。強制終了直前の未書込みデータは失われることがある。プロセス全体が停止した場合やGCで全スレッドが停止している間は監視も動かず、次の採取間隔に現れる。
