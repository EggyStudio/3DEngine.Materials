using System.Buffers.Binary;
using Xunit;

namespace Materials.Tests;

/// <summary>
/// The README's smallest binding, run against a host that stands for an engine and records what
/// the binding gives it, so the build and the suite prove the guide, the block read against the
/// README word for word by <see cref="ModuleTests"/>.
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
