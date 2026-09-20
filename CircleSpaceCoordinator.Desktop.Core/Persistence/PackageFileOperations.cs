namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

using CircleSpaceCoordinator.Core.Model;

public static class PackageFileOperations
{
    public static string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || name != name.Trim() ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.'))
            return "パッケージ名はファイル名に使える1～100文字で入力してください。";
        return null;
    }

    public static string CreateEmpty(string directory, string name, Func<string, string> createDocument)
    {
        if (ValidateName(name) is { } error) throw new ArgumentException(error);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("保存先のフォルダーを選んでください。");
        var json = createDocument(name);
        for (var suffix = 1; ; suffix++)
        {
            var path = Path.Combine(directory, name + (suffix == 1 ? "" : "_" + suffix) + ".package-csc.json");
            if (File.Exists(path)) continue;
            try { FrameLayoutPortableService.SaveDocument(path, json, overwrite: false); return path; }
            catch (IOException) when (File.Exists(path)) { /* Another writer won this filename; never overwrite it. */ }
        }
    }

    public static bool NameMatches(string expected, string entered) =>
        !string.IsNullOrWhiteSpace(expected) && string.Equals(expected, entered, StringComparison.Ordinal);

    public static string DeleteTable(string path, string expectedJson, string tableId, string enteredName,
        Func<string, CircleSpaceCoordinator.Engine.Model.PortablePackage> parse)
    {
        var package = parse(expectedJson);
        var table = package.Materials.Single(item => item.Id == tableId);
        if (table.Kind is not ("genre-styles" or "block-styles")) throw new InvalidOperationException("網掛け表だけを削除できます。");
        if (!NameMatches(table.Name, enteredName)) throw new InvalidOperationException("網掛け表名が一致しません。");
        var root = System.Text.Json.Nodes.JsonNode.Parse(expectedJson)!.AsObject();
        var materials = root["materials"]!.AsArray();
        var item = materials.Single(node => node!["id"]!.GetValue<string>() == tableId);
        materials.Remove(item);
        var updated = root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        parse(updated);
        PortableLibraryService.SaveMetadata(new(path, expectedJson, package, null), updated);
        return updated;
    }

    public static string Rename(string path, string expectedJson, string newName)
    {
        const string extension = ".package-csc.json";
        if (newName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) newName = newName[..^extension.Length];
        if (ValidateName(newName) is { } error) throw new ArgumentException(error);
        var source = Path.GetFullPath(path);
        var destination = Path.Combine(Path.GetDirectoryName(source)!, newName + extension);
        if (!string.Equals(File.ReadAllText(source), expectedJson, StringComparison.Ordinal))
            throw new IOException("確認中にファイルが変更されました。選び直してからリネームしてください。");
        if (string.Equals(source, destination, StringComparison.Ordinal)) return source;
        File.Move(source, destination, overwrite: false);
        return destination;
    }

    public static void Delete(string path, string expectedJson, string packageName, string enteredName)
    {
        if (!NameMatches(packageName, enteredName)) throw new InvalidOperationException("パッケージ名が一致しません。");
        if (!string.Equals(File.ReadAllText(path), expectedJson, StringComparison.Ordinal))
            throw new IOException("確認中にファイルが変更されました。選び直してから削除してください。");
        File.Delete(path);
    }
}
