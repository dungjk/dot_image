using System;
using System.IO;

namespace DotImage.Extended.Webp.Vp8l;

internal sealed class BitReader
{
    internal readonly Stream _r;
    internal uint _bits;
    internal uint _nBits;

    public BitReader(Stream r) => _r = r;

    public uint Read(uint n)
    {
        while (_nBits < n)
        {
            int c = _r.ReadByte();
            if (c < 0)
                throw new EndOfStreamException();
            _bits |= (uint)c << (int)_nBits;
            _nBits += 8;
        }
        uint u = _bits & ((1u << (int)n) - 1);
        _bits >>= (int)n;
        _nBits -= n;
        return u;
    }
}
