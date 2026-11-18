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
/// Each block is written in BC7's mode 6 by default, its endpoints along the block's principal axis
/// and fitted again by least squares, a block opaque throughout keeping its shared bits set so its
/// alpha decodes to 255, in the script's arithmetic and order. Each level is the one above halved
/// by a box, across and then down, each pass rounding a half up, as the script's Pillow halves it.
/// The levels are kept as they are with no supercompression.
/// </para>
/// <para>
/// A block whose colors leave one line is written in mode 1's or mode 7's two subsets where the
/// script would write it so, and every floating sum is Python's, compensated, so two partitions as
/// near as each other rank as they do there. A side that halves unevenly is resampled by Pillow's
/// box weights in its fixed point, so a picture of any size gets the script's levels.
/// </para>
/// </remarks>
public static class Ktx2Writer
{
    private static readonly int[] Weights = [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];
    private static readonly int[] Nearest = [.. Enumerable.Range(0, 65).Select(t => Enumerable.Range(0, 16).MinBy(i => Math.Abs(Weights[i] - t)))];

    /// <summary>A picture as a KTX2 file of BC7 blocks with all its levels.</summary>
    /// <param name="rgba">The texels, four bytes each, rows from the top.</param>
    /// <param name="width">The width in texels, one or more.</param>
    /// <param name="height">The height in texels, one or more.</param>
    /// <param name="colorSpace">The color space the file names, sRGB for a color and linear
    /// otherwise.</param>
    /// <exception cref="ArgumentException">A side is below one, or the texels are not its
    /// size.</exception>
    public static byte[] Encode(byte[] rgba, int width, int height, ColorSpace colorSpace) =>
        Encode(rgba, width, height, PictureFormat.Bc7, colorSpace);

    /// <summary>
    /// A picture as a KTX2 file of BC7, of BC5 of its red and green as a normal holds them, or of
    /// BC4 of its red as a depth does, with all its levels.
    /// </summary>
    /// <param name="rgba">The texels, four bytes each, rows from the top.</param>
    /// <param name="width">The width in texels, one or more.</param>
    /// <param name="height">The height in texels, one or more.</param>
    /// <param name="format">BC7, BC5 or BC4.</param>
    /// <param name="colorSpace">The color space BC7's file names, sRGB or linear; BC5 and BC4 are
    /// linear.</param>
    /// <exception cref="ArgumentException">
    /// A side is below one, the texels are not its size, or the format is none of the three.
    /// </exception>
    public static byte[] Encode(byte[] rgba, int width, int height, PictureFormat format, ColorSpace colorSpace)
    {
        if (format is not (PictureFormat.Bc7 or PictureFormat.Bc5 or PictureFormat.Bc4))
            throw new ArgumentException($"The writer writes BC7, BC5 and BC4, and {format} was asked for.", nameof(format));
        ArgumentNullException.ThrowIfNull(rgba);
        if (width < 1 || height < 1)
            throw new ArgumentException($"A picture of {width} by {height} has no texels to encode.");
        if (rgba.Length != 4L * width * height)
            throw new ArgumentException($"A picture of {width} by {height} holds {4L * width * height} bytes, and {rgba.Length} were given.", nameof(rgba));

        var levels = new List<(byte[] Rgba, int Width, int Height)> { (rgba, width, height) };
        while (levels[^1].Width > 1 || levels[^1].Height > 1)
            levels.Add(Halved(levels[^1].Rgba, levels[^1].Width, levels[^1].Height));
        var blocks = levels.Select(level => Blocks(level.Rgba, level.Width, level.Height, format)).ToList();
        uint vkFormat = format switch
        {
            PictureFormat.Bc5 => 141u,
            PictureFormat.Bc4 => 139u,
            _ => colorSpace == ColorSpace.Srgb ? 146u : 145u,
        };
        return File(vkFormat, width, height, blocks);
    }

