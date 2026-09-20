namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.EditorClient;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private PackageReadDialogModel? packageReadDialog;
    private string? packageReadDirectory;
    private string? packageReadFolderError;
    private readonly int[] packageReadScroll = new int[2];
    private int packageReadFocus;
    private const int PackageReadRowHeight = 32;

    private void OpenPackageReadDialog()
    {
        SetMappingTextFocus(false);
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "パッケージ読込", ""), action =>
        {
            if (action == ModalDialogAction.Accept && packageReadDialog is { CanRead: true } selection)
                ShowPackageGenreTable(selection.Tables[selection.TableIndex]);
            packageReadDialog = null;
        }, [("フォルダーを選ぶ", ModalDialogAction.Increase), ("キャンセル", ModalDialogAction.Cancel), ("読取", ModalDialogAction.Accept)]);
        packageReadDialog = new(json => EditorConnection.Current.ParsePortable(json));
        packageReadFocus = 0;
        packageReadFolderError = null;
        Array.Clear(packageReadScroll);
        if (packageReadDirectory is { } directory) packageReadDialog.SetDirectory(directory);
    }

    private ScreenRectangle PackageReadListArea(int list)
    {
        var panel = ModalBounds();
        var width = (panel.Width - 56) / 2;
        return new(panel.X + 20 + list * (width + 16), panel.Y + 128, width, Math.Max(32, panel.Height - 248));
    }

    private int PackageReadPageSize => Math.Max(1, (int)(PackageReadListArea(0).Height / PackageReadRowHeight));

    private void ChoosePackageReadDirectory()
    {
        if (packageReadDialog is not { } model) return;
        packageReadFolderError = null;
        try
        {
            using var picker = new Forms.OpenFileDialog
            {
                Title = "パッケージを選択、またはファイル名を変えずに［開く］で現在のフォルダーを選択",
                Filter = "パッケージ (*.package-csc.json)|*.package-csc.json",
                InitialDirectory = model.DirectoryPath,
                // A non-file placeholder also permits choosing an empty folder.
                FileName = "このフォルダーを選択",
                CheckFileExists = false,
                CheckPathExists = true,
                AddExtension = false,
                Multiselect = false,
                RestoreDirectory = true,
            };
            // DesktopGL's Window.Handle is an SDL_Window*, not a Windows HWND.
            // Match the existing native file pickers: let WinForms resolve the owner.
            if (picker.ShowDialog() != Forms.DialogResult.OK) return;
            // Preview a chosen package after opening its directory; do not import it.
            var selectedPath = Path.GetFullPath(picker.FileName);
            var directory = Directory.Exists(selectedPath) ? selectedPath : Path.GetDirectoryName(selectedPath);
            if (directory is null || !Directory.Exists(directory))
                throw new DirectoryNotFoundException("選択したフォルダーが見つかりません。");
            packageReadDirectory = directory;
            model.SetDirectory(directory);
            Array.Clear(packageReadScroll);
            var selectedIndex = Array.FindIndex(model.Files,
                path => string.Equals(Path.GetFullPath(path), selectedPath, StringComparison.OrdinalIgnoreCase));
            if (selectedIndex >= 0)
            {
                SelectPackageReadRow(0, selectedIndex);
                packageReadScroll[0] = Math.Min(selectedIndex, Math.Max(0, model.Files.Length - PackageReadPageSize));
                packageReadFocus = 4;
                modalFocus = -1;
            }
        }
        catch (Exception ex)
        {
            packageReadFolderError = "フォルダー選択を開けませんでした：" + ex.Message;
        }
        finally { modalInputDrain = true; }
    }

    private bool UpdatePackageReadDialog(KeyboardState keyboard, MouseState mouse)
    {
        if (packageReadDialog is not { } model) return false;
        // Three footer buttons followed by the file list and the table list.
        if (IsPressed(keyboard, Keys.Tab))
        {
            packageReadFocus = (packageReadFocus + (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? 4 : 1)) % 5;
            modalFocus = packageReadFocus < 3 ? packageReadFocus : -1;
            return true;
        }
        if (IsPressed(keyboard, Keys.Escape)) { ApplyModalAction(ModalDialogAction.Cancel); return true; }
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        for (var list = 0; list < 2; list++)
        {
            var area = PackageReadListArea(list);
            var count = list == 0 ? model.Files.Length : model.Tables.Length;
            if (Contains(area, pointer))
            {
                var wheel = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
                if (wheel != 0) packageReadScroll[list] = Math.Clamp(packageReadScroll[list] - Math.Sign(wheel) * 3, 0, Math.Max(0, count - PackageReadPageSize));
                if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
                {
                    packageReadFocus = list + 3;
                    modalFocus = -1;
                    var row = (int)((pointer.Y - area.Y) / PackageReadRowHeight);
                    var index = packageReadScroll[list] + row;
                    if (row < PackageReadPageSize && index < count) SelectPackageReadRow(list, index);
                }
            }
            if (packageReadFocus != list + 3 || count == 0) continue;
            var selected = list == 0 ? model.FileIndex : model.TableIndex;
            var delta = IsPressed(keyboard, Keys.Down) ? 1 : IsPressed(keyboard, Keys.Up) ? -1
                : IsPressed(keyboard, Keys.PageDown) ? PackageReadPageSize : IsPressed(keyboard, Keys.PageUp) ? -PackageReadPageSize : 0;
            var next = IsPressed(keyboard, Keys.Home) ? 0 : IsPressed(keyboard, Keys.End) ? count - 1
                : delta != 0 ? Math.Clamp(selected + delta, 0, count - 1) : selected;
            if (next != selected)
            {
                SelectPackageReadRow(list, next);
                packageReadScroll[list] = Math.Clamp(packageReadScroll[list], Math.Max(0, next - PackageReadPageSize + 1), Math.Max(0, next));
            }
        }
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            if (packageReadFocus < 3) ApplyModalAction(modalButtons[packageReadFocus].Action);
            else if (packageReadFocus == 3) { packageReadFocus = 4; modalFocus = -1; }
            else ApplyModalAction(ModalDialogAction.Accept);
            return true;
        }
        foreach (var (button, _) in modalButtons) button.UpdatePointer(pointer);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedModalButton = modalButtons.Select(item => item.Button).FirstOrDefault(button => button.Press(pointer));
            if (pressedModalButton is not null)
                modalFocus = packageReadFocus = modalButtons.FindIndex(item => item.Button == pressedModalButton);
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedModalButton;
            pressedModalButton = null;
            if (pressed?.Release(pointer) == true) ApplyModalAction(modalButtons.Single(item => item.Button == pressed).Action);
        }
        return true;
    }

    private void SelectPackageReadRow(int list, int index)
    {
        if (packageReadDialog is not { } model) return;
        if (list == 0) { model.SelectFile(index); packageReadScroll[1] = 0; }
        else model.SelectTable(index);
    }

    private void DrawPackageReadDialog()
    {
        if (packageReadDialog is not { } model) return;
        var panel = ModalBounds();
        textRenderer?.Draw(model.DirectoryPath.Length == 0 ? "フォルダーを選んでください" : model.DirectoryPath,
            ToRectangle(new(panel.X + 20, panel.Y + 58, panel.Width - 40, 32)), Color.LightGray, 16, true);
        for (var list = 0; list < 2; list++)
        {
            var area = PackageReadListArea(list);
            var count = list == 0 ? model.Files.Length : model.Tables.Length;
            packageReadScroll[list] = Math.Clamp(packageReadScroll[list], 0, Math.Max(0, count - PackageReadPageSize));
            textRenderer?.Draw(list == 0 ? "パッケージファイル" : "ジャンルコード表",
                ToRectangle(new(area.X, area.Y - 30, area.Width, 28)), Color.White, 18, true);
            DrawRectangle(area, new Color(18, 23, 29));
            DrawOutline(area, 1, packageReadFocus == list + 3 ? OperationTargetColor : Color.SlateGray);
            for (var row = 0; row < PackageReadPageSize && packageReadScroll[list] + row < count; row++)
            {
                var index = packageReadScroll[list] + row;
                var bounds = new ScreenRectangle(area.X + 2, area.Y + row * PackageReadRowHeight, area.Width - 4, PackageReadRowHeight - 2);
                if (index == (list == 0 ? model.FileIndex : model.TableIndex)) DrawRectangle(bounds, new Color(45, 95, 100));
                textRenderer?.Draw(list == 0 ? Path.GetFileName(model.Files[index]) : model.Tables[index].Name,
                    ToRectangle(bounds, 4), Color.White, 16, true);
            }
            if (count == 0)
                textRenderer?.Draw(list == 0 ? "対象ファイルがありません" : model.FileIndex < 0 ? "ファイルを選んでください" : "ジャンルコード表がありません",
                    ToRectangle(new(area.X + 4, area.Y + 4, area.Width - 8, 32)), Color.LightGray, 15, true);
            textRenderer?.Draw($"{count} 件　↑↓・PageUp/PageDown・ホイール",
                ToRectangle(new(area.X, area.Y + area.Height + 4, area.Width, 24)), Color.LightGray, 12, true);
        }
        var error = packageReadFolderError ?? model.Error;
        textRenderer?.Draw(error ?? "ファイルと表を選んで［読取］を押してください。",
            ToRectangle(new(panel.X + 20, panel.Y + panel.Height - 90, panel.Width - 40, 28)),
            error is null ? Color.LightGray : Color.Salmon, 14, true);
    }
}
