#!/usr/bin/env python3
"""Builds a texture collection from the sources its manifest, a textures.json in the collection's
folder, names, by the recipe the manifest gives each entry.

    scripts/textures.py MANIFEST                    # build every entry whose source is here
    scripts/textures.py MANIFEST NAME ...           # build the entries named
    scripts/textures.py MANIFEST --sources DIR ...  # read the sources under DIR
    scripts/textures.py MANIFEST --out DIR NAME ... # write under DIR, the collection left alone
    scripts/textures.py --sample DIR                # write an entry of no source, for the tests
    scripts/textures.py --repeat FILE DX DY         # the shift near DX, DY where a scan repeats
    scripts/textures.py --encode RGBA W H OUT       # a raw picture as BC7 with its levels, as it is
                                                    # (--format bc5 or bc4 for red and green or red)

An entry is three KTX2 files with their mipmaps, and a fourth for a bark, each level's blocks kept
as they are with no supercompression, so a reader on .NET alone and a GPU take them with no step
between:

    albedo.ktx2   BC7, sRGB     the color in RGB and the opacity in A
    normal.ktx2   BC5           the tangent-space normal's X and Y, Y up the picture, its Z what
                                makes a unit length
    surface.ktx2  BC7, linear   occlusion in R, roughness in G, metallic in B and translucency in A,
                                as glTF's occlusion and metallic-roughness textures place the first
                                three, so one file is bound as the occlusion, the metallic-roughness
                                and the diffuse transmission texture
    depth.ktx2    BC4           a bark's depth for parallax occlusion, white the deepest, a depth
                                below the surface rather than a height, half a byte a texel

The normal and the surface are half the albedo's size. Each level of a file is at a multiple of a
block's sixteen bytes, as the format asks of levels kept as they are.

An entry's kind gives its recipe. A tile, an entry of no kind, tiles at its size, a map resized with
its opposite edges wrapped round, or a scan whose edges do not meet made into a tile from its
repeat, the two shifts at which the pattern matches itself, which --repeat finds from a guess read
off the picture, and a third, half their sum, where the pattern is a half-drop. The tile is the
parallelogram the two shifts span, drawn straight, and each texel is the mean of every copy of its
place the scan holds, weighted down toward the scan's edges, so a texel on one edge of the tile and
its neighbor across the opposite edge are the same place, and a stain or a shade one copy carries is
diluted by the others. The repeat's size in meters is written into the manifest from the dots an
inch the scan's file records.

A bark, a leaf atlas and a grass card are made of parts placed on the picture. A bark's depth is its
source's height turned over where the recipe names one, its grays stretched to black and white, and
otherwise the height its normal map's slopes add up to, integrated over the whole picture at once
through its Fourier transform, which a bark's tiling suits, scaled so its lowest and highest
hundredths are black and white, and softened by a texel, as a steep step in a depth map shows as a
jagged edge.

Each level is made from the one above by a box filter, and the color under a cut-out leaf's
transparent texels is the color of the leaf nearest, so no dark rim shows as it shrinks. A leaf
atlas's color and its surface's four channels are each level's means weighted by how much of each
texel the leaves cover, and the surface's texels outside the leaves take the nearest leaf's as the
color's do, since averaged with the transparent texels round it a leaf two texels across took their
light color and their translucency of none. A leaf atlas's opacity is scaled at each level so the
share of each leaf's box past one half, read between the level's texels as a card samples it, stays
the top level's on average, so a leaf holds in the distance until its box is about a texel across.
A grass entry is a card of blades whose roots stand along its bottom edge, cut out as a leaf atlas
is, its levels keeping the share of the whole card past one half, since its blades are one cluster
rather than leaves apart.

The encoders are written here, BC7's mode 6 with each block's endpoints along its principal axis and
fitted again by least squares, a block opaque throughout keeping its shared bits set so its alpha
decodes to 255, mode 1 for an opaque block mode 6 holds worse than MODE1_ABOVE and mode 7 for a
block with alpha mode 6 holds worse than MODE7_ABOVE, each of two subsets of the partition whose
colors lie nearest the lines through them, which a block of three colors, two gradients or a leaf's
cut edge needs, and BC4 twice for BC5, so the build needs Python 3 and Pillow and nothing else. The
encoding is the one function encode() below, so another format is a second branch of it.

A source is a folder under the folder --sources names, the repository's root that holds the
manifest where none is given, as the entry's source names it. An entry is written into its folder
in the collection, and one the manifest marks waiting into waiting/ beside it, named in a
textures.json there, as --out's and --sample's folders are. Each build into the collection writes
the files' sizes, the encoders' error, the mean color and surface of the opaque texels, a leaf set's
boxes and a repeat's meters back into the manifest. The same sources and recipe make the same bytes.
"""

import argparse
import json
import math
import os
import struct
import sys

from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageMath, ImageStat

# Vulkan's numbers for the formats, which a KTX2 file names its texels by.
BC4_UNORM = 139
BC5_UNORM = 141
BC7_UNORM = 145
BC7_SRGB = 146

# The Khronos data format descriptor's color models and transfer functions for them.
MODEL_BC4 = 131
MODEL_BC5 = 132
MODEL_BC7 = 134
TRANSFER_LINEAR = 1
TRANSFER_SRGB = 2

# BC7's interpolation weights for four-bit indices, in sixty-fourths, and the nearest index for each
# sixty-fourth, so a texel's place between the endpoints is turned into its index by a lookup.
WEIGHTS = (0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64)
NEAREST = [min(range(16), key=lambda i: abs(WEIGHTS[i] - t)) for t in range(65)]


# BC7's partitions of a block into two subsets, a row of its sixteen texels' subsets each read left
# to right and top to bottom, and the anchor texel of each one's second subset, from the Khronos
# Data Format Specification's tables.
PARTITIONS = [
    "0011001100110011", "0001000100010001", "0111011101110111", "0001001100110111",
    "0000000100010011", "0011011101111111", "0001001101111111", "0000000100110111",
    "0000000000010011", "0011011111111111", "0000000101111111", "0000000000010111",
    "0001011111111111", "0000000011111111", "0000111111111111", "0000000000001111",
    "0000100011101111", "0111000100000000", "0000000010001110", "0111001100010000",
    "0011000100000000", "0000100011001110", "0000000010001100", "0111001100110001",
    "0011000100010000", "0000100010001100", "0110011001100110", "0011011001101100",
    "0001011111101000", "0000111111110000", "0111000110001110", "0011100110011100",
    "0101010101010101", "0000111100001111", "0101101001011010", "0011001111001100",
    "0011110000111100", "0101010110101010", "0110100101101001", "0101101010100101",
    "0111001111001110", "0001001111001000", "0011001001001100", "0011101111011100",
    "0110100110010110", "0011110011000011", "0110011010011001", "0000011001100000",
    "0100111001000000", "0010011100100000", "0000001001110010", "0000010011100100",
    "0110110010010011", "0011011011001001", "0110001110011100", "0011100111000110",
    "0110110011001001", "0110001100111001", "0111111010000001", "0001100011100111",
    "0000111100110011", "0011001111110000", "0010001011101110", "0100010001110111",
]
SECOND_ANCHORS = [
    15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
    15, 2, 8, 2, 2, 8, 8, 15, 2, 8, 2, 2, 8, 8, 2, 2,
    15, 15, 6, 8, 2, 8, 15, 15, 2, 8, 2, 2, 2, 15, 15, 6,
    6, 2, 6, 8, 15, 15, 2, 2, 15, 15, 15, 15, 15, 2, 2, 15,
]
PARTITION_MASKS = [sum(1 << i for i, subset in enumerate(row) if subset == "1") for row in PARTITIONS]

# Mode 1's interpolation weights for three-bit indices, and the nearest index for each sixty-fourth.
WEIGHTS3 = (0, 9, 18, 27, 37, 46, 55, 64)
NEAREST3 = [min(range(8), key=lambda i: abs(WEIGHTS3[i] - t)) for t in range(65)]

# The squared error over a block's sixteen texels and three channels past which an opaque block is
# tried in mode 1's two subsets as well, a mean of 4 a channel, 42 dB, so a block mode 6 holds well
# costs nothing more.
MODE1_ABOVE = 16 * 3 * 4

# Mode 7's interpolation weights for two-bit indices, and the nearest index for each sixty-fourth.
WEIGHTS2 = (0, 21, 43, 64)
NEAREST2 = [min(range(4), key=lambda i: abs(WEIGHTS2[i] - t)) for t in range(65)]

