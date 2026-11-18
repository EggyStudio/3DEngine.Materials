namespace Materials;

/// <summary>
/// The engine's files, by the paths a material's pictures name, the one thing the module asks of an
/// engine.
/// </summary>
/// <remarks>
/// A path is read as the engine keeps its files, a folder on disk, an archive or an asset store, so
/// the module opens no file itself.
/// </remarks>
public interface IMaterialFiles
{
    /// <summary>The whole file at a path.</summary>
    /// <param name="path">The path, as a <see cref="MaterialPicture"/> names it.</param>
    /// <exception cref="FileNotFoundException">No file is at the path.</exception>
    byte[] ReadAllBytes(string path);
}
