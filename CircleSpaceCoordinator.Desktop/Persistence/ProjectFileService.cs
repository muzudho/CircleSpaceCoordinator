namespace CircleSpaceCoordinator.Desktop.Persistence;

using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Infrastructure.Json;

public static class ProjectFileService
{
    public static CircleSpaceProject Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ProjectJsonSerializer.Load(File.ReadAllText(Path.GetFullPath(path)));
    }

    public static string GetDefaultWorkingCopyPath()
        => Path.Combine(GetDefaultProjectsDirectory(), "fictional-working-copy.json");

    public static string GetDefaultProjectsDirectory()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
            documents = AppContext.BaseDirectory;
        return Path.Combine(documents, "CircleSpaceCoordinator", "Projects");
    }

    public static void Save(string path, CircleSpaceProject project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(project);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The save path has no directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var json = ProjectJsonSerializer.Save(project, ProjectEvaluator.Evaluate(project));
        var temporaryPath = fullPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
