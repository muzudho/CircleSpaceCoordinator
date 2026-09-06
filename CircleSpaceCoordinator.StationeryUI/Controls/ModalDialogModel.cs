namespace CircleSpaceCoordinator.StationeryUI.Controls;

public enum ModalDialogKind { Message, Confirmation, Minutes, Progress, Text }
public enum ModalDialogAction { None, Accept, Cancel, Stop, Decrease, Increase }

/// <summary>画面内ダイアログの状態。描画や OS の入力 API は利用側が接続します。</summary>
public sealed class ModalDialogModel(ModalDialogKind kind, string title, string message)
{
    public ModalDialogKind Kind { get; } = kind;
    public string Title { get; } = title;
    public string Message { get; set; } = message;
    public int Minutes { get; private set; } = 10;
    public bool IsClosed { get; private set; }
    public bool StopRequested { get; private set; }

    public ModalDialogAction Apply(ModalDialogAction action)
    {
        if (IsClosed) return ModalDialogAction.None;
        if (Kind == ModalDialogKind.Progress)
        {
            if (StopRequested || action is not (ModalDialogAction.Stop or ModalDialogAction.Cancel))
                return ModalDialogAction.None;
            StopRequested = true;
            return ModalDialogAction.Stop;
        }
        if (Kind == ModalDialogKind.Minutes && action is ModalDialogAction.Decrease or ModalDialogAction.Increase)
        {
            Minutes = Math.Clamp(Minutes + (action == ModalDialogAction.Increase ? 1 : -1), 1, 120);
            return ModalDialogAction.None;
        }
        if (action is not (ModalDialogAction.Accept or ModalDialogAction.Cancel)) return ModalDialogAction.None;
        IsClosed = true;
        return action;
    }
}