# The squared error over a block's sixteen texels and four channels past which a block that is not
# opaque throughout is tried in mode 7's two subsets with alpha as well, a mean of 4 a channel.
MODE7_ABOVE = 16 * 4 * 4


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("manifest", nargs="?", help="the collection's textures.json")
    parser.add_argument("names", nargs="*", help="the entries to build, every one whose source is here where none is named")
    parser.add_argument("--out", help="write the entries under this folder, the collection and its manifest left as they are")
    parser.add_argument("--sources", metavar="DIR", help="read the sources under this folder, the repository's root where none is given")
    parser.add_argument("--sample", metavar="DIR", help="write an entry made from no source into DIR and stop")
    parser.add_argument("--repeat", nargs=3, metavar=("FILE", "DX", "DY"), help="print the shift near DX, DY at which the scan in FILE best matches itself")
    parser.add_argument("--encode", nargs=4, metavar=("RGBA", "WIDTH", "HEIGHT", "OUT"), help="write a raw picture, four bytes a texel, as a KTX2 file of BC7 with its levels, neither bled nor cut")
    parser.add_argument("--linear", action="store_true", help="name the --encode file's color space linear rather than sRGB")
    parser.add_argument("--format", choices=("bc7", "bc5", "bc4"), default="bc7", help="the --encode file's format, BC7, BC5 of red and green, or BC4 of red")
    arguments = parser.parse_args()

    if arguments.sample:
        write_entry(sample_maps(), os.path.join(arguments.sample, "sample"), "bark")
        list_entry(arguments.sample, "sample", "bark")
        return 0

    if arguments.encode:
        source, width, height, out = arguments.encode
        with open(source, "rb") as file:
            image = Image.frombytes("RGBA", (int(width), int(height)), file.read())
        if arguments.format == "bc4":
            levels = mip_chain(image.getchannel("R"), bled=False)
            data = ktx2(BC4_UNORM, levels, [bc4(level) for level in levels])
        elif arguments.format == "bc5":
            levels = mip_chain(image, bled=False)
            data = ktx2(BC5_UNORM, levels, [bc5(level) for level in levels])
        else:
            levels = mip_chain(image, bled=False)
            data = ktx2(BC7_UNORM if arguments.linear else BC7_SRGB, levels, [bc7(level) for level in levels])
        with open(out, "wb") as file:
            file.write(data)
        return 0

    if arguments.repeat:
        path, dx, dy = arguments.repeat
        (x, y), difference = repeat_near(path, (int(dx), int(dy)))
        print(f"{x} {y}, a mean difference of {difference:.2f} of 255")
        return 0

    if not arguments.manifest:
        parser.error("a manifest is named, or --sample or --repeat is given")
    manifest_path = os.path.abspath(arguments.manifest)
    collection = os.path.dirname(manifest_path)
    sources = arguments.sources or repository_of(collection)
    with open(manifest_path, encoding="utf-8") as file:
        manifest = json.load(file)

    named = set(arguments.names)
    unknown = named - {entry["name"] for entry in manifest["entries"]}
    if unknown:
        print(f"textures.py: the manifest has no entry {', '.join(sorted(unknown))}", file=sys.stderr)
        return 2

    failed = False
    for entry in manifest["entries"]:
        if named and entry["name"] not in named:
            continue
        source = os.path.join(sources, entry["source"])
        if not os.path.isdir(source):
            if named:
                print(f"textures.py: {entry['name']}'s source {source} is not here", file=sys.stderr)
                failed = True
            continue

        kind = entry.get("kind", "tile")
        root = arguments.out or (collection if ships(entry) else os.path.join(collection, "waiting"))
        maps = read_entry(entry, source)
        boxes = leaf_boxes(maps["albedo"]) if kind == "leaves" else None
        report = write_entry(maps, os.path.join(root, entry["name"]), kind, boxes if boxes is not None else card_box(entry))
        if root != collection:
            list_entry(root, entry["name"], kind)
        print(f"{entry['name']}: {', '.join(f'{file} {size // 1024} KB, {error:.1f} dB' for file, (size, error) in report.items())}"
              + ("" if ships(entry) else ", waiting"))
        if not arguments.out:
            entry["files"] = {file: size for file, (size, _) in report.items()}
            entry["error"] = {file: round(error, 1) for file, (_, error) in report.items()}
            entry["color"] = mean_color(maps["albedo"])
            entry["surface"] = mean_surface(maps)
            if boxes is not None:
                entry["leaves"] = boxes
            if "repeat" in entry:
                entry["meters"] = repeat_meters(entry, source)

    if not arguments.out:
        with open(manifest_path, "w", encoding="utf-8", newline="\n") as file:
            json.dump(manifest, file, indent=2, ensure_ascii=False)
            file.write("\n")
    return 1 if failed else 0


def repository_of(folder):
    """The repository holding a folder, the nearest folder above it with a .git, or the folder itself."""
    at = folder
    while True:
        if os.path.exists(os.path.join(at, ".git")):
            return at
        parent = os.path.dirname(at)
        if parent == at:
            return folder
        at = parent


def list_entry(root, name, kind):
    """
    Names an entry in the textures.json of a folder of entries other than the collection, which
    TreeTextures.Add reads, kept in the order of the names.
    """
    path = os.path.join(root, "textures.json")
    listed = {"entries": []}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as file:
            listed = json.load(file)
    entries = [entry for entry in listed["entries"] if entry["name"] != name] + [{"name": name, "kind": kind}]
    listed["entries"] = sorted(entries, key=lambda entry: entry["name"])
    with open(path, "w", encoding="utf-8", newline="\n") as file:
        json.dump(listed, file, indent=2)
        file.write("\n")


def ships(entry):
    """Whether an entry is carried, which every one is that the manifest does not mark waiting."""
    return not entry.get("waiting")


# The recipe.

def read_entry(entry, source):
    """The entry's maps at their sizes, by the recipe its kind gives, a tile where it gives none."""
    return read_tile(entry, source) if entry.get("kind", "tile") == "tile" else read_parts(entry, source)


