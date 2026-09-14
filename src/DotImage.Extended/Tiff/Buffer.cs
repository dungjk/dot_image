// Ported from golang.org/x/image/tiff/buffer.go.
// Copyright 2011 The Go Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license.

namespace DotImage.Extended.Tiff;

/// <summary>
/// An abstraction over random-access reading, mirroring Go's io.ReaderAt.
/// </summary>
internal interface ITiffReaderAt
{
    /// <summary>
    /// Reads p.Length bytes into p starting at offset off. Throws
    /// <see cref="EndOfStreamException"/> if not all bytes are available.
    /// </summary>
    void ReadAt(Span<byte> p, long off);

    /// <summary>True if <see cref="Slice"/> is supported (i.e. the input is buffered).</summary>
    bool CanSlice { get; }

    /// <summary>Returns n bytes starting at offset off.</summary>
    byte[] Slice(long off, long n);
}

internal static class TiffReaderAtFactory
{
    /// <summary>Converts a Stream into an <see cref="ITiffReaderAt"/>.</summary>
    public static ITiffReaderAt NewReaderAt(Stream r)
    {
        if (r.CanSeek)
        {
            return new SeekableReaderAt(r);
        }
        return new TiffBuffer(r);
    }
}

/// <summary>
/// Buffers a Stream to satisfy random-access reads.
/// </summary>
internal sealed class TiffBuffer : ITiffReaderAt
{
    private const int FillChunkSize = 10 << 20; // 10 MB

    private readonly Stream _r;
    private byte[] _buf = Array.Empty<byte>();

    public TiffBuffer(Stream r)
    {
        _r = r;
    }

    public bool CanSlice => true;

    /// <summary>Reads data from _r until the buffer contains at least end bytes.</summary>
    private void Fill(int end)
    {
        int m = _buf.Length;
        while (m < end)
        {
            int next = System.Math.Min(end - m, FillChunkSize);
            Array.Resize(ref _buf, m + next);
            int total = 0;
            while (total < next)
            {
                int n = _r.Read(_buf, m + total, next - total);
                if (n <= 0)
                {
                    throw new EndOfStreamException();
                }
                total += n;
            }
            m += total;
        }
    }

    public void ReadAt(Span<byte> p, long off)
    {
        if (off < 0)
        {
            // Impossible in correct usage, but check for safety.
            throw new ArgumentOutOfRangeException(nameof(off), "invalid ReadAt offset (bug)");
        }
        if (p.Length == 0)
        {
            return;
        }
        long end64 = off + p.Length;
        if (end64 < off || end64 > int.MaxValue)
        {
            throw new EndOfStreamException();
        }
        int end = (int)end64;

        Fill(end);
        end = System.Math.Min(end, _buf.Length);
        int start = (int)System.Math.Min(off, end);
        if (end - start < p.Length)
        {
            throw new EndOfStreamException();
        }
        _buf.AsSpan(start, p.Length).CopyTo(p);
    }

    /// <summary>Returns a slice of the underlying buffer. The slice contains n bytes starting at offset off.</summary>
    public byte[] Slice(long off, long n)
    {
        if (off < 0 || n < 0)
        {
            // Impossible in correct usage, but check for safety.
            throw new ArgumentOutOfRangeException(nameof(off), $"invalid negative input to Slice({off}, {n}) (bug)");
        }
        long end = off + n;
        if (end < 0 || end > int.MaxValue)
        {
            // end is too large. Treat this as a read error.
            throw new EndOfStreamException();
        }
        Fill((int)end);
        end = System.Math.Min(end, _buf.Length);
        if (end - off < n)
        {
            throw new EndOfStreamException();
        }
        byte[] result = new byte[n];
        Array.Copy(_buf, (int)off, result, 0, result.Length);
        return result;
    }
}

/// <summary>
/// Direct random-access reader over a seekable Stream.
/// </summary>
internal sealed class SeekableReaderAt : ITiffReaderAt
{
    private readonly Stream _r;

    public SeekableReaderAt(Stream r)
    {
        _r = r;
    }

    public bool CanSlice => false;

    public void ReadAt(Span<byte> p, long off)
    {
        if (off < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(off), "invalid ReadAt offset (bug)");
        }
        if (p.Length == 0)
        {
            return;
        }
        if (off + p.Length < off || off + p.Length > _r.Length)
        {
            throw new EndOfStreamException();
        }
        _r.Seek(off, SeekOrigin.Begin);
        int total = 0;
        while (total < p.Length)
        {
            int n = _r.Read(p[total..]);
            if (n <= 0)
            {
                throw new EndOfStreamException();
            }
            total += n;
        }
    }

    public byte[] Slice(long off, long n) => throw new NotSupportedException();
}