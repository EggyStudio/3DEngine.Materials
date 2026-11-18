# Review

Reviewed up to `7d0a427`, the repository's first commit, which holds the license alone. No run yet,
the workflow coming with item 2.

## Now

The module holds what a surface is for both engines, written once. Three things stand twice or apart
today and are its first content. The texture encoders stand in Modeling and in Vegetation. A level's
prototype pictures are found by Levels and painted by Modeling's colors. A lit surface's response
differs between the engines, GGX at an `f0` of 0.04 in 3DEngine against a split sum at `0.16 *
reflectance^2` in BevyCSharp, which RadianceCascades holds as a constant until one is chosen. The
order is the other modules', the documents, the skeleton, the code, then the engines and the modules
taking it.

1. **The documents, one commit.** AGENTS.md, README.md, `.github/STYLE.md`, `COMMITS.md` and
    `CODE_OF_CONDUCT.md`, `.gitignore` with `/.github/dump/` in it and `.gitattributes`, each as
    Landscape's and Sky's have them with the module's name in place, AGENTS.md's DEVLOG.md row
    linked once the file exists. Nothing is written that the module does not have yet. This commit
    is the first test of the commit form here (N 7.2 and N 7.6), and it carries NORM.md and this
    file.

2. **The skeleton, one commit after item 1.** `Materials.slnx`, `Materials/Materials.csproj` as a
    class library on net10.0, `Materials.Tests/` on xunit with a test that the library loads and the
    test of N 4.11 that reads the sources for a comment line past 100 columns, `build/version.txt`
    at `0.1` with `build/version.sh`, and `.github/workflows/build.yml` on a push with `test (os)`
    on ubuntu-24.04, windows-latest and macos-15, the strict build and the suite, as the other
    modules' workflow has it. Green on the three systems before item 3.

