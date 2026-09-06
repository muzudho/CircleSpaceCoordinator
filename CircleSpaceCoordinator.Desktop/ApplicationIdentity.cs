namespace CircleSpaceCoordinator.Desktop;

using System.Reflection;

internal static class ApplicationIdentity
{
    public static string Version { get; } = typeof(ApplicationIdentity).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

    public static string Title => $"Circle Space Coordinator v{Version}";
}
