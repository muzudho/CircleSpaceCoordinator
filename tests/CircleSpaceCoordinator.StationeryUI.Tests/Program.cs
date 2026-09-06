namespace CircleSpaceCoordinator.StationeryUI.Tests;

using CircleSpaceCoordinator.StationeryUI.Canvas;
using CircleSpaceCoordinator.StationeryUI.Controls;
using CircleSpaceCoordinator.StationeryUI.Text;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Screen positions map to grid cells", ScreenPositionsMapToCells),
            ("Panning moves cell bounds", PanningMovesCellBounds),
            ("Zooming preserves the anchored world position", ZoomPreservesAnchor),
            ("A saved viewport can be restored", SavedViewportCanBeRestored),
            ("Invalid viewport values are rejected", InvalidValuesAreRejected),
            ("Icon buttons track hover press and click", IconButtonTracksPointer),
            ("Disabled icon buttons cannot be clicked", DisabledIconButtonCannotClick),
            ("Dialog cancellation cannot later confirm", DialogCancellationIsFinal),
            ("Optimization minutes stay in the allowed range", DialogMinutesAreBounded),
            ("Stopping progress keeps the modal open until completion", ProgressStopKeepsModalOpen),
            ("Underline editing replaces selection and supports undo", UnderlineSelectionAndUndo),
            ("Underline editing keeps Unicode text elements intact", UnderlineUnicode),
            ("Underline input is single line and length limited", UnderlineLengthLimit),
        };
        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"PASS: {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL: {test.Name}");
                Console.Error.WriteLine(exception.Message);
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void ScreenPositionsMapToCells()
    {
        var viewport = new GridViewport(20d);
        AssertEqual(new GridCellAddress(0, 0), viewport.ScreenToCell(new ScreenPoint(0d, 0d)));
        AssertEqual(new GridCellAddress(1, 2), viewport.ScreenToCell(new ScreenPoint(39.9d, 40d)));
        AssertEqual(new GridCellAddress(-1, -1), viewport.ScreenToCell(new ScreenPoint(-0.1d, -0.1d)));
    }

    private static void PanningMovesCellBounds()
    {
        var viewport = new GridViewport(20d);
        viewport.PanBy(5d, 7d);
        AssertEqual(new ScreenRectangle(25d, 27d, 20d, 20d), viewport.GetCellBounds(new GridCellAddress(1, 1)));
    }

    private static void ZoomPreservesAnchor()
    {
        var viewport = new GridViewport(20d);
        var anchor = new ScreenPoint(30d, 50d);
        var cellBefore = viewport.ScreenToCell(anchor);
        viewport.ZoomAt(anchor, 2d);

        AssertEqual(cellBefore, viewport.ScreenToCell(anchor));
        AssertEqual(40d, viewport.CellSize);
        AssertEqual(new ScreenPoint(-30d, -50d), viewport.Origin);
    }

    private static void InvalidValuesAreRejected()
    {
        AssertThrows<ArgumentOutOfRangeException>(() => new GridViewport(0d));
        var viewport = new GridViewport(20d);
        AssertThrows<ArgumentOutOfRangeException>(() => viewport.ZoomAt(new ScreenPoint(), 5d));
        AssertThrows<ArgumentOutOfRangeException>(() => viewport.PanBy(double.NaN, 0d));
    }

    private static void SavedViewportCanBeRestored()
    {
        var viewport = new GridViewport(20d);
        viewport.SetView(1.75d, new ScreenPoint(-123d, 456d));

        AssertEqual(1.75d, viewport.Zoom);
        AssertEqual(new ScreenPoint(-123d, 456d), viewport.Origin);
    }

    private static void IconButtonTracksPointer()
    {
        var button = new IconButtonModel(new ScreenRectangle(10d, 20d, 40d, 40d), "Undo");
        button.UpdatePointer(new ScreenPoint(15d, 25d));
        AssertEqual(true, button.IsPointerOver);
        AssertEqual(true, button.Press(new ScreenPoint(15d, 25d)));
        AssertEqual(true, button.IsPressed);
        AssertEqual(true, button.Release(new ScreenPoint(15d, 25d)));
        AssertEqual(false, button.IsPressed);
    }

    private static void DisabledIconButtonCannotClick()
    {
        var button = new IconButtonModel(new ScreenRectangle(0d, 0d, 40d, 40d), "Redo")
        {
            IsEnabled = false,
        };
        AssertEqual(false, button.Press(new ScreenPoint(10d, 10d)));
        AssertEqual(false, button.Release(new ScreenPoint(10d, 10d)));
        AssertEqual(false, button.IsPointerOver);
        AssertEqual(true, button.Contains(new ScreenPoint(10d, 10d)));
    }

    private static void DialogCancellationIsFinal()
    {
        var dialog = new ModalDialogModel(ModalDialogKind.Confirmation, "Delete", "Confirm?");
        AssertEqual(ModalDialogAction.Cancel, dialog.Apply(ModalDialogAction.Cancel));
        AssertEqual(true, dialog.IsClosed);
        AssertEqual(ModalDialogAction.None, dialog.Apply(ModalDialogAction.Accept));
    }

    private static void DialogMinutesAreBounded()
    {
        var dialog = new ModalDialogModel(ModalDialogKind.Minutes, "Optimize", "Minutes");
        AssertEqual(10, dialog.Minutes);
        for (var index = 0; index < 150; index++) dialog.Apply(ModalDialogAction.Decrease);
        AssertEqual(1, dialog.Minutes);
        for (var index = 0; index < 150; index++) dialog.Apply(ModalDialogAction.Increase);
        AssertEqual(120, dialog.Minutes);
        AssertEqual(false, dialog.IsClosed);
        AssertEqual(ModalDialogAction.Accept, dialog.Apply(ModalDialogAction.Accept));
    }

    private static void ProgressStopKeepsModalOpen()
    {
        var dialog = new ModalDialogModel(ModalDialogKind.Progress, "Optimize", "Running");
        AssertEqual(ModalDialogAction.None, dialog.Apply(ModalDialogAction.Accept));
        AssertEqual(ModalDialogAction.Stop, dialog.Apply(ModalDialogAction.Cancel));
        AssertEqual(true, dialog.StopRequested);
        AssertEqual(false, dialog.IsClosed);
        AssertEqual(ModalDialogAction.None, dialog.Apply(ModalDialogAction.Stop));
    }

    private static void UnderlineSelectionAndUndo()
    {
        var editor = new UnderlineTextEditor("以前の配置");
        editor.Insert("新しい配置");
        AssertEqual("新しい配置", editor.Text);
        editor.Undo();
        AssertEqual("以前の配置", editor.Text);
        editor.Redo();
        AssertEqual("新しい配置", editor.Text);
        editor.Move(-1, true);
        AssertEqual("置", editor.SelectedText);
        editor.Delete(false);
        AssertEqual("新しい配", editor.Text);
    }

    private static void UnderlineUnicode()
    {
        var editor = new UnderlineTextEditor("A😀か\u3099");
        editor.MoveTo(editor.Text.Length);
        editor.Delete(true);
        AssertEqual("A😀", editor.Text);
        editor.Delete(true);
        AssertEqual("A", editor.Text);
        editor.Undo();
        AssertEqual("A😀", editor.Text);
    }

    private static void UnderlineLengthLimit()
    {
        var editor = new UnderlineTextEditor("", 3);
        editor.Insert("A😀B");
        AssertEqual("A😀", editor.Text);
        editor.SelectAll();
        editor.Insert("B\r\nC\tD");
        AssertEqual("BCD", editor.Text);
        var small = new UnderlineTextEditor("", 1);
        small.Insert("😀");
        AssertEqual("", small.Text);
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
        }
        catch (TException)
        {
        }
    }

    private static void AssertEqual<T>(T expected, T actual)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', actual '{actual}'.");
    }
}
