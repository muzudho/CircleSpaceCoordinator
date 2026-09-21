namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private sealed record EventListEntry(EventProjectReference Project, bool Exists, bool? Confidential, string? Error);
    private readonly EventProjectMetadataCache eventMetadata = new(ProjectFileService.Load);
    private readonly List<EventListEntry> eventProjects = [];
    private readonly List<(IconButtonModel Button, Action Execute)> eventButtons = [];
    private IconButtonModel? pressedEventButton;
    private int eventSelection;
    private int eventScroll;
    private int eventFocus = -1;
    private int eventWidth = -1;
    private int eventHeight = -1;
    private int lastEventClick = -1;
    private DateTime lastEventClickAt;
    private EventProjectCatalogService EventCatalog => new(settings!);
    private EventListEntry? SelectedEvent => eventProjects.ElementAtOrDefault(eventSelection);
    private EventListStyle eventStyle = null!;
    private EventListLayout? eventLayout;
    private int eventLayoutWidth = -1, eventLayoutHeight = -1, eventLayoutRevision = -1;
    private EventListLayout EventLayout
    {
        get
        {
            var width = GraphicsDevice.Viewport.Width;
            var height = GraphicsDevice.Viewport.Height;
            if (eventLayout is null || eventLayoutWidth != width || eventLayoutHeight != height || eventLayoutRevision != eventStyle.Revision)
            {
                eventLayout = eventStyle.Arrange(width, height, WorkerBarHeight);
                eventLayoutWidth = width;
                eventLayoutHeight = height;
                eventLayoutRevision = eventStyle.Revision;
                eventWidth = -1;
            }
            return eventLayout;
        }
    }
    private int EventRows => EventLayout.VisibleRows;
    private ScreenRectangle EventRowBounds(int row) => EventLayout.Row(row);

    private void ToggleStyleAutoReload()
    {
        settings!.SaveStyleAutoReload(!settings.Current.StyleAutoReload);
        eventStyle.Update(TimeSpan.Zero, settings.Current.StyleAutoReload);
        eventWidth = -1;
    }

    private void RefreshEventProjects(string? selectedPath = null)
    {
        lastEventClick = -1;
        eventProjects.Clear();
        foreach (var project in EventCatalog.Projects)
        {
            var cached = eventMetadata.Peek(project.Path);
            eventProjects.Add(new(project, cached?.Exists ?? true, cached?.Confidential, cached?.Error));
        }
        eventSelection = Math.Max(0, eventProjects.FindIndex(item => string.Equals(item.Project.Path, selectedPath, StringComparison.OrdinalIgnoreCase)));
        eventScroll = Math.Clamp(eventSelection, 0, Math.Max(0, eventProjects.Count - EventRows));
        eventWidth = -1;
        pressedEventButton = null;
    }

    private void InspectSelectedEvent()
    {
        if (SelectedEvent is not { } selected) return;
        var metadata = eventMetadata.Inspect(selected.Project.Path);
        eventProjects[eventSelection] = selected with { Exists = metadata.Exists, Confidential = metadata.Confidential, Error = metadata.Error };
        eventWidth = -1;
    }

    private void EnsureEventButtons()
    {
        var layout = EventLayout;
        if (eventWidth == GraphicsDevice.Viewport.Width && eventHeight == GraphicsDevice.Viewport.Height) return;
        eventWidth = GraphicsDevice.Viewport.Width;
        eventHeight = GraphicsDevice.Viewport.Height;
        eventScroll = Math.Clamp(eventScroll, 0, Math.Max(0, eventProjects.Count - EventRows));
        pressedEventButton = null;
        eventButtons.Clear();
        var selected = SelectedEvent;
        var usable = selected is { Exists: true, Error: null };
        void Add(string label, Action execute, bool enabled = true)
        {
            var bounds = layout.Area("body/actions/" + EventListStyle.Actions[eventButtons.Count]);
            eventButtons.Add((new IconButtonModel(bounds, label) { IsEnabled = enabled && !eventStartupLoading }, execute));
        }
        Add("開く", () => { if (SelectedEvent is { } item) OpenEventProject(item.Project.Path); }, usable);
        Add("新規作成", CreateEventProject);
        Add("編集", EditEventProject, usable);
        Add("既存ファイルを登録", RegisterEventProject);
        Add("複製", DuplicateEventProject, usable);
        Add("マル秘に設定", MarkEventConfidential, usable && selected?.Confidential != true);
        Add("上へ", () => MoveEvent(-1), eventSelection > 0);
        Add("下へ", () => MoveEvent(1), eventSelection < eventProjects.Count - 1);
        Add("一覧から除外", RemoveEvent, selected is not null);
        Add("終了", Exit);
        eventButtons.Add((new IconButtonModel(layout.Area("reload"),
            "スタイル設定のオートリロード：" + (settings!.Current.StyleAutoReload ? "有効" : "無効"))
            { IsEnabled = !eventStartupLoading }, ToggleStyleAutoReload));
        if (eventFocus >= eventButtons.Count) eventFocus = -1;
    }

    private void UpdateEventProjects(KeyboardState keyboard, MouseState mouse)
    {
        EnsureEventButtons();
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        foreach (var (button, _) in eventButtons) button.UpdatePointer(pointer);
        if (IsPressed(keyboard, Keys.Tab))
            eventFocus = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)
                ? (eventFocus < 0 ? eventButtons.Count - 1 : eventFocus - 1)
                : (eventFocus + 2) % (eventButtons.Count + 1) - 1;
        if (eventFocus == -1)
        {
            var next = eventSelection;
            if (IsPressed(keyboard, Keys.Up)) next--;
            if (IsPressed(keyboard, Keys.Down)) next++;
            if (IsPressed(keyboard, Keys.PageUp)) next -= EventRows;
            if (IsPressed(keyboard, Keys.PageDown)) next += EventRows;
            if (IsPressed(keyboard, Keys.Home)) next = 0;
            if (IsPressed(keyboard, Keys.End)) next = eventProjects.Count - 1;
            next = Math.Clamp(next, 0, Math.Max(0, eventProjects.Count - 1));
            if (next != eventSelection)
            {
                eventSelection = next;
                InspectSelectedEvent();
                eventScroll = Math.Clamp(eventScroll, Math.Max(0, next - Math.Max(1, EventRows) + 1), next);
                eventWidth = -1;
                EnsureEventButtons();
            }
        }
        if (mouse.ScrollWheelValue != previousMouse.ScrollWheelValue)
            eventScroll = Math.Clamp(eventScroll - Math.Sign(mouse.ScrollWheelValue - previousMouse.ScrollWheelValue) * 3,
                0, Math.Max(0, eventProjects.Count - EventRows));
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            if (eventFocus < 0) { if (SelectedEvent is { Exists: true, Error: null } item) OpenEventProject(item.Project.Path); }
            else if (eventButtons[eventFocus].Button.IsEnabled) RunEventAction(eventButtons[eventFocus].Execute);
            return;
        }
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            for (var row = 0; row < EventRows && eventScroll + row < eventProjects.Count; row++)
                if (Contains(EventRowBounds(row), pointer))
                {
                    eventSelection = eventScroll + row;
                    InspectSelectedEvent();
                    var doubleClick = lastEventClick == eventSelection && (DateTime.UtcNow - lastEventClickAt).TotalSeconds < 0.5;
                    lastEventClick = eventSelection;
                    lastEventClickAt = DateTime.UtcNow;
                    eventFocus = -1;
                    eventWidth = -1;
                    EnsureEventButtons();
                    if (doubleClick && SelectedEvent is { Exists: true, Error: null } item) OpenEventProject(item.Project.Path);
                    return;
                }
            pressedEventButton = eventButtons.Select(item => item.Button).FirstOrDefault(button => button.Press(pointer));
            if (pressedEventButton is not null) eventFocus = eventButtons.FindIndex(item => item.Button == pressedEventButton);
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedEventButton;
            pressedEventButton = null;
            if (pressed?.Release(pointer) == true)
                RunEventAction(eventButtons.Single(item => item.Button == pressed).Execute);
        }
    }

    private void DrawEventProjects()
    {
        EnsureEventButtons();
        void Text(string text, ScreenRectangle bounds, int size = 18, bool bold = false) =>
            textRenderer?.Draw(text, ToRectangle(bounds), Color.White, size, bold);
        Text("イベントプロジェクト一覧", EventLayout.Area("title"), 28, true);
        Text("この版から編集は自動保存されます。［プロジェクト］の［すぐ保存］／［セーブポイント］も利用できます。", EventLayout.Area("description"));
        for (var row = 0; row < EventRows && eventScroll + row < eventProjects.Count; row++)
        {
            var index = eventScroll + row;
            var item = eventProjects[index];
            var bounds = EventRowBounds(row);
            var button = new IconButtonModel(bounds, item.Project.DisplayName) { IsSelected = index == eventSelection, IsEnabled = !eventStartupLoading };
            button.UpdatePointer(new ScreenPoint(previousMouse.X, previousMouse.Y));
            OperationButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)), (_, _) => { });
            if (!eventStartupLoading && eventFocus < 0 && index == eventSelection) DrawOutline(bounds, 2, OperationTargetColor);
            Text((item.Confidential == true ? "（秘） " : "") + item.Project.DisplayName, EventLayout.Row(row, "/title"), 19, true);
            var detail = !item.Exists ? "ファイルが見つかりません：" + item.Project.Path : item.Error ?? (item.Confidential is null ? "未確認：" : "") + item.Project.Path;
            var maxCharacters = Math.Max(12, (int)(EventLayout.Row(row, "/detail").Width / 8));
            if (detail.Length > maxCharacters)
            {
                var prefix = maxCharacters / 3;
                detail = detail[..prefix] + "…" + detail[^(maxCharacters - prefix - 1)..];
            }
            Text(detail,
                EventLayout.Row(row, "/detail"), 13);
        }
        if (eventProjects.Count == 0) Text("イベントはまだありません。［新規作成］または［既存ファイルを登録］を選んでください。", EventLayout.Area("body/list"));
        for (var index = 0; index < eventButtons.Count; index++)
        {
            var button = eventButtons[index].Button;
            button.IsSelected = false;
            OperationButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) => textRenderer?.Draw(button.AccessibleName, ToRectangle(area, 5), ToButtonColor(color), 17, true));
            if (eventFocus == index && button.IsEnabled) DrawOutline(button.Bounds, 2, OperationTargetColor);
        }
        Text($"{eventProjects.Count} 件　↑↓：選択　Enter：開く　Tab：操作へ移動　ホイール：スクロール",
            EventLayout.Area("footer"), 16);
        Text(eventStyle.LastError is { } error ? "スタイル設定エラー（直前の配置を維持）：" + error
            : "スタイル設定：" + eventStyle.FilePath, EventLayout.Area("error"), 13);
        if (eventStartupLoading) DrawEventStartupSpinner();
    }

    private void DrawEventStartupSpinner()
    {
        var area = EventLayout.Area("body/list");
        DrawRectangle(area, new Color(12, 18, 28, 150));
        var center = new ScreenPoint(area.X + area.Width / 2, area.Y + area.Height / 2 - 16);
        // A time-based ring keeps animating while engine startup runs on the worker thread.
        for (var index = 0; index < 12; index++)
        {
            var angle = statusHintTime * Math.PI * 2 + index * Math.PI / 6;
            var color = Color.Lerp(new Color(44, 66, 80), new Color(120, 220, 255), index / 11f);
            DrawLine(new(center.X + Math.Cos(angle) * 16, center.Y + Math.Sin(angle) * 16),
                new(center.X + Math.Cos(angle) * 27, center.Y + Math.Sin(angle) * 27), 4, color);
        }
        textRenderer?.Draw("起動しています…", ToRectangle(new ScreenRectangle(center.X - 90, center.Y + 40, 180, 28)), Color.White, 18, true);
    }

    private void RunEventAction(Action action)
    {
        try { action(); }
        catch (Exception ex) { ShowInAppMessage("イベントプロジェクト", ex.Message); }
    }

    private void OpenEventProject(string path)
    {
        RunEventAction(() =>
        {
            if (workspace is not null) return;
            var registered = EventCatalog.Register(path);
            var opened = DesktopApplication.LoadWorkspace(registered.Path);
            try
            {
                workspace = opened;
                workspace.HandleProvider = () => Handle;
                if (workspace is not null) workspace.WorkDateProvider = () => WorkDate;
                projectSavePath = registered.Path;
                dragController = new DeskDragController(opened, viewport);
                commandController = new EditorCommandController(opened);
                participantController = new ParticipantPlacementController(opened);
                editorMode = EditorMode.DeskPlacement;
                activeCanvasTool = ToolbarAction.MoveDesk;
                RestoreWorkingState();
                InitializeAutoSave();
                CreateToolbar();
                settings!.RememberProject(projectSavePath);
            }
            catch
            {
                opened.Dispose();
                workspace = null;
                projectSavePath = null;
                dragController = null;
                commandController = null;
                participantController = null;
                throw;
            }
            modalInputDrain = true;
            Log("project_loaded", true, detail: $"path={projectSavePath}");
        });
    }

    private void RequestReturnToEvents()
    {
        if (workspace is null || optimizationTask is not null) return;
        if (FlushAutoSave()) CloseEventProject();
    }

    private void CloseEventProject()
    {
        savedProjectState = null;
        autoSaveSession = null;
        autoSaveOwner = null;
        autoSaveError = null;
        projectMenuOpen = false;
        projectMenuDrain = true;
        PersistWorkingState();
        workspace?.Dispose();
        workspace = null;
        vacancyProject = null;
        vacancyCache.Clear();
        numberGapsProject = null;
        numberGapsPlanId = null;
        numberGaps = null;
        capacityProject = null;
        capacitySummary = null;
        dragController = null;
        commandController = null;
        participantController = null;
        var closedPath = projectSavePath;
        if (closedPath is not null) eventMetadata.Invalidate(closedPath);
        projectSavePath = null;
        CancelInProgressPointerInteraction();
        toolRingOpen = spaceCatalogOpen = false;
        toolRingInputDrain = spaceCatalogDrain = false;
        selectedCellRange = null;
        selectedFrameIds.Clear();
        topologyFirstDeskId = null;
        topologyFirstCell = topologyFirstCorner = null;
        rangeSwapStatus = screenshotStatus = null;
        participantTable = null;
        tableProject = null;
        tableTextPage = null;
        tableScroll.MoveTo(0, 0, 0, 0, 1, 1);
        selectedChannelId = null;
        selectedNumberChannel = channelScroll = planScroll = circleChannelScroll = 0;
        circleDisplayOverride = null;
        lastCircleDisplayKey = null;
        showEvaluationAnalysis = false;
        editorMode = EditorMode.DeskPlacement;
        nextDeskOrientation = CircleSpaceCoordinator.Core.Geometry.QuarterTurn.North;
        RefreshEventProjects(closedPath);
        eventFocus = -1;
        modalInputDrain = true;
    }

    private void PromptEventPath(string title, string initial, Action<string> accepted) =>
        OpenUnderlineInput(title, initial, path => accepted(Path.GetFullPath(path.Trim('"'))),
            "イベントプロジェクト（*.event-project-csc.json）のパスを入力・貼り付けしてください。\n従来のプロジェクトJSONも登録できます。", 32767);

    private string SuggestedEventPath(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var filename = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return Path.Combine(settings!.Current.ProjectsDirectory, (string.IsNullOrWhiteSpace(filename) ? "event" : filename) + ".event-project-csc.json");
    }

    private void CreateEventProject() => OpenUnderlineInput("新しいイベント", "新しいイベント", name =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "新規イベントの公開区分",
            "このイベントを［マル秘］に設定しますか？\nマル秘を設定すると注意表示が出ます。アプリから解除はできません。"), choice =>
        {
            if (choice == ModalDialogAction.Cancel) return;
            PromptEventPath("新規イベントの保存先", SuggestedEventPath(name), path =>
                RefreshEventProjects(EventCatalog.Create(path, name, choice == ModalDialogAction.Accept).Path));
        }, [("マル秘にする", ModalDialogAction.Accept), ("通常", ModalDialogAction.Decrease), ("キャンセル", ModalDialogAction.Cancel)]);
    }, "イベント名を入力してください（100 文字まで）。");

    private void RegisterEventProject() => PromptEventPath("既存ファイルを登録", settings!.Current.ProjectsDirectory + Path.DirectorySeparatorChar,
        path => RefreshEventProjects(EventCatalog.Register(path).Path));

    private void DuplicateEventProject()
    {
        if (SelectedEvent is not { } selected) return;
        OpenUnderlineInput("イベントを複製", selected.Project.DisplayName + " のコピー", name =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            PromptEventPath("複製先", SuggestedEventPath(name), path =>
                RefreshEventProjects(EventCatalog.Duplicate(selected.Project.Path, path, name).Path));
        }, "複製後のイベント名を入力してください（100 文字まで）。");
    }

    private void EditEventProject()
    {
        if (SelectedEvent is not { Exists: true, Error: null } selected) return;
        var project = ProjectFileService.Load(selected.Project.Path);
        OpenUnderlineInput("イベントを編集：会場名", project.Venue.Name, name =>
        {
            EventCatalog.EditVenueName(selected.Project.Path, name);
            eventMetadata.Invalidate(selected.Project.Path);
            RefreshEventProjects(selected.Project.Path);
        }, "会場名を保存します。フレーム配置データの書出しにもこの名前を使います。", 32767);
    }

    private void MoveEvent(int offset)
    {
        if (SelectedEvent is not { } selected) return;
        EventCatalog.Move(selected.Project.Path, offset);
        RefreshEventProjects(selected.Project.Path);
    }

    private void RemoveEvent()
    {
        if (SelectedEvent is not { } selected) return;
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "一覧から除外",
            $"「{selected.Project.DisplayName}」を一覧から除外しますか？\nプロジェクトファイルは削除されません。"), action =>
        {
            if (action == ModalDialogAction.Accept) RunEventAction(() => { EventCatalog.Remove(selected.Project.Path); eventMetadata.Invalidate(selected.Project.Path); RefreshEventProjects(); });
        }, [("除外する", ModalDialogAction.Accept), ("キャンセル", ModalDialogAction.Cancel)]);
    }

    private void MarkEventConfidential()
    {
        if (SelectedEvent is not { } selected) return;
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "マル秘に設定",
            $"「{selected.Project.DisplayName}」をマル秘にしますか？\n設定後、アプリからは解除できません。"), action =>
        {
            if (action == ModalDialogAction.Accept) RunEventAction(() => { EventCatalog.MarkConfidential(selected.Project.Path); eventMetadata.Invalidate(selected.Project.Path); RefreshEventProjects(selected.Project.Path); InspectSelectedEvent(); });
        }, [("マル秘にする", ModalDialogAction.Accept), ("キャンセル", ModalDialogAction.Cancel)]);
    }
}
