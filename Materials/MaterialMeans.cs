using System.Numerics;

namespace Materials;

/// <summary>
/// A material's means over its surface, which a far card, a picture of many surfaces at once, or a
/// field of the scene's light is painted with where the pictures themselves are too fine to read.
/// </summary>
/// <param name="Color">The base color's mean in linear numbers, over the texels the opacity
/// covers.</param>
/// <param name="Opacity">The share of the surface the opacity covers, one for an opaque
/// material.</param>
/// <param name="Occlusion">The occlusion's mean as the material applies it, one for none.</param>
/// <param name="Roughness">The perceptual roughness's mean.</param>
/// <param name="Metallic">The metallic's mean.</param>
/// <param name="DiffuseTransmission">The diffuse transmission's mean.</param>
public readonly record struct MaterialMeans(Vector3 Color, float Opacity, float Occlusion, float Roughness, float Metallic, float DiffuseTransmission)
{
    /// <summary>
    /// The longest side of the level the means are read from, the first level of each picture that
    /// fits it, so a picture's cost is a few thousand texels whatever its size.
    /// </summary>
    public const int LongestSide = 64;

    /// <summary>The means of a material, its pictures read through the engine's files.</summary>
    /// <remarks>
    /// <para>
    /// A texel's weight is its opacity as the alpha mode reads it, one throughout for an opaque
    /// material, one or none by the cutoff for a masked one and the opacity itself for a blended
    /// one, times the base color's W, so the color and the surface are means over what is seen. A
    /// picture of another size than the opacity's reads the opacity at the nearest of its texels.
    /// Where no texel has any weight the means are taken over every texel.
    /// </para>
    /// <para>
    /// The color is averaged in linear light, each texel's sRGB decoded first, since a mean of
    /// encoded numbers is darker than the light the surface sends back. A one-channel role is its
    /// picture's mean times its factor, the occlusion moved toward one by
    /// <see cref="MaterialDescription.OcclusionStrength"/> as glTF does.
    /// </para>
    /// </remarks>
    /// <param name="material">The material.</param>
    /// <param name="files">
    /// The engine's files, which a KTX2 picture is read from and any other decoded by.
    /// </param>
    /// <exception cref="ArgumentException">The material breaks a rule, with every one it
    /// breaks.</exception>
    /// <exception cref="InvalidDataException"> A picture is neither a KTX2 file the module reads
    /// nor one <see cref="IMaterialFiles.Decode"/> decodes. </exception>
    public static MaterialMeans Of(MaterialDescription material, IMaterialFiles files)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(files);
        if (material.Problems() is { Count: > 0 } problems)
            throw new ArgumentException(string.Join(" ", problems), nameof(material));

        var pictures = new Dictionary<string, Level>();
        Level? Read(PictureRole role)
        {
            if (material.PictureOf(role) is not { } held) return null;
            if (!pictures.TryGetValue(held.Picture.Path, out var texels))
                pictures[held.Picture.Path] = texels = Load(held.Picture.Path, files);

            return texels with { Channel = held.Channel };
        }

        var opacity = Read(PictureRole.Opacity);
        float Weight(float u, float v)
        {
            if (material.AlphaMode == AlphaMode.Opaque) return 1f;
            var alpha = material.BaseColor.W * (opacity is { } o ? o.At(u, v) : 1f);
            return material.AlphaMode == AlphaMode.Mask ? (alpha >= material.AlphaCutoff ? 1f : 0f) : alpha;
        }

        // The share covered, over the opacity's own texels, or the factor alone where none holds
        // it.
        float covered;
        if (material.AlphaMode == AlphaMode.Opaque) covered = 1f;
        else if (opacity is { } shown)
        {
            double sum = 0;
            for (int y = 0; y < shown.Height; y++)
            for (int x = 0; x < shown.Width; x++)
                sum += Weight((x + 0.5f) / shown.Width, (y + 0.5f) / shown.Height);
            covered = (float)(sum / (shown.Width * shown.Height));
        }
        else covered = Weight(0.5f, 0.5f);

        Vector3 color = new(material.BaseColor.X, material.BaseColor.Y, material.BaseColor.Z);
        if (Read(PictureRole.BaseColor) is { } picture)
        {
            Vector3 sum = Vector3.Zero;
            double weights = 0;
            foreach (var (x, y, w) in Texelwise(picture, Weight))
            {
                sum += (float)w * new Vector3(Srgb.Decode(picture.Byte(x, y, 0) / 255f), Srgb.Decode(picture.Byte(x, y, 1) / 255f), Srgb.Decode(picture.Byte(x, y, 2) / 255f));
                weights += w;
            }

            color *= sum / (float)weights;
        }

        float Mean(PictureRole role, float factor)
        {
            if (Read(role) is not { } held) return factor;
            double sum = 0, weights = 0;
            foreach (var (x, y, w) in Texelwise(held, Weight))
            {
                sum += w * held.Byte(x, y, held.Channel) / 255.0;
                weights += w;
            }

            return factor * (float)(sum / weights);
        }

        var occlusion = 1f + material.OcclusionStrength * (Mean(PictureRole.Occlusion, 1f) - 1f);
        return new MaterialMeans(color, covered, occlusion, Mean(PictureRole.Roughness, material.Roughness), Mean(PictureRole.Metallic, material.Metallic), Mean(PictureRole.DiffuseTransmission, material.DiffuseTransmission));
    }

    // A picture's level to read, a KTX2 file's first that fits LongestSide, or the whole picture as
    // the engine decodes a file the module does not read.
    private static Level Load(string path, IMaterialFiles files)
    {
        var bytes = files.ReadAllBytes(path);
        Ktx2 file;
        try
        {
            file = Ktx2.Read(bytes);
        }
        catch (InvalidDataException refused)
        {
            if (files.Decode(path, bytes) is not { } decoded)
                throw new InvalidDataException($"{path} is no KTX2 file the module reads ({refused.Message}), and the engine's files decode none of it.", refused);
            if (decoded.Width < 1 || decoded.Height < 1 || decoded.Rgba.Length != 4L * decoded.Width * decoded.Height)
                throw new InvalidDataException($"{path} decodes to {decoded.Rgba.Length} bytes for {decoded.Width} by {decoded.Height} texels, where four bytes a texel belong.");
            return new Level(decoded.Rgba, decoded.Width, decoded.Height);
        }

        int level = 0;
        while (level + 1 < file.Levels.Count && Math.Max(file.LevelSize(level).Width, file.LevelSize(level).Height) > LongestSide)
            level++;
        var (width, height) = file.LevelSize(level);
        return new Level(file.Decode(level), width, height);
    }

    // Each texel of a picture with its weight, every texel at a weight of one where none has any.
    private static IEnumerable<(int X, int Y, double Weight)> Texelwise(Level picture, Func<float, float, float> weight)
    {
        var weights = new double[picture.Width * picture.Height];
        double total = 0;
        for (int y = 0; y < picture.Height; y++)
        for (int x = 0; x < picture.Width; x++)
            total += weights[y * picture.Width + x] = weight((x + 0.5f) / picture.Width, (y + 0.5f) / picture.Height);
        for (int y = 0; y < picture.Height; y++)
        for (int x = 0; x < picture.Width; x++)
            yield return (x, y, total > 0 ? weights[y * picture.Width + x] : 1.0);
    }

    // A decoded level, four bytes a texel, and the channel a role stands in.
    private readonly record struct Level(byte[] Rgba, int Width, int Height, int Channel = 0)
    {
        public byte Byte(int x, int y, int channel) => Rgba[(y * Width + x) * 4 + channel];

        // The channel at the texel nearest a place of the picture, from zero to one.
        public float At(float u, float v)
        {
            int x = Math.Clamp((int)(u * Width), 0, Width - 1), y = Math.Clamp((int)(v * Height), 0, Height - 1);
            return Byte(x, y, Channel) / 255f;
        }
    }
}