def read_tile(entry, source):
    """A tile's albedo, normal and surface at their sizes, each a tile."""
    width, height = entry["size"]
    small = (max(width // 2, 4), max(height // 2, 4))
    repeat = entry.get("repeat")

    def tile(image, size):
        return repeated(image, repeat, size) if repeat else wrapped(image, size)

    color = read_map(source, entry["albedo"])
    albedo = tile(color, (width, height))
    albedo.putalpha(255)

    if "normal" in entry:
        bumps = read_map(source, entry["normal"])
        if entry["normal"].get("flip_green"):
            r, g, b = bumps.split()
            bumps = Image.merge("RGB", (r, ImageChops.invert(g), b))
        normal = tile(bumps, small)
    else:
        normal = Image.new("RGB", small, (128, 128, 255))

    # Occlusion, roughness, metallic and translucency, a number over the whole tile or a channel of
    # a file the recipe names, which is tiled as the color is.
    layers = []
    for name, default in (("occlusion", 1.0), ("roughness", 0.8), ("metallic", 0.0), ("translucency", 0.0)):
        spec = entry.get(name, default)
        if isinstance(spec, (int, float)):
            layers.append(Image.new("L", small, round(255 * spec)))
        else:
            layers.append(tile(read_channel(source, spec), small))
    return {"albedo": albedo, "normal": normal, "surface": Image.merge("RGBA", layers)}


def read_parts(entry, source):
    """The entry's albedo, normal and surface at their sizes, from its parts as the recipe places
    them."""
    width, height = entry["size"]
    albedo = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    normal = Image.new("RGB", (max(width // 2, 4), max(height // 2, 4)), (128, 128, 255))
    surface = Image.new("RGBA", normal.size, (255, 255, 0, 0))

    for part in entry["parts"]:
        place = part.get("place", [0, 0, 1, 1])
        box = placed(place, albedo.size)
        small = placed(place, normal.size)

        color = read_map(source, part["albedo"], "RGB")
        opacity = read_channel(source, part.get("opacity", 1.0), color.size)
        if part.get("fit"):
            box = fitted(box, color.size)
            small = fitted(small, color.size)
        piece = color.resize(size_of(box), Image.Resampling.LANCZOS)
        piece.putalpha(opacity.resize(size_of(box), Image.Resampling.LANCZOS))
        albedo.paste(piece, box[:2])

        if part.get("normal"):
            bumps = read_map(source, part["normal"], "RGB")
            if part["normal"].get("flip_green"):
                r, g, b = bumps.split()
                bumps = Image.merge("RGB", (r, ImageChops.invert(g), b))
            normal.paste(bumps.resize(size_of(small), Image.Resampling.LANCZOS), small[:2])

        size = color.size
        occlusion = read_channel(source, part.get("occlusion", 1.0), size)
        roughness = read_channel(source, part.get("roughness", 0.8), size)
        metallic = read_channel(source, 0.0, size)
        if part.get("translucency") == "albedo":
            translucency = color.convert("L").point(lambda value: min(255, int(value * part.get("translucency_gain", 1.0))))
        else:
            translucency = read_channel(source, part.get("translucency", 0.0), size)
        layers = Image.merge("RGBA", (occlusion, roughness, metallic, translucency))
        piece = resized(layers, size_of(small), Image.Resampling.LANCZOS)
        mask = opacity.resize(size_of(small), Image.Resampling.LANCZOS)
        surface.paste(piece, small[:2], mask if part.get("opacity") is not None else None)

    maps = {"albedo": albedo, "normal": normal, "surface": surface}
    if entry["kind"] == "bark":
        part = entry["parts"][0]
        if "depth" in part:
            height = stretched(read_channel(source, part["depth"], normal.size))
        else:
            height = height_of(normal)
        maps["depth"] = ImageChops.invert(height).filter(ImageFilter.GaussianBlur(1))
    return maps


def stretched(image):
    """A one-channel image with its lowest and highest hundredths made black and white, as a
    source's height is often a narrow band of grays."""
    histogram = image.histogram()
    total = sum(histogram)
    counted, low, high = 0, 0, 255
    for value, count in enumerate(histogram):
        counted += count
        if counted <= total // 100:
            low = value + 1
        if counted < total - total // 100:
            high = value + 1
    span = max(high - low, 1)
    return image.point(lambda value: max(0, min(255, round(255 * (value - low) / span))))


def height_of(normal):
    """
    The height a tangent-space normal map's slopes add up to, the slope along a row minus its X over
    its Z and down a column its Y over its Z, as an OpenGL map's Y points up the picture, solved for
    over the whole map at once in the Fourier domain as Frankot and Chellappa solve it, the waves
    longer than a quarter of it faded out, its lowest and highest hundredths black and white.
    """
    width, height = normal.size
    data = normal.convert("RGB").tobytes()
    along, down = [], []
    for i in range(width * height):
        x, y, z = (data[3 * i + c] / 127.5 - 1.0 for c in range(3))
        z = max(z, 0.1)
        along.append(complex(-x / z))
        down.append(complex(y / z))
    first, second = fourier(along, width, height), fourier(down, width, height)
    solved = [0j] * (width * height)

    # The waves longer than a quarter of the picture faded out, as a normal map's slopes drift a
    # little over its whole width, which summed would tilt the bark's depth across it.
    cutoff = 2 * math.pi * 4 / min(width, height)
    for row in range(height):
        v = (row if row < height // 2 else row - height) * 2 * math.pi / height
        for column in range(width):
            u = (column if column < width // 2 else column - width) * 2 * math.pi / width
            if u or v:
                at = column + width * row
                kept = 1.0 - math.exp(-(u * u + v * v) / (cutoff * cutoff))
                solved[at] = kept * (-1j * u * first[at] - 1j * v * second[at]) / (u * u + v * v)
    values = [value.real for value in fourier(solved, width, height, invert=True)]
    ordered = sorted(values)
    low, high = ordered[len(ordered) // 100], ordered[-1 - len(ordered) // 100]
    span = high - low or 1.0
    image = Image.new("L", (width, height))
    image.putdata([max(0, min(255, round(255 * (value - low) / span))) for value in values])
    return image


def fourier(values, width, height, invert=False):
    """A picture's two-dimensional discrete Fourier transform, or its inverse, its rows and then its
    columns."""
    rows = [transform(values[row * width:(row + 1) * width], invert) for row in range(height)]
    out = [0j] * (width * height)
    for column in range(width):
        transformed = transform([rows[row][column] for row in range(height)], invert)
        for row in range(height):
            out[column + width * row] = transformed[row]
    return out


def transform(values, invert):
    """The Fourier transform of a power of two of numbers, by Cooley and Tukey's halving."""
    count = len(values)
    out = list(values)
    j = 0
    for i in range(1, count):
        bit = count >> 1
        while j & bit:
            j ^= bit
            bit >>= 1
        j |= bit
        if i < j:
            out[i], out[j] = out[j], out[i]
    length = 2
    while length <= count:
        angle = (2 * math.pi / length) * (1 if invert else -1)
        step = complex(math.cos(angle), math.sin(angle))
        half = length // 2
        for start in range(0, count, length):
            turn = 1 + 0j
            for k in range(half):
                a = out[start + k]
                b = out[start + k + half] * turn
                out[start + k] = a + b
                out[start + k + half] = a - b
                turn *= step
        length <<= 1
    return [value / count for value in out] if invert else out


def read_map(source, spec, mode="RGB"):
    """A map of a recipe, its file read, cropped and changed in color as it says."""
    image = Image.open(os.path.join(source, spec["file"]))
    image.load()
    if "crop" in spec:
        x, y, w, h = spec["crop"]
        image = image.crop((x, y, x + w, y + h))
    image = image.convert(mode)
    if mode == "RGB" and "color" in spec:
        image = recolor(image, spec["color"])
    return image


def read_channel(source, spec, size=None):
    """
    One channel of a recipe, a number from zero to one over the whole size or a channel of a file,
    resized to the size where one is given.
    """
    if isinstance(spec, (int, float)):
        return Image.new("L", size, round(255 * spec))
    image = Image.open(os.path.join(source, spec["file"]))
    image.load()
    if "crop" in spec:
        x, y, w, h = spec["crop"]
        image = image.crop((x, y, x + w, y + h))
    channel = spec.get("channel", "l")
    image = image.convert("L") if channel == "l" else image.convert("RGBA").getchannel(channel.upper())
    if spec.get("invert"):
        image = ImageChops.invert(image)
    if "gain" in spec or "bias" in spec:
        gain, bias = spec.get("gain", 1.0), spec.get("bias", 0.0)
        image = image.point(lambda value: max(0, min(255, round(value * gain + 255 * bias))))
    return image.resize(size, Image.Resampling.LANCZOS) if size is not None and image.size != size else image


def recolor(image, color):
    """A color map with its brightness, saturation and contrast changed, each a factor about one."""
    for name, enhancer in (("brightness", ImageEnhance.Brightness), ("saturation", ImageEnhance.Color), ("contrast", ImageEnhance.Contrast)):
        if name in color:
            image = enhancer(image).enhance(color[name])
    return image


def resized(image, size, method):
    """
    An image resized a channel at a time, since Pillow resizes RGBA through premultiplied alpha,
    which turns a transparent texel's color black and the surface's channels, whose alpha is the
    translucency, to nothing where it is.
    """
    return Image.merge(image.mode, [band.resize(size, method) for band in image.split()])


def wrapped(image, size):
    """
    A tile resized with its opposite edges wrapped round, so the filter reads across an edge what lies
    across it when the tile repeats, and the tile meets itself at its new size.
    """
    if image.size == size:
        return image
    width, height = image.size
    margin = 4 * math.ceil(max(width / size[0], height / size[1])) + 2
    padded = Image.new(image.mode, (width + 2 * margin, height + 2 * margin))
    for x in (-1, 0, 1):
        for y in (-1, 0, 1):
            padded.paste(image, (margin + x * width, margin + y * height))
    return padded.resize(size, Image.Resampling.LANCZOS, box=(margin, margin, margin + width, margin + height))


def repeated(image, repeat, size):
    """
    The tile of a scan's repeat at a size, each texel the mean of every copy of its place in the scan,
    weighted by how far each lies from the scan's edges.
    """
    across, down = repeat["across"], repeat["down"]
    shifts = [(1, 0, 0), (0, 1, 0)]
    if repeat.get("half_drop"):
        shifts.append((0, 0, 1))
    half = ((across[0] + down[0]) / 2, (across[1] + down[1]) / 2)
    width, height = image.size

    # The scan reduced by a box so a texel of the tile is about a texel of it, then drawn straight.
    reduced = (max(1, round(width * size[0] / math.hypot(*across))), max(1, round(height * size[1] / math.hypot(*down))))
    scale_x, scale_y = reduced[0] / width, reduced[1] / height
    bands = [band.resize(reduced, Image.Resampling.BOX) for band in image.split()]
    weight = edge_weight(reduced, max(4, min(reduced) // 16))

    total = Image.new("F", size, 0.0)
    sums = [Image.new("F", size, 0.0) for _ in bands]
    reach = range(-2, 3)
    for i in reach:
        for j in reach:
            for k in ((0, 1) if repeat.get("half_drop") else (0,)):
                x = i * across[0] + j * down[0] + k * half[0]
                y = i * across[1] + j * down[1] + k * half[1]
                data = (across[0] / size[0] * scale_x, down[0] / size[1] * scale_x, x * scale_x,
                        across[1] / size[0] * scale_y, down[1] / size[1] * scale_y, y * scale_y)
                weights = weight.transform(size, Image.Transform.AFFINE, data, Image.Resampling.BILINEAR, fillcolor=0)
                if weights.getextrema()[1] == 0:
                    continue
                weights = weights.convert("F")
                total = ImageMath.lambda_eval(lambda a: a["t"] + a["w"], t=total, w=weights)
                for at, band in enumerate(bands):
                    copy = band.transform(size, Image.Transform.AFFINE, data, Image.Resampling.BICUBIC).convert("F")
                    sums[at] = ImageMath.lambda_eval(lambda a: a["s"] + a["c"] * a["w"], s=sums[at], c=copy, w=weights)

    # A place whose copies all lie at the scan's edges is weighted little but still its copies'
    # mean, as the wallpaper's repeat is 21 pixels taller than its scan and a strip of it lies so.
    if total.getextrema()[0] <= 0:
        raise ValueError("the scan holds no copy of some place of its repeat")
    out = [ImageMath.lambda_eval(lambda a: a["s"] / a["t"] + 0.5, s=s, t=total).convert("L") for s in sums]
    return Image.merge(image.mode, out)


def edge_weight(size, ramp):
    """A weight of 255 over a picture but a band along its edges, where it falls to nothing at the edge."""
    width, height = size
    across = Image.new("L", (width, 1))
    across.putdata([min(255, round(255 * min(x + 0.5, width - x - 0.5) / ramp)) for x in range(width)])
    down = Image.new("L", (1, height))
    down.putdata([min(255, round(255 * min(y + 0.5, height - y - 0.5) / ramp)) for y in range(height)])
    return ImageChops.multiply(across.resize(size, Image.Resampling.NEAREST), down.resize(size, Image.Resampling.NEAREST))


def repeat_near(path, guess):
    """
    The shift near a guess at which a scan differs least from itself, by the mean difference over
    where the two overlap, searched at a quarter of its size, then a half, then the whole.
    """
    image = Image.open(path).convert("L")
    width, height = image.size
    shift, difference = guess, 0.0
    for scale, reach in ((4, 24), (2, 3), (1, 3)):
        small = image.resize((width // scale, height // scale), Image.Resampling.BOX) if scale > 1 else image
        x, y = round(shift[0] / scale), round(shift[1] / scale)
        difference, x, y = min((differs(small, dx, dy), dx, dy) for dx in range(x - reach, x + reach + 1) for dy in range(y - reach, y + reach + 1))
        shift = (x * scale, y * scale)
    return shift, difference


def differs(image, dx, dy):
    width, height = image.size
    left, top = max(0, -dx), max(0, -dy)
    right, bottom = min(width, width - dx), min(height, height - dy)
    one = image.crop((left, top, right, bottom))
    other = image.crop((left + dx, top + dy, right + dx, bottom + dy))
    return ImageStat.Stat(ImageChops.difference(one, other)).mean[0]


def repeat_meters(entry, source):
    """The repeat's width and height in meters, from the dots an inch the scan's file records."""
    with Image.open(os.path.join(source, entry["albedo"]["file"])) as image:
        dots = image.info.get("dpi", (0, 0))
    if not dots[0] or not dots[1]:
        return None
    across, down = entry["repeat"]["across"], entry["repeat"]["down"]
    return [round(math.hypot(*across) / dots[0] * 0.0254, 4), round(math.hypot(*down) / dots[1] * 0.0254, 4)]


def placed(place, size):
    x, y, w, h = place
    return (round(x * size[0]), round(y * size[1]), round((x + w) * size[0]), round((y + h) * size[1]))


def fitted(box, size):
    """A box shrunk to a size's proportions, centered in it."""
    left, top, right, bottom = box
    scale = min((right - left) / size[0], (bottom - top) / size[1])
    w, h = max(1, round(size[0] * scale)), max(1, round(size[1] * scale))
    x, y = left + (right - left - w) // 2, top + (bottom - top - h) // 2
    return (x, y, x + w, y + h)


def size_of(box):
    return (box[2] - box[0], box[3] - box[1])


def sample_maps():
    """An entry from no source, a ringed bark of 64 by 64 with a hole cut in it and its depth from
    its normal, for the tests."""
    albedo = Image.new("RGBA", (64, 64))
    normal = Image.new("RGB", (32, 32))
    surface = Image.new("RGBA", (32, 32))
    for y in range(64):
        for x in range(64):
            ring = int(128 + 100 * math.sin(x * 0.4) * math.cos(y * 0.2))
            hole = (x - 40) ** 2 + (y - 24) ** 2 < 100
            albedo.putpixel((x, y), (ring, 90, 255 - ring, 0 if hole else 255))
    for y in range(32):
        for x in range(32):
            normal.putpixel((x, y), (int(128 + 60 * math.cos(x * 0.8)), int(128 + 60 * math.sin(y * 0.4)), 255))
            surface.putpixel((x, y), (255, 200, 0, 8 * x))
    return {"albedo": albedo, "normal": normal, "surface": surface, "depth": ImageChops.invert(height_of(normal)).filter(ImageFilter.GaussianBlur(1))}


def mean_color(albedo):
    """
    The mean of the albedo's opaque texels in linear light, written in sRGB from zero to one, the
    color a far card is drawn with and a leaf set's summer color a fall tint is worked out against.
    Averaged in sRGB the mean comes out darker, since the curve bows below its chord.
    """
    alpha = albedo.getchannel("A").point(lambda value: 255 if value > 127 else 0)
    color = albedo.convert("RGB")
    count = alpha.histogram()[255]
    if count == 0:
        return [0.0, 0.0, 0.0]
    linear = [value / 255 / 12.92 if value / 255 <= 0.04045 else ((value / 255 + 0.055) / 1.055) ** 2.4 for value in range(256)]
    means = [sum(linear[value] * n for value, n in enumerate(Image.composite(band, Image.new("L", band.size), alpha).histogram())) / count for band in color.split()]
    return [round(12.92 * mean if mean <= 0.0031308 else 1.055 * mean ** (1 / 2.4) - 0.055, 4) for mean in means]


def mean_surface(maps):
    """
    The mean of the surface's occlusion, roughness, metallic and translucency over the albedo's
    opaque texels, from zero to one as the material reads them, which a picture of a whole branch,
    as a tree's far card is, takes for the leaves it holds.
    """
    surface = maps["surface"]
    alpha = maps["albedo"].getchannel("A").resize(surface.size, Image.Resampling.BOX).point(lambda value: 255 if value > 127 else 0)
    count = alpha.histogram()[255]
    if count == 0:
        return [1.0, 1.0, 0.0, 0.0]
    sums = [sum(value * n for value, n in enumerate(Image.composite(band, Image.new("L", band.size), alpha).histogram())) for band in surface.split()]
    return [round(total / count / 255, 4) for total in sums]


def leaf_boxes(albedo):
    """
    Where each leaf of an atlas is, as the share of its width and height from its left and top
    and the share it spans, the opaque regions of its opacity read texel by texel, so a frond's thin
    stalk holds it together, a region under a tenth the largest's area left out as a scrap of a stem
    or a cut, in rows from the top and each row from the left, so a card shows one leaf.
    """
    small = albedo.getchannel("A")
    width, height = small.size
    solid = [value > 127 for value in small.tobytes()]
    seen = [False] * len(solid)
    boxes = []
    for start in range(len(solid)):
        if not solid[start] or seen[start]:
            continue
        stack = [start]
        seen[start] = True
        left, top, right, bottom, area = width, height, 0, 0, 0
        while stack:
            at = stack.pop()
            x, y = at % width, at // width
            left, right, top, bottom = min(left, x), max(right, x), min(top, y), max(bottom, y)
            area += 1
            for near in (at - 1 if x > 0 else -1, at + 1 if x < width - 1 else -1, at - width, at + width):
                if 0 <= near < len(solid) and solid[near] and not seen[near]:
                    seen[near] = True
                    stack.append(near)
        boxes.append((area, left, top, right + 1, bottom + 1))

    if not boxes:
        return []
    largest = max(box[0] for box in boxes)
    kept = sorted((box for box in boxes if box[0] * 10 >= largest), key=lambda box: box[2] + box[4])

    # A row is the leaves whose middles stand within half a typical leaf's height of the first's.
    tall = sorted(box[4] - box[2] for box in kept)[len(kept) // 2]
    rows = []
    for box in kept:
        if rows and (box[2] + box[4]) - (rows[-1][0][2] + rows[-1][0][4]) <= tall:
            rows[-1].append(box)
        else:
            rows.append([box])
    ordered = [box for row in rows for box in sorted(row, key=lambda box: box[1])]
    return [[round(left / width, 4), round(top / height, 4), round((right - left) / width, 4), round((bottom - top) / height, 4)]
            for _, left, top, right, bottom in ordered]


def card_box(entry):
    """
    A grass card's one box, the whole card, its blades standing on its bottom edge, so its levels
    keep the share of its blades past one half as a leaf atlas's keep its leaves, and nothing for
    any other kind.
    """
    return [[0.0, 0.0, 1.0, 1.0]] if entry.get("kind") == "grass" else None


# The files.

def write_entry(maps, folder, kind, boxes=None):
    """
    Writes an entry's files into a folder, and says each one's size and its encoding's error, a
    leaf atlas's levels keeping its leaves by the boxes leaf_boxes() finds.
    """
    os.makedirs(folder, exist_ok=True)
    report = {}
    cut = kind in ("leaves", "grass")
    for name, image in maps.items():
        cover = maps["albedo"].getchannel("A").resize(image.size, Image.Resampling.BOX) if cut and name in ("albedo", "surface") else None
        # A surface's alpha is its translucency, an opacity only where the leaves' cover stands in
        # for it, so a surface not cut out is not bled, which would give its texels that let
        # nothing through the occlusion, roughness and metallic of those that do.
        levels = mip_chain(image, boxes if name == "albedo" and cut else None, cover, cut and name == "surface", name != "surface" or cut)
        format, blocks, error = encode(name, levels)
        data = ktx2(format, levels, blocks)
        path = os.path.join(folder, f"{name}.ktx2")
        with open(path + ".part", "wb") as file:
            file.write(data)
        os.replace(path + ".part", path)
        report[f"{name}.ktx2"] = (len(data), error)
    return report


def mip_chain(image, boxes=None, cover=None, whole=False, bled=True):
    """
    The image and each level under it, half the size of the one above, down to a texel, a leaf
    atlas's opacity scaled at each level to keep the share of its leaves' boxes past one half.
    Where cover gives how much of each texel the leaves cover, a leaf atlas's opacity at the image's
    size, the color, or every channel where whole says so, as the surface's, is each level's mean
    weighted by it (covered()), and the texels it leaves bare are bled from it. An image whose alpha
    is no opacity, as a surface's translucency is where nothing is cut out, is not bled, so bled
    says so.
    """
    if image.mode == "RGBA" and bled:
        image = bleed(image, cover, whole)
    levels = [image]
    rects = [placed(box, image.size) for box in boxes] if boxes else None
    share = sampled_share(image.getchannel("A"), rects, image.size) if rects else None
    while levels[-1].size != (1, 1):
        above = levels[-1]
        size = (max(above.width // 2, 1), max(above.height // 2, 1))
        level = resized(above, size, Image.Resampling.BOX)
        if cover is not None:
            level = covered(above, cover, size, level, 4 if whole else 3)
            cover = cover.resize(size, Image.Resampling.BOX)
        if share is not None:
            alpha = level.getchannel("A")
            scale = sampled_scale(alpha, rects, image.size, share)
            level.putalpha(alpha.point(lambda value: min(255, round(value * scale))))
        levels.append(level)
    return levels


def covered(above, cover, size, plain, bands):
    """
    A level of size from the level above, its first bands the means of the above's weighted by
    cover, how much of each of its texels the leaves cover, and the plain box's where they cover
    none, its other bands the plain box's, so a leaf a texel or two across keeps its own color and
    surface rather than taking the transparent texels' round it.
    """
    weight = cover.convert("F")
    total = weight.resize(size, Image.Resampling.BOX)
    result = []
    for index, band in enumerate(above.split()):
        if index >= bands:
            result.append(plain.getchannel(index))
            continue
        summed = ImageMath.lambda_eval(lambda image: image["b"] * image["w"], b=band.convert("F"), w=weight).resize(size, Image.Resampling.BOX)
        mean = ImageMath.lambda_eval(lambda image: (image["s"] + image["p"] * 0.001) / (image["t"] + 0.001) + 0.5, s=summed, p=plain.getchannel(index).convert("F"), t=total)
        result.append(mean.convert("L"))
    return Image.merge(above.mode, result)


def bleed(image, cover=None, whole=False):
    """
    An image whose transparent texels take the color of the opaque ones nearest them, the
    opaque texels' colors averaged at each size from a texel up to the whole image and the
    finest average that reaches a texel taken for it. Where cover is given it says which texels are
    opaque in place of the image's own alpha, and where whole says so every channel is bled, the
    surface's alpha being its translucency rather than its opacity.
    """
    alpha = image.getchannel("A") if cover is None else cover
    if alpha.getextrema()[0] == 255:
        return image
    color = image if whole else image.convert("RGB")
    mask = alpha.point(lambda value: 255 if value > 0 else 0)
    premultiplied = Image.composite(color, Image.new(color.mode, color.size), mask)

    sizes = []
    size = color.size
    while size != (1, 1):
        size = (max(size[0] // 2, 1), max(size[1] // 2, 1))
        sizes.append(size)

    filled = color
    for size in reversed(sizes):
        weight = mask.resize(size, Image.Resampling.BOX).resize(color.size, Image.Resampling.BILINEAR)
        if whole:
            summed = resized(resized(premultiplied, size, Image.Resampling.BOX), color.size, Image.Resampling.BILINEAR)
        else:
            summed = premultiplied.resize(size, Image.Resampling.BOX).resize(color.size, Image.Resampling.BILINEAR)
        average = Image.merge(color.mode, [
            ImageMath.lambda_eval(lambda image: image["c"] * 255.0 / (image["w"] + 0.001), c=channel.convert("F"), w=weight.convert("F")).convert("L")
            for channel in summed.split()])
        reached = weight.point(lambda value: 255 if value > 0 else 0)
        filled = Image.composite(average, filled, reached)

    result = Image.composite(color, filled, mask)
    if not whole:
        result.putalpha(alpha)
    return result


def sampled_share(alpha, rects, size):
    """
    The leaves' mean share of their boxes past one half, a level's opacity read between its texels
    at the top level's size, as a card drawing the box samples it.
    """
    between = alpha.resize(size, Image.Resampling.BILINEAR)
    shares = []
    for rect in rects:
        histogram = between.crop(rect).histogram()
        shares.append(sum(histogram[128:]) / max(1, sum(histogram)))
    return sum(shares) / len(shares)


def sampled_scale(alpha, rects, size, share):
    """The scale of a level's opacity that keeps the leaves' sampled share nearest the top
    level's."""
    low, high = 0.25, 16.0
    for _ in range(20):
        middle = (low + high) / 2
        if sampled_share(alpha.point(lambda value: min(255, round(value * middle))), rects, size) < share:
            low = middle
        else:
            high = middle
    return high


def encode(name, levels):
    """
    The format of a map and its levels' blocks, with the encoding's error in decibels at the top
    level. The one place a map's format is chosen, so another, Basis Universal's, is a branch here.
    """
    if name == "normal":
        blocks = [bc5(level) for level in levels]
        return BC5_UNORM, blocks, psnr_bc5(levels[0], blocks[0])
    if name == "depth":
        blocks = [bc4(level) for level in levels]
        return BC4_UNORM, blocks, psnr_bc4(levels[0], blocks[0])
    blocks = [bc7(level) for level in levels]
    return BC7_SRGB if name == "albedo" else BC7_UNORM, blocks, psnr_bc7(levels[0], blocks[0])


def texel_blocks(image, channels):
    """The image's 4 by 4 blocks, each its sixteen texels of the channels, a level smaller than a
    block repeating its edge."""
    width, height = image.size
    data = image.tobytes()
    stride = len(image.getbands())
    for by in range(0, max(height, 4), 4):
        for bx in range(0, max(width, 4), 4):
            block = []
            for y in range(by, by + 4):
                row = min(y, height - 1) * width
                for x in range(bx, bx + 4):
                    at = (row + min(x, width - 1)) * stride
                    block.append(tuple(data[at:at + channels]))
            yield block


def bc7(image):
    """The image in BC7, every block in mode 6, its RGBA endpoints with a bit each shared by their
    channels."""
    out = bytearray()
    for block in texel_blocks(image.convert("RGBA"), 4):
        out += bc7_block(block)
    return bytes(out)


def bc7_block(texels):
    """
    A block in BC7, in mode 6, or in mode 1's two subsets where the block is opaque, mode 6's error
    passes MODE1_ABOVE and mode 1 holds it closer, as a block whose colors leave one line does, or in
    mode 7's two subsets with alpha where it is not opaque, as a leaf's cut edge is, and mode 6's
    error passes MODE7_ABOVE.
    """
    block, error = mode6_block(texels)
    opaque = all(texel[3] == 255 for texel in texels)
    if opaque and error > MODE1_ABOVE:
        found = mode1_block(texels)
        if found is not None and found[1] < error:
            return found[0]
    if not opaque and error > MODE7_ABOVE:
        found = mode7_block(texels)
        if found is not None and found[1] < error:
            return found[0]
    return block


def mode6_block(texels):
    """A block in BC7's mode 6 and its squared error over its texels' four channels."""
    # The endpoints along the principal axis of the block's colors, by a few rounds of the power
    # method on their covariance.
    count = 16.0
    mean = [sum(texel[c] for texel in texels) / count for c in range(4)]
    covariance = [[0.0] * 4 for _ in range(4)]
    for texel in texels:
        d = [texel[c] - mean[c] for c in range(4)]
        for i in range(4):
            for j in range(i, 4):
                covariance[i][j] += d[i] * d[j]
    for i in range(4):
        for j in range(i):
            covariance[i][j] = covariance[j][i]
    # The power method starts from the covariance's column of the channel that varies most, which
    # lies along the principal axis wherever the block has one. A fixed start of (1, 1, 1, 1) is
    # turned to nothing by a block whose red and blue move against each other, which then encoded
    # as its mean alone.
    widest = max(range(4), key=lambda c: covariance[c][c])
    axis = list(covariance[widest])
    for _ in range(6):
        axis = [sum(covariance[i][j] * axis[j] for j in range(4)) for i in range(4)]
        largest = max(abs(value) for value in axis)
        if largest == 0.0:
            break
        axis = [value / largest for value in axis]
    length = sum(value * value for value in axis)
    if length == 0.0:
        low = high = mean
    else:
        places = [sum((texel[c] - mean[c]) * axis[c] for c in range(4)) / length for texel in texels]
        low = [mean[c] + axis[c] * min(places) for c in range(4)]
        high = [mean[c] + axis[c] * max(places) for c in range(4)]

    # A block opaque throughout keeps the shared bit set, so its alpha decodes to 255 rather than
    # 254 where the color alone would have chosen the bit clear, and an opaque texture reads
    # opaque.
    shared = (1,) if all(texel[3] == 255 for texel in texels) else (0, 1)
    endpoints = quantize(low, shared), quantize(high, shared)
    indices = bc7_indices(texels, endpoints)

    # Fitted again by least squares to the indices found, kept where it lessens the error.
    refit = least_squares(texels, indices)
    if refit is not None:
        others = quantize(refit[0], shared), quantize(refit[1], shared)
        other_indices = bc7_indices(texels, others)
        if bc7_error(texels, others, other_indices) < bc7_error(texels, endpoints, indices):
            endpoints, indices = others, other_indices

    (q0, p0), (q1, p1) = endpoints
    if indices[0] >= 8:
        q0, q1, p0, p1 = q1, q0, p1, p0
        indices = [15 - index for index in indices]

    bits = 1 << 6
    shift = 7
    for c in range(4):
        bits |= q0[c] << shift
        bits |= q1[c] << (shift + 7)
        shift += 14
    bits |= p0 << shift | p1 << (shift + 1)
    shift += 2
    bits |= indices[0] << shift
    shift += 3
    for index in indices[1:]:
        bits |= index << shift
        shift += 4
    return bits.to_bytes(16, "little"), bc7_error(texels, ((q0, p0), (q1, p1)), indices)


def mode1_block(texels):
    """
    An opaque block in BC7's mode 1, two subsets of a partition each with its endpoints of six bits
    and a bit their channels share, three-bit indices, and its squared error over red, green and
    blue, or nothing where its colors are one. Each of the 64 partitions is judged by how far its
    subsets' colors lie off the lines through them, and the two nearest are encoded in full.
    """
    colors = [texel[:3] for texel in texels]
    if all(color == colors[0] for color in colors):
        return None
    best = None
    moments = [moments_of(color) for color in colors]
    total = [sum(m[k] for m in moments) for k in range(10)]
    for partition in sorted(range(64), key=lambda p: off_lines(moments, total, PARTITION_MASKS[p]))[:2]:
        encoded = mode1_partition(colors, partition)
        if best is None or encoded[1] < best[1]:
            best = encoded
    return best


def moments_of(color):
    """A color's count, its three channels and the six products of two channels, which summed over
    a subset give the subset's covariance."""
    r, g, b = color
    return (1, r, g, b, r * r, r * g, r * b, g * g, g * b, b * b)


def off_lines(moments, total, mask):
    """How far a partition's two subsets' colors lie off the lines through them, the spread of each
    less the spread along its principal axis, from the colors' moments summed over each subset, the
    second subset's sums taken from the whole block's less the first's."""
    second = [0] * 10
    at = mask
    while at:
        low = at & -at
        m = moments[low.bit_length() - 1]
        for k in range(10):
            second[k] += m[k]
        at ^= low
    result = 0.0
    for sums in ([total[k] - second[k] for k in range(10)], second):
        count = sums[0]
        if count < 2:
            continue
        mr, mg, mb = sums[1] / count, sums[2] / count, sums[3] / count
        rr, rg, rb = sums[4] - count * mr * mr, sums[5] - count * mr * mg, sums[6] - count * mr * mb
        gg, gb, bb = sums[7] - count * mg * mg, sums[8] - count * mg * mb, sums[9] - count * mb * mb
        covariance = ((rr, rg, rb), (rg, gg, gb), (rb, gb, bb))
        trace = rr + gg + bb
        widest = max(range(3), key=lambda c: covariance[c][c])
        axis = list(covariance[widest])
        for _ in range(4):
            axis = [covariance[i][0] * axis[0] + covariance[i][1] * axis[1] + covariance[i][2] * axis[2] for i in range(3)]
            largest = max(abs(value) for value in axis)
            if largest == 0.0:
                break
            axis = [value / largest for value in axis]
        length = axis[0] * axis[0] + axis[1] * axis[1] + axis[2] * axis[2]
        along = 0.0 if length == 0.0 else sum(axis[i] * (covariance[i][0] * axis[0] + covariance[i][1] * axis[1] + covariance[i][2] * axis[2]) for i in range(3)) / length
        result += trace - along
    return result


def mode1_partition(colors, partition):
    """The block in mode 1 at a partition, and its squared error over red, green and blue."""
    subsets = [[i for i in range(16) if PARTITIONS[partition][i] == str(s)] for s in (0, 1)]
    anchors = (0, SECOND_ANCHORS[partition])
    endpoints, indices, error = [None, None], [0] * 16, 0
    for s in (0, 1):
        members = [colors[i] for i in subsets[s]]
        ends, chosen, subset_error = mode1_subset(members)
        # The anchor's index keeps its top bit clear, so the subset's ends are turned over where it
        # is set.
        if chosen[subsets[s].index(anchors[s])] >= 4:
            ends = (ends[1], ends[0])
            chosen = [7 - index for index in chosen]
        endpoints[s] = ends
        for i, index in zip(subsets[s], chosen):
            indices[i] = index
        error += subset_error

    bits = 1 << 1
    shift = 2
    bits |= partition << shift
    shift += 6
    for c in range(3):
        for s in (0, 1):
            for e in (0, 1):
                bits |= endpoints[s][e][0][c] << shift
                shift += 6
    for s in (0, 1):
        bits |= endpoints[s][0][1] << shift
        shift += 1
    for i in range(16):
        width = 2 if i in anchors else 3
        bits |= indices[i] << shift
        shift += width
    return bits.to_bytes(16, "little"), error


def mode1_subset(colors):
    """A subset's ends, each six bits a channel with the bit the two share, its three-bit indices and
    its squared error, along its colors' principal axis and fitted again by least squares."""
    low, high = principal_ends(colors)
    best = None
    for bit in (0, 1):
        ends = (mode1_quantize(low, bit), mode1_quantize(high, bit))
        chosen = mode1_indices(colors, ends)
        refit = least_squares_weighted(colors, chosen, WEIGHTS3)
        candidates = [(ends, chosen)]
        if refit is not None:
            others = (mode1_quantize(refit[0], bit), mode1_quantize(refit[1], bit))
            candidates.append((others, mode1_indices(colors, others)))
        for ends, chosen in candidates:
            error = mode1_error(colors, ends, chosen)
            if best is None or error < best[2]:
                best = (ends, chosen, error)
    return best


def principal_ends(colors):
    """The ends of a set of colors of any number of channels along their principal axis, by the power
    method started from the covariance's column of the channel that varies most."""
    count = float(len(colors))
    n = len(colors[0])
    mean = [sum(color[c] for color in colors) / count for c in range(n)]
    covariance = [[sum((color[i] - mean[i]) * (color[j] - mean[j]) for color in colors) for j in range(n)] for i in range(n)]
    widest = max(range(n), key=lambda c: covariance[c][c])
    axis = list(covariance[widest])
    for _ in range(6):
        axis = [sum(covariance[i][j] * axis[j] for j in range(n)) for i in range(n)]
        largest = max(abs(value) for value in axis)
        if largest == 0.0:
            break
        axis = [value / largest for value in axis]
    length = sum(value * value for value in axis)
    if length == 0.0:
        return mean, mean
    places = [sum((color[c] - mean[c]) * axis[c] for c in range(n)) / length for color in colors]
    return [mean[c] + axis[c] * min(places) for c in range(n)], [mean[c] + axis[c] * max(places) for c in range(n)]


def mode1_quantize(endpoint, bit):
    """An end's six bits a channel nearest it with the shared bit given."""
    values = []
    for c in endpoint:
        c = max(0.0, min(255.0, c))
        values.append(min(range(max(0, int(c / 4) - 1), min(63, int(c / 4) + 1) + 1), key=lambda v: abs(mode1_expand(v, bit) - c)))
    return values, bit


def mode1_expand(value, bit):
    """Six bits and the shared bit widened to eight, the top bit repeated below them."""
    seven = value << 1 | bit
    return seven << 1 | seven >> 6


def mode1_indices(colors, ends):
    c0 = [mode1_expand(v, ends[0][1]) for v in ends[0][0]]
    c1 = [mode1_expand(v, ends[1][1]) for v in ends[1][0]]
    d = [b - a for a, b in zip(c0, c1)]
    dd = sum(value * value for value in d)
    if dd == 0:
        return [0] * len(colors)
    return [NEAREST3[max(0, min(64, int(sum((color[c] - c0[c]) * d[c] for c in range(3)) * 64 / dd + 0.5)))] for color in colors]


def mode1_error(colors, ends, indices):
    c0 = [mode1_expand(v, ends[0][1]) for v in ends[0][0]]
    c1 = [mode1_expand(v, ends[1][1]) for v in ends[1][0]]
    error = 0
    for color, index in zip(colors, indices):
        w = WEIGHTS3[index]
        for c in range(3):
            error += (((64 - w) * c0[c] + w * c1[c] + 32 >> 6) - color[c]) ** 2
    return error


def least_squares_weighted(colors, indices, weights):
    """The ends that best give the colors at the weights the indices name, or nothing where they are
    all one."""
    a00 = a01 = a11 = 0.0
    channels = len(colors[0])
    b0 = [0.0] * channels
    b1 = [0.0] * channels
    for color, index in zip(colors, indices):
        t = weights[index] / 64.0
        s = 1.0 - t
        a00 += s * s
        a01 += s * t
        a11 += t * t
        for c in range(channels):
            b0[c] += s * color[c]
            b1[c] += t * color[c]
    determinant = a00 * a11 - a01 * a01
    if abs(determinant) < 1e-9:
        return None
    return ([(a11 * b0[c] - a01 * b1[c]) / determinant for c in range(channels)],
            [(a00 * b1[c] - a01 * b0[c]) / determinant for c in range(channels)])


def mode7_block(texels):
    """
    A block in BC7's mode 7, two subsets of a partition each with its RGBA endpoints of five bits a
    channel and a bit of their own, two-bit indices, and its squared error over the four channels,
    or nothing where its texels are one. Each of the 64 partitions is judged by how far its subsets'
    texels lie off the lines through them, and the two nearest are encoded in full.
    """
    if all(texel == texels[0] for texel in texels):
        return None
    best = None
    for partition in sorted(range(64), key=lambda p: off_lines4(texels, PARTITION_MASKS[p]))[:2]:
        encoded = mode7_partition(texels, partition)
        if best is None or encoded[1] < best[1]:
            best = encoded
    return best


def off_lines4(texels, mask):
    """How far a partition's two subsets' texels lie off the lines through them in RGBA, the spread
    of each less the spread along its principal axis."""
    total = 0.0
    for subset in (0, 1):
        members = [texels[i] for i in range(16) if (mask >> i & 1) == subset]
        if len(members) < 2:
            continue
        count = float(len(members))
        mean = [sum(m[c] for m in members) / count for c in range(4)]
        covariance = [[sum((m[i] - mean[i]) * (m[j] - mean[j]) for m in members) for j in range(4)] for i in range(4)]
        trace = sum(covariance[c][c] for c in range(4))
        widest = max(range(4), key=lambda c: covariance[c][c])
        axis = list(covariance[widest])
        for _ in range(4):
            axis = [sum(covariance[i][j] * axis[j] for j in range(4)) for i in range(4)]
            largest = max(abs(value) for value in axis)
            if largest == 0.0:
                break
            axis = [value / largest for value in axis]
        length = sum(value * value for value in axis)
        along = 0.0 if length == 0.0 else sum(axis[i] * sum(covariance[i][j] * axis[j] for j in range(4)) for i in range(4)) / length
        total += trace - along
    return total


def mode7_partition(texels, partition):
    """The block in mode 7 at a partition, and its squared error over the four channels."""
    subsets = [[i for i in range(16) if PARTITIONS[partition][i] == str(s)] for s in (0, 1)]
    anchors = (0, SECOND_ANCHORS[partition])
    endpoints, indices, error = [None, None], [0] * 16, 0
    for s in (0, 1):
        members = [texels[i] for i in subsets[s]]
        ends, chosen, subset_error = mode7_subset(members)
        # The anchor's index keeps its top bit clear, so the subset's ends are turned over where it
        # is set.
        if chosen[subsets[s].index(anchors[s])] >= 2:
            ends = (ends[1], ends[0])
            chosen = [3 - index for index in chosen]
        endpoints[s] = ends
        for i, index in zip(subsets[s], chosen):
            indices[i] = index
        error += subset_error

    bits = 1 << 7
    shift = 8
    bits |= partition << shift
    shift += 6
    for c in range(4):
        for s in (0, 1):
            for e in (0, 1):
                bits |= endpoints[s][e][0][c] << shift
                shift += 5
    for s in (0, 1):
        for e in (0, 1):
            bits |= endpoints[s][e][1] << shift
            shift += 1
    for i in range(16):
        width = 1 if i in anchors else 2
        bits |= indices[i] << shift
        shift += width
    return bits.to_bytes(16, "little"), error


def mode7_subset(texels):
    """A subset's ends, each five bits a channel with a bit of its own, its two-bit indices and its
    squared error, along its texels' principal axis and fitted again by least squares."""
    low, high = principal_ends(texels)
    ends = (mode7_quantize(low), mode7_quantize(high))
    chosen = mode7_indices(texels, ends)
    best = (ends, chosen, mode7_error(texels, ends, chosen))
    refit = least_squares_weighted(texels, chosen, WEIGHTS2)
    if refit is not None:
        others = (mode7_quantize(refit[0]), mode7_quantize(refit[1]))
        chosen = mode7_indices(texels, others)
        error = mode7_error(texels, others, chosen)
        if error < best[2]:
            best = (others, chosen, error)
    return best


def mode7_quantize(endpoint):
    """An end's five bits a channel and its own bit, the pair nearest it."""
    best = None
    for bit in (0, 1):
        values = []
        for c in endpoint:
            c = max(0.0, min(255.0, c))
            values.append(min(range(max(0, int(c / 8) - 1), min(31, int(c / 8) + 1) + 1), key=lambda v: abs(mode7_expand(v, bit) - c)))
        error = sum((mode7_expand(v, bit) - max(0.0, min(255.0, c))) ** 2 for v, c in zip(values, endpoint))
        if best is None or error < best[0]:
            best = (error, values, bit)
    return best[1], best[2]


def mode7_expand(value, bit):
    """Five bits and the end's own bit widened to eight, the top bits repeated below them."""
    six = value << 1 | bit
    return six << 2 | six >> 4


def mode7_indices(texels, ends):
    c0 = [mode7_expand(v, ends[0][1]) for v in ends[0][0]]
    c1 = [mode7_expand(v, ends[1][1]) for v in ends[1][0]]
    d = [b - a for a, b in zip(c0, c1)]
    dd = sum(value * value for value in d)
    if dd == 0:
        return [0] * len(texels)
    return [NEAREST2[max(0, min(64, int(sum((texel[c] - c0[c]) * d[c] for c in range(4)) * 64 / dd + 0.5)))] for texel in texels]


def mode7_error(texels, ends, indices):
    c0 = [mode7_expand(v, ends[0][1]) for v in ends[0][0]]
    c1 = [mode7_expand(v, ends[1][1]) for v in ends[1][0]]
    error = 0
    for texel, index in zip(texels, indices):
        w = WEIGHTS2[index]
        for c in range(4):
            error += (((64 - w) * c0[c] + w * c1[c] + 32 >> 6) - texel[c]) ** 2
    return error


def quantize(endpoint, bits=(0, 1)):
    """An endpoint's seven bits a channel and the bit they share, the pair nearest it of the bits allowed."""
    best = None
    for bit in bits:
        values = [max(0, min(127, int((max(0.0, min(255.0, c)) - bit) / 2 + 0.5))) for c in endpoint]
        error = sum(((value << 1 | bit) - c) ** 2 for value, c in zip(values, endpoint))
        if best is None or error < best[0]:
            best = (error, values, bit)
    return best[1], best[2]


def expand(endpoint):
    values, bit = endpoint
    return [value << 1 | bit for value in values]


def bc7_indices(texels, endpoints):
    c0, c1 = expand(endpoints[0]), expand(endpoints[1])
    d = [b - a for a, b in zip(c0, c1)]
    dd = sum(value * value for value in d)
    if dd == 0:
        return [0] * 16
    return [NEAREST[max(0, min(64, int(sum((texel[c] - c0[c]) * d[c] for c in range(4)) * 64 / dd + 0.5)))] for texel in texels]


def bc7_error(texels, endpoints, indices):
    c0, c1 = expand(endpoints[0]), expand(endpoints[1])
    error = 0
    for texel, index in zip(texels, indices):
        w = WEIGHTS[index]
        for c in range(4):
            error += (((64 - w) * c0[c] + w * c1[c] + 32 >> 6) - texel[c]) ** 2
    return error


def least_squares(texels, indices):
    """The endpoints that best give the texels at the weights the indices name, or nothing where
    they are all one."""
    a00 = a01 = a11 = 0.0
    b0 = [0.0] * 4
    b1 = [0.0] * 4
    for texel, index in zip(texels, indices):
        t = WEIGHTS[index] / 64.0
        s = 1.0 - t
        a00 += s * s
        a01 += s * t
        a11 += t * t
        for c in range(4):
            b0[c] += s * texel[c]
            b1[c] += t * texel[c]
    determinant = a00 * a11 - a01 * a01
    if abs(determinant) < 1e-9:
        return None
    return ([(a11 * b0[c] - a01 * b1[c]) / determinant for c in range(4)],
            [(a00 * b1[c] - a01 * b0[c]) / determinant for c in range(4)])


def bc5(image):
    """The image's red and green in BC5, each a BC4 block of its own."""
    out = bytearray()
    for block in texel_blocks(image.convert("RGB"), 2):
        out += bc4_block([texel[0] for texel in block])
        out += bc4_block([texel[1] for texel in block])
    return bytes(out)


def bc4(image):
    """The image's one channel in BC4."""
    out = bytearray()
    for block in texel_blocks(image.convert("L"), 1):
        out += bc4_block([texel[0] for texel in block])
    return bytes(out)


def psnr_bc4(image, blocks):
    total = 0
    count = 0
    for at, texels in enumerate(texel_blocks(image.convert("L"), 1)):
        block = blocks[8 * at:8 * at + 8]
        palette = bc4_palette(block)
        bits = int.from_bytes(block, "little") >> 16
        for i, texel in enumerate(texels):
            total += (palette[bits >> (3 * i) & 7] - texel[0]) ** 2
            count += 1
    return psnr(total, count)


def bc4_block(values):
    """One channel's block in BC4's eight-step mode, its highest value first."""
    high, low = max(values), min(values)
    bits = high | low << 8
    if high != low:
        for i, value in enumerate(values):
            step = int((value - low) * 7 / (high - low) + 0.5)
            index = 0 if step == 7 else 1 if step == 0 else 8 - step
            bits |= index << (16 + 3 * i)
    return bits.to_bytes(8, "little")


def bc4_palette(block):
    high, low = block[0], block[1]
    return [high, low] + [((8 - i) * high + (i - 1) * low + 3) // 7 for i in range(2, 8)]


def psnr(errors, count):
    return 99.0 if errors == 0 else 10 * math.log10(255 * 255 * count / errors)


def psnr_bc7(image, blocks):
    """The top level's error against its blocks, each decoded in the mode it was written in, in
    decibels."""
    total = 0
    count = 0
    for at, texels in enumerate(texel_blocks(image.convert("RGBA"), 4)):
        for texel, decoded in zip(texels, bc7_decoded(blocks[16 * at:16 * at + 16])):
            total += sum((a - b) ** 2 for a, b in zip(texel, decoded))
            count += 4
    return psnr(total, count)


def bc7_decoded(block):
    """A block's sixteen texels as RGBA, decoded in mode 6, mode 1 or mode 7, those the encoder
    writes."""
    bits = int.from_bytes(block, "little")
    if bits & 1 << 7 and not bits & 127:
        partition = bits >> 8 & 63
        shift = 14
        raw = [[[0] * 4, [0] * 4], [[0] * 4, [0] * 4]]
        for c in range(4):
            for s in (0, 1):
                for e in (0, 1):
                    raw[s][e][c] = bits >> shift & 31
                    shift += 5
        own = [[0, 0], [0, 0]]
        for s in (0, 1):
            for e in (0, 1):
                own[s][e] = bits >> shift & 1
                shift += 1
        anchors = (0, SECOND_ANCHORS[partition])
        texels = []
        for i in range(16):
            width = 1 if i in anchors else 2
            index = bits >> shift & (1 << width) - 1
            shift += width
            s = int(PARTITIONS[partition][i])
            c0 = [mode7_expand(v, own[s][0]) for v in raw[s][0]]
            c1 = [mode7_expand(v, own[s][1]) for v in raw[s][1]]
            w = WEIGHTS2[index]
            texels.append(tuple((64 - w) * a + w * b + 32 >> 6 for a, b in zip(c0, c1)))
        return texels
    if bits & 1 << 6 and not bits & 63:
        shift = 7
        c0, c1 = [], []
        for _ in range(4):
            c0.append(bits >> shift & 127)
            c1.append(bits >> (shift + 7) & 127)
            shift += 14
        p0, p1 = bits >> shift & 1, bits >> (shift + 1) & 1
        shift += 2
        c0 = [value << 1 | p0 for value in c0]
        c1 = [value << 1 | p1 for value in c1]
        indices = [bits >> shift & 7]
        shift += 3
        for _ in range(15):
            indices.append(bits >> shift & 15)
            shift += 4
        return [tuple((64 - WEIGHTS[i]) * a + WEIGHTS[i] * b + 32 >> 6 for a, b in zip(c0, c1)) for i in indices]

    # Mode 1, its mode's bits 01.
    partition = bits >> 2 & 63
    shift = 8
    raw = [[[0, 0, 0], [0, 0, 0]], [[0, 0, 0], [0, 0, 0]]]
    for c in range(3):
        for s in (0, 1):
            for e in (0, 1):
                raw[s][e][c] = bits >> shift & 63
                shift += 6
    shared = [bits >> shift & 1, bits >> (shift + 1) & 1]
    shift += 2
    anchors = (0, SECOND_ANCHORS[partition])
    texels = []
    for i in range(16):
        width = 2 if i in anchors else 3
        index = bits >> shift & (1 << width) - 1
        shift += width
        s = int(PARTITIONS[partition][i])
        c0 = [mode1_expand(v, shared[s]) for v in raw[s][0]]
        c1 = [mode1_expand(v, shared[s]) for v in raw[s][1]]
        w = WEIGHTS3[index]
        texels.append(tuple((64 - w) * a + w * b + 32 >> 6 for a, b in zip(c0, c1)) + (255,))
    return texels

def psnr_bc5(image, blocks):
    total = 0
    count = 0
    for at, texels in enumerate(texel_blocks(image.convert("RGB"), 2)):
        for channel in range(2):
            block = blocks[16 * at + 8 * channel:16 * at + 8 * channel + 8]
            palette = bc4_palette(block)
            bits = int.from_bytes(block, "little") >> 16
            for i, texel in enumerate(texels):
                total += (palette[bits >> (3 * i) & 7] - texel[channel]) ** 2
                count += 1
    return psnr(total, count)


def ktx2(format, levels, blocks):
    """
    A KTX2 file of the levels' blocks as they are, the largest first in the index and last in the
    file, each level at a multiple of a block's sixteen bytes, as the format asks of levels kept as
    they are.
    """
    width, height = levels[0].size
    descriptor = data_format(format)
    pairs = [(b"KTXwriter", b"build/textures.py")]
    key_values = b""
    for key, value in pairs:
        entry = key + b"\0" + value + b"\0"
        key_values += struct.pack("<I", len(entry)) + entry
        key_values += b"\0" * (-len(key_values) % 4)

    header_size = 12 + 9 * 4 + 4 * 4 + 2 * 8
    index_size = 24 * len(levels)
    descriptor_at = header_size + index_size
    key_values_at = descriptor_at + len(descriptor)
    data_at = key_values_at + len(key_values)

    # The levels' bytes from the smallest to the largest, as the format lays them out.
    offsets = [0] * len(levels)
    at = data_at
    for level in reversed(range(len(levels))):
        at += -at % 16
        offsets[level] = at
        at += len(blocks[level])

    out = bytearray(b"\xabKTX 20\xbb\r\n\x1a\n")
    out += struct.pack("<9I", format, 1, width, height, 0, 0, 1, len(levels), 0)
    out += struct.pack("<4I", descriptor_at, len(descriptor), key_values_at, len(key_values))
    out += struct.pack("<2Q", 0, 0)
    for level in range(len(levels)):
        out += struct.pack("<3Q", offsets[level], len(blocks[level]), len(blocks[level]))
    out += descriptor
    out += key_values
    for level in reversed(range(len(levels))):
        out += b"\0" * (offsets[level] - len(out))
        out += blocks[level]
    return bytes(out)


def data_format(format):
    """The data format descriptor of a block format, its total size first and its one basic
    block."""
    if format == BC4_UNORM:
        model, transfer = MODEL_BC4, TRANSFER_LINEAR
        samples = [(0, 63, 0)]
    elif format == BC5_UNORM:
        model, transfer = MODEL_BC5, TRANSFER_LINEAR
        samples = [(0, 63, 0), (64, 63, 1)]
    else:
        model, transfer = MODEL_BC7, TRANSFER_SRGB if format == BC7_SRGB else TRANSFER_LINEAR
        samples = [(0, 127, 0)]
    block_size = 24 + 16 * len(samples)
    block = struct.pack("<II", 0, 2 | block_size << 16)
    block += struct.pack("<4B", model, 1, transfer, 0)
    block += struct.pack("<4B", 3, 3, 0, 0)
    block += struct.pack("<8B", 8 if format == BC4_UNORM else 16, 0, 0, 0, 0, 0, 0, 0)
    for offset, length, channel in samples:
        block += struct.pack("<HBB4BII", offset, length, channel, 0, 0, 0, 0, 0, 0xFFFFFFFF)
    return struct.pack("<I", 4 + len(block)) + block


if __name__ == "__main__":
    sys.exit(main())
