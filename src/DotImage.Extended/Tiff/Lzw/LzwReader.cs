// Ported from golang.org/x/image/tiff/lzw/reader.go.
//
// Implements LZW as used by the TIFF file format, including an "off by one"
// algorithmic difference when compared to standard LZW.
//
// Copyright 2011 The Go Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license.

namespace DotImage.Extended.Tiff.Lzw;

/// <summary>Specifies the bit ordering in an LZW data stream.</summary>
internal enum LzwOrder
{
    /// <summary>Least Significant Bits first, as used in the GIF file format.</summary>
    Lsb,
    /// <summary>Most Significant Bits first, as used in the TIFF and PDF file formats.</summary>
    Msb,
}

/// <summary>
/// An LZW decoder for the variant of LZW used by the TIFF file format.
/// </summary>
internal sealed class LzwDecoder
{
    private const int MaxWidth = 12;
    private const ushort DecoderInvalidCode = 0xffff;
    private const int FlushBuffer = 1 << MaxWidth;

    private readonly Func<ushort> _read;
    private Stream _r;

    private uint _bits;
    private uint _nBits;
    private uint _width;

    private readonly int _litWidth;
    private Exception? _err;
    private bool _eofReached;

    // The first 1<<litWidth codes are literal codes.
    // The next two codes mean clear and EOF.
    // Other valid codes are in the range [lo, hi] where lo := clear + 2,
    // with the upper bound incrementing on each code seen.
    // overflow is the code at which hi overflows the code width. NOTE: TIFF's LZW is "off by one".
    // last is the most recently seen code, or decoderInvalidCode.
    private ushort _clear, _eof, _hi, _overflow, _last;

    // Each code c in [lo, hi] expands to two or more bytes. For c != hi:
    //   suffix[c] is the last of these bytes.
    //   prefix[c] is the code for all but the last byte.
    //   This code can either be a literal code or another code in [lo, c).
    // The c == hi case is a special case.
    private readonly byte[] _suffix = new byte[1 << MaxWidth];
    private readonly ushort[] _prefix = new ushort[1 << MaxWidth];

    private readonly byte[] _output = new byte[2 * (1 << MaxWidth)];
    private int _o;             // write index into _output
    private ReadOnlyMemory<byte> _toRead; // bytes to return from Read()

    private LzwDecoder(Stream r, int litWidth, LzwOrder order)
    {
        _r = r;
        _litWidth = litWidth;
        switch (order)
        {
            case LzwOrder.Lsb:
                _read = ReadLsb;
                break;
            case LzwOrder.Msb:
                _read = ReadMsb;
                break;
            default:
                throw new InvalidOperationException("lzw: unknown order");
        }
        if (litWidth < 2 || 8 < litWidth)
        {
            throw new InvalidDataException($"lzw: litWidth {litWidth} out of range");
        }
        _width = 1u + (uint)litWidth;
        _clear = (ushort)(1 << litWidth);
        _eof = (ushort)(_clear + 1);
        _hi = _eof;
        _overflow = (ushort)(1u << (int)_width);
        _last = DecoderInvalidCode;
    }

    /// <summary>Creates a new LZW decoder that reads compressed data from r.</summary>
    public static LzwDecoder NewReader(Stream r, LzwOrder order, int litWidth) =>
        new(r, litWidth, order);

    // ReadLsb returns the next code for "Least Significant Bits first" data.
    private ushort ReadLsb()
    {
        while (_nBits < _width)
        {
            int x = _r.ReadByte();
            if (x < 0)
            {
                throw new EndOfStreamException();
            }
            _bits |= (uint)x << (int)_nBits;
            _nBits += 8;
        }
        ushort code = (ushort)(_bits & ((1u << (int)_width) - 1));
        _bits >>= (int)_width;
        _nBits -= _width;
        return code;
    }