    // A level halved where its side is more than one, to half of it rounded down, across and then
    // down, each pass Pillow's box resampling, so a side that halves unevenly gets the script's
    // texels as one that halves whole does.
    private static (byte[] Rgba, int Width, int Height) Halved(byte[] rgba, int width, int height)
    {
        int w = Math.Max(width / 2, 1), h = Math.Max(height / 2, 1);
        var across = w == width ? rgba : new byte[w * height * 4];
        if (w != width)
        {
            var (bounds, weights) = Coefficients(width, w);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < w; x++)
            for (int c = 0; c < 4; c++)
            {
                int sum = 1 << (Precision - 1);
                for (int k = 0; k < bounds[x].Count; k++) sum += rgba[(y * width + bounds[x].First + k) * 4 + c] * weights[x][k];
                across[(y * w + x) * 4 + c] = Clip(sum);
            }
        }

        if (h == height) return (across, w, h);
        var (rows, rowWeights) = Coefficients(height, h);
        var down = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        for (int c = 0; c < 4; c++)
        {
            int sum = 1 << (Precision - 1);
            for (int k = 0; k < rows[y].Count; k++) sum += across[((rows[y].First + k) * w + x) * 4 + c] * rowWeights[y][k];
            down[(y * w + x) * 4 + c] = Clip(sum);
        }

