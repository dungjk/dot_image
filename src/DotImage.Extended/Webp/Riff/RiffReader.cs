using System;
using System.IO;

namespace DotImage.Extended.Webp.Riff;

/// <summary>A four character code.</summary>
public readonly struct FourCC : IEquatable<FourCC>
{
    public readonly byte A, B, C, D;

    public FourCC(byte a, byte b, byte c, byte d) { A = a; B = b; C = c; D = d; }
    public FourCC(char a, char b, char c, char d) { A = (byte)a; B = (byte)b; C = (byte)c; D = (byte)d; }

    public bool Equals(FourCC other) => A == other.A && B == other.B && C == other.C && D == other.D;
    public override bool Equals(object? obj) => obj is FourCC other && Equals(other);
    public override int GetHashCode() => A | (B << 8) | (C << 16) | (D << 24);
    public override string ToString() => $"{(char)A}{(char)B}{(char)C}{(char)D}";

    public static bool operator ==(FourCC left, FourCC right) => left.Equals(right);
    public static bool operator !=(FourCC left, FourCC right) => !left.Equals(right);

    public static readonly FourCC LIST = new('L', 'I', 'S', 'T');
}

/// <summary>Reads chunks from an underlying Stream.</summary>
public sealed class RiffReader
{
    private const int ChunkHeaderSize = 8;

    private readonly Stream _r;
    private uint _totalLen;
    private uint _chunkLen;
    private bool _padded;
    private ChunkReadStream? _chunkReader;
    private readonly byte[] _buf = new byte[ChunkHeaderSize];

    private RiffReader(Stream r) => _r = r;

    /// <summary>Returns the RIFF stream's form type (e.g. "WEBP") and its chunks as a <see cref="RiffReader"/>.</summary>
    public static (FourCC formType, RiffReader reader) NewReader(Stream r)
    {
        var buf = new byte[ChunkHeaderSize];
        if (ReadFull(r, buf) < ChunkHeaderSize)
            throw new RiffException("riff: missing RIFF chunk header");
        if (buf[0] != 'R' || buf[1] != 'I' || buf[2] != 'F' || buf[3] != 'F')
            throw new RiffException("riff: missing RIFF chunk header");
        return NewListReader(U32(buf, 4), r);
    }

    /// <summary>Returns a LIST chunk's list type and its chunks as a <see cref="RiffReader"/>.</summary>
    public static (FourCC listType, RiffReader reader) NewListReader(uint chunkLen, Stream chunkData)
    {
        if (chunkLen < 4)
            throw new RiffException("riff: short chunk data");
        var z = new RiffReader(chunkData);
        if (ReadFull(chunkData, z._buf, 0, 4) < 4)
            throw new RiffException("riff: short chunk data");
        z._totalLen = chunkLen - 4;
        var listType = new FourCC(z._buf[0], z._buf[1], z._buf[2], z._buf[3]);
        return (listType, z);
    }

    /// <summary>Returns the next chunk's ID, length and data stream. Returns default when there are no more chunks.</summary>
    public (FourCC chunkID, uint chunkLen, Stream chunkData) Next()
    {
        // Drain the rest of the previous chunk.
        if (_chunkLen != 0)
        {
            if (_chunkReader != null)
            {
                uint want = _chunkLen;
                long drained = Drain(_chunkReader);
                if ((uint)drained != want)
                    throw new RiffException("riff: short chunk data");
            }
            _chunkLen = 0;
        }
        _chunkReader = null;

        if (_padded)
        {
            if (_totalLen == 0)
                throw new RiffException("riff: list subchunk too long");
            _totalLen--;
            if (ReadFull(_r, _buf, 0, 1) < 1)
                throw new RiffException("riff: missing padding byte");
        }

        if (_totalLen == 0)
            return default;

        if (_totalLen < ChunkHeaderSize)
            throw new RiffException("riff: short chunk header");
        _totalLen -= ChunkHeaderSize;
        if (ReadFull(_r, _buf) < ChunkHeaderSize)
            throw new RiffException("riff: short chunk header");

        var chunkID = new FourCC(_buf[0], _buf[1], _buf[2], _buf[3]);
        _chunkLen = U32(_buf, 4);
        if (_chunkLen > _totalLen)
            throw new RiffException("riff: list subchunk too long");
        _padded = (_chunkLen & 1) == 1;
        _chunkReader = new ChunkReadStream(this);
        return (chunkID, _chunkLen, _chunkReader);
    }

    private long Drain(Stream reader)
    {
        long total = 0;
        var buf = new byte[4096];
        while (true)
        {
            int n = reader.Read(buf, 0, buf.Length);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    private sealed class ChunkReadStream : Stream
    {
        private readonly RiffReader _z;

        public ChunkReadStream(RiffReader z)
        {
            _z = z;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_z._chunkLen == 0) return 0;

            int n = (int)System.Math.Min(_z._chunkLen, (uint)count);
            n = _z._r.Read(buffer, offset, n);
            _z._totalLen -= (uint)n;
            _z._chunkLen -= (uint)n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
    }

    private static uint U32(byte[] b, int offset)
    {
        return (uint)b[offset] | (uint)b[offset + 1] << 8 | (uint)b[offset + 2] << 16 | (uint)b[offset + 3] << 24;
    }

    private static int ReadFull(Stream s, byte[] buf, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int n = s.Read(buf, offset + total, count - total);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    private static int ReadFull(Stream s, byte[] buf)
    {
        return ReadFull(s, buf, 0, buf.Length);
    }
}

public sealed class RiffException : Exception
{
    public RiffException(string message) : base(message) { }
}
