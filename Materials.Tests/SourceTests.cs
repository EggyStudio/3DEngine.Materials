using Xunit;

namespace Materials.Tests;

/// <summary>
/// The library's and the tests' sources, held to the width the repository's prose is wrapped at.
/// </summary>
public sealed class SourceTests
{
    private const int Width = 100;

    private static string Root
    {
        get
        {
            // The repository's root is the folder above the tests that holds the solution.
            var at = new DirectoryInfo(AppContext.BaseDirectory);
            while (at is not null && !File.Exists(Path.Combine(at.FullName, "Materials.slnx")))
                at = at.Parent;
            return at?.FullName
                ?? throw new DirectoryNotFoundException("No Materials.slnx above the tests.");
        }
    }

    [Fact]
    public void Every_Comment_Line_Holds_To_A_Hundred_Columns()
    {
        var root = Root;
        var over = new List<string>();
        foreach (var file in Files(root))
        {
            var lines = File.ReadAllLines(file);
            var xml = Path.GetExtension(file) is ".csproj" or ".props" or ".slnx";
            var hash = Path.GetExtension(file) is ".yml" or ".sh" or ".py";
            var inside = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();

                // A line of an XML comment is one inside its marks, the line that opens it among
                // them.
                var comment = xml ? inside || line.StartsWith("<!--") : hash ? line.StartsWith('#') : line.StartsWith("//");
                if (xml) inside = (inside || line.Contains("<!--")) && !line.Contains("-->");
                if (!comment || lines[i].Length <= Width) continue;

                // A line holding an address alone is left whole, since a broken address opens
                // nothing.
                var text = line.TrimStart('/', '#').Trim();
                if (text.Contains("://") && !text.Contains(' ')) continue;
                over.Add($"{Path.GetRelativePath(root, file)}:{i + 1} ({lines[i].Length})");
            }
        }

        Assert.True(over.Count == 0, $"{over.Count} comment lines past {Width} columns:\n" + string.Join("\n", over));
    }

    // The sources, the project files, the workflows and the scripts, every folder deep, the
    // build's output apart, as the Levels module reads them, with the Python scripts beside.
    private static IEnumerable<string> Files(string root)
    {
        string[] patterns = ["*.cs", "*.csproj", "*.props", "*.slnx", "*.yml", "*.sh", "*.py"];
        foreach (var folder in new[] { "Materials", "Materials.Tests", "build", ".github", "scripts" })
        foreach (var pattern in patterns)
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), pattern, SearchOption.AllDirectories))
        {
            var parts = Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar);
            if (!parts.Contains("bin") && !parts.Contains("obj")) yield return file;
        }

        foreach (var pattern in patterns)
        foreach (var file in Directory.EnumerateFiles(root, pattern)) yield return file;
    }
}
