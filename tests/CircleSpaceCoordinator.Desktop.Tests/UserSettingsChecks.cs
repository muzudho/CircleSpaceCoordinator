namespace CircleSpaceCoordinator.Desktop.Tests;

using System.Runtime.InteropServices;
using CircleSpaceCoordinator.Desktop.Core.Persistence;

internal static partial class Program
{
    private static ApplicationSettingsService CreateIsolatedSettings(string path) =>
        new(path, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "discovery"));

    private static void UserSettingsSurviveBuildOutputRemoval()
    {
        var root = Path.Combine(Path.GetTempPath(), $"csc-settings-{Guid.NewGuid():N}");
        var legacy = Path.Combine(root, "bin");
        var user = Path.Combine(root, "user");
        var projects = Path.Combine(root, "projects");
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(projects);
        try
        {
            AssertEqual(Path.Combine(user, "CircleSpaceCoordinator"), UserSettingsPaths.ResolveDirectory(OSPlatform.Windows, root, user, null));
            AssertEqual(Path.Combine(root, "Library", "Application Support", "CircleSpaceCoordinator"), UserSettingsPaths.ResolveDirectory(OSPlatform.OSX, root, user, null));
            AssertEqual(Path.Combine(user, "CircleSpaceCoordinator"), UserSettingsPaths.ResolveDirectory(OSPlatform.Linux, root, "", user));
            foreach (var xdg in new string?[] { null, "", "relative" })
                AssertEqual(Path.Combine(root, ".config", "CircleSpaceCoordinator"), UserSettingsPaths.ResolveDirectory(OSPlatform.Linux, root, "", xdg));

            var eventPath = Path.Combine(projects, "existing.event-project-csc.json");
            ProjectFileService.Save(eventPath, CreateProject() with { Name = "初回検索" });
            File.WriteAllText(Path.Combine(projects, "unrelated.json"), "{}");
            var firstPath = Path.Combine(root, "fresh", "application-settings.json");
            var first = new ApplicationSettingsService(firstPath, projects);
            AssertEqual(1, first.Current.EventProjects!.Count);
            AssertEqual("初回検索", first.Current.EventProjects[0].DisplayName);
            AssertEqual(true, File.Exists(firstPath));

            var legacyPath = Path.Combine(legacy, "application-settings.json");
            var old = new ApplicationSettingsService(legacyPath, projects);
            old.SaveHandle("引継ぎ担当");
            old.SaveWorkingState(new ProjectWorkingState(eventPath, "plan-1", "DeskPlacement", 1.5, 20, 30));
            var source = File.ReadAllText(legacyPath);
            var migratedPath = UserSettingsPaths.PrepareFile("application-settings.json", legacy, user);
            AssertEqual(source, File.ReadAllText(migratedPath));
            AssertEqual(source, File.ReadAllText(legacyPath));
            var migrated = new ApplicationSettingsService(migratedPath, projects);
            AssertEqual("引継ぎ担当", migrated.Current.Handle);
            AssertEqual(1.5, migrated.GetWorkingState(eventPath)!.Zoom);
            migrated.SaveHandle("更新担当");
            UserSettingsPaths.PrepareFile("application-settings.json", legacy, user);
            AssertEqual("更新担当", new ApplicationSettingsService(migratedPath, projects).Current.Handle);

            File.WriteAllText(Path.Combine(legacy, "space-definitions.json"), "{\"version\":2}");
            var definitions = UserSettingsPaths.PrepareFile("space-definitions.json", legacy, user);
            AssertEqual("{\"version\":2}", File.ReadAllText(definitions));
            Directory.Delete(legacy, recursive: true);
            AssertEqual(migratedPath, UserSettingsPaths.PrepareFile("application-settings.json", legacy, user));
            AssertEqual(1, new ApplicationSettingsService(migratedPath, projects).Current.EventProjects!.Count);
            AssertEqual(true, File.Exists(definitions));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