    // ReadMsb returns the next code for "Most Significant Bits first" data.
    private ushort ReadMsb()
    {
        while (_nBits < _width)
        {
            int x = _r.ReadByte();
            if (x < 0)
            {
                throw new EndOfStreamException();
            }
            _bits |= (uint)x << (24 - (int)_nBits);
            _nBits += 8;
        }
        ushort code = (ushort)(_bits >> (32 - (int)_width));
        _bits <<= (int)_width;
        _nBits -= _width;
        return code;
    }

    /// <summary>Reads decompressed data into b. Returns the number of bytes read.</summary>
    public int Read(Span<byte> b)
    {
        while (true)
        {
            if (_toRead.Length > 0)
            {
                int n = System.Math.Min(b.Length, _toRead.Length);
                _toRead.Span.Slice(0, n).CopyTo(b);
                _toRead = _toRead.Slice(n);
                return n;
            }
            if (_eofReached)
            {
                // Normal end of the LZW stream.
                return 0;
            }
            if (_err != null)
            {
                throw _err;
            }
            Decode();
        }
    }

    /// <summary>
    /// Decode decompresses bytes from r and leaves them in _toRead.
    /// </summary>
    private void Decode()
    {
        // Loop over the code stream, converting codes into decompressed bytes.
        while (true)
        {
            ushort code;
            try
            {
                code = _read();
            }
            catch (EndOfStreamException)
            {
                _err = new EndOfStreamException("lzw: unexpected EOF");
                break;
            }

            if (code < _clear)
            {
                // We have a literal code.
                _output[_o] = (byte)code;
                _o++;
                if (_last != DecoderInvalidCode)
                {
                    // Save what the hi code expands to.
                    _suffix[_hi] = (byte)code;
                    _prefix[_hi] = _last;
                }
            }
            else if (code == _clear)
            {
                _width = 1u + (uint)_litWidth;
                _hi = _eof;
                _overflow = (ushort)(1u << (int)_width);
                _last = DecoderInvalidCode;
                continue;
            }
            else if (code == _eof)
            {
                _eofReached = true;
                break;
            }
            else if (code <= _hi)
            {
                int c = code;
                int i = _output.Length - 1;
                if (code == _hi && _last != DecoderInvalidCode)
                {
                    // code == hi is a special case which expands to the last expansion
                    // followed by the head of the last expansion. To find the head, we walk
                    // the prefix chain until we find a literal code.
                    c = _last;
                    while (c >= _clear)
                    {
                        c = _prefix[c];
                    }
                    _output[i] = (byte)c;
                    i--;
                    c = _last;
                }
                // Copy the suffix chain into output and then write that to w.
                while (c >= _clear)
                {
                    _output[i] = _suffix[c];
                    i--;
                    c = _prefix[c];
                }
                _output[i] = (byte)c;
                // Copy the suffix chain into output. The source run of bytes
                // never overlaps the destination (i > _o), so a plain forward
                // copy is safe and mirrors the Go algorithm's semantics.
                int n = _output.Length - i;
                for (int k = 0; k < n; k++)
                {
                    _output[_o + k] = _output[i + k];
                }
                _o += n;
                if (_last != DecoderInvalidCode)
                {
                    // Save what the hi code expands to.
                    _suffix[_hi] = (byte)c;
                    _prefix[_hi] = _last;
                }
            }
            else
            {
                _err = new InvalidDataException("lzw: invalid code");
                break;
            }

            _last = code;
            _hi = (ushort)(_hi + 1);
            if (_hi + 1 >= _overflow)
            {
                // NOTE: the "+1" is where TIFF's LZW differs from the standard algorithm.
                if (_width == MaxWidth)
                {
                    _last = DecoderInvalidCode;
                }
                else
                {
                    _width++;
                    _overflow = (ushort)(_overflow << 1);
                }
            }
            if (_o >= FlushBuffer)
            {
                break;
            }
        }

        // Flush pending output.
        _toRead = _output.AsMemory(0, _o);
        _o = 0;
    }

    /// <summary>Closes the decoder, discarding any leftover state.</summary>
    public void Close()
    {
        _err = new IOException("lzw: reader/writer is closed");
    }
}