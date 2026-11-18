namespace Materials;

/// <summary>How a picture's numbers in red, green and blue stand for light.</summary>
/// <remarks>Alpha is linear in either, as every GPU format with sRGB samples it.</remarks>
public enum ColorSpace
{
    /// <summary>
    /// A number is proportional to the light, as a roughness, a normal or a mask is.
    /// </summary>
    Linear,

    /// <summary>
    /// A number is encoded by the sRGB transfer function, as a color picked or photographed is,
    /// and a GPU decodes it to linear light as it samples.
    /// </summary>
    Srgb,
}
