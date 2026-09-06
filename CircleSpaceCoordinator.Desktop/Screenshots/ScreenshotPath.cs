namespace CircleSpaceCoordinator.Desktop.Screenshots;

internal static class ScreenshotPath
{
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "CircleSpaceCoordinator",
        "Screenshots");

    public static string Create(string directory, DateTime now) => Path.Combine(
        directory,
        $"screenshot-{now:yyyyMMdd-HHmmss-fff}.png");
}
