using Xunit;

namespace Materials.Tests;

public class PictureLayoutTests
{
    [Fact]
    public void A_Layout_Is_Srgb_Where_It_Holds_A_Color_And_Linear_Otherwise()
    {
        Assert.Equal(ColorSpace.Srgb, PictureLayout.Color.ColorSpace);
        Assert.Equal(ColorSpace.Srgb, PictureLayout.Emissive.ColorSpace);
        Assert.All(
            [PictureLayout.Normal, PictureLayout.Surface, PictureLayout.MetallicRoughness, PictureLayout.Occlusion, PictureLayout.Depth],
            layout => Assert.Equal(ColorSpace.Linear, layout.ColorSpace));
        Assert.Equal(ColorSpace.Srgb, new PictureLayout(PictureRole.BaseColor, PictureRole.BaseColor, PictureRole.BaseColor, PictureRole.Roughness).ColorSpace);
    }

    [Fact]
    public void Only_The_Two_Colors_Are_Read_In_Srgb()
    {
        foreach (var role in Enum.GetValues<PictureRole>())
            Assert.Equal(role is PictureRole.BaseColor or PictureRole.Emissive ? ColorSpace.Srgb : ColorSpace.Linear, PictureLayout.ColorSpaceOf(role));
    }

    [Fact]
    public void The_Surface_Picture_Binds_As_Gltfs_Occlusion_And_Metallic_Roughness_Textures()
    {
        foreach (var role in new[] { PictureRole.Roughness, PictureRole.Metallic })
            Assert.Equal(PictureLayout.MetallicRoughness.ChannelOf(role), PictureLayout.Surface.ChannelOf(role));
        Assert.Equal(PictureLayout.Occlusion.ChannelOf(PictureRole.Occlusion), PictureLayout.Surface.ChannelOf(PictureRole.Occlusion));
        Assert.Equal(3, PictureLayout.Surface.ChannelOf(PictureRole.DiffuseTransmission));
        Assert.Null(PictureLayout.Surface.ChannelOf(PictureRole.BaseColor));
        Assert.Equal(0, PictureLayout.Color.ChannelOf(PictureRole.BaseColor));
    }

    [Fact]
    public void A_Layout_Compares_By_Its_Channels()
    {
        Assert.Equal(PictureLayout.Surface, new PictureLayout(PictureRole.Occlusion, PictureRole.Roughness, PictureRole.Metallic, PictureRole.DiffuseTransmission));
        Assert.Equal(
            new MaterialPicture("bark/surface.ktx2", PictureLayout.Surface),
            new MaterialPicture("bark/surface.ktx2", new PictureLayout(PictureRole.Occlusion, PictureRole.Roughness, PictureRole.Metallic, PictureRole.DiffuseTransmission)));
        Assert.Equal(ColorSpace.Srgb, new MaterialPicture("bark/albedo.ktx2", PictureLayout.Color).ColorSpace);
    }

    [Fact]
    public void A_Normal_Of_Three_Channels_Is_Held()
    {
        var layout = new PictureLayout(PictureRole.NormalX, PictureRole.NormalY, PictureRole.NormalZ, null);
        Assert.Equal(2, layout.ChannelOf(PictureRole.NormalZ));
        Assert.False(PictureLayout.Normal.Holds(PictureRole.NormalZ));
    }

    [Theory]
    [InlineData(null, null, null, null, "one role at least")]
    [InlineData(PictureRole.BaseColor, PictureRole.Roughness, PictureRole.BaseColor, null, "fills red, green and blue together")]
    [InlineData(PictureRole.BaseColor, PictureRole.Emissive, PictureRole.BaseColor, null, "fills red, green and blue together")]
    [InlineData(PictureRole.Roughness, null, null, PictureRole.BaseColor, "alpha holds one channel")]
    [InlineData(PictureRole.Roughness, PictureRole.Roughness, null, null, "names it 2 times")]
    [InlineData(PictureRole.NormalX, null, null, null, "stand together")]
    [InlineData(null, null, PictureRole.NormalZ, null, "stand together")]
    public void A_Layout_That_Breaks_A_Rule_Is_Refused_With_The_Rule(PictureRole? r, PictureRole? g, PictureRole? b, PictureRole? a, string rule)
    {
        Assert.Contains(rule, Assert.Throws<ArgumentException>(() => new PictureLayout(r, g, b, a)).Message);
    }
}
