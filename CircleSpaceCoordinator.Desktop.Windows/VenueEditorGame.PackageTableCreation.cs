namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Text;

public sealed partial class VenueEditorGame
{
    private sealed class PackageTableForm
    {
        public UnderlineTextEditor Name = null!;
        public UnderlineTextEditor Comment = null!;
        public bool CommentFocused;
    }
    private PackageTableForm? packageTableForm;

    private void CreateTableFromReader()
    {
        if (packageReadDialog is not { SelectedPackage: { } package, SelectedJson: { } json, FileIndex: >= 0 } reader || !EnsureHandle()) return;
        var path = reader.Files[reader.FileIndex];
        var selectedId = reader.CanRead ? reader.Tables[reader.TableIndex].Id : null;
        var kind = MappingMaterialKind;
        var defaultName = "新しい網掛け表";
        for (var suffix = 2; package.Materials.Any(table => table.Kind == kind && table.Name == defaultName); suffix++)
            defaultName = "新しい網掛け表_" + suffix;
        void ShowForm(string initialName, string initialComment)
        {
            var form = new PackageTableForm { Comment = new(initialComment, 1000) };
            string? Validate(string _)
            {
                try
                {
                    var name = GenreStyleDefinition.NormalizeTableName(form.Name.Text);
                    GenreStyleDefinition.NormalizeKnowledgeComment(form.Comment.Text);
                    return package.Materials.Any(table => table.Kind == kind && table.Name == name)
                        ? "同じ名前の網掛け表が既にあります。" : null;
                }
                catch (ArgumentException ex) { return ex.Message; }
            }
            OpenUnderlineInput("網掛け表を新規作成", initialName, _ =>
            {
                try
                {
                    var updated = EditorConnection.Current.CreatePortableShadingTable(new(json, kind,
                        form.Name.Text, form.Comment.Text, Handle, WorkDate));
                    var parsed = EditorConnection.Current.ParsePortable(updated);
                    var ids = package.Materials.Select(table => table.Id).ToHashSet(StringComparer.Ordinal);
                    var created = parsed.Materials.Single(table => !ids.Contains(table.Id));
                    PortableLibraryService.SaveMetadata(new(path, json, package, null), updated);
                    ShowPackageGenreTable(created);
                    AttachPackageGenreSaveSession(path, updated, parsed, created);
                }
                catch (Exception ex)
                {
                    ShowNotice("網掛け表を作成できません", ex.Message, () => ShowForm(form.Name.Text, form.Comment.Text));
                }
            }, $"パッケージ：{package.Name}", maxLength: 1000,
                validate: Validate, cancelled: () => OpenPackageReadDialog(path, selectedId), acceptLabel: "作成");
            form.Name = underlineEditor!;
            packageTableForm = form;
        }
        ShowForm(defaultName, "");
    }

    private ScreenRectangle PackageTableInputBounds(bool comment)
    {
        var panel = ModalBounds();
        var nameY = panel.Y + 130;
        var commentY = panel.Y + Math.Max(195, panel.Height - 165);
        return new(panel.X + 20, comment ? commentY : nameY, panel.Width - 40, 42);
    }

    private bool UpdatePackageTableFormFocus(KeyboardState keyboard, MouseState mouse)
    {
        if (packageTableForm is not { } form || compositionText.Length > 0) return false;
        void FocusField(bool comment)
        {
            form.CommentFocused = comment;
            underlineEditor = comment ? form.Comment : form.Name;
            modalFocus = TextInputFocus;
            selectingUnderlineText = false;
            ResetUnderlineKeyRepeat();
            textInputService?.Start();
        }
        if (IsPressed(keyboard, Keys.Tab))
        {
            var count = modalButtons.Count + 2;
            var current = modalFocus < 0 ? (form.CommentFocused ? 1 : 0) : modalFocus + 2;
            var reverse = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            var next = (current + (reverse ? count - 1 : 1)) % count;
            if (next < 2) FocusField(next == 1);
            else { modalFocus = next - 2; textInputService?.Stop(); }
            return true;
        }
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released &&
            Contains(PackageTableInputBounds(!form.CommentFocused), new(mouse.X, mouse.Y)))
        {
            FocusField(!form.CommentFocused);
            underlineEditor!.MoveTo(UnderlineCaretIndex(underlineEditor.Text, mouse.X), false);
            return true;
        }
        return false;
    }

    private void DrawPackageTableForm()
    {
        if (packageTableForm is not { } form) return;
        foreach (var comment in new[] { false, true })
        {
            var bounds = PackageTableInputBounds(comment);
            textRenderer?.Draw(comment ? "コメント（任意）" : "網掛け表の名前", ToRectangle(new(bounds.X, bounds.Y - 28, bounds.Width, 24)), Color.White, 17, true);
            if (comment == form.CommentFocused) continue;
            textRenderer?.Draw((comment ? form.Comment : form.Name).Text, ToRectangle(bounds), Color.White, 22);
            DrawLine(new(bounds.X, bounds.Y + bounds.Height), new(bounds.X + bounds.Width, bounds.Y + bounds.Height), 1, new Color(99, 223, 185));
        }
    }
}
