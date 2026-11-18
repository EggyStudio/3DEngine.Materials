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

## Replies

None yet.
