namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Desktop.Core.Persistence;
using System.Text;

internal static partial class Program
{
    private static void AutoSaveAndSavePoints()
    {
        var root = Path.Combine(Path.GetTempPath(), "csc-autosave-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var project = Path.Combine(root, "event.event-project-csc.json");
            File.WriteAllText(project, "original private data");
            var backups = new SavePointStore(Path.Combine(root, "backups"), project, 2);
            var session = new AutoSaveSession(project);
            var day = new DateOnly(2026, 9, 19);
            session.Save("edit1", backups, day);
            AssertEqual("edit1", File.ReadAllText(project));
            var start = backups.List().Single();
            AssertEqual("original private data", backups.Read(start.Id));
            session.Save("edit2", backups, day);
            AssertEqual(1, backups.List().Count);
            backups.SetProtected(start.Id, true);
            for (var i = 0; i < 5; i++) backups.Create("secret snapshot " + i, "private name " + i);
            AssertEqual(3, backups.List().Count);
            AssertEqual(true, backups.List().Single(item => item.Id == start.Id).Protected);
            foreach (var file in Directory.EnumerateFiles(root, "*.csc-savepoint", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                AssertEqual(false, content.Contains("secret snapshot"));
                AssertEqual(false, content.Contains("private name"));
                AssertEqual(false, content.Contains("original private data"));
            }
            backups.SetProtected(start.Id, false);
            backups.Create("newest", "newest");
            AssertEqual(2, backups.List().Count);
            session.Save("edit3", backups, day);
            AssertEqual(2, backups.List().Count); // daily marker survives rotation
            session.Save("next-day", backups, day.AddDays(1));
            AssertEqual("edit3", backups.Read(backups.List().Single(item => item.DailyDate == "2026-09-20").Id));
            var snapshot = backups.List().First();
            backups.Create("next-day", "Before restore", protect: true);
            session.Save(backups.Read(snapshot.Id), backups, day.AddDays(1));
            AssertEqual(true, backups.List().Any(item => item.Protected && backups.Read(item.Id) == "next-day"));
            var restoreResult = File.ReadAllText(project);
            File.WriteAllText(project, "external change");
            RejectPortable(() => session.Save("do not overwrite", backups, day));
            AssertEqual("external change", File.ReadAllText(project));
            File.WriteAllText(project, restoreResult);
            var blockedRoot = Path.Combine(root, "not-a-directory");
            File.WriteAllText(blockedRoot, "block");
            var failed = new AutoSaveSession(project);
            RejectPortable(() => failed.Save("unsaved edit", new SavePointStore(blockedRoot, project), day));
            AssertEqual(restoreResult, File.ReadAllText(project));
            var emptyProject = Path.Combine(root, "new.event-project-csc.json");
            new AutoSaveSession(emptyProject).Save("first version", new SavePointStore(Path.Combine(root, "backups"), emptyProject), day);
            AssertEqual("first version", File.ReadAllText(emptyProject));
            var settingsPath = Path.Combine(root, "settings.json");
            var settings = new ApplicationSettingsService(settingsPath);
            settings.ConfigureBackups(Path.Combine(root, "custom"), 7);
            var reloaded = new ApplicationSettingsService(settingsPath);
            AssertEqual(7, reloaded.Current.BackupGenerations);
            AssertEqual(Path.Combine(root, "custom"), reloaded.Current.BackupDirectory);
            var protectedBytes = WindowsBackupProtection.Protect(Encoding.UTF8.GetBytes("round trip"));
            AssertEqual("round trip", Encoding.UTF8.GetString(WindowsBackupProtection.Unprotect(protectedBytes)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
