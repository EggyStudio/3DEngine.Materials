using System.Buffers.Binary;

namespace Materials.Tests;

/// <summary>KTX2 files made in a test, as the specification lays them out.</summary>
internal static class Ktx2Files
{
    // A KTX2 file as the specification lays it out, the header, the index, the level index and the
    // levels, the smallest first in the file, with no data format descriptor, which this reads none
    // of.
    public static byte[] Write(uint vkFormat, int width, int height, byte[][] levels)
    {
        int at = 80 + 24 * levels.Length;
        var offsets = new long[levels.Length];
        for (int level = levels.Length - 1; level >= 0; level--)
        {
            offsets[level] = at;
            at += levels[level].Length;
        }

        var file = new byte[at];
        ReadOnlySpan<byte> identifier = [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];
        identifier.CopyTo(file);
        uint[] header = [vkFormat, 1, (uint)width, (uint)height, 0, 0, 1, (uint)levels.Length, 0];
        for (int i = 0; i < header.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12 + 4 * i), header[i]);
        for (int level = 0; level < levels.Length; level++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(80 + 24 * level), (ulong)offsets[level]);
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(88 + 24 * level), (ulong)levels[level].Length);
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(96 + 24 * level), (ulong)levels[level].Length);
            levels[level].CopyTo(file.AsSpan((int)offsets[level]));
        }

        return file;
    }
}
