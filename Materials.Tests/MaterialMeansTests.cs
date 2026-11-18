using System.Numerics;
using Xunit;

namespace Materials.Tests;

public class MaterialMeansTests
{
    // The engine's files as a test holds them, by path.
    private sealed class Files(Dictionary<string, byte[]> files) : IMaterialFiles
    {
        public byte[] ReadAllBytes(string path) => files.TryGetValue(path, out var file) ? file : throw new FileNotFoundException(path);
    }

    // An engine that decodes a picture of its own format, a PNG here standing for any the module
    // reads none of, to the texels it was given.
    private sealed class Decoding(Dictionary<string, byte[]> files, byte[] rgba, int width, int height) : IMaterialFiles
    {
        public byte[] ReadAllBytes(string path) => files.TryGetValue(path, out var file) ? file : throw new FileNotFoundException(path);

        public (byte[] Rgba, int Width, int Height)? Decode(string path, byte[] file) =>
            file.Length > 0 && file[0] == 0x89 ? (rgba, width, height) : null;
    }

    [Fact]
    public void A_Material_Of_No_Picture_Has_Its_Factors_For_Means()
    {
        var material = new MaterialDescription { BaseColor = new Vector4(0.2f, 0.4f, 0.6f, 1), Roughness = 0.7f, Metallic = 0.1f, DiffuseTransmission = 0.3f };
        Assert.Equal(new MaterialMeans(new Vector3(0.2f, 0.4f, 0.6f), 1, 1, 0.7f, 0.1f, 0.3f), MaterialMeans.Of(material, new Files([])));
    }

    [Fact]
    public void The_Color_Is_Averaged_In_Linear_Light()
    {
        // Half the texels at sRGB 188, a linear 0.5, and half black, so the mean is a linear 0.25,
        // where the mean of the encoded numbers would be 94, a linear 0.11.
        var albedo = Picture(2, 1, (x, y) => x == 0 ? [188, 188, 188, 255] : [0, 0, 0, 255]);
        var means = MaterialMeans.Of(new MaterialDescription { Pictures = [new MaterialPicture("a.ktx2", PictureLayout.Color)] }, new Files(new() { ["a.ktx2"] = albedo }));
        Assert.Equal(0.2509f, means.Color.X, 1e-3f);
    }

    [Theory]
    [InlineData(AlphaMode.Opaque, 0.5f, 1f)]
    [InlineData(AlphaMode.Mask, 1f, 0.5f)]
    [InlineData(AlphaMode.Blend, 0.79937f, 0.62549f)]
    public void The_Opacity_Weighs_The_Means_As_The_Alpha_Mode_Reads_It(AlphaMode mode, float red, float covered)
    {
        // A white texel opaque and a black one at an opacity of 64 of 255, so an opaque material
        // takes both alike, a mask keeps the white alone and a blend weighs the black by its 0.251,
        // a red of 1 over 1.251 and a cover of 1.251 over 2.
        var albedo = Picture(2, 1, (x, y) => x == 0 ? [255, 255, 255, 255] : [0, 0, 0, 64]);
        var material = new MaterialDescription { AlphaMode = mode, Pictures = [new MaterialPicture("a.ktx2", PictureLayout.Color)] };
        var means = MaterialMeans.Of(material, new Files(new() { ["a.ktx2"] = albedo }));
        Assert.Equal(red, means.Color.X, 1e-4f);
        Assert.Equal(covered, means.Opacity, 1e-4f);
    }

    [Fact]
    public void A_Surface_Pictures_Channels_Are_Scaled_By_Their_Factors()
    {
        // Occlusion 0.5, roughness 1, metallic 0.25 and translucency 0.5 throughout, the
        // occlusion's strength of a half moving it halfway toward one.
        var surface = Picture(4, 4, (x, y) => [128, 255, 64, 128]);
        var material = new MaterialDescription
        {
            Roughness = 0.5f, Metallic = 1f, DiffuseTransmission = 1f, OcclusionStrength = 0.5f,
            Pictures = [new MaterialPicture("s.ktx2", PictureLayout.Surface)],
        };
        var means = MaterialMeans.Of(material, new Files(new() { ["s.ktx2"] = surface }));
        Assert.Equal(1f + 0.5f * (128 / 255f - 1f), means.Occlusion, 1e-4f);
        Assert.Equal(0.5f, means.Roughness, 1e-4f);
        Assert.Equal(64 / 255f, means.Metallic, 1e-4f);
        Assert.Equal(128 / 255f, means.DiffuseTransmission, 1e-4f);
    }