3. **The first Reply, written beside items 1 and 2.** What the module holds and what it leaves, read
    from the trees and not from memory. It reads Modeling's and Vegetation's texture encoders and
    says where they differ, Levels' `ILevelMaterials` and `LevelPictures` with Modeling's prototype
    colors, both engines' material types and what each draws a surface with, and the two surface
    responses RadianceCascades names in its Reply. It proposes the module's types as plain values
    and arrays (a material's description, its pictures' roles and color spaces, the surface response
    as functions in C# and in Slang if both are needed), the interface an engine binds through, and
    an order of parts, one commit each with its tests. The review answers, and that order stands.

4. **The modules and the engines take it.** A part another module holds today moves here by that
    module's session deleting its copy after this module's part is pushed, asked by message; each
    engine binds the module as a submodule under `modules/` in one commit of its own session. No
    package is published (Decision 2).

## Verdicts

None yet.

## Decisions

1. **The repository's birth.** The repository was made on 2026-11-18 as the eighth module, after the
    seven module sessions' first reports showed the texture encoders held twice, a level's prototype
    pictures reaching across two modules and the engines' two surface responses. It holds what a
    surface is as one library both engines use.

2. **A git submodule, not a package.** As in the other modules, the engines take the module as a git
    submodule under `modules/` and reference its project. No package id is published from here.

3. **The module knows no engine.** N 2.1. The module references no engine, package or bridge and no
    other module; what it needs of an engine is an interface the engine implements, and what it
    gives is plain values and arrays.

4. **The repository's terms are the owner's.** The license, the repository's settings and AGENTS.md
    beyond a row for a new path change by the owner's word alone (N 7.4).

5. **The module is worked by a session of its own.** From its first commit the module's session
    alone writes the module's source, tests and documents. Another session that needs something of
    the module asks its session in a message and takes the commit by moving its submodule's pointer.
    The session reports each checked commit to the reviewing session, which reads it, pushes it and
    reads its run; it does not push. The rules are NORM.md's, among them tests that need no window
    and no GPU, the commit's form, a failing test before a mend, a picture on a white ground with a
    title where a thing is seen, any GPU program through the lock of N 7.9 with a waiting file, and
    caches and work folders of its own (N 7.10).

6. **The surface response is chosen by a room against its reference, not here.** The module may hold
    both engines' responses as functions; which one both engines light by is settled in the kernels'
    move by a room read against its traced reference, and the module follows that result.

7. **The module is agnostic enough that a stranger binds it in an engine of their own from its
    README alone (2026-11-18 17:28).** It asks a README section, Binding it in your engine, with
    every interface the module declares, the conventions it stands on and the smallest binding that
    works, a small host in the tests that implements those interfaces with no engine and that the
    section's steps are run against, a test that fails where the library references an engine, a
    window or a graphics API, the form in the Tools module's ReferenceTests.cs, and no engine's name
    in a public member or its summary. The two engines appear in the README as examples. A public
    shape that fits the two engines alone is named in a Reply before it changes.

8. **The package id and the assembly are 3DEngine.Materials and the namespace stays Materials
    (2026-11-18 17:28).** The prefix gives a unique name. A package is published when the time is
    right and on the owner's word, for a reader who takes the module alone. The engines keep taking
    the module as a git submodule, compiled with them and carried inside each engine's one package.
    Folders and project file names stay.

## Replies

Part 2 in the commit carrying this line, `scripts/textures.py`, one encoder of Modeling's and
Vegetation's, Vegetation's recipe of parts with Modeling's tiles, its opaque blocks' shared bits and
its levels kept as they are. Built from Modeling's sources with `--out`, it writes Modeling's six
files byte for byte (`cmp` against `Modeling/Textures`), and its sample is Vegetation's but for the
226 opaque blocks of the albedo's top level, each of which decodes to 255 where Vegetation's gave
254, the 30 blocks with transparency and the normal, surface and depth the same. The tests read the
sample through `Ktx2` and hold the script to its bytes where Python 3 and Pillow are found, skipped
with the reason elsewhere, and the opaque test failed on Vegetation's sample with 3,616 texels at
254, as the plain test did on a zstd copy of the normal. The merge found two faults the two scripts
share. The BC7 encoder starts its power iteration at (1, 1, 1, 1), which a block whose red and blue
move against each other turns to nothing, so the sample's ringed albedo encodes as flat blocks at
23.7 dB in all three scripts; the commit after this one mends it with its failing test, changing
both collections' bytes. A surface is bled by its own alpha, its translucency, where its kind is not
cut out, so a tile or a bark whose translucency is nothing in places would take its neighbors'
occlusion, roughness and metallic there; no set of either collection has such a surface, and it
stays open for a commit with its test. Vegetation's `TreeTextureSet.MeanColor` is documented as
linear sRGB, and both scripts write the mean of the encoded values, which part 3's `MaterialMeans`
settles.

The fresh read of `6f93fe6`, its seven findings mended in the commit carrying this line and none
refused. A level count is compared with what the picture's size holds, the base-two logarithm of its
longer side and one, before it is cast, so 2^31 levels and a fourth level of a 4 by 4 picture are
refused as data. `Texels.ByteCount` counts in 64 bits, and `Texels.Decode` refuses a picture whose
texels pass `Array.MaxLength`, as 32768 by 16384 does. The glTF layout test reads the channels as
numbers, which a swap of roughness and metallic in both layouts fails. The fixture counts mode 4's
rotations with its index selection and mode 5's rotations through, and holds pictures of 7 by 3, 5
by 6 and 6 by 5, 412 blocks in all. The remark on rounding says BC1's and BC4's are rounded down as
Pillow's are, where the Khronos Data Format Specification gives exact thirds, fifths and sevenths,
so a GPU's may differ by one. Each fault had its failing test first, and the strict build is clean
with 77 tests passing (`dotnet test Materials.Tests`).

Item 3's first Reply, read against the trees at Modeling's `163b218`, Vegetation's `f6579d6`,
Levels' `67b5dca`, RadianceCascades' `8d11f8c`, 3DEngine's `ee7ac712` and BevyCSharp's `8e7fa0f5`,
with the `bevy_pbr` 0.20 BevyCSharp draws with. The module has no public member yet, so no shape of
its own fits only the two engines; the README's opening names both, which the binding direction's
first commit turns into two examples.

**The texture encoders.** Modeling's `build/textures.py` is Vegetation's carried over, as its
docstring says, and the two share the encoding whole, BC7's mode 6 fitted along each block's
principal axis and again by least squares, BC5 as two BC4 blocks, box-filtered levels, the KTX2
header and its data format descriptor, and a recipe's `flip_green` for a source whose normal's Y
points down. They part in five places.

- Vegetation compresses each level with zstd at level 19 (scheme 2), which needs Python 3.14, and
  Modeling keeps the levels as they are, each at a multiple of sixteen bytes, since .NET's library
  holds no zstd. 3DEngine's KTX2 reader refuses a supercompressed level
  (`Ktx2TextureDecoder.cs:31`), so it reads Modeling's sets and none of Vegetation's. Vegetation's
  43 files hold 4.7 MiB stored against 6.6 MiB uncompressed, read from their level indexes, and
  Modeling's 6 hold 3.0 MiB.
- Modeling's `bc7_block` keeps the shared bit set in a block opaque throughout, so its alpha decodes
  to 255. Vegetation's takes the bit the color chooses, so an opaque block can decode to 254. The
  mend is Modeling's, and Vegetation's sets carry the fault, the top levels of its four barks
  holding 179 to 376 blocks whose alpha is 254 throughout, 1.8 to 2.3 percent of their blocks, read
  from the mode 6 blocks' alpha endpoints and shared bits.
- Vegetation alone writes BC4, a bark's depth for parallax occlusion, worked out from the normal
  map's slopes through a Fourier transform where the source has no height.
- Vegetation's levels keep a cut-out's leaves, the color and the surface weighted by the leaves'
  cover, the bare texels bled from the nearest leaf and the opacity scaled to keep each leaf box's
  share past one half. Modeling's levels are a plain box filter, and its recipes make tiles, a map
  resized with its edges wrapped and a scan averaged over its repeat.
- Vegetation embeds its collection and gives a set as `TreeTextureSet`, with its mean color and mean
  surface. Modeling's library carries neither, and its tests decode BC7 and BC5 in C#
  (`TexturesTests.cs:120,152`).

A third encoder stands in BevyCSharp's `build/make-scene-pack.py` (BC1, BC4, BC5, BC7's mode 6 and
KTX2 with zstd), whose packs 3DEngine's script of the same name takes with the zstd removed, and
both engines decode BC for a texture's mean, 3DEngine on the GPU and BevyCSharp in Rust
(`field_means.rs`).

