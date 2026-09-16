namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.OptimizationEngine;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Engine.Model;
using StationeryUI.Canvas;
using StationeryUI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public sealed partial class VenueEditorGame
{
    private ModalDialogModel? modalDialog;
    private Action<ModalDialogAction>? modalCompleted;
    private readonly List<(IconButtonModel Button, ModalDialogAction Action)> modalButtons = [];
    private IconButtonModel? pressedModalButton;
    private bool modalInputDrain;
    private int modalWidth;
    private int modalHeight;
    private int modalFocus;
    private (string Label, ModalDialogAction Action)[]? modalChoices;
    private Task<JobEvent>? optimizationTask;
    private CancellationTokenSource? optimizationCancellation;
    private readonly LatestOptimizationProgress optimizationProgress = new();

    // Draw-time pointer queries must respect the same modal boundary as Update.
    private bool CanShowEditorHover => IsActive && modalDialog is null && !modalInputDrain && !toolRingOpen && !toolRingInputDrain && !spaceCatalogOpen && !spaceCatalogDrain;

    private void OpenModal(ModalDialogModel dialog, Action<ModalDialogAction>? completed = null,
        (string Label, ModalDialogAction Action)[]? choices = null)
    {
        textInputService?.Stop();
        ResetUnderlineKeyRepeat();
        underlineEditor = null;
        selectionLabels = null;
        exportColumnDraft = null;
        exportPlanChoices = null;
        exportPreviewProject = null;
        viewerLines = null;
        viewerDocument = null;
        previewSheet = null;
        CancelInProgressPointerInteraction();
        modalDialog = dialog;
        modalChoices = choices;
        modalCompleted = completed;
        pressedModalButton = null;
        modalButtons.Clear();
        modalWidth = -1;
        // Opening input must not confirm the new dialog.
        modalInputDrain = true;
    }

    private void ShowInAppMessage(string title, string message) =>
        OpenModal(new ModalDialogModel(ModalDialogKind.Message, title, message));

    private bool UpdateModalDialog(KeyboardState keyboard, MouseState mouse)
    {
        if (backgroundOperation is not null) return true;
        if (modalInputDrain)
        {
            if (mouse.LeftButton == ButtonState.Released && keyboard.GetPressedKeys().Length == 0)
                modalInputDrain = false;
            return true;
        }
        if (modalDialog is null) return false;
        EnsureModalButtons();
        if (modalDialog.Kind == ModalDialogKind.Text) return UpdateUnderlineInput(keyboard, mouse);
        if (UpdateExportColumns(keyboard, mouse)) return true;
        if (!UpdateExportPlanPins(keyboard, mouse)) UpdateSelection(keyboard, mouse);
        UpdateTextViewer(keyboard, mouse);
        if (UpdateTablePreview(keyboard, mouse)) return true;
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        foreach (var (button, _) in modalButtons) button.UpdatePointer(pointer);
        if (IsPressed(keyboard, Keys.Escape))
        {
            ApplyModalAction(ModalDialogAction.Cancel);
            return true;
        }
        if (IsPressed(keyboard, Keys.Tab))
            modalFocus = (modalFocus + (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? modalButtons.Count - 1 : 1)) % modalButtons.Count;
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            ApplyModalAction(modalButtons[modalFocus].Action);
            return true;
        }
        if (modalDialog.Kind == ModalDialogKind.Minutes)
        {
            if (IsPressed(keyboard, Keys.Left) || IsPressed(keyboard, Keys.Down)) ApplyModalAction(ModalDialogAction.Decrease);
            if (IsPressed(keyboard, Keys.Right) || IsPressed(keyboard, Keys.Up)) ApplyModalAction(ModalDialogAction.Increase);
        }
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedModalButton = modalButtons.Select(item => item.Button).FirstOrDefault(button => button.Press(pointer));
            if (pressedModalButton is not null) modalFocus = modalButtons.FindIndex(item => item.Button == pressedModalButton);
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedModalButton;
            pressedModalButton = null;
            if (pressed?.Release(pointer) == true)
                ApplyModalAction(modalButtons.Single(item => item.Button == pressed).Action);
        }
        return true;
    }

    private void ApplyModalAction(ModalDialogAction action)
    {
        if (modalDialog is null) return;
        if (exportColumnDraft is { IsComplete: false } && action == ModalDialogAction.Accept) return;
        if (selectionLabels is { Length: 0 } && action == ModalDialogAction.Accept) return;
        // Custom choices use the existing model's closing transition; preserve the chosen result.
        var customChoice = modalChoices?.Any(choice => choice.Action == action) == true;
        var outcome = modalDialog.Apply(customChoice && action != ModalDialogAction.Cancel ? ModalDialogAction.Accept : action);
        if (customChoice) outcome = action;
        if (outcome == ModalDialogAction.Stop)
        {
            optimizationCancellation?.Cancel();
            return;
        }
        if (!modalDialog.IsClosed) return;
        selectingUnderlineText = false;
        textInputService?.Stop();
        var completed = modalCompleted;
        modalDialog = null;
        modalCompleted = null;
        modalChoices = null;
        modalButtons.Clear();
        pressedModalButton = null;
        modalInputDrain = true;
        completed?.Invoke(outcome);
    }

    private ScreenRectangle ModalBounds()
    {
        var availableHeight = GraphicsDevice.Viewport.Height - (modalDialog?.Kind == ModalDialogKind.Text ? TextInputHelpHeight : 0);
        var large = selectionLabels is not null || viewerLines is not null || previewSheet is not null || exportColumnDraft is not null;
        var width = Math.Min(exportPlanChoices is not null ? 1200d : large ? 1000d : 720d, GraphicsDevice.Viewport.Width - 16d);
        var height = Math.Min(large ? 620d : 350d, Math.Max(1, availableHeight - 16d));
        return new ScreenRectangle((GraphicsDevice.Viewport.Width - width) / 2d,
            (availableHeight - height) / 2d, width, height);
    }

    private void EnsureModalButtons()
    {
        if (modalDialog is null) return;
        if (modalWidth == GraphicsDevice.Viewport.Width && modalHeight == GraphicsDevice.Viewport.Height) return;
        modalWidth = GraphicsDevice.Viewport.Width;
        modalHeight = GraphicsDevice.Viewport.Height;
        var initializeFocus = modalButtons.Count == 0;
        pressedModalButton = null;
        modalButtons.Clear();
        var bounds = ModalBounds();
        void Add(string label, ModalDialogAction action, double x, double y, double width) =>
            modalButtons.Add((new IconButtonModel(new ScreenRectangle(x, y, width, 38), label), action));
        var buttonWidth = Math.Min(130, (bounds.Width - 48) / 2);
        var right = bounds.X + bounds.Width - 20 - buttonWidth;
        var bottom = bounds.Y + bounds.Height - 58;
        if (modalChoices is { } choices)
        {
            var choiceWidth = (bounds.Width - 40 - 12 * (choices.Length - 1)) / choices.Length;
            for (var index = 0; index < choices.Length; index++)
                Add(choices[index].Label, choices[index].Action,
                    bounds.X + 20 + index * (choiceWidth + 12), bottom, choiceWidth);
            if (initializeFocus) modalFocus = selectionLabels is not null ? 1 : Math.Max(0, Array.FindIndex(choices, choice => choice.Action == ModalDialogAction.Cancel));
            return;
        }
        if (modalDialog.Kind == ModalDialogKind.Minutes)
        {
            Add("−", ModalDialogAction.Decrease, bounds.X + 20, bottom - 62, 44);
            Add("＋", ModalDialogAction.Increase, bounds.X + bounds.Width - 64, bottom - 62, 44);
        }
        if (modalDialog.Kind is ModalDialogKind.Confirmation or ModalDialogKind.Minutes or ModalDialogKind.Text)
            Add("キャンセル", ModalDialogAction.Cancel, right - buttonWidth - 12, bottom, buttonWidth);
        Add(modalDialog.Kind switch
        {
            ModalDialogKind.Confirmation => "削除する",
            ModalDialogKind.Minutes => "開始",
            ModalDialogKind.Progress => "ストップ",
            ModalDialogKind.Text => "確定",
            _ => "閉じる",
        }, modalDialog.Kind == ModalDialogKind.Progress ? ModalDialogAction.Stop : ModalDialogAction.Accept,
            right, bottom, buttonWidth);
        // Destructive confirmation defaults to Cancel.
        if (initializeFocus)
            modalFocus = modalDialog.Kind == ModalDialogKind.Text ? TextInputFocus
                : modalDialog.Kind == ModalDialogKind.Minutes ? 2 : 0;
    }

    private void DrawModalDialog()
    {
        if (modalDialog is null) return;
        EnsureModalButtons();
        var bounds = ModalBounds();
        DrawRectangle(new ScreenRectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 170));
        DrawRectangle(bounds, new Color(24, 29, 36));
        DrawOutline(bounds, 2, new Color(116, 145, 146));
        textRenderer?.Draw(modalDialog.Title, ToRectangle(new ScreenRectangle(bounds.X + 20, bounds.Y + 16, bounds.Width - 40, 32)), Color.White, 23, true);
        // Explicit newlines are preserved; long lines fit within the panel.
        var lines = modalDialog.Message.Replace("\r", "").Split('\n');
        var availableHeight = Math.Max(1, bounds.Height - (modalDialog.Kind == ModalDialogKind.Minutes ? 180 : 130));
        var lineHeight = Math.Min(30, availableHeight / Math.Max(1, lines.Length));
        for (var index = 0; index < lines.Length; index++)
            textRenderer?.Draw(lines[index], ToRectangle(new ScreenRectangle(bounds.X + 20, bounds.Y + 65 + index * lineHeight,
                bounds.Width - 40, lineHeight)), new Color(230, 236, 240), 18);
        if (modalDialog.Kind == ModalDialogKind.Minutes)
            textRenderer?.Draw($"{modalDialog.Minutes} 分（1～120）", ToRectangle(new ScreenRectangle(bounds.X + 78, bounds.Y + bounds.Height - 120,
                bounds.Width - 156, 38)), Color.White, 22, true);
        if (modalDialog.Kind == ModalDialogKind.Text) DrawUnderlineInput();
        DrawSelection();
        DrawTextViewer();
        DrawTablePreview();
        DrawExportColumns();
        for (var index = 0; index < modalButtons.Count; index++)
        {
            var button = modalButtons[index].Button;
            button.IsSelected = false;
            button.IsEnabled = backgroundOperation is null && !modalDialog.StopRequested && !((selectionLabels is { Length: 0 } || exportColumnDraft is { IsComplete: false }) && modalButtons[index].Action == ModalDialogAction.Accept);
            OperationButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) => textRenderer?.Draw(exportPlanChoices is { } choices && modalButtons[index].Action == ModalDialogAction.Accept && selectionIndex >= choices.Count
                    ? "未決定に戻す" : button.AccessibleName, ToRectangle(area, 5), ToButtonColor(color), 17, true));
            if (index == modalFocus && button.IsEnabled) DrawOutline(button.Bounds, 2, OperationTargetColor);
        }
        DrawTextInputHelp();
    }

    private void OpenOptimizationSettings()
    {
        var dialog = new ModalDialogModel(ModalDialogKind.Minutes, "サークル配置を自動最適化",
            "最適化する時間を指定してください。\nストップを押すと、その時点の最高案を採用します。\n時間は −／＋ ボタンまたは矢印キーで変更できます。");
        OpenModal(dialog, action =>
        {
            if (action == ModalDialogAction.Accept) StartOptimization(dialog.Minutes);
        });
    }

    private void StartOptimization(int minutes)
    {
        if (workspace is null || optimizationTask is not null) return;
        var project = workspace.Project;
        var planId = workspace.SelectedPlanId;
        optimizationCancellation = new CancellationTokenSource();
        var token = optimizationCancellation.Token;
        optimizationProgress.Clear();
        OpenModal(new ModalDialogModel(ModalDialogKind.Progress, "自動最適化", "配置案を最適化しています…"));
        optimizationTask = workspace.OptimizeAsync(minutes, optimizationProgress, token);
        Log("optimization_started", success: true);
    }

    private void PollOptimization()
    {
        if (optimizationTask is null) return;
        if (!optimizationTask.IsCompleted)
        {
            var progress = optimizationProgress.Value;
            if (modalDialog is not null)
                modalDialog.Message = (modalDialog.StopRequested ? "停止しています。最高案を確定中です…" : "配置案を最適化しています…") +
                    (progress is null ? "" : $"\n試行 {progress.Iteration:N0} 回　経過 {progress.Elapsed:mm\\:ss}\n一般 {progress.BestScore.GeneralAttendeeScore:F2}　サークル {progress.BestScore.CircleParticipantScore:F2}");
            return;
        }
        var completed = optimizationTask;
        optimizationTask = null;
        optimizationCancellation?.Dispose();
        optimizationCancellation = null;
        try
        {
            var completion = completed.GetAwaiter().GetResult();
            workspace!.Accept(completion.State);
            var result = WireJson.Read<CirclePlacementOptimizationResult>(completion.ResultJson);
            if (result.BestScore.CompareTo(result.InitialScore) <= 0)
            {
                ShowInAppMessage("自動最適化", "今回の試行では、開始時より評価を改善できませんでした。");
                Log("optimization_completed", success: true, detail: "not_improved");
                return;
            }
            var applied = CircleSpaceCoordinator.Desktop.Core.Interaction.EditorCommandResult.Success;
            ShowInAppMessage("自動最適化", applied.Applied
                ? $"最適化した配置案を新しく追加しました。\n開始時　一般 {result.InitialScore.GeneralAttendeeScore:F2}　サークル {result.InitialScore.CircleParticipantScore:F2}\n最高　一般 {result.BestScore.GeneralAttendeeScore:F2}　サークル {result.BestScore.CircleParticipantScore:F2}\n試行回数 {result.IterationCount:N0}"
                : "配置案を追加できませんでした。\n" + FormatIssues(applied.Issues));
            Log("optimization_completed", success: applied.Applied);
        }
        catch (Exception exception)
        {
            ShowInAppMessage("自動最適化エラー", exception.Message);
            Log("optimization_completed", success: false);
        }
    }

    private void DisposeOptimization()
    {
        optimizationCancellation?.Cancel();
        if (optimizationTask is { } task)
        {
            var cancellation = optimizationCancellation;
            _ = task.ContinueWith(completed =>
            {
                _ = completed.Exception;
                cancellation?.Dispose();
            }, TaskScheduler.Default);
        }
        else optimizationCancellation?.Dispose();
    }

    private sealed class LatestOptimizationProgress : IProgress<CirclePlacementOptimizationProgress>
    {
        private CirclePlacementOptimizationProgress? value;
        public CirclePlacementOptimizationProgress? Value => Volatile.Read(ref value);
        public void Clear() => Volatile.Write(ref value, null);
        public void Report(CirclePlacementOptimizationProgress progress) => Volatile.Write(ref value, progress);
    }
}
