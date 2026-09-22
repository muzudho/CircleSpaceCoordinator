namespace CircleSpaceCoordinator.Desktop.Tests;

using System.Text.Json.Nodes;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using StationeryUI.Inspection;
using StationeryUI.Styling;

internal static partial class Program
{
    private static void EventStyleReloadAndSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), $"csc-event-style-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var settingsPath = Path.Combine(root, "application-settings.json");
            File.WriteAllText(settingsPath, "{\"projectsDirectory\":\"\",\"handle\":\"style tester\"}");
            var settings = CreateIsolatedSettings(settingsPath);
            AssertEqual(true, settings.Current.StyleAutoReload);
            settings.SaveStyleAutoReload(false);
            settings = CreateIsolatedSettings(settingsPath);
            AssertEqual(false, settings.Current.StyleAutoReload);
            AssertEqual("style tester", settings.Current.Handle);
            AssertEqual(false, JsonNode.Parse(File.ReadAllText(settingsPath))!["styleAutoReload"]!.GetValue<bool>());

            var path = Path.Combine(root, "events.style-settings.md");
            var missing = new EventListStyle(path);
            AssertEqual(false, File.Exists(path)); // Loading never creates or overwrites source files.
            AssertEqual(true, missing.LastError is not null);
            File.WriteAllText(path, EventListStyle.DefaultJson);
            var style = new EventListStyle(path);
            AssertEqual(true, File.Exists(path));
            AssertEqual<string?>(null, style.LastError);
            var normal = style.Arrange(1280, 800, 40);
            AssertEqual(24d, normal.Area("title").X);
            AssertEqual(62d, normal.Area("title").Y);
            AssertEqual(224d, normal.Area("body/actions").Width);
            AssertEqual(normal.Area("body/actions/open").X, normal.Area("body/actions/create").X);
            var inspection = normal.Inspect(true);
            AssertEqual(inspection.Count, inspection.Select(entry => entry.Path).Distinct().Count());
            var open = inspection.Single(entry => entry.Path == "/events/regular/body/actions/open");
            AssertEqual(true, open.Visible);
            AssertEqual("button", open.Kind);
            AssertEqual("/events/regular/body/actions", open.ParentPath);
            AssertEqual(normal.Area("body/actions/open"), open.WindowBounds!.Value);
            var openBounds = open.WindowBounds.Value;
            AssertEqual(open.Path, DeveloperCapture.HitTest(inspection,
                openBounds.X + openBounds.Width / 2, openBounds.Y + openBounds.Height / 2)!.Path);
            AssertEqual<StationeryInspectionEntry?>(null, DeveloperCapture.HitTest(normal.Inspect(false),
                openBounds.X + openBounds.Width / 2, openBounds.Y + openBounds.Height / 2));

            // v0.2.0 normalizes legacy JSON names; existing user files must still load.
            var legacyJson = EventListStyle.DefaultJson.Replace("\"grid-layout\"", "\"floating-layout\"")
                .Replace("\"box-layout\"", "\"panel\"");
            EventListStyle.Validate(StationeryStyleSettings.Parse(legacyJson));
            File.WriteAllText(path, legacyJson);
            var legacy = new EventListStyle(path);
            AssertEqual<string?>(null, legacy.LastError);
            AssertEqual(normal.Area("body/actions/open"), legacy.Arrange(1280, 800, 40).Area("body/actions/open"));
            File.WriteAllText(path, EventListStyle.DefaultJson);
            AssertEqual(false, inspection.Single(entry => entry.Path == "/events/compact/body/actions/open").Visible);
            AssertEqual(false, inspection.Single(entry => entry.Path == "/events/rowTemplate/item").Visible);
            AssertEqual(true, inspection.Single(entry => entry.Path == "/events/rowTemplate/item").WindowBounds is null);
            AssertEqual(true, normal.Inspect(false).All(entry => !entry.Visible && entry.WindowBounds is null));
            var compact = style.Arrange(800, 500, 40);
            AssertEqual(compact.Area("body/actions/open").Y, compact.Area("body/actions/create").Y);
            AssertEqual(true, compact.Area("body/actions/create").X > compact.Area("body/actions/open").X);
            AssertEqual(true, compact.Inspect(true).Single(entry => entry.Path == "/events/compact/body/actions/open").Visible);
            AssertEqual(false, compact.Inspect(true).Single(entry => entry.Path == "/events/regular/body/actions/open").Visible);
            foreach (var size in new[] { (1280d, 800d), (800d, 500d), (100d, 100d), (0d, 0d) })
            {
                var layout = style.Arrange(size.Item1, size.Item2, 40);
                AssertEqual(true, layout.VisibleRows >= 0);
                if (layout.VisibleRows > 0)
                {
                    var row = layout.Row(layout.VisibleRows - 1);
                    var list = layout.Area("body/list");
                    AssertEqual(true, row.Y + row.Height <= list.Y + list.Height + 0.001);
                }
                foreach (var action in EventListStyle.Actions)
                {
                    var bounds = layout.Area("body/actions/" + action);
                    AssertEqual(true, bounds.Width >= 0 && bounds.Height >= 0 && double.IsFinite(bounds.Y));
                }
            }

            var json = JsonNode.Parse(EventListStyle.DefaultJson)!;
            var panel = json["layouts"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == "regularPanel")!;
            panel["padding"]!["left"] = "60px";
            var changed = json.ToJsonString();
            File.WriteAllText(path, changed);
            var previous = style.Current;
            AssertEqual(false, style.Update(TimeSpan.FromSeconds(2), false));
            AssertEqual(true, ReferenceEquals(previous, style.Current));
            AssertEqual(false, style.Update(TimeSpan.FromMilliseconds(500), true));
            AssertEqual(true, style.Update(TimeSpan.FromMilliseconds(500), true));
            AssertEqual(60d, style.Arrange(1280, 800, 40).Area("title").X);
            AssertEqual(60d, style.Arrange(1280, 800, 40).Inspect(true)
                .Single(entry => entry.Path == "/events/regular/title").WindowBounds!.Value.X);
            AssertEqual(60d, new EventListStyle(path).Arrange(1280, 800, 40).Area("title").X);

            foreach (var bad in new[] { "{", changed.Replace("\"open\"", "\"missingOpen\""),
                changed.Replace("\"58px\"", "\"0px\"") })
            {
                previous = style.Current;
                File.WriteAllText(path, bad);
                style.Update(TimeSpan.FromMilliseconds(500), true);
                AssertEqual(false, style.Update(TimeSpan.FromMilliseconds(500), true));
                AssertEqual(true, ReferenceEquals(previous, style.Current));
                AssertEqual(true, style.LastError is not null);
            }
            var fallback = new EventListStyle(path);
            AssertEqual(true, fallback.LastError is not null);
            AssertEqual(24d, fallback.Arrange(1280, 800, 40).Area("title").X);
            AssertEqual(true, File.ReadAllText(path) != EventListStyle.DefaultJson); // Invalid user edits are never overwritten.
            File.WriteAllText(path, changed);
            style.Update(TimeSpan.FromMilliseconds(500), true);
            AssertEqual<string?>(null, style.LastError);
            settings.SaveStyleAutoReload(true);
            AssertEqual(true, CreateIsolatedSettings(settingsPath).Current.StyleAutoReload);

            var embedded = new EventListStyle();
            AssertEqual<string?>(null, embedded.FilePath);
            AssertEqual(false, embedded.CanAutoReload);
            AssertEqual(false, embedded.Update(TimeSpan.FromSeconds(2), true));
            AssertEqual<string?>(null, embedded.LastError);
            AssertEqual(24d, embedded.Arrange(1280, 800, 40).Area("title").X);
            var application = EventListStyle.CreateForApplication();
#if DEBUG
            AssertEqual(true, application.CanAutoReload);
            AssertEqual(true, Path.IsPathFullyQualified(application.FilePath!));
            AssertEqual(true, application.FilePath!.EndsWith(Path.Combine("App_Doc", "events.style-settings.md")));
            AssertEqual(EventListStyle.DefaultJson, File.ReadAllText(application.FilePath));
#else
            AssertEqual(false, application.CanAutoReload);
            AssertEqual<string?>(null, application.FilePath);
            AssertEqual(false, application.Update(TimeSpan.FromSeconds(2), true));
            AssertEqual(false, typeof(EventListStyle).Assembly.GetCustomAttributes(false)
                .OfType<System.Reflection.AssemblyMetadataAttribute>().Any(attribute => attribute.Key == "EventListStyleSource"));
#endif
            AssertEqual<string?>(null, application.LastError);

            // A failed settings write must leave the visible toggle and runtime policy unchanged.
            File.Delete(settingsPath);
            Directory.CreateDirectory(settingsPath);
            var failed = false;
            try { settings.SaveStyleAutoReload(false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed = true; }
            AssertEqual(true, failed);
            AssertEqual(true, settings.Current.StyleAutoReload);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
