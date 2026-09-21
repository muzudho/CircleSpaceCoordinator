# StationeryUI local packages

Initial local NuGet feed for https://github.com/muzudho/StationeryUI.
CircleSpaceCoordinator consumes core `StationeryUI` version `0.1.2-csc.5130fff` and MonoGame / Windows version `0.1.1`. The core snapshot adds style parsing and layout calculation for the event list. It is built from unmodified StationeryUI core sources at commit `5130fff6a5cf18aa2559f33388bcc104b3aa1d05`. This is a local snapshot version, not an upstream release. Existing packages are retained unchanged. Packages are checked in so a clean checkout can restore without a sibling source directory.

To reproduce, check out that commit of https://github.com/muzudho/StationeryUI and run:

```powershell
dotnet pack src/StationeryUI/StationeryUI.csproj -c Release -p:Version=0.1.2-csc.5130fff -p:RepositoryCommit=5130fff6a5cf18aa2559f33388bcc104b3aa1d05 -o <CircleSpaceCoordinator>/LocalPackages/StationeryUI
```

See [event list style settings](../../Docs/event-list-style-settings.md) for the file format integration and reload policy.
After NuGet.org publication this feed can be removed once clean restoration from the public source is verified.
