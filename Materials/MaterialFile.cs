using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Materials;

/// <summary>
/// A material as a JSON file of its own beside its pictures, so a folder of materials stands
/// without a glTF to carry them.
/// </summary>
/// <remarks>
/// <para>
/// The file is an object whose <c>format</c> is <c>material/1</c>, with each factor of
/// <see cref="MaterialDescription"/> under its name in camel case, a color as an array of numbers,
/// <c>emissiveUnit</c> as <c>luminance</c> or <c>unitless</c>, <c>alphaMode</c> as <c>opaque</c>,
/// <c>mask</c> or <c>blend</c>, and <c>pictures</c> as objects of a <c>path</c> and a
/// <c>layout</c>. A layout is the name of one of <see cref="PictureLayout"/>'s, as <c>color</c> or
/// <c>surface</c>, or an array of four roles in camel case, nothing standing as null. A field left
/// out takes the description's default.
/// </para>
/// <para>
/// A picture's path in the file is relative to the file's folder and joined to it on reading, with
/// <c>/</c> between the parts as the engine's paths have it, so the folder moves whole.
/// </para>
/// </remarks>
public static class MaterialFile
{
    /// <summary>The value of the file's <c>format</c>.</summary>
    public const string Format = "material/1";

    /// <summary>The extension a material file is named with.</summary>
    public const string Extension = ".material.json";

    private static readonly (string Name, PictureLayout Layout)[] Layouts =
    [
        ("color", PictureLayout.Color), ("normal", PictureLayout.Normal), ("surface", PictureLayout.Surface),
        ("metallicRoughness", PictureLayout.MetallicRoughness), ("occlusion", PictureLayout.Occlusion),
        ("emissive", PictureLayout.Emissive), ("depth", PictureLayout.Depth),
    ];

    private static readonly string[] Fields =
    [
        "format", "baseColor", "metallic", "roughness", "reflectance", "emissive", "emissiveUnit", "normalScale",
        "occlusionStrength", "diffuseTransmission", "alphaMode", "alphaCutoff", "doubleSided", "pictures",
    ];

    /// <summary>Reads a material from its file's text.</summary>
    /// <param name="json">The file's text.</param>
    /// <param name="path">The file's own path among the engine's files, its pictures' paths joined
    /// to its folder.</param>
    /// <exception cref="InvalidDataException">The text is no material file, or breaks a rule, with
    /// the reason.</exception>
    public static MaterialDescription Read(string json, string path)
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
            if (root.ValueKind != JsonValueKind.Object) throw Fault(path, "holds no object");
            foreach (var property in root.EnumerateObject())
                if (!Fields.Contains(property.Name)) throw Fault(path, $"has a field {property.Name}, which a material file has none of");
            if (!root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String || format.GetString() != Format)
                throw Fault(path, $"names no format {Format}");

            var folder = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
            var defaults = new MaterialDescription();
            var material = new MaterialDescription
            {
                BaseColor = root.TryGetProperty("baseColor", out var baseColor) ? Vector4Of(baseColor, path, "baseColor") : defaults.BaseColor,
                Metallic = Number(root, "metallic", defaults.Metallic, path),
                Roughness = Number(root, "roughness", defaults.Roughness, path),
                Reflectance = Number(root, "reflectance", defaults.Reflectance, path),
                Emissive = root.TryGetProperty("emissive", out var emissive) ? Vector3Of(emissive, path, "emissive") : defaults.Emissive,
                EmissiveUnit = Word(root, "emissiveUnit", defaults.EmissiveUnit, path),
                NormalScale = Number(root, "normalScale", defaults.NormalScale, path),
                OcclusionStrength = Number(root, "occlusionStrength", defaults.OcclusionStrength, path),
                DiffuseTransmission = Number(root, "diffuseTransmission", defaults.DiffuseTransmission, path),
                AlphaMode = Word(root, "alphaMode", defaults.AlphaMode, path),
                AlphaCutoff = Number(root, "alphaCutoff", defaults.AlphaCutoff, path),
                DoubleSided = root.TryGetProperty("doubleSided", out var sides)
                    ? sides.ValueKind is JsonValueKind.True or JsonValueKind.False ? sides.GetBoolean() : throw Fault(path, "gives doubleSided as no true or false")
                    : defaults.DoubleSided,
                Pictures = root.TryGetProperty("pictures", out var pictures) ? PicturesOf(pictures, folder, path) : [],
            };

