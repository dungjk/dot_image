// Ported from Go src/compress/lzw


namespace DotImage.Compress;

public enum LzwOrder
{
    Lsb,
    Msb,
}

public sealed class LzwReader : Stream
{
    private const int MaxWidth = 12;
    private const ushort DecoderInvalidCode = 0xffff;
    private const int FlushBuffer = 1 << MaxWidth;

    private IByteSource? _r;
    private uint _bits;
    private uint _nBits;
    private uint _width;
    private Func<LzwReader, (ushort code, Exception? err)>? _read;
    private int _litWidth;
    private Exception? _err;

    private ushort _clear;
    private ushort _eof;
    private ushort _hi;
    private ushort _overflow;
    private ushort _last = DecoderInvalidCode;

    private readonly byte[] _suffix = new byte[1 << MaxWidth];
    private readonly ushort[] _prefix = new ushort[1 << MaxWidth];
    private readonly byte[] _output = new byte[2 * (1 << MaxWidth)];
    private int _o;
    private byte[] _toRead = [];

    public LzwReader(Stream src, LzwOrder order, int litWidth)
    {
        Init(src, order, litWidth);
    }

    public void Init(Stream src, LzwOrder order, int litWidth)
    {
        _r = src as IByteSource ?? new StreamByteSource(src);
        _bits = 0;
        _nBits = 0;
        _err = null;
        _o = 0;
        _toRead = [];
        _last = DecoderInvalidCode;

        switch (order)
        {
            case LzwOrder.Lsb:
                _read = ReadLsb;
                break;
            case LzwOrder.Msb:
                _read = ReadMsb;
                break;
            default:
                _err = new InvalidOperationException("lzw: unknown order");
                return;
        }

        if (litWidth < 2 || litWidth > 8)
        {
            _err = new ArgumentOutOfRangeException(nameof(litWidth), litWidth, $"lzw: litWidth {litWidth} out of range");
            return;
        }

        _litWidth = litWidth;
        _width = (uint)(1 + litWidth);
        _clear = (ushort)(1 << litWidth);
        _eof = (ushort)(_clear + 1);
        _hi = _eof;
        _overflow = (ushort)(1 << (int)_width);
    }

    private (ushort code, Exception? err) ReadLsb(LzwReader r)
    {
        while (r._nBits < r._width)
        {
            int x = r._r!.ReadByte();
            if (x < 0)
                return (0, new EndOfStreamException());
            r._bits |= (uint)x << (int)r._nBits;
            r._nBits += 8;
        }

        ushort code = (ushort)(r._bits & ((1u << (int)r._width) - 1));
        r._bits >>= (int)r._width;
        r._nBits -= r._width;
        return (code, null);
    }

    private (ushort code, Exception? err) ReadMsb(LzwReader r)
    {
        while (r._nBits < r._width)
        {
            int x = r._r!.ReadByte();
            if (x < 0)
                return (0, new EndOfStreamException());
            r._bits |= (uint)x << (int)(24 - r._nBits);
            r._nBits += 8;
        }

        ushort code = (ushort)(r._bits >> (int)(32 - r._width));
        r._bits <<= (int)r._width;
        r._nBits -= r._width;
        return (code, null);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_err is EndOfStreamException && _toRead.Length == 0)
            return 0;

        int total = 0;
        while (count > 0)
        {
            if (_toRead.Length > 0)
            {
                int n = Math.Min(count, _toRead.Length);
                Buffer.BlockCopy(_toRead, 0, buffer, offset, n);
                if (n < _toRead.Length)
                    _toRead = _toRead[n..];
                else
                    _toRead = [];
                offset += n;
                count -= n;
                total += n;
                continue;
            }

            if (_err != null)
            {
                if (_err is EndOfStreamException)
                    return total;
                return total > 0 ? total : throw _err;
            }

            Decode();
        }

