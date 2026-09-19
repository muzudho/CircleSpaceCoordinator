namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

using System.Runtime.InteropServices;

public static class UserSettingsPaths
{
    public static string GetDirectory() => ResolveDirectory(
        OperatingSystem.IsWindows() ? OSPlatform.Windows : OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Linux,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"));

    public static string ResolveDirectory(OSPlatform platform, string userProfile, string localApplicationData, string? xdgConfigHome)
    {
        var root = platform == OSPlatform.Windows ? localApplicationData
            : platform == OSPlatform.OSX ? Path.Combine(userProfile, "Library", "Application Support")
            : !string.IsNullOrWhiteSpace(xdgConfigHome) && Path.IsPathFullyQualified(xdgConfigHome)
                ? xdgConfigHome : Path.Combine(userProfile, ".config");
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new IOException("ユーザー設定フォルダーを取得できませんでした。");
        return Path.Combine(root, "CircleSpaceCoordinator");
    }

    // Only the executable being started is a migration source; never search other installations.
    public static string PrepareFile(string fileName) =>
        PrepareFile(fileName, AppContext.BaseDirectory, GetDirectory());

    public static string PrepareFile(string fileName, string legacyDirectory, string settingsDirectory)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            throw new ArgumentException("A settings file name is required.", nameof(fileName));
        Directory.CreateDirectory(settingsDirectory);
        var destination = Path.Combine(settingsDirectory, fileName);
        var legacy = Path.Combine(legacyDirectory, fileName);
        if (!File.Exists(destination) && File.Exists(legacy))
        {
            // Publish a complete copy atomically and preserve both an existing destination and the source.
            var temporary = Path.Combine(settingsDirectory, $".{fileName}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.Copy(legacy, temporary);
                try { File.Move(temporary, destination, overwrite: false); }
                catch (IOException) when (File.Exists(destination)) { }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return destination;
    }
}
