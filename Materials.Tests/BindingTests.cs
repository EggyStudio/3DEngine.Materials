using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Xunit;

namespace Materials.Tests;

/// <summary>
/// The README's smallest binding, run against a host that stands for an engine and records what
/// the binding gives it, so the build and the suite prove the guide.
/// </summary>
public class BindingTests
{
    /// <summary>
    /// An engine's calls as the README names them, its files by their paths and its GPU sampling
    /// the block formats or not, recording each texture made and the base color bound.
    /// </summary>
    private sealed class Host(Dictionary<string, byte[]> files, bool samplesBlocks)
    {
        public List<(PictureFormat Format, ColorSpace ColorSpace, int Width, int Height, IReadOnlyList<byte[]> Levels)> Textures { get; } = [];

        public (int Texture, int? OpacityChannel)? BaseColor { get; private set; }

        public byte[] ReadAllBytes(string path) => files[path];

        public bool Samples(PictureFormat format) => samplesBlocks || format == PictureFormat.Rgba8;

        public int CreateTexture(PictureFormat format, ColorSpace colorSpace, int width, int height, IReadOnlyList<byte[]> levels)
        {
            Textures.Add((format, colorSpace, width, height, levels));
            return Textures.Count - 1;
        }

        public void BindBaseColor(int texture, int? opacityChannel) => BaseColor = (texture, opacityChannel);
    }

    private static Host Bind(bool samplesBlocks)
    {
        var host = new Host(new() { ["textures/bark/albedo.ktx2"] = Albedo() }, samplesBlocks);
        // The README's block from here to the end marker, word for word.
        // Once, as the engine loads a material's pictures.
        var albedo = new MaterialPicture("textures/bark/albedo.ktx2", PictureLayout.Color);
        var picture = Ktx2.Read(host.ReadAllBytes(albedo.Path));

        // The levels go to the GPU as they are where it samples the format and decoded where it
        // does not, through a view of the layout's color space.
        var format = host.Samples(picture.Format) ? picture.Format : PictureFormat.Rgba8;
        var levels = format == picture.Format
            ? picture.Levels
            : Enumerable.Range(0, picture.Levels.Count).Select(picture.Decode).ToList();
        var texture = host.CreateTexture(format, albedo.ColorSpace, picture.Width, picture.Height, levels);
        host.BindBaseColor(texture, albedo.Layout.ChannelOf(PictureRole.Opacity));
        // The end marker.
        return host;
    }

    [Fact]
    public void The_Readmes_Binding_Gives_A_Gpu_That_Samples_Bc7_Its_Levels_As_They_Are()
    {
        var host = Bind(samplesBlocks: true);
        var texture = Assert.Single(host.Textures);
        Assert.Equal((PictureFormat.Bc7, ColorSpace.Srgb, 8, 8), (texture.Format, texture.ColorSpace, texture.Width, texture.Height));
        Assert.Equal([64, 16, 16, 16], texture.Levels.Select(level => level.Length));
        Assert.Equal((0, 3), host.BaseColor);
    }

    [Fact]
    public void The_Readmes_Binding_Gives_A_Gpu_That_Does_Not_Its_Levels_Decoded()
    {
        // Every block is opaque white, so every decoded byte is 255, four a texel at each level's
        // size.
        var host = Bind(samplesBlocks: false);
        var texture = Assert.Single(host.Textures);
        Assert.Equal((PictureFormat.Rgba8, ColorSpace.Srgb), (texture.Format, texture.ColorSpace));
        Assert.Equal([8 * 8 * 4, 4 * 4 * 4, 2 * 2 * 4, 4], texture.Levels.Select(level => level.Length));
        Assert.All(texture.Levels.SelectMany(level => level), value => Assert.Equal(255, value));
    }

    [Fact]
    public void The_Readmes_Block_Stands_Word_For_Word_In_This_Host()
    {
        var readme = File.ReadAllText(Path.Combine(Repository.Root, "README.md")).ReplaceLineEndings("\n");
        var start = readme.IndexOf("## Binding it in your engine", StringComparison.Ordinal);
        Assert.True(start >= 0, "the README has no section Binding it in your engine");
        var open = readme.IndexOf("```csharp\n", start, StringComparison.Ordinal) + "```csharp\n".Length;
        var block = readme[open..readme.IndexOf("```\n", open, StringComparison.Ordinal)];
        var source = File.ReadAllText(Path.Combine(Repository.Root, "Materials.Tests", "BindingTests.cs")).ReplaceLineEndings("\n");
        var lines = source.Split('\n');
        var from = Array.FindIndex(lines, l => l.Contains("// The README's block from here", StringComparison.Ordinal)) + 1;
        var to = Array.FindIndex(lines, l => l.Contains("// The end marker.", StringComparison.Ordinal));
        var hosted = string.Join("\n", lines[from..to].Select(l => l.Length >= 8 ? l[8..] : l.TrimStart())) + "\n";
        Assert.Equal(block, hosted);
    }

    [Fact]
    public void Every_Public_Type_Is_In_The_Binding_Section_Or_A_Row_Of_A_Readme_Table()
    {
        // A stranger finds every type the library offers from the README alone, by its name in
        // code, whole, as `Ktx2` or `Ktx2.Read`, and not as the start of a longer name.
        var readme = File.ReadAllText(Path.Combine(Repository.Root, "README.md")).ReplaceLineEndings("\n");
        var start = readme.IndexOf("## Binding it in your engine", StringComparison.Ordinal);
        var end = readme.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        var section = readme[start..(end < 0 ? readme.Length : end)];
        var rows = string.Join("\n", readme.Split('\n').Where(l => l.StartsWith('|')));
        var missing = typeof(Ktx2).Assembly.GetExportedTypes()
            .Select(t => t.Name)
            .Where(name => !Named(section, name) && !Named(rows, name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        Assert.True(missing.Count == 0, $"the README names {string.Join(", ", missing)} neither in Binding it in your engine nor in a table");
    }

    private static bool Named(string text, string name) => Regex.IsMatch(text, $@"`{name}([`.(<\[ ])");

    // An 8 by 8 albedo in BC7 and sRGB with its four levels, each block opaque white, mode 6 with
    // every endpoint's bits and both shared bits set.
    private static byte[] Albedo()
    {
        var white = new byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(white, (UInt128.One << 6) | (((UInt128.One << 58) - 1) << 7));
        byte[] Level(int blocks) => [.. Enumerable.Repeat(white, blocks).SelectMany(block => block)];
        return Ktx2Files.Write(146, 8, 8, [Level(4), Level(1), Level(1), Level(1)]);
    }
}