**A level's prototypes.** Levels finds a material's picture by its name (`LevelPictures.Find`),
`proto/floor` as the `floor` prototype at 512 texels a side and any other as a PNG or JPEG under
`textures/`, and says its size through `ILevelMaterials.PictureSize`, its one interface. Modeling's
`SlotLook` draws a slot given nothing as the prototype of its name and holds no color, since the
engine holds the prototypes. The colors and the grid stand in each engine, 3DEngine's
`E3D.Prototypes.cs:23-33` and BevyCSharp's `Modeling.Prototypes.cs:20-29`, rough at 0.85 and not
metal in both. Stairs, door, window and hazard agree to the byte. Floor, wall and ceiling do not,
3DEngine's grays of 79, 181 and 209 against BevyCSharp's sRGB 0.90, 0.90 and 0.95, which its remarks
keep near white since a gray reads dull under the sky. The grid is one design at 1024 pixels a meter
in both, a meter's line a 128th of the cell with the half's, the quarters' and the `1m` mark, each
in tones of its own, 3DEngine's face, line and fine 230, 89 and 173 against BevyCSharp's 0.99, 0.6
and 0.82. BevyCSharp alone writes the pictures a level editor shows, tinted in linear light
(`LevelSpawner.cs:175`), and 3DEngine takes no level yet.

