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
