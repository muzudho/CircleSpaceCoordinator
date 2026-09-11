namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Persistence;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private int circleChannelScroll;
    private string? lastCircleDisplayKey;
    private CircleLabelDisplaySettings? circleDisplayOverride;
    private CircleLabelDisplaySettings CurrentCircleDisplay
    {
        get
        {
            var display = circleDisplayOverride ?? settings?.Current.CircleLabelDisplay ?? new CircleLabelDisplaySettings();
            // A removed channel or a different project must not leave an invisible selection.
            return display.DisplayField == "channel" &&
                workspace?.Project.Evaluation.Features.Any(item => item.Id == display.ChannelId) != true
                ? display with { DisplayField = "internalId" } : display;
        }
    }

    private sealed record CircleDisplayChannel(string Field, string? ChannelId, string Name, string Detail);

    private IReadOnlyList<CircleDisplayChannel> GetCircleDisplayChannels() =>
    [
        new("displayName", null, "サークル名", "サークル名を表示"),
        new("circleId", null, "サークルID", "サークルIDを表示（正規表現で置換可能）"),
        new("internalId", null, "内部ID", "従来の石番号を表示"),
        .. (workspace?.Project.Evaluation.Features.Select(item =>
            new CircleDisplayChannel("channel", item.Id, item.Name, $"列: {item.SourceColumn ?? "（対応なし）"}")) ?? []),
    ];

    private ScreenRectangle CircleRegexButtonBounds()
    {
        var panel = GetChannelPanelBounds();
        return new ScreenRectangle(panel.X + 8, panel.Y + 34, panel.Width - 16, 26);
    }

    private void DrawCircleDisplayChannels()
    {
        var panel = GetChannelPanelBounds();
        var channels = GetCircleDisplayChannels();
        var display = CurrentCircleDisplay;
        var selectionKey = display.DisplayField + ":" + display.ChannelId;
        if (lastCircleDisplayKey != selectionKey)
        {
            var selectedIndex = channels.ToList().FindIndex(item => item.Field == display.DisplayField &&
                (item.Field != "channel" || item.ChannelId == display.ChannelId));
            if (selectedIndex < circleChannelScroll || selectedIndex >= circleChannelScroll + VisibleChannelRows)
                circleChannelScroll = Math.Max(0, selectedIndex - VisibleChannelRows + 1);
            lastCircleDisplayKey = selectionKey;
        }
        circleChannelScroll = Math.Clamp(circleChannelScroll, 0, Math.Max(0, channels.Count - VisibleChannelRows));
        DrawRectangle(panel, new Color(20, 25, 32, 245));
        DrawOutline(panel, 2, new Color(88, 103, 120));
        textRenderer?.Draw("チャンネル", new Rectangle((int)panel.X + 10, (int)panel.Y + 4, 240, 26), Color.White, 20, true);
        DrawLayoutButton(CircleRegexButtonBounds(), "サークルIDの正規表現", false);
        for (var rowIndex = 0; rowIndex < VisibleChannelRows && circleChannelScroll + rowIndex < channels.Count; rowIndex++)
        {
            var item = channels[circleChannelScroll + rowIndex];
            var selected = display.DisplayField == item.Field && (item.Field != "channel" || display.ChannelId == item.ChannelId);
            var row = ChannelRow(rowIndex);
            DrawRectangle(row, selected ? new Color(35, 126, 111) : new Color(29, 36, 45));
            textRenderer?.Draw(item.Name, new Rectangle((int)row.X + 6, (int)row.Y + 2, 240, 32), Color.White, 16, selected);
        }
        var selectedChannel = channels.First(item => item.Field == display.DisplayField &&
            (item.Field != "channel" || item.ChannelId == display.ChannelId));
        textRenderer?.Draw(selectedChannel.Detail, new Rectangle((int)panel.X + 8,
            (int)(panel.Y + panel.Height) - 46, 248, 24), new Color(184, 204, 214), 12);
        textRenderer?.Draw("クリックで表示切替 / ホイールでリスト移動", new Rectangle((int)panel.X + 8,
            (int)(panel.Y + panel.Height) - 22, 248, 20), Color.LightGray, 11);
    }

    private bool HandleCircleDisplayChannelClick(ScreenPoint pointer)
    {
        try
        {
            if (Contains(CircleRegexButtonBounds(), pointer))
            {
                var result = CircleLabelDisplayDialog.Show(CurrentCircleDisplay);
                if (result is not null) SaveCircleDisplay(result);
                return true;
            }
            var channels = GetCircleDisplayChannels();
            for (var row = 0; row < VisibleChannelRows && circleChannelScroll + row < channels.Count; row++)
            {
                if (!Contains(ChannelRow(row), pointer)) continue;
                var item = channels[circleChannelScroll + row];
                SaveCircleDisplay(CurrentCircleDisplay with { DisplayField = item.Field, ChannelId = item.ChannelId });
                Log("circle_display_channel", success: true, detail: $"field={item.Field}");
                break;
            }
        }
        catch (Exception exception) { ShowChannelError(exception); }
        return true;
    }

    private void SaveCircleDisplay(CircleLabelDisplaySettings display)
    {
        settings?.SaveCircleLabelDisplay(display);
        circleDisplayOverride = display;
    }
}
