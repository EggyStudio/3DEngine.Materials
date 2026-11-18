#!/usr/bin/env python3
"""Writes the block decoders' reference, Materials.Tests/references/blocks.json, from Pillow's own
decoders, which are written apart from the module's, so the tests read the module's against them.

    scripts/blocks.py

Each block is drawn from a seeded generator and decoded by Pillow as a 4 by 4 picture in a DDS file,
sixteen a case, and one a partition in a BC7 mode of partitions, so every row of the partition
tables and every anchor is read. The cases are BC1 with its first color the greater and with it not,
BC4 with its first end the greater and with it not, BC5, and BC7 in each of its eight modes, the
reserved block of no mode left to the tests, since Pillow decodes it as opaque black where the
format's specification gives zero in every channel. A texel is written as a GPU samples the format,
so BC4's value is red alone and BC5's two are red and green, with green and blue zero where the
format holds nothing and alpha 255. The same Pillow gives the same file, and the file names the
version it was written with.
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


def dds(dxgi, block):
    """A DDS file of one block, a 4 by 4 picture with its DX10 header."""
    header = bytearray(124)
    struct.pack_into("<7I", header, 0, 124, 0x1 | 0x2 | 0x4 | 0x1000 | 0x80000, 4, 4, len(block), 0, 1)
    struct.pack_into("<2I4s", header, 72, 32, 0x4, b"DX10")
    struct.pack_into("<I", header, 104, 0x1000)
    return b"DDS " + bytes(header) + struct.pack("<5I", dxgi, 3, 0, 1, 0) + block


def texels(format, block):
    """The block's sixteen texels as RGBA bytes, rows from the top, as a GPU samples the format."""
    image = Image.open(io.BytesIO(dds(DXGI[format], block)))
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


def bc7(rng, mode, partition=0):
    """
    A block of random bits in a mode, the mode's bits a run of zeros then a one, and the partition
    given where the mode has partitions.
    """
    value = rng.getrandbits(128)
    value = (value >> (mode + 1) << (mode + 1)) | (1 << mode)
    bits = PARTITION_BITS[mode]
    if bits:
        value = (value & ~(((1 << bits) - 1) << (mode + 1))) | (partition << (mode + 1))
    return value.to_bytes(16, "little")


def main():
    rng = random.Random(20261118)
    cases = []

    def case(name, format, blocks):
        cases.append({"name": name, "format": format,
                      "blocks": [{"block": b.hex(), "texels": texels(format, b).hex()} for b in blocks]})

    case("BC1, the first color the greater", "Bc1", [bc1(rng, True) for _ in range(COUNT)])
    case("BC1, the first color not the greater", "Bc1", [bc1(rng, False) for _ in range(COUNT)])
    case("BC4, the first end the greater", "Bc4", [bc4(rng, True) for _ in range(COUNT)])
    case("BC4, the first end not the greater", "Bc4", [bc4(rng, False) for _ in range(COUNT)])
    case("BC5", "Bc5", [bc4(rng, rng.random() < 0.5) + bc4(rng, rng.random() < 0.5) for _ in range(COUNT)])
    for mode in range(8):
        count = 1 << PARTITION_BITS[mode] if PARTITION_BITS[mode] else COUNT
        case(f"BC7, mode {mode}", "Bc7", [bc7(rng, mode, i) for i in range(count)])

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", newline="\n") as file:
        json.dump({"pillow": PIL.__version__, "cases": cases}, file, indent=1)
        file.write("\n")
    print(f"{OUT}, {sum(len(c['blocks']) for c in cases)} blocks in {len(cases)} cases")


if __name__ == "__main__":
    main()
