namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Engine.Model;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private string? weightCommentFeatureId;
    private string CurrentWeightComment => workspace?.Project.Evaluation.Features
        .FirstOrDefault(item => item.Id == weightCommentFeatureId)?.CommentForWeight ?? "";

    private static string CommentExcerpt(string text, int maximum = 60)
    {
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        var positions = System.Globalization.StringInfo.ParseCombiningCharacters(text);
        return positions.Length <= maximum ? text : text[..positions[maximum]] + "［…］";
    }

    private string? HoveredChannelComment(ScreenPoint pointer)
    {
        if (editorMode != EditorMode.DeskPlacement || workspace is null) return null;
        for (var row = 0; row < VisibleChannelRows; row++)
        {
            var index = row + channelScroll - 3;
            if (index < 0 || index >= workspace.Project.Evaluation.Features.Count || !Contains(ChannelRow(row), pointer)) continue;
            var comment = workspace.Project.Evaluation.Features[index].CommentForChannel;
            return string.IsNullOrWhiteSpace(comment) ? null : "Comment  " + CommentExcerpt(comment);
        }
        return null;
    }

    private void DrawWeightComment()
    {
        var bounds = ModalBounds();
        var top = bounds.Y + bounds.Height - 204;
        textRenderer?.Draw("Comment", ToRectangle(new ScreenRectangle(bounds.X + 20, top, 140, 26)), new Color(160, 189, 201), 15, true);
        var lines = new List<string>();
        foreach (var line in CurrentWeightComment.Replace("\r", "").Split('\n'))
        {
            var elements = System.Globalization.StringInfo.ParseCombiningCharacters(line).Append(line.Length).ToArray();
            if (elements.Length == 1) lines.Add("");
            for (var i = 0; i < elements.Length - 1 && lines.Count <= 4; i += 38)
                lines.Add(line[elements[i]..elements[Math.Min(i + 38, elements.Length - 1)]]);
            if (lines.Count > 4) break;
        }
        if (lines.Count > 4) lines[3] += "［…］";
        for (var i = 0; i < Math.Min(4, lines.Count); i++)
            textRenderer?.Draw(lines[i], ToRectangle(new ScreenRectangle(bounds.X + 20, top + 42 + i * 23, bounds.Width - 40, 22)), Color.LightGray, 16);
    }

    private void EditWeightComment()
    {
        if (workspace is not { } owner || weightCommentFeatureId is not { } id) return;
        if (!EnsureHandle()) return;
        var feature = owner.Project.Evaluation.Features.Single(item => item.Id == id);
        textInputService?.Stop();
        EditChannelCommentText(feature.Name, feature.CommentForWeight ?? "", value =>
        {
            var current = owner.Project.Evaluation.Features.Single(item => item.Id == id);
            owner.Execute(new UpsertChannel(id, current.Name, current.SourceColumn) { CommentForWeight = value, Handle = Handle }, selectedPlanEdit: false);
        });
        modalWidth = -1;
        modalInputDrain = true;
        // Retain the numeric draft while editing its guidance.
        if (modalFocus == TextInputFocus) textInputService?.Start();
    }

    private void EditChannelCommentText(string name, string initial, Action<string> accepted)
    {
        using var form = new Forms.Form { Text = $"Comment — {name}", ClientSize = new(660, 400),
            StartPosition = Forms.FormStartPosition.CenterScreen, AutoScaleMode = Forms.AutoScaleMode.Dpi,
            FormBorderStyle = Forms.FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var label = new Forms.Label { Text = "Comment", Left = 16, Top = 16, Width = 628, Height = 26 };
        var text = new Forms.TextBox { Text = initial, Multiline = true, AcceptsReturn = true, ScrollBars = Forms.ScrollBars.Vertical,
            Left = 16, Top = 48, Width = 628, Height = 290 };
        var save = new Forms.Button { Text = "保存", Left = 418, Top = 352, Width = 108 };
        var cancel = new Forms.Button { Text = "キャンセル", Left = 536, Top = 352, Width = 108, DialogResult = Forms.DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            try { if (text.Text != initial) accepted(text.Text); form.DialogResult = Forms.DialogResult.OK; }
            catch (Exception ex) { Forms.MessageBox.Show(form, ex.Message, "コメントを保存できません"); }
        };
        form.Controls.AddRange([label, text, save, cancel]);
        form.CancelButton = cancel;
        ShowEditorDialog(form);
        modalInputDrain = true;
    }
}
