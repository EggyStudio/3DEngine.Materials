namespace Materials;

/// <summary>
/// What a channel of a material's picture holds, in glTF 2.0's terms and its extensions'.
/// </summary>
/// <remarks>
/// <see cref="BaseColor"/> and <see cref="Emissive"/> fill red, green and blue together and are
/// sRGB. Every other role is one channel and linear, so a picture is sRGB where it holds a color
/// and linear otherwise, its alpha linear in either.
/// </remarks>
public enum PictureRole
{
    /// <summary>
    /// The base color, the albedo of a dielectric and the reflectance of a metal, sRGB.
    /// </summary>
    BaseColor,

    /// <summary>How opaque the surface is, one for opaque, as glTF's base color alpha.</summary>
    Opacity,

    /// <summary>
    /// The tangent-space normal's X, toward the texture's U, from zero for -1 to one for +1.
    /// </summary>
    NormalX,

    /// <summary>
    /// The tangent-space normal's Y, up the picture as glTF's and OpenGL's normal maps have it,
    /// from zero for -1 to one for +1.
    /// </summary>
    NormalY,

    /// <summary>
    /// The tangent-space normal's Z, toward the viewer from the surface, or nothing where a picture
    /// holds X and Y alone and Z is rebuilt to make a unit length.
    /// </summary>
    NormalZ,

    /// <summary>
    /// How much of the light from all around reaches the surface, one for all of it.
    /// </summary>
    Occlusion,

    /// <summary>
    /// The perceptual roughness glTF names, its square the GGX distribution's alpha.
    /// </summary>
    Roughness,

    /// <summary>How metal the surface is, one for a metal.</summary>
    Metallic,

    /// <summary>
    /// The share of light let through the surface and scattered, as glTF's
    /// <c>KHR_materials_diffuse_transmission</c> names it, a leaf's translucency.
    /// </summary>
    DiffuseTransmission,

    /// <summary>The light the surface emits, its color and strength, sRGB.</summary>
    Emissive,

    /// <summary>
    /// The depth below the surface for parallax occlusion, one for the deepest, a depth rather than
    /// a height.
    /// </summary>
    Depth,
}
