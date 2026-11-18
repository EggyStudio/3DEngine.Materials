namespace Materials;

/// <summary>
/// The engine's files, by the paths a material's pictures name, the one thing the module asks of an
/// engine.
/// </summary>
/// <remarks>
/// A path is read as the engine keeps its files, a folder on disk, an archive or an asset store, so
/// the module opens no file itself. The module reads a KTX2 picture itself and asks
/// <see cref="Decode"/> for any other, so an engine whose pictures are PNG or JPEG decodes them
/// with its own decoders.
/// </remarks>
public interface IMaterialFiles
{
    /// <summary>The whole file at a path.</summary>
    /// <param name="path">The path, as a <see cref="MaterialPicture"/> names it.</param>
    /// <exception cref="FileNotFoundException">No file is at the path.</exception>
    byte[] ReadAllBytes(string path);

    /// <summary>
    /// A picture of a format the module does not read, as PNG or JPEG, decoded by the engine, or
    /// nothing where the engine reads none of it either.
    /// </summary>
    /// <remarks>
    /// The texels are four bytes each, red, green, blue and alpha, rows from the top, alpha
    /// straight rather than premultiplied, and each channel the number the file stores, sRGB where
    /// the picture's layout holds a color and linear otherwise, as the module decodes it by the
    /// picture's role. An engine that keeps its pictures as KTX2 alone leaves this as it is.
    /// </remarks>
    /// <param name="path">The picture's path.</param>
    /// <param name="file">The file's bytes, as <see cref="ReadAllBytes"/> gave them.</param>
    (byte[] Rgba, int Width, int Height)? Decode(string path, byte[] file) => null;
}
