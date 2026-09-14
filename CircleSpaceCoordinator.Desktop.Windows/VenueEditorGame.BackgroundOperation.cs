namespace CircleSpaceCoordinator.Desktop.Windows;

using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private Task? backgroundOperation;
    private Action? pollBackgroundOperation;

    private void RunBackground<T>(string title, Func<T> work, Action<T> completed, Action<Exception>? failed = null)
    {
        if (backgroundOperation is not null) return;
        var owner = workspace;
        var task = Task.Run(work);
        backgroundOperation = task;
        OpenModal(new ModalDialogModel(ModalDialogKind.Message, title, "処理中です。しばらくお待ちください。"));
        pollBackgroundOperation = () =>
        {
            if (!task.IsCompleted)
            {
                if (modalDialog is not null) modalDialog.Message = "処理中です" + new string('・', 1 + (int)(statusHintTime * 3) % 4);
                return;
            }
            backgroundOperation = null;
            pollBackgroundOperation = null;
            try
            {
                var result = task.GetAwaiter().GetResult();
                if (workspace != owner) throw new InvalidOperationException("処理対象のイベントが変わりました。");
                completed(result);
            }
            catch (Exception exception)
            {
                if (failed is not null) failed(exception); else ShowInAppMessage(title, exception.Message);
            }
        };
    }

    // File writes cannot be abandoned halfway through when the native window closes.
    private void FinishBackgroundOperation()
    {
        try { backgroundOperation?.GetAwaiter().GetResult(); }
        catch (Exception exception) { Log("background_operation_failed", false, exception.GetType().Name); }
        backgroundOperation = null;
        pollBackgroundOperation = null;
    }
}
