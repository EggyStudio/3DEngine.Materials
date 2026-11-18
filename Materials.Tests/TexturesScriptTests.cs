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
    public void A_Built_Entrys_Mean_Color_Is_The_Linear_Mean_Written_In_Srgb()
    {
        // A tile of 8 by 8 texels, its left half white and its right half black, so its mean is a
        // linear 0.5, which sRGB writes as 0.7354, where the mean of the encoded numbers is 0.5.
        var folder = Directory.CreateTempSubdirectory("materials-mean-");
        try
        {
            Directory.CreateDirectory(Path.Combine(folder.FullName, "source"));
            Directory.CreateDirectory(Path.Combine(folder.FullName, "collection"));
            var ppm = new List<byte>(System.Text.Encoding.ASCII.GetBytes("P6\n8 8\n255\n"));
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                ppm.AddRange(x < 4 ? [255, 255, 255] : [0, 0, 0]);
            File.WriteAllBytes(Path.Combine(folder.FullName, "source", "halves.ppm"), [.. ppm]);
            var manifest = Path.Combine(folder.FullName, "collection", "textures.json");
            File.WriteAllText(manifest, """{"entries": [{"name": "halves", "source": "source", "size": [8, 8], "albedo": {"file": "halves.ppm"}}]}""");

            Programs.Script("textures.py", manifest, "--sources", folder.FullName);
            using var written = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
            var color = written.RootElement.GetProperty("entries")[0].GetProperty("color").EnumerateArray().Select(c => c.GetDouble()).ToArray();
            Assert.Equal([0.7354, 0.7354, 0.7354], color);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [NeedsPillowFact]
    public void A_Block_Whose_Colors_Leave_One_Line_Is_Encoded_Near_Its_Source()
    {
        // Three flat colors in each block and two gradients in each, which one line between two
        // endpoints holds at 16 and 24 dB, encoded at 40 dB or more where a block's colors fall
        // into subsets of their own.
        Assert.InRange(Encoded((x, y) => x % 4 < 2 && y % 4 < 2 ? [220, 40, 40] : y % 4 < 2 ? [40, 200, 60] : [40, 60, 220]), 40, 200);
        Assert.InRange(Encoded((x, y) => x % 4 < 2 ? [(byte)(60 + 40 * (y % 4)), 40, 40] : [40, (byte)(60 + 40 * (x % 4)), 200]), 40, 200);
    }

    // A tile of 16 by 16 texels drawn by a function, built through a manifest and read back from
    // its albedo's top level, its peak signal to noise in decibels over red, green and blue.
    private static double Encoded(Func<int, int, byte[]> texel)
    {
        var folder = Directory.CreateTempSubdirectory("materials-encode-");
        try
        {
            Directory.CreateDirectory(Path.Combine(folder.FullName, "source"));
            Directory.CreateDirectory(Path.Combine(folder.FullName, "collection"));
            var ppm = new List<byte>(System.Text.Encoding.ASCII.GetBytes("P6\n16 16\n255\n"));
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                ppm.AddRange(texel(x, y));
            File.WriteAllBytes(Path.Combine(folder.FullName, "source", "tile.ppm"), [.. ppm]);
            var manifest = Path.Combine(folder.FullName, "collection", "textures.json");
            File.WriteAllText(manifest, """{"entries": [{"name": "tile", "source": "source", "size": [16, 16], "albedo": {"file": "tile.ppm"}}]}""");
            Programs.Script("textures.py", manifest, "--sources", folder.FullName);

            var decoded = Ktx2.Read(File.ReadAllBytes(Path.Combine(folder.FullName, "collection", "tile", "albedo.ktx2"))).Decode();
            double squares = 0;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                var source = texel(x, y);
                for (int c = 0; c < 3; c++) squares += Math.Pow(decoded[(y * 16 + x) * 4 + c] - source[c], 2);
            }

            var mean = squares / (16 * 16 * 3);
            return mean == 0 ? 200 : 10 * Math.Log10(255.0 * 255.0 / mean);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
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
