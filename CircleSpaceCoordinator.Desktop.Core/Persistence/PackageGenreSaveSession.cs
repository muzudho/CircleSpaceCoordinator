namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Engine.Model;

/// <summary>One package edit scope, including immutable opening bytes and stale-file protection.</summary>
public sealed class PackageGenreSaveSession(string path, string openingJson, PortablePackage openingPackage,
    PortableMaterial openingTable)
{
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public string OpeningJson { get; } = openingJson;
    public PortableMaterial OpeningTable { get; } = openingTable;
    public string CurrentJson { get; private set; } = openingJson;
    private PortablePackage currentPackage = openingPackage;
    public PortableMaterial CurrentTable => currentPackage.Materials.Single(item => item.Id == OpeningTable.Id);

    public void Save(PortableMaterial table, string handle, DateOnly date, string log, bool restore,
        Func<PortableGenreTableUpdate, string> update, Func<string, PortablePackage> parse)
    {
        var next = restore ? OpeningJson : update(new(CurrentJson, table, handle, date, log));
        var parsed = parse(next);
        PortableLibraryService.SaveMetadata(new(Path, CurrentJson, currentPackage, null), next);
        CurrentJson = next;
        currentPackage = parsed;
    }
}
