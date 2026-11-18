namespace Materials;

// The sRGB transfer function both ways, in single precision as a GPU and the module's tables
// round it, so a byte the module writes is the one a sampler's decode and a shader's encode give.
internal static class Srgb
{
    public static float Decode(float value) => value <= 0.04045f
        ? value / 12.92f
        : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);

    public static float Encode(float value) => value <= 0.0031308f
        ? value * 12.92f
        : (1.055f * MathF.Pow(value, 1f / 2.4f)) - 0.055f;

    // A number from zero to one as a byte, a half rounded to the even byte as MathF.Round does.
    public static byte Byte(float value) => (byte)Math.Clamp(MathF.Round(value * 255f), 0f, 255f);
}
