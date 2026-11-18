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

The third of the three in the commit carrying this line, `MaterialImport.FromGltf`, a glTF's
materials read into descriptions, from its JSON or from a root already parsed with a material's
index, taking over the material work of Modeling's reader. It reads what that reader reads, glTF's
defaults with a metallic and a roughness of one, the emissive factor times
`KHR_materials_emissive_strength` as `Unitless`, the alpha mode and cutoff, double-sided and the
base color, metallic-roughness, normal, occlusion and emissive textures, an image in a data URI
giving no picture, and adds `KHR_materials_ior` as the reflectance and
`KHR_materials_diffuse_transmission` with its texture's alpha. Roles of one image are one picture
where their channels do not meet, so an ORM texture is one picture of occlusion, roughness and
metallic. Modeling's reader then drops its `Look` and the `Image` inside it and calls the import
with its root, the material's index and the file's path, keeping its `Joined` for its buffers. Three
things it does change for Modeling's files. A factor out of glTF's range is refused with the rule
where the reader clamped it, an alpha mode glTF does not name is refused where the reader took it as
opaque, and an image whose path holds a space keeps its picture where the reader dropped it. The
tests failed first at the build, the type being absent, and the test of a rooted image's path came
with its mend in the same pass. The strict build is clean with 132 tests passing (`dotnet test
Materials.Tests`).

Mode 5 measured and left out. A trial of it, one subset with seven-bit color and eight-bit alpha
ends each on indices of its own, its bits checked against Pillow, was set against modes 6 and 7 with
the best kept for each block of 64 by 64 texels. On lettering over a see-through ground modes 6 and
7 give 35.75 dB in color and 35.42 in alpha and mode 5 adds nothing, 0% of the blocks taking it, and
on a smooth see-through glass whose color runs one way and its opacity another mode 6 alone gives
50.85 and 46.37 dB and 0% take mode 5. Its one line of color loses more where strokes cross a varied
ground than its finer alpha gains, and mode 7 holds the see-through ground where it needed help, so
the module carries no mode 5.

The second of the three in the commit carrying this line, BC7's mode 7 for a block with alpha.
Measured first, Vegetation's leaf sets decoded and encoded again come back at 63 to 68 dB, which
measures nothing, their blocks being mode 6's lines already, and its manifest records their first
encoding from the sources at 29.6 to 35.7 dB, the silver birch at 46.1. A drawn atlas of leaves with
veins and a gradient, cut out and bled as the build bleeds them, gave 32.46 dB over its visible
texels' color with mode 6 alone, its alpha at 56.43. A block that is not opaque throughout whose
mode 6 error passes a mean of 4 a channel is tried in mode 7's two subsets with alpha, each end five
bits a channel and a bit of its own, of the two partitions whose texels lie nearest the lines
through them in RGBA, and kept where mode 7 holds it closer. The atlas's visible color goes to 44.20
dB and its RGBA from 28.35 to 34.26, its alpha to 47.67, far inside what a cutoff of one half reads,
and lettering over a see-through ground goes from 27.22 to 35.93 dB over RGBA, its alpha from 23.49
to 34.99, before mode 5 is tried. A block tried in mode 7 costs about 13 times what mode 6 alone
does, measured under a load of 28 that slowed every timing alike. As an estimate and no measurement,
Vegetation's leaves rebuilt from their sources should rise by about the drawn atlas's 6 dB over
RGBA, to about 35 to 42 dB. The test of the drawn atlas at 40 dB or more over visible color through
a leaves manifest failed first at 32.46, Pillow decodes mode 7's blocks as the encoder means them,
and Modeling's six files keep their bytes, opaque throughout. The sample's albedo changes at its
hole's edge and is written again. Tools is pinned at `1e1ec63` by its pin script, no check new or
changed, and the devlog leaves out the old sentence of two words. The strict build is clean with 124
tests passing (`dotnet test Materials.Tests`).

