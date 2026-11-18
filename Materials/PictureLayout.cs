namespace Materials;

/// <summary>
/// What each of a picture's four channels holds, red, green, blue and alpha, nothing where one
/// holds nothing, and the color space it is read in.
/// </summary>
/// <remarks>
/// <para>
/// A color, <see cref="PictureRole.BaseColor"/> or <see cref="PictureRole.Emissive"/>, fills red,
/// green and blue together and makes the picture sRGB, with any one-channel role in alpha, which is
/// linear in an sRGB picture as well. Every other role is one channel, named once at most, and the
/// normal's X and Y stand together, with its Z beside them or rebuilt. A layout holds one role at
/// least.
/// </para>
/// <para>
/// The layouts the static members give are the ones both texture collections write and glTF binds,
/// so <see cref="Surface"/> is glTF's occlusion and metallic-roughness textures at once, with the
/// diffuse transmission in alpha.
/// </para>
/// </remarks>
public sealed record PictureLayout
{
    /// <summary>A layout of the four channels' roles.</summary>
    /// <param name="r">What red holds, or nothing.</param>
    /// <param name="g">What green holds, or nothing.</param>
    /// <param name="b">What blue holds, or nothing.</param>
    /// <param name="a">What alpha holds, or nothing.</param>
    /// <exception cref="ArgumentException">The roles break a rule the remarks give, with the
    /// rule.</exception>
    public PictureLayout(PictureRole? r, PictureRole? g, PictureRole? b, PictureRole? a)
    {
        PictureRole?[] channels = [r, g, b, a];
        if (Problem(channels) is { } problem) throw new ArgumentException(problem);
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>The base color in red, green and blue and the opacity in alpha, sRGB.</summary>
    public static PictureLayout Color { get; } = new(PictureRole.BaseColor, PictureRole.BaseColor, PictureRole.BaseColor, PictureRole.Opacity);

    /// <summary>The normal's X and Y in red and green, its Z rebuilt, as BC5 holds it.</summary>
    public static PictureLayout Normal { get; } = new(PictureRole.NormalX, PictureRole.NormalY, null, null);

    /// <summary>
    /// The occlusion in red, the roughness in green, the metallic in blue and the diffuse
    /// transmission in alpha, linear.
    /// </summary>
    public static PictureLayout Surface { get; } = new(PictureRole.Occlusion, PictureRole.Roughness, PictureRole.Metallic, PictureRole.DiffuseTransmission);

    /// <summary>
    /// glTF's metallic-roughness texture, the roughness in green and the metallic in blue.
    /// </summary>
    public static PictureLayout MetallicRoughness { get; } = new(null, PictureRole.Roughness, PictureRole.Metallic, null);

    /// <summary>glTF's occlusion texture, the occlusion in red.</summary>
    public static PictureLayout Occlusion { get; } = new(PictureRole.Occlusion, null, null, null);

    /// <summary>The emitted light in red, green and blue, sRGB.</summary>
    public static PictureLayout Emissive { get; } = new(PictureRole.Emissive, PictureRole.Emissive, PictureRole.Emissive, null);

    /// <summary>The depth for parallax occlusion in red, as BC4 holds it.</summary>
    public static PictureLayout Depth { get; } = new(PictureRole.Depth, null, null, null);

    /// <summary>What red holds, or nothing.</summary>
    public PictureRole? R { get; }

    /// <summary>What green holds, or nothing.</summary>
    public PictureRole? G { get; }

    /// <summary>What blue holds, or nothing.</summary>
    public PictureRole? B { get; }

    /// <summary>What alpha holds, or nothing.</summary>
    public PictureRole? A { get; }

    /// <summary>sRGB where red, green and blue hold a color, linear otherwise.</summary>
    public ColorSpace ColorSpace => IsColor(R) ? ColorSpace.Srgb : ColorSpace.Linear;

    /// <summary>
    /// The channel a role stands in, 0 for red to 3 for alpha, the first of the three for a color,
    /// or nothing where the picture does not hold it.
    /// </summary>
    /// <param name="role">The role.</param>
    public int? ChannelOf(PictureRole role)
    {
        PictureRole?[] channels = [R, G, B, A];
        int at = Array.IndexOf(channels, role);
        return at < 0 ? null : at;
    }

    /// <summary>Whether the picture holds a role.</summary>
    /// <param name="role">The role.</param>
    public bool Holds(PictureRole role) => ChannelOf(role) is not null;

    /// <summary>
    /// The color space a role is read in, sRGB for a color and linear for the rest.
    /// </summary>
    /// <param name="role">The role.</param>
    public static ColorSpace ColorSpaceOf(PictureRole role) => IsColor(role) ? ColorSpace.Srgb : ColorSpace.Linear;

    private static bool IsColor(PictureRole? role) => role is PictureRole.BaseColor or PictureRole.Emissive;

    // The first rule the channels break, or nothing.
    private static string? Problem(PictureRole?[] channels)
    {
        if (channels.All(role => role is null)) return "A layout holds one role at least.";
        if (IsColor(channels[3])) return $"{channels[3]} fills red, green and blue, and alpha holds one channel.";

        var color = channels.Take(3).FirstOrDefault(IsColor);
        if (color is not null && !channels.Take(3).All(role => role == color))
            return $"{color} fills red, green and blue together, and the layout gives them {string.Join(", ", channels.Take(3).Select(Name))}.";

        foreach (var role in channels.Where(role => role is not null && !IsColor(role)).GroupBy(role => role))
            if (role.Count() > 1) return $"{role.Key} stands in one channel, and the layout names it {role.Count()} times.";

        bool x = channels.Contains(PictureRole.NormalX), y = channels.Contains(PictureRole.NormalY), z = channels.Contains(PictureRole.NormalZ);
        if (x != y || (z && !x)) return "The normal's X and Y stand together, with its Z beside them or rebuilt.";
        return null;
    }

    private static string Name(PictureRole? role) => role?.ToString() ?? "nothing";
}
