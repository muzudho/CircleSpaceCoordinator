namespace CircleSpaceCoordinator.Desktop;

using CircleSpaceCoordinator.Desktop.Persistence;

internal sealed class EventProjectSelectorForm : System.Windows.Forms.Form
{
    private readonly EventProjectCatalogService catalog;
    private readonly ApplicationSettingsService settings;
    private readonly System.Windows.Forms.ListBox projectList = new();
    private readonly System.Windows.Forms.Button openButton = CreateButton("開く");
    private readonly System.Windows.Forms.Button duplicateButton = CreateButton("複製");
    private readonly System.Windows.Forms.Button confidentialButton = CreateButton("マル秘に設定");
    private readonly System.Windows.Forms.Button moveUpButton = CreateButton("上へ");
    private readonly System.Windows.Forms.Button moveDownButton = CreateButton("下へ");
    private readonly System.Windows.Forms.Button removeButton = CreateButton("一覧から除外");

    public EventProjectSelectorForm(ApplicationSettingsService settings)
    {
        this.settings = settings;
        catalog = new EventProjectCatalogService(settings);
        Text = "Circle Space Coordinator - イベントを選択";
        Width = 760;
        Height = 470;
        MinimumSize = new System.Drawing.Size(640, 380);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        var title = new System.Windows.Forms.Label
        {
            Text = "イベントプロジェクトを選択してください",
            AutoSize = true,
            Font = new System.Drawing.Font(
                System.Drawing.SystemFonts.MessageBoxFont?.FontFamily ?? System.Drawing.FontFamily.GenericSansSerif,
                14f,
                System.Drawing.FontStyle.Bold),
            Left = 18,
            Top = 18,
        };
        projectList.Left = 18;
        projectList.Top = 58;
        projectList.Width = 550;
        projectList.Height = 340;
        projectList.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom |
                             System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        projectList.DisplayMember = nameof(ProjectListItem.Label);
        projectList.DoubleClick += (_, _) => OpenSelected();
        projectList.SelectedIndexChanged += (_, _) => UpdateButtons();

        var newButton = CreateButton("新規作成");
        var registerButton = CreateButton("既存ファイルを登録");
        var buttons = new[] { openButton, newButton, registerButton, duplicateButton, confidentialButton, moveUpButton, moveDownButton, removeButton };
        for (var index = 0; index < buttons.Length; index++)
        {
            buttons[index].Left = 585;
            buttons[index].Top = 58 + index * 45;
            buttons[index].Width = 145;
            buttons[index].Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        }
        openButton.Click += (_, _) => OpenSelected();
        newButton.Click += (_, _) => CreateProject();
        registerButton.Click += (_, _) => RegisterProject();
        duplicateButton.Click += (_, _) => DuplicateProject();
        confidentialButton.Click += (_, _) => MarkSelectedConfidential();
        moveUpButton.Click += (_, _) => MoveSelected(-1);
        moveDownButton.Click += (_, _) => MoveSelected(1);
        removeButton.Click += (_, _) => RemoveSelected();

        Controls.Add(title);
        Controls.Add(projectList);
        Controls.AddRange(buttons);
        AcceptButton = openButton;
        RefreshProjects(settings.Current.LastProjectPath);
    }

    public string? SelectedProjectPath { get; private set; }

    private static System.Windows.Forms.Button CreateButton(string text) => new() { Text = text, Height = 34 };

    private ProjectListItem? SelectedItem => projectList.SelectedItem as ProjectListItem;

