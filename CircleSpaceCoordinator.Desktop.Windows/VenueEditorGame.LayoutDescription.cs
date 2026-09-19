namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Engine.Model;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private string? HoveredDeskLayoutDescription()
    {
        if (!CanShowEditorHover || (!ShowsDeskLayouts && editorMode != EditorMode.GenrePlacement) || workspace is null || hoveredPlanId is null) return null;
        if (editorMode == EditorMode.GenrePlacement)
        {
            var layout = workspace.Project.CircleLayouts.FirstOrDefault(item => item.Id == hoveredPlanId);
            return layout is null ? null : GenreAttributionPrefix(layout.Id) + CommentExcerpt(layout.Description ?? "");
        }
        var description = UsesSeparatedLayouts
            ? workspace.Project.DeskLayouts.FirstOrDefault(item => item.Id == hoveredPlanId)?.Description
            : workspace.Project.Plans.FirstOrDefault(item => item.Id == hoveredPlanId)?.Description;
        if (string.IsNullOrWhiteSpace(description)) return null;

        // Limit by visible text elements, so emoji and combining marks stay intact.
        const int maximumLength = 60;
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(description);
        var excerpt = new System.Text.StringBuilder();
        var count = 0;
        var pendingSpace = false;
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (string.IsNullOrWhiteSpace(element))
            {
                pendingSpace = excerpt.Length > 0;
                continue;
            }
            if (count + (pendingSpace ? 1 : 0) >= maximumLength)
                return $"説明：{excerpt}［…］";
            if (pendingSpace) { excerpt.Append(' '); count++; }
            pendingSpace = false;
            excerpt.Append(element);
            count++;
        }
        return $"説明：{excerpt}";
    }

    private void EditDeskLayoutDescription()
    {
        if (workspace is null) return;
        var owner = workspace;
        var layout = owner.Project.DeskLayouts.Single(item => item.Id == owner.SelectedDeskLayoutId);
        try
        {
            using var form = new Forms.Form
            {
                Text = $"フレーム配置案の説明 — {layout.Name}",
                ClientSize = new System.Drawing.Size(660, 400),
                MinimumSize = new System.Drawing.Size(500, 300),
                StartPosition = Forms.FormStartPosition.CenterScreen,
                AutoScaleMode = Forms.AutoScaleMode.Dpi, MinimizeBox = false,
            };
            var hint = new Forms.Label
            {
                Text = "この配置案に保存する説明です。部分書出しにも自動で含まれます。",
                Left = 16, Top = 16, Width = 628, Height = 32,
                Anchor = Forms.AnchorStyles.Top | Forms.AnchorStyles.Left | Forms.AnchorStyles.Right,
            };
            var description = new Forms.TextBox
            {
                Text = layout.Description ?? "", Multiline = true, AcceptsReturn = true,
                ScrollBars = Forms.ScrollBars.Vertical, Left = 16, Top = 52, Width = 628, Height = 285,
                Anchor = Forms.AnchorStyles.Top | Forms.AnchorStyles.Bottom | Forms.AnchorStyles.Left | Forms.AnchorStyles.Right,
            };
            var save = new Forms.Button { Text = "保存", Left = 418, Top = 353, Width = 108,
                Anchor = Forms.AnchorStyles.Bottom | Forms.AnchorStyles.Right };
            var cancel = new Forms.Button { Text = "キャンセル", Left = 536, Top = 353, Width = 108,
                Anchor = Forms.AnchorStyles.Bottom | Forms.AnchorStyles.Right, DialogResult = Forms.DialogResult.Cancel };
            save.Click += (_, _) =>
            {
                try
                {
                    if (description.Text != (layout.Description ?? ""))
                        owner.Execute(new SetDeskLayoutDescription(layout.Id, description.Text), selectedPlanEdit: false);
                    form.DialogResult = Forms.DialogResult.OK;
                }
                catch (Exception ex) { Forms.MessageBox.Show(form, ex.Message, "説明を保存できません"); }
            };
            form.Controls.AddRange([hint, description, save, cancel]);
            form.CancelButton = cancel;
            ShowEditorDialog(form);
        }
        catch (Exception ex) { ShowInAppMessage("説明を編集できません", ex.Message); }
        finally { modalInputDrain = true; }
    }
}
