using System.Buffers.Binary;

namespace Materials;

/// <summary>
/// A picture's texels in a <see cref="PictureFormat"/>, their size and their decoding to four bytes
/// a texel.
/// </summary>
public static class Texels
{
    /// <summary>
    /// How many bytes a block of the format holds, or 4 for a texel of
    /// <see cref="PictureFormat.Rgba8"/>.
    /// </summary>
    /// <param name="format">The format.</param>
    public static int BlockBytes(PictureFormat format) => format switch
    {
        PictureFormat.Rgba8 => 4,
        PictureFormat.Bc1 or PictureFormat.Bc1Rgb or PictureFormat.Bc4 => 8,
        PictureFormat.Bc5 or PictureFormat.Bc7 => 16,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "No such picture format."),
    };

    /// <summary>
    /// How many bytes a picture of the size holds in the format, in whole blocks.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="width">The width in texels, one or more.</param>
    /// <param name="height">The height in texels, one or more.</param>
    public static long ByteCount(PictureFormat format, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (format == PictureFormat.Rgba8) return 4L * width * height;
        return BlockBytes(format) * ((width + 3L) / 4) * ((height + 3L) / 4);
    }

    /// <summary>
    /// The texels decoded to four bytes each, red, green, blue and alpha, rows from the top, as a
    /// GPU samples the format before any sRGB is decoded.
    /// </summary>
    /// <remarks>
    /// A channel the format does not hold reads as a GPU reads it, zero for green and blue and 255
    /// for alpha, so <see cref="PictureFormat.Bc4"/> gives its value in red alone and
    /// <see cref="PictureFormat.Bc5"/> its two in red and green. Numbers between a block's two ends
    /// are rounded down for BC1 and BC4, as Pillow's decoder rounds them, where the Khronos Data
    /// Format Specification gives exact thirds, fifths and sevenths, so a GPU's may differ from
    /// these by one. BC7's are the specification's own integer weights, rounded to the nearest.
    /// </remarks>
    /// <param name="format">The format the texels are in.</param>
    /// <param name="data">The texels, blocks in rows from the top, exactly <see cref="ByteCount"/>
    /// bytes.</param>
    /// <param name="width">The width in texels.</param>
    /// <param name="height">The height in texels.</param>
    /// <exception cref="ArgumentException">The data is not the size the format and the size
    /// give.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The picture decodes to more bytes than an array holds, <see cref="Array.MaxLength"/>.
    /// </exception>
    public static byte[] Decode(PictureFormat format, ReadOnlySpan<byte> data, int width, int height)
    {
        var expected = ByteCount(format, width, height);
        long decoded = 4L * width * height;
        if (decoded > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(width), $"A {width} by {height} picture decodes to {decoded} bytes, past the {Array.MaxLength} an array holds.");
        if (data.Length != expected)
            throw new ArgumentException($"A {width} by {height} picture in {format} holds {expected} bytes, and {data.Length} were given.", nameof(data));

        var texels = new byte[4L * width * height];
        if (format == PictureFormat.Rgba8)
        {
            data.CopyTo(texels);
            return texels;
        }

        var size = BlockBytes(format);
        Span<byte> block = stackalloc byte[64];
        int across = (width + 3) / 4, down = (height + 3) / 4;
        for (int by = 0; by < down; by++)
        for (int bx = 0; bx < across; bx++)
        {
            var source = data.Slice((by * across + bx) * size, size);
            switch (format)
            {
                case PictureFormat.Bc1: Bc1(source, block, alpha: true); break;
                case PictureFormat.Bc1Rgb: Bc1(source, block, alpha: false); break;
                case PictureFormat.Bc4: Bc4(source, block, 0, plain: true); break;
                case PictureFormat.Bc5:
                    Bc4(source[..8], block, 0, plain: true);
                    Bc4(source[8..], block, 1, plain: false);
                    break;
                default: Bc7(source, block); break;
            }

            for (int y = 0; y < 4 && by * 4 + y < height; y++)
            for (int x = 0; x < 4 && bx * 4 + x < width; x++)
                block.Slice((y * 4 + x) * 4, 4).CopyTo(texels.AsSpan((int)(((long)(by * 4 + y) * width + bx * 4 + x) * 4), 4));
        }

        return texels;
    }

    // -- BC1

    private static void Bc1(ReadOnlySpan<byte> source, Span<byte> block, bool alpha)
    {
        int c0 = BinaryPrimitives.ReadUInt16LittleEndian(source);
        int c1 = BinaryPrimitives.ReadUInt16LittleEndian(source[2..]);
        uint indices = BinaryPrimitives.ReadUInt32LittleEndian(source[4..]);

        Span<int> palette = stackalloc int[16];
        Expand565(c0, palette[..4]);
        Expand565(c1, palette.Slice(4, 4));
        for (int c = 0; c < 3; c++)
        {
            int a = palette[c], b = palette[4 + c];
            if (c0 > c1)
            {
                palette[8 + c] = (2 * a + b) / 3;
                palette[12 + c] = (a + 2 * b) / 3;
            }
            else
            {
                palette[8 + c] = (a + b) / 2;
                palette[12 + c] = 0;
            }
        }

        palette[11] = 255;
        palette[15] = c0 > c1 || !alpha ? 255 : 0;
        for (int i = 0; i < 16; i++)
        {
            int index = (int)(indices >> (2 * i)) & 3;
            for (int c = 0; c < 4; c++) block[i * 4 + c] = (byte)palette[index * 4 + c];
        }
    }

    private static void Expand565(int color, Span<int> rgba)
    {
        int r = (color >> 11) & 31, g = (color >> 5) & 63, b = color & 31;
        rgba[0] = (r << 3) | (r >> 2);
        rgba[1] = (g << 2) | (g >> 4);
        rgba[2] = (b << 3) | (b >> 2);
        rgba[3] = 255;
    }

    // -- BC4, and each half of BC5

    // Writes a channel of the block from a BC4 block, and where plain says so the channels it
    // leaves as a GPU reads them, zero green and blue and an opaque alpha.
    private static void Bc4(ReadOnlySpan<byte> source, Span<byte> block, int channel, bool plain)
    {
        int r0 = source[0], r1 = source[1];
        ulong indices = 0;
        for (int i = 0; i < 6; i++) indices |= (ulong)source[2 + i] << (8 * i);

        Span<int> palette = stackalloc int[8];
        palette[0] = r0;
        palette[1] = r1;
        if (r0 > r1)
        {
            for (int i = 1; i < 7; i++) palette[i + 1] = ((7 - i) * r0 + i * r1) / 7;
        }
        else
        {
            for (int i = 1; i < 5; i++) palette[i + 1] = ((5 - i) * r0 + i * r1) / 5;
            palette[6] = 0;
            palette[7] = 255;
        }

        for (int i = 0; i < 16; i++)
        {
            if (plain)
            {
                block[i * 4 + 1] = 0;
                block[i * 4 + 2] = 0;
                block[i * 4 + 3] = 255;
            }

            block[i * 4 + channel] = (byte)palette[(int)(indices >> (3 * i)) & 7];
        }
    }

    // -- BC7

    // Each mode's subsets, partition bits, rotation bits, index selection bits, color bits, alpha
    // bits, a p-bit an endpoint, a p-bit a subset, index bits and second index bits, as the
    // format's specification tabulates them.
    private static readonly (int Subsets, int Partition, int Rotation, int Selection, int Color, int Alpha, int EndpointP, int SharedP, int Index, int SecondIndex)[] Modes =
    [
        (3, 4, 0, 0, 4, 0, 1, 0, 3, 0),
        (2, 6, 0, 0, 6, 0, 0, 1, 3, 0),
        (3, 6, 0, 0, 5, 0, 0, 0, 2, 0),
        (2, 6, 0, 0, 7, 0, 1, 0, 2, 0),
        (1, 0, 2, 1, 5, 6, 0, 0, 2, 3),
        (1, 0, 2, 0, 7, 8, 0, 0, 2, 2),
        (1, 0, 0, 0, 7, 7, 1, 0, 4, 0),
        (2, 6, 0, 0, 5, 5, 1, 0, 2, 0),
    ];

    private static readonly byte[][] Weights =
    [
        [],
        [],
        [0, 21, 43, 64],
        [0, 9, 18, 27, 37, 46, 55, 64],
        [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64],
    ];

    private static void Bc7(ReadOnlySpan<byte> source, Span<byte> block)
    {
        var bits = BinaryPrimitives.ReadUInt128LittleEndian(source);
        int mode = 0;
        while (mode < 8 && Take(ref bits, 1) == 0) mode++;
        if (mode == 8)
        {
            // A block with no mode is reserved, and decodes to transparent black.
            block.Clear();
            return;
        }

        var m = Modes[mode];
        int partition = Take(ref bits, m.Partition);
        int rotation = Take(ref bits, m.Rotation);
        int selection = Take(ref bits, m.Selection);

        int ends = m.Subsets * 2;
        Span<int> endpoints = stackalloc int[6 * 4];
        for (int c = 0; c < 3; c++)
        for (int e = 0; e < ends; e++)
            endpoints[e * 4 + c] = Take(ref bits, m.Color);
        if (m.Alpha > 0)
            for (int e = 0; e < ends; e++)
                endpoints[e * 4 + 3] = Take(ref bits, m.Alpha);

        int colorBits = m.Color, alphaBits = m.Alpha;
        if (m.EndpointP > 0 || m.SharedP > 0)
        {
            Span<int> p = stackalloc int[6];
            if (m.EndpointP > 0)
                for (int e = 0; e < ends; e++) p[e] = Take(ref bits, 1);
            else
                for (int s = 0; s < m.Subsets; s++) p[2 * s] = p[2 * s + 1] = Take(ref bits, 1);
            for (int e = 0; e < ends; e++)
            for (int c = 0; c < (m.Alpha > 0 ? 4 : 3); c++)
                endpoints[e * 4 + c] = (endpoints[e * 4 + c] << 1) | p[e];
            colorBits++;
            if (m.Alpha > 0) alphaBits++;
        }

        for (int e = 0; e < ends; e++)
        {
            for (int c = 0; c < 3; c++) endpoints[e * 4 + c] = Widen(endpoints[e * 4 + c], colorBits);
            endpoints[e * 4 + 3] = m.Alpha > 0 ? Widen(endpoints[e * 4 + 3], alphaBits) : 255;
        }

        Span<int> subset = stackalloc int[16];
        Span<bool> anchor = stackalloc bool[16];
        anchor[0] = true;
        if (m.Subsets == 2)
        {
            for (int i = 0; i < 16; i++) subset[i] = TwoSubsets[partition][i] - '0';
            anchor[SecondAnchor[partition]] = true;
        }
        else if (m.Subsets == 3)
        {
            for (int i = 0; i < 16; i++) subset[i] = ThreeSubsets[partition][i] - '0';
            anchor[ThirdAnchorOfSecond[partition]] = true;
            anchor[ThirdAnchorOfThird[partition]] = true;
        }

        // An anchor texel's index has its top bit left out, which the encoder keeps zero.
        Span<int> first = stackalloc int[16];
        Span<int> second = stackalloc int[16];
        for (int i = 0; i < 16; i++) first[i] = Take(ref bits, m.Index - (anchor[i] ? 1 : 0));
        if (m.SecondIndex > 0)
            for (int i = 0; i < 16; i++) second[i] = Take(ref bits, m.SecondIndex - (i == 0 ? 1 : 0));

        for (int i = 0; i < 16; i++)
        {
            int s = subset[i];
            int colorWeight, alphaWeight;
            if (m.SecondIndex == 0)
                colorWeight = alphaWeight = Weights[m.Index][first[i]];
            else if (selection == 0)
            {
                colorWeight = Weights[m.Index][first[i]];
                alphaWeight = Weights[m.SecondIndex][second[i]];
            }
            else
            {
                colorWeight = Weights[m.SecondIndex][second[i]];
                alphaWeight = Weights[m.Index][first[i]];
            }

            for (int c = 0; c < 4; c++)
            {
                int a = endpoints[2 * s * 4 + c], b = endpoints[(2 * s + 1) * 4 + c];
                int w = c < 3 ? colorWeight : alphaWeight;
                block[i * 4 + c] = (byte)(((64 - w) * a + w * b + 32) >> 6);
            }

            // A rotation swaps alpha with red, green or blue, so the channel coded apart is any
            // one.
            if (rotation > 0)
                (block[i * 4 + 3], block[i * 4 + rotation - 1]) = (block[i * 4 + rotation - 1], block[i * 4 + 3]);
        }
    }

    private static int Take(ref UInt128 bits, int count)
    {
        if (count == 0) return 0;
        int value = (int)(bits & (((UInt128)1 << count) - 1));
        bits >>= count;
        return value;
    }

    // An endpoint of some bits widened to eight, its top bits repeated below it, so the largest is
    // 255.
    private static int Widen(int value, int bits)
    {
        value <<= 8 - bits;
        return value | (value >> bits);
    }

    // The format's partitions, each a row of sixteen texels' subsets read left to right and top to
    // bottom, and the anchor texel of each subset past the first, from the Khronos Data Format
    // Specification's tables for BC7.
    private static readonly string[] TwoSubsets =
    [
        "0011001100110011",
        "0001000100010001",
        "0111011101110111",
        "0001001100110111",
        "0000000100010011",
        "0011011101111111",
        "0001001101111111",
        "0000000100110111",
        "0000000000010011",
        "0011011111111111",
        "0000000101111111",
        "0000000000010111",
        "0001011111111111",
        "0000000011111111",
        "0000111111111111",
        "0000000000001111",
        "0000100011101111",
        "0111000100000000",
        "0000000010001110",
        "0111001100010000",
        "0011000100000000",
        "0000100011001110",
        "0000000010001100",
        "0111001100110001",
        "0011000100010000",
        "0000100010001100",
        "0110011001100110",
        "0011011001101100",
        "0001011111101000",
        "0000111111110000",
        "0111000110001110",
        "0011100110011100",
        "0101010101010101",
        "0000111100001111",
        "0101101001011010",
        "0011001111001100",
        "0011110000111100",
        "0101010110101010",
        "0110100101101001",
        "0101101010100101",
        "0111001111001110",
        "0001001111001000",
        "0011001001001100",
        "0011101111011100",
        "0110100110010110",
        "0011110011000011",
        "0110011010011001",
        "0000011001100000",
        "0100111001000000",
        "0010011100100000",
        "0000001001110010",
        "0000010011100100",
        "0110110010010011",
        "0011011011001001",
        "0110001110011100",
        "0011100111000110",
        "0110110011001001",
        "0110001100111001",
        "0111111010000001",
        "0001100011100111",
        "0000111100110011",
        "0011001111110000",
        "0010001011101110",
        "0100010001110111",
    ];

    private static readonly string[] ThreeSubsets =
    [
        "0011001102212222",
        "0001001122112221",
        "0000200122112211",
        "0222002200110111",
        "0000000011221122",
        "0011001100220022",
        "0022002211111111",
        "0011001122112211",
        "0000000011112222",
        "0000111111112222",
        "0000111122222222",
        "0012001200120012",
        "0112011201120112",
        "0122012201220122",
        "0011011211221222",
        "0011200122002220",
        "0001001101121122",
        "0111001120012200",
        "0000112211221122",
        "0022002200221111",
        "0111011102220222",
        "0001000122212221",
        "0000001101220122",
        "0000110022102210",
        "0122012200110000",
        "0012001211222222",
        "0110122112210110",
        "0000011012211221",
        "0022110211020022",
        "0110011020022222",
        "0011012201220011",
        "0000200022112221",
        "0000000211221222",
        "0222002200120011",
        "0011001200220222",
        "0120012001200120",
        "0000111122220000",
        "0120120120120120",
        "0120201212010120",
        "0011220011220011",
        "0011112222000011",
        "0101010122222222",
        "0000000021212121",
        "0022112200221122",
        "0022001100220011",
        "0220122102201221",
        "0101222222220101",
        "0000212121212121",
        "0101010101012222",
        "0222011102220111",
        "0002111200021112",
        "0000211221122112",
        "0222011101110222",
        "0002111211120002",
        "0110011001102222",
        "0000000021122112",
        "0110011022222222",
        "0022001100110022",
        "0022112211220022",
        "0000000000002112",
        "0002000100020001",
        "0222122202221222",
        "0101222222222222",
        "0111201122012220",
    ];

    private static readonly byte[] SecondAnchor =
    [
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 2, 8, 2, 2, 8, 8, 15, 2, 8, 2, 2, 8, 8, 2, 2,
        15, 15, 6, 8, 2, 8, 15, 15, 2, 8, 2, 2, 2, 15, 15, 6,
        6, 2, 6, 8, 15, 15, 2, 2, 15, 15, 15, 15, 15, 2, 2, 15,
    ];

    private static readonly byte[] ThirdAnchorOfSecond =
    [
        3, 3, 15, 15, 8, 3, 15, 15, 8, 8, 6, 6, 6, 5, 3, 3,
        3, 3, 8, 15, 3, 3, 6, 10, 5, 8, 8, 6, 8, 5, 15, 15,
        8, 15, 3, 5, 6, 10, 8, 15, 15, 3, 15, 5, 15, 15, 15, 15,
        3, 15, 5, 5, 5, 8, 5, 10, 5, 10, 8, 13, 15, 12, 3, 3,
    ];

    private static readonly byte[] ThirdAnchorOfThird =
    [
        15, 8, 8, 3, 15, 15, 3, 8, 15, 15, 15, 15, 15, 15, 15, 8,
        15, 8, 15, 3, 15, 8, 15, 8, 3, 15, 6, 10, 15, 15, 10, 8,
        15, 3, 15, 10, 10, 8, 9, 10, 6, 15, 8, 15, 3, 6, 6, 8,
        15, 3, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 3, 15, 15, 8,
    ];
}
