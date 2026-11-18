# Materials

[Devlog](DEVLOG.md) [Cheat sheet](CHEATSHEET.md)

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
| `MaterialDescription` and `AlphaMode` | What a surface is in glTF 2.0's terms, its base color, metallic, roughness, reflectance, emitted light, normal scale, occlusion strength, diffuse transmission, alpha mode and pictures, with every rule a description breaks |
| `Reflectance` | A dielectric's reflectance at normal incidence, 0.04 by default, with glTF's index of refraction and the perceptual reflectance whose square times 0.16 it is as functions of it |
| `MaterialMeans` | A material's mean color in linear light and its mean surface over what its opacity covers, read through `IMaterialFiles` from a level of 64 texels a side at most |
| `IMaterialFiles` | The one thing the module asks of an engine, a file's bytes by its path |
| `SurfaceResponse` | How a lit surface sends its light back, GGX's distribution, Smith's height-correlated visibility, Schlick's Fresnel, Lambert's and Burley's diffuse, the reflectance at normal incidence by the metallic, Karis's fit of the split sum and the split sum integrated, and the two forms a probe's ray takes a surface in, `SurfaceResponse.HitForm` |
| `Prototypes` | The one table of prototype materials the engines block a level out with, seven parts of a level by name, their colors, the grid of a meter's cells with its lines and its `1m` mark, a prototype's picture tinted in linear light as a level editor shows it, and a prototype as a `MaterialDescription` |
| `ColorSpace` | Whether a picture's red, green and blue are sRGB or linear, its alpha linear in either |
| `Ktx2` | A KTX2 file's format, color space, size and levels, read from its bytes, the levels kept as they are |
| `Texels` and `PictureFormat` | RGBA8, BC1, BC4, BC5 and BC7 in its eight modes, decoded to four bytes a texel as a GPU samples them |

The decoders give what Pillow's give for 412 blocks, every partition of BC7's and pictures whose
sides are not multiples of four among them (`dotnet test Materials.Tests`, against the reference
`scripts/blocks.py` writes).

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

The module declares one interface, `IMaterialFiles`, a file's bytes by its path, which
`MaterialMeans` reads a material's pictures through, so the engine's files stay the engine's, a
folder, an archive or an asset store. It draws nothing, opens no window, opens no file itself and
keeps no thread, so a binding is the engine's code that reads a picture's bytes, gives them to the
module and binds what it returns to the engine's own textures and materials. The library references
no engine, window or graphics API, which a test of its own holds.

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
- A color in numbers, as `MaterialDescription.BaseColor`, is linear, the BT.709 primaries with a
  D65 white and no transfer curve, and a mean of a picture's color is taken in linear light.
- The emitted light, `MaterialDescription.Emissive`, is linear RGB in candelas a square meter.
- A dielectric's reflectance is the share it reflects at normal incidence, 0.04 by default, and
  glTF's index of refraction and a perceptual reflectance are each one function of it in
  `Reflectance`.
- A decoded texel is four bytes as a GPU samples the format, a channel the format does not hold
  reading zero and an alpha it does not hold 255.
- `SurfaceResponse` gives each term in radiometric form, the diffuse term the albedo over pi, so
  an engine whose lights are given as the light a white diffuse surface returns multiplies them by
  pi.
- The module holds no length and no direction, so it stands on no unit of length, no up axis and
  no handedness.

The smallest binding reads a picture once, gives it to the GPU and takes the material's means, in
a file that references the assembly `3DEngine.Materials` and opens the namespace `Materials`.
`host` stands for the engine's own calls, whatever it names them, and implements `IMaterialFiles`.

```csharp
// Once, as the engine loads a material's pictures.
var albedo = new MaterialPicture("textures/bark/albedo.ktx2", PictureLayout.Color);
var picture = Ktx2.Read(host.ReadAllBytes(albedo.Path));

// The levels go to the GPU as they are where it samples the format and decoded where it
// does not, through a view of the layout's color space.
var format = host.Samples(picture.Format) ? picture.Format : PictureFormat.Rgba8;
var levels = format == picture.Format
    ? picture.Levels
    : Enumerable.Range(0, picture.Levels.Count).Select(picture.Decode).ToList();
var texture = host.CreateTexture(format, albedo.ColorSpace, picture.Width, picture.Height, levels);
host.BindBaseColor(texture, albedo.Layout.ChannelOf(PictureRole.Opacity));

// The material's means, which a far card or a field of the scene's light is painted
// with, its pictures read through the host as IMaterialFiles.
var material = new MaterialDescription { Pictures = [albedo] };
var means = MaterialMeans.Of(material, host);
host.SetMeanColor(texture, means.Color);
```

A desktop GPU samples BC1, BC4, BC5 and BC7 as they are, and one that does not, a phone's among
them, takes the decoded levels. An engine binds a surface picture of `PictureLayout.Surface` as
glTF's occlusion and metallic-roughness textures at once, since their channels are glTF's, and
finds a role's channel in another layout by `PictureLayout.ChannelOf`. 3DEngine and BevyCSharp are
two such bindings, the first choosing a texture's view by the slot it is bound to and the second
taking the color space from the caller.

## A texture collection

`scripts/textures.py` builds a collection of KTX2 pictures from the sources its manifest names, a
`textures.json` in the collection's folder, by the recipe it gives each entry. An entry is an albedo
in BC7 and sRGB, a normal in BC5 and a surface in BC7 laid out as `PictureLayout.Surface`, with a
bark's depth in BC4, each with its levels down to a texel and none supercompressed. A tile tiles at
its size, a scan made into one from its repeat, and a bark, a leaf atlas and a grass card are parts
placed on the picture, a cut-out's levels keeping its leaves in the distance. Each build writes the
files' sizes, the encoders' error and the opaque texels' mean color and surface back into the
manifest. It needs Python 3 and Pillow, and its docstring gives the recipe.

```bash
python3 -I scripts/textures.py <collection>/textures.json [names] [--sources DIR] [--out DIR]
python3 -I scripts/textures.py --sample DIR
python3 -I scripts/textures.py --repeat FILE DX DY
```

A source is a folder under `--sources`, the repository's root where none is given, and `--out`
writes the entries elsewhere with the collection and its manifest left as they are. `--sample`
writes an entry made from no source, the one the tests read, and `--repeat` prints the shift near a
guess at which a scan matches itself.

## Known limits

- `scripts/textures.py` bleeds a surface by its own alpha, which holds the translucency, where the
  entry's kind is not cut out, so a tile or a bark whose translucency is nothing in places takes
  its neighbors' occlusion, roughness and metallic there. No set of the two collections has such a
  surface.

## Building and testing

The tests take the checks every module keeps from the Tools module, a git submodule under
`modules/Tools`, so a clone fetches it with `git clone --recurse-submodules`, or with
`git submodule update --init` in a clone made without it. With the .NET 10 SDK:

```bash
dotnet build Materials.slnx
dotnet test Materials.Tests
```

## License

The Mozilla Public License 2.0, in [LICENSE](LICENSE).
