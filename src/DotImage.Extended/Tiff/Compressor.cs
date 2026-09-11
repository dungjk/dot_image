// Ported from golang.org/x/image/tiff/compress.go.
// Copyright 2011 The Go Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license.

namespace DotImage.Extended.Tiff;

/// <summary>
/// A read-only Stream over a section (offset, length) of an <see cref="ITiffReaderAt"/>.
/// Mirrors Go's io.SectionReader.
/// </summary>
internal sealed class TiffSectionStream : Stream
{
    private readonly ITiffReaderAt _r;
    private long _off;
    private long _n;

    public TiffSectionStream(ITiffReaderAt r, long off, long n)
    {
        _r = r;
        _off = off;
        _n = n;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _n;
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(Span<byte> buffer)
    {
        int count = (int)System.Math.Min(buffer.Length, _n);
        if (count <= 0)
        {
            return 0;
        }
        _r.ReadAt(buffer.Slice(0, count), _off);
        _off += count;
        _n -= count;
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// Helpers for decompressing TIFF block data.
/// </summary>
internal static class TiffCompressor
{
    /// <summary>
    /// Decodes the PackBits-compressed data in the given stream and returns the
    /// uncompressed data. The output size is limited to lim bytes to prevent
    /// decompression bombs.
    ///
    /// The PackBits compression format is described in section 9 (p. 42)
    /// of the TIFF spec.
    /// </summary>
    public static byte[] UnpackBits(Stream r, long lim)
    {
        byte[] buf = new byte[128];
        List<byte> dstList = new(1024);
        bool done = false;
        while (!done)
        {
            int b = r.ReadByte();
            if (b < 0)
            {
                // Reached the end of the input image.
                break;
            }
            sbyte codeB = unchecked((sbyte)(byte)b);
            int code = codeB;
            if (code >= 0)
            {
                int n = code + 1;
                int total = 0;
                while (total < n)
                {
                    int read = r.Read(buf, total, n - total);
                    if (read <= 0)
                    {
                        throw new EndOfStreamException();
                    }
                    total += read;
                }
                for (int i = 0; i < n; i++)
                {
                    dstList.Add(buf[i]);
                }
            }
            else if (code == -128)
            {
                // No-op.
            }
            else
            {
                b = r.ReadByte();
                if (b < 0)
                {
                    throw new EndOfStreamException();
                }
                int count = 1 - code;
                for (int j = 0; j < count; j++)
                {
                    dstList.Add((byte)b);
                }
            }
            if (dstList.Count > lim)
            {
                throw new InvalidDataException("tiff: invalid format: PackBits: decompressed data too large");
            }
        }
        return dstList.ToArray();
    }

    /// <summary>
    /// Reads up to lim bytes from r into a freshly-sized byte array.
    /// Mirrors Go's readBuf helper which reads a decompressor's output.
    /// </summary>
    public static byte[] ReadBuf(Stream r, byte[]? buf, long lim)
    {
        int capacity = buf?.Length ?? 0;
        if (capacity < lim)
        {
            capacity = (int)System.Math.Min(lim, System.Math.Max(capacity * 2, 1024));
        }
        byte[] dst = new byte[capacity];
        long total = 0;
        while (total < lim)
        {
            int chunk = (int)System.Math.Min(lim - total, dst.Length - (int)total);
            if (chunk <= 0)
            {
                Array.Resize(ref dst, (int)System.Math.Min(lim, dst.Length * 2));
                continue;
            }
            int n = r.Read(dst, (int)total, chunk);
            if (n <= 0)
            {
                break;
            }
            total += n;
        }
        if (total != dst.Length)
        {
            Array.Resize(ref dst, (int)total);
        }
        return dst;
    }
}