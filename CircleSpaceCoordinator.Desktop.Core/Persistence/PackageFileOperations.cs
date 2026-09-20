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

    public static void Delete(string path, string expectedJson, string packageName, string enteredName)
    {
        if (!NameMatches(packageName, enteredName)) throw new InvalidOperationException("パッケージ名が一致しません。");
        if (!string.Equals(File.ReadAllText(path), expectedJson, StringComparison.Ordinal))
            throw new IOException("確認中にファイルが変更されました。選び直してから削除してください。");
        File.Delete(path);
    }
}
