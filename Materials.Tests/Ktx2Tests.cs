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
        var picture = Ktx2.Read(Ktx2Files.Write(139, 8, 2, levels));

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
        var picture = Ktx2.Read(Ktx2Files.Write(vkFormat, 4, 4, [level]));
        Assert.Equal((format, colorSpace), (picture.Format, picture.ColorSpace));
    }

    [Fact]
    public void A_Count_Of_No_Levels_Reads_The_One_The_File_Holds()
    {
        var file = Ktx2Files.Write(139, 4, 4, [Block(7)]);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(40), 0);
        Assert.Single(Ktx2.Read(file).Levels);
    }

    [Fact]
    public void A_Supercompressed_File_Is_Refused_With_Its_Scheme()
    {
        var file = Ktx2Files.Write(146, 4, 4, [new byte[16]]);
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
        var file = Ktx2Files.Write(139, 4, 4, [Block(1)]);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(at), value);
        Assert.Contains(reason, Assert.Throws<InvalidDataException>(() => Ktx2.Read(file)).Message);
    }

    [Fact]
    public void A_Level_Of_The_Wrong_Size_Or_Past_The_End_Is_Refused()
    {
        var file = Ktx2Files.Write(139, 4, 4, [Block(1)]);
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(88), 9);
        Assert.Contains("holds 9 bytes", Assert.Throws<InvalidDataException>(() => Ktx2.Read(file)).Message);

        file = Ktx2Files.Write(139, 4, 4, [Block(1)]);
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(80), (ulong)file.Length - 4);
        Assert.Contains("past the end", Assert.Throws<InvalidDataException>(() => Ktx2.Read(file)).Message);
    }

    [Fact]
    public void A_Level_Count_Past_What_The_Picture_Holds_Is_Refused()
    {
        // A 4 by 4 picture holds three levels, 4 by 4, 2 by 2 and 1 by 1, so a fourth is refused
        // though its block is the size a 1 by 1 level holds.
        var file = Ktx2Files.Write(139, 4, 4, [Block(1), Block(2), Block(3), Block(4)]);
        Assert.Contains("names 4 levels, where a 4 by 4 picture holds 3", Assert.Throws<InvalidDataException>(() => Ktx2.Read(file)).Message);
    }

    [Fact]
    public void A_Level_Count_Past_The_Largest_Signed_Integer_Is_Refused_As_Data()
    {
        var file = Ktx2Files.Write(139, 4, 4, [Block(1)]);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(40), 0x8000_0000);
        Assert.Contains("names 2147483648 levels", Assert.Throws<InvalidDataException>(() => Ktx2.Read(file)).Message);
    }

    [Fact]
    public void Bytes_That_Are_Not_Ktx2_Are_Refused()
    {
        Assert.Throws<InvalidDataException>(() => Ktx2.Read(new byte[100]));
        // The identifier alone, short of the header after it.
        Assert.Throws<InvalidDataException>(() => Ktx2.Read(Ktx2Files.Write(139, 4, 4, [Block(1)])[..12]));
    }

    // A BC4 block of one value throughout.
    private static byte[] Block(byte value) => [value, value, 0, 0, 0, 0, 0, 0];
}
