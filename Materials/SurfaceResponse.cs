using System.Numerics;

namespace Materials;

/// <summary>
/// How a lit surface sends its light back, the terms of a microfacet response and the two forms a
/// probe's ray takes it in where it meets a surface, written down as they stand until a room read
/// against its traced reference chooses between them.
/// </summary>
/// <remarks>
/// <para>
/// Every function gives a term in radiometric form, the diffuse term the albedo over pi, so an
/// engine whose lights are given as the light a white diffuse surface returns multiplies them by pi
/// or divides these by it. Roughness is glTF's perceptual roughness, whose square is the alpha the
/// distribution and the visibility take.
/// </para>
/// <para>
/// A dielectric's reflectance at normal incidence is the material's,
/// <see cref="MaterialDescription.Reflectance"/>, in <see cref="F0"/>, the form any shading model
/// reads. <see cref="HitForm.Lobe"/> holds it at <see cref="Reflectance.Dielectric"/> for every
/// material, the one value both forms agree on.
/// </para>
/// </remarks>
public static class SurfaceResponse
{
    /// <summary>The two forms of a probe's hit, the choice each engine sets.</summary>
    public enum HitForm
    {
        /// <summary>
        /// A dielectric's reflectance held at 0.04 whatever the material, a lamp reflected by the
        /// GGX lobe and diffused by Lambert's term times one less the Fresnel, the light from all
        /// around reflected by the split sum's share held to one and diffused by one less it.
        /// </summary>
        Lobe,

        /// <summary>
        /// A dielectric's reflectance the material's, a lamp and the light from all around both
        /// reflected by the split sum's share and diffused by one less it, held to zero.
        /// </summary>
        SplitSum,
    }

    /// <summary>
    /// The least perceptual roughness of <see cref="HitForm.Lobe"/>'s lit pass, 0.045.
    /// </summary>
    public const float LobeRoughnessFloor = 0.045f;

    /// <summary>
    /// The least perceptual roughness of <see cref="HitForm.SplitSum"/>'s lit pass, 0.089.
    /// </summary>
    public const float SplitSumRoughnessFloor = 0.089f;

    /// <summary>The alpha of a perceptual roughness, held to a floor and squared.</summary>
    /// <param name="roughness">glTF's perceptual roughness.</param>
    /// <param name="floor">The least roughness the response takes, as a lit pass holds it.</param>
    public static float Alpha(float roughness, float floor = 0f)
    {
        var held = Math.Clamp(roughness, floor, 1f);
        return held * held;
    }

    /// <summary>
    /// GGX's distribution of microfacet normals at the half way's cosine to the normal.
    /// </summary>
    /// <param name="nh">The cosine between the normal and the half way.</param>
    /// <param name="alpha">The alpha, a perceptual roughness squared.</param>
    public static float Distribution(float nh, float alpha)
    {
        var a2 = alpha * alpha;
        var d = nh * nh * (a2 - 1f) + 1f;
        return a2 / (MathF.PI * d * d);
    }

    /// <summary>Smith's height-correlated visibility, G over 4 nl nv.</summary>
    /// <param name="nv">The cosine between the normal and the way to the eye.</param>
    /// <param name="nl">The cosine between the normal and the way to the light.</param>
    /// <param name="alpha">The alpha, a perceptual roughness squared.</param>
    public static float Visibility(float nv, float nl, float alpha)
    {
        var a2 = alpha * alpha;
        return 0.5f / (nl * MathF.Sqrt(nv * nv * (1f - a2) + a2) + nv * MathF.Sqrt(nl * nl * (1f - a2) + a2));
    }

    /// <summary>Schlick's Fresnel, from a reflectance head on to one at a grazing angle.</summary>
    /// <param name="f0">The reflectance at normal incidence.</param>
    /// <param name="f90">The reflectance at a grazing angle, one for most surfaces.</param>
    /// <param name="vh">The cosine between the way to the eye and the half way.</param>
    public static float Fresnel(float f0, float f90, float vh) => f0 + (f90 - f0) * MathF.Pow(1f - Math.Clamp(vh, 0f, 1f), 5f);

    /// <summary>Schlick's Fresnel for a reflectance of each channel.</summary>
    /// <param name="f0">The reflectance at normal incidence of each channel.</param>
    /// <param name="f90">The reflectance at a grazing angle.</param>
    /// <param name="vh">The cosine between the way to the eye and the half way.</param>
    public static Vector3 Fresnel(Vector3 f0, float f90, float vh) =>
        new(Fresnel(f0.X, f90, vh), Fresnel(f0.Y, f90, vh), Fresnel(f0.Z, f90, vh));

    /// <summary>Lambert's diffuse term, one over pi.</summary>
    public static float Lambert() => 1f / MathF.PI;

    /// <summary>
    /// Burley's diffuse term, which darkens a smooth surface's edges and lightens a rough one's.
    /// </summary>
    /// <param name="alpha">The alpha, a perceptual roughness squared.</param>
    /// <param name="nv">The cosine between the normal and the way to the eye.</param>
    /// <param name="nl">The cosine between the normal and the way to the light.</param>
    /// <param name="lh">The cosine between the way to the light and the half way.</param>
    public static float Burley(float alpha, float nv, float nl, float lh)
    {
        var f90 = 0.5f + 2f * alpha * lh * lh;
        return Fresnel(1f, f90, nl) * Fresnel(1f, f90, nv) / MathF.PI;
    }

