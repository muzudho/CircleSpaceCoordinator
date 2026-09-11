namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record EventProjectReference(string Path, string DisplayName);

public sealed record ProjectWorkingState(
    string ProjectPath,
    string SelectedPlanId,
    string EditorMode,
    double Zoom,
    double OriginX,
    double OriginY,
    IReadOnlyDictionary<string, bool>? Switches = null,
    string? SelectedDeskLayoutId = null);

public sealed record CircleLabelDisplaySettings(
    string DisplayField = "internalId",
    string? CircleIdPattern = null,
    string? CircleIdReplacement = null,
    string? ChannelId = null);

public sealed record ApplicationSettings(
    string ProjectsDirectory,
    string? LastProjectPath,
    IReadOnlyList<EventProjectReference>? EventProjects = null,
    IReadOnlyList<ProjectWorkingState>? ProjectWorkingStates = null,
    string? ParticipantImportDirectory = null,
    string? CircleSeatExportDirectory = null,
    CircleLabelDisplaySettings? CircleLabelDisplay = null)
{
    [JsonPropertyOrder(-100)]
    public string SchemaVersion { get; init; } = "1.1";
}

public sealed class ApplicationSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
    private readonly string settingsPath;

    public ApplicationSettingsService(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        this.settingsPath = Path.GetFullPath(settingsPath);
        Current = LoadOrDefault();
        TrySave();
    }

    public ApplicationSettings Current { get; private set; }

    public void RememberProject(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var fullPath = Path.GetFullPath(projectPath);
        RegisterProject(fullPath, TryReadProjectName(fullPath) ?? Path.GetFileNameWithoutExtension(fullPath));
        Current = Current with { LastProjectPath = fullPath };
        TrySave();
    }

    public void RememberParticipantImportPath(string filePath) =>
        RememberFileDirectory(filePath, isParticipantImport: true);

    public void RememberCircleSeatExportPath(string filePath) =>
        RememberFileDirectory(filePath, isParticipantImport: false);

    public void SaveCircleLabelDisplay(CircleLabelDisplaySettings display)
    {
        ArgumentNullException.ThrowIfNull(display);
        Current = Current with { CircleLabelDisplay = NormalizeCircleLabelDisplay(display) };
        TrySave();
    }

    public void RegisterProject(string projectPath, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var fullPath = Path.GetFullPath(projectPath);
        var projects = Current.EventProjects?.ToList() ?? [];
        var existingIndex = projects.FindIndex(item => PathsEqual(item.Path, fullPath));
        if (existingIndex < 0)
            projects.Add(new EventProjectReference(fullPath, displayName.Trim()));
        else
            projects[existingIndex] = new EventProjectReference(fullPath, displayName.Trim());
        Current = Current with
        {
            ProjectsDirectory = Path.GetDirectoryName(fullPath) ?? Current.ProjectsDirectory,
            EventProjects = projects,
        };
        TrySave();
    }

    public void RemoveProject(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var fullPath = Path.GetFullPath(projectPath);
        var projects = (Current.EventProjects ?? []).Where(item => !PathsEqual(item.Path, fullPath)).ToArray();
        Current = Current with
        {
            LastProjectPath = Current.LastProjectPath is not null && PathsEqual(Current.LastProjectPath, fullPath)
                ? null
                : Current.LastProjectPath,
            EventProjects = projects,
            ProjectWorkingStates = (Current.ProjectWorkingStates ?? [])
                .Where(item => !PathsEqual(item.ProjectPath, fullPath)).ToArray(),
        };
        TrySave();
    }

    public ProjectWorkingState? GetWorkingState(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var fullPath = Path.GetFullPath(projectPath);
        return (Current.ProjectWorkingStates ?? []).LastOrDefault(item => PathsEqual(item.ProjectPath, fullPath));
    }

    public void SaveWorkingState(ProjectWorkingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var fullPath = Path.GetFullPath(state.ProjectPath);
        var states = (Current.ProjectWorkingStates ?? [])
            .Where(item => !PathsEqual(item.ProjectPath, fullPath)).ToList();
        states.Add(state with
        {
            ProjectPath = fullPath,
            Switches = (state.Switches ?? new Dictionary<string, bool>())
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
        });
        Current = Current with { ProjectWorkingStates = states };
        TrySave();
    }

    public bool MoveProject(string projectPath, int offset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        if (offset is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be -1 or 1.");
        var fullPath = Path.GetFullPath(projectPath);
        var projects = Current.EventProjects?.ToList() ?? [];
        var currentIndex = projects.FindIndex(item => PathsEqual(item.Path, fullPath));
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= projects.Count)
            return false;
        (projects[currentIndex], projects[targetIndex]) = (projects[targetIndex], projects[currentIndex]);
        Current = Current with { EventProjects = projects };
        TrySave();
        return true;
    }

    private ApplicationSettings LoadOrDefault()
    {
        var fallback = new ApplicationSettings(ProjectFileService.GetDefaultProjectsDirectory(), null, [], []);
        try
        {
            if (!File.Exists(settingsPath))
                return fallback;
            var loaded = JsonSerializer.Deserialize<ApplicationSettings>(File.ReadAllText(settingsPath), JsonOptions);
            if (loaded is null)
                return fallback;
            var directory = string.IsNullOrWhiteSpace(loaded.ProjectsDirectory)
                ? fallback.ProjectsDirectory
                : Path.GetFullPath(loaded.ProjectsDirectory);
            var lastPath = string.IsNullOrWhiteSpace(loaded.LastProjectPath)
                ? null
                : Path.GetFullPath(loaded.LastProjectPath);
            var projects = new List<EventProjectReference>();
            foreach (var item in loaded.EventProjects ?? [])
            {
                if (string.IsNullOrWhiteSpace(item.Path))
                    continue;
                var fullPath = Path.GetFullPath(item.Path);
                if (projects.Any(existing => PathsEqual(existing.Path, fullPath)))
                    continue;
                var displayName = string.IsNullOrWhiteSpace(item.DisplayName)
                    ? Path.GetFileNameWithoutExtension(fullPath)
                    : item.DisplayName.Trim();
                projects.Add(new EventProjectReference(fullPath, displayName));
            }
            if (lastPath is not null && projects.All(item => !PathsEqual(item.Path, lastPath)))
                projects.Add(new EventProjectReference(lastPath, TryReadProjectName(lastPath) ?? Path.GetFileNameWithoutExtension(lastPath)));
            if (projects.Count == 0 && Directory.Exists(directory))
            {
                foreach (var projectPath in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                             .Order(StringComparer.OrdinalIgnoreCase))
                {
                    var projectName = TryReadProjectName(projectPath);
                    if (projectName is not null)
                        projects.Add(new EventProjectReference(Path.GetFullPath(projectPath), projectName));
                }
            }
            var states = (loaded.ProjectWorkingStates ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item.ProjectPath) &&
                    !string.IsNullOrWhiteSpace(item.SelectedPlanId) &&
                    !string.IsNullOrWhiteSpace(item.EditorMode) &&
                    double.IsFinite(item.Zoom) && item.Zoom is >= 0.25d and <= 4d &&
                    double.IsFinite(item.OriginX) && double.IsFinite(item.OriginY))
                .GroupBy(item => Path.GetFullPath(item.ProjectPath), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last() with { ProjectPath = group.Key }).ToArray();
            return new ApplicationSettings(
                directory,
                lastPath,
                projects,
                states,
                NormalizeExistingDirectory(loaded.ParticipantImportDirectory),
                NormalizeExistingDirectory(loaded.CircleSeatExportDirectory),
                NormalizeCircleLabelDisplay(loaded.CircleLabelDisplay))
            {
                SchemaVersion = "1.1",
            };
        }
        catch (Exception) when (File.Exists(settingsPath))
        {
            return fallback;
        }
    }

    private void TrySave()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            var temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        catch (UnauthorizedAccessException)
        {
            // A read-only deployment directory must not prevent the editor from starting.
        }
        catch (IOException)
        {
            // Keep the in-memory settings when the deployment directory is temporarily unavailable.
        }
    }

    private void RememberFileDirectory(string filePath, bool isParticipantImport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (string.IsNullOrWhiteSpace(directory))
            return;
        Current = isParticipantImport
            ? Current with { ParticipantImportDirectory = directory }
            : Current with { CircleSeatExportDirectory = directory };
        TrySave();
    }

    private static string? NormalizeExistingDirectory(string? directory) =>
        string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
            ? null
            : Path.GetFullPath(directory);

    private static CircleLabelDisplaySettings NormalizeCircleLabelDisplay(CircleLabelDisplaySettings? display)
    {
        var field = display?.DisplayField?.Trim() switch
        {
            "circleId" => "circleId",
            "displayName" => "displayName",
            "channel" when !string.IsNullOrWhiteSpace(display.ChannelId) => "channel",
            _ => "internalId",
        };
        var pattern = string.IsNullOrWhiteSpace(display?.CircleIdPattern) ? null : display.CircleIdPattern.Trim();
        var replacement = string.IsNullOrWhiteSpace(display?.CircleIdReplacement) ? null : display.CircleIdReplacement;
        return new CircleLabelDisplaySettings(field, pattern, replacement,
            string.IsNullOrWhiteSpace(display?.ChannelId) ? null : display.ChannelId.Trim());
    }

    private static string? TryReadProjectName(string path)
    {
        try
        {
            return File.Exists(path) ? ProjectFileService.Load(path).Name : null;
        }
        catch (Exception) when (File.Exists(path))
        {
            return null;
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