The first of the next three in the commit carrying this line, the build's time read before anything
is ported. Under the profiler Modeling's build spent 65% in mode 1's partition search, which for
each of 64 partitions summed each subset's covariance from its colors, 19% in mode 6 and 10% in mode
1's full encoding. The search sums each color's moments once a block and a subset's covariance from
them, the second subset's as the block's less the first's, and Modeling's build goes from 94 s to 44
s (`time python3 -I -B scripts/textures.py Modeling/Textures/textures.json --out`), its six files
the same bytes as before (`cmp`), the sample's held by its test. After it mode 1 takes 52% of the
build, half the search and half the full encoding, and mode 6 36%, so no one cost stands far over
the rest and nothing more is mended here. Tools is pinned at `dc117ac`, and the cheatsheet written
again changes six lines, the tuples of `PictureOf`, `Decode`, `SplitSum`, `SplitSumIntegrated`,
`HitShares` and `LevelSize` with their members' names. The strict build is clean with 123 tests
passing (`dotnet test Materials.Tests`).

The third of the three in the commit carrying this line, the encoder by content and BC7's mode 1.
Measured first, mode 6 holds a two-color edge at 49.38 dB, its two colors on one line, and loses
where a block's colors leave one line, three flat colors a block at 16.04 dB, two gradients a block
at 24.12 and lettering on a varied ground at 30.96. An opaque block whose mode 6 error passes a mean
of 4 a channel is tried in mode 1's two subsets, of the two partitions whose colors lie nearest the
lines through them, and kept there where mode 1 holds it closer, which takes those three to 54.15,
44.49 and 37.78 dB and leaves the edge and a gradient as they were. Modeling's carpet goes from
45.27 to 46.40 dB and its wallpaper from 46.16 to 49.44, 12.8% and 9.8% of their blocks in mode 1,
at 0.93 and 0.51 ms a block where mode 6 alone took 0.12, so Modeling's build takes 94 s. `psnr_bc7`
decodes each block in its own mode, and Pillow decodes mode 1's blocks as the encoder means them.
The test of three colors and of two gradients a block at 40 dB or more failed first at 14.79, and
the reading `2026-11-18-item-3-bc7-modes.webp` shows the content kinds before and after. Modeling's
two albedos change and its normals and surfaces keep their bytes, and Vegetation's albedos change
where an opaque block is tried, its normals, surfaces and depths the same, the surfaces' alpha being
their translucency. The strict build is clean with 123 tests passing (`dotnet test
Materials.Tests`).

The second of the three in the commit carrying this line, `MaterialFile`, a material as a JSON file
of its own beside its pictures, `format` `material/1`, every factor under its name in camel case, a
field left out at its default, a layout by a name of `PictureLayout`'s or four roles, and the
pictures' paths relative to the file's folder, joined on reading and climbing by `..` on writing. A
field the format does not have, a word it does not know, a broken layout and a broken rule are
refused with the reason. `MaterialDescription` compares by its factors and its pictures in order, so
a description written and read again is the same one. The tests were written after the type, where
the order asks them first, and two trials show they fail, the path joining taken out and the
equality made to skip the pictures failing three of them. The strict build is clean with 122 tests
passing (`dotnet test Materials.Tests`).

The first of the next three in the commit carrying this line, a picture of a format the module does
not read. `IMaterialFiles.Decode(path, file)` is a method with a default that decodes nothing, so no
binder breaks, and `MaterialMeans` asks it for any file `Ktx2` refuses, a supercompressed KTX2 among
them, naming the path where the engine decodes none of it either. The README says what `Decode`
owes, four bytes a texel, rows from the top, straight alpha and each channel as the file stores it,
sRGB or linear by the picture's role, and a test holds the rows and the numbers, a surface decoded
by the engine under a KTX2 albedo whose top row alone is opaque keeping that row's roughness of one.
The three tests failed first, the KTX2 reader refusing both pictures and the refusal naming no path.
The strict build is clean with 110 tests passing (`dotnet test Materials.Tests`).

