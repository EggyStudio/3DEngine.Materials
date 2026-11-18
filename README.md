# Materials

Materials is a library both engines, 3DEngine and BevyCSharp, take as a git submodule, for what
a surface is, a material's description, the pictures it is drawn with and their encoding, and the
response of a lit surface to its light, written once for both. It references no engine, and each
engine implements the interfaces it declares where it binds the module.

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
