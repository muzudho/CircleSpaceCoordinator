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
    private SpaceDefinitionCatalog? portableUndo;
    private string? portableAfter;
    public bool CanUndoPortableImport => portableUndo is not null && portableAfter == savedJson;

    public void ImportPortable(SpaceDefinitionCatalog catalog)
    {
        var before = Current;
        Save(catalog, requireRepresentativeCell: false);
        portableUndo = before;
        portableAfter = savedJson;
    }

    public void UndoPortableImport()
    {
        if (!CanUndoPortableImport) throw new InvalidOperationException("取り消せる共通カタログ登録がありません。");
        Save(portableUndo!, requireRepresentativeCell: false);
        portableUndo = null;
        portableAfter = null;
    }

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

    public void Save(SpaceDefinitionCatalog catalog, bool requireRepresentativeCell = true)
    {
        catalog.Validate(requireRepresentativeCell);
        catalog = catalog.NormalizeCellStates() with { SchemaVersion = catalog.IsConfidential ? 2 : catalog.SchemaVersion };
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
