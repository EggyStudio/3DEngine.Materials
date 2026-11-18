using System.Numerics;
using Xunit;

namespace Materials.Tests;

public class MaterialFileTests
{
    private const string Bark = """
        {
          "format": "material/1",
          "baseColor": [0.8, 0.7, 0.6, 1],
          "roughness": 0.9,
          "alphaMode": "mask",
          "pictures": [
            { "path": "albedo.ktx2", "layout": "color" },
            { "path": "../shared/surface.ktx2", "layout": ["occlusion", "roughness", "metallic", null] }
          ]
        }
        """;

    [Fact]
    public void A_File_Written_By_Hand_Reads_To_Its_Description()
    {
        // The pictures' paths are joined to the file's folder, the second climbing out of it, and
        // each field left out takes the description's default.
        var material = MaterialFile.Read(Bark, "materials/bark/bark.material.json");
        Assert.Equal(new Vector4(0.8f, 0.7f, 0.6f, 1f), material.BaseColor);
        Assert.Equal((0.9f, 0f, Reflectance.Dielectric, AlphaMode.Mask, 0.5f), (material.Roughness, material.Metallic, material.Reflectance, material.AlphaMode, material.AlphaCutoff));
        Assert.Equal(
            [
                new MaterialPicture("materials/bark/albedo.ktx2", PictureLayout.Color),
                new MaterialPicture("materials/shared/surface.ktx2", new PictureLayout(PictureRole.Occlusion, PictureRole.Roughness, PictureRole.Metallic, null)),
            ],
            material.Pictures);
    }

    [Fact]
    public void A_Material_Written_And_Read_Again_Is_The_Same_Description()
    {
        var material = new MaterialDescription
        {
            BaseColor = new Vector4(0.25f, 0.5f, 0.75f, 0.9f),
            Metallic = 0.3f,
            Roughness = 0.45f,
            Reflectance = Reflectance.FromIor(1.33f),
            Emissive = new Vector3(2f, 1f, 0.5f),
            EmissiveUnit = EmissiveUnit.Unitless,
            NormalScale = 0.8f,
            OcclusionStrength = 0.6f,
            DiffuseTransmission = 0.2f,
            AlphaMode = AlphaMode.Blend,
            AlphaCutoff = 0.3f,
            DoubleSided = true,
            Pictures =
            [
                new MaterialPicture("set/albedo.ktx2", PictureLayout.Color),
                new MaterialPicture("set/surface.ktx2", PictureLayout.Surface),
                new MaterialPicture("other/normal.ktx2", PictureLayout.Normal),
                new MaterialPicture("set/depth.ktx2", new PictureLayout(null, PictureRole.Depth, null, null)),
            ],
        };
        var text = MaterialFile.Write(material, "set/leaf.material.json");
        Assert.Contains("\"path\": \"../other/normal.ktx2\"", text);
        Assert.Contains("\"layout\": \"surface\"", text);
        Assert.Equal(material, MaterialFile.Read(text, "set/leaf.material.json"));
        Assert.Equal(material.GetHashCode(), MaterialFile.Read(text, "set/leaf.material.json").GetHashCode());
    }

    [Theory]
    [InlineData("""{"format": "material/2"}""", "names no format material/1")]
    [InlineData("""{"format": "material/1", "shininess": 3}""", "has a field shininess")]
    [InlineData("""{"format": "material/1", "metallic": 2}""", "Metallic is 2")]
    [InlineData("""{"format": "material/1", "alphaMode": "cutout"}""", "gives alphaMode as cutout, where opaque, mask, blend belong")]
    [InlineData("""{"format": "material/1", "pictures": [{"path": "a.ktx2", "layout": "albedo"}]}""", "the layout albedo")]
    [InlineData("""{"format": "material/1", "pictures": [{"path": "a.ktx2", "layout": ["normalX", null, null, null]}]}""", "stand together")]
    [InlineData("""{"format": "material/1", "baseColor": [1, 1, 1]}""", "no array of 4 numbers")]
    [InlineData("""not json""", "is no JSON")]
    public void A_Broken_File_Is_Refused_With_The_Reason(string json, string reason)
    {
        Assert.Contains(reason, Assert.Throws<InvalidDataException>(() => MaterialFile.Read(json, "m.material.json")).Message);
    }

    [Fact]
    public void Descriptions_Of_The_Same_Pictures_Compare_Alike()
    {
        MaterialDescription One() => new() { Pictures = [new MaterialPicture("a.ktx2", PictureLayout.Color)] };
        Assert.Equal(One(), One());
        Assert.NotEqual(One(), One() with { Pictures = [new MaterialPicture("b.ktx2", PictureLayout.Color)] });
    }
}
