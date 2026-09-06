# Application

配置案の作成・複製・検証・評価・比較など、画面や保存形式に依存しないユースケースを置きます。

Excel、CSV、JSONとの交換はインターフェース越しに行い、openpyxlを評価ロジックへ持ち込みません。

## 実装済みの編集操作

`Editing/PlanDeskEditor.cs` は、机の新規配置・削除・移動・90度単位の回転を扱います。
机上の参加者割当てと採点位置も同時に移し、編集後のプロジェクトが配置制約に違反する場合は変更を拒否します。

`Plans/PlanCatalogService.cs` は、空の配置案の新規作成・複製・名前変更・削除と総合評価による順位付けを扱います。
同点の場合は元の配置案の順序を維持します。

`Editing/ProjectEditHistory.cs` は、プロジェクト編集のUndo/Redo履歴を管理します。
検証に失敗した編集は現在値と履歴を変更せず、新しい編集を適用した場合はRedo履歴を破棄します。

`Editing/ParticipantAssignmentEditor.cs` は、参加者の机セルへの新規割当て、再割当て、解除を扱います。
必要セル数やセル重複などに違反する操作はCore検証により拒否します。

`Workspace/ProjectWorkspace.cs` は、現在の配置案選択、編集、Undo/Redo、評価ランキングをまとめた画面向けの窓口です。
選択中の案が削除された場合は、残っている先頭の案へ安全に選択を移します。

`Queries/PlanViewService.cs` は、選択案の机、占有セル、参加者割当て、未割当て参加者、評価結果を画面表示用のスナップショットへまとめます。

`Editing/VenueEditor.cs` は会場グリッドの縦横サイズを変更し、既存配置が会場外へ出る縮小を拒否します。`Editing/DeskLayoutService.cs` は配置禁止セルと既存机を避け、選択した机種類で空き領域を埋めます。
