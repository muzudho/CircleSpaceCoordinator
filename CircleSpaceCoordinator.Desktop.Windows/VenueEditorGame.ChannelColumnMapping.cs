namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Engine.Model;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

public sealed partial class VenueEditorGame
{
    private sealed record ChannelMappingRow(string? ColumnKey, string? ChannelId, bool Linked, bool Fixed);

    private void OpenChannelColumnMapping(string? openingStatus = null)
    {
        if (workspace is null) return;
        var owner = workspace;
        using var form = new Forms.Form
        {
            Text = "列とチャンネルの対応付け",
            ClientSize = new Drawing.Size(1000, 590),
            StartPosition = Forms.FormStartPosition.CenterScreen,
            FormBorderStyle = Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            AutoScaleMode = Forms.AutoScaleMode.Dpi,
        };
        var help = new Forms.Label
        {
            Left = 20, Top = 12, Width = 960, Height = 34,
            Text = "左の列名と右の評価チャンネルを選んで紐づけます。固定の番号チャンネルは表の列と直接対応しません。",
        };
        var grid = new Forms.DataGridView
        {
            Left = 20, Top = 50, Width = 960, Height = 430,
            ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false,
            MultiSelect = false, SelectionMode = Forms.DataGridViewSelectionMode.CellSelect,
            AutoSizeColumnsMode = Forms.DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Drawing.Color.FromArgb(28, 35, 44),
            BorderStyle = Forms.BorderStyle.FixedSingle,
        };
        grid.RowTemplate.Height = 32;
        grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { HeaderText = "データの列名", FillWeight = 47, SortMode = Forms.DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { HeaderText = "対応", FillWeight = 6, SortMode = Forms.DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { HeaderText = "チャンネル名", FillWeight = 47, SortMode = Forms.DataGridViewColumnSortMode.NotSortable });
        var link = new Forms.Button { Left = 20, Top = 494, Width = 174, Height = 36, Text = "紐づける" };
        var unlink = new Forms.Button { Left = 204, Top = 494, Width = 174, Height = 36, Text = "対応を外す" };
        var add = new Forms.Button { Left = 388, Top = 494, Width = 190, Height = 36, Text = "チャンネルを追加" };
        var close = new Forms.Button { Left = 820, Top = 494, Width = 160, Height = 36, Text = "閉じる", DialogResult = Forms.DialogResult.Cancel };
        var status = new Forms.Label { Left = 20, Top = 543, Width = 960, Height = 28,
            Text = openingStatus ?? "対応の変更はイベントに保存され、全配置案の評価へ反映されます。" };
        form.Controls.AddRange([help, grid, link, unlink, add, close, status]);
        form.CancelButton = close;

        string? selectedColumn = null;
        string? selectedChannel = null;
        var source = owner.Project.ParticipantTableSource;
        var columnKeys = source?.ColumnKeys.ToArray() ?? owner.Project.Participants
            .SelectMany(item => item.SourceValues.Keys).Distinct(StringComparer.Ordinal).ToArray();
        var columnLabels = columnKeys.Select((key, index) =>
            (Key: key, Label: $"{index + 1}. {(source?.Headers.ElementAtOrDefault(index) is { Length: > 0 } header ? header : key)}"))
            .ToArray();

        void SetStatus(string text, bool error = false)
        {
            status.ForeColor = error ? Drawing.Color.DarkRed : Drawing.Color.FromArgb(35, 75, 85);
            status.Text = text;
        }

        void RefreshSelection()
        {
            foreach (Forms.DataGridViewRow row in grid.Rows)
            {
                var item = (ChannelMappingRow)row.Tag!;
                row.Cells[0].Style.BackColor = item.ColumnKey is not null && item.ColumnKey == selectedColumn
                    ? Drawing.Color.LightCyan : Drawing.Color.White;
                row.Cells[2].Style.BackColor = item.ChannelId is not null && item.ChannelId == selectedChannel
                    ? Drawing.Color.LightCyan : item.Fixed ? Drawing.Color.Gainsboro : Drawing.Color.White;
            }
            link.Enabled = selectedColumn is not null && selectedChannel is not null;
            unlink.Enabled = selectedChannel is not null && owner.Project.Evaluation.Features
                .Any(item => item.Id == selectedChannel && item.SourceColumn is not null);
        }

        void AddRow(string? columnKey, string left, string? channelId, string right, bool linked = false, bool fixedChannel = false)
        {
            var index = grid.Rows.Add(left, "", right);
            grid.Rows[index].Tag = new ChannelMappingRow(columnKey, channelId, linked, fixedChannel);
        }

        void RefreshRows()
        {
            grid.Rows.Clear();
            var features = owner.Project.Evaluation.Features;
            var displayed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var column in columnLabels)
            {
                var matches = features.Where(item => item.SourceColumn == column.Key).ToArray();
                if (matches.Length == 0) AddRow(column.Key, column.Label, null, "");
                foreach (var feature in matches)
                {
                    AddRow(column.Key, column.Label, feature.Id, feature.Name, linked: true);
                    displayed.Add(feature.Id);
                }
            }
            foreach (var feature in features.Where(item => !displayed.Contains(item.Id)))
                AddRow(null, "", feature.Id, feature.SourceColumn is null
                    ? $"{feature.Name}（未対応）" : $"{feature.Name}（元の列なし）");
            foreach (var name in NumberChannelNames)
                AddRow(null, "", null, $"{name}（固定）", fixedChannel: true);
            if (columnLabels.Length == 0)
                SetStatus("サークル一覧の列がありません。先に Excel / CSV を取り込んでください。", error: true);
            RefreshSelection();
        }

        grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1) return;
            e.PaintBackground(e.CellBounds, false);
            if (((ChannelMappingRow)grid.Rows[e.RowIndex].Tag!).Linked)
            {
                using var pen = new Drawing.Pen(Drawing.Color.FromArgb(35, 126, 111), 3);
                var y = e.CellBounds.Top + e.CellBounds.Height / 2;
                e.Graphics?.DrawLine(pen, e.CellBounds.Left + 2, y, e.CellBounds.Right - 2, y);
            }
            e.Handled = true;
        };
        grid.CellClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            var row = (ChannelMappingRow)grid.Rows[e.RowIndex].Tag!;
            if (e.ColumnIndex == 0 && row.ColumnKey is not null) selectedColumn = row.ColumnKey;
            if (e.ColumnIndex == 2 && row.ChannelId is not null) selectedChannel = row.ChannelId;
            RefreshSelection();
            var columnName = columnLabels.FirstOrDefault(item => item.Key == selectedColumn).Label ?? "列を選択";
            var channelName = owner.Project.Evaluation.Features.FirstOrDefault(item => item.Id == selectedChannel)?.Name ?? "チャンネルを選択";
            SetStatus($"選択中：{columnName} → {channelName}");
        };
        link.Click += (_, _) =>
        {
            try
            {
                if (workspace != owner || selectedColumn is null || selectedChannel is null) return;
                var feature = owner.Project.Evaluation.Features.Single(item => item.Id == selectedChannel);
                owner.Execute(new UpsertChannel(feature.Id, feature.Name, selectedColumn), selectedPlanEdit: false);
                SetStatus($"列「{selectedColumn}」を「{feature.Name}」に紐づけました。");
                RefreshRows();
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, error: true); }
        };
        unlink.Click += (_, _) =>
        {
            try
            {
                if (workspace != owner || selectedChannel is null) return;
                var feature = owner.Project.Evaluation.Features.Single(item => item.Id == selectedChannel);
                owner.Execute(new UpsertChannel(feature.Id, feature.Name, null), selectedPlanEdit: false);
                SetStatus($"「{feature.Name}」の列対応を外しました。");
                RefreshRows();
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, error: true); }
        };
        add.Click += (_, _) =>
        {
            using var input = new Forms.Form { Text = "チャンネルを追加", ClientSize = new Drawing.Size(440, 145),
                StartPosition = Forms.FormStartPosition.CenterParent, FormBorderStyle = Forms.FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false };
            var label = new Forms.Label { Left = 16, Top = 16, Width = 408, Height = 24, Text = "新しい評価チャンネル名（100文字まで）" };
            var name = new Forms.TextBox { Left = 16, Top = 46, Width = 408, MaxLength = 100 };
            var accept = new Forms.Button { Left = 220, Top = 98, Width = 96, Text = "追加", DialogResult = Forms.DialogResult.OK };
            var cancel = new Forms.Button { Left = 328, Top = 98, Width = 96, Text = "キャンセル", DialogResult = Forms.DialogResult.Cancel };
            input.Controls.AddRange([label, name, accept, cancel]);
            input.AcceptButton = accept;
            input.CancelButton = cancel;
            if (input.ShowDialog(form) != Forms.DialogResult.OK) return;
            try
            {
                if (workspace != owner) return;
                var value = name.Text.Trim();
                if (value.Length == 0 || NumberChannelNames.Contains(value, StringComparer.Ordinal))
                    throw new ArgumentException("固定の番号チャンネルと異なる名前を入力してください。");
                var id = $"channel-{Guid.NewGuid():N}";
                owner.Execute(new UpsertChannel(id, value, null), selectedPlanEdit: false);
                selectedChannel = id;
                SetStatus($"「{value}」を追加しました。左の列を選んで紐づけてください。");
                RefreshRows();
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, error: true); }
        };
        RefreshRows();
        ShowEditorDialog(form);
        modalInputDrain = true;
    }
}
