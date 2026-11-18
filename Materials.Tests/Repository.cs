namespace Materials.Tests;

/// <summary>The repository the tests run in, found above the tests' own folder.</summary>
internal static class Repository
{
    /// <summary>
    /// The repository's root, the folder above the tests that holds the solution.
    /// </summary>
    public static string Root { get; } = Find();

    /// <summary>The folder of the files the tests read their references from.</summary>
    public static string References => Path.Combine(Root, "Materials.Tests", "references");

    private static string Find()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "Materials.slnx")))
            at = at.Parent;
        return at?.FullName ?? throw new DirectoryNotFoundException("No Materials.slnx above the tests.");
    }
}
