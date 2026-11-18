namespace Materials;

/// <summary>
/// How a picture's texels are stored, plain or in the blocks a GPU samples as they are.
/// </summary>
/// <remarks>
/// A block format keeps a 4 by 4 square of texels in 8 or 16 bytes, and a picture whose side is not
/// a multiple of four is stored in whole blocks, the texels past its edge read by no one.
/// </remarks>
public enum PictureFormat
{
    /// <summary>Four bytes a texel, red, green, blue and alpha.</summary>
    Rgba8,

    /// <summary>
    /// BC1, 8 bytes a block, two colors of 5, 6 and 5 bits and the two between them, or one between
    /// them and a transparent black where the first color is not the greater.
    /// </summary>
    Bc1,

    /// <summary>
    /// BC1 read with no alpha, the transparent black of <see cref="Bc1"/> an opaque one, as a
    /// format of red, green and blue alone samples it.
    /// </summary>
    Bc1Rgb,

    /// <summary>BC4, 8 bytes a block, one channel of eight levels between two ends.</summary>
    Bc4,

    /// <summary>BC5, 16 bytes a block, two channels, each a <see cref="Bc4"/> block.</summary>
    Bc5,

    /// <summary>
    /// BC7, 16 bytes a block, four channels in one of eight modes the block chooses.
    /// </summary>
    Bc7,
}
