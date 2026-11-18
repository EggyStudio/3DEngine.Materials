using System.Buffers.Binary;
using System.Text;

namespace Materials;

/// <summary>
/// A picture written as a KTX2 file of BC7 blocks with its levels down to a texel, the bytes
/// scripts/textures.py writes for the same picture, so an engine makes a texture at run time
/// without the script.
/// </summary>
/// <remarks>
/// <para>
/// Each block is written in BC7's mode 6, its endpoints along the block's principal axis and fitted
/// again by least squares, a block opaque throughout keeping its shared bits set so its alpha
/// decodes to 255, in the script's arithmetic and order. Each level is the one above halved by a
/// box, across and then down, each pass rounding a half up, as the script's Pillow halves it. The
/// levels are kept as they are with no supercompression.
/// </para>
/// <para>
/// The script writes a block whose colors leave one line in mode 1 or mode 7 and encodes BC5 and
/// BC4 as well, which this does not yet, so a picture with such blocks gets other bytes here than
/// from the script, mode 6's. Both sides are powers of two, so every halving is whole.
/// </para>
/// </remarks>
public static class Ktx2Writer
{
    private static readonly int[] Weights = [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];
    private static readonly int[] Nearest = [.. Enumerable.Range(0, 65).Select(t => Enumerable.Range(0, 16).MinBy(i => Math.Abs(Weights[i] - t)))];

    /// <summary>A picture as a KTX2 file of BC7 blocks with all its levels.</summary>
    /// <param name="rgba">The texels, four bytes each, rows from the top.</param>
    /// <param name="width">The width in texels, a power of two.</param>
    /// <param name="height">The height in texels, a power of two.</param>
    /// <param name="colorSpace">The color space the file names, sRGB for a color and linear
    /// otherwise.</param>
    /// <exception cref="ArgumentException">A side is no power of two, or the texels are not its
    /// size.</exception>
    public static byte[] Encode(byte[] rgba, int width, int height, ColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width < 1 || height < 1 || (width & (width - 1)) != 0 || (height & (height - 1)) != 0)
            throw new ArgumentException($"A picture of {width} by {height} is encoded where each side is a power of two, so every level halves whole.");
        if (rgba.Length != 4L * width * height)
            throw new ArgumentException($"A picture of {width} by {height} holds {4L * width * height} bytes, and {rgba.Length} were given.", nameof(rgba));

