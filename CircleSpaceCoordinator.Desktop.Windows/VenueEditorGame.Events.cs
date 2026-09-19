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
    private sealed record EventListEntry(EventProjectReference Project, bool Exists, bool Confidential, string? Error);
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
    private int EventRows => Math.Max(1, (GraphicsDevice.Viewport.Height - WorkerBarHeight - 220) / 64);
    private int EventActionColumns => GraphicsDevice.Viewport.Height < 560 ? 2 : 1;
    private double EventSidebarWidth => EventActionColumns == 2 ? 280 : 188;
    private ScreenRectangle EventRowBounds(int row) => new(24, 108 + WorkerBarHeight + row * 64,
        Math.Max(100, GraphicsDevice.Viewport.Width - EventSidebarWidth - 72), 58);

    private void RefreshEventProjects(string? selectedPath = null)
    {
        lastEventClick = -1;
        eventProjects.Clear();
        foreach (var project in EventCatalog.Projects)
        {
            var exists = File.Exists(project.Path);
            try { eventProjects.Add(new(project, exists, exists && EventCatalog.IsConfidential(project.Path), null)); }
            catch (Exception ex) { eventProjects.Add(new(project, exists, false, ex.Message)); }
        }
        eventSelection = Math.Max(0, eventProjects.FindIndex(item => string.Equals(item.Project.Path, selectedPath, StringComparison.OrdinalIgnoreCase)));
        eventScroll = Math.Clamp(eventSelection, 0, Math.Max(0, eventProjects.Count - EventRows));
        eventWidth = -1;
        pressedEventButton = null;
    }

    private void EnsureEventButtons()
    {
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
            var columns = EventActionColumns;
            var rows = (10 + columns - 1) / columns;
            var step = Math.Clamp((eventHeight - WorkerBarHeight - 164d) / rows, 30, 44);
            var width = (EventSidebarWidth - (columns - 1) * 12) / columns;
            var bounds = new ScreenRectangle(eventWidth - 24 - EventSidebarWidth + (eventButtons.Count % columns) * (width + 12),
                108 + WorkerBarHeight + eventButtons.Count / columns * step, width, step - 6);
            eventButtons.Add((new IconButtonModel(bounds, label) { IsEnabled = enabled }, execute));
        }
        Add("開く", () => { if (SelectedEvent is { } item) OpenEventProject(item.Project.Path); }, usable);
        Add("新規作成", CreateEventProject);
        Add("編集", EditEventProject, usable);
        Add("既存ファイルを登録", RegisterEventProject);
        Add("複製", DuplicateEventProject, usable);
        Add("マル秘に設定", MarkEventConfidential, usable && selected?.Confidential == false);
        Add("上へ", () => MoveEvent(-1), eventSelection > 0);
        Add("下へ", () => MoveEvent(1), eventSelection < eventProjects.Count - 1);
        Add("一覧から除外", RemoveEvent, selected is not null);
        Add("終了", Exit);
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
                eventScroll = Math.Clamp(eventScroll, Math.Max(0, next - EventRows + 1), next);
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
        Text("イベントプロジェクト一覧", new(24, 22 + WorkerBarHeight, GraphicsDevice.Viewport.Width - 48, 42), 28, true);
        Text("この版から編集は自動保存されます。［プロジェクト］の［すぐ保存］／［セーブポイント］も利用できます。", new(24, 68 + WorkerBarHeight, GraphicsDevice.Viewport.Width - 48, 28));
        for (var row = 0; row < EventRows && eventScroll + row < eventProjects.Count; row++)
        {
            var index = eventScroll + row;
            var item = eventProjects[index];
            var bounds = EventRowBounds(row);
            var button = new IconButtonModel(bounds, item.Project.DisplayName) { IsSelected = index == eventSelection };
            button.UpdatePointer(new ScreenPoint(previousMouse.X, previousMouse.Y));
            OperationButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)), (_, _) => { });
            if (eventFocus < 0 && index == eventSelection) DrawOutline(bounds, 2, OperationTargetColor);
            Text((item.Confidential ? "（秘） " : "") + item.Project.DisplayName, new(bounds.X + 10, bounds.Y + 5, bounds.Width - 20, 24), 19, true);
            var detail = !item.Exists ? "ファイルが見つかりません：" + item.Project.Path : item.Error ?? item.Project.Path;
            var maxCharacters = Math.Max(12, (int)((bounds.Width - 20) / 8));
            if (detail.Length > maxCharacters)
            {
                var prefix = maxCharacters / 3;
                detail = detail[..prefix] + "…" + detail[^(maxCharacters - prefix - 1)..];
            }
            Text(detail,
                new(bounds.X + 10, bounds.Y + 31, bounds.Width - 20, 20), 13);
        }
        if (eventProjects.Count == 0) Text("イベントはまだありません。［新規作成］または［既存ファイルを登録］を選んでください。", EventRowBounds(0));
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
            new(24, GraphicsDevice.Viewport.Height - 48, GraphicsDevice.Viewport.Width - 48, 28), 16);
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
            if (action == ModalDialogAction.Accept) RunEventAction(() => { EventCatalog.Remove(selected.Project.Path); RefreshEventProjects(); });
        }, [("除外する", ModalDialogAction.Accept), ("キャンセル", ModalDialogAction.Cancel)]);
    }

    private void MarkEventConfidential()
    {
        if (SelectedEvent is not { } selected) return;
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "マル秘に設定",
            $"「{selected.Project.DisplayName}」をマル秘にしますか？\n設定後、アプリからは解除できません。"), action =>
        {
            if (action == ModalDialogAction.Accept) RunEventAction(() => { EventCatalog.MarkConfidential(selected.Project.Path); RefreshEventProjects(selected.Project.Path); });
        }, [("マル秘にする", ModalDialogAction.Accept), ("キャンセル", ModalDialogAction.Cancel)]);
    }
}
