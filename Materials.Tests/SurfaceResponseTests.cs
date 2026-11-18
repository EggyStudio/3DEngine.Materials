using System.Numerics;
using Xunit;

namespace Materials.Tests;

public class SurfaceResponseTests
{
    [Fact]
    public void The_Terms_Give_The_Values_Worked_By_Hand()
    {
        // GGX at an alpha of 0.5 is 1 over pi times a quarter at the half way along the normal and
        // a quarter over pi across it, the visibility a quarter with both ways along the normal,
        // and Schlick's Fresnel 0.04 head on, one at a grazing angle and 0.07 at a cosine of a
        // half.
        Assert.Equal(1.27324f, SurfaceResponse.Distribution(1f, 0.5f), 1e-4f);
        Assert.Equal(0.0795775f, SurfaceResponse.Distribution(0f, 0.5f), 1e-6f);
        Assert.Equal(0.25f, SurfaceResponse.Visibility(1f, 1f, 0.3f), 1e-6f);
        Assert.Equal(0.5f, SurfaceResponse.Visibility(0.5f, 0.5f, 1f), 1e-6f);
        Assert.Equal(0.04f, SurfaceResponse.Fresnel(0.04f, 1f, 1f), 1e-6f);
        Assert.Equal(1f, SurfaceResponse.Fresnel(0.04f, 1f, 0f), 1e-6f);
        Assert.Equal(0.07f, SurfaceResponse.Fresnel(0.04f, 1f, 0.5f), 1e-6f);
        Assert.Equal(1f / MathF.PI, SurfaceResponse.Lambert(), 1e-6f);
        Assert.Equal(1f / MathF.PI, SurfaceResponse.Burley(0.7f, 1f, 1f, 0.3f), 1e-6f);
        Assert.Equal(6.25f / MathF.PI, SurfaceResponse.Burley(1f, 0f, 0f, 1f), 1e-5f);
    }

    [Fact]
    public void A_Perceptual_Roughness_Is_Held_To_Its_Floor_And_Squared()
    {
        Assert.Equal(0.25f, SurfaceResponse.Alpha(0.5f), 1e-6f);
        Assert.Equal(0.045f * 0.045f, SurfaceResponse.Alpha(0.01f, SurfaceResponse.LeastRoughnessFullPrecision), 1e-7f);
        Assert.Equal(0.089f * 0.089f, SurfaceResponse.Alpha(0.01f, SurfaceResponse.LeastRoughnessHalfPrecision), 1e-7f);
    }

    [Fact]
    public void F0_Moves_The_Dielectrics_Reflectance_Toward_The_Base_Color_By_The_Metallic()
    {
        var red = new Vector3(1, 0, 0);
        Assert.Equal(new Vector3(0.04f), SurfaceResponse.F0(red, 0f, 0.04f));
        Assert.Equal(red, SurfaceResponse.F0(red, 1f, 0.04f));
        Assert.Equal(new Vector3(0.52f, 0.02f, 0.02f), SurfaceResponse.F0(red, 0.5f, 0.04f));
    }

    [Fact]
    public void Karis_Fit_Gives_The_Values_Worked_By_Hand()
    {
        // At a roughness of one the fit is 0.468 less 1.04 times 0.015 and 1.04 times 0.015 less
        // 0.018, whatever the angle, and at none it is 1.04 less 1.04 times 0.044113 and 1.04 times
        // it less 0.04 head on, 2^-9.28 being 0.001613.
        var (scale, bias) = SurfaceResponse.SplitSum(1f, 1f);
        Assert.Equal(0.4524f, scale, 1e-5f);
        Assert.Equal(-0.0024f, bias, 1e-5f);
        (scale, bias) = SurfaceResponse.SplitSum(0f, 1f);
        Assert.Equal(0.994122f, scale, 1e-5f);
        Assert.Equal(0.005878f, bias, 1e-5f);
    }

    [Fact]
    public void The_Integrated_Split_Sum_Keeps_The_Light_It_Is_Given()
    {
        // A mirror seen head on returns all of it in the scale, and no surface returns more than it
        // is given, the largest scale and bias together 1.00005 over a grid of 16 by 16.
        var (scale, bias) = SurfaceResponse.SplitSumIntegrated(0.01f, 1f);
        Assert.Equal(1f, scale, 1e-3f);
        Assert.Equal(0f, bias, 1e-3f);
        for (int i = 0; i < 8; i++)
        for (int j = 0; j < 8; j++)
        {
            var (s, b) = SurfaceResponse.SplitSumIntegrated((i + 0.5f) / 8, (j + 0.5f) / 8, 256);
            Assert.InRange(s + b, 0f, 1.001f);
        }
    }

    [Fact]
    public void Both_Hit_Forms_Agree_At_The_Default_Reflectance_And_Part_Below_It()
    {
        // A gray of 0.5 that is not metal, fully rough and seen head on, so the share is 0.04 times
        // 0.4524 less 0.0024, 0.015696, in both forms, and a reflectance of 0.0144, as a perceptual
        // 0.3 gives, lowers it to 0.004115 in the form that reads the material's.
        var paint = new Vector3(0.5f);
        var lobe = SurfaceResponse.HitShares(SurfaceResponse.HitForm.Lobe, paint, 0f, 1f, 0.04f, 1f);
        var split = SurfaceResponse.HitShares(SurfaceResponse.HitForm.SplitSum, paint, 0f, 1f, 0.04f, 1f);
        Assert.Equal(0.015696f, lobe.Glossy.X, 1e-5f);
        Assert.Equal(0.492152f, lobe.Diffuse.X, 1e-5f);
        Assert.Equal(lobe.Glossy.X, split.Glossy.X, 1e-6f);
        Assert.Equal(lobe.Diffuse.X, split.Diffuse.X, 1e-6f);

        var low = Reflectance.FromPerceptual(0.3f);
        Assert.Equal(0.015696f, SurfaceResponse.HitShares(SurfaceResponse.HitForm.Lobe, paint, 0f, 1f, low, 1f).Glossy.X, 1e-5f);
        Assert.Equal(0.004115f, SurfaceResponse.HitShares(SurfaceResponse.HitForm.SplitSum, paint, 0f, 1f, low, 1f).Glossy.X, 1e-5f);
    }
}
