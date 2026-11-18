using System.Buffers.Binary;
using Xunit;

namespace Materials.Tests;

public class Ktx2Tests
{
    [Fact]
    public void A_File_Gives_Its_Format_Color_Space_Size_And_Levels()
    {
        // An 8 by 2 picture in BC4, its levels 8 by 2 in two blocks and 4 by 1, 2 by 1 and 1 by 1
        // in a block each.
        byte[][] levels = [[.. Block(10), .. Block(10)], Block(20), Block(30), Block(40)];
        var picture = Ktx2.Read(File(139, 8, 2, levels));

        Assert.Equal(PictureFormat.Bc4, picture.Format);
        Assert.Equal(ColorSpace.Linear, picture.ColorSpace);
        Assert.Equal((8, 2), (picture.Width, picture.Height));
        Assert.Equal(4, picture.Levels.Count);
        Assert.Equal((2, 1), picture.LevelSize(2));
        Assert.Equal((1, 1), picture.LevelSize(3));
        Assert.Equal([30, 0, 0, 255, 30, 0, 0, 255], picture.Decode(2));
    }

    [Theory]
    [InlineData(37u, PictureFormat.Rgba8, ColorSpace.Linear)]
    [InlineData(43u, PictureFormat.Rgba8, ColorSpace.Srgb)]
    [InlineData(131u, PictureFormat.Bc1Rgb, ColorSpace.Linear)]
    [InlineData(132u, PictureFormat.Bc1Rgb, ColorSpace.Srgb)]
    [InlineData(133u, PictureFormat.Bc1, ColorSpace.Linear)]
    [InlineData(134u, PictureFormat.Bc1, ColorSpace.Srgb)]
    [InlineData(139u, PictureFormat.Bc4, ColorSpace.Linear)]
    [InlineData(141u, PictureFormat.Bc5, ColorSpace.Linear)]
    [InlineData(145u, PictureFormat.Bc7, ColorSpace.Linear)]
    [InlineData(146u, PictureFormat.Bc7, ColorSpace.Srgb)]
    public void The_Vulkan_Format_Gives_The_Format_And_The_Color_Space(uint vkFormat, PictureFormat format, ColorSpace colorSpace)
    {
        var level = new byte[Texels.ByteCount(format, 4, 4)];
        var picture = Ktx2.Read(File(vkFormat, 4, 4, [level]));
        Assert.Equal((format, colorSpace), (picture.Format, picture.ColorSpace));
    }

    [Fact]
    public void A_Count_Of_No_Levels_Reads_The_One_The_File_Holds()
    {
        var file = File(139, 4, 4, [Block(7)]);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(40), 0);
        Assert.Single(Ktx2.Read(file).Levels);
    }

    [Fact]
    public void A_Supercompressed_File_Is_Refused_With_Its_Scheme()
    {
        var file = File(146, 4, 4, [new byte[16]]);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(44), 2);
        var thrown = Assert.Throws<InvalidDataException>(() => Ktx2.Read(file));
        Assert.Contains("supercompressed (scheme 2)", thrown.Message);
    }

    [Theory]
    [InlineData(28, 1u, "a volume, an array or a cube")]
    [InlineData(32, 6u, "a volume, an array or a cube")]
    [InlineData(36, 6u, "a volume, an array or a cube")]
    [InlineData(12, 0u, "Basis Universal")]
    [InlineData(12, 142u, "Vulkan format is 142")]
    public void A_File_Of_Another_Kind_Is_Refused_With_The_Reason(int at, uint value, string reason)
    {
        var file = File(139, 4, 4, [Block(1)]);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(at), value);
        Assert.Contains(reason, Assert.Throws<InvalidDataException>(() => Ktx2.Read(file)).Message);
    }

    [Fact]
    public void A_Level_Of_The_Wrong_Size_Or_Past_The_End_Is_Refused()
    {
        var file = File(139, 4, 4, [Block(1)]);
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(88), 9);
        Assert.Contains("holds 9 bytes", Assert.Throws<InvalidDataException>(() => Ktx2.Read(file)).Message);

        file = File(139, 4, 4, [Block(1)]);
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(80), (ulong)file.Length - 4);
        Assert.Contains("past the end", Assert.Throws<InvalidDataException>(() => Ktx2.Read(file)).Message);
    }

    [Fact]
    public void Bytes_That_Are_Not_Ktx2_Are_Refused()
    {
        Assert.Throws<InvalidDataException>(() => Ktx2.Read(new byte[100]));
        // The identifier alone, short of the header after it.
        Assert.Throws<InvalidDataException>(() => Ktx2.Read(File(139, 4, 4, [Block(1)])[..12]));
    }

    // A BC4 block of one value throughout.
    private static byte[] Block(byte value) => [value, value, 0, 0, 0, 0, 0, 0];

    // A KTX2 file as the specification lays it out, the header, the index, the level index and the
    // levels, the smallest first in the file, with no data format descriptor, which this reads none
    // of.
    private static byte[] File(uint vkFormat, int width, int height, byte[][] levels)
    {
        int at = 80 + 24 * levels.Length;
        var offsets = new long[levels.Length];
        for (int level = levels.Length - 1; level >= 0; level--)
        {
            offsets[level] = at;
            at += levels[level].Length;
        }

        var file = new byte[at];
        ReadOnlySpan<byte> identifier = [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];
        identifier.CopyTo(file);
        uint[] header = [vkFormat, 1, (uint)width, (uint)height, 0, 0, 1, (uint)levels.Length, 0];
        for (int i = 0; i < header.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12 + 4 * i), header[i]);
        for (int level = 0; level < levels.Length; level++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(80 + 24 * level), (ulong)offsets[level]);
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(88 + 24 * level), (ulong)levels[level].Length);
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(96 + 24 * level), (ulong)levels[level].Length);
            levels[level].CopyTo(file.AsSpan((int)offsets[level]));
        }

        return file;
    }
}
