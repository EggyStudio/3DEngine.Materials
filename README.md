# Materials

Materials is a library both engines, 3DEngine and BevyCSharp, take as a git submodule, for what
a surface is, a material's description, the pictures it is drawn with and their encoding, and the
response of a lit surface to its light, written once for both. It references no engine, and each
engine implements the interfaces it declares where it binds the module.

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

A clone of the engine fetches the module with `git clone --recurse-submodules`, or with
`git submodule update --init` in a clone made without it, and a workflow checks it out with
`submodules: true` on `actions/checkout`. A change the engine uses is committed and pushed here
before the engine's commit that moves its pointer, so the engine's history never names a commit
this repository does not have.

## Building and testing

With the .NET 10 SDK:

```bash
dotnet build Materials.slnx
dotnet test Materials.Tests
```

## License

The Mozilla Public License 2.0, in [LICENSE](LICENSE).