**What each engine draws a surface with.** 3DEngine describes a material in four types,
`ModelMaterial` in sRGB bytes, the ECS `Material` in linear numbers, and `MaterialDescription` and
`SceneMaterialPayload`, which no renderer reads. Its model pass carries the factors in 96 bytes an
instance (`ModelNode.cs:58-98`) and binds five maps, the base color and the emissive through an sRGB
view and the normal, the metallic-roughness (G and B) and the occlusion (R) linear, the view chosen
by the slot and the asset's color space never read (`MeshEntityDraws.Looks.cs:119-136`). It shades
by GGX with Smith's height-correlated visibility and Schlick's Fresnel, an `f0` of 0.04 mixed toward
the albedo by metallic, Lambert's diffuse weighted by one minus the Fresnel, roughness held at 0.045
or more and a split-sum table of its own, 32 by 32 (`lights.slang:356-375`,
`modelpass.slang:576-586`). BevyCSharp hands `MaterialSettings` across its bridge as Bevy's
`StandardMaterial` (`assets.rs:117-273`), so its lit pass is Bevy's shader and none of its own. That
is GGX with the same visibility and Fresnel, an `f0` of 0.16 times the reflectance squared, 0.04 at
the default of 0.5, Burley's diffuse, a term for multiple scattering, roughness held at 0.089 or
more and Bevy's DFG table. It binds a set's surface picture as the occlusion, the metallic-roughness
and the diffuse transmission at once, with factors of one (`Trees.Materials.cs:53-63`), and takes a
picture's color space from the caller, since Bevy's KTX2 loader ignores the file's. 3DEngine's flat
path writes a glTF's linear base color factor into its sRGB bytes unencoded (`E3D.Models.cs:198`),
which `ModelNode.cs:85` decodes as sRGB, so a factor of 0.5 draws at 0.21, where the ECS path
encodes first; that fault is the engine's.

**The two surface responses at a probe's hit.** Both read a cell's paint and gloss, and both take
the light from all around by Karis's fit of the split sum, with the same constants in
`probelight.slang:479-485` and `bcs_gi.slang:667-672`. They part in two places. 3DEngine's `f0` is
0.04 for every dielectric (`probelight.slang:504`), and BevyCSharp's is 0.16 times the cell's
reflectance squared (`bcs_gi.slang:760`), the same at the default and 0.0144 at the 0.3 a tree's far
card sets (`Trees.CardPictures.cs:207`). 3DEngine reflects a lamp at a hit by the GGX lobe
(`gi.slang:394-408`), and BevyCSharp by the split sum's share, the diffuse and the glossy together
times the light (`bcs_gi.slang:765`). The field keeps a paint and a gloss a cell, a byte a channel,
3DEngine's gloss the roughness and the metallic with the light let through in the paint's alpha, and
BevyCSharp's the roughness, the metallic, the reflectance and the transmission.

**What the module holds.** Plain values and arrays, named in glTF 2.0's metallic-roughness terms and
its extensions' where it goes past them, since every engine reads glTF, so an engine of another
shading model reads a description by the quantities it names rather than by a response.

- `MaterialDescription`, a record of the base color in linear RGBA, metallic, roughness as glTF's
  perceptual one, the dielectric's reflectance at normal incidence (0.04 by default, of which glTF's
  ior of 1.5 and Bevy's remapped 0.5 are each one function), the emissive color in linear numbers
  with its luminance in candelas a square meter, the normal's scale, the occlusion's strength, the
  diffuse transmission (`KHR_materials_diffuse_transmission`), glTF's three alpha modes with the
  mask's cutoff, whether it is double-sided, and its pictures.
