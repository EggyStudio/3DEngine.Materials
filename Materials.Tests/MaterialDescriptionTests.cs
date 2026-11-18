using System.Numerics;
using Xunit;

namespace Materials.Tests;

public class MaterialDescriptionTests
{
    [Fact]
    public void The_Three_Forms_Of_A_Dielectrics_Reflectance_Meet_At_Four_Percent()
    {
        Assert.Equal(0.04f, Reflectance.FromIor(1.5f), 1e-6f);
        Assert.Equal(1.5f, Reflectance.ToIor(0.04f), 1e-5f);
        Assert.Equal(0.04f, Reflectance.FromPerceptual(0.5f), 1e-6f);
        Assert.Equal(0.5f, Reflectance.ToPerceptual(0.04f), 1e-6f);
        Assert.Equal(0.16f, Reflectance.FromPerceptual(1f), 1e-6f);
        foreach (var ior in new[] { 1f, 1.33f, 2.42f })
            Assert.Equal(ior, Reflectance.ToIor(Reflectance.FromIor(ior)), 1e-4f);
    }

    [Fact]
    public void A_Description_Of_Nothing_Is_A_Rough_White_Dielectric()
    {
        var material = new MaterialDescription();
        Assert.Equal((Vector4.One, 0f, 1f, Reflectance.Dielectric, AlphaMode.Opaque), (material.BaseColor, material.Metallic, material.Roughness, material.Reflectance, material.AlphaMode));
        Assert.Empty(material.Problems());
        Assert.Null(material.PictureOf(PictureRole.BaseColor));
    }

    [Fact]
    public void A_Gltf_Emission_Is_Kept_As_It_Stands_With_Its_Unit_Named()
    {
        // glTF's emissive factor times its strength has no unit, so an importer keeps the product
        // and names it unitless, where a description of nothing says candelas a square meter.
        var factor = new Vector3(1f, 0.5f, 0f);
        var material = new MaterialDescription { Emissive = factor * 3f, EmissiveUnit = EmissiveUnit.Unitless };
        Assert.Equal((new Vector3(3f, 1.5f, 0f), EmissiveUnit.Unitless), (material.Emissive, material.EmissiveUnit));
        Assert.Equal(EmissiveUnit.Luminance, new MaterialDescription().EmissiveUnit);
        Assert.Empty(material.Problems());
    }

    [Fact]
    public void A_Role_Is_Found_With_Its_Channel_In_The_Picture_That_Holds_It()
    {
        var surface = new MaterialPicture("bark/surface.ktx2", PictureLayout.Surface);
        var material = new MaterialDescription { Pictures = [new MaterialPicture("bark/albedo.ktx2", PictureLayout.Color), surface] };
        Assert.Equal((surface, 1), material.PictureOf(PictureRole.Roughness));
        Assert.Equal((surface, 3), material.PictureOf(PictureRole.DiffuseTransmission));
    }

    [Fact]
    public void Every_Broken_Rule_Is_Said()
    {
        var material = new MaterialDescription
        {
            Metallic = 1.5f,
            Roughness = float.NaN,
            Reflectance = 1f,
            Emissive = new Vector3(-1, 0, 0),
            Pictures = [new MaterialPicture("a.ktx2", PictureLayout.Surface), new MaterialPicture("b.ktx2", PictureLayout.MetallicRoughness)],
        };
        var problems = material.Problems();
        Assert.Contains(problems, p => p.StartsWith("Metallic is 1.5"));
        Assert.Contains(problems, p => p.StartsWith("Roughness is NaN"));
        Assert.Contains(problems, p => p.StartsWith("Reflectance is 1"));
        Assert.Contains(problems, p => p.StartsWith("Emissive is"));
        Assert.Contains(problems, p => p == "Roughness is held by a.ktx2 and b.ktx2, where one picture holds a role.");
        Assert.Contains(problems, p => p == "Metallic is held by a.ktx2 and b.ktx2, where one picture holds a role.");
        Assert.Equal(6, problems.Count);
    }
}
