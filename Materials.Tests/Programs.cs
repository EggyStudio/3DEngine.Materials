using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace Materials.Tests;

/// <summary>
/// Skipped where no Python 3 runs or the one that does has no Pillow, which the texture script
/// needs, so a run's summary says the script's tests did not run.
/// </summary>
public sealed class NeedsPillowFactAttribute : FactAttribute
{
    public NeedsPillowFactAttribute()
    {
        if (Programs.Python.Value is null) Skip = "No Python 3 runs here as python3 or python.";
        else if (!Programs.Pillow.Value) Skip = $"{Programs.Python.Value} has no Pillow, which scripts/textures.py needs.";
    }
}

/// <summary>Runs the module's scripts, the form of the Tools module's.</summary>
public static class Programs
{
    /// <summary>
    /// The command that runs Python 3, <c>python3</c> as Linux and macOS name it, or <c>python</c>
    /// as Windows does, or null where neither runs.
    /// </summary>
    public static readonly Lazy<string?> Python = new(() => new[] { "python3", "python" }.FirstOrDefault(Runs));

    /// <summary>
    /// Whether the Python that runs imports Pillow, in isolated mode as the scripts run.
    /// </summary>
    public static readonly Lazy<bool> Pillow = new(() => Python.Value is { } python && Start(python, ["-I", "-c", "import PIL"]).Code == 0);

    /// <summary>
    /// Runs a script of the repository's <c>scripts</c> folder with Python's isolated mode and
    /// returns what it printed, failing the test where it exits with an error.
    /// </summary>
    public static string Script(string name, params string[] arguments)
    {
        var (code, output, error) = Start(Python.Value!, ["-I", Path.Combine(Repository.Root, "scripts", name), .. arguments]);
        Assert.True(code == 0, $"{name} {string.Join(' ', arguments)} exited with {code}. {error}");
        return output;
    }

    private static (int Code, string Output, string Error) Start(string program, string[] arguments)
    {
        // Read as UTF-8, which the scripts write, where Windows would read a pipe in its console's
        // code page.
        var start = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error.Result);
    }

    // Windows names a Store installer python3 where no Python is installed, which prints where to
    // get one and exits with an error, so the version it prints is read rather than its being
    // found.
    private static bool Runs(string command)
    {
        try
        {
            var (code, output, error) = Start(command, ["--version"]);
            return code == 0 && (output + error).StartsWith("Python 3", StringComparison.Ordinal);
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
