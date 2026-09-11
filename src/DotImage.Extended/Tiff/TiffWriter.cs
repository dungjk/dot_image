// Ported from golang.org/x/image/tiff/writer.go.
// Copyright 2012 The Go Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license.

using System.Buffers.Binary;
using System.IO.Compression;
using DotImage;
using DotImage.Color;

namespace DotImage.Extended.Tiff;

/// <summary>
/// The TIFF format allows to choose the order of the different elements freely.
/// The basic structure of a TIFF file written by this package is:
///   1. Header (8 bytes).
///   2. Image data.
///   3. Image File Directory (IFD).
///   4. "Pointer area" for larger entries in the IFD.
/// We only write little-endian TIFF files.
/// </summary>
public static class TiffWriter
{
    // An IfdEntry is a single entry in an Image File Directory.
    // A value of type Rational is composed of two 32-bit values,
    // thus data contains two uints (numerator and denominator) for a single number.
    private struct IfdEntry
    {
        public int Tag;
        public int Datatype;
        public uint[] Data;

        public void PutData(Span<byte> p)
        {
            foreach (uint d in Data)
            {
                switch (Datatype)
                {
                    case TiffDataTypes.Byte:
                    case TiffDataTypes.Ascii:
                        p[0] = (byte)d;
                        p = p[1..];
                        break;
                    case TiffDataTypes.Short:
                        BinaryPrimitives.WriteUInt16LittleEndian(p, (ushort)d);
                        p = p[2..];
                        break;
                    case TiffDataTypes.Long:
                    case TiffDataTypes.Rational:
                        BinaryPrimitives.WriteUInt32LittleEndian(p, d);
                        p = p[4..];
                        break;
                }
            }
        }
    }

    private static void EncodeGray(Stream w, Memory<byte> pix, int dx, int dy, int stride, bool predictor)
    {
        if (!predictor)
        {
            WritePix(w, pix.Span, dy, dx, stride);
            return;
        }
        byte[] buf = new byte[dx];
        for (int y = 0; y < dy; y++)
        {
            int min = y * stride;
            int max = y * stride + dx;
            int off = 0;
            byte v0 = 0;
            for (int i = min; i < max; i++)
            {
                byte v1 = pix.Span[i];
                buf[off] = (byte)(v1 - v0);
                v0 = v1;
                off++;
            }
            w.Write(buf, 0, buf.Length);
        }
    }

    private static void EncodeGray16(Stream w, Memory<byte> pix, int dx, int dy, int stride, bool predictor)
    {
        byte[] buf = new byte[dx * 2];
        for (int y = 0; y < dy; y++)
        {
            int min = y * stride;
            int max = y * stride + dx * 2;
            int off = 0;
            ushort v0 = 0;
            for (int i = min; i < max; i += 2)
            {
                // An image.Gray16's Pix is in big-endian order.
                ushort v1 = (ushort)(((uint)pix.Span[i] << 8) | pix.Span[i + 1]);
                if (predictor)
                {
                    ushort tmp = v1;
                    v1 = (ushort)(v1 - v0);
                    v0 = tmp;
                }
                // We only write little-endian TIFF files.
                buf[off + 0] = (byte)v1;
                buf[off + 1] = (byte)(v1 >> 8);
                off += 2;
            }
            w.Write(buf, 0, buf.Length);
        }
    }

    private static void EncodeRgba(Stream w, Memory<byte> pix, int dx, int dy, int stride, bool predictor)
    {
        if (!predictor)
        {
            WritePix(w, pix.Span, dy, dx * 4, stride);
            return;
        }
        byte[] buf = new byte[dx * 4];
        for (int y = 0; y < dy; y++)
        {
            int min = y * stride;
            int max = y * stride + dx * 4;
            int off = 0;
            byte r0 = 0, g0 = 0, b0 = 0, a0 = 0;
            for (int i = min; i < max; i += 4)
            {
                byte r1 = pix.Span[i + 0];
                byte g1 = pix.Span[i + 1];
                byte b1 = pix.Span[i + 2];
                byte a1 = pix.Span[i + 3];
                buf[off + 0] = (byte)(r1 - r0);
                buf[off + 1] = (byte)(g1 - g0);
                buf[off + 2] = (byte)(b1 - b0);
                buf[off + 3] = (byte)(a1 - a0);
                off += 4;
                r0 = r1;
                g0 = g1;
                b0 = b1;
                a0 = a1;
            }
            w.Write(buf, 0, buf.Length);
        }
    }

