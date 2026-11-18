using System.Numerics;
using System.Security.Cryptography;
using Xunit;

namespace Materials.Tests;

public class PrototypesTests
{
    [Fact]
    public void The_Table_Holds_The_Seven_Parts_And_Any_Other_Name_Is_White()
    {
        Assert.Equal(["floor", "wall", "ceiling", "stairs", "door", "window", "hazard"], Prototypes.Names);
        Assert.All(Prototypes.Names, name => Assert.True(Prototypes.Colors.ContainsKey(name)));
        Assert.Equal(Vector3.One, Prototypes.SrgbColor("crate"));
        Assert.Equal(Vector3.One, Prototypes.Tint("crate"));
    }

    [Fact]
    public void A_Tint_Is_Its_Color_In_Linear_Numbers()
    {
        // sRGB 0.9 is a linear 0.7874, 0.22 a linear 0.0397.
        var hazard = Prototypes.Tint("hazard");
        Assert.Equal(0.7874f, hazard.X, 1e-4f);
        Assert.Equal(0.0397f, hazard.Y, 1e-4f);
    }

    [Fact]
    public void The_Grid_Has_Its_Lines_And_Its_Mark_Where_The_Cell_Is_Measured()
    {
        // At 1024 pixels a meter, a meter's line four pixels at each edge, the half's 510 to 513,
        // a quarter's 255 and 256, and the mark's 1 two squares of eight in from the line, its top
        // row's square at 36 to 43 across and 20 to 27 down.
        var grid = Prototypes.Grid(1024);
        byte Gray(int x, int y) => grid[(y * 1024 + x) * 4];
        Assert.Equal(153, Gray(0, 100));
        Assert.Equal(153, Gray(1023, 100));
        Assert.Equal(252, Gray(100, 100));
        Assert.Equal(209, Gray(255, 100));
        Assert.Equal(153, Gray(510, 100));
        Assert.Equal(153, Gray(513, 100));
        Assert.Equal(252, Gray(514, 100));
        Assert.Equal(153, Gray(36, 20));
        Assert.Equal(252, Gray(35, 20));
        Assert.All(Enumerable.Range(0, 1024 * 1024), at => Assert.Equal(255, grid[at * 4 + 3]));
    }

    [Fact]
    public void A_Picture_Is_The_Grid_Tinted_In_Linear_Light()
    {
        // The face of 252 decoded, times the floor's linear 0.7874, 0.7874 and 0.7682 and encoded
        // again, gives 227, 227 and 224.
        var floor = Prototypes.Picture("floor", 512);
        Assert.Equal([227, 227, 224, 255], floor.AsSpan((100 * 512 + 100) * 4, 4).ToArray());
    }

    [Fact]
    public void The_Pictures_Are_The_Ones_The_Level_Editor_Shows()
    {
        // The SHA-256 of each 512 pixel picture's RGBA, the pictures a level editor shows the
        // prototypes by, so a change to the table, the grid or the tinting shows here.
        var hashes = new Dictionary<string, string>
        {
            ["floor"] = "2fc825d0ac583b9a478b743ab51c1e6d31960f43cdec66b2619874e327166542",
            ["wall"] = "2fc825d0ac583b9a478b743ab51c1e6d31960f43cdec66b2619874e327166542",
            ["ceiling"] = "32f37abc775fd2032d2f582ffd078f20206bc57ada66add7691333a44ef965fa",
            ["stairs"] = "610f3f1fe0f995681236e161b5321bdf5ddd7ae86d909dc95d41aed69dacd4f8",
            ["door"] = "c022bfdd61ae4a0a9481765bced1467333f40591cb149dc74380775667abb4ac",
            ["window"] = "b99bde3a4eefad8413eedcf5b6c1b5c3144b5bf750b6d8379b741efd2f49d1df",
            ["hazard"] = "acd68bf801da5471d7f6360385690ce0517a40519be77b31d292cdd5b4cafd8c",
        };
        foreach (var (name, hash) in hashes)
            Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(Prototypes.Picture(name, 512))));
    }

    [Fact]
    public void A_Prototype_Is_A_Rough_Dielectric_Tinting_The_Grid()
    {
        var material = Prototypes.Description("door", "textures/grid.ktx2");
        Assert.Equal(new Vector4(Prototypes.Tint("door"), 1), material.BaseColor);
        Assert.Equal((0.85f, 0f), (material.Roughness, material.Metallic));
        Assert.Equal(new MaterialPicture("textures/grid.ktx2", PictureLayout.Color), Assert.Single(material.Pictures));
        Assert.Empty(material.Problems());
    }

    [Theory]
    [InlineData(15)]
    [InlineData(4097)]
    public void A_Grid_Of_Another_Size_Is_Refused(int pixels)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Prototypes.Grid(pixels));
    }
}
