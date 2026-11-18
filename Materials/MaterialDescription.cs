using System.Numerics;

namespace Materials;

/// <summary>
/// What a surface is, in glTF 2.0's metallic-roughness terms and its extensions', each factor and
/// the picture that scales it, so an engine of any shading model reads the quantities it names.
/// </summary>
/// <remarks>
/// <para>
/// A factor multiplies the channel its role's picture holds, as glTF's factors do, and stands alone
/// where no picture holds the role. A color in numbers is linear, the BT.709 primaries with a D65
/// white and no transfer curve, where a picture of a color is sRGB. The emitted light is in
/// candelas a square meter, so an engine whose light has other units scales it once.
/// </para>
/// <para>
/// Where nothing is given the surface is a rough white dielectric, a metallic of zero and a
/// roughness of one, reflecting <see cref="Materials.Reflectance.Dielectric"/> head on.
/// </para>
/// </remarks>
public sealed record MaterialDescription
{
    /// <summary>The base color in linear numbers from zero to one, with the opacity in W.</summary>
    public Vector4 BaseColor { get; init; } = Vector4.One;

    /// <summary>How metal the surface is, from zero to one.</summary>
    public float Metallic { get; init; }

    /// <summary>
    /// glTF's perceptual roughness from zero to one, whose square is GGX's alpha.
    /// </summary>
    public float Roughness { get; init; } = 1f;

    /// <summary>
    /// The share of light a dielectric reflects at normal incidence, from zero to below one, as
    /// <see cref="Materials.Reflectance"/> converts it, a metal taking its reflectance from its
    /// base color instead.
    /// </summary>
    public float Reflectance { get; init; } = Materials.Reflectance.Dielectric;

    /// <summary>The light the surface emits, linear RGB in candelas a square meter.</summary>
    public Vector3 Emissive { get; init; }

    /// <summary>
    /// How far the normal map tilts the surface, glTF's scale, one as the map gives it.
    /// </summary>
    public float NormalScale { get; init; } = 1f;

    /// <summary>
    /// How much the occlusion map darkens the light from all around, from zero for none to one for
    /// all it says.
    /// </summary>
    public float OcclusionStrength { get; init; } = 1f;

    /// <summary>
    /// The share of light let through the surface and scattered, from zero to one, as glTF's
    /// <c>KHR_materials_diffuse_transmission</c> names it.
    /// </summary>
    public float DiffuseTransmission { get; init; }

    /// <summary>How the opacity is read.</summary>
    public AlphaMode AlphaMode { get; init; } = AlphaMode.Opaque;

    /// <summary>
    /// The opacity a texel reaches to be drawn under <see cref="AlphaMode.Mask"/>.
    /// </summary>
    public float AlphaCutoff { get; init; } = 0.5f;

    /// <summary>Whether the surface is seen from behind as well as from the front.</summary>
    public bool DoubleSided { get; init; }

    /// <summary>The pictures that scale the factors, no role held by two of them.</summary>
    public IReadOnlyList<MaterialPicture> Pictures { get; init; } = [];

    /// <summary>
    /// The picture that holds a role and the channel the role stands in, or nothing where none
    /// does.
    /// </summary>
    /// <param name="role">The role.</param>
    public (MaterialPicture Picture, int Channel)? PictureOf(PictureRole role)
    {
        foreach (var picture in Pictures)
            if (picture.Layout.ChannelOf(role) is { } channel)
                return (picture, channel);
        return null;
    }

    /// <summary>Each rule the description breaks, a sentence each, or none.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        void Within(string name, float value, float low, float high)
        {
            if (!(value >= low && value <= high)) problems.Add($"{name} is {value}, where {low} to {high} belongs.");
        }

        Within("BaseColor.X", BaseColor.X, 0, 1);
        Within("BaseColor.Y", BaseColor.Y, 0, 1);
        Within("BaseColor.Z", BaseColor.Z, 0, 1);
        Within("BaseColor.W", BaseColor.W, 0, 1);
        Within(nameof(Metallic), Metallic, 0, 1);
        Within(nameof(Roughness), Roughness, 0, 1);
        Within(nameof(Reflectance), Reflectance, 0, 0.999f);
        Within(nameof(OcclusionStrength), OcclusionStrength, 0, 1);
        Within(nameof(DiffuseTransmission), DiffuseTransmission, 0, 1);
        Within(nameof(AlphaCutoff), AlphaCutoff, 0, 1);
        if (!(Emissive.X >= 0 && Emissive.Y >= 0 && Emissive.Z >= 0 && float.IsFinite(Emissive.X + Emissive.Y + Emissive.Z)))
            problems.Add($"Emissive is {Emissive}, where a light of zero or more in each channel belongs.");
        if (!float.IsFinite(NormalScale)) problems.Add($"NormalScale is {NormalScale}, where a finite number belongs.");

        foreach (var role in Enum.GetValues<PictureRole>())
        {
            var holding = Pictures.Where(picture => picture.Layout.Holds(role)).Select(picture => picture.Path).ToList();
            if (holding.Count > 1) problems.Add($"{role} is held by {string.Join(" and ", holding)}, where one picture holds a role.");
        }

        return problems;
    }
}
