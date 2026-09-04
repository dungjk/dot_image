// Ported from Go src/image/format.go

using System.Buffers;

namespace DotImage;

public sealed class ImageFormatException : Exception
{
    public ImageFormatException() : base("image: unknown format") { }
}

public sealed class ReaderPeeker(Stream stream, int bufferSize = 4096) : Stream
{
    private readonly Stream _stream = stream;
    private readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
    private int _start;
    private int _end;

    public byte[] Peek(int n)
    {
        while (_end - _start < n)
        {
            int read = _stream.Read(_buffer, _end, _buffer.Length - _end);
            if (read == 0) break;
            _end += read;
        }
        int available = _end - _start;
        if (available < n)
            throw new EndOfStreamException();
        var result = new byte[n];
        Buffer.BlockCopy(_buffer, _start, result, 0, n);
        return result;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int fromBuffer = Math.Min(count, _end - _start);
        if (fromBuffer > 0)
        {
            Buffer.BlockCopy(_buffer, _start, buffer, offset, fromBuffer);
            _start += fromBuffer;
            offset += fromBuffer;
            count -= fromBuffer;
        }
        if (count == 0) return fromBuffer;
        return fromBuffer + _stream.Read(buffer, offset, count);
    }

    public override void Flush() => _stream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
    public override void SetLength(long value) => _stream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => _stream.CanWrite;
    public override long Length => _stream.Length;
    public override long Position
    {
        get => _stream.Position - (_end - _start);
        set => throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _stream.Dispose();
        }
        base.Dispose(disposing);
    }
}

public static class FormatRegistry
{
    private sealed class FormatEntry
    {
        public required string Name { get; init; }
        public required string Magic { get; init; }
        public required Func<Stream, IImage> Decode { get; init; }
        public required Func<Stream, Config> DecodeConfig { get; init; }
    }

    private static readonly object Lock = new();
    private static List<FormatEntry> _formats = [];
    private static int _defaultFormatsRegistered;

    internal static void EnsureDefaultFormatsRegistered()
    {
        if (Interlocked.CompareExchange(ref _defaultFormatsRegistered, 1, 0) != 0)
            return;

        Png.FormatRegistration.Register();
        Gif.FormatRegistration.Register();
        Jpeg.FormatRegistration.Register();
    }

    public static void RegisterFormat(
        string name,
        string magic,
        Func<Stream, IImage> decode,
        Func<Stream, Config> decodeConfig)
    {
        lock (Lock)
        {
            _formats = [.._formats, new FormatEntry
            {
                Name = name,
                Magic = magic,
                Decode = decode,
                DecodeConfig = decodeConfig,
            }];
        }
    }

    private static Stream AsReader(Stream r) => r is ReaderPeeker ? r : new ReaderPeeker(r);

    private static bool Match(string magic, byte[] b)
    {
        if (magic.Length != b.Length) return false;
        for (int i = 0; i < b.Length; i++)
        {
            if (magic[i] != b[i] && magic[i] != '?') return false;
        }
        return true;
    }

    private static FormatEntry? Sniff(ReaderPeeker r)
    {
        List<FormatEntry> formats;
        lock (Lock) formats = _formats;
        foreach (var f in formats)
        {
            try
            {
                var b = r.Peek(f.Magic.Length);
                if (Match(f.Magic, b)) return f;
            }
            catch (EndOfStreamException)
            {
                // not enough data
            }
        }
        return null;
    }

    public static (IImage Image, string FormatName) Decode(Stream r)
    {
        EnsureDefaultFormatsRegistered();
        var rr = AsReader(r) as ReaderPeeker ?? new ReaderPeeker(r);
        var f = Sniff(rr);
        if (f == null) throw new ImageFormatException();
        return (f.Decode(rr), f.Name);
    }

    public static (Config Config, string FormatName) DecodeConfig(Stream r)
    {
        EnsureDefaultFormatsRegistered();
        var rr = AsReader(r) as ReaderPeeker ?? new ReaderPeeker(r);
        var f = Sniff(rr);
        if (f == null) throw new ImageFormatException();
        return (f.DecodeConfig(rr), f.Name);
    }
}
