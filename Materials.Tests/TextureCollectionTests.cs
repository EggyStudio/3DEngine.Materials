using System.Numerics;
using Xunit;

namespace Materials.Tests;

public class TextureCollectionTests
{
    // A manifest as scripts/textures.py writes one back, a tile with its repeat's meters, a leaf
    // atlas with its boxes and means, and a bark the manifest marks waiting.
    private const string Manifest = """
        {
          "entries": [
            { "name": "carpet", "source": "src/carpet", "size": [1024, 1024], "albedo": {"file": "c.jpg"},
              "files": {"albedo.ktx2": 1, "normal.ktx2": 1, "surface.ktx2": 1}, "color": [0.5, 0.4, 0.3],
              "surface": [1.0, 0.9, 0.0, 0.0], "meters": [0.5, 0.25] },
            { "name": "birch-leaves", "kind": "leaves", "source": "birch", "size": [512, 512], "parts": [],
              "color": [0.7354, 0.5, 0.2], "surface": [1.0, 0.6, 0.0, 0.4], "leaves": [[0.0, 0.0, 0.5, 0.5], [0.5, 0.5, 0.5, 0.5]] },
            { "name": "oak-bark", "kind": "bark", "source": "oak", "size": [256, 512], "parts": [], "waiting": true }
          ]
        }
        """;

    [Fact]
    public void A_Manifest_Reads_To_Its_Entries_With_Their_Pictures()
    {
        var entries = TextureCollection.Read(Manifest, "textures/textures.json");
        Assert.Equal(["carpet", "birch-leaves", "oak-bark"], entries.Select(entry => entry.Name));
        Assert.Equal([TextureKind.Tile, TextureKind.Leaves, TextureKind.Bark], entries.Select(entry => entry.Kind));

        var carpet = entries[0];
        Assert.Equal(new MaterialPicture("textures/carpet/albedo.ktx2", PictureLayout.Color), carpet.Albedo);
        Assert.Equal(new MaterialPicture("textures/carpet/normal.ktx2", PictureLayout.Normal), carpet.Normal);
        Assert.Equal(new MaterialPicture("textures/carpet/surface.ktx2", PictureLayout.Surface), carpet.Surface);
        Assert.Null(carpet.Depth);
        Assert.Equal((1024, 1024), carpet.Size);
        Assert.Equal((0.5f, 0.25f), carpet.Meters);

        // A waiting entry's files stand in the collection's waiting folder, and a bark has its
        // depth.
        var bark = entries[2];
        Assert.Equal("textures/waiting/oak-bark/albedo.ktx2", bark.Albedo.Path);
        Assert.Equal(new MaterialPicture("textures/waiting/oak-bark/depth.ktx2", PictureLayout.Depth), bark.Depth);
        Assert.True(bark.Waiting);
    }

    [Fact]
    public void An_Entrys_Means_Are_Its_Manifests_The_Color_Decoded_To_Linear_Light()
    {
        // The manifest writes the mean color in sRGB, so 0.7354 is a linear 0.5.
        var leaves = TextureCollection.Read(Manifest, "textures.json")[1];
        Assert.Equal(0.5f, leaves.MeanColor!.Value.X, 1e-3f);
        Assert.Equal(new Vector4(1.0f, 0.6f, 0.0f, 0.4f), leaves.MeanSurface);
        Assert.Equal([(0f, 0f, 0.5f, 0.5f), (0.5f, 0.5f, 0.5f, 0.5f)], leaves.LeafBoxes);
        Assert.Null(TextureCollection.Read(Manifest, "textures.json")[2].MeanColor);
    }

    [Fact]
    public void An_Entry_Stands_For_A_Material_Its_Pictures_Carry()
    {
        // The factors are one so the pictures carry the values, and a cut-out kind is masked.
        var leaves = TextureCollection.Read(Manifest, "textures.json")[1].Description();
        Assert.Equal((Vector4.One, 1f, 1f, 1f, AlphaMode.Mask), (leaves.BaseColor, leaves.Metallic, leaves.Roughness, leaves.DiffuseTransmission, leaves.AlphaMode));
        Assert.Equal(3, leaves.Pictures.Count);
        Assert.Empty(leaves.Problems());
        var bark = TextureCollection.Read(Manifest, "textures.json")[2].Description();
        Assert.Equal(AlphaMode.Opaque, bark.AlphaMode);
        Assert.Equal(4, bark.Pictures.Count);
    }

    [Fact]
    public void The_Scripts_Own_Sample_Listing_Reads()
    {
        var listing = File.ReadAllText(Path.Combine(Repository.References, "textures", "textures.json"));
        var sample = Assert.Single(TextureCollection.Read(listing, "references/textures/textures.json"));
        Assert.Equal(("sample", TextureKind.Bark, "references/textures/sample/depth.ktx2"), (sample.Name, sample.Kind, sample.Depth!.Path));
    }

    [Theory]
    [InlineData("""{"entries": [{"name": "x", "kind": "moss"}]}""", "the kind moss")]
    [InlineData("""{"entries": [{"kind": "bark"}]}""", "an entry with no name")]
    [InlineData("""{"sets": []}""", "no entries")]
    public void A_Broken_Manifest_Is_Refused_With_The_Reason(string json, string reason)
    {
        Assert.Contains(reason, Assert.Throws<InvalidDataException>(() => TextureCollection.Read(json, "textures.json")).Message);
    }
}
