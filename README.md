# Materials

Materials is a library an engine takes as a git submodule, for what a surface is, a material's
description, the pictures it is drawn with and their encoding, and the response of a lit surface to
its light, in the terms of glTF 2.0 that any engine reads. 3DEngine and BevyCSharp are two engines
that take it. It references no engine, and an engine binds it by reading the values it gives into
its own textures and materials.

## What it holds

| Type | Gives |
|---|---|
| `PictureRole` and `PictureLayout` | What each channel of a material's picture holds, in glTF 2.0's terms, and the color space the picture is read in, sRGB where it holds a color and linear otherwise, with the layouts glTF binds and the texture collections write, the surface's occlusion, roughness, metallic and diffuse transmission among them |
| `MaterialPicture` | A picture a material is drawn with, its path among the engine's files and its layout |
| `Ktx2` | A KTX2 file's format, color space, size and levels, read from its bytes, the levels kept as they are |
| `Texels` and `PictureFormat` | RGBA8, BC1, BC4, BC5 and BC7 in its eight modes, decoded to four bytes a texel as a GPU samples them |

The decoders give what Pillow's give for 400 blocks, every partition of BC7's among them
(`dotnet test Materials.Tests`, against the reference `scripts/blocks.py` writes).

## Taking it into an engine

An engine adds the repository as a git submodule under `modules/` and references its project, here
from a project one folder below the engine's root.

```bash
git submodule add https://github.com/EggyStudio/Materials.git modules/Materials
```

```xml
<ProjectReference Include="..\modules\Materials\Materials\Materials.csproj" />
```

The project builds the assembly `3DEngine.Materials`, whose types are in the namespace `Materials`.

A clone of the engine fetches the module with `git clone --recurse-submodules`, or with
`git submodule update --init` in a clone made without it, and a workflow checks it out with
`submodules: true` on `actions/checkout`. A change the engine uses is committed and pushed here
before the engine's commit that moves its pointer, so the engine's history never names a commit
this repository does not have.

## Binding it in your engine

The module declares no interface, since it asks nothing of an engine. It draws nothing, opens no
window, reads no file and keeps no thread, so a binding is the engine's code that reads a picture's
bytes, gives them to the module and binds what it returns to the engine's own textures and
materials. The library references no engine, window or graphics API, which a test of its own holds.

It stands on these conventions.

- A picture's rows run from the top, as KTX2 and glTF lay them out, so a texture coordinate of
  (0, 0) is the picture's top left.
- The module's pictures are KTX2 files whose levels are kept as they are, with no supercompression,
  since .NET's library holds no zstd, so an engine on .NET alone reads every one and hands its
  levels to the GPU with no step between, and git compresses them in the repository all the same.
- A picture's red, green and blue are sRGB where its layout holds a base color or an emissive color
  and linear otherwise, and its alpha is linear in either. The layout fixes the color space by the
  roles, so an engine samples a picture through the view its layout names whatever its file's
  format says.
- A normal map is in tangent space with its Y up the picture, as glTF's and OpenGL's are, each
  component from zero for -1 to one for +1. An engine whose normal maps point Y down negates the
  green channel as it samples, and one holding X and Y alone rebuilds Z to make a unit length.
- Roughness is glTF's perceptual roughness, whose square is the GGX distribution's alpha.
- A decoded texel is four bytes as a GPU samples the format, a channel the format does not hold
  reading zero and an alpha it does not hold 255.
- The module holds no length, no direction and no light, so it stands on no unit of length, no up
  axis, no handedness and no unit of light.

The smallest binding reads a picture once and gives it to the GPU, in a file that references the
assembly `3DEngine.Materials` and opens the namespace `Materials`. `host` stands for the engine's
own calls, whatever it names them.

```csharp
// Once, as the engine loads a material's pictures.
var albedo = new MaterialPicture("textures/bark/albedo.ktx2", PictureLayout.Color);
var picture = Ktx2.Read(host.ReadAllBytes(albedo.Path));

// The levels go to the GPU as they are where it samples the format, and decoded where it does not,
// through a view of the layout's color space.
var format = host.Samples(picture.Format) ? picture.Format : PictureFormat.Rgba8;
var levels = format == picture.Format
    ? picture.Levels
    : Enumerable.Range(0, picture.Levels.Count).Select(picture.Decode).ToList();
var texture = host.CreateTexture(format, albedo.ColorSpace, picture.Width, picture.Height, levels);
host.BindBaseColor(texture, albedo.Layout.ChannelOf(PictureRole.Opacity));
```

A desktop GPU samples BC1, BC4, BC5 and BC7 as they are, and one that does not, a phone's among
them, takes the decoded levels. An engine binds a surface picture of `PictureLayout.Surface` as
glTF's occlusion and metallic-roughness textures at once, since their channels are glTF's, and
finds a role's channel in another layout by `PictureLayout.ChannelOf`. 3DEngine and BevyCSharp are
two such bindings, the first choosing a texture's view by the slot it is bound to and the second
taking the color space from the caller.

## Building and testing

With the .NET 10 SDK:

```bash
dotnet build Materials.slnx
dotnet test Materials.Tests
```

## License

The Mozilla Public License 2.0, in [LICENSE](LICENSE).