    private void RefreshProjects(string? selectedPath = null)
    {
        projectList.BeginUpdate();
        projectList.Items.Clear();
        foreach (var project in catalog.Projects)
            projectList.Items.Add(new ProjectListItem(project, File.Exists(project.Path) && catalog.IsConfidential(project.Path)));
        projectList.EndUpdate();
        if (projectList.Items.Count > 0)
        {
            var selectedIndex = selectedPath is null
                ? 0
                : projectList.Items.Cast<ProjectListItem>().ToList().FindIndex(item => PathsEqual(item.Path, selectedPath));
            projectList.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var selected = SelectedItem;
        var exists = selected is not null && File.Exists(selected.Path);
        openButton.Enabled = exists;
        duplicateButton.Enabled = exists;
        confidentialButton.Enabled = exists && selected?.IsConfidential == false;
        removeButton.Enabled = selected is not null;
        moveUpButton.Enabled = projectList.SelectedIndex > 0;
        moveDownButton.Enabled = projectList.SelectedIndex >= 0 && projectList.SelectedIndex < projectList.Items.Count - 1;
    }

    private void OpenSelected()
    {
        var selected = SelectedItem;
        if (selected is null)
            return;
        try
        {
            catalog.Register(selected.Path);
            SelectedProjectPath = selected.Path;
            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            ShowError("イベントプロジェクトを開けませんでした。", exception);
            RefreshProjects(selected.Path);
        }
    }

    private void CreateProject()
    {
        var name = TextPromptDialog.Show("イベントを新規作成", "イベント名", "新しいイベント");
        if (name is null)
            return;
        var confidentiality = AskConfidentialForNewProject();
        if (confidentiality is null)
            return;
        var path = WindowsProjectFileDialog.Save(MakeFileName(name), settings.Current.ProjectsDirectory);
        if (path is null)
            return;
        try
        {
            var created = catalog.Create(path, name, confidentiality.Value);
            RefreshProjects(created.Path);
        }
        catch (Exception exception)
        {
            ShowError("イベントプロジェクトを作成できませんでした。", exception);
        }
    }

    private void RegisterProject()
    {
        var path = WindowsProjectFileDialog.Open(null, settings.Current.ProjectsDirectory);
        if (path is null)
            return;
        try
        {
            var registered = catalog.Register(path);
            RefreshProjects(registered.Path);
        }
        catch (Exception exception)
        {
            ShowError("有効なイベントプロジェクトではありません。", exception);
        }
    }

    private void DuplicateProject()
    {
        var selected = SelectedItem;
        if (selected is null)
            return;
        var name = TextPromptDialog.Show("イベントを複製", "複製後のイベント名", $"{selected.DisplayName} のコピー");
        if (name is null)
            return;
        var path = WindowsProjectFileDialog.Save(MakeFileName(name), settings.Current.ProjectsDirectory);
        if (path is null)
            return;
        try
        {
            var copy = catalog.Duplicate(selected.Path, path, name);
            RefreshProjects(copy.Path);
        }
        catch (Exception exception)
        {
            ShowError("イベントプロジェクトを複製できませんでした。", exception);
        }
    }

    private void MoveSelected(int offset)
    {
        var selected = SelectedItem;
        if (selected is null || !catalog.Move(selected.Path, offset))
            return;
        RefreshProjects(selected.Path);
    }

    private void RemoveSelected()
    {
        var selected = SelectedItem;
        if (selected is null)
            return;
        var answer = System.Windows.Forms.MessageBox.Show(
            $"「{selected.DisplayName}」を一覧から除外しますか？\n\nイベントプロジェクトファイルは削除されません。",
            "一覧から除外",
            System.Windows.Forms.MessageBoxButtons.YesNo,
            System.Windows.Forms.MessageBoxIcon.Question,
            System.Windows.Forms.MessageBoxDefaultButton.Button2);
        if (answer != System.Windows.Forms.DialogResult.Yes)
            return;
        catalog.Remove(selected.Path);
        RefreshProjects();
    }

    private bool? AskConfidentialForNewProject()
    {
        var answer = System.Windows.Forms.MessageBox.Show(
            this,
            "このイベントプロジェクトを［マル秘］に設定しますか？\n\n" +
            "［はい］を選ぶと、タイトルと画面内に秘密情報の注意表示が出ます。\n" +
            "事故防止のため、アプリからマル秘を外すことはできません。解除する場合はJSONファイルを直接編集してください。",
            "新規イベントの公開区分",
            System.Windows.Forms.MessageBoxButtons.YesNoCancel,
            System.Windows.Forms.MessageBoxIcon.Warning,
            System.Windows.Forms.MessageBoxDefaultButton.Button2);
        return answer switch
        {
            System.Windows.Forms.DialogResult.Yes => true,
            System.Windows.Forms.DialogResult.No => false,
            _ => null,
        };
    }

    private void MarkSelectedConfidential()
    {
        var selected = SelectedItem;
        if (selected is null || selected.IsConfidential)
            return;
        var answer = System.Windows.Forms.MessageBox.Show(
            this,
            $"「{selected.DisplayName}」を［マル秘］に設定しますか？\n\n" +
            "事故防止のため、設定後にアプリからマル秘を外すことはできません。\n" +
            "解除する場合はJSONファイルの project.isConfidential を直接 false に編集してください。",
            "マル秘に設定",
            System.Windows.Forms.MessageBoxButtons.YesNo,
            System.Windows.Forms.MessageBoxIcon.Warning,
            System.Windows.Forms.MessageBoxDefaultButton.Button2);
        if (answer != System.Windows.Forms.DialogResult.Yes)
            return;
        try
        {
            catalog.MarkConfidential(selected.Path);
            RefreshProjects(selected.Path);
        }
        catch (Exception exception)
        {
            ShowError("イベントプロジェクトをマル秘に設定できませんでした。", exception);
        }
    }

    private static void ShowError(string message, Exception exception) =>
        System.Windows.Forms.MessageBox.Show(
            $"{message}\n\n{exception.Message}",
            "Circle Space Coordinator",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Error);

    private static string MakeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return (string.IsNullOrWhiteSpace(sanitized) ? "event" : sanitized) + ".json";
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private sealed class ProjectListItem(EventProjectReference project, bool isConfidential)
    {
        public string Path { get; } = project.Path;
        public string DisplayName { get; } = project.DisplayName;
        public bool IsConfidential { get; } = isConfidential;
        public string Label => File.Exists(Path)
            ? $"{(IsConfidential ? "（秘） " : "")}{DisplayName}   —   {Path}"
            : $"⚠ {DisplayName}   —   ファイルが見つかりません: {Path}";
    }
}
