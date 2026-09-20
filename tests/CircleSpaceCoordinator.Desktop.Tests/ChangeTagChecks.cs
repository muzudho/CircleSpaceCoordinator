namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Application.Layouts;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Json;
using StationeryUI.Controls;

internal static partial class Program
{
    private static void ChangeTagEditing()
    {
        string? Validate(string value) => PersonCredits.GetChangeLogValidationError(value);
        var validationExceptions = 0;
        var ownerThread = Environment.CurrentManagedThreadId;
        void OnException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs args)
        {
            if (Environment.CurrentManagedThreadId == ownerThread) validationExceptions++;
        }
        AppDomain.CurrentDomain.FirstChanceException += OnException;
        try
        {
            for (var i = 0; i < 1000; i++)
            {
                AssertEqual(true, Validate("") is not null);
                AssertEqual(true, Validate("　 ") is not null);
                AssertEqual(true, Validate("\n") is not null);
                AssertEqual(true, Validate("\uD800") is not null);
                AssertEqual(true, Validate("し") is null);
            }
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= OnException; }
        AssertEqual(0, validationExceptions);
        var input = new ChangeTagEditor(Validate);
        AssertEqual(true, input.CanClose);
        input.HasChanges = true;
        AssertEqual(false, input.CanClose);
        input.Insert("   ");
        AssertEqual(false, input.CanClose);
        input.Editor.SelectAll();
        input.Insert(string.Concat(Enumerable.Repeat("😀", 1000)));
        AssertEqual(1000, input.CharacterCount);
        AssertEqual(true, input.CanClose);
        AssertEqual(2000, input.Editor.Text.Length);
        var range = input.VisibleRange(10, text => text.EnumerateRunes().Count());
        AssertEqual((1980, 2000), range);
        input.Editor.MoveTo(0);
        AssertEqual((0, 20), input.VisibleRange(10, text => text.EnumerateRunes().Count()));
        input.Editor.MoveTo(input.Editor.Text.Length);
        input.Insert("a");
        AssertEqual(false, input.CanClose);
        input.Editor.Undo(); input.Edited();
        AssertEqual(true, input.CanClose);
        AssertEqual(false, input.Insert("\ninvalid"));
        AssertEqual(1000, input.CharacterCount);
        AssertEqual(false, input.CanClose);
        input.HasChanges = false;
        AssertEqual(true, input.CanClose);
        input.HasChanges = true;
        input.Editor.SelectAll(); input.Insert("  色を青に変更  ");
        var view = new ChangeTagEditorView();
        var drawn = new List<(string Text, StationeryUI.Canvas.ScreenRectangle Bounds)>();
        view.Draw(input, new(20, 502, 790, 151), 1, true, false, "by test since 2001-02-03", "",
            text => text.Length * 10, (text, bounds, _, _) => drawn.Add((text, bounds)), (_, _) => { });
        AssertEqual(true, drawn.Any(item => item.Text == "(change)"));
        AssertEqual(true, view.CaretBounds.X < view.BadgeBounds.X);
        AssertEqual(true, drawn.Any(item => item.Text.Contains("/ 1000", StringComparison.Ordinal)));
        var empty = new ChangeTagEditor(Validate) { HasChanges = true };
        drawn.Clear();
        view.Draw(empty, new(20, 502, 824, 98), 1, true, false, "author", "",
            text => text.Length * 10, (text, bounds, _, _) => drawn.Add((text, bounds)), (_, _) => { });
        AssertEqual(false, empty.CanClose);
        AssertEqual(1, drawn.Count(item => item.Text.Contains("入力してください", StringComparison.Ordinal)));
        AssertEqual(true, drawn.All(item => item.Bounds.Y < view.InputBounds.Y + view.InputBounds.Height));
        // Resized side-by-side tags use the same current geometry for painting and pointer hits.
        foreach (var scale in new[] { .75, 1.0, 1.25 })
        foreach (var x in new[] { 20.0, 648.0 })
        {
            var panel = new StationeryUI.Canvas.ScreenRectangle(x, 502, 600, 98 * scale);
            var hit = ChangeTagEditorView.GetInputBounds(panel, scale, 16);
            var badgeHit = ChangeTagEditorView.GetBadgeBounds(panel, scale, 16);
            drawn.Clear();
            view.Draw(empty, panel, scale, true, true, "author", "", text => text.Length * 10,
                (text, bounds, _, _) => drawn.Add((text, bounds)), (_, _) => { }, actionAreaWidth: 16);
            AssertEqual(hit, view.InputBounds);
            AssertEqual(badgeHit, drawn.Single(item => item.Text == "(change)").Bounds);
            var placeholder = drawn.Single(item => item.Text.Contains("入力してください", StringComparison.Ordinal)).Bounds;
            AssertEqual(hit.X, placeholder.X);
            AssertEqual(hit.Y, placeholder.Y);
            AssertEqual(true, hit.X >= panel.X && hit.X + hit.Width <= panel.X + panel.Width);
            AssertEqual(true, badgeHit.X + badgeHit.Width <= hit.X + hit.Width);
        }
        // A preedit replaces the selected span visually, at the insertion point;
        // it must not change the committed log or appear in the help row.
        var ime = new ChangeTagEditor(Validate) { HasChanges = true };
        ime.Insert("前の色です");
        ime.Editor.MoveTo(1); ime.Editor.MoveTo(3, extend: true);
        drawn.Clear();
        view.Draw(ime, new(20, 502, 824, 152), 1, true, false, "author", "新しい色",
            text => text.EnumerateRunes().Count() * 14, (text, bounds, _, _) => drawn.Add((text, bounds)), (_, _) => { });
        AssertEqual("前の色です", ime.Editor.Text);
        AssertEqual(true, drawn.Any(item => item.Text == "前新しい色です" && item.Bounds.Y == view.InputBounds.Y));
        AssertEqual(view.InputBounds.X + 14, view.CaretBounds.X);
        AssertEqual(view.CaretBounds.X, view.CompositionBounds.X);
        AssertEqual(56d, view.CompositionBounds.Width);
        AssertEqual(false, drawn.Any(item => item.Text == "新しい色"));
        // Preedit scrolling stays within the field and preserves supplementary characters.
        ime.Editor.SelectAll(); ime.Insert(new string('a', 1000));
        drawn.Clear();
        view.Draw(ime, new(20, 502, 824, 152), 1, true, false, "author", "😀漢字",
            text => text.EnumerateRunes().Count() * 14, (text, bounds, _, _) => drawn.Add((text, bounds)), (_, _) => { });
        AssertEqual(1000, ime.Editor.Text.Length);
        AssertEqual(true, drawn.Any(item => item.Text.EndsWith("😀漢字", StringComparison.Ordinal) && item.Bounds.Y == view.InputBounds.Y));
        AssertEqual(true, view.CompositionBounds.X + view.CompositionBounds.Width < view.BadgeBounds.X);
        AssertEqual(true, input.TryBeginSave(out var log));
        AssertEqual("色を青に変更", log!);
        AssertEqual(false, input.TryBeginSave(out _));
        input.SaveFailed("disk full");
        AssertEqual("色を青に変更", input.Text);
        AssertEqual(true, input.CanClose);
        AssertEqual(true, input.TryBeginSave(out _));
        foreach (var invalid in new[] { "", "  ", "log\n", "\tlog", "log\u2028tail", "\ud800", new string('a', 1001) })
            RejectPortable(() => PersonCredits.NormalizeChangeLog(invalid));
    }

    private static void ChangeTagPackageRoundTrip()
    {
        using var workspace = EditorConnection.Current.Open(ProjectJsonSerializer.Save(PortableExample()));
        var date = new DateOnly(2001, 2, 3);
        var styles = new[] { new GenreStyleDefinition("G", "blue", "white", "solid") { KnowledgeComment = "アクションRPGを含む" } };
        var oldCredits = workspace.Project.GenreStyleCredits;
        workspace.Execute(new SetGenreStyles(styles) { ActorHandle = "editor", WorkDate = date, ChangeLog = "色を青に変更",
            UpdateOverallComment = true, OverallComment = "RPGとアクションが２大勢力" }, selectedPlanEdit: false);
        var credits = workspace.Project.GenreStyleCredits!;
        AssertEqual("色を青に変更", credits.ChangeLog!);
        AssertEqual("editor", credits.Modifier!);
        AssertEqual(date, credits.ModifiedOn!.Value);
        AssertEqual(credits, ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project)).GenreStyleCredits);
        AssertEqual("アクションRPGを含む", ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project)).GenreStyles.Single().KnowledgeComment!);
        AssertEqual("RPGとアクションが２大勢力", ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project)).GenreStyleComment!);
        workspace.Undo();
        AssertEqual(oldCredits, workspace.Project.GenreStyleCredits);
        workspace.Redo();
        AssertEqual(credits, workspace.Project.GenreStyleCredits);
        workspace.Execute(new SetGenreStyles(styles.ToArray()) { ActorHandle = "other", WorkDate = date.AddDays(1), ChangeLog = "no change" }, selectedPlanEdit: false);
        AssertEqual(credits, workspace.Project.GenreStyleCredits);
        RejectPortable(() => workspace.Execute(new SetGenreStyles([styles[0] with { PrimaryColor = "red" }])
            { ActorHandle = "other", WorkDate = date, ChangeLog = "\n" }, selectedPlanEdit: false));
        AssertEqual(credits, workspace.Project.GenreStyleCredits);

        PortableMaterialSelection[] selections = [new("genre-styles", "project"), new("block-styles", "project")];
        var definitions = new SpaceDefinitionCatalog([], []);
        var json = EditorConnection.Current.ExportPortable(new(workspace.Project, [], definitions, "styles", "", [], false)
            { Materials = selections, Handle = "provider" });
        var package = EditorConnection.Current.ParsePortable(json);
        AssertEqual(2, package.Materials.Count);
        var material = package.Materials.Single(item => item.Kind == "genre-styles");
        AssertEqual("provider", material.Credits!.FirstProvider!);
        AssertEqual("editor", material.Credits.Modifier!);
        AssertEqual(credits.ChangeLog!, material.Credits.ChangeLog!);
        AssertEqual(date, material.Credits.ModifiedOn!.Value);
        var recorded = PortableCreditsService.Record(workspace.Project, package, selections, definitions);
        AssertEqual(material.Credits, recorded.GenreStyleCredits);
        using var receiver = EditorConnection.Current.Open(ProjectJsonSerializer.Save(PortableExample()));
        receiver.HandleProvider = () => "receiver";
        var before = receiver.Project.GenreStyleCredits;
        receiver.Execute(new ImportPortableSelection(package, package.Materials.Select(m => new PortableImportItem(m.Id, Guid.NewGuid().ToString(), m.Name)).ToArray()), selectedPlanEdit: false);
        AssertEqual(material.Credits, receiver.Project.GenreStyleCredits);
        AssertEqual("blue", receiver.Project.GenreStyles.Single().PrimaryColor);
        AssertEqual("アクションRPGを含む", receiver.Project.GenreStyles.Single().KnowledgeComment!);
        AssertEqual("RPGとアクションが２大勢力", receiver.Project.GenreStyleComment!);
        receiver.Undo(); AssertEqual(before, receiver.Project.GenreStyleCredits);
        receiver.Redo(); AssertEqual(material.Credits, receiver.Project.GenreStyleCredits);
        receiver.Execute(new SetGenreStyles([styles[0] with { PrimaryColor = "red" }])
            { ActorHandle = "receiver", WorkDate = date.AddDays(1), ChangeLog = "色を赤に変更" }, selectedPlanEdit: false);
        AssertEqual("receiver", receiver.Project.GenreStyleCredits!.Modifier!);
        AssertEqual("色を赤に変更", receiver.Project.GenreStyleCredits.ChangeLog!);
        AssertEqual("provider", receiver.Project.GenreStyleCredits.FirstProvider!);
        // Metadata-free legacy files remain readable; no previous log is invented.
        AssertEqual<string?>(null, new PersonCredits().ChangeLog);
        RejectPortable(() => new PersonCredits { ChangeLog = "missing author and date" }.Validate());
        RejectPortable(() => (material with { BlockStyles = [] }).Validate());
        RejectPortable(() => (material with { GenreStyles = [styles[0], styles[0]] }).Validate());
        RejectPortable(() => (material with { GenreStyles = [styles[0] with { KnowledgeComment = new string('a', 1001) }] }).Validate());
        var beforeCommentEdit = receiver.Project.GenreStyles.Single();
        receiver.Execute(new SetGenreStyles([beforeCommentEdit with { KnowledgeComment = "RPG全般" }])
            { ActorHandle = "comment-editor", WorkDate = date.AddDays(2), ChangeLog = "範囲の説明を修正" }, selectedPlanEdit: false);
        AssertEqual("comment-editor", receiver.Project.GenreStyleCredits!.Modifier!);
        AssertEqual("RPG全般", receiver.Project.GenreStyles.Single().KnowledgeComment!);
        receiver.Undo();
        AssertEqual(beforeCommentEdit, receiver.Project.GenreStyles.Single());
        RejectPortable(() => receiver.Execute(new SetGenreStyles([beforeCommentEdit with { KnowledgeComment = new string('a', 1001) }]), selectedPlanEdit: false));
        AssertEqual(beforeCommentEdit, receiver.Project.GenreStyles.Single());
        var beforeOverallCredits = receiver.Project.GenreStyleCredits;
        receiver.Execute(new SetGenreStyles(receiver.Project.GenreStyles)
        { UpdateOverallComment = true, OverallComment = "テーブルを新設。", ActorHandle = "overview-editor", WorkDate = date, ChangeLog = "全体の傾向を更新" }, selectedPlanEdit: false);
        AssertEqual("overview-editor", receiver.Project.GenreStyleCredits!.Modifier!);
        AssertEqual("テーブルを新設。", receiver.Project.GenreStyleComment!);
        receiver.Undo();
        AssertEqual(beforeOverallCredits, receiver.Project.GenreStyleCredits);
        AssertEqual("RPGとアクションが２大勢力", receiver.Project.GenreStyleComment!);
        RejectPortable(() => receiver.Execute(new SetGenreStyles(receiver.Project.GenreStyles)
        { UpdateOverallComment = true, OverallComment = new string('a', 1001) }, selectedPlanEdit: false));
    }

    private static void GenreKnowledgeComments()
    {
        var original = new GenreStyleDefinition("RPG", "red", "white", "solid") { KnowledgeComment = "アクションRPGを含む" };
        var project = PortableExample() with { GenreStyles = [original] };
        AssertEqual(original, new CircleSpaceCoordinator.Desktop.Core.Interaction.GenreStyleDraft(project).Build().Single(item => item.GenreId == "RPG"));
        var draft = new CircleSpaceCoordinator.Desktop.Core.Interaction.StyleMappingDraft(["RPG"],
            [new("RPG", "red", "white", "solid") { KnowledgeComment = original.KnowledgeComment }]);
        var maximum = string.Concat(Enumerable.Repeat("😀", 1000));
        draft.SetKnowledgeComment(0, maximum);
        AssertEqual(true, draft.HasChanges);
        AssertEqual(maximum, draft.Build().Single().KnowledgeComment!);
        RejectPortable(() => draft.SetKnowledgeComment(0, maximum + "a"));
        AssertEqual(maximum, draft.Build().Single().KnowledgeComment!);
        draft.RestoreOpeningSnapshot();
        AssertEqual(false, draft.HasChanges);
        AssertEqual(original.KnowledgeComment!, draft.Build().Single().KnowledgeComment!);
        draft.SetKnowledgeComment(0, "   ");
        AssertEqual<string?>(null, draft.Build().Single().KnowledgeComment);
        AssertEqual(true, draft.HasChanges);
        RejectPortable(() => draft.SetKnowledgeComment(0, "first\nsecond"));
        RejectPortable(() => draft.SetKnowledgeComment(0, "\ud800"));
        draft.RestoreOpeningSnapshot();
        draft.SetOverallComment(maximum);
        AssertEqual(true, draft.HasChanges);
        AssertEqual(maximum, draft.OverallComment!);
        RejectPortable(() => draft.SetOverallComment(maximum + "a"));
        draft.RestoreOpeningSnapshot();
        AssertEqual(false, draft.HasChanges);
        AssertEqual<string?>(null, draft.OverallComment);
    }
}
