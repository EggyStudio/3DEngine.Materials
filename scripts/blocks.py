#!/usr/bin/env python3
"""Writes the block decoders' reference, Materials.Tests/references/blocks.json, from Pillow's own
decoders, which are written apart from the module's, so the tests read the module's against them.

    scripts/blocks.py

Each block is drawn from a seeded generator and decoded by Pillow as a picture in a DDS file,
sixteen a case. A BC7 mode's fields after its own bits are counted through rather than drawn, one
block a partition in a mode of partitions, so every row of the partition tables and every anchor is
read, and mode 4's rotations with its index selection and mode 5's rotations each in turn. The cases
are BC1 with its first color the greater and with it not, BC4 with its first end the greater and
with it not, BC5, BC7 in each of its eight modes, and a picture of BC1, of BC5 and of BC7 whose
sides are not multiples of four, whose blocks are cut at the right and the bottom. The reserved
block of no mode is left to the tests, since Pillow decodes it as opaque black where the format's
specification gives zero in every channel. A texel is written as a GPU samples the format, so BC4's
value is red alone and BC5's two are red and green, with green and blue zero where the format holds
nothing and alpha 255. The same Pillow gives the same file, and the file names the version it was
written with.
"""

import io
import json
import os
import random
import struct

import PIL
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Materials.Tests", "references", "blocks.json")

# DXGI's numbers for the formats, which a DDS file's DX10 header names them by.
DXGI = {"Bc1": 71, "Bc4": 80, "Bc5": 83, "Bc7": 98}
COUNT = 16


def dds(dxgi, blocks, width, height):
    """A DDS file of a picture's blocks with its DX10 header."""
    header = bytearray(124)
    struct.pack_into("<7I", header, 0, 124, 0x1 | 0x2 | 0x4 | 0x1000 | 0x80000, height, width, len(blocks), 0, 1)
    struct.pack_into("<2I4s", header, 72, 32, 0x4, b"DX10")
    struct.pack_into("<I", header, 104, 0x1000)
    return b"DDS " + bytes(header) + struct.pack("<5I", dxgi, 3, 0, 1, 0) + blocks


def texels(format, blocks, width=4, height=4):
    """The picture's texels as RGBA bytes, rows from the top, as a GPU samples the format."""
    image = Image.open(io.BytesIO(dds(DXGI[format], blocks, width, height)))
    image.load()
    raw = image.tobytes()
    if format == "Bc4":
        return b"".join(bytes((value, 0, 0, 255)) for value in raw)
    if format == "Bc5":
        return b"".join(bytes((raw[i], raw[i + 1], 0, 255)) for i in range(0, len(raw), 3))
    return raw


def bc1(rng, greater):
    block = bytearray(rng.getrandbits(8) for _ in range(8))
    c0, c1 = struct.unpack_from("<2H", block)
    low, high = sorted((c0, c1))
    if low == high:
        high = min(high + 1, 0xFFFF)
        low = high - 1
    struct.pack_into("<2H", block, 0, *((high, low) if greater else (low, high)))
    return bytes(block)


def bc4(rng, greater):
    block = bytearray(rng.getrandbits(8) for _ in range(8))
    low, high = sorted(block[:2])
    block[0], block[1] = (high, low) if greater else (low, high)
    if greater and block[0] == block[1]:
        block[0] = min(255, block[0] + 1)
        block[1] = block[0] - 1
    return bytes(block)


# How many bits each BC7 mode gives its partition, after the mode's own bits.
PARTITION_BITS = (4, 6, 6, 6, 0, 0, 0, 6)


# How many bits each BC7 mode gives its rotation and its index selection after the partition's.
ROTATION_BITS = (0, 0, 0, 0, 2, 2, 0, 0)
SELECTION_BITS = (0, 0, 0, 0, 1, 0, 0, 0)


def bc7(rng, mode, case=0):
    """
    A block of random bits in a mode, the mode's bits a run of zeros then a one, and the fields
    after them set from the case, the partition where the mode has partitions and the rotation and
    the index selection where it has those, so a run of cases reads each value.
    """
    value = rng.getrandbits(128)
    value = (value >> (mode + 1) << (mode + 1)) | (1 << mode)
    bits = PARTITION_BITS[mode] + ROTATION_BITS[mode] + SELECTION_BITS[mode]
    if bits:
        value = (value & ~(((1 << bits) - 1) << (mode + 1))) | ((case % (1 << bits)) << (mode + 1))
    return value.to_bytes(16, "little")


def main():
    rng = random.Random(20261118)
    cases = []

    def case(name, format, blocks, width=4, height=4):
        cases.append({"name": name, "format": format, "width": width, "height": height,
                      "blocks": [{"block": b.hex(), "texels": texels(format, b, width, height).hex()} for b in blocks]})

    case("BC1, the first color the greater", "Bc1", [bc1(rng, True) for _ in range(COUNT)])
    case("BC1, the first color not the greater", "Bc1", [bc1(rng, False) for _ in range(COUNT)])
    case("BC4, the first end the greater", "Bc4", [bc4(rng, True) for _ in range(COUNT)])
    case("BC4, the first end not the greater", "Bc4", [bc4(rng, False) for _ in range(COUNT)])
    case("BC5", "Bc5", [bc4(rng, rng.random() < 0.5) + bc4(rng, rng.random() < 0.5) for _ in range(COUNT)])
    for mode in range(8):
        count = max(COUNT, 1 << PARTITION_BITS[mode])
        case(f"BC7, mode {mode}", "Bc7", [bc7(rng, mode, i) for i in range(count)])

    # Pictures whose sides are not multiples of four, whole blocks cut at the right and the bottom.
    case("BC1, a 7 by 3 picture", "Bc1", [bc1(rng, i % 2 == 0) + bc1(rng, i % 2 == 1) for i in range(4)], 7, 3)
    case("BC5, a 5 by 6 picture", "Bc5", [b"".join(bc4(rng, True) + bc4(rng, False) for _ in range(4)) for _ in range(4)], 5, 6)
    case("BC7, a 6 by 5 picture", "Bc7", [b"".join(bc7(rng, rng.randrange(8), rng.getrandbits(8)) for _ in range(4)) for _ in range(4)], 6, 5)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", newline="\n") as file:
        json.dump({"pillow": PIL.__version__, "cases": cases}, file, indent=1)
        file.write("\n")
    print(f"{OUT}, {sum(len(c['blocks']) for c in cases)} blocks in {len(cases)} cases")


if __name__ == "__main__":
    main()
