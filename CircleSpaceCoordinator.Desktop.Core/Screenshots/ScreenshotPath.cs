namespace CircleSpaceCoordinator.Desktop.Core.Screenshots;

using System.Security.Cryptography;
using System.Text;

public static class ScreenshotPath
{
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "CircleSpaceCoordinator",
        "Screenshots");

    public static string ForProject(string projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        // The event ID survives file moves; the opaque folder name does not expose the event name.
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(projectId)))[..16];
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CircleSpaceCoordinator", "Screenshots", "project-" + id);
    }

    public static string Create(string directory, DateTime now) => Path.Combine(
        directory,
        $"screenshot-{now:yyyyMMdd-HHmmss-fff}.png");
}
