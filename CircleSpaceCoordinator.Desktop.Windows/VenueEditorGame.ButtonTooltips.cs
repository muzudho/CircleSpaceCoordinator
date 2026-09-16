namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private string HoveredButtonDescription()
    {
        var mouse = Mouse.GetState();
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        if (toolRingOpen)
        {
            var index = toolRingButtons.FindIndex(button => Contains(button.Bounds, pointer));
            return index >= 0 ? toolRingDefinition.Entries[index].Description : "";
        }
        if (spaceCatalogOpen)
            return catalogButtons.FirstOrDefault(item => Contains(item.Model.Bounds, pointer)).Model?.AccessibleName ?? "";
        if (!CanShowEditorHover) return "";
        var toolbar = toolbarButtons.FirstOrDefault(item => Contains(item.Model.Bounds, pointer));
        if (toolbar is not null)
            return activeCanvasTool == ToolbarAction.AddDesk && toolbar.Action is ToolbarAction.RotateLeft or ToolbarAction.RotateRight
                ? toolbar.Action == ToolbarAction.RotateLeft ? "次のフレームを左へ90度回転" : "次のフレームを右へ90度回転"
                : toolbar.Model.AccessibleName;
        if (editorMode == EditorMode.DeskPlacement)
        {
            if (Contains(NextSpaceSelectionButton.Bounds, pointer)) return "フレームカタログを開き、次に配置するフレームを選択する";
            if (NextSpace is not null && Contains(NextDirectionBounds, pointer)) return "次に配置するフレームの向きを選択する";
        }
        if (workspace is null) return "";
        if (ShowsChannels)
        {
            if (editorMode == EditorMode.CirclePlacement)
            {
                if (Contains(CircleRegexButtonBounds(), pointer)) return "サークルIDの表示に使う正規表現を設定する";
            }
            else
            {
                if (Contains(ChannelButton(0), pointer)) return "評価値のチャンネルを追加する";
                if (Contains(ChannelButton(1), pointer)) return "選択中のチャンネルの読込列・名前を変更する";
                if (Contains(ChannelButton(2), pointer)) return "選択中の評価値チャンネルを削除する";
                if (ShowsChannelFooterButton && Contains(BlockStyleButton(), pointer))
                    return ShowsBlockStyleButton ? "ブロック番号と色・網掛けパターンの対応を編集する"
                        : selectedNumberChannel == 1 ? "旗からの順番でフレーム番号を自動連番する"
                        : "セル番号入力ウィザードを開く";
            }
        }
        if (UsesSeparatedLayouts && editorMode is EditorMode.DeskPlacement or EditorMode.IslandDefinition or EditorMode.GenrePlacement or EditorMode.CirclePlacement)
        {
            var layout = ShowsDeskLayouts ? "フレーム配置" : "サークル配置";
            if (Contains(GetLayoutAddBounds(), pointer)) return $"{layout}を追加する";
            if (Contains(GetLayoutDuplicateBounds(), pointer)) return $"選択中の{layout}を複製する";
            if (Contains(GetLayoutDeleteBounds(), pointer)) return $"選択中の{layout}を削除する";
            if (Contains(GetLayoutRenameBounds(), pointer)) return $"選択中の{layout}の名前を変更する";
            if (!ShowsDeskLayouts && Contains(GetLayoutBindBounds(), pointer)) return "選択中のサークル配置のフレーム配置を変更する";
            if (!ShowsDeskLayouts && Contains(GetDeskLayoutParentBounds(), pointer)) return "表示するフレーム配置を選択する";
            if (ShowsLayoutOrder)
                foreach (var direction in new[] { -1, 1 })
                    if (Contains(LayoutOrderButton(direction), pointer))
                        return direction < 0 ? "選択中のフレーム配置を1つ上へ移動する" : "選択中のフレーム配置を1つ下へ移動する";
        }
        return "";
    }
}
