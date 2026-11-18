namespace Materials;

/// <summary>
/// A dielectric's reflectance at normal incidence, the share of light it reflects when lit and
/// seen head on, and the two other forms an engine may take it in.
/// </summary>
/// <remarks>
/// The module holds the reflectance itself, which every shading model reads, and gives glTF's index
/// of refraction and the perceptual reflectance of Filament's material model as functions of it.
/// 0.04, an index of 1.5 and a perceptual 0.5 are the same surface, water, plastic and most other
/// things a scene is made of. A metal's reflectance is its base color, which none of these give.
/// </remarks>
public static class Reflectance
{
    /// <summary>The reflectance of most dielectrics, 0.04, an index of refraction of 1.5.</summary>
    public const float Dielectric = 0.04f;

    /// <summary>
    /// The reflectance of a dielectric of an index of refraction, as glTF's
    /// <c>KHR_materials_ior</c> gives it.
    /// </summary>
    /// <param name="ior">The index of refraction, one or more.</param>
    public static float FromIor(float ior)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ior, 1f);
        var share = (ior - 1f) / (ior + 1f);
        return share * share;
    }

    /// <summary>The index of refraction of a dielectric of a reflectance.</summary>
    /// <param name="reflectance">The reflectance, from zero to below one.</param>
    public static float ToIor(float reflectance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reflectance);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(reflectance, 1f);
        var root = MathF.Sqrt(reflectance);
        return (1f + root) / (1f - root);
    }

    /// <summary>
    /// The reflectance of a perceptual reflectance, 0.16 times its square, so 0.5 is 0.04 and 1 is
    /// 0.16, a gemstone's.
    /// </summary>
    /// <param name="perceptual">The perceptual reflectance, from zero to one.</param>
    public static float FromPerceptual(float perceptual)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(perceptual);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(perceptual, 1f);
        return 0.16f * perceptual * perceptual;
    }

    /// <summary>
    /// The perceptual reflectance of a reflectance, the square root of a sixth and a quarter of it.
    /// </summary>
    /// <param name="reflectance">The reflectance, from zero to 0.16.</param>
    public static float ToPerceptual(float reflectance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reflectance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reflectance, 0.16f);
        return MathF.Sqrt(reflectance / 0.16f);
    }
}
