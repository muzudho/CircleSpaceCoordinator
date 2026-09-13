# StationeryUI local packages

Initial local NuGet feed for https://github.com/muzudho/StationeryUI.
CircleSpaceCoordinator consumes version 0.1.1 of all three packages. Version 0.1.1 adds `StationeryUI.Controls.RingMenuLayout`, extracted from this application, to the core package. Packages are checked in so a clean checkout can restore without a sibling source directory. The original 0.1.0 packages are retained unchanged. Other applications can adopt 0.1.1 to share the ring geometry.
After NuGet.org publication this feed can be removed once clean restoration from the public source is verified.
