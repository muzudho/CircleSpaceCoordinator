namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Core.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private sealed record MappingEditorButton(IconButtonModel Button, Action Execute, string? ColorId = null, string? PatternId = null, string? Tooltip = null);
    private bool mappingButtonsHaveChanges;
    private bool mappingKnowledgeComments;
    private StyleMappingDraft? mappingDraft;
    private string mappingKeyLabel = "";
    private string mappingEmptyMessage = "";
    private Action<StyleMappingEntry[], string, string, DateOnly>? applyStyleMapping;
    private ChangeTagEditor? mappingChangeTag;
    private bool mappingExitConfirmationOpen;
    private PersonCredits? mappingPreviousCredits;
    private StyleMappingEntry[] mappingAppliedStyles = [];
    private string? mappingAppliedOverallComment;
    private string mappingGenreCodeTableName = "";
    private string mappingAppliedGenreCodeTableName = "";
    private bool MappingTableNameChanged => mappingKnowledgeComments && mappingGenreCodeTableName != mappingAppliedGenreCodeTableName;
    private string[] mappingAppliedGenreOrder = [];
    private string? mappingAppliedGenreOrderComment;
    private bool mappingOrderChanged;
    private readonly List<MappingEditorButton> mappingEditorButtons = [];
    private IconButtonModel? pressedMappingButton;
    private int mappingRow;
    private int mappingColumn = 1;
    private int mappingScroll;
    private int mappingFocus = -1;
    private int mappingPickerColumn;
    private int mappingWidth = -1;
    private int mappingHeight = -1;
    private const int MappingVisibleRows = 6;
    private static readonly double[] DefaultMappingColumnEdges = [20, 290, 450, 610, 810, 980];
    private double[] GenreMappingColumnEdges => [20, 20 + Math.Max(48, Math.Max(mappingDraft?.Rows.Count ?? 1, mappingGenreCodeOrder.Length).ToString().Length * 16 + 16), 250, 330, 410, 490, 570, 980];
    private double[] MappingColumnEdges => mappingKnowledgeComments ? GenreMappingColumnEdges : DefaultMappingColumnEdges;
    private double MappingCanvasHeight => mappingKnowledgeComments ? 690d : 660d;
    private bool GenreChartVisible => mappingKnowledgeComments && genrePageTab > 0;
    private double MappingEditorScale => Math.Max(0.1, Math.Min(GraphicsDevice.Viewport.Width / 1000d, (GraphicsDevice.Viewport.Height - WorkerBarHeight - StatusBarHeight) / MappingCanvasHeight));
    private ScreenRectangle MappingBounds(double x, double y, double width, double height)
    {
        var scale = MappingEditorScale;
        return new((GraphicsDevice.Viewport.Width - 1000 * scale) / 2 + x * scale,
            WorkerBarHeight + (GraphicsDevice.Viewport.Height - WorkerBarHeight - StatusBarHeight - MappingCanvasHeight * scale) / 2 + y * scale, width * scale, height * scale);
    }
    private ScreenRectangle MappingCell(int visibleRow, int column, bool right = false) =>
        MappingGridBounds(MappingColumnEdges[column], 142 + visibleRow * 52, MappingColumnEdges[column + 1] - MappingColumnEdges[column] - 6, 46, right);

    /// <summary>Opens the shared editor; the caller owns persistence and undo.</summary>
    private void OpenShadingTableEditor(StyleMappingDraft draft, string keyLabel, string emptyMessage,
        Action<StyleMappingEntry[], string, string, DateOnly> apply, PersonCredits? previousCredits = null, bool knowledgeComments = false)
    {
        CancelInProgressPointerInteraction();
        mappingDraft = draft;
        mappingKnowledgeComments = knowledgeComments;
        genrePageTab = genrePreviewScroll = 0;
        genreGridLayoutMode = GenreGridLayoutMode.FullWidth;
        packageGenreTable = null;
        packageGenreScroll = 0;
        selectedGenreKey = null;
        selectedPackageGenreKey = null;
        genrePieExpanded = false;
        mappingKeyLabel = keyLabel;
        mappingEmptyMessage = emptyMessage;
        applyStyleMapping = apply;
        mappingPreviousCredits = previousCredits;
        mappingChangeTag = new ChangeTagEditor(ValidateMappingChangeLog);
        mappingTextSuppressExit = false;
        mappingTextRange = (0, 0);
        mappingAppliedStyles = draft.Build();
        mappingAppliedOverallComment = draft.OverallComment;
        mappingAppliedGenreCodeTableName = mappingGenreCodeTableName;
        mappingAppliedGenreOrder = mappingGenreCodeOrder.ToArray();
        mappingAppliedGenreOrderComment = mappingGenreCodeOrderComment;
        mappingOrderChanged = false;
        mappingRow = mappingScroll = mappingPickerColumn = 0;
        genreScrollbarDrag = -1;
        if (knowledgeComments) InitializeGenreScopeAutoSave();
        mappingColumn = knowledgeComments ? 2 : 1;
        mappingFocus = -1;
        mappingWidth = -1;
        modalInputDrain = true;
    }

    private void CloseStyleMappingEditor()
    {
        CancelInProgressPointerInteraction();
        genrePieExpanded = false;
        mappingDraft = null;
        SetMappingTextFocus(false);
        mappingChangeTag = null;
        packageChangeTag = null;
        mappingOrderChanged = false;
        applyStyleMapping = null;
        mappingEditorButtons.Clear();
        mappingPickerColumn = 0;
        modalInputDrain = true;
    }

    private void SaveStyleMapping()
    {
        TryFinishStyleMapping();
    }

    private void DiscardStyleMapping()
    {
        mappingDraft?.RestoreOpeningSnapshot();
        mappingGenreCodeTableName = mappingAppliedGenreCodeTableName;
        mappingGenreCodeOrder = mappingAppliedGenreOrder.ToArray();
        mappingGenreCodeOrderComment = mappingAppliedGenreOrderComment;
        genreCodeOrder = mappingGenreCodeOrder.ToArray();
        CloseStyleMappingEditor();
    }

    private bool TryExitStyleMapping()
    {
        if (mappingDraft is not null && mappingKnowledgeComments)
        {
            return TryExitGenreScope();
        }
        if (mappingExitConfirmationOpen) return false;
        SyncMappingChangeTag();
        if (mappingDraft?.HasChanges != true && !mappingOrderChanged && !MappingTableNameChanged ||
            mappingChangeTag?.CanClose == true && mappingComposition.Length == 0)
            return TryFinishStyleMapping();

        // Cancel the OS close request now; resume Exit only after an explicit choice.
        mappingExitConfirmationOpen = true;
        SetMappingTextFocus(false);
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "終了の確認",
            "変更を破棄してアプリケーションを終了しますか"), action =>
        {
            mappingExitConfirmationOpen = false;
            if (action != ModalDialogAction.Accept) return;
            // Draft edits never touch the project before confirmation. Restore its
            // page-opening copy, then discard the input without creating a new tag.
            DiscardStyleMapping();
            Exit();
        }, [("終了する", ModalDialogAction.Accept), ("キャンセル", ModalDialogAction.Cancel)]);
        return false;
    }

    private bool TryFinishStyleMapping()
    {
        if (mappingDraft is null || applyStyleMapping is null) return true;
        if (mappingKnowledgeComments) return FinishGenreScope();
        if (mappingComposition.Length > 0) return false;
        SyncMappingChangeTag();
        if (mappingChangeTag is null) return false;
        if ((mappingDraft.HasChanges || mappingOrderChanged || MappingTableNameChanged) && !EnsureHandle()) return false;
        if (!mappingChangeTag.TryBeginSave(out var changeLog))
        {
            SetMappingTextFocus(true);
            return false;
        }
        SetMappingTextFocus(false);
        var applied = false;
        var previousStyles = mappingAppliedStyles;
        var previousOverallComment = mappingAppliedOverallComment;
        var previousTableName = mappingAppliedGenreCodeTableName;
        var previousOrder = mappingAppliedGenreOrder;
        var previousOrderComment = mappingAppliedGenreOrderComment;
        try
        {
            var styles = mappingDraft.Build();
            if (!styles.SequenceEqual(mappingAppliedStyles) || mappingDraft.OverallComment != mappingAppliedOverallComment || mappingOrderChanged || MappingTableNameChanged)
            {
                applyStyleMapping(styles, changeLog!, Handle, WorkDate);
                mappingAppliedStyles = styles;
                mappingAppliedOverallComment = mappingDraft.OverallComment;
                mappingAppliedGenreCodeTableName = mappingGenreCodeTableName;
                mappingAppliedGenreOrder = mappingGenreCodeOrder.ToArray();
                mappingAppliedGenreOrderComment = mappingGenreCodeOrderComment;
                mappingOrderChanged = false;
                applied = true;
            }
            if (!FlushAutoSave())
            {
                if (applied)
                {
                    workspace!.Undo(); mappingAppliedStyles = previousStyles; mappingAppliedOverallComment = previousOverallComment;
                    mappingAppliedGenreCodeTableName = previousTableName;
                    mappingAppliedGenreOrder = previousOrder;
                    mappingAppliedGenreOrderComment = previousOrderComment;
                    mappingOrderChanged = !mappingGenreCodeOrder.SequenceEqual(previousOrder) || mappingGenreCodeOrderComment != previousOrderComment;
                }
                mappingChangeTag.SaveFailed("保存できませんでした。入力を保持しています。閉じるで再試行してください。");
                return false;
            }
            CloseStyleMappingEditor();
            return true;
        }
        catch (Exception ex) { mappingChangeTag?.SaveFailed(ex.Message); return false; }
    }

    private void OpenMappingPicker(int row, int column)
    {
        if (GenreGridVisible && column == 1) { OpenGenreNameEditor(row, false); return; }
        if (mappingKnowledgeComments && mappingDraft is { } targetDraft && row >= 0 && row < targetDraft.Rows.Count)
            SelectGenreTarget(targetDraft.Rows[row].Key);
        if (mappingKnowledgeComments && column == 6)
        {
            OpenGenreKnowledgeComment(row);
            return;
        }
        if (mappingKnowledgeComments && column == 0) { OpenGenreCodeOrderEditor(); return; }
        if (mappingDraft is null || row < 0 || row >= mappingDraft.Rows.Count ||
            (mappingKnowledgeComments ? column is < 2 or > 4 : column is < 1 or > 3)) return;
        if (mappingKnowledgeComments && column == 3 && mappingDraft.Rows[row].Pattern == "solid") return;
        mappingRow = row;
        mappingColumn = column;
        // Keep the grid column for focus; normalize the picker column exactly once.
        if (mappingKnowledgeComments) column--;
        mappingPickerColumn = column;
        SetMappingTextFocus(false);
        var style = mappingDraft.Rows[row];
        var current = column == 3 ? style.Pattern : column == 1 ? style.PrimaryColor : style.SecondaryColor;
        var choices = column == 3 ? StyleMappingDraft.Patterns : StyleMappingDraft.Colors;
        mappingFocus = choices.Select((choice, index) => (choice, index)).Where(item => item.choice.Id == current)
            .Select(item => item.index).DefaultIfEmpty(column == 3 ? 0 : choices.Count).First();
        mappingWidth = -1;
        pressedMappingButton = null;
        modalInputDrain = true;
    }

    private void CloseMappingPicker()
    {
        if (packageCellDraft is { HasChanges: true } packageDraft) ApplyPackageCellDraft(packageDraft);
        packageCellDraft = null;
        mappingPickerColumn = 0;
        mappingFocus = -1;
        mappingWidth = -1;
        pressedMappingButton = null;
        modalInputDrain = true;
    }

    private void BuildMappingEditorButtons()
    {
        if (mappingDraft is not { } draft) return;
        SyncMappingChangeTag();
        var hasChanges = mappingKnowledgeComments ? GenreScopeChanged : draft.HasChanges || mappingOrderChanged || MappingTableNameChanged;
        if (mappingPickerColumn == 0 && mappingEditorButtons.FirstOrDefault(item => item.Button.AccessibleName == "閉じる") is { } close)
            close.Button.IsEnabled = MappingCommentsCanClose && mappingComposition.Length == 0;
        if (mappingWidth == GraphicsDevice.Viewport.Width && mappingHeight == GraphicsDevice.Viewport.Height &&
            (mappingPickerColumn > 0 || mappingButtonsHaveChanges == hasChanges &&
                (!mappingKnowledgeComments || genreButtonsProjectChanged == GenreProjectChanged && genreButtonsPackageChanged == GenrePackageChanged))) return;
        genreButtonsProjectChanged = GenreProjectChanged;
        genreButtonsPackageChanged = GenrePackageChanged;
        mappingButtonsHaveChanges = hasChanges;
        mappingWidth = GraphicsDevice.Viewport.Width;
        mappingHeight = GraphicsDevice.Viewport.Height;
        mappingEditorButtons.Clear();
        pressedMappingButton = null;
        void Add(string label, ScreenRectangle bounds, Action action, bool enabled = true, string? color = null, string? pattern = null, string? tooltip = null) =>
            mappingEditorButtons.Add(new(new IconButtonModel(bounds, label) { IsEnabled = enabled }, action, color, pattern, tooltip));
        if (mappingPickerColumn > 0)
        {
            draft = packageCellDraft ?? draft;
            var column = mappingPickerColumn;
            var row = packageCellDraft is null ? mappingRow : packageCellRow;
            var choices = column == 3 ? StyleMappingDraft.Patterns : StyleMappingDraft.Colors;
            var columns = column == 3 ? 4 : 5;
            var width = (840d - (columns - 1) * 12) / columns;
            var height = column == 3 ? (308d - 12 * ((choices.Count + columns - 1) / columns - 1)) / ((choices.Count + columns - 1) / columns) : 76;
            for (var index = 0; index < choices.Count; index++)
            {
                var choice = choices[index];
                Add(choice.Label, MappingBounds(80 + index % columns * (width + 12), 180 + index / columns * (height + 12), width, height), () =>
                {
                    if (column == 3) draft.SetPattern(row, choice.Id);
                    else if (column == 2 && draft.Rows[row].Pattern == "solid")
                    {
                        CloseMappingPicker();
                        return;
                    }
                    else draft.SetColor(row, column == 1, choice.Id);
                    CloseMappingPicker();
                }, color: column == 3 ? null : choice.Id, pattern: column == 3 ? choice.Id : null,
                    tooltip: choice.Id.Contains("diagonal-up", StringComparison.Ordinal) ? "／ 右肩上がり斜線（バロック・ダイアゴナル）"
                        : choice.Id.Contains("diagonal-down", StringComparison.Ordinal) ? "＼ 右肩下がり斜線（シニスター・ダイアゴナル）" : null);
            }
            if (column != 3)
                Add("任意の色（#RRGGBB）", MappingBounds(80, 500, 340, 40), () =>
                {
                    var current = column == 1 ? draft.Rows[row].PrimaryColor : draft.Rows[row].SecondaryColor;
                    var color = GenreColorFromId(current, Color.Gray);
                    OpenUnderlineInput("色コードを指定", $"#{color.R:X2}{color.G:X2}{color.B:X2}", value =>
                    {
                        draft.SetColor(row, column == 1, value);
                        CloseMappingPicker();
                    }, "#RRGGBB 形式で指定してください。例：#3366CC\nキャンセルすると色見本に戻ります。", 7);
                });
            Add("キャンセル", MappingBounds(730, 500, 190, 40), CloseMappingPicker);
        }
        else
        {
            if (mappingKnowledgeComments) AddGenreTabs();
            AddGenreGridLayoutButton();
            if (!mappingKnowledgeComments || genrePageTab is 2 or 3)
            {
                Add(GenreGridSplit ? "前へ" : "前のページ", MappingGridBounds(20, 460, 160, 32), () => ScrollGenreOrMapping(-MappingVisibleRows), GenreChartVisible ? genrePreviewScroll > 0 : mappingScroll > 0,
                    tooltip: "前のページの行を表示します。PageUpでも移動できます。");
                Add(GenreGridSplit ? "次へ" : "次のページ", MappingGridBounds(192, 460, 160, 32), () => ScrollGenreOrMapping(MappingVisibleRows), GenreChartVisible ? genrePreviewScroll + GenrePreviewPageSize < BuildGenrePreviewGroups().Count : mappingScroll + MappingVisibleRows < draft.Rows.Count,
                    tooltip: "次のページの行を表示します。PageDownでも移動できます。");
            }
            if (GenreGridVisible)
            {
                Add("新規作成", GenreRowActionBounds(0, 120), CreateGenreRow,
                    !GenreRowActionsRight || packageGenreTable is not null,
                    tooltip: $"操作対象の上に新しい{MappingRowLabel}を作り、その行を操作対象にします。");
                if (GenreGridSplit)
                    Add("反対側へコピー", GenreRowActionBounds(132, 200), CopyGenreToOtherPane,
                        packageGenreTable is not null && (selectedGenreKey is not null || selectedPackageGenreKey is not null),
                        tooltip: "対象の行を反対側の表へコピーします。右側の変更は作業中の表に保持します。");
                Add("削除", GenreRowActionBounds(GenreGridSplit ? 344 : 132, 100), DeleteGenreRow,
                    GenreRowActionsRight ? selectedPackageGenreKey is not null : selectedGenreKey is not null,
                    tooltip: "操作対象の行を表から削除します。サークルや配置の値は変更しません。");
            }
            Add("閉じる", MappingCloseBounds, SaveStyleMapping, MappingCommentsCanClose && mappingComposition.Length == 0,
                tooltip: hasChanges ? "変更は自動で保存されます。前のページに戻ります。" : "前のページに戻ります。");
            if (mappingKnowledgeComments)
            {
                if (GenreProjectChanged)
                    Add("プロジェクトへの変更を破棄", GenreTagButtonBounds(right: false), () => DiscardGenreSide(project: true),
                        tooltip: "プロジェクトを編集開始時点へ戻し、自動保存します。パッケージの変更は残します。");
                if (GenrePackageChanged)
                    Add("パッケージへの変更を破棄", GenreTagButtonBounds(right: true), () => DiscardGenreSide(project: false),
                        tooltip: "読み込み時点のパッケージへ戻し、自動保存します。この画面で作業を続けられます。");
            }
            else if (hasChanges)
                Add("破棄", MappingDiscardBounds, DiscardStyleMapping,
                    tooltip: "変更を元に戻して、前のページに戻ります。");
        }
        if (mappingFocus >= mappingEditorButtons.Count) mappingFocus = -1;
    }

    private void ScrollMappingRows(int offset)
    {
        if (GenreGridVisible) { SetGenreScroll(false, mappingScroll + offset); return; }
        var lastPage = Math.Max(0, (mappingDraft!.Rows.Count - 1) / MappingVisibleRows);
        var page = Math.Clamp(mappingScroll / MappingVisibleRows + Math.Sign(offset), 0, lastPage);
        mappingScroll = page * MappingVisibleRows;
        mappingRow = Math.Clamp(mappingRow, mappingScroll, Math.Max(mappingScroll, Math.Min(mappingDraft.Rows.Count - 1, mappingScroll + MappingVisibleRows - 1)));
        mappingWidth = -1;
    }

    private void UpdateStyleMappingEditor(KeyboardState keyboard, MouseState mouse)
    {
        if (mappingDraft is not { } draft) return;
        if (genreOrderDialogOpen) { UpdateGenreOrderDialog(keyboard, mouse); return; }
        if (genrePieExpanded) { UpdateExpandedGenrePie(keyboard, mouse); return; }
        BuildMappingEditorButtons();
        if (UpdateGenreScrollbars(mouse)) return;
        if (mappingKnowledgeComments && mappingPickerColumn == 0 && mouse.LeftButton == ButtonState.Pressed &&
            previousMouse.LeftButton == ButtonState.Released && Contains(MappingTableNameBounds, new(mouse.X, mouse.Y)))
        {
            SetMappingTextFocus(false);
            OpenUnderlineInput("表名", mappingGenreCodeTableName, value =>
            {
                mappingGenreCodeTableName = GenreStyleDefinition.NormalizeTableName(value);
                mappingWidth = -1;
            }, "イベントに保存する表の名前です。パッケージへ単独で書き出すときもこの名前を使います。", int.MaxValue,
                validate: value => { try { GenreStyleDefinition.NormalizeTableName(value); return null; } catch (ArgumentException ex) { return ex.Message; } });
            return;
        }
        if (mappingKnowledgeComments && mappingPickerColumn == 0 && mouse.LeftButton == ButtonState.Pressed &&
            previousMouse.LeftButton == ButtonState.Released && Contains(MappingOverallCommentBounds, new(mouse.X, mouse.Y)))
        {
            OpenMappingOverallComment();
            return;
        }
        if (mappingPickerColumn == 0 && UpdateMappingChangeTag(keyboard, mouse)) return;
        if (IsPressed(keyboard, Keys.Escape))
        {
            if (mappingPickerColumn > 0) CloseMappingPicker(); else SaveStyleMapping();
            return;
        }
        if (mappingPickerColumn == 0 && IsControlDown(keyboard) && IsPressed(keyboard, Keys.S)) { SaveStyleMapping(); return; }
        if (GenreChartVisible && !mappingTextFocused)
        {
            if (IsPressed(keyboard, Keys.PageUp)) ScrollGenreOrMapping(-1);
            if (IsPressed(keyboard, Keys.PageDown)) ScrollGenreOrMapping(1);
        }
        if (IsPressed(keyboard, Keys.Tab))
        {
            var backwards = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            if (mappingPickerColumn > 0)
                mappingFocus = (mappingFocus + (backwards ? mappingEditorButtons.Count - 1 : 1)) % mappingEditorButtons.Count;
            else mappingFocus = backwards ? mappingFocus < 0 ? mappingEditorButtons.Count - 1 : mappingFocus - 1
                : (mappingFocus + 2) % (mappingEditorButtons.Count + 1) - 1;
        }
        if (mappingPickerColumn > 0)
        {
            var columns = mappingPickerColumn == 3 ? 4 : 5;
            var offset = IsPressed(keyboard, Keys.Right) ? 1 : IsPressed(keyboard, Keys.Left) ? -1
                : IsPressed(keyboard, Keys.Down) ? columns : IsPressed(keyboard, Keys.Up) ? -columns : 0;
            mappingFocus = Math.Clamp(mappingFocus + offset, 0, mappingEditorButtons.Count - 1);
        }
        else if (mappingFocus < 0 && !GenreChartVisible && GenreGridSplit && selectedPackageGenreKey is { } packageKey)
        {
            var rows = PackageGenreRows();
            var index = Array.FindIndex(rows, row => row.Key == packageKey);
            var delta = IsPressed(keyboard, Keys.Up) ? -1 : IsPressed(keyboard, Keys.Down) ? 1 : 0;
            if (delta != 0 && rows.Length > 0) SelectPackageGenreTarget(rows[Math.Clamp(index + delta, 0, rows.Length - 1)].Key);
            if (IsPressed(keyboard, Keys.PageUp)) ScrollPackageGenreRows(-MappingVisibleRows);
            if (IsPressed(keyboard, Keys.PageDown)) ScrollPackageGenreRows(MappingVisibleRows);
        }
        else if (mappingFocus < 0 && !GenreChartVisible)
        {
            if (IsPressed(keyboard, Keys.Left)) mappingColumn = mappingKnowledgeComments
                ? mappingColumn switch { 6 => 4, 4 => 3, 3 => 2, 2 => 1, _ => 0 }
                : mappingColumn == 5 ? 3 : Math.Max(1, mappingColumn - 1);
            if (IsPressed(keyboard, Keys.Right)) mappingColumn = mappingKnowledgeComments
                ? mappingColumn switch { 0 => 2, 2 => 3, 3 => 4, 4 => 6, _ => 6 }
                : mappingColumn >= 3 ? 3 : mappingColumn + 1;
            if (IsPressed(keyboard, Keys.Up)) mappingRow = Math.Max(0, mappingRow - 1);
            if (IsPressed(keyboard, Keys.Down)) mappingRow = Math.Min(Math.Max(0, draft.Rows.Count - 1), mappingRow + 1);
            if (IsPressed(keyboard, Keys.Up) || IsPressed(keyboard, Keys.Down))
            {
                if (mappingKnowledgeComments && draft.Rows.Count > 0)
                {
                    selectedGenreKey = draft.Rows[mappingRow].Key;
                    selectedPackageGenreKey = null;
                }
                mappingScroll = GenreGridVisible ? EnsureGenreRowVisible(mappingScroll, mappingRow, draft.Rows.Count)
                    : mappingRow / MappingVisibleRows * MappingVisibleRows;
                mappingWidth = -1;
            }
            if (IsPressed(keyboard, Keys.PageUp)) ScrollMappingRows(-MappingVisibleRows);
            if (IsPressed(keyboard, Keys.PageDown)) ScrollMappingRows(MappingVisibleRows);
        }
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            if (mappingFocus < 0 && !GenreChartVisible && selectedPackageGenreKey is null) OpenMappingPicker(mappingRow, mappingColumn);
            else if (mappingFocus >= 0 && mappingEditorButtons[mappingFocus].Button.IsEnabled) mappingEditorButtons[mappingFocus].Execute();
            return;
        }
        if (mappingPickerColumn == 0 && GenreGridSplit && mouse.ScrollWheelValue != previousMouse.ScrollWheelValue &&
            Contains(GenreScrollArea(right: true), new(mouse.X, mouse.Y)))
        {
            ScrollPackageGenreRows(-Math.Sign(mouse.ScrollWheelValue - previousMouse.ScrollWheelValue) * 3);
            BuildMappingEditorButtons();
        }
        if (mappingPickerColumn == 0 && mouse.ScrollWheelValue != previousMouse.ScrollWheelValue &&
            (!GenreGridVisible || Contains(GenreScrollArea(right: false), new(mouse.X, mouse.Y))))
        {
            ScrollGenreOrMapping(-Math.Sign(mouse.ScrollWheelValue - previousMouse.ScrollWheelValue) * 3);
            BuildMappingEditorButtons();
        }
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        foreach (var item in mappingEditorButtons) item.Button.UpdatePointer(pointer);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            if (GenreGridVisible && mappingPickerColumn == 0 && TryClickGenreGridRow(pointer)) return;
            if (mappingKnowledgeComments && mappingPickerColumn == 0)
            {
                if (GenreGridSplit)
                {
                    var rows = PackageGenreRows();
                    for (var row = 0; row < MappingVisibleRows && packageGenreScroll + row < rows.Length; row++)
                        if (Contains(MappingCell(row, 1, right: true), pointer))
                        {
                            SelectPackageGenreTarget(rows[packageGenreScroll + row].Key);
                            return;
                        }
                }
                if (genrePageTab == 1)
                {
                    var groups = BuildGenrePreviewGroups();
                    for (var index = 0; index < groups.Count; index++)
                        if (Contains(GenreCatalogTile(index, groups.Count), pointer))
                        {
                            SelectGenreTarget(groups[index].GenreId, navigate: true);
                            return;
                        }
                }
                else if (genrePageTab == 0)
                    for (var row = 0; row < MappingVisibleRows && mappingScroll + row < draft.Rows.Count; row++)
                        if (Contains(MappingCell(row, 1), pointer))
                        {
                            SelectGenreTarget(draft.Rows[mappingScroll + row].Key);
                            return;
                        }
            }
            pressedMappingButton = mappingEditorButtons.Select(item => item.Button).FirstOrDefault(button => button.Press(pointer));
            if (pressedMappingButton is not null) mappingFocus = mappingEditorButtons.FindIndex(item => item.Button == pressedMappingButton);
            else if (mappingPickerColumn == 0 && !GenreChartVisible)
                for (var row = 0; row < MappingVisibleRows && mappingScroll + row < draft.Rows.Count; row++)
                for (var column = mappingKnowledgeComments ? 0 : 1; column <= (mappingKnowledgeComments ? 6 : 3); column++)
                    if (column != (mappingKnowledgeComments ? 5 : 4))
                    if (Contains(MappingCell(row, column), pointer)) { OpenMappingPicker(mappingScroll + row, column); return; }
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedMappingButton;
            pressedMappingButton = null;
            if (pressed?.Release(pointer) == true) mappingEditorButtons.Single(item => item.Button == pressed).Execute();
        }
    }

    private void DrawStyleMappingEditor()
    {
        using var timing = performance?.Measure("genre_draw");
        if (mappingDraft is not { } draft) return;
        if (genreOrderDialogOpen) { DrawGenreOrderDialog(); return; }
        if (genrePieExpanded) { DrawExpandedGenrePie(); return; }
        BuildMappingEditorButtons();
        void Text(string text, ScreenRectangle bounds, int size = 17, Color? color = null) =>
            textRenderer?.Draw(text, ToRectangle(bounds, 3), color ?? Color.White, Math.Max(10, (int)(size * MappingEditorScale)), true);
        Text(mappingBlocks ? "ブロック ＞ 網掛け対応表" : "ジャンル",
            MappingBounds(20, 18, mappingBlocks ? 620 : 78, 38), 22);
        if (mappingKnowledgeComments && genrePageTab == 3)
            Text("スペース数順", MappingBounds(700, 18, 280, 36), 18, new Color(180, 220, 230));
        if (mappingKnowledgeComments)
        {
            DrawTableHeader(mappingGenreCodeTableName, draft.OverallComment);
        }
        if (GenreChartVisible) DrawGenrePreview();
        else
        {
            var headers = mappingKnowledgeComments
                ? new[] { GenreGridSplit ? "順" : "並び順", MappingRowLabel, "太線色", "細線色", "網掛け", "見本", "コメント" }
                : new[] { mappingKeyLabel, "太線色", "細線色", "網掛け（白黒見本）", "配色の見本" };
            DrawRectangle(MappingGridBounds(20, 102, 960, 34), new Color(48, 65, 77));
            for (var column = 0; column < headers.Length; column++)
                Text(headers[column], MappingGridBounds(MappingColumnEdges[column], 104, MappingColumnEdges[column + 1] - MappingColumnEdges[column] - 6, 30), GenreGridSplit ? 13 : 17);
            for (var row = 0; row < MappingVisibleRows && mappingScroll + row < draft.Rows.Count; row++)
            {
                var style = draft.Rows[mappingScroll + row];
                for (var column = 0; column < headers.Length; column++)
                {
                    var bounds = MappingCell(row, column);
                    if (mappingKnowledgeComments && column == 3 && style.Pattern == "solid")
                        continue;
                    var plainCell = mappingKnowledgeComments && column is 1 or 6;
                    if (!plainCell) DrawRectangle(bounds, new Color(35, 43, 54));
                    if (column == 0)
                    {
                        var orderIndex = Array.IndexOf(mappingGenreCodeOrder, style.Key);
                        var value = (orderIndex >= 0 ? orderIndex + 1 : mappingScroll + row + 1).ToString();
                        var size = Math.Max(10, (int)(17 * MappingEditorScale));
                        var measured = textRenderer?.Measure(value, size).X ?? 0;
                        textRenderer?.Draw(value, ToRectangle(new(bounds.X + bounds.Width - measured - 8, bounds.Y, measured + 4, bounds.Height), 3), Color.White, size, true);
                    }
                    else if (column == 1) Text(style.Key, bounds);
                    else if (column == 6) Text(style.KnowledgeComment ?? "コメントを入力", bounds, 14);
                    else if (column is 2 or 3)
                    {
                        if (column == 3 && style.Pattern == "solid")
                            continue;
                        else
                        {
                            var id = column == 2 ? style.PrimaryColor : style.SecondaryColor;
                            var color = GenreColorFromId(id, Color.Gray);
                            DrawRectangle(bounds, color);
                            Text(StyleMappingDraft.Colors.FirstOrDefault(choice => choice.Id == id).Label ?? id, bounds, 17, MappingColorText(color));
                        }
                    }
                    else
                    {
                        var swatch = new ScreenRectangle(bounds.X + 4, bounds.Y + 3, bounds.Width - 8,
                            column == 4 && !mappingKnowledgeComments ? bounds.Height * 0.54 : bounds.Height - 6);
                        DrawRectangle(swatch, column == 4 ? Color.Black : GenreColorFromId(style.PrimaryColor, Color.Gray));
                        DrawGenrePattern(swatch, GenrePatternFromId(style.Pattern), column == 4 ? Color.White : GenreColorFromId(style.SecondaryColor, Color.White), 255);
                        if (column == 4 && !mappingKnowledgeComments) Text(StyleMappingDraft.Patterns.FirstOrDefault(choice => choice.Id == style.Pattern).Label ?? style.Pattern,
                            new(bounds.X, bounds.Y + bounds.Height * 0.58, bounds.Width, bounds.Height * 0.4), 12);
                    }
                    if (!plainCell) DrawOutline(bounds, 1, new Color(100, 119, 130));
                    if (!GenreGridVisible && mappingPickerColumn == 0 && mappingFocus < 0 && selectedGenreKey is null && selectedPackageGenreKey is null && mappingRow == mappingScroll + row && mappingColumn == column)
                    {
                        if (plainCell)
                            DrawLine(new(bounds.X + 6, bounds.Y + bounds.Height - 7), new(bounds.X + bounds.Width - 6, bounds.Y + bounds.Height - 7), 2, OperationTargetColor);
                        else DrawOutline(bounds, 2, OperationTargetColor);
                    }
                }
                if (mappingKnowledgeComments && style.Key == selectedGenreKey)
                {
                    DrawOutline(GenreTargetRowBounds(row), 2 * MappingEditorScale, OperationTargetColor);
                    DrawGenreCellHover(row, style.Pattern, false);
                }
            }
            if (draft.Rows.Count == 0) Text(mappingEmptyMessage, MappingGridBounds(20, 142, 960, 46));
            if (GenreGridSplit) DrawPackageGenreGrid();
            if (GenreGridVisible) DrawGenreScrollbars();
        }
        DrawMappingChangeTag();
        if (mappingKnowledgeComments) DrawPackageChangeTag();
        if (mappingPickerColumn > 0)
        {
            DrawRectangle(new(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 190));
            var panel = MappingBounds(60, 90, 880, 470);
            DrawRectangle(panel, new Color(24, 29, 36));
            DrawOutline(panel, 2, Color.LightSlateGray);
            Text(mappingPickerColumn == 3 ? "網掛けを選択 — 黒が太線色、白が細線色" : mappingPickerColumn == 1 ? "太線色を選択" : "細線色を選択", MappingBounds(80, 108, 840, 38), 23);
        }
        for (var index = 0; index < mappingEditorButtons.Count; index++)
        {
            var item = mappingEditorButtons[index];
            var button = item.Button;
            OperationButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) =>
                {
                    if (button.AccessibleName == GenreGridLayoutButtonName) DrawGenreGridShip(area, ToButtonColor(color));
                    else if (item.ColorId is null && item.PatternId is null) Text(button.AccessibleName, area, 17, ToButtonColor(color));
                });
            var bounds = button.Bounds;
            if (item.ColorId is { } colorId)
            {
                var color = GenreColorFromId(colorId, Color.Gray);
                var inside = new ScreenRectangle(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8);
                DrawRectangle(inside, color);
                Text(button.AccessibleName, inside, 17, MappingColorText(color));
            }
            if (item.PatternId is { } pattern)
            {
                var inside = new ScreenRectangle(bounds.X + 8, bounds.Y + 8, bounds.Width - 16, Math.Max(1, bounds.Height - 36 * MappingEditorScale));
                DrawRectangle(inside, Color.Black);
                DrawGenrePattern(inside, GenrePatternFromId(pattern), Color.White, 255);
                Text(button.AccessibleName, new(bounds.X + 4, bounds.Y + bounds.Height - 27 * MappingEditorScale, bounds.Width - 8, 26 * MappingEditorScale), 17);
            }
            if (mappingFocus == index && button.IsEnabled) DrawOutline(bounds, 2, OperationTargetColor);
        }
        DrawMappingButtonTooltip();
    }

    private void DrawMappingButtonTooltip()
    {
        if (modalDialog is not null) return;
        var mouse = Mouse.GetState();
        var hovered = CanShowEditorHover ? mappingEditorButtons.FirstOrDefault(item => Contains(item.Button.Bounds, new(mouse.X, mouse.Y))) : null;
        var focused = !mappingTextFocused && mappingFocus >= 0 && mappingFocus < mappingEditorButtons.Count
            ? mappingEditorButtons[mappingFocus] : null;
        var tooltip = hovered?.Tooltip;
        if (GenreGridVisible && mappingPickerColumn == 0 && CanShowEditorHover)
            for (var side = 0; side < (GenreGridSplit ? 2 : 1); side++)
            {
                var right = side == 1;
                if (!Contains(GenreScrollTrack(right), new(mouse.X, mouse.Y))) continue;
                var count = GenreScrollCount(right);
                var start = right ? packageGenreScroll : mappingScroll;
                tooltip = $"{(right ? "パッケージ" : "プロジェクト")}：{(count == 0 ? 0 : start + 1)} - {Math.Min(count, start + MappingVisibleRows)} / {count} 件";
                break;
            }
        if (mappingKnowledgeComments && mappingPickerColumn == 0 && CanShowEditorHover && Contains(MappingOverallCommentBounds, new(mouse.X, mouse.Y)))
            tooltip = "全体コメントをクリックして編集します。1000文字以内、空欄で削除できます。";
        if (GenreChartVisible) tooltip ??= GenrePreviewTooltip(new(mouse.X, mouse.Y));
        if (mappingPickerColumn > 0)
            tooltip ??= "見本をクリックして選択　Tab・矢印：移動　Enter：決定　Esc：キャンセル";
        else if (tooltip is null && mappingDraft is { } draft)
        {
            if (CanShowEditorHover)
                for (var row = 0; row < MappingVisibleRows && mappingScroll + row < draft.Rows.Count; row++)
                    for (var column = 1; column < MappingColumnEdges.Length - 1; column++)
                        if (Contains(MappingCell(row, column), new(mouse.X, mouse.Y)))
                            tooltip = column switch
                            {
                                1 => "並び順をクリックして、ジャンルコード順とコメントを編集します。",
                                6 => "コメントをクリックして全文を編集します。1000文字以内、空欄で削除できます。",
                                4 or 5 => "黒＝太線色、白＝細線色。網掛けのセルをクリックしてパターンを選択します。",
                                _ => "色のセルをクリックして太線色・細線色を選択します。単色の細線色は未使用です。",
                            };
            tooltip ??= focused?.Tooltip;
            tooltip ??= mappingKnowledgeComments
                ? "色・網掛け・コメントをクリックして編集　矢印：セル移動　Enter：選択　Tab：操作ボタンへ"
                : "色・網掛けをクリックして編集　矢印：セル移動　Enter：選択　Tab：操作ボタンへ";
        }
        DrawStatusBar(tooltip, $"{mappingKeyLabel}：{mappingDraft?.Rows.Count ?? 0} 件");
    }

    private static Color MappingColorText(Color color) => color.R * 0.299 + color.G * 0.587 + color.B * 0.114 < 145 ? Color.White : Color.Black;
}
