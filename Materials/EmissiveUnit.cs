namespace Materials;

/// <summary>What a material's emitted light is measured in.</summary>
/// <remarks>
/// A material made from physical numbers says its light in candelas a square meter, and one read
/// from glTF keeps glTF's emissive factor times its strength, which carries no unit, so no
/// luminance is made up for it. An engine takes a luminance as it is and scales a unitless emission
/// by a convention of its own, as the luminance it gives a strength of one under its exposure.
/// </remarks>
public enum EmissiveUnit
{
    /// <summary>Linear RGB in candelas a square meter.</summary>
    Luminance,

    /// <summary>
    /// glTF's emissive factor times its <c>KHR_materials_emissive_strength</c>, linear RGB as they
    /// stand, with no unit.
    /// </summary>
    Unitless,
}
