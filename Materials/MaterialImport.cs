using System.Numerics;
using System.Text.Json;

namespace Materials;

/// <summary>
/// The materials of a glTF 2.0 file read into descriptions, its factors, its alpha mode and its
/// textures by the roles glTF gives them, with the extensions the description holds.
/// </summary>
/// <remarks>
/// <para>
/// A factor the file leaves out takes glTF's default, a metallic and a roughness of one among them,
/// which differ from a description of nothing's. The emitted light is glTF's emissive factor times
/// <c>KHR_materials_emissive_strength</c>, kept as <see cref="EmissiveUnit.Unitless"/>, the
/// reflectance comes from <c>KHR_materials_ior</c>, and the diffuse transmission and its texture's
/// alpha from <c>KHR_materials_diffuse_transmission</c>.
/// </para>
/// <para>
/// A texture's image is a picture at its URI joined to the file's folder, unescaped. Two roles of
/// one image are one picture where their channels do not meet, as an occlusion and a
/// metallic-roughness texture packed in one image are, and two pictures otherwise. An image held in
/// a data URI or a buffer has no path, so its role has no picture, and every texture is read from
/// the first set of texture coordinates, a texture transform not being held.
/// </para>
/// </remarks>
public static class MaterialImport
{
    /// <summary>Every material of a glTF file, in the order of its <c>materials</c>.</summary>
    /// <param name="json">The glTF file's JSON, a binary file's JSON chunk for a GLB.</param>
    /// <param name="path">The file's path among the engine's files, its images' URIs joined to its
    /// folder.</param>
    /// <exception cref="InvalidDataException">The JSON is no glTF a material reads from, or a
    /// material breaks a rule, with the reason.</exception>
    public static IReadOnlyList<MaterialDescription> FromGltf(string json, string path)
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
            var root = document.RootElement;
            if (!root.TryGetProperty("materials", out var materials)) return [];
            if (materials.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"{path} gives its materials as no array.");
            return [.. materials.EnumerateArray().Select((material, index) => FromGltf(root, index, path))];
        }
    }

    /// <summary>A material of a glTF file already parsed, by its index.</summary>
    /// <param name="root">The glTF file's root object.</param>
    /// <param name="index">The material's index in its <c>materials</c>.</param>
    /// <param name="path">The file's path among the engine's files, its images' URIs joined to its
    /// folder.</param>
    /// <exception cref="InvalidDataException">The file holds no such material, or it breaks a rule,
    /// with the reason.</exception>
    public static MaterialDescription FromGltf(JsonElement root, int index, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!root.TryGetProperty("materials", out var materials) || materials.ValueKind != JsonValueKind.Array || index < 0 || index >= materials.GetArrayLength())
            throw new InvalidDataException($"{path} holds no material {index}.");
        var material = materials[index];
        var folder = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
        string Where(string what) => $"{path}'s material {index} {what}";

        var pbr = material.TryGetProperty("pbrMetallicRoughness", out var given) && given.ValueKind == JsonValueKind.Object ? given : default;
        var color = Numbers(pbr, "baseColorFactor", [1f, 1f, 1f, 1f], Where);
        var emissive = Numbers(material, "emissiveFactor", [0f, 0f, 0f], Where);
        var extensions = material.TryGetProperty("extensions", out var found) && found.ValueKind == JsonValueKind.Object ? found : default;
        var strength = Number(Extension(extensions, "KHR_materials_emissive_strength"), "emissiveStrength", 1f, Where);
        var iorExtension = Extension(extensions, "KHR_materials_ior");
        var ior = Number(iorExtension, "ior", 1.5f, Where);
        var transmission = Extension(extensions, "KHR_materials_diffuse_transmission");

        var roles = new List<(string Path, PictureLayout Layout)>();
        void Take(JsonElement owner, string property, PictureLayout layout)
        {
            if (Image(root, owner, property, folder, Where) is { } image) roles.Add((image, layout));
        }

        Take(pbr, "baseColorTexture", PictureLayout.Color);
        Take(pbr, "metallicRoughnessTexture", PictureLayout.MetallicRoughness);
        Take(material, "normalTexture", PictureLayout.Normal);
        Take(material, "occlusionTexture", PictureLayout.Occlusion);
        Take(material, "emissiveTexture", PictureLayout.Emissive);
        Take(transmission, "diffuseTransmissionTexture", new PictureLayout(null, null, null, PictureRole.DiffuseTransmission));

        var alpha = material.TryGetProperty("alphaMode", out var mode) ? mode.GetString() : "OPAQUE";
        var description = new MaterialDescription
        {
            BaseColor = new Vector4(color[0], color[1], color[2], color[3]),
            Metallic = Number(pbr, "metallicFactor", 1f, Where),
            Roughness = Number(pbr, "roughnessFactor", 1f, Where),
            // Without the extension the reflectance is the dielectric's own, which an ior of 1.5
            // gives but for the float's last place.
            Reflectance = iorExtension.ValueKind != JsonValueKind.Object ? Reflectance.Dielectric
                : ior >= 1f ? Reflectance.FromIor(ior)
                : throw new InvalidDataException(Where($"gives an ior of {ior}, where one or more belongs.")),
            Emissive = new Vector3(emissive[0], emissive[1], emissive[2]) * strength,
            EmissiveUnit = EmissiveUnit.Unitless,
            NormalScale = Number(Object(material, "normalTexture"), "scale", 1f, Where),
            OcclusionStrength = Number(Object(material, "occlusionTexture"), "strength", 1f, Where),
            DiffuseTransmission = Number(transmission, "diffuseTransmissionFactor", 0f, Where),
            AlphaMode = alpha switch
            {
                "OPAQUE" => AlphaMode.Opaque,
                "MASK" => AlphaMode.Mask,
                "BLEND" => AlphaMode.Blend,
                _ => throw new InvalidDataException(Where($"gives the alphaMode {alpha}, where OPAQUE, MASK or BLEND belongs.")),
            },
            AlphaCutoff = Number(material, "alphaCutoff", 0.5f, Where),
            DoubleSided = material.TryGetProperty("doubleSided", out var sides) && sides.ValueKind == JsonValueKind.True,
            Pictures = Merged(roles),
        };

        if (description.Problems() is { Count: > 0 } problems) throw new InvalidDataException(Where("breaks a rule. " + string.Join(" ", problems)));
        return description;
    }

    // The roles of each image as one picture where their channels do not meet, in the order the
    // images were first taken.
    private static List<MaterialPicture> Merged(List<(string Path, PictureLayout Layout)> roles)
    {
        var pictures = new List<MaterialPicture>();
        foreach (var (path, layout) in roles)
        {
            var at = pictures.FindIndex(picture => picture.Path == path && Joined(picture.Layout, layout) is not null);
            if (at < 0) pictures.Add(new MaterialPicture(path, layout));
            else pictures[at] = new MaterialPicture(path, Joined(pictures[at].Layout, layout)!);
        }

        return pictures;
    }

    // Two layouts as one where no channel holds a role in both and the roles together keep the
    // layout's rules, or nothing.
    private static PictureLayout? Joined(PictureLayout first, PictureLayout second)
    {
        PictureRole?[] a = [first.R, first.G, first.B, first.A], b = [second.R, second.G, second.B, second.A];
        if (Enumerable.Range(0, 4).Any(c => a[c] is not null && b[c] is not null)) return null;
        try
        {
            return new PictureLayout(a[0] ?? b[0], a[1] ?? b[1], a[2] ?? b[2], a[3] ?? b[3]);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // A texture's image as a path joined to the folder, or nothing where the image has no URI of a
    // file.
    private static string? Image(JsonElement root, JsonElement owner, string property, string folder, Func<string, string> where)
    {
        if (owner.ValueKind != JsonValueKind.Object || !owner.TryGetProperty(property, out var info)) return null;
        if (!info.TryGetProperty("index", out var at) || !at.TryGetInt32(out var texture)) throw new InvalidDataException(where($"gives {property} no index."));
        if (!root.TryGetProperty("textures", out var textures) || texture < 0 || texture >= textures.GetArrayLength())
            throw new InvalidDataException(where($"names texture {texture}, which the file has none of."));
        if (!textures[texture].TryGetProperty("source", out var source) || !source.TryGetInt32(out var image)) return null;
        if (!root.TryGetProperty("images", out var images) || image < 0 || image >= images.GetArrayLength())
            throw new InvalidDataException(where($"names image {image}, which the file has none of."));
        if (!images[image].TryGetProperty("uri", out var uri) || uri.GetString() is not { Length: > 0 } text) return null;
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;
        return Joined(folder, Uri.UnescapeDataString(text));
    }

    // A path joined to a folder, its parts by /, a part of .. taking the one before it away, and a
    // rooted one kept as it is.
    private static string Joined(string folder, string relative)
    {
        if (relative.StartsWith('/') || Path.IsPathRooted(relative)) return relative.Replace('\\', '/');
        var parts = new List<string>(folder.Length == 0 ? [] : folder.Split('/'));
        foreach (var part in relative.Replace('\\', '/').Split('/'))
            if (part == "..") { if (parts.Count > 0 && parts[^1] != "..") parts.RemoveAt(parts.Count - 1); else parts.Add(part); }
            else if (part is not ("." or "")) parts.Add(part);
        return string.Join('/', parts);
    }

    private static JsonElement Extension(JsonElement extensions, string name) =>
        extensions.ValueKind == JsonValueKind.Object && extensions.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.Object ? found : default;

    private static JsonElement Object(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.Object ? found : default;

    private static float Number(JsonElement owner, string name, float otherwise, Func<string, string> where) =>
        owner.ValueKind != JsonValueKind.Object || !owner.TryGetProperty(name, out var value) ? otherwise
        : value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var number) ? number
        : throw new InvalidDataException(where($"gives {name} as no number."));

    private static float[] Numbers(JsonElement owner, string name, float[] otherwise, Func<string, string> where)
    {
        if (owner.ValueKind != JsonValueKind.Object || !owner.TryGetProperty(name, out var value)) return otherwise;
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != otherwise.Length)
            throw new InvalidDataException(where($"gives {name} as no array of {otherwise.Length} numbers."));
        return [.. value.EnumerateArray().Select(n => n.ValueKind == JsonValueKind.Number && n.TryGetSingle(out var x) ? x : throw new InvalidDataException(where($"gives {name} a part that is no number.")))];
    }
}
