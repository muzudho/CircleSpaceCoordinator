namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using System.Text.Json;
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

    public string FilePath { get; }
    public StationeryStyleSettings Current { get; private set; }
    public string? LastError { get; private set; }
    public int Revision { get; private set; }
    public double RowHeight => RowTracks[0].Value;
    public double RowStride => RowTracks.Sum(track => track.Value);
    private IReadOnlyList<LayoutTrack> RowTracks => Current.Layouts.Single(layout => layout.Id == "eventRow").Rows;

    public static string DefaultJson
    {
        get
        {
            using var stream = typeof(EventListStyle).Assembly.GetManifestResourceStream("CircleSpaceCoordinator.EventListStyle.json")!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    public EventListStyle(string filePath)
    {
        FilePath = Path.GetFullPath(filePath);
        var fallback = DefaultJson;
        Current = StationeryStyleSettings.Parse(fallback);
        Validate(Current);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if (!File.Exists(FilePath))
            {
                using var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream);
                writer.Write(fallback);
            }
        }
        catch (Exception ex) when (IsReadError(ex)) { LastError = ex.Message; }
        Read(stable: false); // Startup always reads the saved style, even when monitoring is disabled.
    }

    public bool Update(TimeSpan delta, bool enabled)
    {
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
            var text = File.ReadAllText(FilePath);
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
                style.Layouts.Any(layout => layout.Id == binding.Layout && layout.Type == "floating-layout")))
                throw new JsonException($"操作欄の floating-layout が必要です: {path}/body/actions");
        }
        Require("/events/rowTemplate", "container", false);
        foreach (var child in new[] { "item", "item/title", "item/detail" }) Require("/events/rowTemplate/" + child, "container");
        var rows = style.Layouts.SingleOrDefault(layout => layout.Id == "eventRow");
        if (rows is null || rows.Type != "floating-layout" || rows.Rows.Count != 2 || rows.Rows.Any(row => row.IsRate) ||
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
            Current.Layouts.Any(layout => layout.Id == binding.Layout && layout.Type == "floating-layout"));
        var desiredHeight = Current.Layouts.Single(layout => layout.Id == actions.Layout).Rows.Where(row => !row.IsRate).Sum(row => row.Value);
        return new(this, result, result.ContentBounds["/events/regular/body/actions"].Height < desiredHeight
            ? "/events/compact" : "/events/regular", top);
    }
}

public sealed class EventListLayout(EventListStyle style, StationeryLayoutResult result, string root, double top)
{
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
