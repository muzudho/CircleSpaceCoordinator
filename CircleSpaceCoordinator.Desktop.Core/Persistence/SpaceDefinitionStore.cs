namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

using System.Text.Json;
using CircleSpaceCoordinator.Core.Model;

/// <summary>One catalog per application installation, independent of event and plan selection.</summary>
public sealed class SpaceDefinitionStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public string Path { get; }
    public SpaceDefinitionCatalog Current { get; private set; }
    private string? savedJson;

    public SpaceDefinitionStore(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        if (File.Exists(Path))
        {
            savedJson = File.ReadAllText(Path);
            Current = JsonSerializer.Deserialize<SpaceDefinitionCatalog>(savedJson, Options) ?? throw new InvalidDataException("フレーム定義を読み込めません。");
        }
        else Current = SpaceDefinitionCatalog.CreateDefault();
        // Earlier versions allowed no representative cell. Keep those definitions editable.
        Current.Validate(requireRepresentativeCell: false);
        Current = Current.NormalizeCellStates();
    }

    public void Save(SpaceDefinitionCatalog catalog)
    {
        catalog.Validate();
        catalog = catalog.NormalizeCellStates();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        // A second running application must reload instead of silently overwriting edits.
        using var guard = new FileStream(Path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if ((File.Exists(Path) ? File.ReadAllText(Path) : null) != savedJson)
            throw new IOException("別のアプリでフレーム定義が変更されました。アプリを開き直してください。");
        var json = JsonSerializer.Serialize(catalog, Options);
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, Path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        savedJson = json;
        Current = catalog;
    }
}