        return total;
    }

    public override int Read(Span<byte> buffer)
    {
        byte[] arr = buffer.Length <= 256 ? stackalloc byte[buffer.Length].ToArray() : new byte[buffer.Length];
        int n = Read(arr, 0, buffer.Length);
        arr.AsSpan(0, n).CopyTo(buffer);
        return n;
    }

    private void Decode()
    {
        while (true)
        {
            var (code, readErr) = _read!(this);
            if (readErr != null)
            {
                if (readErr is EndOfStreamException)
                    _err = new EndOfStreamException("Unexpected end of stream", readErr);
                else
                    _err = readErr;
                break;
            }

            switch (true)
            {
                case true when code < _clear:
                {
                    _output[_o++] = (byte)code;
                    if (_last != DecoderInvalidCode)
                    {
                        _suffix[_hi] = (byte)code;
                        _prefix[_hi] = _last;
                    }
                    break;
                }
                case true when code == _clear:
                    _width = (uint)(1 + _litWidth);
                    _hi = _eof;
                    _overflow = (ushort)(1 << (int)_width);
                    _last = DecoderInvalidCode;
                    continue;
                case true when code == _eof:
                    _err = new EndOfStreamException();
                    goto flush;
                case true when code <= _hi:
                {
                    ushort c = code;
                    int i = _output.Length - 1;
                    if (code == _hi && _last != DecoderInvalidCode)
                    {
                        c = _last;
                        while (c >= _clear)
                            c = _prefix[c];
                        _output[i] = (byte)c;
                        i--;
                        c = _last;
                    }

                    while (c >= _clear)
                    {
                        _output[i] = _suffix[c];
                        i--;
                        c = _prefix[c];
                    }

                    _output[i] = (byte)c;
                    int copyLen = _output.Length - i;
                    Array.Copy(_output, i, _output, _o, copyLen);
                    _o += copyLen;
                    if (_last != DecoderInvalidCode)
                    {
                        _suffix[_hi] = (byte)c;
                        _prefix[_hi] = _last;
                    }
                    break;
                }
                default:
                    _err = new InvalidOperationException("lzw: invalid code");
                    goto flush;
            }

            _last = code;
            _hi++;
            if (_hi >= _overflow)
            {
                if (_hi > _overflow)
                    throw new InvalidOperationException("unreachable");
                if (_width == MaxWidth)
                {
                    _last = DecoderInvalidCode;
                    _hi--;
                }
                else
                {
                    _width++;
                    _overflow = (ushort)(1 << (int)_width);
                }
            }

            if (_o >= FlushBuffer)
                goto flush;
        }

    flush:
        _toRead = _output[.._o];
        _o = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _err = new ObjectDisposedException(nameof(LzwReader));
        base.Dispose(disposing);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

public sealed class LzwWriter : Stream
{
    private const uint MaxCode = (1 << 12) - 1;
    private const uint InvalidCode = uint.MaxValue;
    private const int TableSize = 4 * (1 << 12);
    private const int TableMask = TableSize - 1;
    private const uint InvalidEntry = 0;

    private IByteSink? _w;
    private uint _litWidth;
    private LzwOrder _order;
    private Func<LzwWriter, uint, Exception?>? _write;
    private uint _nBits;
    private uint _width;
    private uint _bits;
    private uint _hi;
    private uint _overflow;
    private uint _savedCode = InvalidCode;
    private Exception? _err;
    private readonly uint[] _table = new uint[TableSize];

    private static readonly Exception OutOfCodes = new InvalidOperationException("lzw: out of codes");

    public LzwWriter(Stream dst, LzwOrder order, int litWidth)
    {
        Init(dst, order, litWidth);
    }

    public void Init(Stream dst, LzwOrder order, int litWidth)
    {
        switch (order)
        {
            case LzwOrder.Lsb:
                _write = WriteLsb;
                break;
            case LzwOrder.Msb:
                _write = WriteMsb;
                break;
            default:
                _err = new InvalidOperationException("lzw: unknown order");
                return;
        }

        if (litWidth < 2 || litWidth > 8)
        {
            _err = new ArgumentOutOfRangeException(nameof(litWidth), litWidth, $"lzw: litWidth {litWidth} out of range");
            return;
        }

        _w = dst as IByteSink ?? new StreamByteSink(dst);
        _order = order;
        _litWidth = (uint)litWidth;
        _width = 1 + _litWidth;
        _hi = (1u << (int)_litWidth) + 1;
        _overflow = 1u << (int)(1 + _litWidth);
        _savedCode = InvalidCode;
        _err = null;
        _nBits = 0;
        _bits = 0;
    }

    private Exception? WriteLsb(LzwWriter w, uint c)
    {
        w._bits |= c << (int)w._nBits;
        w._nBits += w._width;
        while (w._nBits >= 8)
        {
            try
            {
                w._w!.WriteByte((byte)w._bits);
            }
            catch (Exception ex)
            {
                return ex;
            }
            w._bits >>= 8;
            w._nBits -= 8;
        }
        return null;
    }

    private Exception? WriteMsb(LzwWriter w, uint c)
    {
        w._bits |= c << (int)(32 - w._width - w._nBits);
        w._nBits += w._width;
        while (w._nBits >= 8)
        {
            try
            {
                w._w!.WriteByte((byte)(w._bits >> 24));
            }
            catch (Exception ex)
            {
                return ex;
            }
            w._bits <<= 8;
            w._nBits -= 8;
        }
        return null;
    }

    private Exception? IncHi()
    {
        _hi++;
        if (_hi == _overflow)
        {
            _width++;
            _overflow <<= 1;
        }

        if (_hi == MaxCode)
        {
            uint clear = 1u << (int)_litWidth;
            var err = _write!(this, clear);
            if (err != null) return err;
            _width = _litWidth + 1;
            _hi = clear + 1;
            _overflow = clear << 1;
            Array.Fill(_table, InvalidEntry);
            return OutOfCodes;
        }

        return null;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_err != null) throw _err;
        if (count == 0) return;

        byte maxLit = (byte)((1 << (int)_litWidth) - 1);
        if (maxLit != 0xff)
        {
            for (int i = offset; i < offset + count; i++)
            {
                if (buffer[i] > maxLit)
                {
                    _err = new InvalidOperationException("lzw: input byte too large for the litWidth");
                    throw _err;
                }
            }
        }

        uint code = _savedCode;
        int start = offset;
        if (code == InvalidCode)
        {
            uint clear = 1u << (int)_litWidth;
            _err = _write!(this, clear);
            if (_err != null) throw _err;
            code = buffer[offset];
            start = offset + 1;
        }

        for (int idx = start; idx < offset + count; idx++)
        {
            uint literal = buffer[idx];
            uint key = (code << 8) | literal;
            int hash = (int)(((key >> 12) ^ key) & TableMask);
            while (true)
            {
                uint t = _table[hash];
                if (t != InvalidEntry)
                {
                    if (key == t >> 12)
                    {
                        code = t & MaxCode;
                        goto nextByte;
                    }
                    hash = (hash + 1) & TableMask;
                    continue;
                }
                break;
            }

            _err = _write!(this, code);
            if (_err != null) throw _err;
            code = literal;
            var err1 = IncHi();
            if (err1 != null)
            {
                if (ReferenceEquals(err1, OutOfCodes))
                    continue;
                _err = err1;
                throw _err;
            }
            _table[hash] = (key << 12) | _hi;

        nextByte: ;
        }

        _savedCode = code;
    }

    public override void Flush()
    {
        CloseStream();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            CloseStream();
        base.Dispose(disposing);
    }

    private void CloseStream()
    {
        if (_err is ObjectDisposedException)
            return;
        if (_err != null)
            return;

        if (_savedCode != InvalidCode)
        {
            var err = _write!(this, _savedCode);
            if (err != null)
            {
                _err = err;
                return;
            }
            var err1 = IncHi();
            if (err1 != null && !ReferenceEquals(err1, OutOfCodes))
            {
                _err = err1;
                return;
            }
        }
        else
        {
            uint clear = 1u << (int)_litWidth;
            var err = _write!(this, clear);
            if (err != null)
            {
                _err = err;
                return;
            }
        }

        uint eof = (1u << (int)_litWidth) + 1;
        var eofErr = _write!(this, eof);
        if (eofErr != null)
        {
            _err = eofErr;
            return;
        }

        if (_nBits > 0)
        {
            if (_order == LzwOrder.Msb)
                _bits >>= 24;
            try
            {
                _w!.WriteByte((byte)_bits);
            }
            catch (Exception ex)
            {
                _err = ex;
                return;
            }
        }

        try
        {
            _w!.Flush();
        }
        catch (Exception ex)
        {
            _err = ex;
            return;
        }

        _err = new ObjectDisposedException(nameof(LzwWriter));
        _w = null;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

internal interface IByteSource
{
    int ReadByte();
}

internal interface IByteSink
{
    void WriteByte(byte value);
    void Flush();
}

internal sealed class StreamByteSource(Stream s) : IByteSource
{
    public int ReadByte() => s.ReadByte();
}

internal sealed class StreamByteSink(Stream s) : IByteSink
{
    public void WriteByte(byte value) => s.WriteByte(value);
    public void Flush() => s.Flush();
}