            if (material.Problems() is { Count: > 0 } problems) throw Fault(path, "breaks a rule. " + string.Join(" ", problems));
            return material;
        }
    }

    /// <summary>Writes a material as its file's text, each field the description holds.</summary>
    /// <param name="material">The material.</param>
    /// <param name="path"> The path the file is kept at among the engine's files, its pictures'
    /// paths written relative to its folder. </param>
    /// <exception cref="ArgumentException">The material breaks a rule, with every one it
    /// breaks.</exception>
    public static string Write(MaterialDescription material, string path)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(path);
        if (material.Problems() is { Count: > 0 } problems)
            throw new ArgumentException(string.Join(" ", problems), nameof(material));

        var folder = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            Numbers(writer, "baseColor", [material.BaseColor.X, material.BaseColor.Y, material.BaseColor.Z, material.BaseColor.W]);
            writer.WriteNumber("metallic", material.Metallic);
            writer.WriteNumber("roughness", material.Roughness);
            writer.WriteNumber("reflectance", material.Reflectance);
            Numbers(writer, "emissive", [material.Emissive.X, material.Emissive.Y, material.Emissive.Z]);
            writer.WriteString("emissiveUnit", Camel(material.EmissiveUnit.ToString()));
            writer.WriteNumber("normalScale", material.NormalScale);
            writer.WriteNumber("occlusionStrength", material.OcclusionStrength);
            writer.WriteNumber("diffuseTransmission", material.DiffuseTransmission);
            writer.WriteString("alphaMode", Camel(material.AlphaMode.ToString()));
            writer.WriteNumber("alphaCutoff", material.AlphaCutoff);
            writer.WriteBoolean("doubleSided", material.DoubleSided);
            writer.WriteStartArray("pictures");
            foreach (var picture in material.Pictures)
            {
                writer.WriteStartObject();
                writer.WriteString("path", Relative(picture.Path, folder));
                if (Layouts.FirstOrDefault(known => known.Layout == picture.Layout).Name is { } name)
                    writer.WriteString("layout", name);
                else
                {
                    writer.WriteStartArray("layout");
                    foreach (var role in new[] { picture.Layout.R, picture.Layout.G, picture.Layout.B, picture.Layout.A })
                        if (role is { } held) writer.WriteStringValue(Camel(held.ToString()));
                        else writer.WriteNullValue();
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    private static IReadOnlyList<MaterialPicture> PicturesOf(JsonElement pictures, string folder, string path)
    {
        if (pictures.ValueKind != JsonValueKind.Array) throw Fault(path, "gives pictures as no array");
        var list = new List<MaterialPicture>();
        foreach (var picture in pictures.EnumerateArray())
        {
            if (picture.ValueKind != JsonValueKind.Object) throw Fault(path, "gives a picture as no object");
            foreach (var property in picture.EnumerateObject())
                if (property.Name is not ("path" or "layout")) throw Fault(path, $"gives a picture a field {property.Name}, where a picture has a path and a layout");
            if (!picture.TryGetProperty("path", out var at) || at.ValueKind != JsonValueKind.String || at.GetString() is not { Length: > 0 } relative)
                throw Fault(path, "gives a picture no path");
            if (!picture.TryGetProperty("layout", out var layout)) throw Fault(path, $"gives {relative} no layout");
            list.Add(new MaterialPicture(Joined(folder, relative), LayoutOf(layout, path, relative)));
        }

        return list;
    }

    private static PictureLayout LayoutOf(JsonElement layout, string path, string picture)
    {
        if (layout.ValueKind == JsonValueKind.String)
        {
            var name = layout.GetString();
            return Layouts.FirstOrDefault(known => known.Name == name).Layout
                ?? throw Fault(path, $"gives {picture} the layout {name}, where {string.Join(", ", Layouts.Select(known => known.Name))} or four roles belong");
        }

        if (layout.ValueKind != JsonValueKind.Array || layout.GetArrayLength() != 4)
            throw Fault(path, $"gives {picture} a layout that is no name and no array of four roles");
        var roles = layout.EnumerateArray().Select(role => role.ValueKind == JsonValueKind.Null
            ? (PictureRole?)null
            : role.ValueKind == JsonValueKind.String && Enum.TryParse<PictureRole>(role.GetString(), ignoreCase: true, out var known) && Camel(known.ToString()) == role.GetString()
                ? known
                : throw Fault(path, $"gives {picture} the role {role}, which no picture holds")).ToArray();
        try
        {
            return new PictureLayout(roles[0], roles[1], roles[2], roles[3]);
        }
        catch (ArgumentException broken)
        {
            throw Fault(path, $"gives {picture} a layout that breaks a rule. {broken.Message}");
        }
    }

    private static float Number(JsonElement root, string name, float fallback, string path) =>
        !root.TryGetProperty(name, out var value) ? fallback
        : value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var number) ? number
        : throw Fault(path, $"gives {name} as no number");

    private static T Word<T>(JsonElement root, string name, T fallback, string path) where T : struct, Enum
    {
        if (!root.TryGetProperty(name, out var value)) return fallback;
        foreach (var known in Enum.GetValues<T>())
            if (value.ValueKind == JsonValueKind.String && value.GetString() == Camel(known.ToString())) return known;
        throw Fault(path, $"gives {name} as {value}, where {string.Join(", ", Enum.GetValues<T>().Select(known => Camel(known.ToString())))} belong");
    }

    private static Vector4 Vector4Of(JsonElement value, string path, string name) =>
        Numbers(value, 4, path, name) is var n ? new Vector4(n[0], n[1], n[2], n[3]) : default;

    private static Vector3 Vector3Of(JsonElement value, string path, string name) =>
        Numbers(value, 3, path, name) is var n ? new Vector3(n[0], n[1], n[2]) : default;

    private static float[] Numbers(JsonElement value, int count, string path, string name)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != count)
            throw Fault(path, $"gives {name} as no array of {count} numbers");
        return [.. value.EnumerateArray().Select(n => n.ValueKind == JsonValueKind.Number && n.TryGetSingle(out var x) ? x : throw Fault(path, $"gives {name} a part that is no number"))];
    }

    private static void Numbers(Utf8JsonWriter writer, string name, float[] values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values) writer.WriteNumberValue(value);
        writer.WriteEndArray();
    }

    // A path joined to a folder, its parts by /, a part of .. taking the one before it away.
    private static string Joined(string folder, string relative)
    {
        var parts = new List<string>(folder.Length == 0 ? [] : folder.Split('/'));
        foreach (var part in relative.Split('/'))
            if (part == "..") { if (parts.Count > 0 && parts[^1] != "..") parts.RemoveAt(parts.Count - 1); else parts.Add(part); }
            else if (part is not ("." or "")) parts.Add(part);
        return string.Join('/', parts);
    }

    // A path written relative to a folder, climbing out of it by .. where it lies elsewhere.
    private static string Relative(string target, string folder)
    {
        var from = folder.Length == 0 ? [] : folder.Split('/');
        var to = target.Split('/');
        int common = 0;
        while (common < from.Length && common < to.Length - 1 && from[common] == to[common]) common++;
        return string.Join('/', Enumerable.Repeat("..", from.Length - common).Concat(to[common..]));
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static InvalidDataException Fault(string path, string what) => new($"{path} {what}.");
}
