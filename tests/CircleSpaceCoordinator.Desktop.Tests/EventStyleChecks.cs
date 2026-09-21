namespace CircleSpaceCoordinator.Desktop.Tests;

using System.Text.Json.Nodes;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;

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

            var path = Path.Combine(root, "events.stationery-style.json");
            var style = new EventListStyle(path);
            AssertEqual(true, File.Exists(path));
            AssertEqual<string?>(null, style.LastError);
            var normal = style.Arrange(1280, 800, 40);
            AssertEqual(24d, normal.Area("title").X);
            AssertEqual(62d, normal.Area("title").Y);
            AssertEqual(224d, normal.Area("body/actions").Width);
            AssertEqual(normal.Area("body/actions/open").X, normal.Area("body/actions/create").X);
            var compact = style.Arrange(800, 500, 40);
            AssertEqual(compact.Area("body/actions/open").Y, compact.Area("body/actions/create").Y);
            AssertEqual(true, compact.Area("body/actions/create").X > compact.Area("body/actions/open").X);
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
