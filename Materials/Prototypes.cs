using System.Numerics;

namespace Materials;

/// <summary>
/// The prototype materials a level is blocked out with, a grid of a meter's cells tinted by a color
/// for each part of a level, so a reader tells what each surface is for before it has textures of
/// its own.
/// </summary>
/// <remarks>
/// <para>
/// A floor and a wall nearly white, a ceiling whiter still, stairs, ramps and the moving parts
/// orange, a door green, a window blue and a hazard red, as sRGB numbers a color picker shows, and
/// any other name white, which shows the grid's own tones. The floors and walls are near white
/// rather than gray, which reads as dull under the sky, and a wall parts from the floor it stands
/// on by the light on each. Every prototype is rough at 0.85 and not metal, so the light shows the
/// shape and a highlight never hides the lines.
/// </para>
/// <para>
/// The grid's face is all but white, so a color shows as itself, and its lines a light gray, so the
/// grid measures a face without darkening it. A face of 0.99 rather than 1 keeps a white face
/// tinted by a color of a whole tenth, as the hazard's 0.9, off a half of a byte, which one
/// system's rounding and another's would part on.
/// </para>
/// </remarks>
public static class Prototypes
{
    /// <summary>
    /// The names of the parts of a level a prototype is for, in the order of the table.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = ["floor", "wall", "ceiling", "stairs", "door", "window", "hazard"];

    /// <summary>Each prototype's color by its name, as sRGB numbers from zero to one.</summary>
    public static IReadOnlyDictionary<string, Vector3> Colors { get; } = new Dictionary<string, Vector3>(StringComparer.Ordinal)
    {
        ["floor"] = new(0.90f, 0.90f, 0.89f),
        ["wall"] = new(0.90f, 0.90f, 0.89f),
        ["ceiling"] = new(0.95f, 0.95f, 0.96f),
        ["stairs"] = new(0.96f, 0.46f, 0.13f),
        ["door"] = new(0.38f, 0.72f, 0.38f),
        ["window"] = new(0.36f, 0.62f, 0.92f),
        ["hazard"] = new(0.90f, 0.22f, 0.16f),
    };

    /// <summary>The grid's face, all but white, as sRGB numbers.</summary>
    public static Vector3 Face { get; } = new(0.99f, 0.99f, 0.99f);

    /// <summary>
    /// The grid's lines a meter and a half meter apart and its mark, as sRGB numbers.
    /// </summary>
    public static Vector3 Line { get; } = new(0.6f, 0.6f, 0.6f);

    /// <summary>The grid's lines a quarter of a meter apart, as sRGB numbers.</summary>
    public static Vector3 Fine { get; } = new(0.82f, 0.82f, 0.82f);

    /// <summary>The roughness of every prototype.</summary>
    public const float Roughness = 0.85f;

    /// <summary>
    /// The pixels a meter of the grid has where a picture is drawn at a surface's size.
    /// </summary>
    public const int PixelsPerMeter = 1024;

    /// <summary>
    /// A prototype's color as sRGB numbers, white for a name the table has none of.
    /// </summary>
    /// <param name="name">The part of a level, as a material slot or a level's material names
    /// it.</param>
    public static Vector3 SrgbColor(string name) => Colors.TryGetValue(name, out var color) ? color : Vector3.One;

    /// <summary>
    /// A prototype's color in linear numbers, the factor its grid is multiplied by.
    /// </summary>
    /// <param name="name">The part of a level.</param>
    public static Vector3 Tint(string name)
    {
        var color = SrgbColor(name);
        return new Vector3(Srgb.Decode(color.X), Srgb.Decode(color.Y), Srgb.Decode(color.Z));
    }

    /// <summary>
    /// A prototype as a material, its tint the base color over the grid's picture, rough and not
    /// metal.
    /// </summary>
    /// <param name="name">The part of a level.</param>
    /// <param name="grid">The path the engine keeps the grid's picture at, untinted.</param>
    public static MaterialDescription Description(string name, string grid) => new()
    {
        BaseColor = new Vector4(Tint(name), 1f),
        Roughness = Roughness,
        Pictures = [new MaterialPicture(grid, PictureLayout.Color)],
    };

    /// <summary>
    /// The grid's cell in its own tones, four bytes a pixel in sRGB, rows from the top.
    /// </summary>
    /// <param name="pixelsPerMeter">How many pixels a side, 16 to 4096.</param>
    public static byte[] Grid(int pixelsPerMeter) => Grid(pixelsPerMeter, Face, Line, Fine);

