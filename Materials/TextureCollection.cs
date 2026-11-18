using System.Numerics;
using System.Text.Json;

namespace Materials;

/// <summary>
/// What an entry of a texture collection is drawn as, the recipe it was built by.
/// </summary>
public enum TextureKind
{
    /// <summary>A tile that repeats, an entry the manifest gives no kind.</summary>
    Tile,

    /// <summary>
    /// A bark that tiles round a stem and along it, with a depth for parallax occlusion.
    /// </summary>
    Bark,

    /// <summary>An atlas of leaves cut out over nothing.</summary>
    Leaves,

    /// <summary>
    /// A card of grass blades cut out over nothing, their roots along its bottom edge.
    /// </summary>
    Grass,
}

/// <summary>
/// An entry of a texture collection, its pictures and what the build wrote of it into the
/// manifest.
/// </summary>
/// <param name="Name">The entry's name, which its folder is named by.</param>
/// <param name="Kind">The recipe it was built by.</param>
/// <param name="Albedo">The color and opacity, BC7 in sRGB.</param>
/// <param name="Normal">The tangent-space normal's X and Y, BC5.</param>
/// <param name="Surface">The occlusion, roughness, metallic and translucency, BC7.</param>
public sealed record TextureEntry(string Name, TextureKind Kind, MaterialPicture Albedo, MaterialPicture Normal, MaterialPicture Surface)
{
    /// <summary>A bark's depth for parallax occlusion, BC4, or nothing for another kind.</summary>
    public MaterialPicture? Depth { get; init; }

    /// <summary>
    /// The albedo's width and height in texels, or nothing where the manifest does not say.
    /// </summary>
    public (int Width, int Height)? Size { get; init; }

    /// <summary>
    /// The mean color of the albedo's opaque texels in linear numbers, or nothing where the
    /// manifest does not say.
    /// </summary>
    public Vector3? MeanColor { get; init; }

    /// <summary>
    /// The mean occlusion, roughness, metallic and translucency over the albedo's opaque texels, or
    /// nothing where the manifest does not say.
    /// </summary>
    public Vector4? MeanSurface { get; init; }

    /// <summary>
    /// Where each leaf of a leaf atlas is, the share of the atlas's width and height from its left
    /// and top and the share it spans, or none where the manifest gives none.
    /// </summary>
    public IReadOnlyList<(float U, float V, float Width, float Height)> LeafBoxes { get; init; } = [];

    /// <summary>
    /// A tile's repeat in meters across and down, or nothing where the manifest does not say.
    /// </summary>
    public (float Across, float Down)? Meters { get; init; }

    /// <summary>
    /// Whether the manifest marks the entry waiting, its files in the collection's waiting folder.
    /// </summary>
    public bool Waiting { get; init; }

    /// <summary>
    /// The material the entry stands for, its factors one so its pictures carry the values, masked
    /// where its kind is cut out.
    /// </summary>
    public MaterialDescription Description() => new()
    {
        Metallic = 1f,
        Roughness = 1f,
        DiffuseTransmission = 1f,
        AlphaMode = Kind is TextureKind.Leaves or TextureKind.Grass ? AlphaMode.Mask : AlphaMode.Opaque,
        Pictures = Depth is { } depth ? [Albedo, Normal, Surface, depth] : [Albedo, Normal, Surface],
    };
}

/// <summary>
/// A texture collection's manifest read, the <c>textures.json</c> scripts/textures.py builds a
/// collection by and writes back into, or the listing it writes beside an entry built elsewhere.
/// </summary>
/// <remarks>
/// An entry's files stand in a folder of its name beside the manifest, or in the manifest's
/// <c>waiting</c> folder where it is marked waiting, and a bark has its depth beside them, which
/// the build always writes. The paths are given, not looked for, so the engine checks the files as
/// it reads them.
/// </remarks>
public static class TextureCollection
{
    /// <summary>The entries of a manifest, in its order.</summary>
    /// <param name="json">The manifest's text.</param>
    /// <param name="path">The manifest's path among the engine's files, its entries' folders beside
    /// it.</param>
    /// <exception cref="InvalidDataException">The text is no manifest, or an entry breaks its form,
    /// with the reason.</exception>
    public static IReadOnlyList<TextureEntry> Read(string json, string path)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(path);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException fault)
        {
            throw new InvalidDataException($"{path} is no JSON ({fault.Message}).", fault);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"{path} holds no entries.");
            var folder = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
            return [.. entries.EnumerateArray().Select(entry => Entry(entry, folder, path))];
        }
    }

    private static TextureEntry Entry(JsonElement entry, string folder, string path)
    {
        if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("name", out var named) || named.GetString() is not { Length: > 0 } name)
            throw new InvalidDataException($"{path} names an entry with no name.");
        var kind = !entry.TryGetProperty("kind", out var kindOf) ? TextureKind.Tile : kindOf.GetString() switch
        {
            "bark" => TextureKind.Bark,
            "leaves" => TextureKind.Leaves,
            "grass" => TextureKind.Grass,
            var other => throw new InvalidDataException($"{path} gives the entry {name} the kind {other}, where bark, leaves or grass or none belongs."),
        };

        var waiting = entry.TryGetProperty("waiting", out var marked) && marked.ValueKind == JsonValueKind.True;
        var at = string.Join('/', new[] { folder, waiting ? "waiting" : "", name }.Where(part => part.Length > 0));
        float[]? Numbers(string field, int count) =>
            !entry.TryGetProperty(field, out var value) ? null
            : value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == count && value.EnumerateArray().All(n => n.ValueKind == JsonValueKind.Number)
                ? [.. value.EnumerateArray().Select(n => n.GetSingle())]
                : throw new InvalidDataException($"{path} gives the entry {name} its {field} as no array of {count} numbers.");

        var size = Numbers("size", 2);
        var color = Numbers("color", 3);
        var surface = Numbers("surface", 4);
        var meters = entry.TryGetProperty("meters", out var m) && m.ValueKind == JsonValueKind.Null ? null : Numbers("meters", 2);
        var boxes = new List<(float, float, float, float)>();
        if (entry.TryGetProperty("leaves", out var leaves))
            foreach (var box in leaves.EnumerateArray())
            {
                if (box.ValueKind != JsonValueKind.Array || box.GetArrayLength() != 4)
                    throw new InvalidDataException($"{path} gives the entry {name} a leaf box that is no four numbers.");
                boxes.Add((box[0].GetSingle(), box[1].GetSingle(), box[2].GetSingle(), box[3].GetSingle()));
            }

        return new TextureEntry(
            name, kind,
            new MaterialPicture($"{at}/albedo.ktx2", PictureLayout.Color),
            new MaterialPicture($"{at}/normal.ktx2", PictureLayout.Normal),
            new MaterialPicture($"{at}/surface.ktx2", PictureLayout.Surface))
        {
            Depth = kind == TextureKind.Bark ? new MaterialPicture($"{at}/depth.ktx2", PictureLayout.Depth) : null,
            Size = size is null ? null : ((int)size[0], (int)size[1]),
            MeanColor = color is null ? null : new Vector3(Srgb.Decode(color[0]), Srgb.Decode(color[1]), Srgb.Decode(color[2])),
            MeanSurface = surface is null ? null : new Vector4(surface[0], surface[1], surface[2], surface[3]),
            LeafBoxes = boxes,
            Meters = meters is null ? null : (meters[0], meters[1]),
            Waiting = waiting,
        };
    }
}