    /// <summary>
    /// The reflectance at normal incidence of a surface, the dielectric's reflectance moved toward
    /// the base color by how metal it is.
    /// </summary>
    /// <param name="baseColor">The base color in linear numbers.</param>
    /// <param name="metallic">How metal the surface is.</param>
    /// <param name="reflectance">The dielectric's reflectance at normal incidence.</param>
    public static Vector3 F0(Vector3 baseColor, float metallic, float reflectance) =>
        Vector3.Lerp(new Vector3(reflectance), baseColor, metallic);

    /// <summary>
    /// Karis's fit of the split sum, the scale on a reflectance and the bias added to it that give
    /// the share of the light from all around a surface reflects toward the eye.
    /// </summary>
    /// <param name="roughness">The perceptual roughness.</param>
    /// <param name="nv">The cosine between the normal and the way to the eye.</param>
    public static (float Scale, float Bias) SplitSum(float roughness, float nv)
    {
        Vector4 r = roughness * new Vector4(-1f, -0.0275f, -0.572f, 0.022f) + new Vector4(1f, 0.0425f, 1.04f, -0.04f);
        var a004 = MathF.Min(r.X * r.X, MathF.Pow(2f, -9.28f * nv)) * r.X + r.Y;
        return (-1.04f * a004 + r.Z, 1.04f * a004 + r.W);
    }

    /// <summary>
    /// The split sum integrated by importance sampling GGX, the scale and the bias of
    /// <see cref="SplitSum"/> without a fit, for a table an engine samples.
    /// </summary>
    /// <param name="roughness">The perceptual roughness.</param>
    /// <param name="nv">The cosine between the normal and the way to the eye.</param>
    /// <param name="samples">How many half ways are drawn, Hammersley's points.</param>
    public static (float Scale, float Bias) SplitSumIntegrated(float roughness, float nv, int samples = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 1);
        var a2 = Alpha(roughness) * Alpha(roughness);
        var v = new Vector3(MathF.Sqrt(MathF.Max(0, 1 - nv * nv)), 0, nv);
        float scale = 0, bias = 0;
        for (var i = 0; i < samples; i++)
        {
            var (u1, u2) = ((i + 0.5f) / samples, Reversed((uint)i) / 4294967296f);
            var phi = 2 * MathF.PI * u1;
            var cosTheta = MathF.Sqrt((1 - u2) / (1 + (a2 - 1) * u2));
            var sinTheta = MathF.Sqrt(MathF.Max(0, 1 - cosTheta * cosTheta));
            var h = new Vector3(sinTheta * MathF.Cos(phi), sinTheta * MathF.Sin(phi), cosTheta);
            var vh = Vector3.Dot(v, h);
            var l = 2 * vh * h - v;
            if (l.Z <= 0 || vh <= 0) continue;
            var weight = Visibility(nv, l.Z, Alpha(roughness)) * 4 * l.Z * vh / h.Z;
            var fresnel = MathF.Pow(1 - vh, 5);
            scale += (1 - fresnel) * weight;
            bias += fresnel * weight;
        }

        return (scale / samples, bias / samples);
    }

    /// <summary>
    /// The weights a probe's ray gives a surface it meets in a form, the diffuse color that takes
    /// the light from all around and the glossy share that takes the light along the mirror way.
    /// </summary>
    /// <remarks>
    /// <see cref="HitForm.Lobe"/> holds the share to one and weighs the diffuse by one less it, and
    /// reflects a lamp by the GGX lobe. <see cref="HitForm.SplitSum"/> weighs the diffuse by one
    /// less the share held to zero, and reflects a lamp by the diffuse and the share together.
    /// </remarks>
    /// <param name="form">The form.</param>
    /// <param name="paint">The surface's base color in linear numbers.</param>
    /// <param name="metallic">How metal it is.</param>
    /// <param name="roughness">Its perceptual roughness.</param>
    /// <param name="reflectance">Its dielectric's reflectance, which <see cref="HitForm.Lobe"/>
    /// sets aside.</param>
    /// <param name="nv">The cosine between its normal and the way back along the ray.</param>
    public static (Vector3 Diffuse, Vector3 Glossy) HitShares(HitForm form, Vector3 paint, float metallic, float roughness, float reflectance, float nv)
    {
        var (scale, bias) = SplitSum(roughness, nv);
        if (form == HitForm.Lobe)
        {
            var share = Vector3.Clamp(F0(paint, metallic, Reflectance.Dielectric) * scale + new Vector3(bias), Vector3.Zero, Vector3.One);
            return (paint * (1 - metallic) * (Vector3.One - share), share);
        }

        var glossy = F0(paint, metallic, reflectance) * scale + new Vector3(bias);
        return (paint * (1 - metallic) * Vector3.Clamp(Vector3.One - glossy, Vector3.Zero, Vector3.One), glossy);
    }

    // A uint's bits in the reverse order, Van der Corput's radical inverse in base 2 times 2^32.
    private static uint Reversed(uint bits)
    {
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        return ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
    }
}
