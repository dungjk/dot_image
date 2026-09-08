// Ported from Go golang.org/x/image/vp8/partition.go

namespace DotImage.Extended.Webp.Vp8;

// Each VP8 frame consists of between 2 and 9 bitstream partitions.
// Each partition is byte-aligned and is independently arithmetic-encoded.
//
// This file implements decoding a partition's bitstream, as specified in
// chapter 7. The implementation follows libwebp's approach instead of the
// specification's reference C implementation. For example, we use a look-up
// table instead of a for loop to recalibrate the encoded range.
internal sealed class Partition
{
    private static readonly byte[] LutShift =
    {
        7, 6, 6, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    };

    private static readonly byte[] LutRangeM1 =
    {
        127,
        127, 191,
        127, 159, 191, 223,
        127, 143, 159, 175, 191, 207, 223, 239,
        127, 135, 143, 151, 159, 167, 175, 183, 191, 199, 207, 215, 223, 231, 239, 247,
        127, 131, 135, 139, 143, 147, 151, 155, 159, 163, 167, 171, 175, 179, 183, 187,
        191, 195, 199, 203, 207, 211, 215, 219, 223, 227, 231, 235, 239, 243, 247, 251,
        127, 129, 131, 133, 135, 137, 139, 141, 143, 145, 147, 149, 151, 153, 155, 157,
        159, 161, 163, 165, 167, 169, 171, 173, 175, 177, 179, 181, 183, 185, 187, 189,
        191, 193, 195, 197, 199, 201, 203, 205, 207, 209, 211, 213, 215, 217, 219, 221,
        223, 225, 227, 229, 231, 233, 235, 237, 239, 241, 243, 245, 247, 249, 251, 253,
    };

    // UniformProb represents a 50% probability that the next bit is 0.
    internal const byte UniformProb = 128;

    // Buf is the input bytes.
    internal byte[] Buf = [];
    // R is how many of Buf's bytes have been consumed.
    internal int R;
    // RangeM1 is range minus 1, where range is in the arithmetic coding sense.
    internal uint RangeM1;
    // Bits and NBits hold those bits shifted out of Buf but not yet consumed.
    internal uint Bits;
    internal byte NBits;
    // UnexpectedEof tells whether we tried to read past Buf.
    internal bool UnexpectedEof;

    // Init initializes the partition.
    internal void Init(byte[] buf)
    {
        Buf = buf;
        R = 0;
        RangeM1 = 254;
        Bits = 0;
        NBits = 0;
        UnexpectedEof = false;
    }

    // ReadBit returns the next bit.
    internal bool ReadBit(byte prob)
    {
        if (NBits < 8)
        {
            if (R >= Buf.Length)
            {
                UnexpectedEof = true;
                return false;
            }
            // Expression split for 386 compiler.
            uint x = Buf[R];
            Bits |= x << (8 - NBits);
            R++;
            NBits += 8;
        }
        uint split = ((RangeM1 * prob) >> 8) + 1;
        bool bit = Bits >= split << 8;
        if (bit)
        {
            RangeM1 -= split;
            Bits -= split << 8;
        }
        else
        {
            RangeM1 = split - 1;
        }
        if (RangeM1 < 127)
        {
            byte shift = LutShift[(int)RangeM1];
            RangeM1 = LutRangeM1[(int)RangeM1];
            Bits <<= shift;
            NBits -= shift;
        }
        return bit;
    }

    // ReadUint returns the next n-bit unsigned integer.
    internal uint ReadUint(byte prob, int n)
    {
        uint u = 0;
        while (n > 0)
        {
            n--;
            if (ReadBit(prob))
            {
                u |= 1u << n;
            }
        }
        return u;
    }

    // ReadInt returns the next n-bit signed integer.
    internal int ReadInt(byte prob, int n)
    {
        uint u = ReadUint(prob, n);
        bool b = ReadBit(prob);
        if (b)
        {
            return -(int)u;
        }
        return (int)u;
    }

    // ReadOptionalInt returns the next n-bit signed integer in an encoding
    // where the likely result is zero.
    internal int ReadOptionalInt(byte prob, int n)
    {
        if (!ReadBit(prob))
        {
            return 0;
        }
        return ReadInt(prob, n);
    }
}