namespace CircleSpaceCoordinator.Desktop.Windows;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Engine.Model;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private void ManageChannelKnowledge()
    {
        if (workspace is null) return;
        var owner = workspace;
        using var form = PortableForm("チャンネルの知見 — 保存・閲覧・明示的な列対応");
        var sources = owner.Project.Evaluation.Features.ToArray();
        var source = new Forms.ComboBox { Left = 16, Top = 16, Width = 450, DropDownStyle = Forms.ComboBoxStyle.DropDownList };
        source.Items.AddRange(sources.Select(item => (object)item.Name).ToArray());
        if (sources.Length > 0) source.SelectedIndex = 0;
        var capture = new Forms.Button { Text = "このチャンネルを知見に保存", Left = 480, Top = 16, Width = 236, Enabled = sources.Length > 0 };
        var list = new Forms.ListBox { Left = 16, Top = 58, Width = 700, Height = 115 };
        var detail = new Forms.TextBox { Left = 16, Top = 185, Width = 700, Height = 225, Multiline = true,
            ReadOnly = true, ScrollBars = Forms.ScrollBars.Both, WordWrap = false };
        var name = PortableText(form, "追加チャンネル名", 422, "");
        var column = new Forms.ComboBox { Left = 175, Top = 454, Width = 540, DropDownStyle = Forms.ComboBoxStyle.DropDownList };
        column.Items.AddRange(owner.Project.Participants.SelectMany(item => item.SourceValues.Keys).Distinct().Cast<object>().ToArray());
        form.Controls.Add(new Forms.Label { Left = 16, Top = 457, Width = 155, Text = "実際のサークル列" });
        var status = PortableStatus(form);
        var bind = new Forms.Button { Text = "列対応を確認して追加", Left = 330, Top = 558, Width = 235 };
        var edit = new Forms.Button { Text = "選択した知見を編集", Left = 16, Top = 558, Width = 220 };
        var close = new Forms.Button { Text = "閉じる", Left = 580, Top = 558, Width = 135, DialogResult = Forms.DialogResult.Cancel };
        ChannelKnowledge[] knowledge = [];
        void Refresh()
        {
            knowledge = owner.Project.ChannelKnowledge.ToArray();
            list.Items.Clear();
            list.Items.AddRange(knowledge.Select(item => (object)$"{(item.IsConfidential ? "【マル秘】" : "")}{item.Name}（保存された知見・未対応付け）").ToArray());
            if (knowledge.Length > 0) list.SelectedIndex = 0;
            bind.Enabled = knowledge.Length > 0 && column.Items.Count > 0;
            edit.Enabled = knowledge.Length > 0;
        }
        list.SelectedIndexChanged += (_, _) =>
        {
            if (list.SelectedIndex < 0) return;
            var item = knowledge[list.SelectedIndex];
            name.Text = item.Name;
            detail.Text = $"{item.Description}\r\n狙い・適用条件：{item.Purpose}\r\n推奨列：{item.InputRule.RecommendedColumn}\r\n意味：{item.InputRule.Meaning}\r\n値：{item.InputRule.ValueMeanings}\r\n空欄：{(item.InputRule.BlankIsZero ? "0" : "エラー")}\r\n許可する数値：{(item.InputRule.AllowedValues is null ? "有限の数値" : string.Join(", ", item.InputRule.AllowedValues))}\r\nScale={item.Scale}, Offset={item.Offset}, OverallWeight={item.OverallWeight}\r\n既定重み：{item.DefaultWeight}\r\n{(item.Venue is null ? "ひな形（座標なし）" : $"会場：{item.Venue.Name} / {item.Venue.Width} × {item.Venue.Height}")}\r\n" +
                string.Join("\r\n", item.Cells.Select(cell => $"({cell.X}, {cell.Y}) = {cell.Weight:0.000}"));
            detail.Text = $"{(item.Credits ?? new CircleSpaceCoordinator.Core.Model.PersonCredits()).AttributionText}\r\n" + detail.Text;
        };
        capture.Click += (_, _) =>
        {
            if (source.SelectedIndex < 0) return;
            CaptureKnowledgeDialog(sources[source.SelectedIndex]);
            Refresh();
        };
        edit.Click += (_, _) =>
        {
            if (list.SelectedIndex < 0) return;
            var item = knowledge[list.SelectedIndex];
            CaptureKnowledgeDialog(new(item.Id, item.Name, item.Scale, item.Offset, item.OverallWeight)
                { Description = item.Description, Purpose = item.Purpose, InputRule = item.InputRule, IsConfidential = item.IsConfidential }, item);
            Refresh();
        };
        bind.Click += (_, _) =>
        {
            try
            {
                if (list.SelectedIndex < 0 || column.SelectedItem is not string selectedColumn)
                    throw new InvalidOperationException("知見と実際の列を選択してください。同名列でも明示的な確認が必要です。");
                var item = knowledge[list.SelectedIndex];
                if (Forms.MessageBox.Show(form, $"{(item.IsConfidential ? "【マル秘】\n" : "")}{item.InputRule.Meaning}\n{item.InputRule.ValueMeanings}\n列「{selectedColumn}」へ対応付けます。\n新しい評価チャンネルを追加するため、全配置案の評価に作用します。",
                    "入力ルールと影響の確認", Forms.MessageBoxButtons.OKCancel) != Forms.DialogResult.OK) return;
                owner.Execute(new BindChannelKnowledge(item.Id, "channel-" + Guid.NewGuid().ToString("N"), name.Text.Trim(), selectedColumn), selectedPlanEdit: false);
                status.Text = "チャンネルを追加しました。保存された知見は再利用用に保持しています。";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        form.Controls.AddRange([source, capture, list, detail, column, bind, edit, close]);
        form.CancelButton = close;
        Refresh();
        form.ShowDialog();
        modalInputDrain = true;
    }

    private void CaptureKnowledgeDialog(EvaluationFeature feature, ChannelKnowledge? editing = null)
    {
        if (workspace is null || !EnsureHandle()) return;
        var previous = editing ?? workspace.Project.ChannelKnowledge.FirstOrDefault(item => item.Name == feature.Name);
        if (previous is not null)
            feature = feature with { Description = previous.Description, Purpose = previous.Purpose, InputRule = previous.InputRule };
        using var form = PortableForm("運営の知見を保存 — サークル実データは含めません");
        var description = PortableText(form, "説明", 20, feature.Description ?? "");
        var purpose = PortableText(form, "狙い・適用条件", 60, feature.Purpose ?? "");
        var column = PortableText(form, "推奨列名", 100, feature.InputRule?.RecommendedColumn ?? feature.SourceColumn ?? feature.Name);
        var meaning = PortableText(form, "列の意味", 140, feature.InputRule?.Meaning ?? "");
        var values = PortableText(form, "数値の意味", 180, feature.InputRule?.ValueMeanings ?? "");
        var allowed = PortableText(form, "許可値（カンマ区切り）", 220,
            feature.InputRule?.AllowedValues is { } numbers ? string.Join(",", numbers.Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture))) : "");
        form.Controls.Add(new Forms.Label { Left = 175, Top = 254, Width = 540, Height = 45,
            Text = "例：1=書籍あり、0=なし。許可値が空なら有限の数値を許可。\n重みと係数は現在のチャンネルから保存します。" });
        var blank = new Forms.CheckBox { Left = 16, Top = 312, Width = 600, Text = "空欄を0として扱う", Checked = feature.InputRule?.BlankIsZero ?? true };
        var confidential = new Forms.CheckBox { Left = 16, Top = 350, Width = 600, Text = "この知見をマル秘として保存",
            Checked = workspace.Project.IsConfidential || feature.IsConfidential, Enabled = !workspace.Project.IsConfidential && !feature.IsConfidential };
        var status = PortableStatus(form);
        var save = new Forms.Button { Left = 440, Top = 558, Width = 130, Text = previous is null ? "知見を保存" : "知見を上書き" };
        var cancel = new Forms.Button { Left = 580, Top = 558, Width = 135, Text = "キャンセル", DialogResult = Forms.DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            try
            {
                var parsed = string.IsNullOrWhiteSpace(allowed.Text) ? null : allowed.Text.Split(',')
                    .Select(value => double.Parse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                if (editing is not null)
                    workspace.Execute(new UpdateChannelKnowledge(editing with { Description = description.Text, Purpose = purpose.Text,
                        InputRule = new(column.Text, meaning.Text, values.Text, blank.Checked, parsed), IsConfidential = confidential.Checked }, Handle), selectedPlanEdit: false);
                else workspace.Execute(new CaptureChannelKnowledge(feature.Id, previous?.Id ?? "knowledge-" + Guid.NewGuid().ToString("N"), description.Text, purpose.Text,
                    new(column.Text, meaning.Text, values.Text, blank.Checked, parsed), confidential.Checked)
                    { Handle = Handle, Overwrite = previous is not null }, selectedPlanEdit: false);
                form.DialogResult = Forms.DialogResult.OK;
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        form.Controls.AddRange([blank, confidential, save, cancel]);
        form.CancelButton = cancel;
        form.ShowDialog();
    }
}