    /// <summary>
    /// The grid's cell in the tones given, four bytes a pixel in sRGB, rows from the top.
    /// </summary>
    /// <remarks>
    /// A meter's line is a 128th of the cell wide, two pixels at least, half of it on each edge, so
    /// cells side by side make it whole, the half's line a 256th, and a quarter's a 512th, a pixel
    /// at least, each starting half its width before its place, rounded to the nearest pixel, a
    /// half rounded up. A pixel on a meter's or the half's line, or on the mark, is the line's
    /// tone, else one on a quarter's line the fine tone, else the face's. The mark is <c>1m</c> in
    /// letters of five by seven squares, each square a 128th of the cell, two squares in from the
    /// cell's top left inside its line, where a square is a pixel or more.
    /// </remarks>
    /// <param name="pixelsPerMeter">How many pixels a side, 16 to 4096.</param>
    /// <param name="face">The face's tone as sRGB numbers.</param>
    /// <param name="line">The meter's and the half's lines' and the mark's tone.</param>
    /// <param name="fine">The quarters' lines' tone.</param>
    public static byte[] Grid(int pixelsPerMeter, Vector3 face, Vector3 line, Vector3 fine)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pixelsPerMeter, 16);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pixelsPerMeter, 4096);
        var size = pixelsPerMeter;
        var edge = Math.Max(2, size / 128) / 2;

        // Along each axis, 2 where a pixel is on a line of the line's tone and 1 on a fine one.
        var kind = new byte[size];
        for (var at = 0; at < size; at++)
            if (at < edge || at >= size - edge) kind[at] = 2;

        void Mark(double place, int width, byte which)
        {
            var start = (int)Math.Round(place - width / 2.0, MidpointRounding.AwayFromZero);
            for (var at = start; at < start + width; at++) kind[at] = Math.Max(kind[at], which);
        }

        Mark(size / 2.0, Math.Max(1, size / 256), 2);
        Mark(size / 4.0, Math.Max(1, size / 512), 1);
        Mark(size * 3 / 4.0, Math.Max(1, size / 512), 1);

        byte[] faceBytes = Bytes(face), lineBytes = Bytes(line), fineBytes = Bytes(fine);
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var which = Math.Max(kind[x], kind[y]);
            (which == 2 ? lineBytes : which == 1 ? fineBytes : faceBytes).CopyTo(pixels, (y * size + x) * 4);
        }

        var square = size / 128;
        if (square >= 1)
        {
            var (left, top) = (edge + 2 * square, edge + 2 * square);
            foreach (var (glyph, column) in new[] { (One, 0), (Em, 6) })
                for (var row = 0; row < 7; row++)
                for (var bit = 0; bit < 5; bit++)
                {
                    if ((glyph[row] >> (4 - bit) & 1) == 0) continue;
                    for (var dy = 0; dy < square; dy++)
                    for (var dx = 0; dx < square; dx++)
                        lineBytes.CopyTo(pixels, ((top + row * square + dy) * size + left + (column + bit) * square + dx) * 4);
                }
        }

        return pixels;
    }

    /// <summary>
    /// A prototype's picture as a level editor shows it, the grid tinted by the prototype's color
    /// as its material multiplies its texture, four bytes a pixel in sRGB, rows from the top.
    /// </summary>
    /// <param name="name">The part of a level.</param>
    /// <param name="pixelsPerMeter">How many pixels a side, 16 to 4096.</param>
    public static byte[] Picture(string name, int pixelsPerMeter) => Picture(SrgbColor(name), pixelsPerMeter, Face, Line, Fine);

    /// <summary>
    /// The grid in the tones given tinted by a color, each pixel decoded from sRGB, multiplied by
    /// the color in linear light and encoded again, as a material samples and multiplies its
    /// texture.
    /// </summary>
    /// <param name="color">The tint as sRGB numbers.</param>
    /// <param name="pixelsPerMeter">How many pixels a side, 16 to 4096.</param>
    /// <param name="face">The face's tone as sRGB numbers.</param>
    /// <param name="line">The meter's and the half's lines' and the mark's tone.</param>
    /// <param name="fine">The quarters' lines' tone.</param>
    public static byte[] Picture(Vector3 color, int pixelsPerMeter, Vector3 face, Vector3 line, Vector3 fine)
    {
        var pixels = Grid(pixelsPerMeter, face, line, fine);
        float[] tints = [Srgb.Decode(color.X), Srgb.Decode(color.Y), Srgb.Decode(color.Z)];
        var tables = new byte[3][];
        for (var channel = 0; channel < 3; channel++)
        {
            tables[channel] = new byte[256];
            for (var value = 0; value < 256; value++)
                tables[channel][value] = Srgb.Byte(Srgb.Encode(Srgb.Decode(value / 255f) * tints[channel]));
        }

        for (var at = 0; at < pixels.Length; at += 4)
        for (var channel = 0; channel < 3; channel++)
            pixels[at + channel] = tables[channel][pixels[at + channel]];
        return pixels;
    }

    // A tone as the grid's bytes, through linear light and back as a material's color is.
    private static byte[] Bytes(Vector3 tone) =>
        [Srgb.Byte(Srgb.Encode(Srgb.Decode(tone.X))), Srgb.Byte(Srgb.Encode(Srgb.Decode(tone.Y))), Srgb.Byte(Srgb.Encode(Srgb.Decode(tone.Z))), 255];

    // The mark's letters, a 1 and an m, five squares a row, the leftmost square the row's highest
    // bit.
    private static readonly byte[] One = [0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110];
    private static readonly byte[] Em = [0b00000, 0b00000, 0b11010, 0b10101, 0b10101, 0b10101, 0b10101];
}
