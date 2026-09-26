namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Application.Queries;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Logging;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.Desktop.Core.Screenshots;
using CircleSpaceCoordinator.Desktop.Windows.Persistence;
using StationeryUI.MonoGame.Audio;
using StationeryUI.MonoGame.Effects;
using CircleSpaceCoordinator.Desktop.Windows.Text;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Tabular;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame : Game
{
    private const int StatusBarHeight = 58;
    private const int ToolbarHeight = 112 + WorkerBarHeight;
    private static readonly Color CanvasGridColor = new(70, 78, 92);
    private static readonly Color GenreDeskWireframeColor = new(126, 150, 164);
    private static readonly Color OperationTargetColor = new(115, 231, 255);
    private static readonly Color SelectionHighlightColor = new(74, 210, 255, 72);
    private static readonly Color IslandConnectionColor = new(73, 220, 205);
    private static readonly Color DisabledIslandConnectionColor = new(104, 108, 112);
    private static readonly Color[] GenrePalette =
    [
        new(210, 72, 65),   // red
        new(229, 194, 55),  // yellow
        new(166, 201, 65),  // yellow green
        new(69, 184, 85),   // green
        new(70, 190, 207),  // cyan
        new(45, 162, 147),  // blue green
        new(64, 132, 207),  // blue
        new(65, 80, 160),   // indigo
        new(116, 82, 190),  // blue violet
        new(181, 71, 151),  // red violet
        new(224, 103, 153), // pink
        new(151, 99, 62),   // brown
    ];
    private static readonly string[] GenreColorIds =
    [
        "red", "yellow", "yellow-green", "green", "cyan", "blue-green",
        "blue", "indigo", "blue-violet", "red-violet", "pink", "brown",
    ];
    private readonly GraphicsDeviceManager graphics;
    private readonly GridViewport viewport = new(32d);
    private RemoteWorkspace? workspace;
    private DeskDragController? dragController;
    private EditorCommandController? commandController;
    private ParticipantPlacementController? participantController;
    private readonly IOperationLogger operationLogger;
    private readonly PerformanceRecorder? performance;
    private StartupTiming? startupTiming;
    private long screenshotRequestedAt;
    private ApplicationSettingsService? settings;
    private string? projectSavePath;
    private readonly List<ToolbarButton> toolbarButtons = [];
    private readonly List<double> toolbarSeparators = [];
    private ToolbarButton? pressedToolbarButton;
    private EditorMode editorMode = EditorMode.DeskPlacement;
    private ToolbarAction activeCanvasTool = ToolbarAction.MoveDesk;
    private SpriteBatch? spriteBatch;
    private Texture2D? pixel;
    private DynamicTextRenderer? textRenderer;
    private MouseState previousMouse;
    private KeyboardState previousKeyboard;
    private const double ScreenshotEffectDurationSeconds = 0.42d;
    private readonly ScreenshotEffect screenshotEffect = new();
    private bool screenshotRequested;
    private double screenshotEffectStartedAt = double.NegativeInfinity;
    private SoundEffect? screenshotShutterSound;
    private SoundEffectInstance? screenshotShutterSoundInstance;
    private string? screenshotStatus;
    private string? hoveredPlanId;
    private bool hoveredPlanCopy;
    private bool hoveredPlanRename;
    private bool hoveredLayoutAdd;
    private bool hoveredLayoutDuplicate;
    private bool hoveredLayoutDelete;
    private bool hoveredLayoutBind;
    private bool hoveredDeskLayoutParent;
    private bool hoveredLayoutRename;
    private string secondaryStatusMessage = "";
    private QuarterTurn nextDeskOrientation = QuarterTurn.North;
    private ScreenPoint? lastDeskGhostPointer;
    private ParticipantToken? draggedParticipantToken;
    private ScreenPoint participantDragStart;
    private ScreenPoint participantDragPointer;
    private GridPosition? rangeSelectionAnchor;
    private GridPosition? rangeSelectionCurrent;
    private CellRange? selectedCellRange;
    private CellRange? draggedCellRange;
    private GridPosition rangeDragGrabOffset;
    private string? rangeSwapStatus;
    private bool leftPanActive;
    private string? topologyFirstDeskId;
    private GridPosition? topologyFirstCell;
    private GridPosition? topologyFirstCorner;
    private bool showEvaluationAnalysis;
    private readonly bool startEngines;
    private EngineRuntime? ownedEngineRuntime;

    public VenueEditorGame(
        RemoteWorkspace? workspace = null,
        IOperationLogger? operationLogger = null,
        string? projectSavePath = null,
        ApplicationSettingsService? settings = null,
        bool startEngines = false,
        PerformanceRecorder? performance = null)
    {
        this.workspace = workspace;
        if (workspace is not null) workspace.HandleProvider = () => Handle;
        if (workspace is not null) workspace.WorkDateProvider = () => WorkDate;
        this.startEngines = startEngines;
        this.operationLogger = operationLogger ?? NullOperationLogger.Instance;
        this.performance = performance;
        this.projectSavePath = projectSavePath;
        // Names and paths are available before the engines; decode projects only on selection.
        this.settings = settings ?? new ApplicationSettingsService(UserSettingsPaths.PrepareFile("application-settings.json"), deferProjectMetadata: true);
        userProfilePromptPending = string.IsNullOrWhiteSpace(this.settings.Current.Handle);
        eventStyle = EventListStyle.CreateForApplication();
        dragController = workspace is null ? null : new DeskDragController(workspace, viewport);
        commandController = workspace is null ? null : new EditorCommandController(workspace);
        participantController = workspace is null ? null : new ParticipantPlacementController(workspace);
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
        };
        // SDL creates the native window after this constructor. Keep the initial title ASCII-only;
        // UpdateWindowPresentation sets the Unicode event name through SDL's UTF-8 update path.
        Window.Title = ApplicationIdentity.Title;
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        Exiting += (_, args) => { if (!FinishWorkerEdit(true) || !TryExitStyleMapping() || !FlushAutoSave()) { args.Cancel = true; return; } PersistWorkingState(); };
        CreateToolbar();
        Log("application_start", success: true, detail: workspace is null ? "empty_grid" : "project_loaded");
    }

    protected override void Initialize()
    {
        SdlWindowIcon.TrySet(Window, Path.Combine(AppContext.BaseDirectory, "Assets", "long-table-app-icon.png"));
        RestoreWorkingState();
        try { _ = SpaceDefinitions; }
        catch (Exception ex) { ShowInAppMessage("フレーム定義", ex.Message); }
        CreateToolbar();
        previousMouse = Mouse.GetState();
        previousKeyboard = Keyboard.GetState();
        RefreshEventProjects(this.settings?.Current.LastProjectPath);
        if (!startEngines && workspace is null && projectSavePath is { } initialPath)
        {
            projectSavePath = null;
            OpenEventProject(initialPath);
        }
        base.Initialize();
        if (startEngines)
        {
            if (performance is not null) startupTiming = new StartupTiming(performance);
            RunBackground("エンジンの起動", () => ownedEngineRuntime = EngineRuntime.StartAsync(AppContext.BaseDirectory,
                startupTiming: (name, milliseconds) => startupTiming?.Record(name, milliseconds)).GetAwaiter().GetResult(), runtime =>
            {
                var readyStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                EditorConnection.Current = runtime.Connection;
                RefreshEventProjects(this.settings?.Current.LastProjectPath);
                modalDialog = null;
                modalButtons.Clear();
                modalInputDrain = true;
                if (projectSavePath is { } path) { projectSavePath = null; OpenEventProject(path); }
                startupTiming?.Record("ui_ready", System.Diagnostics.Stopwatch.GetElapsedTime(readyStarted).TotalMilliseconds);
            }, exception =>
            {
                startupTiming?.Complete(false);
                startupTiming = null;
                OpenModal(new ModalDialogModel(ModalDialogKind.Message, "エンジンの起動に失敗しました", exception.Message), _ => Exit());
            }, eventStartupOverlay: true);
        }
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        textRenderer = new DynamicTextRenderer(GraphicsDevice, spriteBatch, performance);
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData([Color.White]);
        screenshotShutterSound = ScreenshotShutterSound.Create();
        screenshotShutterSoundInstance = screenshotShutterSound.CreateInstance();
    }

    protected override void Update(GameTime gameTime)
    {
        performance?.Heartbeat(mappingDraft is not null ? modalDialog?.Kind == ModalDialogKind.Text ? "genre_text" : "genre"
            : modalDialog?.Kind == ModalDialogKind.Text ? "text" : workspace is null ? "events" : "editor");
        using var updateTiming = performance?.Measure("update");
        statusHintTime = gameTime.TotalGameTime.TotalSeconds;
        eventStyle.Update(gameTime.ElapsedGameTime, settings!.Current.StyleAutoReload);
        UpdateDeveloperWindow(gameTime, Keyboard.GetState(), IsActive);
        pollBackgroundOperation?.Invoke();
        if (!ShowsStatusHintTimer) statusHintContext = null;
        try
        {
            using (performance?.Measure("autosave_check")) UpdateAutoSave();
            using (performance?.Measure("input_update")) UpdateEditor(gameTime);
        }
        catch (InvalidOperationException exception) when (exception.InnerException is Grpc.Core.RpcException)
        {
            CancelInProgressPointerInteraction();
            try { workspace?.Refresh(); } catch (InvalidOperationException) { }
            ShowInAppMessage("エディターエンジンとの通信", exception.Message);
        }
    }

    private void UpdateEditor(GameTime gameTime)
    {
        if (Window.ClientBounds.Width > 0 && Window.ClientBounds.Width != projectToolbarWidth) CreateToolbar();
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        if (!IsActive)
        {
            ResetUnderlineKeyRepeat();
            CancelInProgressPointerInteraction();
            // Keep the current physical input state.  Otherwise a button held while
            // this window was inactive would look like a fresh click on activation.
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }

        if (developerWindow.CaptureEnabled)
        {
            CancelInProgressPointerInteraction();
            ResetUnderlineKeyRepeat();
            if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released &&
                modalDialog is null && !projectMenuOpen)
            {
                var hit = StationeryUI.Inspection.DeveloperCapture.HitTest(InspectStationery(), mouse.X, mouse.Y);
                if (hit is not null) developerWindow.SelectCaptured(hit.Path);
            }
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }

        if (userProfilePromptPending && backgroundOperation is null && !eventStartupLoading && modalDialog is null && !projectMenuOpen)
        {
            userProfilePromptPending = false;
            userProfileOpenedForStartup = true;
            OpenUserProfile();
        }

        if (UpdateWorkerBar(keyboard, mouse)) { previousKeyboard = keyboard; base.Update(gameTime); return; }

        // Screen capture remains available while either overlay owns input.
        if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.P))
            RequestScreenshot();
        if (userProfileOpen)
        {
            if (!UpdateModalDialog(keyboard, mouse)) UpdateUserProfile(keyboard, mouse);
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }
        if (projectMenuOpen || projectMenuDrain)
        {
            PollOptimization();
            if (!UpdateModalDialog(keyboard, mouse)) UpdateProjectMenu(keyboard, mouse);
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }
        if (mappingDraft is not null)
        {
            if (!UpdateModalDialog(keyboard, mouse)) UpdateStyleMappingEditor(keyboard, mouse);
            previousMouse = mouse;
            previousKeyboard = keyboard;
            base.Update(gameTime);
            return;
        }
        if (frameDraft is not null)
        {
            if (!UpdateModalDialog(keyboard, mouse)) UpdateFrameDefinitionEditor(keyboard, mouse);
            previousMouse = mouse;
            previousKeyboard = keyboard;
            base.Update(gameTime);
            return;
        }
        if (workspace is null)
        {
            if (!UpdateModalDialog(keyboard, mouse)) UpdateEventProjects(keyboard, mouse);
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }
        if (UpdateSpaceCatalog(keyboard, mouse) || UpdateToolRing(keyboard, mouse))
        {
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }

        PollOptimization();
        if (UpdateModalDialog(keyboard, mouse))
        {
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }

        if (IsPressed(keyboard, Keys.Escape))
        {
            if (IsAddressSwapMode || addressSwapChannel >= 0)
            {
                var wasDragging = addressSwapChannel >= 0;
                CancelInProgressPointerInteraction();
                if (!wasDragging) addressSwapEnabled = false;
                rangeSwapStatus = wasDragging ? "番地のスワップを取り消しました" : "番地入力モード";
                previousMouse = mouse;
                previousKeyboard = keyboard;
                return;
            }
            RequestReturnToEvents();
            previousMouse = mouse;
            previousKeyboard = keyboard;
            return;
        }
        if (editorMode != EditorMode.SpaceDefinitions && IsControlDown(keyboard) && IsPressed(keyboard, Keys.S))
            SaveProject();
        if (editorMode != EditorMode.SpaceDefinitions && IsControlDown(keyboard) && IsPressed(keyboard, Keys.O))
        {
            ReturnToEventList();
            previousMouse = mouse;
            previousKeyboard = keyboard;
            return;
        }

        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        UpdateToolbar(pointer);
        if (editorMode == EditorMode.DeskPlacement && (Contains(NextSpaceBounds, pointer) || Contains(SpaceCapacityBounds, pointer) || pressedNextSpace))
        {
            if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            {
                CancelInProgressPointerInteraction();
                pressedNextSpace = Contains(NextSpaceBounds, pointer);
                NextSpaceSelectionButton.Press(pointer);
                pressedNextDirection = NextSpace is not null && NextDirectionButton.Press(pointer);
            }
            if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
            {
                var open = NextSpaceSelectionButton.Release(pointer);
                var direction = pressedNextDirection;
                var openDirection = NextDirectionButton.Release(pointer);
                pressedNextSpace = false;
                pressedNextDirection = false;
                if (direction && openDirection) OpenToolRing(ToolbarAction.NextDirectionMenu);
                else if (open) OpenSpaceCatalog();
                else CancelInProgressPointerInteraction();
            }
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }
        if (editorMode == EditorMode.SpaceDefinitions)
        {
            UpdateSpaceDefinitions(mouse, pointer);
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }
        if (editorMode is EditorMode.ParticipantData or EditorMode.CirclePlacementDecision)
        {
            UpdateParticipantDataInput(keyboard, mouse, pointer);
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }
        if (HandleChannelScrollbarInput(mouse, pointer) || HandlePlanScrollbarInput(mouse, pointer))
        {
            hoveredPlanId = null;
            previousMouse = mouse;
            previousKeyboard = keyboard;
            UpdateWindowPresentation();
            base.Update(gameTime);
            return;
        }
        hoveredPlanId = editorMode == EditorMode.GenreData ? null : HitTestPlanList(pointer);
        hoveredPlanCopy = !UsesSeparatedLayouts && editorMode != EditorMode.GenreData && workspace is not null && Contains(GetPlanCopyBounds(), pointer);
        hoveredPlanRename = !UsesSeparatedLayouts && editorMode != EditorMode.GenreData && workspace is not null && Contains(GetPlanRenameBounds(), pointer);
        hoveredLayoutAdd = UsesSeparatedLayouts && Contains(GetLayoutAddBounds(), pointer);
        hoveredLayoutDuplicate = UsesSeparatedLayouts && (ShowsDeskLayouts || workspace!.HasSelectedCircleLayout) && Contains(GetLayoutDuplicateBounds(), pointer);
        hoveredLayoutDelete = UsesSeparatedLayouts && Contains(GetLayoutDeleteBounds(), pointer);
        hoveredLayoutBind = UsesSeparatedLayouts && workspace!.HasSelectedCircleLayout && (editorMode is EditorMode.GenrePlacement or EditorMode.CirclePlacement) && Contains(GetLayoutBindBounds(), pointer);
        hoveredDeskLayoutParent = UsesSeparatedLayouts && (editorMode is EditorMode.GenrePlacement or EditorMode.CirclePlacement) && Contains(GetDeskLayoutParentBounds(), pointer);
        hoveredLayoutRename = UsesSeparatedLayouts && (ShowsDeskLayouts || workspace!.HasSelectedCircleLayout) && Contains(GetLayoutRenameBounds(), pointer);
        if (activeCanvasTool == ToolbarAction.AddDesk &&
            IsPointerInEditorCanvas(pointer) &&
            IsDeskGhostClearOfToolbar(pointer))
            lastDeskGhostPointer = pointer;
        else if (activeCanvasTool != ToolbarAction.AddDesk)
            lastDeskGhostPointer = null;
        if (commandController is not null && editorMode != EditorMode.GenreData)
        {
            if (IsPressed(keyboard, Keys.Tab))
            {
                var direction = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? -1 : 1;
                CycleDisplayedPlan(direction);
                Log("plan_cycle", success: true, detail: $"direction={direction}");
            }
            if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.Z))
                Log("undo", success: commandController.Undo());
            if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.Y))
                Log("redo", success: commandController.Redo());
            if (IsPressed(keyboard, Keys.E) || IsPressed(keyboard, Keys.Q))
            {
                var clockwise = keyboard.IsKeyDown(Keys.E);
                if (activeCanvasTool == ToolbarAction.AddDesk)
                {
                    RotateNextDesk(clockwise);
                    LogPointer("desk_ghost_rotate", pointer, true,
                        $"direction={(clockwise ? "clockwise" : "counterclockwise")};orientation={nextDeskOrientation}");
                }
                else
                {
                    var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
                var result = commandController.RotateDeskAt(cell, clockwise);
                RememberAffectedOrientation(result);
                LogPointer("desk_rotate", pointer, result.Applied,
                    $"direction={(clockwise ? "clockwise" : "counterclockwise")};issues={FormatIssues(result.Issues)}");
                }
            }
            if (editorMode == EditorMode.CirclePlacement && participantController is not null &&
                (IsPressed(keyboard, Keys.Up) || IsPressed(keyboard, Keys.Down)))
            {
                var direction = keyboard.IsKeyDown(Keys.Down) ? 1 : -1;
                participantController.CycleUnassigned(direction);
                Log("participant_cycle", success: participantController.SelectedParticipantId is not null, detail: $"direction={direction}");
            }
            if (editorMode == EditorMode.CirclePlacement && participantController is not null && IsPressed(keyboard, Keys.A))
            {
                var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
                var result = participantController.AssignSelectedAt(cell);
                LogPointer("participant_assign", pointer, result.Applied, $"issues={FormatIssues(result.Issues)}");
            }
            if (editorMode == EditorMode.CirclePlacement && participantController is not null && IsPressed(keyboard, Keys.Delete))
            {
                var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
                var result = participantController.UnassignAt(cell);
                LogPointer("participant_unassign", pointer, result.Applied, $"issues={FormatIssues(result.Issues)}");
            }
        }
        if (mouse.MiddleButton == ButtonState.Pressed && previousMouse.MiddleButton == ButtonState.Pressed)
        {
            viewport.PanBy(mouse.X - previousMouse.X, mouse.Y - previousMouse.Y);
            LogPointer("viewport_pan", pointer, true,
                $"deltaX={mouse.X - previousMouse.X};deltaY={mouse.Y - previousMouse.Y}");
        }

        var wheelDelta = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        if (wheelDelta != 0 && !ScrollChannels(pointer, wheelDelta) && !ScrollPlanList(pointer, wheelDelta))
        {
            var zoom = Math.Clamp(
                viewport.Zoom * (wheelDelta > 0 ? 1.1d : 1d / 1.1d),
                GridViewport.MinimumZoom,
                GridViewport.MaximumZoom);
            viewport.ZoomAt(pointer, zoom);
            LogPointer("viewport_zoom", pointer, true, $"zoom={zoom:0.###}");
        }

        if (mouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Released)
            LogPointer("pointer_right_down", pointer, true);

        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedToolbarButton = toolbarButtons.LastOrDefault(button => button.Model.Contains(pointer));
            if (pressedToolbarButton is not null)
            {
                var accepted = pressedToolbarButton.Model.Press(pointer);
                LogPointer("toolbar_press", pointer, accepted,
                    $"action={pressedToolbarButton.Action};state={(accepted ? "enabled" : "disabled")}");
            }
            else if (HandleLayoutOrderClick(pointer))
            {
            }
            else if (HandleChannelPanelClick(pointer))
            {
            }
            else if (hoveredLayoutAdd)
            {
                var outcome = PromptCreateLayout();
                LogPointer("layout_create", pointer, outcome.Success, outcome.Detail);
            }
            else if (hoveredLayoutDuplicate)
            {
                var outcome = PromptDuplicateLayout();
                LogPointer("layout_duplicate", pointer, outcome.Success, outcome.Detail);
            }
            else if (hoveredLayoutDelete)
            {
                var outcome = CanRemoveLayout ? PromptDeleteLayout() : (Success: false, Detail: "layout_remove_disabled");
                LogPointer("layout_delete", pointer, outcome.Success, outcome.Detail);
            }
            else if (hoveredDeskLayoutParent)
            {
                OpenLayoutSelection("表示するフレーム配置", workspace!.SelectedDeskLayoutId, target =>
                {
                    if (target == workspace.SelectedDeskLayoutId) return;
                    dragController?.Cancel();
                    workspace.SelectDeskLayout(target);
                    planScroll = 0;
                    hoveredPlanId = null;
                    LogPointer("desk_layout_select", pointer, true, $"deskLayoutId={target}");
                });
            }
            else if (hoveredLayoutBind)
            {
                var outcome = PromptRebindCircleLayout();
                LogPointer("circle_layout_rebind", pointer, outcome.Success, outcome.Detail);
            }
            else if (hoveredLayoutRename)
            {
                var outcome = PromptRenameLayout();
                LogPointer("layout_rename", pointer, outcome.Success, outcome.Detail);
            }
            else if (hoveredPlanCopy && commandController is not null)
            {
                var outcome = PromptCopyDeskLayout();
                LogPointer("plan_copy", pointer, outcome.Success, outcome.Detail);
            }
            else if (hoveredPlanRename && commandController is not null)
            {
                var outcome = PromptRenameSelectedPlan();
                LogPointer("plan_rename", pointer, outcome.Success, outcome.Detail);
            }
            else if (hoveredPlanId is not null && workspace is not null)
            {
                SelectDisplayedLayout(hoveredPlanId);
                LogPointer("plan_select", pointer, true, $"displayedIndex={GetDisplayedPlans().ToList().FindIndex(plan => plan.PlanId == hoveredPlanId)}");
            }
            else if (IsControlDown(keyboard) && !keyboard.IsKeyDown(Keys.LeftShift) && !keyboard.IsKeyDown(Keys.RightShift) &&
                     IsPointerInEditorCanvas(pointer) && CanSwapSelectedAddress(pointer))
            {
                BeginAddressSwap(pointer);
            }
            else if (CanSelectCellRange && (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)) && IsPointerInEditorCanvas(pointer))
            {
                // Shift reserves the gesture for selection, including over an existing range.
                rangeSelectionAnchor = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
                rangeSelectionCurrent = rangeSelectionAnchor;
                frameSelectionStart = IsFrameNumberChannelSelected ? pointer : null;
                frameSelectionPointer = pointer;
                selectedFrameIds.Clear();
                rangeSwapStatus = null;
                LogPointer("range_select_start", pointer, true);
            }
            else if (keyboard.IsKeyDown(Keys.Space) && IsPointerInEditorCanvas(pointer))
            {
                // The space bar temporarily turns the pointer into the hand tool.
                // Do not change activeCanvasTool here: releasing Space restores the
                // selected tool automatically, even when the mouse drag is still in progress.
                leftPanActive = true;
                LogPointer("viewport_pan_start", pointer, true, "button=left;temporary=space");
            }
            else if (IsAddressSwapMode && IsPointerInEditorCanvas(pointer))
            {
                BeginAddressSwap(pointer);
            }
            else if (editorMode == EditorMode.DeskPlacement && activeCanvasTool == ToolbarAction.EditSeatName &&
                     IsControlDown(keyboard) && IsPointerInEditorCanvas(pointer))
            {
                // Ctrl only swaps an existing address selection; it must not open input elsewhere.
                rangeSwapStatus = "Shift＋ドラッグで範囲を選択してから、選択範囲をCtrl＋ドラッグしてください";
            }
            else if (IsWeightChannelSelected && activeCanvasTool == ToolbarAction.EditSeatName && IsPointerInEditorCanvas(pointer))
            {
                EditChannelWeight(VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer)));
            }
            else if (IsSeatNameRangeEditing &&
                     selectedCellRange is { } seatRange &&
                     seatRange.Contains(VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer))) &&
                     IsPointerInEditorCanvas(pointer))
            {
                var result = EditNumberCells(seatRange.Contains);
                rangeSwapStatus = result.Applied
                    ? "選択チャンネルの番号をまとめて変更しました"
                    : result.Issues.Any(issue => issue.Code == "seatLabel.pair.duplicate")
                        ? "同じブロック名とセル番の組が重複するため、変更をキャンセルしました"
                        : result.Issues.Count > 0
                            ? "選択チャンネルの番号をまとめて変更できませんでした"
                            : null;
                LogPointer("seat_label_bulk_edit", pointer, result.Applied, $"issues={FormatIssues(result.Issues)}");
            }
            else if ((editorMode is EditorMode.GenrePlacement or EditorMode.CirclePlacement) &&
                     selectedCellRange is { } range &&
                     range.Contains(VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer))) &&
                     IsPointerInEditorCanvas(pointer))
            {
                var grabbedCell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
                draggedCellRange = range;
                rangeDragGrabOffset = new GridPosition(grabbedCell.X - range.Left, grabbedCell.Y - range.Top);
                participantDragPointer = pointer;
                rangeSwapStatus = null;
                LogPointer("range_swap_start", pointer, true, $"width={range.Width};height={range.Height}");
            }
            else if ((editorMode is EditorMode.GenrePlacement or EditorMode.CirclePlacement) &&
                     HitTestParticipant(pointer) is { } participantToken)
            {
                draggedParticipantToken = participantToken;
                participantDragStart = pointer;
                participantDragPointer = pointer;
                LogPointer("participant_drag_start", pointer, true,
                    FormatParticipantLogDetail(participantToken.ParticipantId));
            }
            else if (editorMode == EditorMode.IslandDefinition && activeCanvasTool == ToolbarAction.ToggleAutomaticIslandConnection &&
                     TryToggleAutomaticIslandConnection(pointer, out var connection))
            {
                LogPointer("island_connection_toggle", pointer, true,
                    $"first={connection.FirstCell};second={connection.SecondCell}");
            }
            else if (activeCanvasTool == ToolbarAction.PanViewport)
            {
                leftPanActive = true;
                LogPointer("viewport_pan_start", pointer, true);
            }
            else if (activeCanvasTool == ToolbarAction.MoveDesk && dragController is not null)
                LogPointer("pointer_left_down", pointer, dragController.BeginDrag(pointer));
            else
            {
                var outcome = ExecuteCanvasTool(pointer);
                LogPointer("canvas_tool", pointer, outcome.Success, $"tool={activeCanvasTool};{outcome.Detail}");
            }
        }
        else if (mouse.LeftButton == ButtonState.Pressed)
        {
            if (pressedToolbarButton is not null)
                pressedToolbarButton.Model.UpdatePointer(pointer);
            else if (leftPanActive)
            {
                viewport.PanBy(mouse.X - previousMouse.X, mouse.Y - previousMouse.Y);
                LogPointer("viewport_pan", pointer, true,
                    $"button=left;deltaX={mouse.X - previousMouse.X};deltaY={mouse.Y - previousMouse.Y}");
            }
            else if (rangeSelectionAnchor is not null)
            {
                rangeSelectionCurrent = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
                frameSelectionPointer = pointer;
            }
            else if (draggedCellRange is not null)
                participantDragPointer = pointer;
            else if (draggedParticipantToken is not null)
                participantDragPointer = pointer;
            else if (activeCanvasTool == ToolbarAction.MoveDesk && dragController is not null)
                dragController.UpdateDrag(pointer);
        }
        else if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            if (pressedToolbarButton is not null)
            {
                var button = pressedToolbarButton;
                pressedToolbarButton = null;
                var clicked = button.Model.Release(pointer);
                var outcome = clicked
                    ? ExecuteToolbarAction(button.Action, pointer)
                    : (false, button.Model.IsEnabled ? "cancelled" : "disabled");
                LogPointer("toolbar_click", pointer, outcome.Item1, $"action={button.Action};{outcome.Item2}");
            }
            else if (leftPanActive)
            {
                leftPanActive = false;
                LogPointer("viewport_pan_end", pointer, true);
            }
            else if (rangeSelectionAnchor is { } anchor)
            {
                var current = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
                selectedCellRange = CellRange.From(anchor, current);
                FinishFrameSelection(pointer);
                rangeSelectionAnchor = null;
                rangeSelectionCurrent = null;
                LogPointer("range_select_end", pointer, true,
                    $"left={selectedCellRange.Value.Left};top={selectedCellRange.Value.Top};width={selectedCellRange.Value.Width};height={selectedCellRange.Value.Height}");
            }
            else if (addressSwapChannel >= 0 && draggedCellRange is not null)
            {
                FinishAddressSwap(pointer);
            }
            else if (draggedCellRange is { } range && participantController is not null)
            {
                var dropCell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
                var target = new GridPosition(dropCell.X - rangeDragGrabOffset.X, dropCell.Y - rangeDragGrabOffset.Y);
                var result = participantController.SwapCellRegions(new GridPosition(range.Left, range.Top), target, range.Width, range.Height);
                rangeSwapStatus = result.Applied ? "範囲をスワップしました" : FormatRangeSwapIssues(result.Issues);
                LogPointer("range_swap", pointer, result.Applied,
                    $"width={range.Width};height={range.Height};issues={FormatIssues(result.Issues)}");
                if (result.Applied)
                    selectedCellRange = null;
                draggedCellRange = null;
            }
            else if (draggedParticipantToken is { } participantToken && participantController is not null)
            {
                var moved = DistanceSquared(participantDragStart, pointer) >= 16d;
                if (moved)
                {
                    var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
                    var swapTarget = HitTestParticipant(pointer);
                    var deskExists = workspace!.GetSelectedPlanSnapshot().Desks
                        .Any(desk => desk.OccupiedCells.Contains(cell));
                    var temporaryIds = workspace.SelectedPlan.TemporaryPlacements.Select(item => item.ParticipantId).ToHashSet();
                    var swapping = (participantToken.Assigned || temporaryIds.Contains(participantToken.ParticipantId)) &&
                        swapTarget is not null && (swapTarget.Assigned || temporaryIds.Contains(swapTarget.ParticipantId)) &&
                        swapTarget.ParticipantId != participantToken.ParticipantId;
                    var grabbed = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(participantDragStart));
                    var parkedPosition = participantToken.Position + new GridPosition(cell.X - grabbed.X, cell.Y - grabbed.Y);
                    var result = !IsPointerInEditorCanvas(pointer) ? EditorCommandResult.NoTarget : swapping
                        ? participantController.SwapParticipants(participantToken.ParticipantId, swapTarget!.ParticipantId)
                        : !deskExists
                            ? participantController.ParkParticipantAt(participantToken.ParticipantId, participantToken.DisplayCells, participantToken.Position, parkedPosition)
                            : participantController.PlaceParticipantAt(participantToken.ParticipantId, cell);
                    rangeSwapStatus = result.Applied
                        ? !deskExists ? "通路に仮置きしました" : null
                        : result.Issues.FirstOrDefault()?.Message;
                    LogPointer("participant_drop", pointer, result.Applied,
                        $"{FormatParticipantLogDetail(participantToken.ParticipantId)};action={(swapping ? "swap" : deskExists ? "place" : "park")};issues={FormatIssues(result.Issues)}",
                        result.Applied ? null : result.Issues.Any(issue => issue.Code == "assignment.circleLayout.required")
                            ? UiOperationFailure.CircleLayoutRequired : result.Issues.Count == 0 ? UiOperationFailure.NoTarget : UiOperationFailure.ValidationRejected);
                    if (result.Issues.Any(issue => issue.Code == "assignment.circleLayout.required"))
                        ShowMissingCircleLayout();
                }
                else if (!participantToken.Assigned)
                {
                    var selected = participantController.SelectParticipant(participantToken.ParticipantId);
                    if (selected)
                        activeCanvasTool = ToolbarAction.AssignParticipant;
                    LogPointer("participant_select", pointer, selected,
                        FormatParticipantLogDetail(participantToken.ParticipantId));
                }
                else if (activeCanvasTool == ToolbarAction.UnassignParticipant)
                {
                    var result = participantController.UnassignAt(participantToken.Position);
                    LogPointer("participant_unassign", pointer, result.Applied,
                        $"{FormatParticipantLogDetail(participantToken.ParticipantId)};issues={FormatIssues(result.Issues)}");
                }
                draggedParticipantToken = null;
            }
            else if (dragController?.IsDragging == true)
            {
                var result = dragController.Drop();
                if (result.Applied && result.AffectedOrientation is { } orientation)
                    nextDeskOrientation = orientation;
                LogPointer("desk_drop", pointer, result.Applied, $"issues={FormatIssues(result.Issues)}");
            }
        }

        previousMouse = mouse;
        previousKeyboard = keyboard;
        UpdateWindowPresentation();
        base.Update(gameTime);
    }

    private void CancelInProgressPointerInteraction()
    {
        genrePieRestoreButton?.CancelPress();
        pressedProjectMenuButton?.CancelPress();
        pressedProjectMenuButton = null;
        viewerDragging = false;
        selectionPressed = -1;
        pressedMappingButton?.CancelPress();
        pressedMappingButton = null;
        pressedFrameButton?.CancelPress();
        pressedFrameButton = null;
        pressedEventButton?.CancelPress();
        pressedEventButton = null;
        foreach (var (button, _) in eventButtons) button.ClearPointerState();
        pressedNextSpace = false;
        pressedNextDirection = false;
        nextSpaceSelectionButton?.CancelPress();
        nextDirectionButton?.CancelPress();
        pressedCatalogButton?.CancelPress();
        pressedCatalogButton = null;
        foreach (var item in catalogButtons) item.Model.ClearPointerState();
        pressedSpaceButton?.CancelPress();
        pressedSpaceButton = null;
        foreach (var item in spaceButtons) item.Button.ClearPointerState();
        pressedToolRingButton?.CancelPress();
        pressedToolRingButton = null;
        foreach (var button in toolRingButtons) button.ClearPointerState();
        selectingUnderlineText = false;
        tableScrollDragVertical = null;
        draggingPlanScrollbar = false;
        capturingChannelScrollbar = draggingChannelScrollbar = false;
        foreach (var button in toolbarButtons) button.Model.ClearPointerState();
        hoveredPlanId = null;
        hoveredPlanCopy = hoveredPlanRename = false;
        hoveredLayoutAdd = hoveredLayoutDelete = hoveredLayoutBind = hoveredLayoutRename = false;
        hoveredLayoutDuplicate = false;
        hoveredDeskLayoutParent = false;
        lastDeskGhostPointer = null;
        pressedModalButton?.CancelPress();
        pressedModalButton = null;
        pressedToolbarButton?.Model.CancelPress();
        pressedToolbarButton = null;
        leftPanActive = false;
        dragController?.Cancel();
        draggedParticipantToken = null;
        rangeSelectionAnchor = null;
        rangeSelectionCurrent = null;
        frameSelectionStart = null;
        draggedCellRange = null;
        addressSwapChannel = -1;
        addressSwapProject = null;
    }

    protected override void Draw(GameTime gameTime)
    {
        using var drawTiming = performance?.Measure("draw");
        GraphicsDevice.Clear(new Color(24, 28, 36));
        if (spriteBatch is null || pixel is null)
            return;

        // Preserve antialiased text strokes when labels are scaled to fit their bounds.
        spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        if (userProfileOpen)
        {
            DrawUserProfile();
            DrawModalDialog();
        }
        else if (mappingDraft is not null)
        {
            DrawStyleMappingEditor();
            DrawModalDialog();
        }
        else if (frameDraft is not null)
        {
            DrawFrameDefinitionEditor();
            DrawModalDialog();
        }
        else if (workspace is null)
        {
            DrawEventProjects();
            DrawModalDialog();
        }
        else
        {
            if (ShowVenueAboveRingCover)
                DrawRectangle(new ScreenRectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 170));
            if (editorMode == EditorMode.SpaceDefinitions)
                DrawSpaceDefinitions();
            else if (editorMode is EditorMode.ParticipantData or EditorMode.CirclePlacementDecision)
                DrawParticipantData();
            else if (editorMode == EditorMode.GenreData)
                DrawGenreDataDashboard();
            else
            {
                DrawGrid();
                DrawBlockedCells();
                DrawBlockBackgrounds();
                DrawDesks();
                if (!IsWeightChannelSelected && !ShowCircleHeatmap && selectedNumberChannel == 1) DrawMissingDeskNumbers();
                if (IsWeightChannelSelected) DrawChannelWeights();
                else if (ShowCircleHeatmap) DrawChannelWeights(CircleHeatmapChannelId, underStones: true);
                else DrawNumberLabels();
                DrawDeskPlacementGhost();
                DrawPillarGhost();
                if (editorMode == EditorMode.IslandDefinition)
                    DrawVenueTopology();
                // Genre tiles cover desk orientation markers where they overlap.
                if (editorMode == EditorMode.GenrePlacement)
                    DrawGenreDeskWireframes();
                if ((editorMode is EditorMode.GenrePlacement or EditorMode.CirclePlacement) && !ShowCircleHeatmap)
                    DrawGenreAssignments();
                if (editorMode == EditorMode.GenrePlacement)
                {
                    DrawGenreTokensAndConnectors();
                    if (showEvaluationAnalysis)
                        DrawEvaluationAnalysis();
                    DrawVacantSeats();
                    DrawParticipantDragGhost();
                }
                if (editorMode == EditorMode.CirclePlacement)
                {
                    if (showEvaluationAnalysis)
                        DrawEvaluationAnalysis();
                    DrawVacantSeats();
                    DrawAssignments();
                    DrawParticipantDragGhost();
                    if (ShowCircleHeatmap) DrawCircleWeightLabels();
                }
                DrawRangeSelection();
                DrawAddressSwapFramePreview();
                DrawDeskEditTarget();
                DrawPlanList();
                DrawChannels();
                DrawGenreSummary();
                DrawOffscreenParticipants();
            }
            DrawToolbar();
            DrawNextSpace();
            DrawConfidentialBadge();
            if (ShowVenueAboveRingCover) DrawVenueRingPanelCover();
            if (!toolRingOpen) DrawStatusBar();
            DrawToolRing();
            DrawSpaceCatalog();
            DrawProjectMenu();
            DrawModalDialog();
        }
        DrawWorkerBar();
        using (performance?.Measure("draw_submit")) spriteBatch.End();
        RecordTextFrameSubmitted();
        if (startupTiming is { } startup)
        {
            if (eventStartupLoading) startup.SpinnerFrameSubmitted();
            else { startup.Complete(true); startupTiming = null; }
        }

        if (screenshotRequested)
        {
            screenshotRequested = false;
            CaptureScreenshot(gameTime.TotalGameTime.TotalSeconds);
        }

        var effectAge = gameTime.TotalGameTime.TotalSeconds - screenshotEffectStartedAt;
        if (effectAge >= 0d && effectAge < ScreenshotEffectDurationSeconds)
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            screenshotEffect.Draw(
                (float)(effectAge / ScreenshotEffectDurationSeconds),
                new ScreenshotEffectDrawingCallbacks(
                    GraphicsDevice.PresentationParameters.BackBufferWidth,
                    GraphicsDevice.PresentationParameters.BackBufferHeight,
                    (rectangle, color) => spriteBatch.Draw(pixel, rectangle, color)));
            spriteBatch.End();
        }
        textRenderer?.EndFrame();
        base.Draw(gameTime);
    }

    private void CaptureScreenshot(double now)
    {
        using var captureTiming = performance?.Measure("screenshot_total");
        if (screenshotRequestedAt != 0)
            performance?.Record("screenshot_request_to_capture", System.Diagnostics.Stopwatch.GetElapsedTime(screenshotRequestedAt).TotalMilliseconds);
        screenshotRequestedAt = 0;
        try
        {
            var directory = ScreenshotPath.DefaultDirectory;
            Directory.CreateDirectory(directory);
            var path = ScreenshotPath.Create(directory, DateTime.Now);
            var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var pixels = new Color[width * height];
            using (performance?.Measure("screenshot_gpu_readback")) GraphicsDevice.GetBackBufferData(pixels);
            using var texture = new Texture2D(GraphicsDevice, width, height);
            using (performance?.Measure("screenshot_texture_upload")) texture.SetData(pixels);
            using var stream = File.Create(path);
            using (performance?.Measure("screenshot_png_write")) texture.SaveAsPng(stream, width, height);

            screenshotEffectStartedAt = now;
            if (screenshotShutterSoundInstance is not null)
            {
                if (screenshotShutterSoundInstance.State == SoundState.Playing)
                    screenshotShutterSoundInstance.Stop();
                screenshotShutterSoundInstance.Volume = 0.72f;
                screenshotShutterSoundInstance.Play();
            }
            screenshotStatus = $"SCREENSHOT SAVED: {Path.GetFileName(path)}";
            performance?.Record("screenshot_completed", 0);
            Log("screenshot_saved", success: true, detail: $"path={path};size={width}x{height}");
        }
        catch (Exception exception)
        {
            screenshotStatus = $"SCREENSHOT FAILED: {exception.Message}";
            performance?.Record("screenshot_failed", 0);
            Log("screenshot_saved", success: false, detail: $"error={exception.GetType().Name};message={exception.Message}");
        }
    }

    private void RequestScreenshot()
    {
        if (!screenshotRequested) screenshotRequestedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        screenshotRequested = true;
        performance?.Record("screenshot_requested", 0);
    }

    private void DrawGrid()
    {
        var width = workspace?.Project.Venue.Width ?? 30;
        var height = workspace?.Project.Venue.Height ?? 20;
        const double dotSize = 2d;
        for (var row = 0; row <= height; row++)
        for (var column = 0; column <= width; column++)
        {
            var bounds = viewport.GetCellBounds(new GridCellAddress(column, row));
            DrawRectangle(new ScreenRectangle(
                bounds.X - dotSize / 2d,
                bounds.Y - dotSize / 2d,
                dotSize,
                dotSize), CanvasGridColor);
        }
    }

    private IReadOnlyList<RankedPlan> GetDisplayedPlans()
    {
        if (workspace is null)
            return [];
        var evaluated = workspace.RankPlans();
        if (UsesSeparatedLayouts)
        {
            if (editorMode is EditorMode.DeskPlacement or EditorMode.IslandDefinition)
            {
                return workspace.Project.DeskLayouts
                    .Select((desk, index) => new RankedPlan(index + 1, desk.Id, desk.Name,
                        0d, 0d, 0d, true, []))
                    .ToArray();
            }

            evaluated = evaluated.Where(item => workspace.Project.CircleLayouts
                .Any(circle => circle.Id == item.PlanId && circle.DeskLayoutId == workspace.SelectedDeskLayoutId)).ToArray();
        }
        if (editorMode != EditorMode.DeskPlacement && editorMode != EditorMode.IslandDefinition)
            return evaluated;
        return evaluated.OrderBy(plan => plan.PlanName, StringComparer.CurrentCulture)
            .ThenBy(plan => plan.PlanId, StringComparer.Ordinal)
            .Select((plan, index) => plan with { Rank = index + 1 })
            .ToArray();
    }

    private void CycleDisplayedPlan(int direction)
    {
        if (workspace is null || direction == 0)
            return;
        var plans = GetDisplayedPlans();
        if (plans.Count == 0) return;
        var currentIndex = plans.ToList().FindIndex(plan => plan.PlanId == SelectedDisplayedLayoutId);
        var nextIndex = (currentIndex + Math.Sign(direction) + plans.Count) % plans.Count;
        SelectDisplayedLayout(plans[nextIndex].PlanId);
        planScroll = Math.Clamp(nextIndex - GetVisiblePlanRowCount() + 1, 0, Math.Max(0, plans.Count - GetVisiblePlanRowCount()));
    }

    private bool ShowsDeskLayouts => editorMode is EditorMode.DeskPlacement or EditorMode.IslandDefinition;

    private string? SelectedDisplayedLayoutId => UsesSeparatedLayouts && ShowsDeskLayouts
        ? workspace!.SelectedDeskLayoutId : workspace?.SelectedPlanId;

    private void SelectDisplayedLayout(string id)
    {
        dragController?.Cancel();
        if (UsesSeparatedLayouts && ShowsDeskLayouts) workspace!.SelectDeskLayout(id);
        else workspace!.SelectPlan(id);
    }

    private bool CanRemoveLayout => UsesSeparatedLayouts && (ShowsDeskLayouts
        ? workspace!.CanRemoveSelectedDeskLayout
        : workspace!.HasSelectedCircleLayout && workspace.Project.CircleLayouts.Count > 1);

    private void DrawPlanList()
    {
        if (workspace is null)
            return;
        var plans = GetDisplayedPlans();
        var visibleCount = Math.Min(plans.Count, GetVisiblePlanRowCount());
        planScroll = Math.Clamp(planScroll, 0, Math.Max(0, plans.Count - visibleCount));
        var panel = GetPlanListBounds(visibleCount);
        var showsDeskLayouts = editorMode is EditorMode.DeskPlacement or EditorMode.IslandDefinition;
        var hasDeskParent = UsesSeparatedLayouts && !showsDeskLayouts;
        var listPanel = hasDeskParent
            ? new ScreenRectangle(panel.X + 8d, panel.Y + 32d, panel.Width - 8d, panel.Height - 32d)
            : panel;
        DrawRectangle(listPanel, new Color(20, 25, 32, 238));
        DrawOutline(listPanel, 2d, new Color(88, 103, 120));
        if (UsesSeparatedLayouts)
        {
            if (hasDeskParent)
                DrawLayoutButton(GetDeskLayoutParentBounds(), $"フレーム配置: {CurrentDeskLayoutName()} ▾", hoveredDeskLayoutParent);
            textRenderer?.Draw(
                showsDeskLayouts ? "フレーム配置" : "└ 配置案",
                new Rectangle((int)listPanel.X + 10, (int)listPanel.Y + 2, (int)listPanel.Width - 20, hasDeskParent ? 22 : 32),
                Color.White,
                hasDeskParent ? 18 : 22,
                true);

            DrawLayoutButton(GetLayoutAddBounds(), "+", hoveredLayoutAdd);
            DrawLayoutOrderButtons();
            DrawLayoutButton(GetLayoutDuplicateBounds(), "複製", hoveredLayoutDuplicate, showsDeskLayouts || workspace.HasSelectedCircleLayout);
            DrawLayoutButton(GetLayoutDeleteBounds(), "Remove", hoveredLayoutDelete, CanRemoveLayout);
            if (editorMode is EditorMode.GenrePlacement or EditorMode.CirclePlacement)
                DrawLayoutButton(GetLayoutBindBounds(), "Link", hoveredLayoutBind, workspace.HasSelectedCircleLayout);
            DrawLayoutButton(GetLayoutRenameBounds(), "Rename", hoveredLayoutRename, showsDeskLayouts || workspace.HasSelectedCircleLayout);
        }
        else
        {
            textRenderer?.Draw("配置案", new Rectangle((int)panel.X + 10, (int)panel.Y + 2, (int)panel.Width - 20, 32), Color.White, 22, true);
            var copy = GetPlanCopyBounds();
            DrawRectangle(copy, hoveredPlanCopy ? new Color(58, 82, 94) : new Color(36, 48, 58));
            DrawOutline(copy, 1d, hoveredPlanCopy ? new Color(178, 219, 226) : new Color(126, 150, 164));
            textRenderer?.Draw("色んなコピー", ToRectangle(copy, 5), Color.White, 13, true);
            var rename = GetPlanRenameBounds();
            DrawRectangle(rename, hoveredPlanRename ? new Color(58, 82, 94) : new Color(36, 48, 58));
            DrawOutline(rename, 1d, hoveredPlanRename ? new Color(178, 219, 226) : new Color(126, 150, 164));
            textRenderer?.Draw("Rename", ToRectangle(rename, 7), Color.White, 16, true);
        }
        if (plans.Count == 0)
        {
            textRenderer?.Draw("サークル配置案なし（＋で追加）", ToRectangle(GetPlanRowBounds(0), 6), new Color(184, 204, 214), 16);
            return;
        }
        var minimum = plans.Min(plan => plan.GeneralAttendeeScore);
        var maximum = plans.Max(plan => plan.GeneralAttendeeScore);
        for (var index = 0; index < visibleCount; index++)
        {
            var plan = plans[index + planScroll];
            var row = GetPlanRowBounds(index);
            var selected = plan.PlanId == SelectedDisplayedLayoutId;
            var hovered = plan.PlanId == hoveredPlanId;
            DrawRectangle(row, selected
                ? new Color(35, 126, 111)
                : hovered ? new Color(52, 65, 78) : new Color(29, 36, 45));
            DrawOutline(row, 1d, selected ? new Color(129, 235, 202) : new Color(70, 82, 96));
            var capacityMismatch = editorMode == EditorMode.DeskPlacement &&
                GetSpaceCapacity().Layouts[plan.PlanId] != GetSpaceCapacity().Requested;
            if (capacityMismatch)
                DrawErrorMark(new ScreenRectangle(row.X + row.Width - 25, row.Y + 5, 20, 20));

            textRenderer?.Draw(
                $"{plan.Rank}. {plan.PlanName}",
                new Rectangle((int)row.X + 8, (int)row.Y + 2,
                    showsDeskLayouts ? (int)row.Width - (capacityMismatch ? 36 : 16) : (int)row.Width - 136, 24),
                Color.White,
                pixelHeight: 19,
                bold: selected);
            if (!showsDeskLayouts)
            {
                textRenderer?.Draw(
                    plan.CombinedSpaceRequirementsSatisfied
                        ? $"般 {plan.GeneralAttendeeScore:0.##}  サ {plan.CircleParticipantScore:0.##}"
                        : "合体違反  般 0  サ 0",
                    new Rectangle((int)(row.X + row.Width) - 120, (int)row.Y + 2, 112, 24),
                    plan.CombinedSpaceRequirementsSatisfied ? new Color(244, 208, 111) : new Color(255, 116, 116),
                    pixelHeight: 16,
                    bold: true);

                var normalized = maximum <= minimum ? 1d : (plan.GeneralAttendeeScore - minimum) / (maximum - minimum);
                var vacant = VacantSeatCount(plan.PlanId);
                var barX = vacant > 0 ? row.X + 160 : row.X + 10;
                var barWidth = row.X + row.Width - 10 - barX;
                DrawRectangle(new ScreenRectangle(barX, row.Y + 27d, barWidth, 6d), new Color(13, 17, 22));
                DrawRectangle(new ScreenRectangle(barX, row.Y + 27d, Math.Max(3d, barWidth * normalized), 6d),
                    selected ? new Color(244, 208, 111) : new Color(104, 157, 204));
                if (vacant > 0)
                {
                    DrawRectangle(new ScreenRectangle(row.X + 5, row.Y + 24, 151, 13), new Color(91, 55, 14));
                    textRenderer?.Draw($"未完成・空き{vacant}スペース", new Rectangle((int)row.X + 7, (int)row.Y + 24, 147, 13),
                        VacancyColor, 12, true);
                    DrawRectangle(new ScreenRectangle(row.X, row.Y, 3, row.Height), VacancyColor);
                }
            }
        }
        DrawPlanScrollbar();
    }

    private string? HitTestPlanList(ScreenPoint pointer)
    {
        if (workspace is null)
            return null;
        var plans = GetDisplayedPlans();
        var visibleCount = Math.Min(plans.Count, GetVisiblePlanRowCount());
        planScroll = Math.Clamp(planScroll, 0, Math.Max(0, plans.Count - visibleCount));
        for (var index = 0; index < visibleCount; index++)
        {
            var row = GetPlanRowBounds(index);
            if (pointer.X >= row.X && pointer.X < row.X + row.Width &&
                pointer.Y >= row.Y && pointer.Y < row.Y + row.Height)
                return plans[index + planScroll].PlanId;
        }
        return null;
    }

    private int GetVisiblePlanRowCount() => Math.Max(0,
        (GraphicsDevice.PresentationParameters.BackBufferHeight - ToolbarHeight - 116 - StatusBarHeight -
         (editorMode == EditorMode.GenrePlacement ? 210 : ShowsChannels ? ChannelPanelReservedHeight : 0)) / 42);

    private ScreenRectangle GetPlanListBounds(int rowCount) => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth - 276d,
        ToolbarHeight + 12d,
        264d,
        Math.Max(1, rowCount) * 42d + 92d);

    private ScreenRectangle GetPlanRowBounds(int index) => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth - (UsesSeparatedLayouts && !ShowsDeskLayouts ? 264d : 272d),
        ToolbarHeight + 100d + index * 42d,
        UsesSeparatedLayouts && !ShowsDeskLayouts ? 228d : 236d,
        38d);

    private ScreenRectangle GetPlanCopyBounds() => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth - 192d,
        ToolbarHeight + 68d,
        90d,
        26d);

    private ScreenRectangle GetPlanRenameBounds() => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth - 96d,
        ToolbarHeight + 68d,
        76d,
        26d);

    private ScreenRectangle GetLayoutAddBounds() => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth - 264d, ToolbarHeight + 68d, 34d, 26d);

    private ScreenRectangle GetLayoutDeleteBounds() => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth - 174d, ToolbarHeight + 68d, 54d, 26d);

    private ScreenRectangle GetLayoutDuplicateBounds() => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth - 224d, ToolbarHeight + 68d, 46d, 26d);

    private ScreenRectangle GetLayoutBindBounds() => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth - 116d, ToolbarHeight + 68d, 42d, 26d);

    private ScreenRectangle GetDeskLayoutParentBounds() => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth - 276d, ToolbarHeight + 12d, 264d, 28d);

    private ScreenRectangle GetLayoutRenameBounds() => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth - 70d, ToolbarHeight + 68d, 50d, 26d);

    private bool UsesSeparatedLayouts => workspace is not null && workspace.Project.DeskLayouts.Count != 0;

    private string CurrentDeskLayoutName()
    {
        if (workspace is null)
            return "なし";
        return workspace.Project.DeskLayouts.Single(item => item.Id == workspace.SelectedDeskLayoutId).Name;
    }

    private void DrawLayoutButton(ScreenRectangle bounds, string label, bool hovered, bool enabled = true)
    {
        hovered &= enabled;
        DrawRectangle(bounds, hovered ? new Color(58, 82, 94) : new Color(36, 48, 58));
        DrawOutline(bounds, 1d, hovered ? new Color(178, 219, 226) : new Color(126, 150, 164));
        textRenderer?.Draw(label, ToRectangle(bounds, 3), enabled ? Color.White : new Color(100, 110, 120), label is "Rename" or "Remove" ? 13 : 15, true);
    }

    private ScreenRectangle GetGenreSummaryBounds()
    {
        var planCount = Math.Min(GetDisplayedPlans().Count, GetVisiblePlanRowCount());
        var planBottom = GetPlanListBounds(planCount).Y + GetPlanListBounds(planCount).Height;
        return new ScreenRectangle(GraphicsDevice.PresentationParameters.BackBufferWidth - 276d, planBottom + 10d, 264d, 196d);
    }

    private void DrawGenreSummary()
    {
        if (editorMode != EditorMode.GenrePlacement || workspace is null)
            return;
        var panel = GetGenreSummaryBounds();
        DrawRectangle(panel, new Color(20, 25, 32, 238));
        DrawOutline(panel, 2d, new Color(88, 103, 120));
        textRenderer?.Draw("ジャンル構成", new Rectangle((int)panel.X + 10, (int)panel.Y + 6, 150, 24), Color.White, 18, true);

        var groups = workspace.Project.Participants
            .GroupBy(item => string.IsNullOrWhiteSpace(item.GenreId) ? "（未設定）" : item.GenreId!, StringComparer.Ordinal)
            .Select(group => (GenreId: group.Key, Count: group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.GenreId, StringComparer.Ordinal)
            .ToArray();
        if (groups.Length == 0)
        {
            textRenderer?.Draw("サークルデータなし", new Rectangle((int)panel.X + 14, (int)panel.Y + 54, 220, 24),
                new Color(184, 204, 214), 16);
            return;
        }

        var total = groups.Sum(item => item.Count);
        var center = new ScreenPoint(panel.X + 66d, panel.Y + 91d);
        const double radius = 48d;
        const int segments = 180;
        for (var index = 0; index < segments; index++)
        {
            var target = (index + 0.5d) / segments * total;
            var running = 0;
            var slice = groups[0];
            foreach (var group in groups)
            {
                running += group.Count;
                if (target <= running)
                {
                    slice = group;
                    break;
                }
            }
            var angle = -Math.PI / 2d + index * Math.PI * 2d / segments;
            DrawLine(center, new ScreenPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius),
                3.5d, GetGenreStyle(slice.GenreId == "（未設定）" ? null : slice.GenreId).Primary);
        }
        DrawPiePatterns(center, radius, groups, total);
        DrawCircle(center, radius, new Color(226, 235, 240));

        var maximum = groups[0];
        textRenderer?.Draw($"最大: {maximum.GenreId} ({maximum.Count})",
            new Rectangle((int)panel.X + 12, (int)panel.Y + 148, 240, 23), new Color(244, 208, 111), 16, true);
        textRenderer?.Draw($"全 {total} サークル",
            new Rectangle((int)panel.X + 12, (int)panel.Y + 170, 240, 20), new Color(184, 204, 214), 14);

        for (var index = 0; index < Math.Min(5, groups.Length); index++)
        {
            var group = groups[index];
            var y = panel.Y + 42d + index * 20d;
            DrawGenreTile(new ScreenRectangle(panel.X + 124d, y + 3d, 12d, 12d),
                group.GenreId == "（未設定）" ? null : group.GenreId, new Color(226, 235, 240), 1d);
            textRenderer?.Draw($"{group.GenreId} {group.Count}",
                new Rectangle((int)panel.X + 142, (int)y, 108, 18), Color.White, 14);
        }
    }

    private IReadOnlyList<GenreDataGroup> BuildGenreDataGroups() => workspace?.Project.Participants
        .GroupBy(item => string.IsNullOrWhiteSpace(item.GenreId) ? "（未設定）" : item.GenreId!, StringComparer.Ordinal)
        .Select(group => new GenreDataGroup(
            group.Key,
            group.Sum(item => item.RequiredCellCount),
            group.Count()))
        .OrderByDescending(item => item.SpaceCount)
        .ThenByDescending(item => item.CircleCount)
        .ThenBy(item => item.GenreId, StringComparer.Ordinal)
        .ToArray() ?? [];

    private void DrawGenreDataDashboard()
    {
        if (workspace is null)
            return;

        var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var contentTop = ToolbarHeight + 12d;
        var contentBottom = height - StatusBarHeight - 12d;
        var contentHeight = Math.Max(1d, contentBottom - contentTop);
        var left = new ScreenRectangle(12d, contentTop, Math.Max(1d, width / 2d - 18d), contentHeight);
        var right = new ScreenRectangle(width / 2d + 6d, contentTop, Math.Max(1d, width / 2d - 18d), contentHeight);
        DrawRectangle(left, new Color(20, 25, 32, 238));
        DrawRectangle(right, new Color(20, 25, 32, 238));
        DrawOutline(left, 2d, new Color(88, 103, 120));
        DrawOutline(right, 2d, new Color(88, 103, 120));

        textRenderer?.Draw("ジャンル別フレーム構成", new Rectangle((int)left.X + 16, (int)left.Y + 12, (int)left.Width - 32, 28), Color.White, 21, true);
        textRenderer?.Draw("ジャンル名と網掛けパターン", new Rectangle((int)right.X + 16, (int)right.Y + 12, (int)right.Width - 32, 28), Color.White, 21, true);
        var groups = BuildGenreDataGroups();
        if (groups.Count == 0)
        {
            textRenderer?.Draw("サークルデータがありません", new Rectangle((int)left.X + 18, (int)left.Y + 56, (int)left.Width - 36, 24), new Color(184, 204, 214), 17);
            return;
        }

        var totalSpaces = groups.Sum(item => item.SpaceCount);
        var center = new ScreenPoint(left.X + left.Width / 2d, left.Y + left.Height / 2d + 12d);
        var radius = Math.Max(24d, Math.Min(left.Width / 2d - 34d, left.Height / 2d - 56d));
        const int segments = 720;
        for (var index = 0; index < segments; index++)
        {
            var slice = FindGenreSlice(groups, (index + .5d) / segments * totalSpaces);
            var angle = -Math.PI / 2d + index * Math.PI * 2d / segments;
            DrawLine(center, new ScreenPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius), 2.5d,
                GetGenreStyle(slice.GenreId == "（未設定）" ? null : slice.GenreId).Primary);
        }
        DrawPiePatterns(center, radius, groups.Select(item => (item.GenreId, item.SpaceCount)).ToArray(), totalSpaces);
        DrawCircle(center, radius, new Color(226, 235, 240));
        textRenderer?.Draw($"合計 {totalSpaces}sp", new Rectangle((int)left.X + 16, (int)(left.Y + left.Height - 36), (int)left.Width - 32, 24), new Color(244, 208, 111), 18, true);

        DrawGenreDataLegend(right, groups);
    }

    private static GenreDataGroup FindGenreSlice(IReadOnlyList<GenreDataGroup> groups, double target)
    {
        var running = 0;
        foreach (var group in groups)
        {
            running += group.SpaceCount;
            if (target <= running)
                return group;
        }
        return groups[^1];
    }

    private void DrawGenreDataLegend(ScreenRectangle panel, IReadOnlyList<GenreDataGroup> groups)
    {
        const double headerHeight = 48d;
        const double rowHeight = 42d;
        var rowsPerColumn = Math.Max(1, (int)((panel.Height - headerHeight - 10d) / rowHeight));
        var columnCount = (int)Math.Ceiling(groups.Count / (double)rowsPerColumn);
        var columnWidth = panel.Width / columnCount;
        for (var index = 0; index < groups.Count; index++)
        {
            var column = index / rowsPerColumn;
            var row = index % rowsPerColumn;
            var group = groups[index];
            var x = panel.X + column * columnWidth + 12d;
            var y = panel.Y + headerHeight + row * rowHeight;
            var swatch = new ScreenRectangle(x, y + 4d, Math.Min(58d, Math.Max(24d, columnWidth * .2d)), 26d);
            DrawGenreTile(swatch, group.GenreId == "（未設定）" ? null : group.GenreId, new Color(226, 235, 240), 1d);
            var textX = swatch.X + swatch.Width + 9d;
            var statsWidth = Math.Min(92d, Math.Max(58d, columnWidth * .28d));
            textRenderer?.Draw(group.GenreId, new Rectangle((int)textX, (int)y, Math.Max(1, (int)(columnWidth - swatch.Width - statsWidth - 32d)), 20), Color.White, 16, true);
            textRenderer?.Draw($"{group.SpaceCount}sp  {group.CircleCount}c", new Rectangle((int)(panel.X + (column + 1) * columnWidth - statsWidth - 10d), (int)y, (int)statsWidth, 20), new Color(244, 208, 111), 15, true);
        }
    }

    private void DrawPiePatterns(
        ScreenPoint center,
        double radius,
        IReadOnlyList<(string GenreId, int Count)> groups,
        int total)
    {
        var styles = groups.Select(group => group.GenreId).Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, id => GetGenreStyle(id == "（未設定）" ? null : id), StringComparer.Ordinal);
        var sampleSize = styles.Values.Any(style => DiagonalPattern.IsDiagonal(style.Pattern)) ? 1 : radius > 120d ? 4 : 2;
        for (var offsetY = -(int)radius; offsetY < radius; offsetY += sampleSize)
        for (var offsetX = -(int)radius; offsetX < radius; offsetX += sampleSize)
        {
            if (offsetX * offsetX + offsetY * offsetY > (radius - 1d) * (radius - 1d))
                continue;
            var angle = Math.Atan2(offsetY, offsetX) + Math.PI / 2d;
            if (angle < 0d)
                angle += Math.PI * 2d;
            var target = angle / (Math.PI * 2d) * total;
            var running = 0;
            var slice = groups[0];
            foreach (var group in groups)
            {
                running += group.Count;
                if (target < running)
                {
                    slice = group;
                    break;
                }
            }
            var style = styles[slice.GenreId];
            if (!GenrePatternUsesSecondary(style.Pattern, offsetX + (int)radius, offsetY + (int)radius))
                continue;
            var secondary = new Color(style.Secondary.R, style.Secondary.G, style.Secondary.B, (byte)255);
            DrawRectangle(new ScreenRectangle(center.X + offsetX, center.Y + offsetY, sampleSize, sampleSize), secondary);
        }
    }

    private static bool GenrePatternUsesSecondary(int pattern, int x, int y)
    {
        if (DiagonalPattern.IsDiagonal(pattern)) return DiagonalPattern.UsesSecondary(pattern, x, y);
        const int spacing = 10;
        return pattern switch
        {
            1 => PositiveModulo(y - spacing / 2, spacing) < 2,
            2 => PositiveModulo(x - spacing / 2, spacing) < 3,
            3 => PositiveModulo(x - 3, spacing) < spacing / 2 &&
                 PositiveModulo(y - 3, spacing) < spacing / 2,
            4 => PositiveModulo(x - spacing / 2, spacing) < 2 ||
                 PositiveModulo(y - spacing / 2, spacing) < 2,
            5 => PositiveModulo(y, spacing) < spacing / 2,
            6 => IsPieDot(x, y, spacing),
            7 => (x / (spacing / 2) + y / (spacing / 2)) % 2 == 0,
            _ => false,
        };
    }

    private static bool IsPieDot(int x, int y, int spacing)
    {
        var row = y / spacing;
        return PositiveModulo(y - 3, spacing) < 3 &&
               PositiveModulo(x - 3 - (row % 2 == 1 ? spacing / 2 : 0), spacing) < 3;
    }

    private static int PositiveModulo(int value, int divisor) => (value % divisor + divisor) % divisor;

    private void DrawVenueTopology()
    {
        if (workspace is null)
            return;
        var snapshot = workspace.GetSelectedPlanSnapshot();
        var desks = snapshot.Desks.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var deskTypes = workspace.Project.DeskTypes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var pointer = new ScreenPoint(Mouse.GetState().X, Mouse.GetState().Y);
        var disabledConnections = workspace.SelectedPlan.DisabledIslandConnections
            .Select(connection => NormalizeConnection(connection.FirstCell, connection.SecondCell))
            .ToHashSet();
        foreach (var connection in workspace.View.AutomaticEdges)
        {
            var normalized = NormalizeConnection(connection.FirstCell, connection.SecondCell);
            var color = disabledConnections.Contains(normalized) ? DisabledIslandConnectionColor : IslandConnectionColor;
            DrawLine(GetCellCenter(connection.FirstCell), GetCellCenter(connection.SecondCell), 5d, color);
            DrawCircle(GetCellCenter(connection.FirstCell), 4d, color);
            DrawCircle(GetCellCenter(connection.SecondCell), 4d, color);
        }
        if (CanShowEditorHover && activeCanvasTool == ToolbarAction.ToggleAutomaticIslandConnection &&
            TryFindAutomaticIslandConnection(pointer, out var hoveredAutomaticConnection))
        {
            var firstPoint = GetCellCenter(hoveredAutomaticConnection.FirstCell);
            var secondPoint = GetCellCenter(hoveredAutomaticConnection.SecondCell);
            DrawLine(firstPoint, secondPoint, 9d, new Color(255, 225, 92));
            DrawCircle(firstPoint, 6d, Color.White);
            DrawCircle(secondPoint, 6d, Color.White);
        }

        foreach (var region in workspace.SelectedPlan.FacingRegions)
        {
            var rectangle = InsetFacingRegion(GetCellRegionBounds(region.FirstCorner, region.SecondCorner));
            var highlighted = CanShowEditorHover && activeCanvasTool == ToolbarAction.RemoveTopology && Contains(rectangle, pointer);
            DrawRectangle(rectangle, highlighted ? new Color(230, 84, 84, 82) : new Color(142, 82, 210, 48));
            DrawOutline(rectangle, highlighted ? 6d : 3d, highlighted ? new Color(255, 116, 116) : new Color(190, 126, 255));
        }

        foreach (var connector in workspace.SelectedPlan.IslandConnectors)
        {
            if (IslandConnectorEndpoints.Resolve(workspace.SelectedPlan, deskTypes, connector, workspace.View.AutomaticEdges) is not { } endpoints)
                continue;
            var firstPoint = GetCellCenter(endpoints.FirstCell);
            var secondPoint = GetCellCenter(endpoints.SecondCell);
            var highlighted = CanShowEditorHover && activeCanvasTool == ToolbarAction.RemoveTopology &&
                DistanceSquaredToSegment(pointer, firstPoint, secondPoint) <= 81d;
            DrawLine(firstPoint, secondPoint, highlighted ? 9d : 5d, highlighted ? new Color(255, 92, 92) : new Color(255, 166, 64));
            DrawCircle(firstPoint, highlighted ? 6d : 4d, highlighted ? Color.White : new Color(255, 190, 92));
            DrawCircle(secondPoint, highlighted ? 6d : 4d, highlighted ? Color.White : new Color(255, 190, 92));
        }

        DrawIslandDistances();

        if (topologyFirstCell is { } selectedCell)
            DrawLine(GetCellCenter(selectedCell), pointer, 3d, new Color(255, 190, 92, 180));
        if (topologyFirstCorner is { } corner)
        {
            var current = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
            DrawOutline(InsetFacingRegion(GetCellRegionBounds(corner, current)), 2d, new Color(211, 169, 255, 180));
        }
    }

    private void DrawEvaluationAnalysis()
    {
        if (workspace is null)
            return;
        var plan = workspace.SelectedPlan;
        var topology = workspace.View.Topology;
        var participantsById = workspace.Project.Participants.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var combinedPartners = workspace.View.CombinedPartners;
        var genresByParticipant = participantsById.ToDictionary(
            pair => pair.Key,
            pair => combinedPartners[pair.Key].Append(pair.Key)
                .Select(id => participantsById[id].GenreId)
                .Where(genre => !string.IsNullOrWhiteSpace(genre))
                .Select(genre => genre!)
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var genresByCell = plan.Assignments
            .SelectMany(assignment =>
                assignment.OccupiedCells.Select(cell => (Cell: cell, Genres: genresByParticipant[assignment.ParticipantId])))
            .ToDictionary(item => item.Cell, item => item.Genres, EqualityComparer<GridPosition>.Default);
        var bright = new Color(104, 255, 124);
        var shadow = new Color(9, 42, 25, 220);

        foreach (var (cell, adjacent) in topology.Neighbors)
        foreach (var neighbor in adjacent.Where(neighbor => CompareCells(cell, neighbor) < 0))
            if (genresByCell.TryGetValue(cell, out var firstGenres) &&
                genresByCell.TryGetValue(neighbor, out var secondGenres) && firstGenres.Overlaps(secondGenres))
            {
                var first = GetCellCenter(cell);
                var second = GetCellCenter(neighbor);
                DrawLine(first, second, 9d, shadow);
                DrawLine(first, second, 5d, bright);
            }

        foreach (var (firstCell, secondCell) in topology.FacingCellPairs)
            if (genresByCell.TryGetValue(firstCell, out var firstGenres) &&
                genresByCell.TryGetValue(secondCell, out var secondGenres) && firstGenres.Overlaps(secondGenres))
                DrawAnalysisQuadrilateral(GetCellCenter(firstCell), GetCellCenter(secondCell), shadow, bright);

        static int CompareCells(GridPosition first, GridPosition second) =>
            first.Y != second.Y ? first.Y.CompareTo(second.Y) : first.X.CompareTo(second.X);
    }

    private void DrawAnalysisQuadrilateral(ScreenPoint first, ScreenPoint second, Color shadow, Color bright)
    {
        var delta = new Vector2((float)(second.X - first.X), (float)(second.Y - first.Y));
        if (delta.LengthSquared() < 0.001f) return;
        var perpendicular = Vector2.Normalize(new Vector2(-delta.Y, delta.X)) * 6f;
        var a = new ScreenPoint(first.X + perpendicular.X, first.Y + perpendicular.Y);
        var b = new ScreenPoint(second.X + perpendicular.X, second.Y + perpendicular.Y);
        var c = new ScreenPoint(second.X - perpendicular.X, second.Y - perpendicular.Y);
        var d = new ScreenPoint(first.X - perpendicular.X, first.Y - perpendicular.Y);
        foreach (var (from, to) in new[] { (a, b), (b, c), (c, d), (d, a) })
            DrawLine(from, to, 5d, shadow);
        foreach (var (from, to) in new[] { (a, b), (b, c), (c, d), (d, a) })
            DrawLine(from, to, 2d, bright);
    }

    private ScreenPoint GetDeskCenter(IEnumerable<GridPosition> cells)
    {
        var value = cells.ToArray();
        var topLeft = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(value.Min(cell => cell.X), value.Min(cell => cell.Y))));
        var bottomRight = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(value.Max(cell => cell.X) + 1, value.Max(cell => cell.Y) + 1)));
        return new ScreenPoint((topLeft.X + bottomRight.X) / 2d, (topLeft.Y + bottomRight.Y) / 2d);
    }

    private bool TryToggleAutomaticIslandConnection(
        ScreenPoint pointer,
        out (GridPosition FirstCell, GridPosition SecondCell) connection)
    {
        connection = default;
        if (workspace is null || commandController is null)
            return false;
        if (!TryFindAutomaticIslandConnection(pointer, out var candidate))
            return false;
        var result = commandController.ToggleAutomaticIslandConnection(candidate.FirstCell, candidate.SecondCell);
        if (!result.Applied)
            return false;
        connection = candidate;
        return true;
    }

    private bool TryFindAutomaticIslandConnection(
        ScreenPoint pointer,
        out (GridPosition FirstCell, GridPosition SecondCell) connection)
    {
        connection = default;
        if (workspace is null)
            return false;
        var deskTypes = workspace.Project.DeskTypes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var candidate = workspace.View.AutomaticEdges
            .Select(item => (Connection: item, Distance: DistanceSquaredToSegment(pointer,
                GetCellCenter(item.FirstCell), GetCellCenter(item.SecondCell))))
            .Where(item => item.Distance <= 81d)
            .OrderBy(item => item.Distance)
            .Select(item => item.Connection)
            .FirstOrDefault();
        if (candidate == default)
            return false;
        connection = candidate;
        return true;
    }

    private static (GridPosition FirstCell, GridPosition SecondCell) NormalizeConnection(GridPosition first, GridPosition second) =>
        first.Y < second.Y || first.Y == second.Y && first.X <= second.X ? (first, second) : (second, first);

    private static double DistanceSquaredToSegment(ScreenPoint point, ScreenPoint first, ScreenPoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0.001d)
            return DistanceSquared(point, first);
        var t = Math.Clamp(((point.X - first.X) * dx + (point.Y - first.Y) * dy) / lengthSquared, 0d, 1d);
        return DistanceSquared(point, new ScreenPoint(first.X + t * dx, first.Y + t * dy));
    }

    private ScreenRectangle GetCellRegionBounds(GridPosition first, GridPosition second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        var right = Math.Max(first.X, second.X) + 1;
        var bottom = Math.Max(first.Y, second.Y) + 1;
        var topLeft = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(left, top)));
        var bottomRight = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(right, bottom)));
        return new ScreenRectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    private ScreenRectangle InsetFacingRegion(ScreenRectangle bounds)
    {
        var cellBounds = viewport.GetCellBounds(new GridCellAddress(0, 0));
        var insetX = Math.Min(cellBounds.Width / 4d, Math.Max(0d, bounds.Width / 2d - 1d));
        var insetY = Math.Min(cellBounds.Height / 4d, Math.Max(0d, bounds.Height / 2d - 1d));
        return new ScreenRectangle(
            bounds.X + insetX,
            bounds.Y + insetY,
            bounds.Width - insetX * 2d,
            bounds.Height - insetY * 2d);
    }

    private void DrawDesks()
    {
        if (workspace is null)
            return;

        if (editorMode == EditorMode.GenrePlacement)
            return;

        if (editorMode == EditorMode.IslandDefinition || IsBlockNumberChannelSelected)
        {
            foreach (var desk in workspace.GetSelectedPlanSnapshot().Desks)
                DrawDeskWireframe(desk.OccupiedCells, desk.Orientation, GenreDeskWireframeColor);
            if (IsBlockNumberChannelSelected && dragController?.PreviewAnchor is { } wireAnchor &&
                dragController.DraggedDeskId is { } wireDeskId)
            {
                var placed = workspace.SelectedPlan.DeskPlacements.Single(item => item.Id == wireDeskId);
                var type = workspace.Project.DeskTypes.Single(item => item.Id == placed.DeskTypeId);
                DrawDeskWireframe(type.Footprint.Select(cell => wireAnchor + cell.Rotate(placed.Orientation)),
                    placed.Orientation, OperationTargetColor);
            }
            return;
        }

        var snapshot = workspace.GetSelectedPlanSnapshot();
        foreach (var desk in snapshot.Desks)
        {
            var placed = workspace.SelectedPlan.DeskPlacements.Single(p => p.Id == desk.Id);
            var definition = workspace.Project.DeskTypes.Single(t => t.Id == placed.DeskTypeId);
            if (definition.Space is { } space)
            {
                DrawSpaceOnCanvas(space, placed.Anchor, placed.Orientation, false, false);
                continue;
            }
            DrawDesk(
                desk.OccupiedCells,
                desk.Orientation,
                new Color(198, 145, 54),
                new Color(92, 60, 23),
                new Color(145, 98, 38));
        }

        if (dragController?.PreviewAnchor is { } previewAnchor && dragController.DraggedDeskId is { } deskId)
        {
            var placement = workspace.SelectedPlan.DeskPlacements.Single(item => item.Id == deskId);
            var deskType = workspace.Project.DeskTypes.Single(item => item.Id == placement.DeskTypeId);
            if (deskType.Space is { } space)
            {
                DrawSpaceOnCanvas(space, previewAnchor, placement.Orientation, true, false);
                return;
            }
            var previewCells = deskType.Footprint
                .Select(relative => previewAnchor + relative.Rotate(placement.Orientation));
            DrawDesk(
                previewCells,
                placement.Orientation,
                new Color(232, 174, 72),
                new Color(255, 220, 139),
                new Color(145, 98, 38));
        }
    }

    private void DrawMissingDeskNumbers()
    {
        if (workspace is null || editorMode != EditorMode.DeskPlacement)
            return;

        var plan = workspace.SelectedPlan;
        foreach (var placement in CircleSeatExportBuilder.FindMissingDeskNumbers(plan))
        {
            var type = workspace.Project.DeskTypes.Single(item => item.Id == placement.DeskTypeId);
            var cells = placement.GetOccupiedCells(type);
            if (cells.Count == 0) continue;
            var topLeft = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(cells.Min(cell => cell.X), cells.Min(cell => cell.Y))));
            var bottomRight = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(cells.Max(cell => cell.X) + 1, cells.Max(cell => cell.Y) + 1)));
            var warning = new ScreenRectangle(topLeft.X + 2, topLeft.Y + 2,
                Math.Max(1, bottomRight.X - topLeft.X - 4), Math.Max(1, bottomRight.Y - topLeft.Y - 4));
            DrawMissingNumber(warning);
        }
    }

    private bool IsSpaceNumberTarget(string placementId, GridPosition cell)
    {
        if (workspace is null) return false;
        var placement = workspace.SelectedPlan.DeskPlacements.Single(item => item.Id == placementId);
        var type = workspace.Project.DeskTypes.Single(item => item.Id == placement.DeskTypeId);
        return (selectedNumberChannel == 1 || activeCanvasTool == ToolbarAction.EditDeskNumber
            ? placement.GetFrameNumberCells(type) : placement.GetSeatCells(type)).Contains(cell);
    }

    private int VenueTextSize(int basePixelHeight) =>
        Math.Clamp((int)Math.Round(basePixelHeight * viewport.Zoom), 6, 40);

    private void DrawDeskEditTarget()
    {
        if (workspace is null || editorMode != EditorMode.DeskPlacement || !CanShowEditorHover)
            return;

        var desks = workspace.GetSelectedPlanSnapshot().Desks;
        if (dragController?.DraggedDeskId is { } draggedId && dragController.PreviewAnchor is { } previewAnchor)
        {
            var dragged = desks.Single(desk => desk.Id == draggedId);
            DrawRectangle(TargetBounds(dragged.OccupiedCells), SelectionHighlightColor);
            var offset = new GridPosition(previewAnchor.X - dragged.Anchor.X, previewAnchor.Y - dragged.Anchor.Y);
            DrawOutline(TargetBounds(dragged.OccupiedCells.Select(cell => cell + offset)), 3d, OperationTargetColor);
            return;
        }

        var mouse = Mouse.GetState();
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        var keyboard = Keyboard.GetState();
        if (!IsPointerInEditorCanvas(pointer) || IsControlDown(keyboard) || keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) || keyboard.IsKeyDown(Keys.Space) ||
            leftPanActive || rangeSelectionAnchor is not null || pressedToolbarButton is not null)
            return;
        if (activeCanvasTool is not (ToolbarAction.EditSeatName or ToolbarAction.EditDeskNumber or
            ToolbarAction.MoveDesk or ToolbarAction.RemoveDesk or ToolbarAction.RotateLeft or ToolbarAction.RotateRight))
            return;

        var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
        // A click inside a selected seat-name range edits the entire range.
        if (activeCanvasTool == ToolbarAction.EditSeatName && IsWeightChannelSelected && !GetWeightChannelCells().Contains(cell))
            return;
        if (IsSeatNameRangeEditing && selectedCellRange is { } range && range.Contains(cell))
        {
            DrawOutline(GetCellRegionBounds(new GridPosition(range.Left, range.Top),
                new GridPosition(range.Right, range.Bottom)), 3d, OperationTargetColor);
            return;
        }
        var desk = desks.LastOrDefault(item => item.OccupiedCells.Contains(cell));
        if (desk is null)
            return;

        if ((activeCanvasTool == ToolbarAction.EditDeskNumber || activeCanvasTool == ToolbarAction.EditSeatName && !IsWeightChannelSelected) &&
            !IsSpaceNumberTarget(desk.Id, cell))
            return;

        DrawOutline(TargetBounds(activeCanvasTool == ToolbarAction.EditSeatName && (IsWeightChannelSelected || selectedNumberChannel != 1) ? [cell] : desk.OccupiedCells),
            3d, OperationTargetColor);

        ScreenRectangle TargetBounds(IEnumerable<GridPosition> cells)
        {
            var occupied = cells.ToArray();
            var bounds = GetCellRegionBounds(
                new GridPosition(occupied.Min(item => item.X), occupied.Min(item => item.Y)),
                new GridPosition(occupied.Max(item => item.X), occupied.Max(item => item.Y)));
            return new ScreenRectangle(bounds.X + 1d, bounds.Y + 1d, bounds.Width - 2d, bounds.Height - 2d);
        }
    }

    private void DrawGenreDeskWireframes()
    {
        if (workspace is null)
            return;

        foreach (var desk in workspace.GetSelectedPlanSnapshot().Desks)
            DrawDeskWireframe(desk.OccupiedCells, desk.Orientation, GenreDeskWireframeColor, outlineWidth: 1d);
    }

    private void DrawDesk(
        IEnumerable<GridPosition> occupiedCells,
        QuarterTurn orientation,
        Color fill,
        Color border,
        Color marker)
    {
        var cells = occupiedCells.ToArray();
        if (cells.Length == 0)
            return;

        var left = cells.Min(cell => cell.X);
        var top = cells.Min(cell => cell.Y);
        var right = cells.Max(cell => cell.X) + 1;
        var bottom = cells.Max(cell => cell.Y) + 1;
        var topLeft = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(left, top)));
        var bottomRight = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(right, bottom)));
        var bounds = new ScreenRectangle(
            topLeft.X + 2d,
            topLeft.Y + 2d,
            bottomRight.X - topLeft.X - 4d,
            bottomRight.Y - topLeft.Y - 4d);

        DrawRectangle(bounds, fill);
        DrawOutline(bounds, 2d, border);
        DrawDeskOrientationMarker(bounds, orientation, marker);
    }

    private void DrawDeskOrientationMarker(ScreenRectangle bounds, QuarterTurn orientation, Color color)
    {
        const double thickness = 4d;
        var horizontalLength = Math.Min(18d, Math.Max(8d, bounds.Width * 0.28d));
        var verticalLength = Math.Min(18d, Math.Max(8d, bounds.Height * 0.28d));
        var marker = orientation switch
        {
            QuarterTurn.North => new ScreenRectangle(bounds.X + (bounds.Width - horizontalLength) / 2d, bounds.Y + 3d, horizontalLength, thickness),
            QuarterTurn.East => new ScreenRectangle(bounds.X + bounds.Width - thickness - 3d, bounds.Y + (bounds.Height - verticalLength) / 2d, thickness, verticalLength),
            QuarterTurn.South => new ScreenRectangle(bounds.X + (bounds.Width - horizontalLength) / 2d, bounds.Y + bounds.Height - thickness - 3d, horizontalLength, thickness),
            QuarterTurn.West => new ScreenRectangle(bounds.X + 3d, bounds.Y + (bounds.Height - verticalLength) / 2d, thickness, verticalLength),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation)),
        };
        DrawRectangle(marker, color);
    }

    private void DrawDeskPlacementGhost()
    {
        if (!CanShowEditorHover) return;
        if (workspace is null || activeCanvasTool != ToolbarAction.AddDesk)
            return;

        var mouse = Mouse.GetState();
        var currentPointer = new ScreenPoint(mouse.X, mouse.Y);
        var pointer = IsPointerInEditorCanvas(currentPointer)
            ? currentPointer
            : IsGhostRotationButtonHovered()
                ? lastDeskGhostPointer
                : null;
        if (pointer is null)
            return;

        var deskType = NextSpaceType;
        if (deskType is null)
            return;

        var anchor = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer.Value));
        var cells = deskType.Footprint
            .Select(relative => anchor + relative.Rotate(nextDeskOrientation))
            .ToArray();
        var occupied = workspace.GetSelectedPlanSnapshot().Desks
            .SelectMany(desk => desk.OccupiedCells)
            .ToHashSet();
        var canPlace = cells.All(cell => workspace.Project.Venue.CanPlaceAt(cell) && !occupied.Contains(cell));
        if (deskType.Space is { } space)
        {
            DrawSpaceOnCanvas(space, anchor, nextDeskOrientation, true, false,
                canPlace ? new Color(178, 232, 255) : new Color(236, 135, 145));
            return;
        }
        DrawDeskWireframe(
            cells,
            nextDeskOrientation,
            canPlace ? new Color(178, 232, 255, 220) : new Color(236, 135, 145, 210));
    }

    private void DrawDeskWireframe(
        IEnumerable<GridPosition> occupiedCells,
        QuarterTurn orientation,
        Color color,
        double outlineWidth = 3d)
    {
        var cells = occupiedCells.ToArray();
        if (cells.Length == 0)
            return;

        var left = cells.Min(cell => cell.X);
        var top = cells.Min(cell => cell.Y);
        var right = cells.Max(cell => cell.X) + 1;
        var bottom = cells.Max(cell => cell.Y) + 1;
        var topLeft = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(left, top)));
        var bottomRight = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(right, bottom)));
        var bounds = new ScreenRectangle(
            topLeft.X + 3d,
            topLeft.Y + 3d,
            bottomRight.X - topLeft.X - 6d,
            bottomRight.Y - topLeft.Y - 6d);

        DrawOutline(bounds, outlineWidth, color);
        DrawDeskOrientationMarker(bounds, orientation, color);
    }

    private bool IsPointerInEditorCanvas(ScreenPoint pointer)
    {
        if (editorMode == EditorMode.DeskPlacement && (Contains(NextSpaceBounds, pointer) || Contains(SpaceCapacityBounds, pointer))) return false;
        var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        if (pointer.Y < ToolbarHeight || pointer.Y >= height - StatusBarHeight)
            return false;

        var visiblePlanCount = Math.Min(GetDisplayedPlans().Count, GetVisiblePlanRowCount());
        if (Contains(GetPlanListBounds(visiblePlanCount), pointer))
            return false;
        if (ShowsChannels && Contains(GetChannelPanelBounds(), pointer))
            return false;
        return editorMode != EditorMode.GenrePlacement || !Contains(GetGenreSummaryBounds(), pointer);
    }

    private bool IsGhostRotationButtonHovered() => toolbarButtons.Any(button =>
        button.Model.IsPointerOver &&
        button.Action is ToolbarAction.RotateLeft or ToolbarAction.RotateRight);

    private bool IsDeskGhostClearOfToolbar(ScreenPoint pointer)
    {
        var deskType = NextSpaceType;
        if (deskType is null)
            return false;

        var anchor = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
        var top = deskType.Footprint
            .Select(relative => anchor + relative.Rotate(nextDeskOrientation))
            .Min(cell => cell.Y);
        var topCellBounds = viewport.GetCellBounds(
            VenueCanvasMapper.ToCanvasCell(new GridPosition(anchor.X, top)));
        return topCellBounds.Y + 3d >= ToolbarHeight;
    }

    private void DrawBlockedCells()
    {
        if (workspace is null)
            return;
        foreach (var cell in workspace.Project.Venue.BlockedCells)
        {
            var hovered = CanShowEditorHover && activeCanvasTool == ToolbarAction.RemovePillar &&
                VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(new ScreenPoint(Mouse.GetState().X, Mouse.GetState().Y))) == cell;
            DrawPillar(cell, hovered ? new Color(160, 76, 76) : new Color(54, 60, 68),
                hovered ? new Color(255, 128, 128) : new Color(126, 136, 146));
        }
    }

    private void DrawPillarGhost()
    {
        if (!CanShowEditorHover) return;
        if (workspace is null || activeCanvasTool != ToolbarAction.AddPillar)
            return;
        var pointer = new ScreenPoint(Mouse.GetState().X, Mouse.GetState().Y);
        if (!IsPointerInEditorCanvas(pointer))
            return;
        var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
        var occupied = workspace.GetSelectedPlanSnapshot().Desks.Any(desk => desk.OccupiedCells.Contains(cell));
        var canPlace = workspace.Project.Venue.CanPlaceAt(cell) && !occupied;
        DrawPillar(cell, canPlace ? new Color(77, 88, 98, 180) : new Color(154, 70, 70, 180),
            canPlace ? new Color(184, 198, 210, 220) : new Color(255, 138, 138, 220));
    }

    private void DrawPillar(GridPosition cell, Color fill, Color outline)
    {
        var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
        var inner = new ScreenRectangle(bounds.X + 2d, bounds.Y + 2d, bounds.Width - 4d, bounds.Height - 4d);
        DrawRectangle(inner, fill);
        DrawOutline(inner, 2d, outline);
        for (var y = inner.Y + 7d; y < inner.Y + inner.Height - 3d; y += 9d)
        for (var x = inner.X + 7d; x < inner.X + inner.Width - 3d; x += 9d)
            DrawCircle(new ScreenPoint(x, y), 1.5d, outline);
    }

    private void DrawRangeSelection()
    {
        if (!CanSelectCellRange)
            return;
        if (IsFrameNumberChannelSelected)
        {
            DrawFrameSelection();
            return;
        }
        var range = rangeSelectionAnchor is { } anchor && rangeSelectionCurrent is { } current
            ? CellRange.From(anchor, current)
            : selectedCellRange;
        if (range is { } selected)
        {
            var bounds = GetCellRegionBounds(new GridPosition(selected.Left, selected.Top),
                new GridPosition(selected.Right, selected.Bottom));
            DrawRectangle(bounds, SelectionHighlightColor);
            if (IsBlockNumberChannelSelected) DrawOutline(bounds, 2, OperationTargetColor);
        }
        if (draggedCellRange is { } dragged)
        {
            var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(participantDragPointer));
            var target = new GridPosition(cell.X - rangeDragGrabOffset.X, cell.Y - rangeDragGrabOffset.Y);
            var bounds = GetCellRegionBounds(target,
                new GridPosition(target.X + dragged.Width - 1, target.Y + dragged.Height - 1));
            DrawOutline(bounds, 3d, OperationTargetColor);
        }
    }

    private void DrawAssignments()
    {
        if (workspace is null)
            return;

        var snapshot = workspace.GetSelectedPlanSnapshot();
        var tokens = BuildParticipantTokens(snapshot);

        foreach (var pair in tokens.Where(token => token.CombinedSpaceId is not null).GroupBy(token => token.CombinedSpaceId))
        {
            var members = pair.Take(2).ToArray();
            if (members.Length == 2)
                DrawCombinedConnector(members[0], members[1], snapshot.AudienceEvaluation.CombinedSpaceRequirementsSatisfied);
        }
        foreach (var token in tokens.Where(token =>
                     token.ParticipantId != draggedParticipantToken?.ParticipantId && IsTwoSpaceSingleCircle(token)))
            DrawTwoSpaceConnector(token);
        foreach (var token in tokens.Where(token => token.ParticipantId != draggedParticipantToken?.ParticipantId))
            DrawParticipantToken(token);
    }

    private void DrawGenreAssignments()
    {
        if (workspace is null)
            return;
        foreach (var assignment in workspace.GetSelectedPlanSnapshot().Assignments
                     .Where(item => item.ParticipantId != draggedParticipantToken?.ParticipantId))
        {
            if (!AreTokenCellsSeats(assignment.OccupiedCells)) continue;
            foreach (var cell in assignment.OccupiedCells)
            {
                var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
                var inset = editorMode == EditorMode.GenrePlacement ? 4d : 7d;
                DrawGenreTile(new ScreenRectangle(bounds.X + inset, bounds.Y + inset,
                    bounds.Width - inset * 2d, bounds.Height - inset * 2d), assignment.GenreId, Color.Transparent, 0d);
            }
        }
    }

    private void DrawGenreTokensAndConnectors()
    {
        if (workspace is null)
            return;
        var snapshot = workspace.GetSelectedPlanSnapshot();
        var tokens = BuildParticipantTokens(snapshot);
        foreach (var pair in tokens.Where(token => token.CombinedSpaceId is not null).GroupBy(token => token.CombinedSpaceId))
        {
            var members = pair.Take(2).ToArray();
            if (members.Length == 2)
                DrawCombinedConnector(members[0], members[1], snapshot.AudienceEvaluation.CombinedSpaceRequirementsSatisfied);
        }
        foreach (var token in tokens.Where(token =>
                     token.ParticipantId != draggedParticipantToken?.ParticipantId && IsTwoSpaceSingleCircle(token)))
            DrawTwoSpaceConnector(token);
        foreach (var token in tokens.Where(token => token.ParticipantId != draggedParticipantToken?.ParticipantId))
        {
            var genreId = workspace.Project.Participants.Single(item => item.Id == token.ParticipantId).GenreId;
            foreach (var cell in token.DisplayCells)
            {
                var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
                var inset = token.Assigned ? 5d : 7d;
                var tile = new ScreenRectangle(bounds.X + inset, bounds.Y + inset, bounds.Width - inset * 2d, bounds.Height - inset * 2d);
                if (IsOffSeatToken(token))
                {
                    DrawOffSeatToken(tile, genreId, IsTwoSpaceSingleCircle(token) && cell != token.Position);
                    continue;
                }
                if (IsTwoSpaceSingleCircle(token) && cell != token.Position)
                {
                    DrawRectangle(tile, CanvasGridColor);
                    DrawOutline(tile, 2d, GetGenreStyle(genreId).Primary);
                }
                else
                    DrawGenreTile(tile, genreId, CanvasGridColor, 1d);
            }
        }
    }

    private GenreVisualStyle GetGenreStyle(string? genreId)
    {
        if (string.IsNullOrWhiteSpace(genreId))
            return new GenreVisualStyle(new Color(104, 112, 124), Color.White, 1);
        if (mappingKnowledgeComments && !mappingBlocks && mappingDraft?.Rows.FirstOrDefault(item => item.Key == genreId) is { } preview)
            return new GenreVisualStyle(GenreColorFromId(preview.PrimaryColor, Color.Gray),
                GenreColorFromId(preview.SecondaryColor, Color.White), GenrePatternFromId(preview.Pattern));
        var genreIds = workspace?.Project.Participants
            .Select(item => item.GenreId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? [];
        var index = Array.IndexOf(genreIds, genreId);
        if (index < 0)
            index = 0;
        var configured = workspace?.Project.GenreStyles.FirstOrDefault(item => item.GenreId == genreId);
        if (configured is not null)
            return new GenreVisualStyle(
                GenreColorFromId(configured.PrimaryColor, GenrePalette[index % GenrePalette.Length]),
                GenreColorFromId(configured.SecondaryColor, Color.White),
                GenrePatternFromId(configured.Pattern));
        return new GenreVisualStyle(GenrePalette[index % GenrePalette.Length], Color.White, index / GenrePalette.Length);
    }

    private void DrawGenreTile(ScreenRectangle bounds, string? genreId, Color border, double borderWidth)
    {
        var style = GetGenreStyle(genreId);
        DrawRectangle(bounds, style.Primary);
        DrawGenrePattern(bounds, style.Pattern, style.Secondary);
        if (borderWidth > 0d)
            DrawOutline(bounds, borderWidth, border);
    }

    private void DrawGenrePattern(ScreenRectangle bounds, int pattern, Color secondary, byte opacity = 255, bool round = false)
    {
        if (pattern <= 0)
            return;
        var type = (pattern - 1) % 7;
        var densityLevel = (pattern - 1) / 7;
        var spacing = Math.Max(4, 10 - densityLevel * 2);
        var mark = new Color(secondary.R, secondary.G, secondary.B, opacity);
        void FillPattern(ScreenRectangle rectangle, Color color)
        {
            if (round)
            {
                FillRoundToken(bounds, rectangle, color);
                return;
            }
            var left = Math.Max(bounds.X, rectangle.X);
            var top = Math.Max(bounds.Y, rectangle.Y);
            var right = Math.Min(bounds.X + bounds.Width, rectangle.X + rectangle.Width);
            var bottom = Math.Min(bounds.Y + bounds.Height, rectangle.Y + rectangle.Height);
            if (right > left && bottom > top)
                DrawRectangle(new ScreenRectangle(left, top, right - left, bottom - top), color);
        }
        if (DiagonalPattern.IsDiagonal(pattern))
        {
            for (var y = 0; y < bounds.Height; y++)
            {
                var start = -1;
                for (var x = 0; x <= Math.Ceiling(bounds.Width); x++)
                {
                    var filled = x < bounds.Width && DiagonalPattern.UsesSecondary(pattern, x, y);
                    if (filled && start < 0) start = x;
                    if (!filled && start >= 0)
                    {
                        FillPattern(new(bounds.X + start, bounds.Y + y, x - start, 1), mark);
                        start = -1;
                    }
                }
            }
            return;
        }
        if (type is 0 or 3)
        {
            for (var y = bounds.Y + spacing / 2d; y < bounds.Y + bounds.Height; y += spacing)
                FillPattern(new ScreenRectangle(bounds.X, y, bounds.Width, 1d), mark);
        }
        if (type == 4)
        {
            var bandHeight = Math.Max(2d, spacing / 2d);
            for (var y = bounds.Y; y < bounds.Y + bounds.Height; y += bandHeight * 2d)
                FillPattern(new ScreenRectangle(bounds.X, y, bounds.Width,
                    Math.Min(bandHeight, bounds.Y + bounds.Height - y)), mark);
        }
        if (type is 1 or 3)
        {
            for (var x = bounds.X + spacing / 2d; x < bounds.X + bounds.Width; x += spacing)
                FillPattern(new ScreenRectangle(x, bounds.Y, type == 3 ? 1d : 2d, bounds.Height), mark);
        }
        if (type is 2 or 5)
        {
            var row = 0;
            for (var y = bounds.Y + 3d; y < bounds.Y + bounds.Height - 1d; y += spacing, row++)
            for (var x = bounds.X + 3d + (type == 5 && row % 2 == 1 ? spacing / 2d : 0d);
                 x < bounds.X + bounds.Width - 1d; x += spacing)
                FillPattern(new ScreenRectangle(x, y, type == 2 ? spacing / 2d : 2d,
                    type == 2 ? spacing / 2d : 2d), mark);
        }
        if (type == 6)
        {
            var square = Math.Max(3, spacing / 2);
            var row = 0;
            for (var y = bounds.Y; y < bounds.Y + bounds.Height; y += square, row++)
            {
                var column = 0;
                for (var x = bounds.X; x < bounds.X + bounds.Width; x += square, column++)
                {
                    if ((row + column) % 2 != 0)
                        continue;
                    FillPattern(new ScreenRectangle(
                        x,
                        y,
                        Math.Min(square, bounds.X + bounds.Width - x),
                        Math.Min(square, bounds.Y + bounds.Height - y)), mark);
                }
            }
        }
    }

    private static Color GenreColorFromId(string id, Color fallback)
    {
        if (RgbHexColor.TryParse(id, out var red, out var green, out var blue))
            return new Color(red, green, blue);
        var index = Array.IndexOf(GenreColorIds, id);
        return index >= 0 ? GenrePalette[index] : id switch
        {
            "white" => Color.White,
            "black" => new Color(25, 28, 32),
            "gray" => new Color(128, 136, 144),
            _ => fallback,
        };
    }

    private static int GenrePatternFromId(string id) => id switch
    {
        "solid" => 0,
        "horizontal" => 1,
        "vertical" => 2,
        "checker" => 3,
        "thick-grid" => 3,
        "grid" => 4,
        "thick-horizontal" => 5,
        "uniform-horizontal" => 5,
        "dots" => 6,
        "checkerboard" => 7,
        _ => DiagonalPattern.FromId(id),
    };

    private void DrawOffscreenParticipants()
    {
        if (workspace is null || editorMode is not (EditorMode.GenrePlacement or EditorMode.CirclePlacement))
            return;
        var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var visible = new ScreenRectangle(0, ToolbarHeight, width, height - ToolbarHeight - StatusBarHeight);
        var groups = BuildParticipantTokens(workspace.GetSelectedPlanSnapshot())
            .Where(token => !token.Assigned && token.ParticipantId != draggedParticipantToken?.ParticipantId)
            .Select(token => OffscreenParticipantIndicator.FindEdge(GetCellRegionBounds(
                new GridPosition(token.DisplayCells.Min(cell => cell.X), token.DisplayCells.Min(cell => cell.Y)),
                new GridPosition(token.DisplayCells.Max(cell => cell.X), token.DisplayCells.Max(cell => cell.Y))), visible))
            .Where(edge => edge is not null).GroupBy(edge => edge!.Value);
        foreach (var group in groups)
        {
            const double margin = 14d;
            var point = group.Key switch
            {
                ScreenEdge.Left => new ScreenPoint(margin, visible.Y + visible.Height / 2d),
                ScreenEdge.Right => new ScreenPoint(width - margin, visible.Y + visible.Height / 2d),
                ScreenEdge.Top => new ScreenPoint(width / 2d, visible.Y + margin),
                _ => new ScreenPoint(width / 2d, visible.Y + visible.Height - margin),
            };
            var direction = group.Key switch
            {
                ScreenEdge.Left => new ScreenPoint(-1, 0),
                ScreenEdge.Right => new ScreenPoint(1, 0),
                ScreenEdge.Top => new ScreenPoint(0, -1),
                _ => new ScreenPoint(0, 1),
            };
            var tail = new ScreenPoint(point.X - direction.X * 18, point.Y - direction.Y * 18);
            var color = new Color(255, 198, 96);
            DrawLine(tail, point, 4d, color);
            DrawLine(point, new ScreenPoint(point.X - direction.X * 8 - direction.Y * 6, point.Y - direction.Y * 8 + direction.X * 6), 4d, color);
            DrawLine(point, new ScreenPoint(point.X - direction.X * 8 + direction.Y * 6, point.Y - direction.Y * 8 - direction.X * 6), 4d, color);
            var label = new ScreenRectangle(
                Math.Clamp(point.X - direction.X * 72 - 45, 4, width - 94),
                Math.Clamp(point.Y - direction.Y * 38 - 12, visible.Y, visible.Y + visible.Height - 24), 90, 24);
            DrawRectangle(label, new Color(24, 28, 36, 235));
            textRenderer?.Draw($"未配置 {group.Count()}件", ToRectangle(label), color, 12, true);
        }
    }

    private List<ParticipantToken> BuildParticipantTokens(PlanSnapshot snapshot)
    {
        if (workspace is null)
            return [];
        var combinedIds = BuildCombinedParticipantIds();
        var participantNumbers = workspace.Project.Participants
            .Select((participant, index) => (participant.Id, Number: index + 1))
            .ToDictionary(item => item.Id, item => item.Number);
        var tokens = snapshot.Assignments
            .Select(assignment => new ParticipantToken(
                assignment.ParticipantId,
                assignment.ScoringPosition,
                assignment.OccupiedCells.ToArray(),
                combinedIds.GetValueOrDefault(assignment.ParticipantId) ?? assignment.CombinedSpaceId,
                true,
                participantNumbers[assignment.ParticipantId]))
            .ToList();
        tokens.AddRange(workspace.SelectedPlan.TemporaryPlacements.Concat(GetStagedParticipants()).Select(item => new ParticipantToken(
            item.ParticipantId, item.ScoringPosition, item.OccupiedCells.ToArray(),
            combinedIds.GetValueOrDefault(item.ParticipantId) ?? item.CombinedSpaceId, false, participantNumbers[item.ParticipantId])));
        return tokens;
    }

    private ParticipantToken? HitTestParticipant(ScreenPoint pointer)
    {
        if (workspace is null)
            return null;
        var tokens = BuildParticipantTokens(workspace.GetSelectedPlanSnapshot());
        return tokens.LastOrDefault(token =>
            token.DisplayCells.Any(cell =>
                Contains(viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell)), pointer)));
    }

    private void DrawParticipantDragGhost()
    {
        if (draggedParticipantToken is not { } token)
            return;
        var cellSize = viewport.GetCellBounds(new GridCellAddress(0, 0)).Width;
        var cellWidth = Math.Clamp(cellSize - 12d, 16d, 34d);
        var spanWidth = token.DisplayCells.Max(cell => cell.X) - token.DisplayCells.Min(cell => cell.X) + 1;
        var spanHeight = token.DisplayCells.Max(cell => cell.Y) - token.DisplayCells.Min(cell => cell.Y) + 1;
        var width = cellWidth + Math.Max(0, spanWidth - 1) * cellSize;
        var height = cellWidth + Math.Max(0, spanHeight - 1) * cellSize;
        var bounds = new ScreenRectangle(
            participantDragPointer.X - width / 2d,
            participantDragPointer.Y - height / 2d,
            width,
            height);
        var ghostColor = editorMode == EditorMode.GenrePlacement && workspace is not null
            ? GetGenreStyle(workspace.Project.Participants.Single(item => item.Id == token.ParticipantId).GenreId).Primary
            : new Color(92, 151, 213, 205);
        DrawRectangle(bounds, CircleStoneFill(ghostColor));
        if (editorMode == EditorMode.GenrePlacement && workspace is not null)
        {
            var style = GetGenreStyle(workspace.Project.Participants.Single(item => item.Id == token.ParticipantId).GenreId);
            DrawGenrePattern(bounds, style.Pattern, style.Secondary);
        }
        DrawOutline(bounds, 3d, new Color(207, 239, 255));
        if (UseTranslucentCircleStones) DrawCircleStoneLabel(GetCircleLabel(token), bounds);
        else textRenderer?.Draw(editorMode == EditorMode.CirclePlacement ? GetCircleLabel(token) : token.Number.ToString(),
            ToRectangle(bounds, 2), Color.White, 14, true);
    }

    private Dictionary<string, string?> BuildCombinedParticipantIds()
    {
        if (workspace is null)
            return [];
        var result = workspace.Project.Plans
            .SelectMany(plan => plan.Assignments.Concat(plan.TemporaryPlacements))
            .Where(assignment => assignment.CombinedSpaceId is not null)
            .GroupBy(assignment => assignment.ParticipantId)
            .ToDictionary(group => group.Key, group => group.First().CombinedSpaceId);
        var byCircleId = workspace.Project.Participants.ToDictionary(item => item.CircleId, StringComparer.Ordinal);
        foreach (var participant in workspace.Project.Participants)
        {
            if (participant.CombinedWithCircleId is not { } partnerCircleId ||
                !byCircleId.TryGetValue(partnerCircleId, out var partner))
                continue;
            var ids = new[] { participant.Id, partner.Id }.Order(StringComparer.Ordinal).ToArray();
            var combinedId = $"imported:{ids[0]}:{ids[1]}";
            result[participant.Id] = combinedId;
            result[partner.Id] = combinedId;
        }
        return result;
    }

    private void DrawCombinedConnector(ParticipantToken first, ParticipantToken second, bool requirementsSatisfied)
    {
        var firstTokenCenter = GetParticipantTokenCenter(first);
        var secondTokenCenter = GetParticipantTokenCenter(second);
        DrawLine(firstTokenCenter, secondTokenCenter, 8d,
            requirementsSatisfied ? new Color(196, 62, 255) : new Color(255, 60, 72));
    }

    private void DrawTwoSpaceConnector(ParticipantToken token)
    {
        var cells = token.DisplayCells
            .OrderBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .ToArray();
        if (cells.Length != 2)
            return;

        DrawLine(GetCellCenter(cells[0]), GetCellCenter(cells[1]), 6d, new Color(196, 62, 255));
    }

    private static bool IsTwoSpaceSingleCircle(ParticipantToken token) =>
        token.CombinedSpaceId is null && token.DisplayCells.Count == 2;

    private ScreenPoint GetCellCenter(GridPosition cell)
    {
        var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
        return new ScreenPoint(bounds.X + bounds.Width / 2d, bounds.Y + bounds.Height / 2d);
    }

    private ScreenPoint GetParticipantTokenCenter(ParticipantToken token)
    {
        var cells = token.DisplayCells;
        var topLeft = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(
            cells.Min(cell => cell.X), cells.Min(cell => cell.Y))));
        var bottomRight = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(
            cells.Max(cell => cell.X) + 1, cells.Max(cell => cell.Y) + 1)));
        return new ScreenPoint(
            (topLeft.X + bottomRight.X) / 2d,
            (topLeft.Y + bottomRight.Y) / 2d);
    }

    private void DrawParticipantToken(ParticipantToken token)
    {
        var cells = token.DisplayCells;
        var label = GetCircleLabel(token);
        if (IsOffSeatToken(token))
        {
            var genreId = workspace?.Project.Participants.Single(item => item.Id == token.ParticipantId).GenreId;
            foreach (var cell in cells)
            {
                var cellBounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
                var hollow = IsTwoSpaceSingleCircle(token) && cell != token.Position;
                DrawOffSeatToken(new ScreenRectangle(cellBounds.X + 6, cellBounds.Y + 6,
                    cellBounds.Width - 12, cellBounds.Height - 12), genreId, hollow, hollow ? null : label);
            }
            return;
        }
        if (IsTwoSpaceSingleCircle(token))
        {
            foreach (var cell in cells)
            {
                var cellBounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
                var cellTokenBounds = new ScreenRectangle(
                    cellBounds.X + 6d, cellBounds.Y + 6d, cellBounds.Width - 12d, cellBounds.Height - 12d);
                if (cell != token.Position)
                {
                    DrawOutline(cellTokenBounds, 2d, new Color(202, 220, 239));
                    continue;
                }
                DrawRectangle(cellTokenBounds, CircleStoneFill(token.Assigned ? new Color(61, 112, 181) : new Color(94, 72, 132)));
                DrawOutline(cellTokenBounds, 2d, new Color(202, 220, 239));
                DrawCircleStoneLabel(label, cellTokenBounds);
            }
            return;
        }
        var topLeft = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(
            cells.Min(cell => cell.X), cells.Min(cell => cell.Y))));
        var bottomRight = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(
            cells.Max(cell => cell.X) + 1, cells.Max(cell => cell.Y) + 1)));
        var bounds = new ScreenRectangle(
            topLeft.X + 6d,
            topLeft.Y + 6d,
            bottomRight.X - topLeft.X - 12d,
            bottomRight.Y - topLeft.Y - 12d);
        DrawRectangle(bounds, CircleStoneFill(token.Assigned ? new Color(61, 112, 181) : new Color(94, 72, 132)));
        DrawOutline(bounds, 2d, token.CombinedSpaceId is null ? new Color(202, 220, 239) : new Color(228, 191, 255));
        DrawCircleStoneLabel(label, bounds);
    }

    private string GetCircleLabel(ParticipantToken token)
    {
        var participant = workspace?.Project.Participants.SingleOrDefault(item => item.Id == token.ParticipantId);
        if (participant is null)
            return token.Number.ToString();

        return CircleLabelFormatter.Format(participant, token.Number, CurrentCircleDisplay);
    }

    private bool FitVenueToEditorCanvas()
    {
        if (workspace is null)
            return false;

        const double margin = 24d;
        const double rightPanelWidth = 288d;
        var venue = workspace.Project.Venue;
        var availableWidth = GraphicsDevice.PresentationParameters.BackBufferWidth - rightPanelWidth - margin * 2d;
        var availableHeight = GraphicsDevice.PresentationParameters.BackBufferHeight - ToolbarHeight - StatusBarHeight - margin * 2d;
        if (availableWidth <= 0d || availableHeight <= 0d)
            return false;

        var zoom = Math.Clamp(
            Math.Min(
                availableWidth / (venue.Width * viewport.BaseCellSize),
                availableHeight / (venue.Height * viewport.BaseCellSize)),
            GridViewport.MinimumZoom,
            GridViewport.MaximumZoom);
        var cellSize = viewport.BaseCellSize * zoom;
        viewport.SetView(
            zoom,
            new ScreenPoint(
                margin + (availableWidth - venue.Width * cellSize) / 2d,
                ToolbarHeight + margin + (availableHeight - venue.Height * cellSize) / 2d));
        return true;
    }

    private void DrawCell(GridCellAddress cell, Color color)
    {
        var bounds = viewport.GetCellBounds(cell);
        DrawRectangle(new ScreenRectangle(bounds.X + 2d, bounds.Y + 2d, bounds.Width - 4d, bounds.Height - 4d), color);
    }

    private void DrawInsetCell(GridCellAddress cell, double inset, Color color)
    {
        var bounds = viewport.GetCellBounds(cell);
        DrawRectangle(new ScreenRectangle(
            bounds.X + inset,
            bounds.Y + inset,
            bounds.Width - inset * 2d,
            bounds.Height - inset * 2d), color);
    }

    private void CreateToolbar()
    {
        toolbarButtons.Clear();
        toolbarSeparators.Clear();
        var modeActions = new[]
        {
            ToolbarAction.ParticipantDataMode,
            ToolbarAction.DeskPlacementMode,
            ToolbarAction.IslandDefinitionMode,
            ToolbarAction.GenrePlacementMode,
            ToolbarAction.CirclePlacementMode,
            ToolbarAction.CirclePlacementDecisionMode,
        };
        var commonStart = new[]
        {
            ToolbarAction.PanViewport,
            ToolbarAction.FitVenueToWindow,
            ToolbarAction.Undo,
            ToolbarAction.Redo,
        };
        var deskActions = new[]
        {
            ToolbarAction.EditDeskLayoutDescription,
            ToolbarAction.MoveDesk,
            ToolbarAction.DeskMenu,
            ToolbarAction.EditSeatName,
            ToolbarAction.SwapAddresses,
            ToolbarAction.PillarMenu,
            ToolbarAction.FillDesks,
            ToolbarAction.RotateLeft,
            ToolbarAction.RotateRight,
            ToolbarAction.VenueSizeMenu,
        };
        var circleActions = new[]
        {
            ToolbarAction.ShowEvaluationBreakdown,
            ToolbarAction.ToggleCircleStoneTransparency,
            ToolbarAction.OptimizeCirclePlacement,
            ToolbarAction.AssignParticipant,
            ToolbarAction.UnassignParticipant,
            ToolbarAction.ToggleEvaluationAnalysis,
        };
        var genreActions = new[]
        {
            ToolbarAction.ShowEvaluationBreakdown,
            ToolbarAction.FillVacantSeats,
            ToolbarAction.EditGenreStyles,
            ToolbarAction.ToggleEvaluationAnalysis,
        };
        var islandActions = new[]
        {
            ToolbarAction.AddIslandStart,
            ToolbarAction.RemoveIslandStart,
            ToolbarAction.AddIslandConnector,
            ToolbarAction.AddFacingRegion,
            ToolbarAction.ToggleAutomaticIslandConnection,
            ToolbarAction.RemoveTopology,
        };
        var commonEnd = new[]
        {
            ToolbarAction.CaptureScreenshot,
        };
        var visibleModeCount = modeActions.Length - (frameModesCollapsed ? 2 : 0) - (circleModesCollapsed ? 2 : 0);
        var toolbarWidth = Window.ClientBounds.Width > 0 ? Window.ClientBounds.Width : graphics.PreferredBackBufferWidth;
        projectToolbarWidth = toolbarWidth;
        var modeWidth = Math.Min(158d, Math.Max(44d, (toolbarWidth - 492d) / visibleModeCount));
        toolbarButtons.Add(new ToolbarButton(ToolbarAction.ProjectMenu,
            new IconButtonModel(new ScreenRectangle(12, 7 + WorkerBarHeight, 142, 40), GetAccessibleName(ToolbarAction.ProjectMenu))));
        toolbarButtons.Add(new ToolbarButton(ToolbarAction.GenreMenu,
            new IconButtonModel(new ScreenRectangle(164, 7 + WorkerBarHeight, 120, 40), GetAccessibleName(ToolbarAction.GenreMenu))));
        var modeX = 294d;
        for (var index = 0; index < modeActions.Length; index++)
        {
            var action = modeActions[index];
            if (action is ToolbarAction.DeskPlacementMode or ToolbarAction.GenrePlacementMode)
            {
                var frameGroup = action == ToolbarAction.DeskPlacementMode;
                var collapsed = frameGroup ? frameModesCollapsed : circleModesCollapsed;
                var groupName = frameGroup ? "フレーム配置・島定義" : "ジャンル配置・サークル配置";
                toolbarButtons.Add(new ToolbarButton(
                    frameGroup ? ToolbarAction.ToggleFrameModes : ToolbarAction.ToggleCircleModes,
                    new IconButtonModel(new ScreenRectangle(modeX, 7d + WorkerBarHeight, 24d, 24d),
                        $"{groupName}を{(collapsed ? "展開する" : "収納する")}")));
                modeX += 30d;
            }
            if (frameModesCollapsed && action is (ToolbarAction.DeskPlacementMode or ToolbarAction.IslandDefinitionMode) ||
                circleModesCollapsed && action is (ToolbarAction.GenrePlacementMode or ToolbarAction.CirclePlacementMode))
                continue;
            toolbarButtons.Add(new ToolbarButton(
                action,
                new IconButtonModel(new ScreenRectangle(modeX, 7d + WorkerBarHeight, modeWidth - 8d, 40d), GetAccessibleName(action))));
            modeX += modeWidth;
        }
        var modeSpecificActions = editorMode switch
        {
            EditorMode.ParticipantData => [ToolbarAction.ImportParticipants, ToolbarAction.Undo, ToolbarAction.Redo],
            EditorMode.CirclePlacementDecision => [ToolbarAction.SelectExportTarget, ToolbarAction.SelectExportColumns, ToolbarAction.ExportSeatAssignments, ToolbarAction.Undo, ToolbarAction.Redo],
            EditorMode.DeskPlacement => deskActions,
            EditorMode.IslandDefinition => islandActions,
            EditorMode.GenrePlacement => genreActions,
            EditorMode.CirclePlacement => circleActions,
            EditorMode.GenreData => [],
            _ => [],
        };
        var actions = (editorMode is EditorMode.ParticipantData or EditorMode.CirclePlacementDecision or EditorMode.SpaceDefinitions ? Array.Empty<ToolbarAction>() : commonStart)
            .Concat(modeSpecificActions)
            .Concat(editorMode == EditorMode.SpaceDefinitions ? [ToolbarAction.CaptureScreenshot] : commonEnd)
            .OrderBy(GetToolbarActionGroup)
            .ToArray();
        var actionX = 12d;
        for (var index = 0; index < actions.Length; index++)
        {
            var action = actions[index];
            if (index > 0 && GetToolbarActionGroup(action) != GetToolbarActionGroup(actions[index - 1]))
            {
                toolbarSeparators.Add(actionX + 4d);
                actionX += 14d;
            }
            var buttonWidth = action is ToolbarAction.EditDeskLayoutDescription or ToolbarAction.ToggleCircleStoneTransparency or ToolbarAction.ExportFrameLayout or ToolbarAction.ImportFrameLayout ? 134d : action is ToolbarAction.DeskMenu or ToolbarAction.PillarMenu or ToolbarAction.VenueSizeMenu ? 44d : action == ToolbarAction.SelectExportPlan ? 210d : action is ToolbarAction.ImportParticipants or ToolbarAction.SelectExportTarget or ToolbarAction.SelectExportColumns or ToolbarAction.ExportSeatAssignments ? 170d :
                editorMode == EditorMode.DeskPlacement ? 42d : 44d;
            toolbarButtons.Add(new ToolbarButton(
                action,
                new IconButtonModel(new ScreenRectangle(actionX, 59d + WorkerBarHeight, buttonWidth, 44d), GetAccessibleName(action))));
            actionX += buttonWidth + 5d;
        }
        if (editorMode == EditorMode.CirclePlacementDecision)
            toolbarButtons.Add(new ToolbarButton(ToolbarAction.SelectExportPlan,
                new IconButtonModel(new ScreenRectangle(192, 184 + WorkerBarHeight, 84, 32), GetAccessibleName(ToolbarAction.SelectExportPlan))));
    }

    // Canvas tools share activeCanvasTool; the analysis toggle has independent state.
    // Keep each state group together before commands that run once per click.
    private static int GetToolbarActionGroup(ToolbarAction action) => action switch
    {
        ToolbarAction.PillarMenu or ToolbarAction.DeskMenu or ToolbarAction.PanViewport or ToolbarAction.MoveDesk or ToolbarAction.AddDesk or
        ToolbarAction.RemoveDesk or ToolbarAction.EditSeatName or ToolbarAction.SwapAddresses or ToolbarAction.EditDeskNumber or
        ToolbarAction.AddPillar or ToolbarAction.RemovePillar or ToolbarAction.RotateLeft or ToolbarAction.RotateRight or
        ToolbarAction.AssignParticipant or ToolbarAction.UnassignParticipant or ToolbarAction.AddIslandConnector or
        ToolbarAction.AddIslandStart or ToolbarAction.RemoveIslandStart or ToolbarAction.AddFacingRegion or ToolbarAction.ToggleAutomaticIslandConnection or ToolbarAction.RemoveTopology => 0,
        ToolbarAction.ToggleEvaluationAnalysis or ToolbarAction.ToggleCircleStoneTransparency => 1,
        _ => 2,
    };

    private void UpdateToolbar(ScreenPoint pointer)
    {
        if (editorMode == EditorMode.CirclePlacementDecision) RefreshExportPreview();
        foreach (var button in toolbarButtons)
        {
            button.Model.IsEnabled = button.Action switch
            {
                ToolbarAction.ToggleCircleStoneTransparency => ShowCircleHeatmap,
                ToolbarAction.FillVacantSeats => workspace?.HasSelectedCircleLayout == true && optimizationTask is null && backgroundOperation is null,
                ToolbarAction.ToggleFrameModes or ToolbarAction.ToggleCircleModes => true,
                ToolbarAction.ProjectMenu or ToolbarAction.GenreMenu => workspace is not null && optimizationTask is null,
                ToolbarAction.SpaceDefinitionsMode => true,
                ToolbarAction.DeskMenu or ToolbarAction.PillarMenu or ToolbarAction.VenueSizeMenu => workspace is not null,
                ToolbarAction.PreviousPlan or ToolbarAction.NextPlan => GetDisplayedPlans().Count > 1,
                ToolbarAction.Undo => workspace?.CanUndo == true,
                ToolbarAction.Redo => workspace?.CanRedo == true,
                ToolbarAction.DuplicatePlan => workspace?.HasSelectedCircleLayout == true,
                ToolbarAction.ParticipantDataMode or ToolbarAction.DeskPlacementMode or ToolbarAction.IslandDefinitionMode or ToolbarAction.GenrePlacementMode or ToolbarAction.CirclePlacementMode or ToolbarAction.GenreDataMode or ToolbarAction.CirclePlacementDecisionMode => workspace is not null,
                ToolbarAction.PanViewport or ToolbarAction.FitVenueToWindow => workspace is not null,
                ToolbarAction.EditDeskLayoutDescription or ToolbarAction.ImportParticipants or ToolbarAction.ExportFrameLayout or ToolbarAction.ImportFrameLayout => workspace is not null && optimizationTask is null,
                ToolbarAction.SelectExportPlan or ToolbarAction.SelectExportTarget => workspace is not null,
                ToolbarAction.SelectExportColumns => workspace is not null && exportTargetSheet is not null,
                ToolbarAction.ExportSeatAssignments => workspace is not null && exportColumnsConfirmed && preparedExport is not null,
                ToolbarAction.OptimizeCirclePlacement => workspace?.HasSelectedCircleLayout == true,
                ToolbarAction.EditGenreStyles => workspace is not null && workspace.Project.Participants.Any(item => !string.IsNullOrWhiteSpace(item.GenreId)),
                ToolbarAction.ToggleEvaluationAnalysis => workspace is not null,
                ToolbarAction.ShowEvaluationBreakdown => workspace?.HasSelectedCircleLayout == true,
                ToolbarAction.SwapAddresses => workspace is not null && ShowsAddressSwapButton,
                ToolbarAction.AssignParticipant => workspace?.HasSelectedCircleLayout == true && participantController?.SelectedParticipantId is not null,
                ToolbarAction.MoveDesk or ToolbarAction.AddDesk or ToolbarAction.RemoveDesk or ToolbarAction.EditSeatName or ToolbarAction.EditDeskNumber or ToolbarAction.AddPillar or ToolbarAction.RemovePillar or ToolbarAction.FillDesks or
                ToolbarAction.RotateLeft or ToolbarAction.RotateRight or ToolbarAction.UnassignParticipant or
                ToolbarAction.AddIslandConnector or ToolbarAction.AddIslandStart or ToolbarAction.RemoveIslandStart or ToolbarAction.AddFacingRegion or ToolbarAction.ToggleAutomaticIslandConnection or ToolbarAction.RemoveTopology or
                ToolbarAction.DecreaseWidth or ToolbarAction.IncreaseWidth or ToolbarAction.DecreaseHeight or ToolbarAction.IncreaseHeight => workspace is not null,
                ToolbarAction.CaptureScreenshot => true,
                ToolbarAction.ReturnToEventList => workspace is not null,
                ToolbarAction.SaveProject => workspace is not null && projectSavePath is not null,
                _ => false,
            };
            button.Model.IsSelected = button.Action == activeCanvasTool && !(button.Action == ToolbarAction.EditSeatName && IsAddressSwapMode) ||
                button.Action == ToolbarAction.ToggleCircleStoneTransparency && UseTranslucentCircleStones ||
                button.Action == ToolbarAction.SwapAddresses && IsAddressSwapMode ||
                button.Action == ToolbarAction.SpaceDefinitionsMode && editorMode == EditorMode.SpaceDefinitions ||
                button.Action == GetToolMenu(activeCanvasTool) ||
                button.Action == ToolbarAction.ParticipantDataMode && editorMode == EditorMode.ParticipantData ||
                button.Action == ToolbarAction.ToggleEvaluationAnalysis && showEvaluationAnalysis ||
                button.Action == ToolbarAction.DeskPlacementMode && editorMode == EditorMode.DeskPlacement ||
                button.Action == ToolbarAction.IslandDefinitionMode && editorMode == EditorMode.IslandDefinition ||
                button.Action == ToolbarAction.GenrePlacementMode && editorMode == EditorMode.GenrePlacement ||
                button.Action == ToolbarAction.CirclePlacementMode && editorMode == EditorMode.CirclePlacement ||
                button.Action == ToolbarAction.GenreDataMode && editorMode == EditorMode.GenreData ||
                button.Action == ToolbarAction.CirclePlacementDecisionMode && editorMode == EditorMode.CirclePlacementDecision;
            button.Model.UpdatePointer(pointer);
        }
        var activeButton = toolbarButtons.SingleOrDefault(button => button.Action ==
            GetToolMenu(activeCanvasTool));
        if (activeButton?.Model.IsEnabled != true)
            activeCanvasTool = editorMode == EditorMode.DeskPlacement
                ? ToolbarAction.MoveDesk
                : editorMode is EditorMode.GenrePlacement or EditorMode.IslandDefinition ? ToolbarAction.PanViewport : ToolbarAction.UnassignParticipant;
    }

    private (bool Success, string Detail) ExecuteToolbarAction(ToolbarAction action, ScreenPoint pointer)
    {
        if (action == ToolbarAction.ProjectMenu)
        {
            OpenProjectMenu();
            return (true, "project_menu_opened");
        }
        if (action == ToolbarAction.ToggleCircleStoneTransparency)
        {
            if (!ShowCircleHeatmap) return (false, "no_numeric_channel");
            translucentCircleStones = !translucentCircleStones;
            return (true, $"translucent={translucentCircleStones}");
        }
        if (action == ToolbarAction.FillVacantSeats)
        {
            FillVacantSeats();
            return (true, "fill_vacant_seats");
        }
        if (action is ToolbarAction.ToggleFrameModes or ToolbarAction.ToggleCircleModes)
        {
            if (action == ToolbarAction.ToggleFrameModes) frameModesCollapsed = !frameModesCollapsed;
            else circleModesCollapsed = !circleModesCollapsed;
            CreateToolbar();
            UpdateToolbar(pointer);
            return (true, "mode_group_toggled");
        }
        if (action is ToolbarAction.FaceNorth or ToolbarAction.FaceEast or ToolbarAction.FaceSouth or ToolbarAction.FaceWest)
        {
            nextDeskOrientation = action switch
            {
                ToolbarAction.FaceNorth => QuarterTurn.North,
                ToolbarAction.FaceEast => QuarterTurn.East,
                ToolbarAction.FaceSouth => QuarterTurn.South,
                _ => QuarterTurn.West,
            };
            return (true, $"next_orientation={nextDeskOrientation}");
        }
        if (action == ToolbarAction.EditDeskLayoutDescription)
        {
            EditDeskLayoutDescription();
            return (true, "desk_layout_description");
        }
        if (action == ToolbarAction.ExportFrameLayout)
        {
            ExportPortable();
            return (true, "frame_layout_export");
        }
        if (action == ToolbarAction.ImportFrameLayout)
        {
            ImportPortable();
            return (true, "frame_layout_import");
        }
        if (action == ToolbarAction.SpaceDefinitionsMode)
        {
            OpenSpaceDefinitions();
            return (spaceCatalogOpen, "space_catalog");
        }
        if (ToolRings.Any(ring => ring.Menu == action))
        {
            OpenToolRing(action);
            return (true, "tool_menu_opened");
        }
        if (action == ToolbarAction.CaptureScreenshot)
        {
            RequestScreenshot();
            return (true, "capture_requested");
        }
        if (action == ToolbarAction.SaveProject)
            return SaveProject();
        if (action == ToolbarAction.ReturnToEventList)
            return ReturnToEventList();
        if (commandController is null || workspace is null)
            return (false, "no_project");
        if (action == ToolbarAction.CirclePlacementDecisionMode)
            return ChangeEditorMode(EditorMode.CirclePlacementDecision);
        if (action == ToolbarAction.SelectExportColumns)
        {
            OpenExportColumns();
            return (true, "dialog_opened");
        }
        if (action == ToolbarAction.SelectExportTarget)
        {
            OpenExportTarget();
            return (true, "dialog_opened");
        }
        if (action == ToolbarAction.ParticipantDataMode)
            return ChangeEditorMode(EditorMode.ParticipantData);
        if (action == ToolbarAction.DeskPlacementMode)
            return ChangeEditorMode(EditorMode.DeskPlacement);
        if (action == ToolbarAction.IslandDefinitionMode)
            return ChangeEditorMode(EditorMode.IslandDefinition);
        if (action == ToolbarAction.GenrePlacementMode)
            return ChangeEditorMode(EditorMode.GenrePlacement);
        if (action == ToolbarAction.CirclePlacementMode)
            return ChangeEditorMode(EditorMode.CirclePlacement);
        if (action == ToolbarAction.GenreMenu || action == ToolbarAction.GenreDataMode)
        {
            OpenGenreMenu();
            return (true, "dialog_opened");
        }
        if (action == ToolbarAction.ImportParticipants)
        {
            OpenParticipantImport();
            return (true, "dialog_opened");
        }
        if (action == ToolbarAction.SelectExportPlan)
        {
            OpenExportPlanSelection();
            return (true, "dialog_opened");
        }
        if (action == ToolbarAction.ExportSeatAssignments)
        {
            try
            {
                OpenSeatExport();
                return (true, "dialog_opened");
            }
            catch (InvalidOperationException exception)
            {
                ShowInAppMessage("Excelへ書き出し", exception.Message);
                return (false, "export_validation_failed");
            }
        }
        if (action == ToolbarAction.OptimizeCirclePlacement)
        {
            OpenOptimizationSettings();
            return (true, "dialog_opened");
        }
        if (action == ToolbarAction.EditGenreStyles)
        {
            OpenGenreStyleEditor();
            return (true, "genre_styles");
        }
        if (action == ToolbarAction.SwapAddresses)
        {
            ToggleAddressSwapMode();
            return (true, "address_swap_mode");
        }
        if (action == ToolbarAction.ShowEvaluationBreakdown)
        {
            OpenEvaluationBreakdown();
            return (true, "evaluation_breakdown");
        }
        if (action == ToolbarAction.ToggleEvaluationAnalysis)
        {
            showEvaluationAnalysis = !showEvaluationAnalysis;
            return (true, $"analysis={(showEvaluationAnalysis ? "on" : "off")}");
        }
        if (activeCanvasTool == ToolbarAction.AddDesk &&
            action is ToolbarAction.RotateLeft or ToolbarAction.RotateRight)
        {
            RotateNextDesk(clockwise: action == ToolbarAction.RotateRight);
            return (true, $"ghost_rotated;orientation={nextDeskOrientation}");
        }
        return action switch
        {
            ToolbarAction.PreviousPlan => CyclePlan(-1),
            ToolbarAction.NextPlan => CyclePlan(1),
            ToolbarAction.Undo => (commandController.Undo(), "command=undo"),
            ToolbarAction.Redo => (commandController.Redo(), "command=redo"),
            ToolbarAction.DuplicatePlan => PromptDuplicateSelectedPlan(),
            ToolbarAction.FitVenueToWindow => (FitVenueToEditorCanvas(), "venue_fitted"),
            ToolbarAction.FillDesks => FormatOutcome(commandController.FillDesks()),
            ToolbarAction.DecreaseWidth => FormatOutcome(commandController.ResizeVenue(-1, 0)),
            ToolbarAction.IncreaseWidth => FormatOutcome(commandController.ResizeVenue(1, 0)),
            ToolbarAction.DecreaseHeight => FormatOutcome(commandController.ResizeVenue(0, -1)),
            ToolbarAction.IncreaseHeight => FormatOutcome(commandController.ResizeVenue(0, 1)),
            ToolbarAction.ExpandTop => FormatOutcome(commandController.ResizeVenue(0, 1, 0, 1)),
            ToolbarAction.ShrinkTop => FormatOutcome(commandController.ResizeVenue(0, -1, 0, -1)),
            ToolbarAction.ExpandLeft => FormatOutcome(commandController.ResizeVenue(1, 0, 1, 0)),
            ToolbarAction.ShrinkLeft => FormatOutcome(commandController.ResizeVenue(-1, 0, -1, 0)),
            ToolbarAction.PanViewport or ToolbarAction.MoveDesk or ToolbarAction.AddDesk or ToolbarAction.RemoveDesk or ToolbarAction.EditSeatName or ToolbarAction.EditDeskNumber or ToolbarAction.AddPillar or ToolbarAction.RemovePillar or ToolbarAction.RotateLeft or ToolbarAction.RotateRight or
            ToolbarAction.AssignParticipant or ToolbarAction.UnassignParticipant or ToolbarAction.AddIslandConnector or
            ToolbarAction.AddIslandStart or ToolbarAction.RemoveIslandStart or ToolbarAction.AddFacingRegion or ToolbarAction.ToggleAutomaticIslandConnection or ToolbarAction.RemoveTopology => SelectCanvasTool(action),
            _ => (false, "unavailable"),
        };

        (bool Success, string Detail) CyclePlan(int direction)
        {
            CycleDisplayedPlan(direction);
            return (true, $"direction={direction}");
        }

        (bool Success, string Detail) SelectCanvasTool(ToolbarAction tool)
        {
            addressSwapEnabled = false;
            activeCanvasTool = tool;
            topologyFirstDeskId = null;
            topologyFirstCell = null;
            topologyFirstCorner = null;
            return (true, "tool_selected");
        }

        (bool Success, string Detail) ChangeEditorMode(EditorMode mode)
        {
            addressSwapEnabled = false;
            addressSwapChannel = -1;
            addressSwapProject = null;
            editorMode = mode;
            tableTextPage = null;
            activeCanvasTool = mode == EditorMode.DeskPlacement
                ? ToolbarAction.MoveDesk
                : mode is EditorMode.GenrePlacement or EditorMode.IslandDefinition or EditorMode.GenreData ? ToolbarAction.PanViewport : ToolbarAction.UnassignParticipant;
            topologyFirstDeskId = null;
            topologyFirstCell = null;
            topologyFirstCorner = null;
            rangeSelectionAnchor = null;
            rangeSelectionCurrent = null;
            selectedCellRange = null;
            selectedFrameIds.Clear();
            frameSelectionStart = null;
            draggedCellRange = null;
            pressedToolbarButton = null;
            CreateToolbar();
            return (true, $"mode={mode}");
        }

    }

    private static (bool Success, string Detail) FormatOutcome(EditorCommandResult result) =>
        result.Applied
            ? (true, "issues=none")
            : result.Issues.Count == 0
                ? (false, "target=none")
                : (false, $"issues={FormatIssues(result.Issues)}");

    private bool IsSeatNameRangeEditing =>
        editorMode == EditorMode.DeskPlacement && activeCanvasTool == ToolbarAction.EditSeatName &&
        (IsWeightChannelSelected || selectedNumberChannel != 1);

    private bool CanSelectCellRange =>
        editorMode is EditorMode.DeskPlacement or EditorMode.GenrePlacement or EditorMode.CirclePlacement;

    private (bool Success, string Detail) ExecuteCanvasTool(ScreenPoint pointer)
    {
        if (commandController is null || participantController is null)
            return (false, "no_project");
        var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
        var result = activeCanvasTool switch
        {
            ToolbarAction.RotateLeft => commandController.RotateDeskAt(cell, clockwise: false),
            ToolbarAction.RotateRight => commandController.RotateDeskAt(cell, clockwise: true),
            ToolbarAction.AddDesk => NextSpaceType is { } type ? commandController.AddDeskAt(cell, nextDeskOrientation, type) : EditorCommandResult.NoTarget,
            ToolbarAction.RemoveDesk => RemoveFrameAt(cell),
            ToolbarAction.EditSeatName => selectedNumberChannel == 1 ? EditDeskNumberAt(cell) : EditSeatNameAt(cell),
            ToolbarAction.EditDeskNumber => EditDeskNumberAt(cell),
            ToolbarAction.AddPillar => commandController.AddPillarAt(cell),
            ToolbarAction.RemovePillar => commandController.RemovePillarAt(cell),
            ToolbarAction.AssignParticipant => participantController.AssignSelectedAt(cell),
            ToolbarAction.UnassignParticipant => participantController.UnassignAt(cell),
            ToolbarAction.AddIslandStart => commandController.SetIslandStart(cell),
            ToolbarAction.RemoveIslandStart => commandController.SetIslandStart(cell, remove: true),
            ToolbarAction.AddIslandConnector => AddIslandConnectorAt(cell),
            ToolbarAction.AddFacingRegion => AddFacingRegionAt(cell),
            ToolbarAction.RemoveTopology => RemoveTopologyAt(pointer, cell),
            _ => EditorCommandResult.NoTarget,
        };
        RememberAffectedOrientation(result);
        return result.Applied
            ? (true, "issues=none")
            : result.Issues.Count == 0
                ? (false, "target=none")
                : (false, $"issues={FormatIssues(result.Issues)}");

        EditorCommandResult RemoveFrameAt(GridPosition position)
        {
            var removal = commandController.RemoveDeskAt(position);
            if (removal.Applied)
            {
                selectedCellRange = null;
                selectedFrameIds.Clear();
                rangeSwapStatus = "フレーム本体を削除しました。Ctrl＋Zで元に戻せます。";
            }
            else if (removal.Issues.Count > 0)
                ShowInAppMessage("フレームを削除できません", string.Join("\n", removal.Issues.Select(issue => issue.Message)));
            return removal;
        }

        EditorCommandResult AddIslandConnectorAt(GridPosition position)
        {
            var types = workspace!.Project.DeskTypes.ToDictionary(type => type.Id);
            var desk = workspace.SelectedPlan.DeskPlacements.LastOrDefault(item => item.GetSeatCells(types[item.DeskTypeId]).Contains(position));
            if (desk is null) return EditorCommandResult.NoTarget;
            if (topologyFirstDeskId is null)
            {
                topologyFirstDeskId = desk.Id;
                topologyFirstCell = position;
                return EditorCommandResult.Success;
            }
            var first = topologyFirstDeskId;
            var firstCell = topologyFirstCell!.Value;
            topologyFirstDeskId = null;
            topologyFirstCell = null;
            return commandController.AddIslandConnector(first, desk.Id, firstCell, position);
        }

        EditorCommandResult EditSeatNameAt(GridPosition position) => EditNumberCells(cell => cell == position);

        EditorCommandResult EditDeskNumberAt(GridPosition position)
        {
            var desk = workspace!.GetSelectedPlanSnapshot().Desks.LastOrDefault(item => item.OccupiedCells.Contains(position));
            if (desk is null || !IsSpaceNumberTarget(desk.Id, position))
                return EditorCommandResult.NoTarget;
            var placement = workspace.SelectedPlan.DeskPlacements.Single(item => item.Id == desk.Id);
            var targets = IsFrameNumberChannelSelected && selectedFrameIds.Contains(desk.Id)
                ? workspace.SelectedPlan.DeskPlacements.Where(item => selectedFrameIds.Contains(item.Id)).ToArray()
                : [placement];
            var values = targets.Select(item => item.DeskNumber).Distinct().ToArray();
            var editingWorkspace = workspace;
            var planId = workspace.SelectedPlanId;
            var ids = targets.Select(item => item.Id).ToArray();
            OpenNumberInput(targets.Length > 1 ? $"フレーム番号（{targets.Length} フレーム）" : "フレーム番号",
                values.Length == 1 ? values[0] : "", values.Length > 1, value =>
                {
                    if (workspace != editingWorkspace || workspace.SelectedPlanId != planId)
                        throw new InvalidOperationException("編集対象が変わりました。選び直してください。");
                    var result = commandController.SetDeskNumbers(ids, string.IsNullOrEmpty(value) ? null : value);
                    Log("frame_number_edit", result.Applied);
                    if (!result.Applied) ShowInAppMessage("番号入力", "番号を変更できませんでした。");
                });
            return EditorCommandResult.Success;
        }

        EditorCommandResult RemoveTopologyAt(ScreenPoint point, GridPosition position)
        {
            var snapshot = workspace!.GetSelectedPlanSnapshot();
            var desks = snapshot.Desks.ToDictionary(item => item.Id, StringComparer.Ordinal);
            var connector = workspace.SelectedPlan.IslandConnectors
                .Select(item => (Connector: item, Distance: ConnectorDistanceSquared(item, desks, point)))
                .Where(item => item.Distance <= 81d)
                .OrderBy(item => item.Distance)
                .Select(item => item.Connector)
                .FirstOrDefault();
            return connector is null
                ? commandController.RemoveTopologyAt(position)
                : commandController.RemoveIslandConnector(connector.Id);
        }

        EditorCommandResult AddFacingRegionAt(GridPosition position)
        {
            if (topologyFirstCorner is null)
            {
                topologyFirstCorner = position;
                return EditorCommandResult.Success;
            }
            var first = topologyFirstCorner.Value;
            topologyFirstCorner = null;
            return commandController.AddFacingRegion(first, position);
        }

        double ConnectorDistanceSquared(IslandConnector connector, IReadOnlyDictionary<string, DeskView> desks, ScreenPoint point)
        {
            var types = workspace!.Project.DeskTypes.ToDictionary(type => type.Id);
            if (IslandConnectorEndpoints.Resolve(workspace.SelectedPlan, types, connector, workspace.View.AutomaticEdges) is not { } endpoints)
                return double.PositiveInfinity;
            var firstPoint = GetCellCenter(endpoints.FirstCell);
            var secondPoint = GetCellCenter(endpoints.SecondCell);
            return DistanceSquaredToSegment(point, firstPoint, secondPoint);
        }
    }

    private void RememberAffectedOrientation(EditorCommandResult result)
    {
        if (result.Applied && result.AffectedOrientation is { } orientation)
            nextDeskOrientation = orientation;
    }

    private void RotateNextDesk(bool clockwise)
    {
        var delta = clockwise ? 1 : 3;
        nextDeskOrientation = (QuarterTurn)(((int)nextDeskOrientation + delta) % 4);
        KeepRememberedGhostBelowToolbar();
    }

    private void KeepRememberedGhostBelowToolbar()
    {
        if (lastDeskGhostPointer is not { } pointer)
            return;

        for (var attempt = 0; attempt < 4 && !IsDeskGhostClearOfToolbar(pointer); attempt++)
        {
            var cell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
            var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
            pointer = new ScreenPoint(pointer.X, pointer.Y + bounds.Height);
        }
        lastDeskGhostPointer = pointer;
    }

    private void DrawToolbar()
    {
        DrawRectangle(new ScreenRectangle(0d, 0d, GraphicsDevice.Viewport.Width, ToolbarHeight), new Color(18, 22, 28));
        foreach (var separatorX in toolbarSeparators)
            DrawRectangle(new ScreenRectangle(separatorX, 65d + WorkerBarHeight, 1d, 32d), new Color(80, 87, 98));
        foreach (var button in toolbarButtons)
        {
            if (button.Action is ToolbarAction.ToggleFrameModes or ToolbarAction.ToggleCircleModes)
            {
                DrawModeGroupToggle(button);
                continue;
            }
            OperationButtonRenderer.Draw(button.Model,
                (bounds, color) => DrawRectangle(bounds, ToButtonColor(color)),
                (bounds, thickness, color) => DrawOutline(bounds, thickness, ToButtonColor(color)),
                (bounds, color) =>
                {
                    var foreground = ToButtonColor(color);
                    if (button.Action == ToolbarAction.ProjectMenu)
                        textRenderer?.Draw("プロジェクト ▼", ToRectangle(bounds, 5), foreground, 17, true);
                    else if (button.Action == ToolbarAction.GenreMenu)
                        textRenderer?.Draw("ジャンル", ToRectangle(bounds, 5), foreground, 17, true);
                    else if (button.Action is ToolbarAction.SpaceDefinitionsMode or ToolbarAction.ParticipantDataMode or ToolbarAction.DeskPlacementMode or ToolbarAction.IslandDefinitionMode or ToolbarAction.GenrePlacementMode or ToolbarAction.CirclePlacementMode or ToolbarAction.GenreDataMode or ToolbarAction.CirclePlacementDecisionMode)
                        textRenderer?.Draw(GetModeLabel(button.Action), ToRectangle(bounds, 5), foreground, 17, true);
                    else if (button.Action is ToolbarAction.EditDeskLayoutDescription or ToolbarAction.ToggleCircleStoneTransparency or ToolbarAction.ImportFrameLayout or ToolbarAction.ExportFrameLayout or ToolbarAction.ImportParticipants or ToolbarAction.SelectExportPlan or ToolbarAction.SelectExportTarget or ToolbarAction.SelectExportColumns or ToolbarAction.ExportSeatAssignments)
                        textRenderer?.Draw(button.Action switch
                        {
                            ToolbarAction.ToggleCircleStoneTransparency => "石を半透明",
                            ToolbarAction.EditDeskLayoutDescription => "配置案の説明",
                            ToolbarAction.ExportFrameLayout => "部分書出し",
                            ToolbarAction.ImportFrameLayout => "部分読込み",
                            ToolbarAction.ImportParticipants => "Excel / CSV 読込",
                            ToolbarAction.SelectExportTarget => "出力先",
                            ToolbarAction.SelectExportColumns => "出力列",
                            ToolbarAction.SelectExportPlan => "変更",
                            _ => "書き出す",
                        }, ToRectangle(bounds, 5), foreground, 16, true);
                    else if (button.Action is ToolbarAction.DeskMenu or ToolbarAction.PillarMenu)
                        DrawToolbarIcon(GetToolMenu(activeCanvasTool) == button.Action
                            ? activeCanvasTool : button.Action, bounds, foreground);
                    else
                        DrawToolbarIcon(button.Action, bounds, foreground);
                    if (ToolRings.Any(ring => ring.Menu == button.Action))
                        DrawCircle(new ScreenPoint(bounds.X + bounds.Width - 6d, bounds.Y + bounds.Height - 6d), 3.5d, foreground);
                });
        }
    }

    private static Color ToButtonColor(ButtonColor color) => new(color.R, color.G, color.B, color.A);
    private void DrawToolbarIcon(ToolbarAction action, ScreenRectangle bounds, Color color)
    {
        if (action == ToolbarAction.FillVacantSeats)
        {
            DrawWizardIcon(bounds, color);
            return;
        }
        if (action is ToolbarAction.FaceNorth or ToolbarAction.FaceEast or ToolbarAction.FaceSouth or ToolbarAction.FaceWest)
        {
            var orientation = action switch
            {
                ToolbarAction.FaceNorth => QuarterTurn.North,
                ToolbarAction.FaceEast => QuarterTurn.East,
                ToolbarAction.FaceSouth => QuarterTurn.South,
                _ => QuarterTurn.West,
            };
            var vertical = orientation is QuarterTurn.East or QuarterTurn.West;
            var width = vertical ? 18d : 30d;
            var height = vertical ? 30d : 18d;
            var spaceBounds = new ScreenRectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
            DrawOutline(spaceBounds, 2, color);
            DrawDeskOrientationMarker(spaceBounds, orientation, color);
            return;
        }
        if (action is ToolbarAction.VenueSizeMenu or ToolbarAction.ExpandTop or ToolbarAction.ShrinkTop or
            ToolbarAction.ExpandLeft or ToolbarAction.ShrinkLeft or ToolbarAction.IncreaseWidth or ToolbarAction.DecreaseWidth or
            ToolbarAction.IncreaseHeight or ToolbarAction.DecreaseHeight)
        {
            DrawVenueSizeIcon(action, bounds, color);
            return;
        }
        var center = new ScreenPoint(bounds.X + bounds.Width / 2d, bounds.Y + bounds.Height / 2d);
        switch (action)
        {
            case ToolbarAction.DeskMenu:
            case ToolbarAction.DeskPlacementMode:
                DrawOutline(new ScreenRectangle(center.X - 13d, center.Y - 7d, 26d, 14d), 3d, color);
                DrawLine(new ScreenPoint(center.X - 9d, center.Y + 7d), new ScreenPoint(center.X - 9d, center.Y + 13d), 3d, color);
                DrawLine(new ScreenPoint(center.X + 9d, center.Y + 7d), new ScreenPoint(center.X + 9d, center.Y + 13d), 3d, color);
                break;
            case ToolbarAction.CirclePlacementMode:
                DrawCircle(center, 11d, color);
                DrawCircle(new ScreenPoint(center.X + 8d, center.Y + 7d), 5d, color);
                break;
            case ToolbarAction.GenreDataMode:
                DrawCircle(center, 12d, color);
                DrawLine(center, new ScreenPoint(center.X, center.Y - 12d), 2d, color);
                DrawLine(center, new ScreenPoint(center.X + 11d, center.Y + 5d), 2d, color);
                break;
            case ToolbarAction.AddIslandStart:
            case ToolbarAction.RemoveIslandStart:
                DrawLine(new ScreenPoint(center.X - 8, center.Y + 12), new ScreenPoint(center.X - 8, center.Y - 12), 3, color);
                DrawOutline(new ScreenRectangle(center.X - 8, center.Y - 12, 19, 12), 3, color);
                if (action == ToolbarAction.RemoveIslandStart)
                    DrawLine(new ScreenPoint(center.X - 12, center.Y + 10), new ScreenPoint(center.X + 12, center.Y - 10), 3, color);
                break;
            case ToolbarAction.AddIslandConnector:
                DrawLine(new ScreenPoint(center.X - 12d, center.Y + 8d), new ScreenPoint(center.X + 12d, center.Y - 8d), 3d, color);
                DrawCircle(new ScreenPoint(center.X - 12d, center.Y + 8d), 3d, color);
                DrawCircle(new ScreenPoint(center.X + 12d, center.Y - 8d), 3d, color);
                break;
            case ToolbarAction.ToggleAutomaticIslandConnection:
                DrawLine(new ScreenPoint(center.X - 12d, center.Y), new ScreenPoint(center.X + 12d, center.Y), 3d, color);
                DrawLine(new ScreenPoint(center.X - 2d, center.Y - 8d), new ScreenPoint(center.X + 2d, center.Y + 8d), 3d, color);
                DrawCircle(new ScreenPoint(center.X - 12d, center.Y), 3d, color);
                DrawCircle(new ScreenPoint(center.X + 12d, center.Y), 3d, color);
                break;
            case ToolbarAction.AddFacingRegion:
                DrawOutline(new ScreenRectangle(center.X - 13d, center.Y - 9d, 26d, 18d), 3d, color);
                break;
            case ToolbarAction.RemoveTopology:
                DrawLine(new ScreenPoint(center.X - 9d, center.Y - 9d), new ScreenPoint(center.X + 9d, center.Y + 9d), 4d, color);
                DrawLine(new ScreenPoint(center.X + 9d, center.Y - 9d), new ScreenPoint(center.X - 9d, center.Y + 9d), 4d, color);
                break;
            case ToolbarAction.ShowEvaluationBreakdown:
                DrawLine(new ScreenPoint(center.X - 14, center.Y + 13), new ScreenPoint(center.X + 15, center.Y + 13), 2, color);
                for (var bar = 0; bar < 3; bar++)
                    DrawRectangle(new ScreenRectangle(center.X - 12 + bar * 10, center.Y + 9 - bar * 8, 6, 4 + bar * 8), color);
                break;
            case ToolbarAction.ToggleEvaluationAnalysis:
                DrawCircle(center, 12d, color);
                DrawLine(new ScreenPoint(center.X - 8d, center.Y), new ScreenPoint(center.X - 2d, center.Y + 6d), 3d, color);
                DrawLine(new ScreenPoint(center.X - 2d, center.Y + 6d), new ScreenPoint(center.X + 9d, center.Y - 7d), 3d, color);
                break;
            case ToolbarAction.PanViewport:
                DrawOutline(new ScreenRectangle(center.X - 8d, center.Y - 3d, 17d, 16d), 2d, color);
                DrawLine(new ScreenPoint(center.X - 8d, center.Y), new ScreenPoint(center.X - 11d, center.Y - 6d), 3d, color);
                DrawLine(new ScreenPoint(center.X - 4d, center.Y - 3d), new ScreenPoint(center.X - 4d, center.Y - 13d), 3d, color);
                DrawLine(new ScreenPoint(center.X + 1d, center.Y - 3d), new ScreenPoint(center.X + 1d, center.Y - 14d), 3d, color);
                DrawLine(new ScreenPoint(center.X + 6d, center.Y - 3d), new ScreenPoint(center.X + 6d, center.Y - 11d), 3d, color);
                break;
            case ToolbarAction.FitVenueToWindow:
                DrawOutline(new ScreenRectangle(center.X - 12d, center.Y - 12d, 24d, 24d), 2d, color);
                DrawOutline(new ScreenRectangle(center.X - 6d, center.Y - 6d, 12d, 12d), 2d, color);
                DrawLine(new ScreenPoint(center.X - 9d, center.Y - 9d), new ScreenPoint(center.X - 5d, center.Y - 5d), 2d, color);
                DrawLine(new ScreenPoint(center.X + 9d, center.Y - 9d), new ScreenPoint(center.X + 5d, center.Y - 5d), 2d, color);
                DrawLine(new ScreenPoint(center.X - 9d, center.Y + 9d), new ScreenPoint(center.X - 5d, center.Y + 5d), 2d, color);
                DrawLine(new ScreenPoint(center.X + 9d, center.Y + 9d), new ScreenPoint(center.X + 5d, center.Y + 5d), 2d, color);
                break;
            case ToolbarAction.ImportParticipants:
                DrawOutline(new ScreenRectangle(center.X - 12d, center.Y - 11d, 24d, 22d), 2d, color);
                DrawLine(new ScreenPoint(center.X, center.Y - 15d), new ScreenPoint(center.X, center.Y + 4d), 3d, color);
                DrawArrowHead(new ScreenPoint(center.X, center.Y + 4d), 0d, 1d, color);
                break;
            case ToolbarAction.ExportSeatAssignments:
                DrawOutline(new ScreenRectangle(center.X - 12d, center.Y - 11d, 24d, 22d), 2d, color);
                DrawLine(new ScreenPoint(center.X, center.Y - 5d), new ScreenPoint(center.X, center.Y + 14d), 3d, color);
                DrawArrowHead(new ScreenPoint(center.X, center.Y + 14d), 0d, 1d, color);
                break;
            case ToolbarAction.OptimizeCirclePlacement:
                DrawOutline(new ScreenRectangle(center.X - 11d, center.Y - 11d, 22d, 22d), 2d, color);
                textRenderer?.Draw("最", new Rectangle((int)center.X - 8, (int)center.Y - 9, 16, 18), color, 14, true);
                break;
            case ToolbarAction.EditGenreStyles:
                for (var y = -1; y <= 1; y += 2)
                for (var x = -1; x <= 1; x += 2)
                    DrawRectangle(new ScreenRectangle(center.X + x * 7d - 5d, center.Y + y * 7d - 5d, 10d, 10d),
                        x == y ? new Color(229, 194, 55) : new Color(64, 132, 207));
                break;
            case ToolbarAction.Undo:
            case ToolbarAction.Redo:
            case ToolbarAction.RotateLeft:
            case ToolbarAction.RotateRight:
                DrawArrow(center, pointsRight: action is ToolbarAction.Redo or ToolbarAction.RotateRight, color,
                    hooked: action is ToolbarAction.Undo or ToolbarAction.Redo);
                break;
            case ToolbarAction.PreviousPlan:
            case ToolbarAction.NextPlan:
                var right = action == ToolbarAction.NextPlan;
                DrawArrow(new ScreenPoint(center.X - (right ? 5d : -5d), center.Y), right, color, hooked: false);
                DrawArrow(new ScreenPoint(center.X + (right ? 5d : -5d), center.Y), right, color, hooked: false);
                break;
            case ToolbarAction.AssignParticipant:
                DrawLine(new ScreenPoint(center.X - 10d, center.Y), new ScreenPoint(center.X + 10d, center.Y), 4d, color);
                DrawLine(new ScreenPoint(center.X, center.Y - 10d), new ScreenPoint(center.X, center.Y + 10d), 4d, color);
                break;
            case ToolbarAction.UnassignParticipant:
                DrawLine(new ScreenPoint(center.X - 9d, center.Y - 9d), new ScreenPoint(center.X + 9d, center.Y + 9d), 4d, color);
                DrawLine(new ScreenPoint(center.X + 9d, center.Y - 9d), new ScreenPoint(center.X - 9d, center.Y + 9d), 4d, color);
                break;
            case ToolbarAction.AddDesk:
                DrawOutline(new ScreenRectangle(center.X - 10d, center.Y - 7d, 20d, 14d), 3d, color);
                DrawLine(new ScreenPoint(center.X, center.Y - 13d), new ScreenPoint(center.X, center.Y - 5d), 3d, color);
                DrawLine(new ScreenPoint(center.X - 4d, center.Y - 9d), new ScreenPoint(center.X + 4d, center.Y - 9d), 3d, color);
                break;
            case ToolbarAction.RemoveDesk:
                DrawOutline(new ScreenRectangle(center.X - 10d, center.Y - 7d, 20d, 14d), 3d, color);
                DrawLine(new ScreenPoint(center.X - 5d, center.Y), new ScreenPoint(center.X + 5d, center.Y), 3d, color);
                break;
            case ToolbarAction.EditSeatName:
                DrawOutline(new ScreenRectangle(center.X - 12d, center.Y - 10d, 24d, 20d), 2d, color);
                textRenderer?.Draw("番号", new Rectangle((int)center.X - 11, (int)center.Y - 8, 22, 16), color, 11, true);
                DrawLine(new ScreenPoint(center.X + 5d, center.Y + 7d), new ScreenPoint(center.X + 12d, center.Y + 14d), 3d, color);
                DrawLine(new ScreenPoint(center.X + 8d, center.Y + 4d), new ScreenPoint(center.X + 12d, center.Y + 8d), 3d, color);
                break;
            case ToolbarAction.EditDeskNumber:
                DrawOutline(new ScreenRectangle(center.X - 12d, center.Y - 10d, 24d, 20d), 2d, color);
                textRenderer?.Draw("番", new Rectangle((int)center.X - 9, (int)center.Y - 8, 18, 16), color, 14, true);
                DrawLine(new ScreenPoint(center.X + 5d, center.Y + 7d), new ScreenPoint(center.X + 12d, center.Y + 14d), 3d, color);
                break;
            case ToolbarAction.SwapAddresses:
                DrawArrow(new ScreenPoint(center.X, center.Y - 7), true, color, hooked: false);
                DrawArrow(new ScreenPoint(center.X, center.Y + 7), false, color, hooked: false);
                break;
            case ToolbarAction.PillarMenu:
            case ToolbarAction.AddPillar:
            case ToolbarAction.RemovePillar:
                DrawRectangle(new ScreenRectangle(center.X - 10d, center.Y - 10d, 20d, 20d), new Color(94, 104, 114));
                DrawOutline(new ScreenRectangle(center.X - 10d, center.Y - 10d, 20d, 20d), 2d, color);
                DrawCircle(new ScreenPoint(center.X - 4d, center.Y - 4d), 1.5d, color);
                DrawCircle(new ScreenPoint(center.X + 4d, center.Y - 4d), 1.5d, color);
                DrawCircle(new ScreenPoint(center.X - 4d, center.Y + 4d), 1.5d, color);
                DrawCircle(new ScreenPoint(center.X + 4d, center.Y + 4d), 1.5d, color);
                if (action != ToolbarAction.PillarMenu)
                    DrawLine(new ScreenPoint(center.X - 4d, center.Y + 14d), new ScreenPoint(center.X + 4d, center.Y + 14d), 2d, color);
                if (action == ToolbarAction.AddPillar)
                    DrawLine(new ScreenPoint(center.X, center.Y + 10d), new ScreenPoint(center.X, center.Y + 18d), 2d, color);
                break;
            case ToolbarAction.FillDesks:
                for (var y = -1; y <= 1; y += 2)
                for (var x = -1; x <= 1; x += 2)
                    DrawOutline(new ScreenRectangle(center.X + x * 7d - 5d, center.Y + y * 6d - 4d, 10d, 8d), 2d, color);
                break;
            case ToolbarAction.DuplicatePlan:
                DrawOutline(new ScreenRectangle(center.X - 8d, center.Y - 10d, 17d, 19d), 2d, color);
                DrawOutline(new ScreenRectangle(center.X - 12d, center.Y - 6d, 17d, 19d), 2d, color);
                DrawLine(new ScreenPoint(center.X + 4d, center.Y - 13d), new ScreenPoint(center.X + 12d, center.Y - 13d), 2d, color);
                DrawLine(new ScreenPoint(center.X + 8d, center.Y - 17d), new ScreenPoint(center.X + 8d, center.Y - 9d), 2d, color);
                break;
            case ToolbarAction.DecreaseWidth:
            case ToolbarAction.IncreaseWidth:
                DrawLine(new ScreenPoint(center.X - 11d, center.Y), new ScreenPoint(center.X + 11d, center.Y), 3d, color);
                DrawSizeSign(center, action == ToolbarAction.IncreaseWidth, color);
                break;
            case ToolbarAction.DecreaseHeight:
            case ToolbarAction.IncreaseHeight:
                DrawLine(new ScreenPoint(center.X, center.Y - 11d), new ScreenPoint(center.X, center.Y + 11d), 3d, color);
                DrawSizeSign(center, action == ToolbarAction.IncreaseHeight, color);
                break;
            case ToolbarAction.MoveDesk:
                DrawLine(new ScreenPoint(center.X - 10d, center.Y), new ScreenPoint(center.X + 10d, center.Y), 3d, color);
                DrawLine(new ScreenPoint(center.X, center.Y - 10d), new ScreenPoint(center.X, center.Y + 10d), 3d, color);
                DrawArrowHead(new ScreenPoint(center.X + 10d, center.Y), 1d, 0d, color);
                DrawArrowHead(new ScreenPoint(center.X - 10d, center.Y), -1d, 0d, color);
                DrawArrowHead(new ScreenPoint(center.X, center.Y + 10d), 0d, 1d, color);
                DrawArrowHead(new ScreenPoint(center.X, center.Y - 10d), 0d, -1d, color);
                break;
            case ToolbarAction.CaptureScreenshot:
                DrawOutline(new ScreenRectangle(center.X - 12d, center.Y - 8d, 24d, 17d), 3d, color);
                DrawRectangle(new ScreenRectangle(center.X - 7d, center.Y - 12d, 9d, 4d), color);
                DrawOutline(new ScreenRectangle(center.X - 5d, center.Y - 5d, 10d, 10d), 2d, color);
                break;
            case ToolbarAction.SaveProject:
                DrawOutline(new ScreenRectangle(center.X - 11d, center.Y - 12d, 22d, 24d), 3d, color);
                DrawRectangle(new ScreenRectangle(center.X - 6d, center.Y - 10d, 10d, 7d), color);
                DrawOutline(new ScreenRectangle(center.X - 7d, center.Y + 2d, 14d, 8d), 2d, color);
                break;
            case ToolbarAction.ReturnToEventList:
                DrawOutline(new ScreenRectangle(center.X - 12d, center.Y - 7d, 24d, 15d), 3d, color);
                DrawRectangle(new ScreenRectangle(center.X - 9d, center.Y - 11d, 10d, 5d), color);
                DrawArrow(new ScreenPoint(center.X, center.Y), pointsRight: false, color, hooked: false);
                break;
        }
    }

    private void DrawSizeSign(ScreenPoint center, bool plus, Color color)
    {
        DrawLine(new ScreenPoint(center.X - 5d, center.Y), new ScreenPoint(center.X + 5d, center.Y), 3d, color);
        if (plus)
            DrawLine(new ScreenPoint(center.X, center.Y - 5d), new ScreenPoint(center.X, center.Y + 5d), 3d, color);
    }

    private void DrawArrowHead(ScreenPoint tip, double directionX, double directionY, Color color)
    {
        var perpendicularX = -directionY;
        var perpendicularY = directionX;
        var baseX = tip.X - directionX * 6d;
        var baseY = tip.Y - directionY * 6d;
        DrawLine(tip, new ScreenPoint(baseX + perpendicularX * 4d, baseY + perpendicularY * 4d), 3d, color);
        DrawLine(tip, new ScreenPoint(baseX - perpendicularX * 4d, baseY - perpendicularY * 4d), 3d, color);
    }

    private void DrawArrow(ScreenPoint center, bool pointsRight, Color color, bool hooked)
    {
        var direction = pointsRight ? 1d : -1d;
        DrawLine(new ScreenPoint(center.X - direction * 10d, center.Y), new ScreenPoint(center.X + direction * 8d, center.Y), 4d, color);
        DrawLine(new ScreenPoint(center.X + direction * 8d, center.Y), new ScreenPoint(center.X + direction * 1d, center.Y - 7d), 4d, color);
        DrawLine(new ScreenPoint(center.X + direction * 8d, center.Y), new ScreenPoint(center.X + direction * 1d, center.Y + 7d), 4d, color);
        if (hooked)
            DrawLine(new ScreenPoint(center.X - direction * 10d, center.Y), new ScreenPoint(center.X - direction * 10d, center.Y + 8d), 4d, color);
    }

    private void DrawLine(ScreenPoint from, ScreenPoint to, double width, Color color)
    {
        if (spriteBatch is null || pixel is null)
            return;
        var delta = new Vector2((float)(to.X - from.X), (float)(to.Y - from.Y));
        spriteBatch.Draw(
            pixel,
            new Vector2((float)from.X, (float)from.Y),
            null,
            color,
            MathF.Atan2(delta.Y, delta.X),
            new Vector2(0f, 0.5f),
            new Vector2(delta.Length(), (float)width),
            SpriteEffects.None,
            0f);
    }

    private void DrawCircle(ScreenPoint center, double radius, Color color)
    {
        const int segments = 20;
        for (var index = 0; index < segments; index++)
        {
            var firstAngle = Math.PI * 2d * index / segments;
            var secondAngle = Math.PI * 2d * (index + 1) / segments;
            DrawLine(
                new ScreenPoint(center.X + Math.Cos(firstAngle) * radius, center.Y + Math.Sin(firstAngle) * radius),
                new ScreenPoint(center.X + Math.Cos(secondAngle) * radius, center.Y + Math.Sin(secondAngle) * radius),
                2d,
                color);
        }
    }

    private void DrawOutline(ScreenRectangle rectangle, double thickness, Color color)
    {
        DrawRectangle(new ScreenRectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        DrawRectangle(new ScreenRectangle(rectangle.X, rectangle.Y + rectangle.Height - thickness, rectangle.Width, thickness), color);
        DrawRectangle(new ScreenRectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        DrawRectangle(new ScreenRectangle(rectangle.X + rectangle.Width - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }

    private static string GetAccessibleName(ToolbarAction action) => action switch
    {
        ToolbarAction.GenreMenu => "ジャンル：色・網掛けとジャンルデータを表示する",
        ToolbarAction.ToggleCircleStoneTransparency => "数値チャンネルで石の塗りを半透明にし、下地の重みを見る（再クリックで戻す）",
        ToolbarAction.FillVacantSeats => "未配置・仮置きのサークル石を、合体ルールを守って空いている配置可能セルへ一括配置する",
        ToolbarAction.ExportFrameLayout => "フレーム配置案を選び、会場・定義と一緒に部分書出しする",
        ToolbarAction.EditDeskLayoutDescription => "選択中のフレーム配置案の説明を編集する。説明はイベントに保存され、部分書出しにも含まれます",
        ToolbarAction.ImportFrameLayout => "ポータブル・旧配置データから案を選んで現在のイベントに追加する",
        ToolbarAction.SpaceDefinitionsMode => "このフレーム配置の型と申込スペースを編集する",
        ToolbarAction.DeskMenu => "フレーム：追加・削除を選択",
        ToolbarAction.PillarMenu => "柱：追加・削除を選択",
        ToolbarAction.VenueSizeMenu => "会場サイズ：上下左右の辺を伸ばす・縮める",
        ToolbarAction.CirclePlacementDecisionMode => "サークル配置を決定し、出力先の表を確認する",
        ToolbarAction.SelectExportTarget => "出力先の Excel / CSV ファイルとシートを選択する",
        ToolbarAction.SelectExportColumns => "番号を書き込む列と照合ID列を設定し、書出しプレビューを表示する",
        ToolbarAction.ParticipantDataMode => "サークルデータを表で確認し、Excel / CSV を読み込む",
        ToolbarAction.DeskPlacementMode => "フレーム配置モードへ切り替える",
        ToolbarAction.IslandDefinitionMode => "島定義モードへ切り替える",
        ToolbarAction.GenrePlacementMode => "ジャンル配置モードへ切り替える",
        ToolbarAction.CirclePlacementMode => "サークル配置モードへ切り替える",
        ToolbarAction.GenreDataMode => "ジャンルデータモードへ切り替える",
        ToolbarAction.PanViewport => "ハンドツール：会場全体を左ドラッグで移動する（Spaceキーを押しながらの左ドラッグでも一時的に使える）",
        ToolbarAction.FitVenueToWindow => "会場全体を画面内に収める",
        ToolbarAction.ImportParticipants => "参加サークル一覧をExcelまたはCSVから読み込む",
        ToolbarAction.SelectExportPlan => "書出しに使う配置決定案を選択する（未決定にも戻せます）",
        ToolbarAction.ExportSeatAssignments => "配置決定案のブロック番号・セル番を出力先の Excel / CSV へ書き出す",
        ToolbarAction.OptimizeCirclePlacement => "現在の配置案を初期状態にして、一般参加評価値、次にサークル参加評価値の順で自動最適化する",
        ToolbarAction.EditGenreStyles => "ジャンルと色・網掛けパターンの対応を編集する",
        ToolbarAction.AddIslandStart => "スタートを配置する（同じセルを再クリックで入口を右回転。入口側から距離を数え、出口へ直接進まない）",
        ToolbarAction.RemoveIslandStart => "スタートを削除する（旗のあるセルをクリック）",
        ToolbarAction.AddIslandConnector => "島接続補助直線を追加する（配置可能セルを2回選択）",
        ToolbarAction.ToggleAutomaticIslandConnection => "自動島接続を有効・無効に切り替える（線をクリック）",
        ToolbarAction.AddFacingRegion => "向かい合わせ領域矩形を追加する（対角を2回選択）",
        ToolbarAction.RemoveTopology => "島接続補助直線または向かい合わせ領域を削除する",
        ToolbarAction.ToggleEvaluationAnalysis => "一般参加者評価の分析表示をオン／オフする",
        ToolbarAction.ShowEvaluationBreakdown => "配置案の評価内訳：チャンネルの得点・倍率・寄与率を表示する",
        ToolbarAction.PreviousPlan => "前の配置案",
        ToolbarAction.NextPlan => "次の配置案",
        ToolbarAction.Undo => "元に戻す",
        ToolbarAction.Redo => "やり直す",
        ToolbarAction.DuplicatePlan => "現在の配置案を配置修正版として複製する",
        ToolbarAction.MoveDesk => "フレームを移動する",
        ToolbarAction.AddDesk => "フレームを追加する",
        ToolbarAction.RemoveDesk => "フレームを削除する",
        ToolbarAction.EditSeatName => "番地入力：クリックで入力、Shift＋ドラッグで選択、選択範囲をCtrl＋ドラッグでスワップ",
        ToolbarAction.SwapAddresses => "番地のスワップ：選択中の番地をドラッグで交換する（再度押すと入力へ戻る）",
        ToolbarAction.EditDeskNumber => "フレーム番号を変更する（全てのフレームに設定が必要）",
        ToolbarAction.AddPillar => "柱を置く",
        ToolbarAction.RemovePillar => "柱を消す",
        ToolbarAction.FillDesks => "空き領域へフレームを自動配置する",
        ToolbarAction.RotateLeft => "フレームを左回転",
        ToolbarAction.RotateRight => "フレームを右回転",
        ToolbarAction.AssignParticipant => "サークルを割り当てる",
        ToolbarAction.UnassignParticipant => "サークル割当てを解除する",
        ToolbarAction.DecreaseWidth => "会場を横に1セル縮める",
        ToolbarAction.IncreaseWidth => "会場を横に1セル広げる",
        ToolbarAction.DecreaseHeight => "会場を縦に1セル縮める",
        ToolbarAction.IncreaseHeight => "会場を縦に1セル広げる",
        ToolbarAction.CaptureScreenshot => "スクリーンショットを撮る（Ctrl+P）",
        ToolbarAction.ProjectMenu => "プロジェクト：開く・保存・一部の書出し／取込み・閉じる",
        ToolbarAction.SaveProject => "イベントプロジェクトを保存する（Ctrl+S）",
        ToolbarAction.ReturnToEventList => "プロジェクトを閉じる（イベント一覧へ／Ctrl+O）",
        _ => action.ToString(),
    };

    private static string GetModeLabel(ToolbarAction action) => action switch
    {
        ToolbarAction.SpaceDefinitionsMode => "フレーム定義",
        ToolbarAction.CirclePlacementDecisionMode => "スペース番号書き出し",
        ToolbarAction.ParticipantDataMode => "サークルデータ",
        ToolbarAction.DeskPlacementMode => "フレーム配置",
        ToolbarAction.IslandDefinitionMode => "島定義",
        ToolbarAction.GenrePlacementMode => "ジャンル配置",
        ToolbarAction.CirclePlacementMode => "サークル配置",
        ToolbarAction.GenreDataMode => "ジャンルデータ",
        _ => "",
    };

    private bool IsPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && previousKeyboard.IsKeyUp(key);

    private static bool IsControlDown(KeyboardState keyboard) =>
        keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);

    private void DrawStatusBar(string? toolDescription = null, string? heading = null)
    {
        var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var top = height - StatusBarHeight;
        DrawRectangle(new ScreenRectangle(0d, top, width, StatusBarHeight), new Color(14, 20, 28, 248));
        DrawRectangle(new ScreenRectangle(0d, top, width, 2d), new Color(72, 143, 153));
        textRenderer?.Draw(
            heading ?? HoveredButtonDescription(),
            new Rectangle(14, top + 5, Math.Max(1, width - 288), 23),
            Color.White,
            pixelHeight: 17,
            bold: true);
        DrawAutoSaveStatus(width, top);
        var layoutDescription = HoveredDeskLayoutDescription();
        var showHintTimer = toolDescription is null && layoutDescription is null && ShowsStatusHintTimer && statusHintContext is not null;
        const int hintTimerWidth = 80;
        textRenderer?.Draw(
            toolDescription ?? layoutDescription ?? secondaryStatusMessage,
            new Rectangle(14, top + 30, Math.Max(1, width - 28 - (showHintTimer ? hintTimerWidth + 12 : 0)), 20),
            new Color(184, 204, 214),
            pixelHeight: 15);
        if (showHintTimer)
        {
            var track = new ScreenRectangle(width - 14 - hintTimerWidth, top + 38, hintTimerWidth, 5);
            DrawRectangle(track, new Color(57, 75, 84));
            DrawRectangle(new ScreenRectangle(track.X, track.Y, track.Width * StatusHintProgress, track.Height),
                new Color(110, 160, 170));
        }
    }

    private void UpdateWindowPresentation()
    {
        Window.Title = GetWindowTitle();
        if (editorMode == EditorMode.SpaceDefinitions)
        {
            secondaryStatusMessage = spaceDefinitionStatus;
            return;
        }
        if (workspace is null)
        {
            secondaryStatusMessage = "";
            return;
        }

        if (editorMode is EditorMode.ParticipantData or EditorMode.CirclePlacementDecision)
        {
            secondaryStatusMessage = toolbarButtons.FirstOrDefault(button => button.Model.IsPointerOver)?.Model.AccessibleName
                ?? "ホイール: 縦スクロール　Shift＋ホイール: 横　矢印 / PageUp・Down / Home・End: 移動　セルクリック: 全文";
            return;
        }
        var hoveredToolbarButton = toolbarButtons.FirstOrDefault(button => button.Model.IsPointerOver);
        var hoveredButton = hoveredToolbarButton is not null &&
            activeCanvasTool == ToolbarAction.AddDesk &&
            hoveredToolbarButton.Action is ToolbarAction.RotateLeft or ToolbarAction.RotateRight
                ? hoveredToolbarButton.Action == ToolbarAction.RotateLeft
                    ? "次のフレームを左へ90度回転"
                    : "次のフレームを右へ90度回転"
                : hoveredToolbarButton?.Model.AccessibleName;
        var hoveredPlan = hoveredPlanId is null
            ? null
            : GetDisplayedPlans().FirstOrDefault(plan => plan.PlanId == hoveredPlanId);
        var details = new List<string>();
        if (editorMode != EditorMode.DeskPlacement && !workspace.GetSelectedPlanSnapshot().AudienceEvaluation.CombinedSpaceRequirementsSatisfied)
            details.Add("⚠ 合体サークルが同じフレームにありません");
        if (ShowsLayoutOrder && CanShowEditorHover)
            foreach (var direction in new[] { -1, 1 })
                if (Contains(LayoutOrderButton(direction), new ScreenPoint(previousMouse.X, previousMouse.Y)))
                    details.Add(direction < 0 ? "選択中のフレーム配置を1つ上へ移動" : "選択中のフレーム配置を1つ下へ移動");
        if (editorMode == EditorMode.DeskPlacement && hoveredPlan is not null)
        {
            var mismatch = GetSpaceCapacity().Layouts[hoveredPlan.PlanId] != GetSpaceCapacity().Requested;
            details.Add((mismatch ? "エラー：スペース数が一致していません。" : "") + DescribeSpaceCapacity(hoveredPlan.PlanId));
        }
        if (CanShowEditorHover && GetNumberChannelHoverError(new ScreenPoint(previousMouse.X, previousMouse.Y)) is { } numberError)
            details.Add(numberError);
        if (ShowsVacantSeats)
        {
            details.Add($"○ 空きスペース：{GetVacantSeats(workspace.SelectedPlan).Count}（サークル配置で消えます）");
            details.Add("オレンジ縁の丸い駒：席外・未配置のサークル");
        }
        if (hoveredButton is not null)
            details.Add(hoveredButton);
        if (hoveredLayoutAdd)
            details.Add(editorMode is EditorMode.DeskPlacement or EditorMode.IslandDefinition
                ? "フレーム配置を追加する"
                : "選択中のフレーム配置にサークル配置を追加する");
        if (hoveredLayoutDuplicate)
            details.Add(ShowsDeskLayouts ? "選択中のフレーム配置を独立したフレーム配置として複製する" : "選択中のサークル配置案を複製する");
        if (hoveredLayoutDelete)
            details.Add(editorMode is EditorMode.DeskPlacement or EditorMode.IslandDefinition
                ? "未使用のフレーム配置を削除する"
                : "選択中のサークル配置を削除する");
        if (hoveredDeskLayoutParent)
            details.Add("フレーム配置を選択して、そのフレーム配置の配置案を表示する");
        if (hoveredLayoutBind)
            details.Add("選択中のサークル配置のフレーム配置を変更する");
        if (hoveredLayoutRename)
            details.Add(editorMode is EditorMode.DeskPlacement or EditorMode.IslandDefinition
                ? "選択中のフレーム配置の名前を変更する"
                : "選択中のサークル配置の名前を変更する");
        if (hoveredPlan is not null)
            details.Add(editorMode == EditorMode.DeskPlacement
                ? $"一覧: {hoveredPlan.Rank}番 {hoveredPlan.PlanName}（{(UsesSeparatedLayouts ? "手動順" : "名前順")}）"
                : $"一覧: {hoveredPlan.Rank}位 {hoveredPlan.PlanName}　一般 {hoveredPlan.GeneralAttendeeScore:0.##}／サークル {hoveredPlan.CircleParticipantScore:0.##}");
        if (screenshotStatus is not null)
            details.Add(screenshotStatus);
        if (rangeSwapStatus is not null)
            details.Add(rangeSwapStatus);
        if (workspace.SelectedPlan.TemporaryPlacements.Count > 0)
            details.Add($"⚠ 仮置き: {workspace.SelectedPlan.TemporaryPlacements.Count}件");
        if (editorMode == EditorMode.IslandDefinition)
        {
            var deskIds = workspace.SelectedPlan.DeskPlacements.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            var orphanCount = workspace.SelectedPlan.IslandConnectors.Count(item =>
                !deskIds.Contains(item.FirstDeskId) || !deskIds.Contains(item.SecondDeskId));
            if (orphanCount > 0)
                details.Add($"⚠ 接続先を失った島接続補助直線: {orphanCount}本");
        }
        secondaryStatusMessage = ShowsStatusHintTimer
            ? GetRotatingFrameNumberHint(details)
            : details.Count == 0
            ? "ツールボタンにマウスを合わせると操作説明を表示します"
            : string.Join("　｜　", details);
    }

    private string GetWindowTitle() => workspace is null
        ? $"{ApplicationIdentity.Title} - イベントリスト"
        : $"{ApplicationIdentity.Title} - {(CurrentLayoutIsConfidential ? "（秘）" : "")}{workspace.Project.Name}";

    private bool CurrentLayoutIsConfidential => workspace is { } owner && (owner.Project.IsConfidential ||
        owner.Project.DeskLayouts.Any(layout => layout.Id == owner.SelectedDeskLayoutId && layout.IsConfidential));

    private void DrawConfidentialBadge()
    {
        if (!CurrentLayoutIsConfidential)
            return;
        var bounds = new ScreenRectangle(GraphicsDevice.Viewport.Width - 126d, 8d, 112d, 40d);
        DrawRectangle(bounds, new Color(153, 35, 42));
        DrawOutline(bounds, 2d, new Color(255, 221, 154));
        textRenderer?.Draw("秘 SECRET", ToRectangle(bounds, 5), Color.White, 18, true);
    }

    private string FormatParticipantLogDetail(string participantId)
    {
        var participant = workspace?.Project.Participants.SingleOrDefault(item => item.Id == participantId);
        return participant is null
            ? $"participantId={participantId}"
            : $"participantId={participant.Id};circleId={participant.CircleId};circleName={participant.DisplayName}";
    }

    private static double DistanceSquared(ScreenPoint left, ScreenPoint right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        return x * x + y * y;
    }

    private static string FormatOrientation(QuarterTurn orientation) => orientation switch
    {
        QuarterTurn.North => "↑ 上向き（横）",
        QuarterTurn.East => "→ 右向き（縦）",
        QuarterTurn.South => "↓ 下向き（横）",
        QuarterTurn.West => "← 左向き（縦）",
        _ => throw new ArgumentOutOfRangeException(nameof(orientation)),
    };

    private void LogPointer(string action, ScreenPoint pointer, bool? success, string? detail = null, UiOperationFailure? failureCode = null)
    {
        var cell = viewport.ScreenToCell(pointer);
        operationLogger.Log(new UiOperationLogEntry(
            DateTimeOffset.UtcNow,
            action,
            (int)pointer.X,
            (int)pointer.Y,
            cell.Column,
            cell.Row,
            success,
            detail,
            failureCode));
    }

    private void Log(string action, bool? success = null, string? detail = null) =>
        operationLogger.Log(new UiOperationLogEntry(DateTimeOffset.UtcNow, action, Success: success, Detail: detail));

    private void ShowMissingCircleLayout()
    {
        if (workspace is null) return;
        var deskId = workspace.SelectedDeskLayoutId;
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "サークル配置案が必要です",
            "このフレーム配置には、サークルの配置先となる配置案がありません。\nサークル配置案を作成すると、フレームへの配置と通路への仮置きができます。"), action =>
        {
            if (action != ModalDialogAction.Accept) return;
            OpenUnderlineInput("サークル配置案を追加", $"サークル配置{workspace.Project.CircleLayouts.Count + 1}", name =>
            {
                var id = $"circle-layout-{Guid.NewGuid():N}";
                workspace.Execute(new LayoutCatalogServiceCreateCircleLayout(id, name, deskId), selectedPlanEdit: false);
                workspace.SelectPlan(id);
                CreateToolbar();
                rangeSwapStatus = "サークル配置案を作成しました。サークルをもう一度置いてください。";
            }, "新しいサークル配置案の名前を入力してください。\n作成後、サークルをもう一度ドラッグして配置してください。");
        }, [("配置案を作成", ModalDialogAction.Accept), ("キャンセル", ModalDialogAction.Cancel)]);
    }

    private (bool Success, string Detail) SaveProject()
    {
        using var timing = performance?.Measure("project_save");
        savedProjectState = null;
        if (workspace is null || projectSavePath is null)
            return (false, "save_path_unavailable");
        try
        {
            var projectWithView = workspace.Project with
            {
                EditorView = new CircleSpaceCoordinator.Core.Model.EditorViewState(
                    viewport.Zoom,
                    viewport.Origin.X,
                    viewport.Origin.Y),
            };
            if (!ReferenceEquals(autoSaveOwner, workspace)) InitializeAutoSave();
            autoSaveSession!.Save(EditorConnection.Current.Encode(projectWithView), SavePoints, DateOnly.FromDateTime(DateTime.Now));
            autoSaveError = null;
            autoSavePending = false;
            settings?.RememberProject(projectSavePath);
            PersistWorkingState();
            screenshotStatus = $"PROJECT SAVED: {Path.GetFileName(projectSavePath)}";
            Log("project_saved", success: true, detail: $"path={projectSavePath}");
            savedProjectState = (workspace, workspace.Revision, projectSavePath, viewport.Zoom, viewport.Origin.X, viewport.Origin.Y);
            return (true, $"path={projectSavePath}");
        }
        catch (Exception exception)
        {
            autoSaveError = exception.Message;
            savedProjectState = null;
            autoSavePending = false;
            screenshotStatus = $"PROJECT SAVE FAILED: {exception.Message}";
            Log("project_saved", success: false, detail: $"error={exception.GetType().Name};message={exception.Message}");
            return (false, $"error={exception.GetType().Name}");
        }
    }

    private (bool Success, string Detail) PromptCreateLayout()
    {
        if (workspace is null) return (false, "workspace_unavailable");
        var isDesk = editorMode is EditorMode.DeskPlacement or EditorMode.IslandDefinition;
        var deskLayoutId = SelectedDeskLayoutId();
        OpenUnderlineInput(isDesk ? "フレーム配置を追加" : "サークル配置を追加",
            isDesk ? $"フレーム配置{workspace.Project.DeskLayouts.Count + 1}" : $"サークル配置{workspace.Project.CircleLayouts.Count + 1}", name =>
        {
            var id = $"{(isDesk ? "desk-layout" : "circle-layout")}-{Guid.NewGuid():N}";
            if (isDesk)
            {
                workspace.Execute(new LayoutCatalogServiceCreateDeskLayout( id, name), selectedPlanEdit: false);
                workspace.SelectDeskLayout(id);
            }
            else
            {
                workspace.Execute(new LayoutCatalogServiceCreateCircleLayout( id, name, deskLayoutId), selectedPlanEdit: false);
                workspace.SelectPlan(id);
            }
            Log("layout_create", success: true, detail: $"id={id}");
        }, "追加する配置の名前を入力してください（100 文字まで）。\n［確定］で作成し、［キャンセル］で取り消します。");
        return (true, "dialog_opened");
    }

    private (bool Success, string Detail) PromptDeleteLayout()
    {
        if (workspace is null) return (false, "workspace_unavailable");
        var isDesk = editorMode is EditorMode.DeskPlacement or EditorMode.IslandDefinition;
        var id = isDesk ? SelectedDeskLayoutId() : workspace.SelectedPlanId;
        if (isDesk && workspace.Project.CircleLayouts.Any(item => item.DeskLayoutId == id))
        {
            ShowInAppMessage("フレーム配置を削除", "このフレーム配置はサークル配置で使用中です。\n先にサークル配置を削除または紐付け変更してください。");
            return (false, "desk_layout_referenced");
        }
        if (!isDesk && workspace.Project.CircleLayouts.Count <= 1)
        {
            ShowInAppMessage("サークル配置を削除", "最後のサークル配置は削除できません。");
            return (false, "last_circle_layout");
        }
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "配置を削除",
            isDesk ? $"フレーム配置「{CurrentDeskLayoutName()}」を削除しますか？" : "このサークル配置を削除しますか？"), action =>
        {
            if (action != ModalDialogAction.Accept) return;
            try
            {
                workspace.Execute(isDesk
                    ? new LayoutCatalogServiceRemoveDeskLayout( id)
                    : new LayoutCatalogServiceRemoveCircleLayout(id), selectedPlanEdit: false);
                Log("layout_delete", success: true);
            }
            catch (Exception exception)
            {
                ShowInAppMessage("配置を削除", exception.Message);
                Log("layout_delete", success: false);
            }
        });
        return (true, "dialog_opened");
    }
    private (bool Success, string Detail) PromptRebindCircleLayout()
    {
        if (workspace is null) return (false, "workspace_unavailable");
        var circle = workspace.Project.CircleLayouts.Single(item => item.Id == workspace.SelectedPlanId);
        OpenLayoutSelection("紐付け先のフレーム配置", circle.DeskLayoutId, target =>
        {
            if (target == circle.DeskLayoutId) return;
            workspace.Execute(new LayoutCatalogServiceReassignCircleLayout( circle.Id, target), selectedPlanEdit: false);
            workspace.SelectPlan(circle.Id);
            Log("circle_layout_rebind", true);
        });
        return (true, "dialog_opened");
    }

    private string SelectedDeskLayoutId() => workspace!.SelectedDeskLayoutId;

    private (bool Success, string Detail) PromptRenameLayout()
    {
        if (workspace is null) return (false, "workspace_unavailable");
        var isDesk = editorMode is EditorMode.DeskPlacement or EditorMode.IslandDefinition;
        var id = isDesk ? SelectedDeskLayoutId() : workspace.SelectedPlanId;
        var currentName = isDesk
            ? workspace.Project.DeskLayouts.Single(item => item.Id == id).Name
            : workspace.Project.CircleLayouts.Single(item => item.Id == id).Name;
        OpenUnderlineInput(isDesk ? "フレーム配置の名前を変更" : "サークル配置の名前を変更", currentName, name =>
        {
            workspace.Execute(isDesk
                ? new LayoutCatalogServiceRenameDeskLayout( id, name)
                : new LayoutCatalogServiceRenameCircleLayout(id, name), selectedPlanEdit: false);
        });
        return (true, "dialog_opened");
    }
    private (bool Success, string Detail) PromptDuplicateLayout()
    {
        if (workspace is null) return (false, "workspace_unavailable");
        if (!ShowsDeskLayouts) return PromptDuplicateSelectedPlan();
        var sourceId = workspace.SelectedDeskLayoutId;
        OpenUnderlineInput("フレーム配置を複製", $"{CurrentDeskLayoutName()}2", name =>
        {
            var id = $"desk-layout-{Guid.NewGuid():N}";
            workspace.Execute(new LayoutCatalogServiceDuplicateDeskLayout(sourceId!, id, name), selectedPlanEdit: false);
            workspace.SelectDeskLayout(id);
            Log("layout_duplicate", success: true, detail: $"id={id}");
        }, "複製先の名前を入力してください（100 文字まで）。\n［確定］で複製し、［キャンセル］で取り消します。");
        return (true, "dialog_opened");
    }

    private (bool Success, string Detail) PromptDuplicateSelectedPlan()
    {
        if (workspace is null || commandController is null)
            return (false, "workspace_unavailable");
        OpenUnderlineInput("配置案を複製", $"{workspace.SelectedPlan.Name}2", name =>
        {
            var result = commandController.DuplicateSelectedPlan(name);
            var outcome = FormatOutcome(result);
            Log("plan_duplicate", outcome.Success, outcome.Detail);
            if (!result.Applied) ShowInAppMessage("配置案を複製", "配置案を複製できませんでした。配置内容を確認してください。");
        }, "複製先の名前を入力してください（100 文字まで）。\n［確定］で複製し、［キャンセル］で取り消します。");
        return (true, "dialog_opened");
    }

    private IReadOnlyList<CircleSeatExportRow> BuildCircleSeatExportRows() => workspace is null
        ? []
        : CircleSeatExportBuilder.BuildDecided(workspace.Project);

    private (bool Success, string Detail) PromptCopyDeskLayout()
    {
        if (workspace is null || commandController is null)
            return (false, "workspace_unavailable");
        var plans = workspace.Project.Plans.ToArray();
        if (plans.Length < 2) { ShowInAppMessage("色んなコピー", "コピーには二つ以上の配置案が必要です。"); return (false, "too_few_plans"); }
        var source = 0;
        var destination = 1;
        void ShowDraft() => OpenSelection("色んなコピー：フレーム配置だけコピー",
            [$"コピー元：{plans[source].Name}（{plans[source].Id}）", $"コピー先：{plans[destination].Name}（{plans[destination].Id}）", "この内容でコピーする"], 0, index =>
        {
            if (index < 2)
            {
                OpenSelection(index == 0 ? "コピー元" : "コピー先", plans.Select(plan => $"{plan.Name}（{plan.Id}）").ToArray(),
                    index == 0 ? source : destination, chosen => { if (index == 0) source = chosen; else destination = chosen; ShowDraft(); }, ShowDraft);
                return;
            }
            if (source == destination) { ShowNotice("色んなコピー", "別のコピー元・コピー先を選んでください。", ShowDraft); return; }
            OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "コピーの確認",
                "フレーム・セル番・島定義をコピーします。\nコピー先のサークル配置は維持します。収まらない場合は変更しません。"), action =>
            {
                if (action != ModalDialogAction.Accept) { ShowDraft(); return; }
                try
                {
                    var result = commandController.CopyDeskLayout(plans[source].Id, plans[destination].Id);
                    if (!result.Applied)
                    {
                        var message = result.Issues.Any(issue => issue.Code == "assignment.cell.withoutDesk")
                            ? "コピー先のサークル配置が、コピー元のフレーム配置に収まりません。\nサークルを先に移動または解除してからコピーしてください。"
                            : "フレーム配置をコピーできませんでした。\nコピー元・コピー先の内容を確認してください。";
                        ShowNotice("色んなコピー", message, ShowDraft);
                    }
                    Log("plan_copy", result.Applied);
                }
                catch (Exception exception) { ShowNotice("色んなコピー", exception.Message, ShowDraft); }
            }, [("キャンセル", ModalDialogAction.Cancel), ("コピー", ModalDialogAction.Accept)]);
        });
        ShowDraft();
        return (true, "dialog_opened");
    }

    private (bool Success, string Detail) PromptRenameSelectedPlan()
    {
        if (workspace is null || commandController is null)
            return (false, "workspace_unavailable");
        OpenUnderlineInput("配置案名を変更", workspace.SelectedPlan.Name, name =>
        {
            var result = commandController.RenameSelectedPlan(name);
            if (!result.Applied) throw new InvalidOperationException(FormatIssues(result.Issues));
        });
        return (true, "dialog_opened");
    }
    private (bool Success, string Detail) ReturnToEventList()
    {
        RequestReturnToEvents();
        return (true, "return_confirmation_opened");
    }

    private void RestoreProjectView()
    {
        if (workspace?.Project.EditorView is { } view)
            viewport.SetView(view.Zoom, new ScreenPoint(view.OriginX, view.OriginY));
        else
            viewport.SetView(1d, new ScreenPoint(32d, 88d));
    }

    private void RestoreWorkingState()
    {
        if (workspace is null || projectSavePath is null || settings?.GetWorkingState(projectSavePath) is not { } state)
        {
            RestoreProjectView();
            return;
        }

        viewport.SetView(state.Zoom, new ScreenPoint(state.OriginX, state.OriginY));
        if (workspace.Project.Plans.Any(plan => plan.Id == state.SelectedPlanId))
            workspace.SelectPlan(state.SelectedPlanId);
        if (state.SelectedDeskLayoutId is { } deskId && workspace.Project.DeskLayouts.Any(desk => desk.Id == deskId))
            workspace.SelectDeskLayout(deskId);
        editorMode = Enum.TryParse<EditorMode>(state.EditorMode, ignoreCase: true, out var restoredMode)
            ? restoredMode
            : EditorMode.DeskPlacement;
        if (editorMode == EditorMode.SpaceDefinitions)
        {
            editorMode = EditorMode.DeskPlacement;
        }
        showEvaluationAnalysis = state.Switches?.GetValueOrDefault("evaluationAnalysis") == true;
        activeCanvasTool = editorMode == EditorMode.DeskPlacement
            ? ToolbarAction.MoveDesk
            : editorMode is EditorMode.GenrePlacement or EditorMode.IslandDefinition or EditorMode.GenreData
                ? ToolbarAction.PanViewport
                : ToolbarAction.UnassignParticipant;
    }

    private void PersistWorkingState()
    {
        if (workspace is null || projectSavePath is null || settings is null)
            return;
        settings.SaveWorkingState(new ProjectWorkingState(
            projectSavePath,
            workspace.SelectedPlanId,
            editorMode.ToString(),
            viewport.Zoom,
            viewport.Origin.X,
            viewport.Origin.Y,
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["evaluationAnalysis"] = showEvaluationAnalysis,
            }, workspace.SelectedDeskLayoutId, GetExportPlanPins().ToArray()));
    }

    private static string FormatIssues(IReadOnlyList<CircleSpaceCoordinator.Core.Validation.ValidationIssue> issues) =>
        issues.Count == 0 ? "none" : string.Join(',', issues.Select(issue => issue.Code));

    private static string FormatRangeSwapIssues(IReadOnlyList<CircleSpaceCoordinator.Core.Validation.ValidationIssue> issues) =>
        issues.FirstOrDefault()?.Code switch
        {
            "rangeSwap.overlap" => "移動元と移動先が重なっているため、範囲スワップをキャンセルしました",
            "rangeSwap.source.protrudes" => "選択範囲からはみ出すサークルがあるため、範囲スワップをキャンセルしました",
            "rangeSwap.destination.protrudes" => "移動先範囲からはみ出すサークルがあるため、範囲スワップをキャンセルしました",
            "rangeSwap.destination.invalid" or "rangeSwap.destination.noDesk" => "移動先にフレーム以外のセルがあるため、範囲スワップをキャンセルしました",
            _ => "範囲スワップをキャンセルしました",
        };

    private void DrawRectangle(ScreenRectangle rectangle, Color color)
    {
        if (spriteBatch is null || pixel is null || rectangle.Width <= 0d || rectangle.Height <= 0d)
            return;
        spriteBatch.Draw(pixel, new Rectangle(
            (int)Math.Round(rectangle.X),
            (int)Math.Round(rectangle.Y),
            Math.Max(1, (int)Math.Round(rectangle.Width)),
            Math.Max(1, (int)Math.Round(rectangle.Height))), color);
    }

    private static bool Contains(ScreenRectangle rectangle, ScreenPoint point) =>
        point.X >= rectangle.X && point.X < rectangle.X + rectangle.Width &&
        point.Y >= rectangle.Y && point.Y < rectangle.Y + rectangle.Height;

    private static Rectangle ToRectangle(ScreenRectangle rectangle, int inset = 0) => new(
        (int)Math.Round(rectangle.X) + inset,
        (int)Math.Round(rectangle.Y) + inset,
        Math.Max(1, (int)Math.Round(rectangle.Width) - inset * 2),
        Math.Max(1, (int)Math.Round(rectangle.Height) - inset * 2));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            developerWindow.Dispose();
            FinishBackgroundOperation();
            textInputService?.Dispose();
            DisposeOptimization();
            workspace?.Dispose();
            ownedEngineRuntime?.Dispose();
            screenshotShutterSoundInstance?.Dispose();
            screenshotShutterSound?.Dispose();
            textRenderer?.Dispose();
            tableTextRenderer?.Dispose();
            pixel?.Dispose();
            spriteBatch?.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal enum ToolbarAction
{
    ProjectMenu,
    GenreMenu,
    ToggleCircleStoneTransparency,
    FillVacantSeats,
    ToggleFrameModes,
    ToggleCircleModes,
    EditDeskLayoutDescription,
    ExportFrameLayout,
    ImportFrameLayout,
    SpaceDefinitionsMode,
    VenueSizeMenu,
    ExpandTop,
    ShrinkTop,
    ExpandLeft,
    ShrinkLeft,
    PillarMenu,
    DeskMenu,
    NextDirectionMenu,
    FaceNorth,
    FaceEast,
    FaceSouth,
    FaceWest,
    CirclePlacementDecisionMode,
    SelectExportTarget,
    SelectExportColumns,
    ParticipantDataMode,
    DeskPlacementMode,
    IslandDefinitionMode,
    GenrePlacementMode,
    CirclePlacementMode,
    GenreDataMode,
    PanViewport,
    FitVenueToWindow,
    PreviousPlan,
    NextPlan,
    Undo,
    Redo,
    DuplicatePlan,
    MoveDesk,
    AddDesk,
    RemoveDesk,
    EditSeatName,
    SwapAddresses,
    EditDeskNumber,
    AddPillar,
    RemovePillar,
    FillDesks,
    RotateLeft,
    RotateRight,
    AssignParticipant,
    UnassignParticipant,
    ImportParticipants,
    ExportSeatAssignments,
    SelectExportPlan,
    OptimizeCirclePlacement,
    EditGenreStyles,
    AddIslandStart,
    RemoveIslandStart,
    AddIslandConnector,
    AddFacingRegion,
    ToggleAutomaticIslandConnection,
    RemoveTopology,
    ToggleEvaluationAnalysis,
    ShowEvaluationBreakdown,
    DecreaseWidth,
    IncreaseWidth,
    DecreaseHeight,
    IncreaseHeight,
    ReturnToEventList,
    CaptureScreenshot,
    SaveProject,
}

