using Xunit;

namespace Materials.Tests;

/// <summary>
/// The sample set <c>scripts/textures.py --sample</c> writes, a ringed bark of 64 by 64 with a hole
/// cut in it, read through <see cref="Ktx2"/>, and the script held to the bytes the repository
/// keeps.
/// </summary>
public class TexturesScriptTests
{
    private static readonly string Sample = Path.Combine(Repository.References, "textures", "sample");

    [Theory]
    [InlineData("albedo", PictureFormat.Bc7, ColorSpace.Srgb, 64, 7)]
    [InlineData("normal", PictureFormat.Bc5, ColorSpace.Linear, 32, 6)]
    [InlineData("surface", PictureFormat.Bc7, ColorSpace.Linear, 32, 6)]
    [InlineData("depth", PictureFormat.Bc4, ColorSpace.Linear, 32, 6)]
    public void The_Sample_Sets_Files_Are_Plain_Ktx2_In_Their_Formats(string name, PictureFormat format, ColorSpace colorSpace, int size, int levels)
    {
        // Read at all only where no level is supercompressed, which Ktx2 refuses.
        var picture = Ktx2.Read(File.ReadAllBytes(Path.Combine(Sample, name + ".ktx2")));
        Assert.Equal((format, colorSpace, size, size, levels), (picture.Format, picture.ColorSpace, picture.Width, picture.Height, picture.Levels.Count));
        Assert.Equal((1, 1), picture.LevelSize(levels - 1));
    }

    [Fact]
    public void An_Opaque_Block_Of_The_Sample_Decodes_To_An_Alpha_Of_255()
    {
        // A block wholly outside the hole, a circle of radius 10 at (40, 24), is opaque throughout,
        // and keeps its shared bits set so no texel of it decodes to 254.
        var texels = Ktx2.Read(File.ReadAllBytes(Path.Combine(Sample, "albedo.ktx2"))).Decode();
        var short_ = new List<string>();
        for (int by = 0; by < 16; by++)
        for (int bx = 0; bx < 16; bx++)
        {
            bool opaque = true;
            for (int i = 0; i < 16; i++)
            {
                int x = bx * 4 + i % 4, y = by * 4 + i / 4;
                if ((x - 40) * (x - 40) + (y - 24) * (y - 24) < 100) opaque = false;
            }

            if (!opaque) continue;
            for (int i = 0; i < 16; i++)
            {
                int x = bx * 4 + i % 4, y = by * 4 + i / 4;
                if (texels[(y * 64 + x) * 4 + 3] != 255) short_.Add($"({x}, {y}) {texels[(y * 64 + x) * 4 + 3]}");
            }
        }

        Assert.True(short_.Count == 0, $"{short_.Count} opaque texels decode below 255, as {string.Join(", ", short_.Take(5))}");
    }

    [Fact]
    public void The_Samples_Albedo_Decodes_Near_The_Rings_It_Was_Drawn_From()
    {
        // The rings as scripts/textures.py draws them, red and blue moving against each other with
        // green held, which a power iteration started at (1, 1, 1, 1) turned to flat blocks, a mean
        // error of 13.71 of 255 over the opaque texels, where the start from the widest channel's
        // column gives 1.64.
        var texels = Ktx2.Read(File.ReadAllBytes(Path.Combine(Sample, "albedo.ktx2"))).Decode();
        double total = 0;
        int count = 0;
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            if ((x - 40) * (x - 40) + (y - 24) * (y - 24) < 100) continue;
            int ring = (int)(128 + 100 * Math.Sin(x * 0.4) * Math.Cos(y * 0.2));
            int[] drawn = [ring, 90, 255 - ring];
            for (int c = 0; c < 3; c++) total += Math.Abs(texels[(y * 64 + x) * 4 + c] - drawn[c]);
            count += 3;
        }

        Assert.InRange(total / count, 0, 2.0);
    }

    [NeedsPillowFact]
    public void The_Script_Writes_The_Sample_The_Repository_Holds()
    {
        var folder = Directory.CreateTempSubdirectory("materials-sample-");
        try
        {
            Programs.Script("textures.py", "--sample", folder.FullName);
            foreach (var file in new[] { "sample/albedo.ktx2", "sample/normal.ktx2", "sample/surface.ktx2", "sample/depth.ktx2", "textures.json" })
                Assert.True(
                    File.ReadAllBytes(Path.Combine(folder.FullName, file)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(Repository.References, "textures", file))),
                    $"The script writes {file} otherwise than the repository holds it.");
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
