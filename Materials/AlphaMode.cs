namespace Materials;

/// <summary>How a material's opacity is read, glTF's three modes.</summary>
public enum AlphaMode
{
    /// <summary>The surface is opaque whatever its opacity says.</summary>
    Opaque,

    /// <summary>
    /// A texel is drawn where its opacity reaches the cutoff and left out elsewhere.
    /// </summary>
    Mask,

    /// <summary>The surface is blended over what lies behind it by its opacity.</summary>
    Blend,
}