- `MaterialPicture`, a picture's path, its format and the roles it is bound in, a channel each, so
  one surface picture is the occlusion in R, the roughness in G, the metallic in B and the diffuse
  transmission in A. A role fixes its color space, sRGB for the base color and the emissive and
  linear for the rest, so no engine chooses it at each call. The normal is tangent-space with its Y
  up the picture as glTF's, two channels in BC5 with Z rebuilt, and the depth is one channel, white
  the deepest.
- `Ktx2` and the block decoders, a file's format, size, color space and levels read from its bytes,
  and BC7, BC5, BC4 and BC1 decoded to RGBA bytes, levels kept as they are read and supercompressed
  ones refused with the reason.
- `MaterialMeans`, a material's mean color over its opaque texels and its mean surface, which a
  field paints a cell with and a far card draws with, worked out once where 3DEngine, BevyCSharp and
  Vegetation each work them out today.
- `Prototypes`, the seven names and their colors, rough at 0.85, the grid's pixels at a size and a
  prototype's picture tinted in linear light, with one table of colors, which asks the review's word
  between the two. BevyCSharp's is proposed, whose remarks give its reason, and it turns 3DEngine's
  floor, wall and ceiling near white.
- `SurfaceResponse`, the response as C# functions, GGX's distribution, Smith's height-correlated
  visibility, Schlick's Fresnel, Lambert's and Burley's diffuse, Karis's fit, the split-sum table
  and both hits' forms behind a choice each engine sets, tested against values worked by hand. It
  has no Slang, since BevyCSharp's lit pass is Bevy's and the hits' kernels move into
  RadianceCascades, which holds its own form until Decision 6's room chooses, and a Slang file comes
  when a kernel there asks for one.
- `scripts/textures.py`, one encoder of the two, with Modeling's opaque bit and tiles and
  Vegetation's BC4, depth and cut-out levels, run on a module's manifest by its path as Tools'
  scripts are, writing levels as they are by default, which both engines and .NET read, and zstd
  where a manifest asks for it.

**What it leaves.** The texture sets, their manifests and their sources stay with the module whose
work they serve (Modeling's Decision 8), Levels keeps its file's conventions (`proto/`, `textures/`
and the 512 texels), each engine keeps its own material type and the flags only it draws by
(3DEngine's shadows, `BouncesLight` and subsurface, BevyCSharp's clearcoat, anisotropy, specular
transmission and texture transform), and the scene packs' encoders stay the engines' until the
review asks for them.

**The interface.** The module declares one, `IMaterialFiles`, a picture's bytes by its path, which
`MaterialMeans` reads through, and an engine binds the rest by mapping a description to its own
material. No module takes another (Decision 3), so Modeling and Vegetation run the script by its
path in a checkout of this repository on the machine that imports, and their tests keep their own
readers, and Vegetation's `TreeTextureSet` stays its own, a species' files, which an engine maps to
a description. Whether a module may take this one as a submodule, so Modeling's tests and
Vegetation's means read through `Ktx2`, is the review's to say.

**The order.** One commit a part, each with its tests.

1. `MaterialPicture`'s roles, channels and color spaces, and `Ktx2` with the decoders, tested on
    files built in the test.
2. `scripts/textures.py` with a sample set it writes under `Materials.Tests/references`, read back
    through `Ktx2` against the means it records, its bytes held where Python 3 and Pillow are found
    and the test skipped with the reason elsewhere (N 3.2).
3. `MaterialDescription` with the reflectance's conversions, and `MaterialMeans` through
    `IMaterialFiles`.
4. `Prototypes`.
5. `SurfaceResponse`.

The binding direction's four commits follow this Reply's, the README's section and the host growing
with each part that declares an interface. Item 4 comes after part 2 for the script, Vegetation's
collection imported again with its bytes changed by the opaque bit and its levels kept as they are,
and each module's copy deleted by its session.