internal enum EditorMode
{
    SpaceDefinitions,
    DeskPlacement,
    IslandDefinition,
    GenrePlacement,
    CirclePlacement,
    GenreData,
    ParticipantData,
    CirclePlacementDecision,
}

internal sealed record ToolbarButton(ToolbarAction Action, IconButtonModel Model);

internal readonly record struct GenreVisualStyle(Color Primary, Color Secondary, int Pattern);

internal readonly record struct GenreDataGroup(string GenreId, int SpaceCount, int CircleCount);

internal readonly record struct SeatLabelTarget(string DeskPlacementId, GridPosition RelativeCell, GridPosition Cell);

internal readonly record struct CellRange(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left + 1;
    public int Height => Bottom - Top + 1;

    public bool Contains(GridPosition cell) =>
        cell.X >= Left && cell.X <= Right && cell.Y >= Top && cell.Y <= Bottom;

    public static CellRange From(GridPosition first, GridPosition second) => new(
        Math.Min(first.X, second.X), Math.Min(first.Y, second.Y),
        Math.Max(first.X, second.X), Math.Max(first.Y, second.Y));
}

internal sealed record ParticipantToken(
    string ParticipantId,
    GridPosition Position,
    IReadOnlyList<GridPosition> DisplayCells,
    string? CombinedSpaceId,
    bool Assigned,
    int Number);
