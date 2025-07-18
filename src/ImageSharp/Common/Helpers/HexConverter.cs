// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Common.Helpers;

internal static class HexConverter
{
    /// <summary>
    /// Parses a hexadecimal string into a byte array without allocations. Throws on non-hexadecimal character.
    /// Adapted from https://source.dot.net/#System.Private.CoreLib/Convert.cs,c9e4fbeaca708991.
    /// </summary>
    /// <param name="chars">The hexadecimal string to parse.</param>
    /// <param name="bytes">The destination for the parsed bytes. Must be at least <paramref name="chars"/>.Length / 2 bytes long.</param>
    /// <returns>The number of bytes written to <paramref name="bytes"/>.</returns>
    public static int HexStringToBytes(ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        if (Numerics.Modulo2(chars.Length) != 0)
        {
            throw new ArgumentException("Input string length must be a multiple of 2", nameof(chars));
        }

        if ((bytes.Length << 1 /* bit-hack for *2 */) < chars.Length)
        {
            throw new ArgumentException("Output span must be at least half the length of the input string");
        }

        // Slightly better performance in the loop below, allows us to skip a bounds check
        // while still supporting output buffers that are larger than necessary
        bytes = bytes[..(chars.Length >> 1)];   // bit-hack for / 2

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int FromChar(int c)
        {
            // Map from an ASCII char to its hex value, e.g. arr['b'] == 11. 0xFF means it's not a hex digit.
            // This doesn't actually allocate.
            ReadOnlySpan<byte> charToHexLookup =
            [
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
                0x0,  0x1,  0x2,  0x3,  0x4,  0x5,  0x6,  0x7,  0x8,  0x9,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
                0xFF, 0xA,  0xB,  0xC,  0xD,  0xE,  0xF,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 79
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
                0xFF, 0xa,  0xb,  0xc,  0xd,  0xe,  0xf,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 111
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 127
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 143
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 159
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 175
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 191
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 207
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 223
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 239
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF // 255
            ];

            return (uint)c >= (uint)charToHexLookup.Length ? 0xFF : charToHexLookup[c];
        }

        // See https://source.dot.net/#System.Private.CoreLib/HexConverter.cs,4681d45a0aa0b361
        int i = 0;
        int j = 0;
        int byteLo = 0;
        int byteHi = 0;
        while (j < bytes.Length)
        {
            byteLo = FromChar(chars[i + 1]);
            byteHi = FromChar(chars[i]);

            // byteHi hasn't been shifted to the high half yet, so the only way the bitwise or produces this pattern
            // is if either byteHi or byteLo was not a hex character.
            if ((byteLo | byteHi) == 0xFF)
            {
                break;
            }

            bytes[j++] = (byte)((byteHi << 4) | byteLo);
            i += 2;
        }

        if (byteLo == 0xFF)
        {
            i++;
        }

        if ((byteLo | byteHi) == 0xFF)
        {
            throw new ArgumentException("Input string contained non-hexadecimal characters", nameof(chars));
        }

        return j;
    }
}
