namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Tabular;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

public sealed partial class VenueEditorGame
{
    private sealed record ChannelMappingRow(string Id, string? ColumnKey, string? ChannelKey, string ChannelName, bool Fixed)
    {
        public bool Linked => ColumnKey is not null && ChannelKey is not null;
    }

    private sealed record ChannelMappingDrag(string Kind, string Value);

    private static readonly (string Id, string Name)[] ParticipantFieldNames =
    [
        ("circleId", "サークルID"), ("displayName", "サークル名"),
        ("requiredCellCount", "必要セル数"), ("combinedWithCircleId", "合体先サークルID"),
        ("genreId", "ジャンルID"),
    ];

    private Forms.DialogResult OpenChannelColumnMapping(string? openingStatus = null,
        ParticipantTableSheet? pendingSheet = null, string? pendingPath = null,
        ParticipantCsvEncoding? pendingEncoding = null)
    {
        if (workspace is null) return Forms.DialogResult.Cancel;
        var owner = workspace;
        var importing = pendingSheet is not null;
        using var form = new Forms.Form
        {
            Text = importing ? "取込む列とチャンネルの対応付け" : "列とチャンネルの対応付け",
            ClientSize = new Drawing.Size(1050, 560),
            StartPosition = Forms.FormStartPosition.CenterScreen,
            FormBorderStyle = Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, AutoScaleMode = Forms.AutoScaleMode.Dpi,
        };
        var help = new Forms.Label
        {
            Left = 20, Top = 12, Width = pendingEncoding is null ? importing ? 890 : 1010 : 760, Height = 42,
            Text = "列名をチャンネル名へドラッグすると対応します。行番号をドラッグすると行を並べ替えます。左右は６行ずつ表示します。",
        };
        var changeEncoding = new Forms.Button { Left = 795, Top = 12, Width = 120, Height = 32,
            Text = pendingEncoding == ParticipantCsvEncoding.ShiftJis ? "文字コード：CP932" : "文字コード：UTF-8",
            Visible = pendingEncoding is not null };
        var preview = new Forms.Button { Left = 925, Top = 12, Width = 105, Height = 32,
            Text = "表を確認", Visible = importing };
        Forms.DataGridView MakeGrid(int left)
        {
            var grid = new Forms.DataGridView
            {
                Left = left, Top = 67, Width = 495, Height = 338,
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false, RowHeadersVisible = false,
                MultiSelect = false, SelectionMode = Forms.DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = Forms.DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Drawing.Color.FromArgb(28, 35, 44),
                BorderStyle = Forms.BorderStyle.FixedSingle, AllowDrop = true,
            };
            grid.RowTemplate.Height = 50;
            grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { HeaderText = "行", FillWeight = 9, SortMode = Forms.DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { HeaderText = "データの列名", FillWeight = 41, SortMode = Forms.DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { HeaderText = "対応", FillWeight = 8, SortMode = Forms.DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { HeaderText = "チャンネル名", FillWeight = 42, SortMode = Forms.DataGridViewColumnSortMode.NotSortable });
            return grid;
        }
        var leftGrid = MakeGrid(20);
        var rightGrid = MakeGrid(535);
        var previous = new Forms.Button { Left = 20, Top = 418, Width = 100, Height = 34, Text = "前へ" };
        var next = new Forms.Button { Left = 130, Top = 418, Width = 100, Height = 34, Text = "次へ" };
        var pageLabel = new Forms.Label { Left = 244, Top = 424, Width = 270, Height = 24 };
        var link = new Forms.Button { Left = 535, Top = 418, Width = 100, Height = 34, Text = "紐づける" };
        var unlink = new Forms.Button { Left = 645, Top = 418, Width = 110, Height = 34, Text = "対応を外す" };
        var add = new Forms.Button { Left = 765, Top = 418, Width = 150, Height = 34, Text = "チャンネルを追加" };
        var close = new Forms.Button { Left = 925, Top = 418, Width = 105, Height = 34, Text = "閉じる", DialogResult = Forms.DialogResult.Cancel };
        var import = new Forms.Button { Left = 805, Top = 475, Width = 225, Height = 42, Text = "この対応で取り込む", Visible = importing };
        if (importing) close.Text = "キャンセル";
        var status = new Forms.Label { Left = 20, Top = 474, Width = importing ? 770 : 1010, Height = 70,
            Text = openingStatus ?? "列とチャンネルの対応はイベントに保存されます。" };
        form.Controls.AddRange([help, changeEncoding, preview, leftGrid, rightGrid, previous, next, pageLabel, link, unlink, add, close, status, import]);
        form.CancelButton = close;
        changeEncoding.Click += (_, _) => { form.DialogResult = Forms.DialogResult.Retry; form.Close(); };
        preview.Click += (_, _) =>
        {
            if (pendingSheet is null) return;
            using var viewer = new Forms.Form { Text = "取込み表のプレビュー（先頭100行）", ClientSize = new Drawing.Size(950, 520),
                StartPosition = Forms.FormStartPosition.CenterParent, AutoScaleMode = Forms.AutoScaleMode.Dpi };
            var table = new Forms.DataGridView { Dock = Forms.DockStyle.Fill, ReadOnly = true,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
                AutoSizeColumnsMode = Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells };
            foreach (var header in pendingSheet.Headers) table.Columns.Add("column" + table.Columns.Count,
                string.IsNullOrWhiteSpace(header) ? "（見出しなし）" : header);
            foreach (var row in pendingSheet.Rows.Take(100))
                table.Rows.Add(Enumerable.Range(0, pendingSheet.Headers.Count)
                    .Select(index => index < row.Count ? row[index] : "").Cast<object>().ToArray());
            viewer.Controls.Add(table);
            ShowEditorDialog(viewer, form);
        };

        var source = pendingSheet is null ? owner.Project.ParticipantTableSource :
            new ParticipantTableSource(Path.GetFileName(pendingPath ?? ""), pendingSheet.Name,
                pendingSheet.Headers.ToArray(), ParticipantTableMapper.GetColumnKeys(pendingSheet.Headers));
        var columnKeys = source?.ColumnKeys.ToArray() ?? owner.Project.Participants
            .SelectMany(item => item.SourceValues.Keys).Distinct(StringComparer.Ordinal).ToArray();
        var columnNames = columnKeys.Select((key, index) =>
            (Key: key, Name: $"{index + 1}. {(source?.Headers.ElementAtOrDefault(index) is { Length: > 0 } header ? header : key)}"))
            .ToDictionary(item => item.Key, item => item.Name, StringComparer.Ordinal);
        ParticipantFieldColumns? fields = source?.FieldColumns;
        if (fields is null && source is { Headers.Count: > 0 })
        {
            // Older projects did not store these five column bindings. Start from the importer's header guesses.
            var guess = ParticipantTableMapper.Guess(source.Headers);
            fields = new ParticipantFieldColumns(columnKeys[guess.CircleIdColumn], columnKeys[guess.DisplayNameColumn],
                guess.RequiredCellCountColumn is { } required ? columnKeys[required] : null,
                guess.CombinedWithCircleIdColumn is { } combined ? columnKeys[combined] : null,
                guess.GenreIdColumn is { } genre ? columnKeys[genre] : null);
        }
        var page = 0;
        var features = owner.Project.Evaluation.Features.Select(item => pendingSheet is not null &&
            item.SourceColumn is { } column && !columnKeys.Contains(column, StringComparer.Ordinal)
            ? item with { SourceColumn = null } : item).ToList();
        IReadOnlyList<EvaluationFeature> CurrentFeatures() => importing ? features : owner.Project.Evaluation.Features;
        IReadOnlyList<string>? mappingOrder = source?.MappingOrder ?? owner.Project.ParticipantTableSource?.MappingOrder;
        string? selectedColumn = null;
        string? selectedChannel = null;
        var rows = new List<ChannelMappingRow>();

        string? FieldColumn(string id) => id switch
        {
            "circleId" => fields?.CircleId, "displayName" => fields?.DisplayName,
            "requiredCellCount" => fields?.RequiredCellCount,
            "combinedWithCircleId" => fields?.CombinedWithCircleId,
            "genreId" => fields?.GenreId, _ => null,
        };
        string ChannelName(string key) => key.StartsWith("field:", StringComparison.Ordinal)
            ? ParticipantFieldNames.First(item => item.Id == key[6..]).Name
            : CurrentFeatures().First(item => item.Id == key[8..]).Name;
        void SetStatus(string message, bool error = false)
        {
            status.ForeColor = error ? Drawing.Color.DarkRed : Drawing.Color.FromArgb(35, 75, 85);
            status.Text = message;
        }

        void RefreshSelection()
        {
            foreach (var grid in new[] { leftGrid, rightGrid })
            foreach (Forms.DataGridViewRow gridRow in grid.Rows)
            {
                if (gridRow.Tag is not ChannelMappingRow item) continue;
                gridRow.Cells[1].Style.BackColor = item.ColumnKey == selectedColumn && selectedColumn is not null
                    ? Drawing.Color.LightCyan : Drawing.Color.White;
                gridRow.Cells[3].Style.BackColor = item.ChannelKey == selectedChannel && selectedChannel is not null
                    ? Drawing.Color.LightCyan : item.Fixed ? Drawing.Color.Gainsboro : Drawing.Color.White;
            }
            link.Enabled = selectedColumn is not null && selectedChannel is not null;
            unlink.Enabled = selectedChannel is { } key &&
                (key.StartsWith("feature:", StringComparison.Ordinal)
                    ? CurrentFeatures().Any(item => item.Id == key[8..] && item.SourceColumn is not null)
                    : key is not "field:circleId" and not "field:displayName" && FieldColumn(key[6..]) is not null);
        }

        void BuildRows()
        {
            rows.Clear();
            var displayed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var column in columnKeys)
            {
                var any = false;
                foreach (var field in ParticipantFieldNames.Where(item => FieldColumn(item.Id) == column))
                {
                    rows.Add(new ChannelMappingRow("field:" + field.Id, column, "field:" + field.Id, field.Name, false));
                    displayed.Add("field:" + field.Id);
                    any = true;
                }
                foreach (var feature in CurrentFeatures().Where(item => item.SourceColumn == column))
                {
                    rows.Add(new ChannelMappingRow("feature:" + feature.Id, column, "feature:" + feature.Id, feature.Name, false));
                    displayed.Add("feature:" + feature.Id);
                    any = true;
                }
                if (!any) rows.Add(new ChannelMappingRow("column:" + column, column, null, "", false));
            }
            foreach (var field in ParticipantFieldNames.Where(item => !displayed.Contains("field:" + item.Id)))
                rows.Add(new ChannelMappingRow("field:" + field.Id, null, "field:" + field.Id, field.Name + "（未対応）", false));
            foreach (var feature in CurrentFeatures().Where(item => !displayed.Contains("feature:" + item.Id)))
                rows.Add(new ChannelMappingRow("feature:" + feature.Id, null, "feature:" + feature.Id, feature.Name + "（未対応）", false));
            foreach (var name in NumberChannelNames)
                rows.Add(new ChannelMappingRow("number:" + name, null, null, name + "（固定）", true));
            var order = mappingOrder;
            if (order is not null)
            {
                var indexes = order.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
                rows = rows.Select((item, index) => (item, index))
                    .OrderBy(pair => indexes.TryGetValue(pair.item.Id, out var position) ? position : order.Count + pair.index)
                    .Select(pair => pair.item).ToList();
            }
        }

        void ShowPage()
        {
            page = Math.Clamp(page, 0, Math.Max(0, (rows.Count - 1) / 6));
            foreach (var (grid, start) in new[] { (leftGrid, page * 6), (rightGrid, (page + 1) * 6) })
            {
                grid.Rows.Clear();
                for (var i = 0; i < 6; i++)
                {
                    var position = start + i;
                    var item = position < rows.Count ? rows[position] : null;
                    var index = grid.Rows.Add(item is null ? "" : (position + 1).ToString(),
                        item?.ColumnKey is { } key ? columnNames.GetValueOrDefault(key, key) : "", "", item?.ChannelName ?? "");
                    grid.Rows[index].Tag = item;
                }
            }
            previous.Enabled = page > 0;
            next.Enabled = (page + 1) * 6 < rows.Count;
            var rightStart = (page + 1) * 6;
            pageLabel.Text = $"左 {page * 6 + 1}～{Math.Min(rightStart, rows.Count)} / 右 " +
                (rightStart < rows.Count ? $"{rightStart + 1}～{Math.Min(rightStart + 6, rows.Count)}" : "なし");
            RefreshSelection();
        }

        void RefreshRows()
        {
            if (!importing) fields = owner.Project.ParticipantTableSource?.FieldColumns ?? fields;
            if (!importing) mappingOrder = owner.Project.ParticipantTableSource?.MappingOrder;
            BuildRows();
            ShowPage();
            if (columnKeys.Length == 0)
                SetStatus("サークル一覧の列がありません。先に Excel / CSV を取り込んでください。", true);
        }

        void Bind(string column, string channel)
        {
            if (workspace != owner) throw new InvalidOperationException("対象のイベントが変わりました。");
            if (channel.StartsWith("feature:", StringComparison.Ordinal))
            {
                var feature = CurrentFeatures().Single(item => item.Id == channel[8..]);
                if (importing) features[features.FindIndex(item => item.Id == feature.Id)] = feature with { SourceColumn = column };
                else owner.Execute(new UpsertChannel(feature.Id, feature.Name, column), selectedPlanEdit: false);
            }
            else
            {
                var current = fields ?? throw new InvalidOperationException("先に Excel / CSV を取り込んでください。");
                var updated = channel[6..] switch
                {
                    "circleId" => current with { CircleId = column },
                    "displayName" => current with { DisplayName = column },
                    "requiredCellCount" => current with { RequiredCellCount = column },
                    "combinedWithCircleId" => current with { CombinedWithCircleId = column },
                    "genreId" => current with { GenreId = column },
                    _ => throw new ArgumentException("チャンネルを選んでください。"),
                };
                if (importing) fields = updated;
                else owner.Execute(new SetParticipantFieldColumns(updated), selectedPlanEdit: false);
            }
            selectedColumn = column;
            selectedChannel = channel;
            RefreshRows();
            SetStatus($"「{columnNames.GetValueOrDefault(column, column)}」を「{ChannelName(channel)}」に対応付けました。");
        }

        foreach (var grid in new[] { leftGrid, rightGrid })
        {
            grid.CellPainting += (_, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex != 2) return;
                e.PaintBackground(e.CellBounds, false);
                if (grid.Rows[e.RowIndex].Tag is ChannelMappingRow { Linked: true })
                {
                    using var pen = new Drawing.Pen(Drawing.Color.FromArgb(35, 126, 111), 3);
                    var y = e.CellBounds.Top + e.CellBounds.Height / 2;
                    e.Graphics?.DrawLine(pen, e.CellBounds.Left + 2, y, e.CellBounds.Right - 2, y);
                }
                e.Handled = true;
            };
            grid.CellClick += (_, e) =>
            {
                if (e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not ChannelMappingRow item) return;
                if (e.ColumnIndex == 1 && item.ColumnKey is not null) selectedColumn = item.ColumnKey;
                if (e.ColumnIndex == 3 && item.ChannelKey is not null) selectedChannel = item.ChannelKey;
                RefreshSelection();
                SetStatus($"選択中：{(selectedColumn is { } key ? columnNames.GetValueOrDefault(key, key) : "列を選択")} → " +
                    (selectedChannel is { } channel ? ChannelName(channel) : "チャンネルを選択"));
            };
            Drawing.Point? dragStart = null;
            int dragRow = -1, dragColumn = -1;
            grid.MouseDown += (_, e) =>
            {
                var hit = grid.HitTest(e.X, e.Y);
                dragStart = new Drawing.Point(e.X, e.Y);
                dragRow = hit.RowIndex;
                dragColumn = hit.ColumnIndex;
            };
            grid.MouseMove += (_, e) =>
            {
                if (e.Button != Forms.MouseButtons.Left || dragStart is not { } origin || dragRow < 0 ||
                    Math.Abs(e.X - origin.X) + Math.Abs(e.Y - origin.Y) < 8) return;
                dragStart = null;
                if (grid.Rows[dragRow].Tag is not ChannelMappingRow item) return;
                var payload = dragColumn switch
                {
                    0 => new ChannelMappingDrag("row", item.Id),
                    1 when item.ColumnKey is { } column => new ChannelMappingDrag("column", column),
                    3 when item.ChannelKey is { } channel => new ChannelMappingDrag("channel", channel),
                    _ => null,
                };
                if (payload is not null) grid.DoDragDrop(payload, Forms.DragDropEffects.Move | Forms.DragDropEffects.Link);
            };
            grid.DragEnter += (_, e) =>
            {
                if (e.Data?.GetData(typeof(ChannelMappingDrag)) is ChannelMappingDrag payload)
                    e.Effect = payload.Kind == "row" ? Forms.DragDropEffects.Move : Forms.DragDropEffects.Link;
            };
            grid.DragDrop += (_, e) =>
            {
                try
                {
                    if (e.Data?.GetData(typeof(ChannelMappingDrag)) is not ChannelMappingDrag payload) return;
                    var point = grid.PointToClient(new Drawing.Point(e.X, e.Y));
                    var hit = grid.HitTest(point.X, point.Y);
                    if (hit.RowIndex < 0 || grid.Rows[hit.RowIndex].Tag is not ChannelMappingRow target) return;
                    if (payload.Kind == "row")
                    {
                        if (payload.Value == target.Id || source is null) return;
                        var moved = rows.Single(item => item.Id == payload.Value);
                        rows.Remove(moved);
                        rows.Insert(rows.FindIndex(item => item.Id == target.Id), moved);
                        var order = rows.Select(item => item.Id).ToArray();
                        if (importing) mappingOrder = order;
                        else owner.Execute(new SetParticipantMappingOrder(order), selectedPlanEdit: false);
                        RefreshRows();
                        SetStatus($"{rows.FindIndex(item => item.Id == moved.Id) + 1} 行目へ移動しました。");
                    }
                    else if (payload.Kind == "column" && target.ChannelKey is { } channel)
                        Bind(payload.Value, channel);
                    else if (payload.Kind == "channel" && target.ColumnKey is { } column)
                        Bind(column, payload.Value);
                }
                catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); RefreshRows(); }
            };
        }

        previous.Click += (_, _) => { page--; ShowPage(); };
        next.Click += (_, _) => { page++; ShowPage(); };
        link.Click += (_, _) =>
        {
            try { if (selectedColumn is { } column && selectedChannel is { } channel) Bind(column, channel); }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        };
        unlink.Click += (_, _) =>
        {
            try
            {
                if (workspace != owner || selectedChannel is not { } channel) return;
                if (channel.StartsWith("feature:", StringComparison.Ordinal))
                {
                    var feature = CurrentFeatures().Single(item => item.Id == channel[8..]);
                    if (importing) features[features.FindIndex(item => item.Id == feature.Id)] = feature with { SourceColumn = null };
                    else owner.Execute(new UpsertChannel(feature.Id, feature.Name, null), selectedPlanEdit: false);
                }
                else
                {
                    var current = fields ?? throw new InvalidOperationException("列情報がありません。");
                    var updated = channel[6..] switch
                    {
                        "requiredCellCount" => current with { RequiredCellCount = null },
                        "combinedWithCircleId" => current with { CombinedWithCircleId = null },
                        "genreId" => current with { GenreId = null },
                        _ => throw new ArgumentException("サークルIDとサークル名の対応は必須です。"),
                    };
                    if (importing) fields = updated;
                    else owner.Execute(new SetParticipantFieldColumns(updated), selectedPlanEdit: false);
                }
                RefreshRows();
                SetStatus("列の対応を外しました。");
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
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
                if (value.Length == 0 || NumberChannelNames.Contains(value, StringComparer.Ordinal) ||
                    ParticipantFieldNames.Any(item => item.Name == value))
                    throw new ArgumentException("既定のチャンネルと異なる名前を入力してください。");
                var id = $"channel-{Guid.NewGuid():N}";
                if (importing) features.Add(new EvaluationFeature(id, value, 1, 0, 1));
                else owner.Execute(new UpsertChannel(id, value, null), selectedPlanEdit: false);
                selectedChannel = "feature:" + id;
                RefreshRows();
                SetStatus($"「{value}」を追加しました。列名をドラッグして対応付けてください。");
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        };
        import.Click += (_, _) =>
        {
            try
            {
                if (pendingSheet is null || source is null || fields is null || workspace != owner) return;
                int Index(string key) => Array.IndexOf(columnKeys, key);
                int? OptionalIndex(string? key) => key is null ? null : Index(key);
                var mapping = new ParticipantColumnMapping(Index(fields.CircleId), Index(fields.DisplayName),
                    OptionalIndex(fields.RequiredCellCount), OptionalIndex(fields.CombinedWithCircleId), OptionalIndex(fields.GenreId));
                var participants = ParticipantTableMapper.Map(pendingSheet, mapping);
                var removedColumns = owner.Project.Evaluation.Features.Count(item => item.SourceColumn is not null &&
                    !columnKeys.Contains(item.SourceColumn, StringComparer.Ordinal));
                var message = $"参加サークル {participants.Count} 件を取り込みます。\n一覧から消えたサークルの配置は解除されます。" +
                    (removedColumns > 0 ? $"\n表にない列の対応が {removedColumns} 件外れます。" : "");
                if (Forms.MessageBox.Show(form, message, "参加サークル一覧の確認",
                    Forms.MessageBoxButtons.OKCancel, Forms.MessageBoxIcon.Question) != Forms.DialogResult.OK) return;
                owner.Execute(new ImportMappedParticipants(participants,
                    source with { FieldColumns = fields, MappingOrder = mappingOrder }, features.ToArray()),
                    selectedPlanEdit: false);
                Log("participant_import", true, $"participants={participants.Count}");
                form.DialogResult = Forms.DialogResult.OK;
                form.Close();
            }
            catch (Exception ex) { SetStatus(ex.GetBaseException().Message, true); }
        };
        RefreshRows();
        var result = ShowEditorDialog(form);
        modalInputDrain = true;
        return result;
    }
}