    private static void EncodeRgba64(Stream w, Memory<byte> pix, int dx, int dy, int stride, bool predictor)
    {
        byte[] buf = new byte[dx * 8];
        for (int y = 0; y < dy; y++)
        {
            int min = y * stride;
            int max = y * stride + dx * 8;
            int off = 0;
            ushort r0 = 0, g0 = 0, b0 = 0, a0 = 0;
            for (int i = min; i < max; i += 8)
            {
                // An image.RGBA64's Pix is in big-endian order.
                ushort r1 = (ushort)(((uint)pix.Span[i + 0] << 8) | pix.Span[i + 1]);
                ushort g1 = (ushort)(((uint)pix.Span[i + 2] << 8) | pix.Span[i + 3]);
                ushort b1 = (ushort)(((uint)pix.Span[i + 4] << 8) | pix.Span[i + 5]);
                ushort a1 = (ushort)(((uint)pix.Span[i + 6] << 8) | pix.Span[i + 7]);
                if (predictor)
                {
                    ushort tmp;
                    tmp = r1; r1 = (ushort)(r1 - r0); r0 = tmp;
                    tmp = g1; g1 = (ushort)(g1 - g0); g0 = tmp;
                    tmp = b1; b1 = (ushort)(b1 - b0); b0 = tmp;
                    tmp = a1; a1 = (ushort)(a1 - a0); a0 = tmp;
                }
                // We only write little-endian TIFF files.
                buf[off + 0] = (byte)r1;
                buf[off + 1] = (byte)(r1 >> 8);
                buf[off + 2] = (byte)g1;
                buf[off + 3] = (byte)(g1 >> 8);
                buf[off + 4] = (byte)b1;
                buf[off + 5] = (byte)(b1 >> 8);
                buf[off + 6] = (byte)a1;
                buf[off + 7] = (byte)(a1 >> 8);
                off += 8;
            }
            w.Write(buf, 0, buf.Length);
        }
    }

    private static void Encode(Stream w, IImage m, bool predictor)
    {
        Rect bounds = m.Bounds();
        byte[] buf = new byte[4 * bounds.Dx()];
        for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
        {
            int off = 0;
            if (predictor)
            {
                byte r0 = 0, g0 = 0, b0 = 0, a0 = 0;
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                {
                    (uint r, uint g, uint b, uint a) = m.At(x, y).Rgba();
                    byte r1 = (byte)(r >> 8);
                    byte g1 = (byte)(g >> 8);
                    byte b1 = (byte)(b >> 8);
                    byte a1 = (byte)(a >> 8);
                    buf[off + 0] = (byte)(r1 - r0);
                    buf[off + 1] = (byte)(g1 - g0);
                    buf[off + 2] = (byte)(b1 - b0);
                    buf[off + 3] = (byte)(a1 - a0);
                    off += 4;
                    r0 = r1;
                    g0 = g1;
                    b0 = b1;
                    a0 = a1;
                }
            }
            else
            {
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                {
                    (uint r, uint g, uint b, uint a) = m.At(x, y).Rgba();
                    buf[off + 0] = (byte)(r >> 8);
                    buf[off + 1] = (byte)(g >> 8);
                    buf[off + 2] = (byte)(b >> 8);
                    buf[off + 3] = (byte)(a >> 8);
                    off += 4;
                }
            }
            w.Write(buf, 0, buf.Length);
        }
    }

    // WritePix writes the internal byte array of an image to w. It is less
    // general but much faster then Encode. WritePix is used when pix directly
    // corresponds to one of the TIFF image types.
    private static void WritePix(Stream w, Span<byte> pix, int nrows, int length, int stride)
    {
        if (length == stride)
        {
            w.Write(pix.Slice(0, nrows * length));
            return;
        }
        for (; nrows > 0; nrows--)
        {
            w.Write(pix.Slice(0, length));
            pix = pix.Slice(stride);
        }
    }