    [Fact]
    public void The_Means_Are_Read_From_The_First_Level_That_Fits_Sixty_Four_Texels()
    {
        // 128 by 128 white, then 64 by 64 black, so a mean of black says the second level was read.
        var file = Ktx2Files.Write(43, 128, 128, [Fill(128, 255), Fill(64, 0), Fill(32, 255)]);
        var means = MaterialMeans.Of(new MaterialDescription { Pictures = [new MaterialPicture("a.ktx2", PictureLayout.Color)] }, new Files(new() { ["a.ktx2"] = file }));
        Assert.Equal(0f, means.Color.X);
    }

    [Fact]
    public void A_Picture_In_A_Format_The_Module_Does_Not_Read_Is_Decoded_By_The_Engine()
    {
        // The same half sRGB 188 and half black as a KTX2 gives, so the engine's decoding gives the
        // same linear mean of 0.25 its own picture holds.
        byte[] rgba = [188, 188, 188, 255, 0, 0, 0, 255];
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10];
        var material = new MaterialDescription { Pictures = [new MaterialPicture("a.png", PictureLayout.Color)] };
        var means = MaterialMeans.Of(material, new Decoding(new() { ["a.png"] = png }, rgba, 2, 1));
        Assert.Equal(0.2509f, means.Color.X, 1e-3f);
    }

    [Fact]
    public void An_Engines_Decoding_Gives_Rows_From_The_Top_In_The_Pictures_Own_Numbers()
    {
        // A surface decoded by the engine, its top row's roughness 255 and its bottom row's 0, as
        // stored and not decoded from sRGB, under an albedo whose top row alone is opaque, so a
        // mask keeps the top row's roughness of one where rows given from the bottom would keep
        // none.
        byte[] surface = [255, 255, 0, 0, 255, 0, 0, 0];
        var albedo = Ktx2Files.Write(43, 1, 2, [[255, 255, 255, 255, 255, 255, 255, 0]]);
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G'];
        var material = new MaterialDescription
        {
            AlphaMode = AlphaMode.Mask,
            Pictures = [new MaterialPicture("a.ktx2", PictureLayout.Color), new MaterialPicture("s.png", PictureLayout.Surface)],
        };
        var means = MaterialMeans.Of(material, new Decoding(new() { ["a.ktx2"] = albedo, ["s.png"] = png }, surface, 1, 2));
        Assert.Equal(1f, means.Roughness, 1e-6f);
        Assert.Equal(1f, means.Occlusion, 1e-6f);
        Assert.Equal(0.5f, means.Opacity, 1e-6f);
    }

    [Fact]
    public void A_Picture_Neither_The_Module_Nor_The_Engine_Reads_Is_Refused_With_Its_Path()
    {
        var material = new MaterialDescription { Pictures = [new MaterialPicture("a.webp", PictureLayout.Color)] };
        var thrown = Assert.Throws<InvalidDataException>(() => MaterialMeans.Of(material, new Files(new() { ["a.webp"] = [1, 2, 3] })));
        Assert.Contains("a.webp", thrown.Message);
    }

    [Fact]
    public void A_Material_That_Breaks_A_Rule_Is_Refused_With_It()
    {
        var thrown = Assert.Throws<ArgumentException>(() => MaterialMeans.Of(new MaterialDescription { Metallic = 2 }, new Files([])));
        Assert.Contains("Metallic is 2", thrown.Message);
    }

    // An RGBA8 picture in sRGB of one level, its texels from a function of their place.
    private static byte[] Picture(int width, int height, Func<int, int, byte[]> texel)
    {
        var level = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            texel(x, y).CopyTo(level, (y * width + x) * 4);
        return Ktx2Files.Write(43, width, height, [level]);
    }

    // A square level of one gray throughout, opaque.
    private static byte[] Fill(int side, byte gray) =>
        [.. Enumerable.Range(0, side * side).SelectMany(_ => new[] { gray, gray, gray, (byte)255 })];
}