        var levels = new List<(byte[] Rgba, int Width, int Height)> { (rgba, width, height) };
        while (levels[^1].Width > 1 || levels[^1].Height > 1)
            levels.Add(Halved(levels[^1].Rgba, levels[^1].Width, levels[^1].Height));
        var blocks = levels.Select(level => Blocks(level.Rgba, level.Width, level.Height)).ToList();
        return File(colorSpace == ColorSpace.Srgb ? 146u : 145u, width, height, blocks);
    }

    // A level halved where its side is more than one, across and then down, each pass a half up.
    private static (byte[] Rgba, int Width, int Height) Halved(byte[] rgba, int width, int height)
    {
        int w = Math.Max(width / 2, 1), h = Math.Max(height / 2, 1);
        var across = new byte[w * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < w; x++)
        for (int c = 0; c < 4; c++)
            across[(y * w + x) * 4 + c] = width == 1 ? rgba[(y * width + x) * 4 + c]
                : (byte)((rgba[(y * width + 2 * x) * 4 + c] + rgba[(y * width + 2 * x + 1) * 4 + c] + 1) >> 1);
        var down = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        for (int c = 0; c < 4; c++)
            down[(y * w + x) * 4 + c] = height == 1 ? across[(y * w + x) * 4 + c]
                : (byte)((across[(2 * y * w + x) * 4 + c] + across[((2 * y + 1) * w + x) * 4 + c] + 1) >> 1);
        return (down, w, h);
    }

    // A level's blocks in rows from the top, a level smaller than a block repeating its edge.
    private static byte[] Blocks(byte[] rgba, int width, int height)
    {
        var output = new List<byte>();
        var texels = new int[16][];
        for (int by = 0; by < Math.Max(height, 4); by += 4)
        for (int bx = 0; bx < Math.Max(width, 4); bx += 4)
        {
            for (int i = 0; i < 16; i++)
            {
                int x = Math.Min(bx + i % 4, width - 1), y = Math.Min(by + i / 4, height - 1), at = (y * width + x) * 4;
                texels[i] = [rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]];
            }

            output.AddRange(Mode6(texels));
        }

        return [.. output];
    }

    private static byte[] Mode6(int[][] texels)
    {
        // The endpoints along the principal axis of the block's colors, by six rounds of the power
        // method on their covariance, started from the column of the channel that varies most.
        var mean = new double[4];
        for (int c = 0; c < 4; c++)
        {
            double sum = 0;
            foreach (var texel in texels) sum += texel[c];
            mean[c] = sum / 16.0;
        }

        var covariance = new double[4, 4];
        var d = new double[4];
        foreach (var texel in texels)
        {
            for (int c = 0; c < 4; c++) d[c] = texel[c] - mean[c];
            for (int i = 0; i < 4; i++)
            for (int j = i; j < 4; j++)
                covariance[i, j] += d[i] * d[j];
        }

        for (int i = 0; i < 4; i++)
        for (int j = 0; j < i; j++)
            covariance[i, j] = covariance[j, i];
        int widest = 0;
        for (int c = 1; c < 4; c++)
            if (covariance[c, c] > covariance[widest, widest]) widest = c;
        var axis = new double[4];
        for (int j = 0; j < 4; j++) axis[j] = covariance[widest, j];
        for (int round = 0; round < 6; round++)
        {
            var next = new double[4];
            double largest = 0;
            for (int i = 0; i < 4; i++)
            {
                double sum = 0;
                for (int j = 0; j < 4; j++) sum += covariance[i, j] * axis[j];
                next[i] = sum;
                largest = Math.Max(largest, Math.Abs(sum));
            }

            axis = next;
            if (largest == 0.0) break;
            for (int i = 0; i < 4; i++) axis[i] /= largest;
        }

        double length = 0;
        foreach (var value in axis) length += value * value;
        double[] low = mean, high = mean;
        if (length != 0.0)
        {
            double least = double.MaxValue, most = double.MinValue;
            foreach (var texel in texels)
            {
                double place = 0;
                for (int c = 0; c < 4; c++) place += (texel[c] - mean[c]) * axis[c];
                place /= length;
                least = Math.Min(least, place);
                most = Math.Max(most, place);
            }

            low = new double[4];
            high = new double[4];
            for (int c = 0; c < 4; c++)
            {
                low[c] = mean[c] + axis[c] * least;
                high[c] = mean[c] + axis[c] * most;
            }
        }

        // A block opaque throughout keeps the shared bit set, so its alpha decodes to 255.
        int[] shared = texels.All(texel => texel[3] == 255) ? [1] : [0, 1];
        var (e0, e1) = (Quantize(low, shared), Quantize(high, shared));
        var indices = Indices(texels, e0, e1);
        if (LeastSquares(texels, indices) is { } refit)
        {
            var (o0, o1) = (Quantize(refit.Low, shared), Quantize(refit.High, shared));
            var others = Indices(texels, o0, o1);
            if (Error(texels, o0, o1, others) < Error(texels, e0, e1, indices)) (e0, e1, indices) = (o0, o1, others);
        }

        if (indices[0] >= 8)
        {
            (e0, e1) = (e1, e0);
            for (int i = 0; i < 16; i++) indices[i] = 15 - indices[i];
        }

        UInt128 bits = (UInt128)1 << 6;
        int shift = 7;
        for (int c = 0; c < 4; c++)
        {
            bits |= (UInt128)e0.Values[c] << shift;
            bits |= (UInt128)e1.Values[c] << (shift + 7);
            shift += 14;
        }

        bits |= (UInt128)e0.Bit << shift | (UInt128)e1.Bit << (shift + 1);
        shift += 2;
        bits |= (UInt128)indices[0] << shift;
        shift += 3;
        for (int i = 1; i < 16; i++, shift += 4) bits |= (UInt128)indices[i] << shift;
        var block = new byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(block, bits);
        return block;
    }

    // An endpoint's seven bits a channel and the bit they share, the pair nearest it of the bits
    // allowed, the first kept where two are as near.
    private static (int[] Values, int Bit) Quantize(double[] endpoint, int[] allowed)
    {
        (double Error, int[] Values, int Bit)? best = null;
        foreach (var bit in allowed)
        {
            var values = new int[4];
            double error = 0;
            for (int c = 0; c < 4; c++)
            {
                values[c] = Math.Clamp((int)((Math.Clamp(endpoint[c], 0.0, 255.0) - bit) / 2 + 0.5), 0, 127);
                error += Math.Pow((values[c] << 1 | bit) - endpoint[c], 2);
            }

            if (best is null || error < best.Value.Error) best = (error, values, bit);
        }

        return (best!.Value.Values, best.Value.Bit);
    }

    private static int[] Expand((int[] Values, int Bit) end) => [.. end.Values.Select(value => value << 1 | end.Bit)];

    private static int[] Indices(int[][] texels, (int[] Values, int Bit) a, (int[] Values, int Bit) b)
    {
        int[] c0 = Expand(a), c1 = Expand(b), d = new int[4];
        int dd = 0;
        for (int c = 0; c < 4; c++)
        {
            d[c] = c1[c] - c0[c];
            dd += d[c] * d[c];
        }

        var indices = new int[16];
        if (dd == 0) return indices;
        for (int i = 0; i < 16; i++)
        {
            int dot = 0;
            for (int c = 0; c < 4; c++) dot += (texels[i][c] - c0[c]) * d[c];
            indices[i] = Nearest[Math.Clamp((int)(dot * 64 / (double)dd + 0.5), 0, 64)];
        }

        return indices;
    }

    private static long Error(int[][] texels, (int[] Values, int Bit) a, (int[] Values, int Bit) b, int[] indices)
    {
        int[] c0 = Expand(a), c1 = Expand(b);
        long error = 0;
        for (int i = 0; i < 16; i++)
        {
            int w = Weights[indices[i]];
            for (int c = 0; c < 4; c++)
            {
                long off = (((64 - w) * c0[c] + w * c1[c] + 32) >> 6) - texels[i][c];
                error += off * off;
            }
        }

        return error;
    }

    // The endpoints that best give the texels at the weights the indices name, or nothing where
    // they are all one.
    private static (double[] Low, double[] High)? LeastSquares(int[][] texels, int[] indices)
    {
        double a00 = 0, a01 = 0, a11 = 0;
        double[] b0 = new double[4], b1 = new double[4];
        for (int i = 0; i < 16; i++)
        {
            double t = Weights[indices[i]] / 64.0, s = 1.0 - t;
            a00 += s * s;
            a01 += s * t;
            a11 += t * t;
            for (int c = 0; c < 4; c++)
            {
                b0[c] += s * texels[i][c];
                b1[c] += t * texels[i][c];
            }
        }

        double determinant = a00 * a11 - a01 * a01;
        if (Math.Abs(determinant) < 1e-9) return null;
        double[] low = new double[4], high = new double[4];
        for (int c = 0; c < 4; c++)
        {
            low[c] = (a11 * b0[c] - a01 * b1[c]) / determinant;
            high[c] = (a00 * b1[c] - a01 * b0[c]) / determinant;
        }

        return (low, high);
    }

    // The file as the script lays it out, its header, its level index from the largest, its data
    // format descriptor, its key and value of the writer and its levels from the smallest, each at
    // a multiple of sixteen bytes.
    private static byte[] File(uint vkFormat, int width, int height, List<byte[]> levels)
    {
        var descriptor = new List<byte>();
        void U32(List<byte> into, uint value) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, value); into.AddRange(b.ToArray()); }
        var block = new List<byte>();
        U32(block, 0);
        U32(block, 2u | (24u + 16u) << 16);
        block.AddRange([134, 1, (byte)(vkFormat == 146 ? 2 : 1), 0, 3, 3, 0, 0, 16, 0, 0, 0, 0, 0, 0, 0]);
        block.AddRange([0, 0, 127, 0, 0, 0, 0, 0]);
        U32(block, 0);
        U32(block, 0xFFFFFFFF);
        U32(descriptor, (uint)(4 + block.Count));
        descriptor.AddRange(block);

        var entry = Encoding.ASCII.GetBytes("KTXwriter\0build/textures.py\0");
        var keyValues = new List<byte>();
        U32(keyValues, (uint)entry.Length);
        keyValues.AddRange(entry);
        while (keyValues.Count % 4 != 0) keyValues.Add(0);

        int descriptorAt = 80 + 24 * levels.Count, keyValuesAt = descriptorAt + descriptor.Count, at = keyValuesAt + keyValues.Count;
        var offsets = new long[levels.Count];
        for (int level = levels.Count - 1; level >= 0; level--)
        {
            at += (16 - at % 16) % 16;
            offsets[level] = at;
            at += levels[level].Length;
        }

        var file = new byte[at];
        ReadOnlySpan<byte> identifier = [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];
        identifier.CopyTo(file);
        uint[] header = [vkFormat, 1, (uint)width, (uint)height, 0, 0, 1, (uint)levels.Count, 0, (uint)descriptorAt, (uint)descriptor.Count, (uint)keyValuesAt, (uint)keyValues.Count];
        for (int i = 0; i < header.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12 + 4 * i), header[i]);
        for (int level = 0; level < levels.Count; level++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(80 + 24 * level), (ulong)offsets[level]);
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(88 + 24 * level), (ulong)levels[level].Length);
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(96 + 24 * level), (ulong)levels[level].Length);
            levels[level].CopyTo(file, offsets[level]);
        }

        descriptor.CopyTo(file, descriptorAt);
        keyValues.CopyTo(file, keyValuesAt);
        return file;
    }
}
