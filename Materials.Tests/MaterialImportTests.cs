using System.Numerics;
using Xunit;

namespace Materials.Tests;

public class MaterialImportTests
{
    // A glTF of two materials, the first using every field the import reads, its occlusion and
    // metallic-roughness one image as an ORM texture packs them, the second giving nothing.
    private const string Gltf = """
        {
          "asset": { "version": "2.0" },
          "images": [
            { "uri": "textures/albedo.png" },
            { "uri": "textures/orm%20map.png" },
            { "uri": "../shared/normal.png" },
            { "uri": "data:image/png;base64,iVBORw0KGgo=" },
            { "uri": "textures/leaf.png" }
          ],
          "textures": [ { "source": 0 }, { "source": 1 }, { "source": 2 }, { "source": 3 }, { "source": 4 } ],
          "materials": [
            {
              "pbrMetallicRoughness": {
                "baseColorFactor": [0.5, 0.6, 0.7, 0.8],
                "baseColorTexture": { "index": 0 },
                "metallicFactor": 0.25,
                "roughnessFactor": 0.75,
                "metallicRoughnessTexture": { "index": 1 }
              },
              "normalTexture": { "index": 2, "scale": 0.5 },
              "occlusionTexture": { "index": 1, "strength": 0.9 },
              "emissiveTexture": { "index": 3 },
              "emissiveFactor": [1, 0.5, 0],
              "alphaMode": "MASK",
              "alphaCutoff": 0.3,
              "doubleSided": true,
              "extensions": {
                "KHR_materials_emissive_strength": { "emissiveStrength": 4 },
                "KHR_materials_ior": { "ior": 1.33 },
                "KHR_materials_diffuse_transmission": { "diffuseTransmissionFactor": 0.4, "diffuseTransmissionTexture": { "index": 4 } }
              }
            },
            {}
          ]
        }
        """;

    [Fact]
    public void A_Gltf_Material_Reads_To_Its_Description()
    {
        var material = MaterialImport.FromGltf(Gltf, "models/tree/tree.gltf")[0];
        Assert.Equal(new Vector4(0.5f, 0.6f, 0.7f, 0.8f), material.BaseColor);
        Assert.Equal((0.25f, 0.75f), (material.Metallic, material.Roughness));
        Assert.Equal(Reflectance.FromIor(1.33f), material.Reflectance, 1e-6f);
        Assert.Equal((new Vector3(4f, 2f, 0f), EmissiveUnit.Unitless), (material.Emissive, material.EmissiveUnit));
        Assert.Equal((0.5f, 0.9f, 0.4f), (material.NormalScale, material.OcclusionStrength, material.DiffuseTransmission));
        Assert.Equal((AlphaMode.Mask, 0.3f, true), (material.AlphaMode, material.AlphaCutoff, material.DoubleSided));
        Assert.Empty(material.Problems());
    }

    [Fact]
    public void Its_Pictures_Are_Joined_To_The_Files_Folder_And_One_Image_Holds_The_Roles_It_Packs()
    {
        // The occlusion and the metallic-roughness are one image, so one picture holds the three
        // roles where glTF places them, and the embedded emissive image has no path to give.
        var material = MaterialImport.FromGltf(Gltf, "models/tree/tree.gltf")[0];
        Assert.Equal(
            [
                new MaterialPicture("models/tree/textures/albedo.png", PictureLayout.Color),
                new MaterialPicture("models/tree/textures/orm map.png", new PictureLayout(PictureRole.Occlusion, PictureRole.Roughness, PictureRole.Metallic, null)),
                new MaterialPicture("models/shared/normal.png", PictureLayout.Normal),
                new MaterialPicture("models/tree/textures/leaf.png", new PictureLayout(null, null, null, PictureRole.DiffuseTransmission)),
            ],
            material.Pictures);
    }

    [Fact]
    public void A_Material_Giving_Nothing_Takes_Gltfs_Defaults()
    {
        // glTF's metallic and roughness are one where a file gives none, as its specification
        // has it.
        var material = MaterialImport.FromGltf(Gltf, "tree.gltf")[1];
        Assert.Equal((Vector4.One, 1f, 1f, Reflectance.Dielectric, AlphaMode.Opaque), (material.BaseColor, material.Metallic, material.Roughness, material.Reflectance, material.AlphaMode));
        Assert.Empty(material.Pictures);
    }

    [Fact]
    public void A_Rooted_Images_Path_Is_Kept_As_It_Is()
    {
        const string json = """{"images": [{"uri": "/library/bricks.png"}], "textures": [{"source": 0}], "materials": [{"pbrMetallicRoughness": {"baseColorTexture": {"index": 0}}}]}""";
        Assert.Equal("/library/bricks.png", MaterialImport.FromGltf(json, "models/wall.gltf")[0].Pictures[0].Path);
    }

    [Theory]
    [InlineData("""{"materials": [{"alphaMode": "CUTOUT"}]}""", "alphaMode CUTOUT")]
    [InlineData("""{"materials": [{"pbrMetallicRoughness": {"baseColorTexture": {"index": 3}}}]}""", "texture 3")]
    [InlineData("""{"materials": [{"pbrMetallicRoughness": {"metallicFactor": 2}}]}""", "Metallic is 2")]
    [InlineData("""not json""", "is no JSON")]
    public void A_Broken_Material_Is_Refused_With_The_Reason(string json, string reason)
    {
        Assert.Contains(reason, Assert.Throws<InvalidDataException>(() => MaterialImport.FromGltf(json, "a.gltf")).Message);
    }
}
