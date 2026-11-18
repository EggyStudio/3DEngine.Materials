# Materials cheatsheet

Every public type of `3DEngine.Materials` with its members, a line each of its declaration and the
first sentence of its summary. A material is a `MaterialDescription` of glTF's factors and its
`MaterialPicture`s, each picture a `PictureLayout` of `PictureRole`s whose file `Ktx2` reads and
`Texels` decodes, its means taken by `MaterialMeans` through the engine's `IMaterialFiles`, a lit
surface's response written down in `SurfaceResponse`, and a level blocked out in `Prototypes`. The
README's section Binding it in your engine gives the conventions and the smallest binding.

<!-- written by Cheatsheet.Write from the built library -->
```csharp
enum PictureRole                                         // What a channel of a material's picture holds, in glTF 2.0's terms and its extensions'
BaseColor = 0,                                           // The base color, the albedo of a dielectric and the reflectance of a metal, sRGB
Opacity = 1,                                             // How opaque the surface is, one for opaque, as glTF's base color alpha
NormalX = 2,                                             // The tangent-space normal's X, toward the texture's U, from zero for -1 to one for +1
NormalY = 3,                                             // The tangent-space normal's Y, up the picture as glTF's and OpenGL's normal maps have it, from zero for -1 to one for +1
NormalZ = 4,                                             // The tangent-space normal's Z, toward the viewer from the surface, or nothing where a picture holds X and Y alone and Z is rebuilt to make a unit length
Occlusion = 5,                                           // How much of the light from all around reaches the surface, one for all of it
Roughness = 6,                                           // The perceptual roughness glTF names, its square the GGX distribution's alpha
Metallic = 7,                                            // How metal the surface is, one for a metal
DiffuseTransmission = 8,                                 // The share of light let through the surface and scattered, as glTF's KHR_materials_diffuse_transmission names it, a leaf's translucency
Emissive = 9,                                            // The light the surface emits, its color and strength, sRGB
Depth = 10,                                              // The depth below the surface for parallax occlusion, one for the deepest, a depth rather than a height

record PictureLayout                                     // What each of a picture's four channels holds, red, green, blue and alpha, nothing where one holds nothing, and the color space it is read in
PictureLayout(PictureRole? r, PictureRole? g, PictureRole? b, PictureRole? a);  // A layout of the four channels' roles
PictureLayout Color { get; }                             // The base color in red, green and blue and the opacity in alpha, sRGB
PictureLayout Normal { get; }                            // The normal's X and Y in red and green, its Z rebuilt, as BC5 holds it
PictureLayout Surface { get; }                           // The occlusion in red, the roughness in green, the metallic in blue and the diffuse transmission in alpha, linear
PictureLayout MetallicRoughness { get; }                 // glTF's metallic-roughness texture, the roughness in green and the metallic in blue
PictureLayout Occlusion { get; }                         // glTF's occlusion texture, the occlusion in red
PictureLayout Emissive { get; }                          // The emitted light in red, green and blue, sRGB
PictureLayout Depth { get; }                             // The depth for parallax occlusion in red, as BC4 holds it
PictureRole? R { get; }                                  // What red holds, or nothing
PictureRole? G { get; }                                  // What green holds, or nothing
PictureRole? B { get; }                                  // What blue holds, or nothing
PictureRole? A { get; }                                  // What alpha holds, or nothing
ColorSpace ColorSpace { get; }                           // sRGB where red, green and blue hold a color, linear otherwise
int? ChannelOf(PictureRole role);                        // The channel a role stands in, 0 for red to 3 for alpha, the first of the three for a color, or nothing where the picture does not hold it
bool Holds(PictureRole role);                            // Whether the picture holds a role
ColorSpace ColorSpaceOf(PictureRole role);               // The color space a role is read in, sRGB for a color and linear for the rest

record MaterialPicture                                   // A picture a material is drawn with, where it is and what its channels hold
MaterialPicture(string Path, PictureLayout Layout);
string Path { get; init; }                               // The picture's path among the engine's files, as the material's description names it, read by the engine
PictureLayout Layout { get; init; }                      // What each channel holds, which also gives the color space it is read in
ColorSpace ColorSpace { get; }                           // The color space the picture is read in, its layout's

record MaterialDescription                               // What a surface is, in glTF 2.0's metallic-roughness terms and its extensions', each factor and the picture that scales it, so an engine of any shading model reads the quantities it names
MaterialDescription();
Vector4 BaseColor { get; init; }                         // The base color in linear numbers from zero to one, with the opacity in W
float Metallic { get; init; }                            // How metal the surface is, from zero to one
float Roughness { get; init; }                           // glTF's perceptual roughness from zero to one, whose square is GGX's alpha
float Reflectance { get; init; }                         // The share of light a dielectric reflects at normal incidence, from zero to below one, as Reflectance converts it, a metal taking its reflectance from its base color instead
Vector3 Emissive { get; init; }                          // The light the surface emits, linear RGB in candelas a square meter
float NormalScale { get; init; }                         // How far the normal map tilts the surface, glTF's scale, one as the map gives it
float OcclusionStrength { get; init; }                   // How much the occlusion map darkens the light from all around, from zero for none to one for all it says
float DiffuseTransmission { get; init; }                 // The share of light let through the surface and scattered, from zero to one, as glTF's KHR_materials_diffuse_transmission names it
AlphaMode AlphaMode { get; init; }                       // How the opacity is read
float AlphaCutoff { get; init; }                         // The opacity a texel reaches to be drawn under Mask
bool DoubleSided { get; init; }                          // Whether the surface is seen from behind as well as from the front
IReadOnlyList<MaterialPicture> Pictures { get; init; }   // The pictures that scale the factors, no role held by two of them
(MaterialPicture, int)? PictureOf(PictureRole role);     // The picture that holds a role and the channel the role stands in, or nothing where none does
IReadOnlyList<string> Problems();                        // Each rule the description breaks, a sentence each, or none

enum AlphaMode                                           // How a material's opacity is read, glTF's three modes
Opaque = 0,                                              // The surface is opaque whatever its opacity says
Mask = 1,                                                // A texel is drawn where its opacity reaches the cutoff and left out elsewhere
Blend = 2,                                               // The surface is blended over what lies behind it by its opacity

class Reflectance                                        // A dielectric's reflectance at normal incidence, the share of light it reflects when lit and seen head on, and the two other forms an engine may take it in
float Dielectric = 0.04f;                                // The reflectance of most dielectrics, 0.04, an index of refraction of 1.5
float FromIor(float ior);                                // The reflectance of a dielectric of an index of refraction, as glTF's KHR_materials_ior gives it
float ToIor(float reflectance);                          // The index of refraction of a dielectric of a reflectance
float FromPerceptual(float perceptual);                  // The reflectance of a perceptual reflectance, 0.16 times its square, so 0.5 is 0.04 and 1 is 0.16, a gemstone's
float ToPerceptual(float reflectance);                   // The perceptual reflectance of a reflectance, the square root of a sixth and a quarter of it

record struct MaterialMeans                              // A material's means over its surface, which a far card, a picture of many surfaces at once, or a field of the scene's light is painted with where the pictures themselves are too fine to read
int LongestSide = 64;                                    // The longest side of the level the means are read from, the first level of each picture that fits it, so a picture's cost is a few thousand texels whatever its size
MaterialMeans(Vector3 Color, float Opacity, float Occlusion, float Roughness, float Metallic, float DiffuseTransmission);
Vector3 Color { get; init; }                             // The base color's mean in linear numbers, over the texels the opacity covers
float Opacity { get; init; }                             // The share of the surface the opacity covers, one for an opaque material
float Occlusion { get; init; }                           // The occlusion's mean as the material applies it, one for none
float Roughness { get; init; }                           // The perceptual roughness's mean
float Metallic { get; init; }                            // The metallic's mean
float DiffuseTransmission { get; init; }                 // The diffuse transmission's mean
MaterialMeans Of(MaterialDescription material, IMaterialFiles files);  // The means of a material, its pictures read through the engine's files

interface IMaterialFiles                                 // The engine's files, by the paths a material's pictures name, the one thing the module asks of an engine
byte[] ReadAllBytes(string path);                        // The whole file at a path

class SurfaceResponse                                    // How a lit surface sends its light back, the terms of a microfacet response and the two forms a probe's ray takes it in where it meets a surface, written down as they stand until a room read against its traced reference chooses between them
float LeastRoughnessFullPrecision = 0.045f;              // The least perceptual roughness a response computed in single precision takes, 0.045, as Filament's material model holds it, a smaller one's distribution passing what the precision keeps
float LeastRoughnessHalfPrecision = 0.089f;              // The least perceptual roughness a response computed in half precision takes, 0.089, as Filament's material model holds it
float Alpha(float roughness, float floor = 0f);          // The alpha of a perceptual roughness, held to a floor and squared
float Distribution(float nh, float alpha);               // GGX's distribution of microfacet normals at the half way's cosine to the normal
float Visibility(float nv, float nl, float alpha);       // Smith's height-correlated visibility, G over 4 nl nv
float Fresnel(float f0, float f90, float vh);            // Schlick's Fresnel, from a reflectance head on to one at a grazing angle
Vector3 Fresnel(Vector3 f0, float f90, float vh);        // Schlick's Fresnel for a reflectance of each channel
float Lambert();                                         // Lambert's diffuse term, one over pi
float Burley(float alpha, float nv, float nl, float lh);  // Burley's diffuse term, which darkens a smooth surface's edges and lightens a rough one's
Vector3 F0(Vector3 baseColor, float metallic, float reflectance);  // The reflectance at normal incidence of a surface, the dielectric's reflectance moved toward the base color by how metal it is
(float, float) SplitSum(float roughness, float nv);      // Karis's fit of the split sum, the scale on a reflectance and the bias added to it that give the share of the light from all around a surface reflects toward the eye
(float, float) SplitSumIntegrated(float roughness, float nv, int samples = 1024);  // The split sum integrated by importance sampling GGX, the scale and the bias of SplitSum without a fit, for a table an engine samples
(Vector3, Vector3) HitShares(SurfaceResponse.HitForm form, Vector3 paint, float metallic, float roughness, float reflectance, float nv);  // The weights a probe's ray gives a surface it meets in a form, the diffuse color that takes the light from all around and the glossy share that takes the light along the mirror way

class Prototypes                                         // The prototype materials a level is blocked out with, a grid of a meter's cells tinted by a color for each part of a level, so a reader tells what each surface is for before it has textures of its own
float Roughness = 0.85f;                                 // The roughness of every prototype
int PixelsPerMeter = 1024;                               // The pixels a meter of the grid has where a picture is drawn at a surface's size
IReadOnlyList<string> Names { get; }                     // The names of the parts of a level a prototype is for, in the order of the table
IReadOnlyDictionary<string, Vector3> Colors { get; }     // Each prototype's color by its name, as sRGB numbers from zero to one
Vector3 Face { get; }                                    // The grid's face, all but white, as sRGB numbers
Vector3 Line { get; }                                    // The grid's lines a meter and a half meter apart and its mark, as sRGB numbers
Vector3 Fine { get; }                                    // The grid's lines a quarter of a meter apart, as sRGB numbers
Vector3 SrgbColor(string name);                          // A prototype's color as sRGB numbers, white for a name the table has none of
Vector3 Tint(string name);                               // A prototype's color in linear numbers, the factor its grid is multiplied by
MaterialDescription Description(string name, string grid);  // A prototype as a material, its tint the base color over the grid's picture, rough and not metal
byte[] Grid(int pixelsPerMeter);                         // The grid's cell in its own tones, four bytes a pixel in sRGB, rows from the top
byte[] Grid(int pixelsPerMeter, Vector3 face, Vector3 line, Vector3 fine);  // The grid's cell in the tones given, four bytes a pixel in sRGB, rows from the top
byte[] Picture(string name, int pixelsPerMeter);         // A prototype's picture as a level editor shows it, the grid tinted by the prototype's color as its material multiplies its texture, four bytes a pixel in sRGB, rows from the top
byte[] Picture(Vector3 color, int pixelsPerMeter, Vector3 face, Vector3 line, Vector3 fine);  // The grid in the tones given tinted by a color, each pixel decoded from sRGB, multiplied by the color in linear light and encoded again, as a material samples and multiplies its texture

enum ColorSpace                                          // How a picture's numbers in red, green and blue stand for light
Linear = 0,                                              // A number is proportional to the light, as a roughness, a normal or a mask is
Srgb = 1,                                                // A number is encoded by the sRGB transfer function, as a color picked or photographed is, and a GPU decodes it to linear light as it samples

class Ktx2                                               // A KTX2 file's picture, its format, color space, size and levels, read from the file's bytes
PictureFormat Format { get; }                            // How the texels are stored
ColorSpace ColorSpace { get; }                           // Whether red, green and blue are sRGB or linear, as the file's format says
int Width { get; }                                       // The width of level 0 in texels
int Height { get; }                                      // The height of level 0 in texels
IReadOnlyList<byte[]> Levels { get; }                    // Each level's texels in Format, level 0 first
(int, int) LevelSize(int level);                         // A level's width and height in texels
byte[] Decode(int level = 0);                            // A level decoded to four bytes a texel, as Decode gives it
Ktx2 Read(ReadOnlySpan<byte> file);                      // Reads a KTX2 file from its bytes

class Texels                                             // A picture's texels in a PictureFormat, their size and their decoding to four bytes a texel
int BlockBytes(PictureFormat format);                    // How many bytes a block of the format holds, or 4 for a texel of Rgba8
long ByteCount(PictureFormat format, int width, int height);  // How many bytes a picture of the size holds in the format, in whole blocks
byte[] Decode(PictureFormat format, ReadOnlySpan<byte> data, int width, int height);  // The texels decoded to four bytes each, red, green, blue and alpha, rows from the top, as a GPU samples the format before any sRGB is decoded

enum PictureFormat                                       // How a picture's texels are stored, plain or in the blocks a GPU samples as they are
Rgba8 = 0,                                               // Four bytes a texel, red, green, blue and alpha
Bc1 = 1,                                                 // BC1, 8 bytes a block, two colors of 5, 6 and 5 bits and the two between them, or one between them and a transparent black where the first color is not the greater
Bc1Rgb = 2,                                              // BC1 read with no alpha, the transparent black of Bc1 an opaque one, as a format of red, green and blue alone samples it
Bc4 = 3,                                                 // BC4, 8 bytes a block, one channel of eight levels between two ends
Bc5 = 4,                                                 // BC5, 16 bytes a block, two channels, each a Bc4 block
Bc7 = 5,                                                 // BC7, 16 bytes a block, four channels in one of eight modes the block chooses
```
