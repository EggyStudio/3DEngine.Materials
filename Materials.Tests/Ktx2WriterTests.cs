using Xunit;

namespace Materials.Tests;

public class Ktx2WriterTests
{
    private static readonly string Reference = Path.Combine(Repository.References, "encoder");

    // The drawn tile, 16 by 16 texels whose color runs along x alone, as RGBA with an opaque alpha.
    private static byte[] Tile()
    {
        var ppm = File.ReadAllBytes(Path.Combine(Reference, "source", "gradient.ppm"));
        var header = "P6\n16 16\n255\n".Length;
        var rgba = new byte[16 * 16 * 4];
        for (int at = 0; at < 16 * 16; at++)
        {
            ppm.AsSpan(header + at * 3, 3).CopyTo(rgba.AsSpan(at * 4));
            rgba[at * 4 + 3] = 255;
        }

        return rgba;
    }

    [Fact]
    public void The_Writer_Writes_The_Scripts_Bytes_For_A_Tile_Mode_6_Holds()
    {
        var written = Ktx2Writer.Encode(Tile(), 16, 16, ColorSpace.Srgb);
        Assert.Equal(File.ReadAllBytes(Path.Combine(Reference, "gradient-albedo.ktx2")), written);
        var read = Ktx2.Read(written);
        Assert.Equal((PictureFormat.Bc7, ColorSpace.Srgb, 5), (read.Format, read.ColorSpace, read.Levels.Count));
    }

    [Fact]
    public void The_Writer_Writes_The_Scripts_Bytes_For_Blocks_Of_Modes_1_And_7()
    {
        // Three opaque colors a block, an opaque gradient and a leaf's cut edge with its vein,
        // which the script writes in modes 1, 6 and 7.
        var rgba = File.ReadAllBytes(Path.Combine(Reference, "source", "modes.rgba"));
        Assert.Equal(File.ReadAllBytes(Path.Combine(Reference, "modes-albedo.ktx2")), Ktx2Writer.Encode(rgba, 16, 16, ColorSpace.Srgb));
    }

    [NeedsPillowFact]
    public void The_Scripts_Encoding_Of_A_Raw_Picture_Writes_The_Reference()
    {
        var folder = Directory.CreateTempSubdirectory("materials-encode-");
        try
        {
            var written = Path.Combine(folder.FullName, "modes.ktx2");
            Programs.Script("textures.py", "--encode", Path.Combine(Reference, "source", "modes.rgba"), "16", "16", written);
            Assert.Equal(File.ReadAllBytes(Path.Combine(Reference, "modes-albedo.ktx2")), File.ReadAllBytes(written));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(PictureFormat.Bc5, "modes-bc5.ktx2")]
    [InlineData(PictureFormat.Bc4, "modes-bc4.ktx2")]
    public void The_Writer_Writes_The_Scripts_Bytes_In_Bc5_And_Bc4(PictureFormat format, string reference)
    {
        // BC5 of the picture's red and green and BC4 of its red, as a normal and a depth hold them.
        var rgba = File.ReadAllBytes(Path.Combine(Reference, "source", "modes.rgba"));
        Assert.Equal(File.ReadAllBytes(Path.Combine(Reference, reference)), Ktx2Writer.Encode(rgba, 16, 16, format, ColorSpace.Linear));
    }

    [NeedsPillowFact]
    public void The_Scripts_Bc5_And_Bc4_Write_Their_References()
    {
        var folder = Directory.CreateTempSubdirectory("materials-bc45-");
        try
        {
            foreach (var format in new[] { "bc5", "bc4" })
            {
                var written = Path.Combine(folder.FullName, format + ".ktx2");
                Programs.Script("textures.py", "--encode", Path.Combine(Reference, "source", "modes.rgba"), "16", "16", written, "--format", format);
                Assert.Equal(File.ReadAllBytes(Path.Combine(Reference, $"modes-{format}.ktx2")), File.ReadAllBytes(written));
            }
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [NeedsPillowFact]
    public void The_Script_Writes_The_Reference_From_Its_Source()
    {
        var folder = Directory.CreateTempSubdirectory("materials-writer-");
        try
        {
            var manifest = Path.Combine(folder.FullName, "textures.json");
            File.WriteAllText(manifest, "{\"entries\": [{\"name\": \"gradient\", \"source\": \"source\", \"size\": [16, 16], \"albedo\": {\"file\": \"gradient.ppm\"}}]}");
            Programs.Script("textures.py", manifest, "--sources", Reference, "--out", folder.FullName);
            Assert.Equal(File.ReadAllBytes(Path.Combine(Reference, "gradient-albedo.ktx2")), File.ReadAllBytes(Path.Combine(folder.FullName, "gradient", "albedo.ktx2")));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(12, 16)]
    [InlineData(16, 6)]
    public void A_Side_That_Is_No_Power_Of_Two_Is_Refused(int width, int height)
    {
        var thrown = Assert.Throws<ArgumentException>(() => Ktx2Writer.Encode(new byte[width * height * 4], width, height, ColorSpace.Linear));
        Assert.Contains("power of two", thrown.Message);
    }
}
