# StationeryUI local packages

Initial local NuGet feed for https://github.com/muzudho/StationeryUI.
CircleSpaceCoordinator consumes `StationeryUI`, `StationeryUI.MonoGame` and `StationeryUI.Windows` version `0.1.2-csc.5130fff`. The core provides style parsing and layout calculation; MonoGame and Windows provide the existing F12 developer view and window launcher. These packages are built from unmodified sources at commit `5130fff6a5cf18aa2559f33388bcc104b3aa1d05`. This is a local snapshot version, not an upstream release. Existing packages are retained unchanged. Packages are checked in so a clean checkout can restore without a sibling source directory.

To reproduce, check out that commit of https://github.com/muzudho/StationeryUI and run:

```powershell
dotnet pack src/StationeryUI/StationeryUI.csproj -c Release -p:Version=0.1.2-csc.5130fff -p:RepositoryCommit=5130fff6a5cf18aa2559f33388bcc104b3aa1d05 -o <CircleSpaceCoordinator>/LocalPackages/StationeryUI
dotnet pack src/StationeryUI.MonoGame/StationeryUI.MonoGame.csproj -c Release -p:Version=0.1.2-csc.5130fff -p:RepositoryCommit=5130fff6a5cf18aa2559f33388bcc104b3aa1d05 -o <CircleSpaceCoordinator>/LocalPackages/StationeryUI
dotnet pack src/StationeryUI.Windows/StationeryUI.Windows.csproj -c Release -p:Version=0.1.2-csc.5130fff -p:RepositoryCommit=5130fff6a5cf18aa2559f33388bcc104b3aa1d05 -o <CircleSpaceCoordinator>/LocalPackages/StationeryUI
```

See [event list style settings](../../Docs/event-list-style-settings.md) for the file format integration and reload policy.
After NuGet.org publication this feed can be removed once clean restoration from the public source is verified.
