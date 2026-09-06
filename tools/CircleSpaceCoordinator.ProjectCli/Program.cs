namespace CircleSpaceCoordinator.ProjectCli;

using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Infrastructure.Json;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length is < 2 or > 3 || args[0] != "evaluate")
        {
            Console.Error.WriteLine("Usage: CircleSpaceCoordinator.ProjectCli evaluate <input.json> [output.json]");
            return 2;
        }

        try
        {
            var inputPath = Path.GetFullPath(args[1]);
            var project = ProjectJsonSerializer.Load(File.ReadAllText(inputPath));
            var results = ProjectEvaluator.Evaluate(project);

            if (args.Length == 2)
            {
                Console.WriteLine($"Project valid: participants={project.Participants.Count}; plans={project.Plans.Count}; results={results.Count}");
                return 0;
            }

            var outputPath = Path.GetFullPath(args[2]);
            if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Input and output JSON paths must be different.");
            if (File.Exists(outputPath))
                throw new IOException($"Output already exists: {outputPath}");

            var outputDirectory = Path.GetDirectoryName(outputPath)
                ?? throw new InvalidOperationException("The output path has no parent directory.");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(outputPath, ProjectJsonSerializer.Save(project, results));
            Console.WriteLine($"Wrote evaluated project: plans={results.Count}; output={outputPath}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }
}
