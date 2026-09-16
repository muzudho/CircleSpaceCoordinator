namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private void DrawFrameTargetCatalog()
    {
        if (frameDraft is not { } draft) return;
        void Text(string value, ScreenRectangle bounds, int size = 15, Color? color = null) =>
            textRenderer?.Draw(value, ToRectangle(bounds, 0), color ?? Color.White, Math.Max(10, (int)(size * FrameScale)));
        if (frameTargets.Length == 0)
        {
            Text("割当先がありません。先にフレームの型へ区画を定義してください。", FrameBounds(20, 220, 960, 60), 18);
            return;
        }
        var pointer = new ScreenPoint(previousMouse.X, previousMouse.Y);
        for (var slot = 0; slot < FrameTargetPageSize && frameTargetScroll + slot < frameTargets.Length; slot++)
        {
            var index = frameTargetScroll + slot;
            var item = frameTargets[index];
            var selected = draft.Targets.Contains(item.Target);
            var x = 20 + slot % FrameTargetColumns * 224;
            var y = 206 + slot / FrameTargetColumns * 164;
            var bounds = FrameTargetCard(slot);
            DrawRectangle(bounds, selected ? new Color(26, 82, 73) : new Color(35, 43, 54));
            DrawOutline(bounds, 1, selected ? Color.Teal : new Color(65, 76, 92));
            if (Contains(bounds, pointer) || frameFocus < 0 && index == frameTargetSelection)
                DrawOutline(bounds, 2, Color.Turquoise);
            DrawSpaceTypePreview(item.Type, QuarterTurn.North, FrameBounds(x + 12, y + 8, 190, 86), item.Target.Area);
            var count = item.Type.Cells.Count(cell => cell.Area == item.Target.Area);
            Text($"{(selected ? "☑" : "☐")} 区画{item.Target.Area} · {count}セル", FrameBounds(x + 10, y + 97, 194, 23), 16);
            Text(item.Type.Name, FrameBounds(x + 10, y + 123, 194, 28), 13, Color.LightGray);
        }
        var current = frameTargets[frameTargetSelection];
        DrawRectangle(FrameBounds(704, 206, 276, 320), new Color(28, 36, 47));
        DrawSpaceTypePreview(current.Type, QuarterTurn.North, FrameBounds(716, 218, 252, 136), current.Target.Area);
        Text($"区画{current.Target.Area} · {current.Type.Cells.Count(cell => cell.Area == current.Target.Area)}セル" +
            (draft.Targets.Contains(current.Target) ? "  選択済み" : "  未選択"), FrameBounds(716, 364, 252, 26), 17);
        Text(current.Type.Name, FrameBounds(716, 390, 252, 106), 14);
        Text($"{current.Type.Kind} ／ {current.Type.Width}×{current.Type.Height}\n色付きの区画が割当対象", FrameBounds(716, 496, 252, 30), 12, Color.LightGray);
        Text($"{draft.Targets.Count}区画を選択 ／ {frameTargets.Length}候補　{frameTargetScroll / FrameTargetPageSize + 1}/{(frameTargets.Length + FrameTargetPageSize - 1) / FrameTargetPageSize}ページ",
            FrameBounds(20, 550, 600, 24));
        Text("矢印：移動 ／ Space：選択・解除 ／ ホイール：ページ切替", FrameBounds(20, 578, 600, 24), 14, Color.LightGray);
    }
}
