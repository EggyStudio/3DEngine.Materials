using System.Text.Json;
using Xunit;

namespace Materials.Tests;

public class TexelsTests
{
    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var item in Reference().RootElement.GetProperty("cases").EnumerateArray())
            data.Add(item.GetProperty("name").GetString()!);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_Block_Decodes_As_Pillows_Decoder_Decodes_It(string name)
    {
        // Pillow's decoders are written apart from the module's, so a fault in a mode's bits or in
        // a partition's table shows as a texel that differs (scripts/blocks.py).
        var item = Reference().RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);
        var format = Enum.Parse<PictureFormat>(item.GetProperty("format").GetString()!);
        int width = item.GetProperty("width").GetInt32(), height = item.GetProperty("height").GetInt32();
        int count = 0;
        foreach (var picture in item.GetProperty("blocks").EnumerateArray())
        {
            var bytes = Convert.FromHexString(picture.GetProperty("block").GetString()!);
            var expected = picture.GetProperty("texels").GetString()!;
            Assert.Equal(expected, Convert.ToHexStringLower(Texels.Decode(format, bytes, width, height)));
            count++;
        }

        Assert.True(count > 0, $"{name} holds no picture.");
    }

    [Fact]
    public void Bc1_Places_Two_Colors_Between_Its_Ends_Where_The_First_Is_The_Greater()
    {
        // Red and blue in 5:6:5, every texel the first color between, two thirds red.
        byte[] block = [0x00, 0xF8, 0x1F, 0x00, 0xAA, 0xAA, 0xAA, 0xAA];
        var texels = Texels.Decode(PictureFormat.Bc1, block, 4, 4);
        Assert.Equal([170, 0, 85, 255], texels[..4]);
    }

    [Fact]
    public void Bc1_Reads_Its_Fourth_Color_As_Transparent_Black_And_Bc1Rgb_As_Opaque_Black()
    {
        // The first color is not the greater, so index 3 is black, transparent where alpha is read.
        byte[] block = [0x1F, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF];
        Assert.Equal([0, 0, 0, 0], Texels.Decode(PictureFormat.Bc1, block, 4, 4)[..4]);
        Assert.Equal([0, 0, 0, 255], Texels.Decode(PictureFormat.Bc1Rgb, block, 4, 4)[..4]);
    }

    [Fact]
    public void An_Opaque_Bc7_Block_With_Its_Shared_Bits_Set_Decodes_To_255()
    {
        // Mode 6 with every endpoint's seven bits and both p-bits set, so each end is 255 in every
        // channel, which a p-bit left clear would make 254.
        var value = (UInt128.One << 6) | (((UInt128.One << 58) - 1) << 7) | (UInt128.One << 63) | (UInt128.One << 64);
        var block = new byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt128LittleEndian(block, value);
        Assert.All(Texels.Decode(PictureFormat.Bc7, block, 4, 4), texel => Assert.Equal(255, texel));

        var cleared = value & ~(UInt128.One << 63) & ~(UInt128.One << 64);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt128LittleEndian(block, cleared);
        Assert.All(Texels.Decode(PictureFormat.Bc7, block, 4, 4), texel => Assert.Equal(254, texel));
    }

    [Fact]
    public void The_Reserved_Bc7_Block_Of_No_Mode_Decodes_To_Zero()
    {
        // The format's specification gives zero in every channel, where Pillow gives opaque black.
        Assert.All(Texels.Decode(PictureFormat.Bc7, new byte[16], 4, 4), texel => Assert.Equal(0, texel));
    }

    [Fact]
    public void Bc4_And_Bc5_Read_As_A_Gpu_Samples_Them()
    {
        // Both ends 200 and 100 with every index 0, so the value is the first end.
        byte[] bc4 = [200, 100, 0, 0, 0, 0, 0, 0];
        Assert.Equal([200, 0, 0, 255], Texels.Decode(PictureFormat.Bc4, bc4, 4, 4)[..4]);
        byte[] bc5 = [200, 100, 0, 0, 0, 0, 0, 0, 50, 60, 0, 0, 0, 0, 0, 0];
        Assert.Equal([200, 50, 0, 255], Texels.Decode(PictureFormat.Bc5, bc5, 4, 4)[..4]);
    }

    [Fact]
    public void A_Picture_Whose_Side_Is_Not_A_Multiple_Of_Four_Keeps_The_Texels_Inside_It()
    {
        // Two BC4 blocks across for a width of five, the second's left column the fifth texel.
        var data = new byte[16];
        data[0] = data[1] = 10;
        data[8] = data[9] = 90;
        var texels = Texels.Decode(PictureFormat.Bc4, data, 5, 3);
        Assert.Equal(5 * 3 * 4, texels.Length);
        Assert.Equal(10, texels[(2 * 5 + 3) * 4]);
        Assert.Equal(90, texels[(2 * 5 + 4) * 4]);
        Assert.Equal(16, Texels.ByteCount(PictureFormat.Bc4, 5, 3));
        Assert.Equal(60, Texels.ByteCount(PictureFormat.Rgba8, 5, 3));
    }

    [Fact]
    public void Data_Of_Another_Size_Is_Refused_With_The_Size_Expected()
    {
        var thrown = Assert.Throws<ArgumentException>(() => Texels.Decode(PictureFormat.Bc7, new byte[15], 4, 4));
        Assert.Contains("holds 16 bytes, and 15 were given", thrown.Message);
    }

    [Fact]
    public void A_Picture_Decoded_Past_What_An_Array_Holds_Is_Refused_With_The_Reason()
    {
        // 32768 by 16384 texels decode to 2^31 bytes, past the largest array .NET makes.
        var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => Texels.Decode(PictureFormat.Bc1, new byte[8], 32768, 16384));
        Assert.Contains("decodes to 2147483648 bytes", thrown.Message);
    }

    [Fact]
    public void A_Size_Near_The_Largest_Integer_Counts_Its_Bytes_Without_Overflowing()
    {
        Assert.Equal(16L * (int.MaxValue / 4L + 1), Texels.ByteCount(PictureFormat.Bc7, int.MaxValue, 1));
        Assert.Equal(4L * int.MaxValue * 2, Texels.ByteCount(PictureFormat.Rgba8, int.MaxValue, 2));
    }

    private static JsonDocument Reference() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository.References, "blocks.json")));
}
