namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
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
    private string? packageReadNotice;
    private readonly int[] packageReadScroll = new int[2];
    private int packageReadFocus;
    private const int PackageReadRowHeight = 32;
    private const int PackageReadButtonCount = 8;
    // Application-specific action, intercepted before the shared modal model handles it.
    private const ModalDialogAction PackageRenameAction = (ModalDialogAction)100;
    private const ModalDialogAction PackageSelectFileAction = (ModalDialogAction)101;
    private const ModalDialogAction PackageCreateTableAction = (ModalDialogAction)102;

    private void OpenPackageReadDialog()
        => OpenPackageReadDialog(null, null);

    private void OpenGenrePackageReader(string? sourcePath, string? tableId = null)
    {
        projectMenuOpen = false;
        CancelInProgressPointerInteraction();
        OpenGenreStyleEditor();
        genrePageTab = 0;
        genreGridLayoutMode = GenreGridLayoutMode.SplitPane;
        mappingWidth = -1;
        OpenPackageReadDialog(sourcePath, tableId);
    }

    private void OpenPackageReadDialog(string? sourcePath, string? tableId)
    {
        genreCloseAfterDiscard = false;
        if (GenreSavePending && !SaveGenreScope(() => OpenPackageReadDialog(sourcePath, tableId))) return;
        if (GenrePackageChanged || genrePackageRequiresComment)
        {
            SyncMappingChangeTag();
            if (packageChangeTag?.CanClose != true)
            {
                ShowInAppMessage("パッケージの変更が残っています", "［パッケージへの変更コメント］を書いてから別の表を読み込むか、［パッケージへの変更を破棄］で元に戻してください。");
                return;
            }
            if (!SaveGenreScope()) return;
        }
        if (GenreSavePending && !SaveGenreScope()) return;
        SetMappingTextFocus(false);
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "パッケージ読込", ""), action =>
        {
            if (action == ModalDialogAction.Accept && packageReadDialog is { CanRead: true, SelectedJson: not null, SelectedPackage: not null } selection)
            {
                var table = selection.Tables[selection.TableIndex];
                void ReadTable()
                {
                    ShowPackageGenreTable(table);
                    AttachPackageGenreSaveSession(selection.Files[selection.FileIndex], selection.SelectedJson,
                        selection.SelectedPackage, table);
                }
                if (PackageGenreSaveSession.HasMissingComment(table))
                    OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "変更コメントの確認",
                        "変更コメントが未入力のデータです。\nどのような変更が行われたデータか分かりません。\n読み込みますか？"),
                        answer => { if (answer == ModalDialogAction.Accept) ReadTable(); },
                        [("キャンセル", ModalDialogAction.Cancel), ("読み込む", ModalDialogAction.Accept)]);
                else ReadTable();
            }
            packageReadDialog = null;
        }, [("フォルダー選択", ModalDialogAction.Increase), ("ファイル選択", PackageSelectFileAction),
            ("パッケージを新規作成", ModalDialogAction.Decrease), ("網掛け表を新規作成", PackageCreateTableAction),
            ("リネーム", PackageRenameAction), ("削除", ModalDialogAction.Stop),
            ("キャンセル", ModalDialogAction.Cancel), ("読取", ModalDialogAction.Accept)]);
        packageReadDialog = new(json => EditorConnection.Current.ParsePortable(json), MappingMaterialKind);
        packageReadFocus = 0;
        packageReadFolderError = null;
        packageReadNotice = null;
        Array.Clear(packageReadScroll);
        if (sourcePath is not null)
        {
            packageReadDialog.SelectPath(sourcePath, tableId);
            packageReadDirectory = packageReadDialog.DirectoryPath;
            packageReadScroll[0] = Math.Max(0, Math.Min(packageReadDialog.FileIndex,
                packageReadDialog.Files.Length - PackageReadPageSize));
            packageReadScroll[1] = Math.Max(0, Math.Min(packageReadDialog.TableIndex,
                packageReadDialog.Tables.Length - PackageReadPageSize));
        }
        else if (packageReadDirectory is { } directory) packageReadDialog.SetDirectory(directory);
    }

    private ScreenRectangle PackageReadListArea(int list)
    {
        var panel = ModalBounds();
        var width = (panel.Width - 56) / 2;
        return new(panel.X + 20 + list * (width + 16), panel.Y + 128, width, Math.Max(32, panel.Height - 326));
    }

    private int PackageReadPageSize => Math.Max(1, (int)(PackageReadListArea(0).Height / PackageReadRowHeight));

    private void ChoosePackageReadDirectory()
    {
        if (packageReadDialog is not { } model) return;
        packageReadFolderError = null;
        try
        {
            using var picker = new Forms.FolderBrowserDialog
            {
                Description = "パッケージの保存フォルダーを選択",
                UseDescriptionForTitle = true,
                SelectedPath = model.DirectoryPath,
                ShowNewFolderButton = true,
            };
            // DesktopGL's Window.Handle is an SDL_Window*, not a Windows HWND.
            if (picker.ShowDialog() != Forms.DialogResult.OK) return;
            var directory = Path.GetFullPath(picker.SelectedPath);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException("選択したフォルダーが見つかりません。");
            packageReadDirectory = directory;
            model.SetDirectory(directory);
            Array.Clear(packageReadScroll);
            packageReadNotice = null;
            packageReadFocus = PackageReadButtonCount;
            modalFocus = -1;
        }
        catch (Exception ex)
        {
            packageReadFolderError = "フォルダー選択を開けませんでした：" + ex.Message;
        }
        finally { modalInputDrain = true; }
    }

    private void ChoosePackageReadFile()
    {
        if (packageReadDialog is not { } model) return;
        packageReadFolderError = null;
        try
        {
            using var picker = new Forms.OpenFileDialog
            {
                Title = "パッケージファイルを選択",
                Filter = "パッケージ (*.package-csc.json)|*.package-csc.json",
                InitialDirectory = model.DirectoryPath,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                RestoreDirectory = true,
            };
            if (picker.ShowDialog() != Forms.DialogResult.OK) return;
            model.SelectPath(picker.FileName);
            packageReadDirectory = model.DirectoryPath;
            Array.Clear(packageReadScroll);
            packageReadScroll[0] = Math.Max(0, Math.Min(model.FileIndex, model.Files.Length - PackageReadPageSize));
            packageReadNotice = null;
            packageReadFocus = PackageReadButtonCount + 1;
            modalFocus = -1;
        }
        catch (Exception ex)
        {
            packageReadFolderError = "ファイルを選択できませんでした：" + ex.Message;
        }
        finally { modalInputDrain = true; }
    }

    private bool UpdatePackageReadDialog(KeyboardState keyboard, MouseState mouse)
    {
        if (packageReadDialog is not { } model) return false;
        // Footer buttons followed by the file list and the table list.
        if (IsPressed(keyboard, Keys.Tab))
        {
            var count = PackageReadButtonCount + 2;
            packageReadFocus = (packageReadFocus + (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? count - 1 : 1)) % count;
            modalFocus = packageReadFocus < PackageReadButtonCount ? packageReadFocus : -1;
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
                    packageReadFocus = list + PackageReadButtonCount;
                    modalFocus = -1;
                    var row = (int)((pointer.Y - area.Y) / PackageReadRowHeight);
                    var index = packageReadScroll[list] + row;
                    if (row < PackageReadPageSize && index < count) SelectPackageReadRow(list, index);
                }
            }
            if (packageReadFocus != list + PackageReadButtonCount || count == 0) continue;
            var selected = list == 0 ? model.FileIndex : model.TableIndex;
            var delta = IsPressed(keyboard, Keys.Down) ? 1 : IsPressed(keyboard, Keys.Up) ? -1
                : IsPressed(keyboard, Keys.PageDown) ? PackageReadPageSize : IsPressed(keyboard, Keys.PageUp) ? -PackageReadPageSize : 0;
            var next = IsPressed(keyboard, Keys.Home) ? 0 : IsPressed(keyboard, Keys.End) ? count - 1
                : delta != 0 ? Math.Clamp(selected + delta, 0, count - 1) : selected;
            if (next != selected || delta != 0 || IsPressed(keyboard, Keys.Home) || IsPressed(keyboard, Keys.End))
            {
                SelectPackageReadRow(list, next);
                packageReadScroll[list] = Math.Clamp(packageReadScroll[list], Math.Max(0, next - PackageReadPageSize + 1), Math.Max(0, next));
            }
        }
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            if (packageReadFocus < PackageReadButtonCount) ApplyModalAction(modalButtons[packageReadFocus].Action);
            else if (packageReadFocus == PackageReadButtonCount) model.TargetFile();
            else if (model.CanRead) model.SelectTable(model.TableIndex);
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
        if (list == 0)
        {
            if (index == model.FileIndex) model.TargetFile();
            else { model.SelectFile(index); packageReadScroll[1] = 0; }
        }
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
            textRenderer?.Draw(list == 0 ? "パッケージファイル" : "網掛け対応表",
                ToRectangle(new(area.X, area.Y - 30, area.Width, 28)), Color.White, 18, true);
            DrawRectangle(area, new Color(18, 23, 29));
            DrawOutline(area, 1, packageReadFocus == list + PackageReadButtonCount ? Color.White : Color.SlateGray);
            for (var row = 0; row < PackageReadPageSize && packageReadScroll[list] + row < count; row++)
            {
                var index = packageReadScroll[list] + row;
                var bounds = new ScreenRectangle(area.X + 2, area.Y + row * PackageReadRowHeight, area.Width - 4, PackageReadRowHeight - 2);
                if (index == (list == 0 ? model.FileIndex : model.TableIndex)) DrawRectangle(bounds, new Color(45, 95, 100));
                textRenderer?.Draw(list == 0 ? Path.GetFileName(model.Files[index]) : model.Tables[index].Name,
                    ToRectangle(bounds, 4), Color.White, 16, true);
                if (index == (list == 0 ? model.FileIndex : model.TableIndex) &&
                    model.OperationTarget == (list == 0 ? PackageReadTarget.File : PackageReadTarget.Table))
                    DrawOutline(bounds, 2, OperationTargetColor);
            }
            if (count == 0)
                textRenderer?.Draw(list == 0 ? "対象ファイルがありません" : model.FileIndex < 0 ? "ファイルを選んでください" : "対応する網掛け対応表がありません",
                    ToRectangle(new(area.X + 4, area.Y + 4, area.Width - 8, 32)), Color.LightGray, 15, true);
            textRenderer?.Draw($"{count} 件　↑↓・PageUp/PageDown・ホイール",
                ToRectangle(new(area.X, area.Y + area.Height + 4, area.Width, 24)), Color.LightGray, 12, true);
        }
        var error = packageReadFolderError ?? model.Error;
        var target = model.OperationTarget == PackageReadTarget.Table && model.CanRead ? "網掛け表：" + model.Tables[model.TableIndex].Name
            : model.OperationTarget == PackageReadTarget.File && model.FileIndex >= 0 ? "パッケージ：" + Path.GetFileName(model.Files[model.FileIndex]) : "なし";
        textRenderer?.Draw("背景色＝選択　水色枠＝操作対象（リネーム・削除）　" + target,
            ToRectangle(new(panel.X + 20, panel.Y + panel.Height - 164, panel.Width - 40, 24)), OperationTargetColor, 13, true);
        textRenderer?.Draw(error ?? packageReadNotice ?? "行をクリックして操作対象を指定。［読取］は選択中の表を読み込みます。",
            ToRectangle(new(panel.X + 20, panel.Y + panel.Height - 140, panel.Width - 40, 28)),
            error is null ? Color.LightGray : Color.Salmon, 14, true);
    }

    private void DrawPackageCreateTooltip()
    {
        if (!IsActive || packageReadDialog is not { } reader || Directory.Exists(reader.DirectoryPath)) return;
        var button = modalButtons.FirstOrDefault(item => item.Action == ModalDialogAction.Decrease).Button;
        var mouse = Mouse.GetState();
        if (button is null || !Contains(button.Bounds, new(mouse.X, mouse.Y))) return;
        const string message = "先にフォルダーを選択してください";
        var width = Math.Min((textRenderer?.Measure(message, 16).X ?? 280) + 20, GraphicsDevice.Viewport.Width - 8);
        var bounds = new ScreenRectangle(Math.Clamp(mouse.X + 12d, 4, Math.Max(4, GraphicsDevice.Viewport.Width - width - 4)),
            Math.Max(4, mouse.Y - 40), width, 32);
        DrawRectangle(bounds, new Color(24, 29, 36));
        DrawOutline(bounds, 1, Color.LightSlateGray);
        textRenderer?.Draw(message, ToRectangle(bounds, 6), Color.White, 16, true);
    }
}