The fit's sheet in the commit carrying this line, `2026-11-18-item-3-split-sum-fit.webp`, Karis's
fit against the split sum integrated, the share reflected by roughness and cosine for a dielectric
of f0 0.04 and a white metal of f0 1, with the signed difference. Rays arriving at random meet a
surface at a cosine whose density is twice itself, which no engine is needed to say, and over such
hits the dielectric's difference has a mean of 0.0067, 15.8% of the hits past 0.01 and the largest
0.084 at a roughness and a cosine of 0.12, and the metal's a mean of 0.10, 95.6% past 0.01 and the
largest 0.509 at a roughness of 0.99 seen at a cosine of 0.008. Where a probe's hits fall in
roughness is the scene's materials', which only an engine's scene gives. The integral at a roughness
of one seen head on is worked by hand as 0.3069, which a test holds the integration to and where the
fit gives 0.45. The choice between the forms stays open until the radiance cascades and both engines
have read the sheet. The strict build is clean with 107 tests passing (`dotnet test
Materials.Tests`).

The emitted light's unit in the commit carrying this line, the answer to Modeling's import.
`MaterialDescription.EmissiveUnit` names what `Emissive` is measured in, `EmissiveUnit.Luminance`,
candelas a square meter and the default, or `EmissiveUnit.Unitless`, glTF's emissive factor times
its strength as they stand, since glTF gives the product no unit and no one luminance for a strength
of one serves every exposure. An importer writes the product with `Unitless`, and an engine takes a
luminance as it is and scales a unitless emission by a convention of its own. The test that a glTF
emission is kept with its unit named failed first at the build, the member being absent, and the
README's conventions say it. The strict build is clean with 106 tests passing (`dotnet test
Materials.Tests`).

The one tone in the commit carrying this line. The floor, the wall and the ceiling are one color,
sRGB 0.90, 0.90 and 0.89, the tone two of them held, the five other colors and the grid as they
were, the test that the three give one color and one picture failing first on the ceiling's 0.95.
The ceiling's stored hash is the floor's, the reading `2026-11-18-item-3-prototype-tables.webp` is
made again, and the cheatsheet and the devlog are written again. Tools is pinned at `75d77f2` with
its seven facts in their form, `No_Tracked_Text_Names_An_Engine` and
`The_Readme_Opens_With_Its_Link_Row` passing on their first run, and the cheatsheet is one word
throughout. The strict build is clean with 105 tests passing (`dotnet test Materials.Tests`).

The sweep in the commit carrying this line, the module naming no engine and describing no engine's
form or table, the package and assembly name apart. The reviewer's `git grep` found 9 lines before
and finds none after, the README's opening and binding section, AGENTS.md's opening, the prototype
reading's caption, the devlog drawn from it and a test's comment, and the comments of the workflow,
`Directory.Build.props` and the library's project that spoke of the engines are reworded. The
prototype reading shows the one table alone. `SurfaceResponse`'s floors are named for what they are,
`LeastRoughnessFullPrecision` at 0.045 and `LeastRoughnessHalfPrecision` at 0.089, Filament's two,
and the README says what each `HitForm` is for. `scripts/textures.py` writes a set's mean color as
the mean of its opaque texels in linear light written in sRGB, as Vegetation's `cb2ab62` does, its
test failing first at 0.5 where 0.7354 belongs for a tile half white and half black. The strict
build is clean with 102 tests passing (`dotnet test Materials.Tests`).

Decision line to carry: the prototype table is `Prototypes`, BevyCSharp's colors and grid tones,
chosen by the owner on 2026-11-18 from `2026-11-18-item-3-prototype-tables.webp`. BevyCSharp's
bridge takes it by reading the table, the grid and the pictures from the module, its look and its
level editor's pictures unchanged, which the module's hashes hold. 3DEngine takes it by replacing
its own table, the floor, wall and ceiling grays of 79, 181 and 209, and its grid's tones of 230, 89
and 173 with the module's, its stairs, door, window and hazard keeping their colors and lightening
by the grid's face.

Part 5 in the commit carrying this line, `SurfaceResponse`, both forms written down as they stand
and no choice made, Decision 6's room choosing. It holds GGX's distribution, Smith's height-
correlated visibility, Schlick's Fresnel, Lambert's and Burley's diffuse, `F0` from the material's
reflectance, the general form, Karis's fit of the split sum and the split sum integrated by
importance sampling, each in radiometric form, and `HitShares` by `HitForm.Lobe`, a reflectance of
0.04 for every dielectric with the share held to one, and `HitForm.SplitSum`, the material's
reflectance with one less the share held to zero, with the floors of roughness of the two lit
passes, 0.045 and 0.089. The two forms give the same shares at a reflectance of 0.04, 0.015696 for a
rough gray head on, and part at 0.0144, a perceptual 0.3, where `SplitSum` gives 0.004115. Karis's
fit stands within 0.396 of the integrated split sum over a grid of 16 by 16, the largest at a
roughness of 0.97 and a cosine of 0.03, a grazing view of a rough surface, which both forms read at
a probe's hit; the integration returns a mirror's light whole head on and never more than it is
given, 1.00005 at most. The strict build is clean with 100 tests passing (`dotnet test
Materials.Tests`).

Part 4 in the commit carrying this line, `Prototypes`, one table of the seven parts' colors, the
grid of a meter's cell, a prototype's picture tinted in linear light and a prototype as a
`MaterialDescription`, rough at 0.85 and not metal. The table and the grid's tones proposed are
BevyCSharp's, whose remarks give their reason, and the seven pictures at 512 pixels are BevyCSharp's
level editor pictures byte for byte, held by their hashes. The reading `2026-11-18-item-3-prototype-
tables.webp` sets both engines' tables side by side for the owner, stairs, door, window and hazard
one color in both and darker in 3DEngine's for its grid face of 230 against 252, floor, wall and
ceiling its grays of 79, 181 and 209 against near whites of 0.90, 0.90 and 0.95. No table is final
until the owner has seen it, and neither engine changes its prototypes before. The strict build is
clean with 94 tests passing (`dotnet test Materials.Tests`).

Part 3 in the commit carrying this line. `MaterialDescription` holds glTF's factors with the
reflectance at normal incidence, 0.04 where nothing is given, the emitted light in candelas a square
meter and its pictures, and says every rule it breaks, a factor out of its range or a role two
pictures hold. `Reflectance` gives glTF's index of refraction and the perceptual reflectance of
`0.16 r^2` as functions of it, 1.5 and 0.5 both 0.04. `MaterialMeans.Of` reads a material's pictures
through `IMaterialFiles`, the one interface the module declares, from the first level of 64 texels a
side or fewer, the color in linear light and every mean weighted by the opacity as the alpha mode
reads it, which settles the mean color the two scripts wrote as a mean of encoded numbers. The
README names the interface and the conventions of color and light, and its binding block, run by the
host, takes a material's means through it. The strict build is clean with 86 tests passing (`dotnet
test Materials.Tests`).

The BC7 encoder's mend in the commit carrying this line. Its power iteration starts from the
covariance's column of the channel that varies most, where it started at (1, 1, 1, 1), which a block
whose red and blue move against each other turned to nothing, so the block encoded as its mean. The
sample's albedo goes from 23.70 dB to 34.30 dB and its mean error from the rings it is drawn from
from 13.71 of 255 to 1.64, the test holding it under 2 having failed at 13.71 first, and the reading
`2026-11-18-item-3-bc7-axis.webp` shows it whole and a block of it before and after. Modeling's
carpet holds at 45.27 dB and its wallpaper goes from 46.15 to 46.16, measured on the albedos built
from Modeling's sources with `--out`. Both collections are built again through `scripts/textures.py`
when their copies give way. Modeling's two albedos change, 622 bytes of the carpet's and 3,306 of
the wallpaper's, and its normals and surfaces are the same bytes. Every albedo and every surface of
Vegetation's 43 files changes, both being BC7, by this start and by the opaque blocks' shared bits,
and its normals in BC5 and depths in BC4 are the same bytes, those encoders being untouched. The
width test reads the project files, the workflow, `build`'s and `scripts`' files and the Python
scripts' comments as the Levels module's does, and failed first on `scripts/textures.py:500` and
`:881`, both rewrapped. The bleeding of a surface by its translucency stands in the README's known
limits.

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