        return (down, w, h);
    }

    // The bits of a weight's fraction in Pillow's resampling of eight-bit channels.
    private const int Precision = 22;

    // Each output texel's first source texel, how many it reads and their weights, for a box
    // filter resampling a side of one length to another, as Pillow's precompute_coeffs and
    // normalize_coeffs_8bpc give them.
    private static ((int First, int Count)[] Bounds, int[][] Weights) Coefficients(int inSize, int outSize)
    {
        double scale = (double)inSize / outSize, filterscale = Math.Max(scale, 1.0);
        double support = 0.5 * filterscale, inverse = 1.0 / filterscale;
        var bounds = new (int, int)[outSize];
        var weights = new int[outSize][];
        for (int x = 0; x < outSize; x++)
        {
            double center = (x + 0.5) * scale;
            int first = Math.Max(0, (int)(center - support + 0.5));
            int last = Math.Min(inSize, (int)(center + support + 0.5));
            int count = last - first;
            var raw = new double[count];
            double total = 0.0;
            for (int k = 0; k < count; k++)
            {
                double at = (k + first - center + 0.5) * inverse;
                raw[k] = at > -0.5 && at <= 0.5 ? 1.0 : 0.0;
                total += raw[k];
            }

            weights[x] = new int[count];
            for (int k = 0; k < count; k++)
            {
                double share = total != 0.0 ? raw[k] / total : raw[k];
                weights[x][k] = share < 0 ? (int)(-0.5 + share * (1 << Precision)) : (int)(0.5 + share * (1 << Precision));
            }

            bounds[x] = (first, count);
        }

        return (bounds, weights);
    }

    private static byte Clip(int sum) => sum >= 1 << Precision << 8 ? (byte)255 : sum <= 0 ? (byte)0 : (byte)(sum >> Precision);

    // A level's blocks in rows from the top, a level smaller than a block repeating its edge.
    private static byte[] Blocks(byte[] rgba, int width, int height, PictureFormat format)
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

            if (format == PictureFormat.Bc7) output.AddRange(Block(texels));
            else
            {
                output.AddRange(Bc4([.. texels.Select(texel => texel[0])]));
                if (format == PictureFormat.Bc5) output.AddRange(Bc4([.. texels.Select(texel => texel[1])]));
            }
        }

        return [.. output];
    }

    // A sum of floating numbers as Python's sum() gives it, which compensates each addition by
    // Neumaier's method, so a key near nothing comes out as the script's and two partitions as near
    // as each other rank as they do there.
    private struct Sum
    {
        private double _total, _compensation;

        public void Add(double x)
        {
            double t = _total + x;
            _compensation += Math.Abs(_total) >= Math.Abs(x) ? (_total - t) + x : (x - t) + _total;
            _total = t;
        }

        public readonly double Value => _compensation != 0 && double.IsFinite(_compensation) ? _total + _compensation : _total;
    }

    private static readonly int[] Weights3 = [0, 9, 18, 27, 37, 46, 55, 64];
    private static readonly int[] Nearest3 = [.. Enumerable.Range(0, 65).Select(t => Enumerable.Range(0, 8).MinBy(i => Math.Abs(Weights3[i] - t)))];
    private static readonly int[] Weights2 = [0, 21, 43, 64];
    private static readonly int[] Nearest2 = [.. Enumerable.Range(0, 65).Select(t => Enumerable.Range(0, 4).MinBy(i => Math.Abs(Weights2[i] - t)))];
    private static readonly int[] Masks = [.. Texels.TwoSubsets.Select(row => Enumerable.Range(0, 16).Sum(i => row[i] == '1' ? 1 << i : 0))];

    // The script's errors past which mode 1 is tried for an opaque block and mode 7 for another, a
    // mean of 4 a channel over three channels and over four.
    private const long Mode1Above = 16 * 3 * 4;
    private const long Mode7Above = 16 * 4 * 4;

    // A block in mode 6, or in mode 1 or mode 7 where the script would write it so.
    private static byte[] Block(int[][] texels)
    {
        var (block, error) = Mode6(texels);
        bool opaque = texels.All(texel => texel[3] == 255);
        if (opaque && error > Mode1Above && Mode1(texels) is { } one && one.Error < error) return one.Block;
        if (!opaque && error > Mode7Above && Mode7(texels) is { } seven && seven.Error < error) return seven.Block;
        return block;
    }

    private static (byte[] Block, long Error) Mode6(int[][] texels)
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
                var sum = new Sum();
                for (int j = 0; j < 4; j++) sum.Add(covariance[i, j] * axis[j]);
                next[i] = sum.Value;
                largest = Math.Max(largest, Math.Abs(next[i]));
            }

            axis = next;
            if (largest == 0.0) break;
            for (int i = 0; i < 4; i++) axis[i] /= largest;
        }

        var lengthSum = new Sum();
        foreach (var value in axis) lengthSum.Add(value * value);
        double length = lengthSum.Value;
        double[] low = mean, high = mean;
        if (length != 0.0)
        {
            double least = double.MaxValue, most = double.MinValue;
            foreach (var texel in texels)
            {
                var sum = new Sum();
                for (int c = 0; c < 4; c++) sum.Add((texel[c] - mean[c]) * axis[c]);
                double place = sum.Value / length;
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
        return (block, Error(texels, e0, e1, indices));
    }

    // -- BC4, and each half of BC5

    // One channel's block in BC4's eight-step mode, its highest value first, as the script writes
    // it.
    private static byte[] Bc4(int[] values)
    {
        int high = values.Max(), low = values.Min();
        ulong bits = (uint)high | (ulong)(uint)low << 8;
        if (high != low)
            for (int i = 0; i < 16; i++)
            {
                int step = (int)((values[i] - low) * 7 / (double)(high - low) + 0.5);
                int index = step == 7 ? 0 : step == 0 ? 1 : 8 - step;
                bits |= (ulong)(uint)index << (16 + 3 * i);
            }

        var block = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(block, bits);
        return block;
    }

    // -- Mode 1, two subsets of an opaque block, six bits a channel and a bit each subset's ends
    // share

    private static (byte[] Block, long Error)? Mode1(int[][] texels)
    {
        var colors = texels.Select(texel => new[] { texel[0], texel[1], texel[2] }).ToArray();
        if (colors.All(color => color.SequenceEqual(colors[0]))) return null;
        var moments = colors.Select(c => new long[] { 1, c[0], c[1], c[2], c[0] * c[0], c[0] * c[1], c[0] * c[2], c[1] * c[1], c[1] * c[2], c[2] * c[2] }).ToArray();
        var total = new long[10];
        foreach (var m in moments) for (int k = 0; k < 10; k++) total[k] += m[k];
        (byte[] Block, long Error)? best = null;
        foreach (var partition in Enumerable.Range(0, 64).OrderBy(p => OffLines(moments, total, Masks[p])).Take(2))
        {
            var encoded = Mode1Partition(colors, partition);
            if (best is null || encoded.Error < best.Value.Error) best = encoded;
        }

        return best;
    }

    // How far a partition's two subsets lie off the lines through them, from the colors' moments.
    private static double OffLines(long[][] moments, long[] total, int mask)
    {
        var second = new long[10];
        for (int at = mask; at != 0; at &= at - 1)
        {
            var m = moments[System.Numerics.BitOperations.TrailingZeroCount(at)];
            for (int k = 0; k < 10; k++) second[k] += m[k];
        }

        double result = 0.0;
        foreach (var sums in new[] { Enumerable.Range(0, 10).Select(k => total[k] - second[k]).ToArray(), second })
        {
            long count = sums[0];
            if (count < 2) continue;
            double mr = (double)sums[1] / count, mg = (double)sums[2] / count, mb = (double)sums[3] / count;
            double rr = sums[4] - count * mr * mr, rg = sums[5] - count * mr * mg, rb = sums[6] - count * mr * mb;
            double gg = sums[7] - count * mg * mg, gb = sums[8] - count * mg * mb, bb = sums[9] - count * mb * mb;
            double[,] cov = { { rr, rg, rb }, { rg, gg, gb }, { rb, gb, bb } };
            double trace = rr + gg + bb;
            int widest = 0;
            for (int c = 1; c < 3; c++) if (cov[c, c] > cov[widest, widest]) widest = c;
            double[] axis = [cov[widest, 0], cov[widest, 1], cov[widest, 2]];
            for (int round = 0; round < 4; round++)
            {
                double[] next = [0, 0, 0];
                for (int i = 0; i < 3; i++) next[i] = cov[i, 0] * axis[0] + cov[i, 1] * axis[1] + cov[i, 2] * axis[2];
                axis = next;
                double largest = Math.Max(Math.Max(Math.Abs(axis[0]), Math.Abs(axis[1])), Math.Abs(axis[2]));
                if (largest == 0.0) break;
                for (int i = 0; i < 3; i++) axis[i] /= largest;
            }

            double length = axis[0] * axis[0] + axis[1] * axis[1] + axis[2] * axis[2];
            double along = 0.0;
            if (length != 0.0)
            {
                var sum = new Sum();
                for (int i = 0; i < 3; i++) sum.Add(axis[i] * (cov[i, 0] * axis[0] + cov[i, 1] * axis[1] + cov[i, 2] * axis[2]));
                along = sum.Value / length;
            }

            result += trace - along;
        }

        return result;
    }

    private static (byte[] Block, long Error) Mode1Partition(int[][] colors, int partition)
    {
        var row = Texels.TwoSubsets[partition];
        int[][] subsets = [[.. Enumerable.Range(0, 16).Where(i => row[i] == '0')], [.. Enumerable.Range(0, 16).Where(i => row[i] == '1')]];
        int[] anchors = [0, Texels.SecondAnchor[partition]];
        var ends = new ((int[] Values, int Bit) A, (int[] Values, int Bit) B)[2];
        var indices = new int[16];
        long error = 0;
        for (int s = 0; s < 2; s++)
        {
            var (end, chosen, subsetError) = Mode1Subset([.. subsets[s].Select(i => colors[i])]);
            if (chosen[Array.IndexOf(subsets[s], anchors[s])] >= 4)
            {
                end = (end.B, end.A);
                chosen = [.. chosen.Select(i => 7 - i)];
            }

            ends[s] = end;
            for (int k = 0; k < subsets[s].Length; k++) indices[subsets[s][k]] = chosen[k];
            error += subsetError;
        }

        UInt128 bits = (UInt128)1 << 1;
        int shift = 2;
        bits |= (UInt128)partition << shift;
        shift += 6;
        for (int c = 0; c < 3; c++)
        for (int s = 0; s < 2; s++)
        {
            bits |= (UInt128)ends[s].A.Values[c] << shift;
            bits |= (UInt128)ends[s].B.Values[c] << (shift + 6);
            shift += 12;
        }

        for (int s = 0; s < 2; s++, shift++) bits |= (UInt128)ends[s].A.Bit << shift;
        for (int i = 0; i < 16; i++)
        {
            bits |= (UInt128)indices[i] << shift;
            shift += anchors.Contains(i) ? 2 : 3;
        }

        var block = new byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(block, bits);
        return (block, error);
    }

    private static (((int[] Values, int Bit) A, (int[] Values, int Bit) B) Ends, int[] Indices, long Error) Mode1Subset(int[][] colors)
    {
        var (low, high) = PrincipalEnds(colors);
        (((int[], int), (int[], int)) Ends, int[] Indices, long Error)? best = null;
        foreach (var bit in new[] { 0, 1 })
        {
            var ends = (Mode1Quantize(low, bit), Mode1Quantize(high, bit));
            var chosen = SubsetIndices(colors, ends, Mode1Expand, Weights3, Nearest3);
            var candidates = new List<(((int[], int), (int[], int)), int[])> { (ends, chosen) };
            if (LeastSquaresWeighted(colors, chosen, Weights3) is { } refit)
            {
                var others = (Mode1Quantize(refit.Low, bit), Mode1Quantize(refit.High, bit));
                candidates.Add((others, SubsetIndices(colors, others, Mode1Expand, Weights3, Nearest3)));
            }

            foreach (var (candidate, indices) in candidates)
            {
                var error = SubsetError(colors, candidate, indices, Mode1Expand, Weights3);
                if (best is null || error < best.Value.Error) best = (candidate, indices, error);
            }
        }

        return best!.Value;
    }

    private static (int[] Values, int Bit) Mode1Quantize(double[] endpoint, int bit)
    {
        var values = new int[endpoint.Length];
        for (int c = 0; c < endpoint.Length; c++)
        {
            double v = Math.Clamp(endpoint[c], 0.0, 255.0);
            int from = Math.Max(0, (int)(v / 4) - 1), to = Math.Min(63, (int)(v / 4) + 1);
            values[c] = Enumerable.Range(from, to - from + 1).MinBy(x => Math.Abs(Mode1Expand(x, bit) - v));
        }

        return (values, bit);
    }

    private static int Mode1Expand(int value, int bit)
    {
        int seven = value << 1 | bit;
        return seven << 1 | seven >> 6;
    }

    // -- Mode 7, two subsets with alpha, five bits a channel and a bit each end's own

    private static (byte[] Block, long Error)? Mode7(int[][] texels)
    {
        if (texels.All(texel => texel.SequenceEqual(texels[0]))) return null;
        (byte[] Block, long Error)? best = null;
        foreach (var partition in Enumerable.Range(0, 64).OrderBy(p => OffLines4(texels, Masks[p])).Take(2))
        {
            var encoded = Mode7Partition(texels, partition);
            if (best is null || encoded.Error < best.Value.Error) best = encoded;
        }

        return best;
    }

    private static double OffLines4(int[][] texels, int mask)
    {
        double total = 0.0;
        for (int subset = 0; subset < 2; subset++)
        {
            var members = Enumerable.Range(0, 16).Where(i => (mask >> i & 1) == subset).Select(i => texels[i]).ToArray();
            if (members.Length < 2) continue;
            double count = members.Length;
            var mean = new double[4];
            for (int c = 0; c < 4; c++) { double s = 0; foreach (var m in members) s += m[c]; mean[c] = s / count; }
            var cov = new double[4, 4];
            for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                var s = new Sum();
                foreach (var m in members) s.Add((m[i] - mean[i]) * (m[j] - mean[j]));
                cov[i, j] = s.Value;
            }

            var traceSum = new Sum();
            for (int c = 0; c < 4; c++) traceSum.Add(cov[c, c]);
            double trace = traceSum.Value;
            int widest = 0;
            for (int c = 1; c < 4; c++) if (cov[c, c] > cov[widest, widest]) widest = c;
            var axis = new double[4];
            for (int j = 0; j < 4; j++) axis[j] = cov[widest, j];
            for (int round = 0; round < 4; round++)
            {
                var next = new double[4];
                double largest = 0;
                for (int i = 0; i < 4; i++)
                {
                    var s = new Sum();
                    for (int j = 0; j < 4; j++) s.Add(cov[i, j] * axis[j]);
                    next[i] = s.Value;
                    largest = Math.Max(largest, Math.Abs(next[i]));
                }

                axis = next;
                if (largest == 0.0) break;
                for (int i = 0; i < 4; i++) axis[i] /= largest;
            }

            var lengthSum = new Sum();
            foreach (var a in axis) lengthSum.Add(a * a);
            double length = lengthSum.Value;
            double along = 0.0;
            if (length != 0.0)
            {
                var sum = new Sum();
                for (int i = 0; i < 4; i++)
                {
                    var inner = new Sum();
                    for (int j = 0; j < 4; j++) inner.Add(cov[i, j] * axis[j]);
                    sum.Add(axis[i] * inner.Value);
                }

                along = sum.Value / length;
            }

            total += trace - along;
        }

        return total;
    }

    private static (byte[] Block, long Error) Mode7Partition(int[][] texels, int partition)
    {
        var row = Texels.TwoSubsets[partition];
        int[][] subsets = [[.. Enumerable.Range(0, 16).Where(i => row[i] == '0')], [.. Enumerable.Range(0, 16).Where(i => row[i] == '1')]];
        int[] anchors = [0, Texels.SecondAnchor[partition]];
        var ends = new ((int[] Values, int Bit) A, (int[] Values, int Bit) B)[2];
        var indices = new int[16];
        long error = 0;
        for (int s = 0; s < 2; s++)
        {
            var members = subsets[s].Select(i => texels[i]).ToArray();
            var (low, high) = PrincipalEnds(members);
            var end = (A: Mode7Quantize(low), B: Mode7Quantize(high));
            var chosen = SubsetIndices(members, end, Mode7Expand, Weights2, Nearest2);
            var subsetError = SubsetError(members, end, chosen, Mode7Expand, Weights2);
            if (LeastSquaresWeighted(members, chosen, Weights2) is { } refit)
            {
                var others = (A: Mode7Quantize(refit.Low), B: Mode7Quantize(refit.High));
                var otherIndices = SubsetIndices(members, others, Mode7Expand, Weights2, Nearest2);
                var otherError = SubsetError(members, others, otherIndices, Mode7Expand, Weights2);
                if (otherError < subsetError) (end, chosen, subsetError) = (others, otherIndices, otherError);
            }

            if (chosen[Array.IndexOf(subsets[s], anchors[s])] >= 2)
            {
                end = (end.B, end.A);
                chosen = [.. chosen.Select(i => 3 - i)];
            }

            ends[s] = end;
            for (int k = 0; k < subsets[s].Length; k++) indices[subsets[s][k]] = chosen[k];
            error += subsetError;
        }

        UInt128 bits = (UInt128)1 << 7;
        int shift = 8;
        bits |= (UInt128)partition << shift;
        shift += 6;
        for (int c = 0; c < 4; c++)
        for (int s = 0; s < 2; s++)
        {
            bits |= (UInt128)ends[s].A.Values[c] << shift;
            bits |= (UInt128)ends[s].B.Values[c] << (shift + 5);
            shift += 10;
        }

        for (int s = 0; s < 2; s++)
        {
            bits |= (UInt128)ends[s].A.Bit << shift++;
            bits |= (UInt128)ends[s].B.Bit << shift++;
        }

        for (int i = 0; i < 16; i++)
        {
            bits |= (UInt128)indices[i] << shift;
            shift += anchors.Contains(i) ? 1 : 2;
        }

        var block = new byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(block, bits);
        return (block, error);
    }

    // An end's five bits a channel and its own bit, the pair nearest it.
    private static (int[] Values, int Bit) Mode7Quantize(double[] endpoint)
    {
        (double Error, int[] Values, int Bit)? best = null;
        foreach (var bit in new[] { 0, 1 })
        {
            var values = new int[4];
            var sum = new Sum();
            for (int c = 0; c < 4; c++)
            {
                double v = Math.Clamp(endpoint[c], 0.0, 255.0);
                int from = Math.Max(0, (int)(v / 8) - 1), to = Math.Min(31, (int)(v / 8) + 1);
                values[c] = Enumerable.Range(from, to - from + 1).MinBy(x => Math.Abs(Mode7Expand(x, bit) - v));
                sum.Add(Math.Pow(Mode7Expand(values[c], bit) - v, 2));
            }

            double error = sum.Value;

            if (best is null || error < best.Value.Error) best = (error, values, bit);
        }

        return (best!.Value.Values, best.Value.Bit);
    }

    private static int Mode7Expand(int value, int bit)
    {
        int six = value << 1 | bit;
        return six << 2 | six >> 4;
    }

    // -- What modes 1 and 7 share

    // The ends of a set of colors of any number of channels along their principal axis, as the
    // script's principal_ends gives them.
    private static (double[] Low, double[] High) PrincipalEnds(int[][] colors)
    {
        int n = colors[0].Length;
        double count = colors.Length;
        var mean = new double[n];
        for (int c = 0; c < n; c++) { double s = 0; foreach (var color in colors) s += color[c]; mean[c] = s / count; }
        var cov = new double[n, n];
        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
        {
            var s = new Sum();
            foreach (var color in colors) s.Add((color[i] - mean[i]) * (color[j] - mean[j]));
            cov[i, j] = s.Value;
        }

        int widest = 0;
        for (int c = 1; c < n; c++) if (cov[c, c] > cov[widest, widest]) widest = c;
        var axis = new double[n];
        for (int j = 0; j < n; j++) axis[j] = cov[widest, j];
        for (int round = 0; round < 6; round++)
        {
            var next = new double[n];
            double largest = 0;
            for (int i = 0; i < n; i++)
            {
                var s = new Sum();
                for (int j = 0; j < n; j++) s.Add(cov[i, j] * axis[j]);
                next[i] = s.Value;
                largest = Math.Max(largest, Math.Abs(next[i]));
            }

            axis = next;
            if (largest == 0.0) break;
            for (int i = 0; i < n; i++) axis[i] /= largest;
        }

        var lengthSum = new Sum();
        foreach (var a in axis) lengthSum.Add(a * a);
        double length = lengthSum.Value;
        if (length == 0.0) return (mean, mean);
        double least = double.MaxValue, most = double.MinValue;
        foreach (var color in colors)
        {
            var sum = new Sum();
            for (int c = 0; c < n; c++) sum.Add((color[c] - mean[c]) * axis[c]);
            double place = sum.Value / length;
            least = Math.Min(least, place);
            most = Math.Max(most, place);
        }

        double[] low = new double[n], high = new double[n];
        for (int c = 0; c < n; c++)
        {
            low[c] = mean[c] + axis[c] * least;
            high[c] = mean[c] + axis[c] * most;
        }

        return (low, high);
    }

    private static int[] SubsetIndices(int[][] colors, ((int[] Values, int Bit) A, (int[] Values, int Bit) B) ends, Func<int, int, int> expand, int[] weights, int[] nearest)
    {
        int n = colors[0].Length;
        var c0 = ends.A.Values.Select(v => expand(v, ends.A.Bit)).ToArray();
        var c1 = ends.B.Values.Select(v => expand(v, ends.B.Bit)).ToArray();
        int dd = 0;
        var d = new int[n];
        for (int c = 0; c < n; c++) { d[c] = c1[c] - c0[c]; dd += d[c] * d[c]; }
        var indices = new int[colors.Length];
        if (dd == 0) return indices;
        for (int i = 0; i < colors.Length; i++)
        {
            int dot = 0;
            for (int c = 0; c < n; c++) dot += (colors[i][c] - c0[c]) * d[c];
            indices[i] = nearest[Math.Clamp((int)(dot * 64 / (double)dd + 0.5), 0, 64)];
        }

        return indices;
    }

    private static long SubsetError(int[][] colors, ((int[] Values, int Bit) A, (int[] Values, int Bit) B) ends, int[] indices, Func<int, int, int> expand, int[] weights)
    {
        int n = colors[0].Length;
        var c0 = ends.A.Values.Select(v => expand(v, ends.A.Bit)).ToArray();
        var c1 = ends.B.Values.Select(v => expand(v, ends.B.Bit)).ToArray();
        long error = 0;
        for (int i = 0; i < colors.Length; i++)
        {
            int w = weights[indices[i]];
            for (int c = 0; c < n; c++)
            {
                long off = (((64 - w) * c0[c] + w * c1[c] + 32) >> 6) - colors[i][c];
                error += off * off;
            }
        }

        return error;
    }

    private static (double[] Low, double[] High)? LeastSquaresWeighted(int[][] colors, int[] indices, int[] weights)
    {
        int n = colors[0].Length;
        double a00 = 0, a01 = 0, a11 = 0;
        double[] b0 = new double[n], b1 = new double[n];
        for (int i = 0; i < colors.Length; i++)
        {
            double t = weights[indices[i]] / 64.0, s = 1.0 - t;
            a00 += s * s;
            a01 += s * t;
            a11 += t * t;
            for (int c = 0; c < n; c++)
            {
                b0[c] += s * colors[i][c];
                b1[c] += t * colors[i][c];
            }
        }

        double determinant = a00 * a11 - a01 * a01;
        if (Math.Abs(determinant) < 1e-9) return null;
        double[] low = new double[n], high = new double[n];
        for (int c = 0; c < n; c++)
        {
            low[c] = (a11 * b0[c] - a01 * b1[c]) / determinant;
            high[c] = (a00 * b1[c] - a01 * b0[c]) / determinant;
        }

        return (low, high);
    }

    // An endpoint's seven bits a channel and the bit they share, the pair nearest it of the bits
    // allowed, the first kept where two are as near.
    private static (int[] Values, int Bit) Quantize(double[] endpoint, int[] allowed)
    {
        (double Error, int[] Values, int Bit)? best = null;
        foreach (var bit in allowed)
        {
            var values = new int[4];
            var sum = new Sum();
            for (int c = 0; c < 4; c++)
            {
                values[c] = Math.Clamp((int)((Math.Clamp(endpoint[c], 0.0, 255.0) - bit) / 2 + 0.5), 0, 127);
                sum.Add(Math.Pow((values[c] << 1 | bit) - endpoint[c], 2));
            }

            double error = sum.Value;
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
        // The color model, the transfer, the block's bytes and its samples' offset, bit length less
        // one and channel, as the script's data_format gives them.
        var (model, transfer, bytes, samples) = vkFormat switch
        {
            139u => (131, 1, 8, new (int Offset, int Length, int Channel)[] { (0, 63, 0) }),
            141u => (132, 1, 16, [(0, 63, 0), (64, 63, 1)]),
            _ => (134, vkFormat == 146 ? 2 : 1, 16, [(0, 127, 0)]),
        };
        var block = new List<byte>();
        U32(block, 0);
        U32(block, 2u | (uint)(24 + 16 * samples.Length) << 16);
        block.AddRange([(byte)model, 1, (byte)transfer, 0, 3, 3, 0, 0, (byte)bytes, 0, 0, 0, 0, 0, 0, 0]);
        foreach (var (offset, length, channel) in samples)
        {
            block.AddRange([(byte)offset, (byte)(offset >> 8), (byte)length, (byte)channel, 0, 0, 0, 0]);
            U32(block, 0);
            U32(block, 0xFFFFFFFF);
        }
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
