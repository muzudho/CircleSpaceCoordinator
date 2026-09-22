namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using System.Text.Json;
using StationeryUI.Inspection;
using StationeryUI.Canvas;
using StationeryUI.Styling;

/// <summary>Layout-only integration: application settings own the reload policy.</summary>
public sealed class EventListStyle
{
    public static readonly string[] Actions = ["open", "create", "edit", "register", "duplicate", "confidential", "up", "down", "remove", "exit"];
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);
    private string? acceptedText;
    private string? pendingText;
    private TimeSpan elapsed;
    private bool wasEnabled;

    public string? FilePath { get; }
    public bool CanAutoReload => FilePath is not null;
    public StationeryStyleSettings Current { get; private set; }
    public string? LastError { get; private set; }
    public int Revision { get; private set; }
    public double RowHeight => RowTracks[0].Value;
    public double RowStride => RowTracks.Sum(track => track.Value);
    private IReadOnlyList<LayoutTrack> RowTracks => Current.Layouts.Single(layout => layout.Path == "eventRow").Rows;

    public static string DefaultJson
    {
        get
        {
            using var stream = typeof(EventListStyle).Assembly.GetManifestResourceStream("CircleSpaceCoordinator.EventListStyle.json")!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    public static EventListStyle CreateForApplication()
    {
#if DEBUG
        var source = System.Reflection.CustomAttributeExtensions
            .GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>(typeof(EventListStyle).Assembly)
            .Single(attribute => attribute.Key == "EventListStyleSource").Value!;
        return new(source);
#else
        return new();
#endif
    }

    // An explicit path is for development and tests; Release uses only embedded data.
    public EventListStyle(string? filePath = null)
    {
        FilePath = filePath is null ? null : Path.GetFullPath(filePath);
        Current = StationeryStyleSettings.Parse(DefaultJson);
        Validate(Current);
        if (CanAutoReload) Read(stable: false);
    }

    public bool Update(TimeSpan delta, bool enabled)
    {
        if (!CanAutoReload) return false;
        if (enabled != wasEnabled)
        {
            pendingText = null;
            elapsed = TimeSpan.Zero;
            wasEnabled = enabled;
        }
        if (!enabled) return false;
        elapsed += delta;
        if (elapsed < Interval) return false;
        elapsed = TimeSpan.Zero;
        return Read(stable: true);
    }

    private bool Read(bool stable)
    {
        try
        {
            var text = File.ReadAllText(FilePath!);
            if (text == acceptedText) { pendingText = null; LastError = null; return false; }
            if (stable && text != pendingText) { pendingText = text; return false; }
            var next = StationeryStyleSettings.Parse(text);
            Validate(next);
            Current = next;
            acceptedText = text;
            pendingText = null;
            LastError = null;
            Revision++;
            return true;
        }
        catch (Exception ex) when (IsReadError(ex))
        {
            pendingText = null;
            LastError = ex.Message;
            return false;
        }
    }

    private static bool IsReadError(Exception ex) => ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException;

    public static void Validate(StationeryStyleSettings style)
    {
        var root = style.Models[0].CreateTree();
        if (root.Path != "/events") throw new JsonException("イベント一覧のルート /events が必要です。");
        void Require(string path, string type, bool placed = true)
        {
            if (root.Resolve(path)?.Kind != type || placed && !style.Bindings.Any(binding => binding.Children.Any(child => child.ModelPath == path)))
                throw new JsonException($"必要なモデルまたはセル配置がありません: {path} ({type})");
        }
        foreach (var variant in new[] { "regular", "compact" })
        {
            var path = "/events/" + variant;
            Require(path, "container", false);
            foreach (var child in new[] { "title", "description", "body", "body/list", "body/actions", "error", "footer" }) Require(path + "/" + child, "container");
            Require(path + "/reload", "button");
            foreach (var action in Actions) Require(path + "/body/actions/" + action, "button");
            if (!style.Bindings.Any(binding => binding.ModelPath == path + "/body/actions" &&
                style.Layouts.Any(layout => layout.Path == binding.Layout && layout.Type == "grid-layout")))
                throw new JsonException($"操作欄の grid-layout が必要です: {path}/body/actions");
        }
        Require("/events/rowTemplate", "container", false);
        foreach (var child in new[] { "item", "item/title", "item/detail" }) Require("/events/rowTemplate/" + child, "container");
        var rows = style.Layouts.SingleOrDefault(layout => layout.Path == "eventRow");
        if (rows is null || rows.Type != "grid-layout" || rows.Rows.Count != 2 || rows.Rows.Any(row => row.IsRate) ||
            rows.Rows[0].Value < 1 || !double.IsFinite(rows.Rows.Sum(row => row.Value)) ||
            !style.Bindings.Any(binding => binding.Layout == "eventRow" && binding.ModelPath == "/events/rowTemplate" &&
                binding.Children.Any(child => child.ModelPath == "/events/rowTemplate/item" && child.Row == 0)))
            throw new JsonException("eventRow は行の高さ (1px 以上) と行間の2行を px で指定してください。");
        foreach (var size in new[] { (1280d, 680d), (800d, 480d), (0d, 0d) })
        {
            var result = StationeryLayoutEngine.Arrange(style, size.Item1, size.Item2);
            if (result.Bounds.Values.Concat(result.ContentBounds.Values).Any(box =>
                !double.IsFinite(box.X) || !double.IsFinite(box.Y) || !double.IsFinite(box.Width) || !double.IsFinite(box.Height)))
                throw new JsonException("配置寸法が大きすぎます。");
        }
    }

    public EventListLayout Arrange(double width, double height, double top)
    {
        var result = StationeryLayoutEngine.Arrange(Current, Math.Max(0, width), Math.Max(0, height - top));
        var actions = Current.Bindings.Single(binding => binding.ModelPath == "/events/regular/body/actions" &&
            Current.Layouts.Any(layout => layout.Path == binding.Layout && layout.Type == "grid-layout"));
        var desiredHeight = Current.Layouts.Single(layout => layout.Path == actions.Layout).Rows.Where(row => !row.IsRate).Sum(row => row.Value);
        return new(this, result, result.ContentBounds["/events/regular/body/actions"].Height < desiredHeight
            ? "/events/compact" : "/events/regular", top);
    }
}

public sealed class EventListLayout(EventListStyle style, StationeryLayoutResult result, string root, double top)
{
    private static readonly IReadOnlyDictionary<string, string> InspectionLabels = new Dictionary<string, string>
    {
        [""] = "イベント一覧", ["title"] = "見出し", ["description"] = "説明", ["body"] = "本文",
        ["body/list"] = "イベント一覧の表示領域", ["body/actions"] = "操作欄",
        ["reload"] = "スタイル設定のオートリロード", ["error"] = "スタイル設定の状態", ["footer"] = "操作案内",
        ["body/actions/open"] = "開く", ["body/actions/create"] = "新規作成", ["body/actions/edit"] = "編集",
        ["body/actions/register"] = "既存ファイルを登録", ["body/actions/duplicate"] = "複製",
        ["body/actions/confidential"] = "マル秘に設定", ["body/actions/up"] = "上へ", ["body/actions/down"] = "下へ",
        ["body/actions/remove"] = "一覧から除外", ["body/actions/exit"] = "終了",
    };

    /// <summary>Report registered models; inactive variants and templates have no live screen bounds.</summary>
    public IReadOnlyList<StationeryInspectionEntry> Inspect(bool pageVisible)
    {
        var entries = new List<StationeryInspectionEntry>();
        void Visit(StationeryNode node)
        {
            var isRoot = node.Path == "/events";
            var inVariant = node.Path == root || node.Path.StartsWith(root + "/", StringComparison.Ordinal);
            var relative = inVariant ? node.Path[root.Length..].TrimStart('/') : "";
            var connected = isRoot || inVariant && InspectionLabels.ContainsKey(relative);
            var visible = pageVisible && connected;
            ScreenRectangle? bounds = visible ? result.ContentBounds[node.Path] with { Y = result.ContentBounds[node.Path].Y + top } : null;
            var label = isRoot ? "イベント一覧（登録済みモデル）" : inVariant && InspectionLabels.TryGetValue(relative, out var name)
                ? name : node.Path.StartsWith("/events/rowTemplate", StringComparison.Ordinal)
                    ? node.Id + "（行のひな型・表示実体なし）" : node.Id;
            entries.Add(new(node.Id, node.Path, node.Parent?.Path, node.Kind, label, visible, bounds));
            foreach (var child in node.Children) Visit(child);
        }
        Visit(style.Current.Models[0].CreateTree());
        return entries;
    }

    private readonly StationeryLayoutResult rowTemplate = StationeryLayoutEngine.Arrange(style.Current,
        result.ContentBounds[root + "/body/list"].Width, style.RowStride);
    public ScreenRectangle Area(string name)
    {
        var box = result.ContentBounds[root + "/" + name];
        return box with { Y = box.Y + top };
    }

    public int VisibleRows => Math.Max(0, (int)Math.Min(int.MaxValue, Math.Floor((Area("body/list").Height + style.RowStride - style.RowHeight) / style.RowStride)));

    public ScreenRectangle Row(int index, string part = "")
    {
        var list = Area("body/list");
        var row = rowTemplate.Bounds["/events/rowTemplate/item" + part];
        return row with { X = row.X + list.X, Y = row.Y + list.Y + index * style.RowStride };
    }
}
