using System.Buffers.Binary;

namespace Materials;

/// <summary>
/// A KTX2 file's picture, its format, color space, size and levels, read from the file's bytes.
/// </summary>
/// <remarks>
/// <para>
/// It reads a two-dimensional picture of one layer and one face, its levels kept as they are, in
/// the formats <see cref="PictureFormat"/> names, by the file's Vulkan format, which also gives the
/// color space. A file of a supercompressed level, a cube, an array, a volume, a Basis Universal
/// payload or another format is refused with the reason, since .NET's library holds no zstd and the
/// module decodes no other format.
/// </para>
/// <para>
/// Level 0 is the picture itself, and each level after it half the size of the one before, a texel
/// at least each way, as the KTX2 specification lays them out.
/// </para>
/// </remarks>
public sealed class Ktx2
{
    private static ReadOnlySpan<byte> Identifier => [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

    private Ktx2(PictureFormat format, ColorSpace colorSpace, int width, int height, byte[][] levels)
    {
        Format = format;
        ColorSpace = colorSpace;
        Width = width;
        Height = height;
        Levels = levels;
    }

    /// <summary>How the texels are stored.</summary>
    public PictureFormat Format { get; }

    /// <summary>
    /// Whether red, green and blue are sRGB or linear, as the file's format says.
    /// </summary>
    public ColorSpace ColorSpace { get; }

    /// <summary>The width of level 0 in texels.</summary>
    public int Width { get; }

    /// <summary>The height of level 0 in texels.</summary>
    public int Height { get; }

    /// <summary>Each level's texels in <see cref="Format"/>, level 0 first.</summary>
    public IReadOnlyList<byte[]> Levels { get; }

    /// <summary>A level's width and height in texels.</summary>
    /// <param name="level">The level, 0 for the picture itself.</param>
    public (int Width, int Height) LevelSize(int level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(level, Levels.Count);
        return (Math.Max(1, Width >> level), Math.Max(1, Height >> level));
    }

    /// <summary>
    /// A level decoded to four bytes a texel, as <see cref="Texels.Decode"/> gives it.
    /// </summary>
    /// <param name="level">The level, 0 for the picture itself.</param>
    public byte[] Decode(int level = 0)
    {
        var (width, height) = LevelSize(level);
        return Texels.Decode(Format, Levels[level], width, height);
    }

    /// <summary>Reads a KTX2 file from its bytes.</summary>
    /// <param name="file">The whole file.</param>
    /// <exception cref="InvalidDataException">The bytes are not a KTX2 file this reads, with the
    /// reason.</exception>
    public static Ktx2 Read(ReadOnlySpan<byte> file)
    {
        if (file.Length < 80 || !file[..12].SequenceEqual(Identifier))
            throw new InvalidDataException("The bytes do not begin with KTX2's identifier and its 80 byte header.");

        uint vkFormat = U32(file, 12);
        uint width = U32(file, 20), height = U32(file, 24), depth = U32(file, 28);
        uint layers = U32(file, 32), faces = U32(file, 36), levelCount = U32(file, 40);
        uint scheme = U32(file, 44);

        var (format, colorSpace) = FormatOf(vkFormat);
        if (scheme != 0)
            throw new InvalidDataException($"The file's levels are supercompressed (scheme {scheme}), and this reads levels kept as they are, scheme 0, since .NET's library holds no zstd.");
        if (depth > 0 || layers > 1 || faces != 1)
            throw new InvalidDataException($"The file holds a volume, an array or a cube (depth {depth}, layers {layers}, faces {faces}), and this reads one flat picture.");
        if (width < 1 || height < 1 || width > 1 << 16 || height > 1 << 16)
            throw new InvalidDataException($"The file's picture is {width} by {height}, where a side of 1 to 65536 texels belongs.");

        // A count of none asks a reader to make the levels below the one the file holds. The count
        // is compared as it is written, before it is cast, so no count turns negative.
        uint most = 1 + (uint)System.Numerics.BitOperations.Log2(Math.Max(width, height));
        if (levelCount > most)
            throw new InvalidDataException($"The file names {levelCount} levels, where a {width} by {height} picture holds {most}.");
        int count = (int)Math.Max(levelCount, 1);
        if (80 + 24L * count > file.Length)
            throw new InvalidDataException($"The file's index of {count} levels runs past the end of the file.");

        var levels = new byte[count][];
        for (int level = 0; level < count; level++)
        {
            ulong offset = U64(file, 80 + 24 * level), length = U64(file, 88 + 24 * level);
            int w = Math.Max(1, (int)width >> level), h = Math.Max(1, (int)height >> level);
            long expected = Texels.ByteCount(format, w, h);
            if ((long)length != expected)
                throw new InvalidDataException($"Level {level} holds {length} bytes, where a {w} by {h} level in {format} holds {expected}.");
            if (offset > (ulong)file.Length || length > (ulong)file.Length - offset)
                throw new InvalidDataException($"Level {level} runs past the end of the file.");
            levels[level] = file.Slice((int)offset, (int)length).ToArray();
        }

        return new Ktx2(format, colorSpace, (int)width, (int)height, levels);
    }

    // The Vulkan formats read, by their numbers in VkFormat.
    private static (PictureFormat, ColorSpace) FormatOf(uint vkFormat) => vkFormat switch
    {
        37 => (PictureFormat.Rgba8, ColorSpace.Linear),
        43 => (PictureFormat.Rgba8, ColorSpace.Srgb),
        131 => (PictureFormat.Bc1Rgb, ColorSpace.Linear),
        132 => (PictureFormat.Bc1Rgb, ColorSpace.Srgb),
        133 => (PictureFormat.Bc1, ColorSpace.Linear),
        134 => (PictureFormat.Bc1, ColorSpace.Srgb),
        139 => (PictureFormat.Bc4, ColorSpace.Linear),
        141 => (PictureFormat.Bc5, ColorSpace.Linear),
        145 => (PictureFormat.Bc7, ColorSpace.Linear),
        146 => (PictureFormat.Bc7, ColorSpace.Srgb),
        0 => throw new InvalidDataException("The file's format is undefined, as a Basis Universal payload's is, which this does not transcode."),
        _ => throw new InvalidDataException($"The file's Vulkan format is {vkFormat}, and this reads RGBA8, BC1, BC4, BC5 and BC7 (37, 43, 131 to 134, 139, 141, 145 and 146)."),
    };

    private static uint U32(ReadOnlySpan<byte> file, int at) => BinaryPrimitives.ReadUInt32LittleEndian(file[at..]);

    private static ulong U64(ReadOnlySpan<byte> file, int at) => BinaryPrimitives.ReadUInt64LittleEndian(file[at..]);
}