    private static void WriteIfd(Stream w, int ifdOffset, List<IfdEntry> d)
    {
        byte[] buf = new byte[TiffConsts.IfdLen];
        // Make space for "pointer area" containing IFD entry data
        // longer than 4 bytes.
        byte[] parea = new byte[1024];
        int pstart = ifdOffset + TiffConsts.IfdLen * d.Count + 6;
        int o = 0; // Current offset in parea.

        // The IFD has to be written with the tags in ascending order.
        d.Sort((a, b) => a.Tag.CompareTo(b.Tag));

        // Write the number of entries in this IFD.
        Span<byte> countBuf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(countBuf, (ushort)d.Count);
        w.Write(countBuf);

        foreach (IfdEntry ent in d)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), (ushort)ent.Tag);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), (ushort)ent.Datatype);
            uint count = (uint)ent.Data.Length;
            if (ent.Datatype == TiffDataTypes.Rational)
            {
                count /= 2;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4, 4), count);
            int datalen = (int)(count * TiffDataTypes.Lengths[ent.Datatype]);
            if (datalen <= 4)
            {
                ent.PutData(buf.AsSpan(8, 4));
            }
            else
            {
                if (o + datalen > parea.Length)
                {
                    int newlen = parea.Length + 1024;
                    while (o + datalen > newlen)
                    {
                        newlen += 1024;
                    }
                    byte[] newarea = new byte[newlen];
                    Array.Copy(parea, newarea, parea.Length);
                    parea = newarea;
                }
                ent.PutData(parea.AsSpan(o, datalen));
                BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(8, 4), (uint)(pstart + o));
                o += datalen;
            }
            w.Write(buf, 0, buf.Length);
        }
        // The IFD ends with the offset of the next IFD in the file,
        // or zero if it is the last one (page 14).
        Span<byte> zeroBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(zeroBuf, 0);
        w.Write(zeroBuf);
        w.Write(parea, 0, o);
    }

    /// <summary>Options are the encoding parameters.</summary>
    public class Options
    {
        /// <summary>Compression is the type of compression used.</summary>
        public CompressionType Compression = CompressionType.Uncompressed;

        /// <summary>
        /// Predictor determines whether a differencing predictor is used;
        /// if true, instead of each pixel's color, the color difference to the
        /// preceding one is saved. This improves the compression for certain
        /// types of images and compressors. For example, it works well for
        /// photos with Deflate compression.
        /// </summary>
        public bool Predictor;
    }

    /// <summary>Writes the image m to w. opt determines the options used for encoding, such as the compression type. If opt is null, an uncompressed image is written.</summary>
    public static void Encode(Stream w, IImage m, Options? opt)
    {
        Point d = m.Bounds().Size();

        if (d.X == 0 || d.Y == 0)
        {
            throw new InvalidOperationException("tiff: zero-size image");
        }

        uint compression = (uint)TiffCompressionTypes.None;
        bool predictor = false;
        if (opt != null)
        {
            compression = opt.Compression.SpecValue();
            // The predictor field is only used with LZW. See page 64 of the spec.
            predictor = opt.Predictor && compression == (uint)TiffCompressionTypes.Lzw;
        }

        w.Write(stackalloc byte[] { (byte)'I', (byte)'I', 0x2A, 0x00 });

        // Compressed data is written into a buffer first, so that we
        // know the compressed size.
        var buf = new MemoryStream();
        // dst holds the destination for the pixel data of the image --
        // either w or a writer to buf.
        Stream dst;
        // imageLen is the length of the pixel data in bytes.
        // The offset of the IFD is imageLen + 8 header bytes.
        int imageLen;

        switch (compression)
        {
            case (uint)TiffCompressionTypes.None:
                dst = w;
                // Write IFD offset before outputting pixel data.
                imageLen = m switch
                {
                    PalettedImage => d.X * d.Y * 1,
                    GrayImage => d.X * d.Y * 1,
                    Gray16Image => d.X * d.Y * 2,
                    Rgba64Image => d.X * d.Y * 8,
                    Nrgba64Image => d.X * d.Y * 8,
                    _ => d.X * d.Y * 4,
                };
                WriteUint32LittleEndian(w, (uint)(imageLen + 8));
                break;
            case (uint)TiffCompressionTypes.Deflate:
                dst = new ZLibStream(buf, CompressionMode.Compress, leaveOpen: true);
                imageLen = 0;
                break;
            default:
                throw new InvalidOperationException("tiff: unsupported compression");
        }

        uint pr = (uint)TiffPredictors.None;
        uint photometricInterpretation = (uint)TiffPhotometricInterpretations.Rgb;
        uint samplesPerPixel = 4;
        uint[] bitsPerSample = { 8, 8, 8, 8 };
        uint extraSamples = 0;
        uint[] colorMap = Array.Empty<uint>();

        if (predictor)
        {
            pr = (uint)TiffPredictors.Horizontal;
        }
        switch (m)
        {
            case PalettedImage paletted:
                photometricInterpretation = (uint)TiffPhotometricInterpretations.Paletted;
                samplesPerPixel = 1;
                bitsPerSample = new uint[] { 8 };
                colorMap = new uint[256 * 3];
                for (int i = 0; i < 256 && i < paletted.Palette.Length; i++)
                {
                    (uint r, uint g, uint b, _) = paletted.Palette[i].Rgba();
                    colorMap[i + 0 * 256] = r;
                    colorMap[i + 1 * 256] = g;
                    colorMap[i + 2 * 256] = b;
                }
                EncodeGray(dst, paletted.Pix, d.X, d.Y, paletted.Stride, predictor);
                break;
            case GrayImage gray:
                photometricInterpretation = (uint)TiffPhotometricInterpretations.BlackIsZero;
                samplesPerPixel = 1;
                bitsPerSample = new uint[] { 8 };
                EncodeGray(dst, gray.Pix, d.X, d.Y, gray.Stride, predictor);
                break;
            case Gray16Image gray16:
                photometricInterpretation = (uint)TiffPhotometricInterpretations.BlackIsZero;
                samplesPerPixel = 1;
                bitsPerSample = new uint[] { 16 };
                EncodeGray16(dst, gray16.Pix, d.X, d.Y, gray16.Stride, predictor);
                break;
            case NrgbaImage nrgba:
                extraSamples = 2; // Unassociated alpha.
                EncodeRgba(dst, nrgba.Pix, d.X, d.Y, nrgba.Stride, predictor);
                break;
            case Nrgba64Image nrgba64:
                extraSamples = 2; // Unassociated alpha.
                bitsPerSample = new uint[] { 16, 16, 16, 16 };
                EncodeRgba64(dst, nrgba64.Pix, d.X, d.Y, nrgba64.Stride, predictor);
                break;
            case RgbaImage rgba:
                extraSamples = 1; // Associated alpha.
                EncodeRgba(dst, rgba.Pix, d.X, d.Y, rgba.Stride, predictor);
                break;
            case Rgba64Image rgba64:
                extraSamples = 1; // Associated alpha.
                bitsPerSample = new uint[] { 16, 16, 16, 16 };
                EncodeRgba64(dst, rgba64.Pix, d.X, d.Y, rgba64.Stride, predictor);
                break;
            default:
                extraSamples = 1; // Associated alpha.
                Encode(dst, m, predictor);
                break;
        }

        if (compression != (uint)TiffCompressionTypes.None)
        {
            dst.Dispose();
            imageLen = (int)buf.Length;
            WriteUint32LittleEndian(w, (uint)(imageLen + 8));
            buf.Position = 0;
            buf.CopyTo(w);
        }

        var ifd = new List<IfdEntry>
        {
            new() { Tag = TiffTags.ImageWidth, Datatype = TiffDataTypes.Short, Data = new uint[] { (uint)d.X } },
            new() { Tag = TiffTags.ImageLength, Datatype = TiffDataTypes.Short, Data = new uint[] { (uint)d.Y } },
            new() { Tag = TiffTags.BitsPerSample, Datatype = TiffDataTypes.Short, Data = bitsPerSample },
            new() { Tag = TiffTags.Compression, Datatype = TiffDataTypes.Short, Data = new uint[] { compression } },
            new() { Tag = TiffTags.PhotometricInterpretation, Datatype = TiffDataTypes.Short, Data = new uint[] { photometricInterpretation } },
            new() { Tag = TiffTags.StripOffsets, Datatype = TiffDataTypes.Long, Data = new uint[] { 8 } },
            new() { Tag = TiffTags.SamplesPerPixel, Datatype = TiffDataTypes.Short, Data = new uint[] { samplesPerPixel } },
            new() { Tag = TiffTags.RowsPerStrip, Datatype = TiffDataTypes.Short, Data = new uint[] { (uint)d.Y } },
            new() { Tag = TiffTags.StripByteCounts, Datatype = TiffDataTypes.Long, Data = new uint[] { (uint)imageLen } },
            // There is currently no support for storing the image
            // resolution, so give a bogus value of 72x72 dpi.
            new() { Tag = TiffTags.XResolution, Datatype = TiffDataTypes.Rational, Data = new uint[] { 72, 1 } },
            new() { Tag = TiffTags.YResolution, Datatype = TiffDataTypes.Rational, Data = new uint[] { 72, 1 } },
            new() { Tag = TiffTags.ResolutionUnit, Datatype = TiffDataTypes.Short, Data = new uint[] { (uint)TiffResolutionUnits.PerInch } },
        };
        if (pr != (uint)TiffPredictors.None)
        {
            ifd.Add(new IfdEntry { Tag = TiffTags.Predictor, Datatype = TiffDataTypes.Short, Data = new uint[] { pr } });
        }
        if (colorMap.Length != 0)
        {
            ifd.Add(new IfdEntry { Tag = TiffTags.ColorMap, Datatype = TiffDataTypes.Short, Data = colorMap });
        }
        if (extraSamples > 0)
        {
            ifd.Add(new IfdEntry { Tag = TiffTags.ExtraSamples, Datatype = TiffDataTypes.Short, Data = new uint[] { extraSamples } });
        }

        WriteIfd(w, imageLen + 8, ifd);
    }

    private static void WriteUint32LittleEndian(Stream w, uint v)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, v);
        w.Write(buf);
    }
}