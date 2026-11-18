namespace Materials;

/// <summary>A picture a material is drawn with, where it is and what its channels hold.</summary>
/// <param name="Path">
/// The picture's path among the engine's files, as the material's description names it, read by the
/// engine.
/// </param>
/// <param name="Layout">What each channel holds, which also gives the color space it is read
/// in.</param>
public sealed record MaterialPicture(string Path, PictureLayout Layout)
{
    /// <summary>The color space the picture is read in, its layout's.</summary>
    public ColorSpace ColorSpace => Layout.ColorSpace;
}
