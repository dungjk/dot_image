// Ported from golang.org/x/image/bmp/reader.go and golang.org/x/image/bmp/writer.go.
//
// Copyright 2011 The Go Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license.
// Copyright 2013 The Go Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license.


namespace DotImage.Extended.Bmp;

using System.Buffers.Binary;
using DotImage;
using DotImage.Color;
using DotImage.Extended.Internal;

/// <summary>
/// Package bmp implements a BMP image decoder and encoder.
/// The BMP specification is at http://www.digicamsoft.com/bmp/bmp.html.
/// </summary>
public static class Bmp
{
    /// <summary>ErrUnsupported means that the input BMP image uses a valid but unsupported feature.</summary>
    public const string ErrUnsupportedMessage = "bmp: unsupported BMP image";

    /// <summary>Reads a BMP image from the stream and returns it as an IImage. Limitation: The file must be 8, 24 or 32 bits per pixel.</summary>
    public static IImage Decode(Stream r)
    {
        DecodeConfigResult result = DecodeConfigInternal(r);
        return result.Bpp switch
        {
            1 or 2 or 4 or 8 => DecodePaletted(r, result.Config, result.TopDown, result.Bpp),
            24 => DecodeRgb(r, result.Config, result.TopDown),
            32 => DecodeNrgba(r, result.Config, result.TopDown, result.AllowAlpha),
            _ => throw new NotSupportedException(ErrUnsupportedMessage),
        };
    }

    /// <summary>Returns the color model and dimensions of a BMP image without decoding the entire image. Limitation: The file must be 8, 24 or 32 bits per pixel.</summary>
    public static Config DecodeConfig(Stream r)
    {
        return DecodeConfigInternal(r).Config;
    }

    /// <summary>Writes the image m to w in BMP format.</summary>
    public static void Encode(Stream w, IImage m)
    {
        Point d = m.Bounds().Size();
        if (d.X < 0 || d.Y < 0)
        {
            throw new InvalidDataException("bmp: negative bounds");
        }

        var h = new Header
        {
            SigBm = [(byte)'B', (byte)'M'],
            FileSize = 14 + 40,
            PixOffset = 14 + 40,
            DibHeaderSize = 40,
            Width = (uint)d.X,
            Height = (uint)d.Y,
            ColorPlane = 1,
        };

        int step;
        byte[]? palette = null;
        bool opaque = false;
        switch (m)
        {
            case GrayImage gray:
                step = (d.X + 3) & ~3;
                palette = new byte[1024];
                for (int i = 0; i < 256; i++)
                {
                    palette[i * 4 + 0] = (byte)i;
                    palette[i * 4 + 1] = (byte)i;
                    palette[i * 4 + 2] = (byte)i;
                    palette[i * 4 + 3] = 0xFF;
                }

                h.ImageSize = (uint)(d.Y * step);
                h.FileSize += (uint)(palette.Length + h.ImageSize);
                h.PixOffset += (uint)palette.Length;
                h.Bpp = 8;
                break;

            case PalettedImage paletted:
                step = (d.X + 3) & ~3;
                palette = new byte[1024];
                for (int i = 0; i < paletted.Palette.Length && i < 256; i++)
                {
                    (uint r, uint g, uint bl, _) = paletted.Palette[i].Rgba();
                    palette[i * 4 + 0] = (byte)(bl >> 8);
                    palette[i * 4 + 1] = (byte)(g >> 8);
                    palette[i * 4 + 2] = (byte)(r >> 8);
                    palette[i * 4 + 3] = 0xFF;
                }

                h.ImageSize = (uint)(d.Y * step);
                h.FileSize += (uint)(palette.Length + h.ImageSize);
                h.PixOffset += (uint)palette.Length;
                h.Bpp = 8;
                break;

            case RgbaImage rgba:
                opaque = rgba.Opaque();
                if (opaque)
                {
                    step = (3 * d.X + 3) & ~3;
                    h.Bpp = 24;
                }
                else
                {
                    step = 4 * d.X;
                    h.Bpp = 32;
                }

                h.ImageSize = (uint)(d.Y * step);
                h.FileSize += h.ImageSize;
                break;

            case NrgbaImage nrgba:
                opaque = nrgba.Opaque();
                if (opaque)
                {
                    step = (3 * d.X + 3) & ~3;
                    h.Bpp = 24;
                }
                else
                {
                    step = 4 * d.X;
                    h.Bpp = 32;
                }

                h.ImageSize = (uint)(d.Y * step);
                h.FileSize += h.ImageSize;
                break;

            default:
                step = (3 * d.X + 3) & ~3;
                h.ImageSize = (uint)(d.Y * step);
                h.FileSize += h.ImageSize;
                h.Bpp = 24;
                break;
        }

        byte[] headerBuf = new byte[Header.SizeInBytes];
        h.WriteTo(headerBuf);
        w.Write(headerBuf, 0, headerBuf.Length);
        if (palette != null)
        {
            w.Write(palette, 0, palette.Length);
        }

        if (d.X == 0 || d.Y == 0)
        {
            return;
        }

        switch (m)
        {
            case GrayImage gray:
                EncodePaletted(w, gray.Pix, d.X, d.Y, gray.Stride, step);
                break;
            case PalettedImage paletted:
                EncodePaletted(w, paletted.Pix, d.X, d.Y, paletted.Stride, step);
                break;
            case RgbaImage rgba:
                EncodeRgba(w, rgba.Pix, d.X, d.Y, rgba.Stride, step, opaque);
                break;
            case NrgbaImage nrgba:
                EncodeNrgba(w, nrgba.Pix, d.X, d.Y, nrgba.Stride, step, opaque);
                break;
            default:
                EncodeGeneric(w, m, step);
                break;
        }
    }

    /// <summary>Registers the bmp format. Call this to mimic Go's package-level init() registration.</summary>
    public static class FormatRegistration
    {
        public static void Register()
        {
            DotImage.FormatRegistry.RegisterFormat(
                "bmp",
                "BM????\x00\x00\x00\x00",
                Decode,
                DecodeConfig);
        }
    }

    private static ushort ReadUint16(ReadOnlySpan<byte> b)
    {
        return (ushort)(b[0] | b[1] << 8);
    }

    private static uint ReadUint32(ReadOnlySpan<byte> b)
    {
        return (uint)(b[0] | b[1] << 8 | b[2] << 16 | b[3] << 24);
    }

    /// <summary>Reads a 1, 2, 4 or 8 bit-per-pixel BMP image from r. If topDown is false, the image rows will be read bottom-up.</summary>
    private static IImage DecodePaletted(Stream r, Config c, bool topDown, int bpp)
    {
        Palette palette = (Palette)c.ColorModel;
        PalettedImage paletted = Images.NewPaletted(Geometry.Rect(0, 0, c.Width, c.Height), palette);
        if (c.Width == 0 || c.Height == 0)
        {
            return paletted;
        }

        int y0, y1, yDelta = -1;
        y0 = c.Height - 1;
        y1 = -1;
        if (topDown)
        {
            y0 = 0;
            y1 = c.Height;
            yDelta = +1;
        }

        int pixelsPerByte = 8 / bpp;
        // Pad up to ensure each row is 4-bytes aligned.
        int bytesPerRow = ((c.Width + pixelsPerByte - 1) / pixelsPerByte + 3) & ~3;
        byte[] b = new byte[bytesPerRow];

        for (int y = y0; y != y1; y += yDelta)
        {
            Span<byte> p = paletted.Pix.Span.Slice(y * paletted.Stride, c.Width);
            ReadFull(r, b);
            int byteIndex = 0, bitIndex = 8;
            byte mask = (byte)((1 << bpp) - 1);
            for (int pixIndex = 0; pixIndex < c.Width; pixIndex++)
            {
                bitIndex -= bpp;
                byte paletteIndex = (byte)(b[byteIndex] >> bitIndex & mask);
                if (paletteIndex >= palette.Length)
                {
                    throw new InvalidDataException("bmp: invalid palette index");
                }

                p[pixIndex] = paletteIndex;
                if (bitIndex == 0)
                {
                    byteIndex++;
                    bitIndex = 8;
                }
            }
        }

        return paletted;
    }

    /// <summary>Reads a 24 bit-per-pixel BMP image from r. If topDown is false, the image rows will be read bottom-up.</summary>
    private static IImage DecodeRgb(Stream r, Config c, bool topDown)
    {
        RgbaImage rgba = Images.NewRgba(Geometry.Rect(0, 0, c.Width, c.Height));
        if (c.Width == 0 || c.Height == 0)
        {
            return rgba;
        }

        // There are 3 bytes per pixel, and each row is 4-byte aligned.
        byte[] b = new byte[(3 * c.Width + 3) & ~3];
        int y0, y1, yDelta = -1;
        y0 = c.Height - 1;
        y1 = -1;
        if (topDown)
        {
            y0 = 0;
            y1 = c.Height;
            yDelta = +1;
        }

        for (int y = y0; y != y1; y += yDelta)
        {
            ReadFull(r, b);
            Span<byte> p = rgba.Pix.Span.Slice(y * rgba.Stride, c.Width * 4);
            for (int i = 0, j = 0; i < p.Length; i += 4, j += 3)
            {
                // BMP images are stored in BGR order rather than RGB order.
                p[i + 0] = b[j + 2];
                p[i + 1] = b[j + 1];
                p[i + 2] = b[j + 0];
                p[i + 3] = 0xFF;
            }
        }

        return rgba;
    }

    /// <summary>Reads a 32 bit-per-pixel BMP image from r. If topDown is false, the image rows will be read bottom-up.</summary>
    private static IImage DecodeNrgba(Stream r, Config c, bool topDown, bool allowAlpha)
    {
        NrgbaImage rgba = Images.NewNrgba(Geometry.Rect(0, 0, c.Width, c.Height));
        if (c.Width == 0 || c.Height == 0)
        {
            return rgba;
        }

        int y0, y1, yDelta = -1;
        y0 = c.Height - 1;
        y1 = -1;
        if (topDown)
        {
            y0 = 0;
            y1 = c.Height;
            yDelta = +1;
        }

        for (int y = y0; y != y1; y += yDelta)
        {
            Span<byte> p = rgba.Pix.Span.Slice(y * rgba.Stride, c.Width * 4);
            ReadFull(r, p);
            for (int i = 0; i < p.Length; i += 4)
            {
                // BMP images are stored in BGRA order rather than RGBA order.
                (p[i + 0], p[i + 2]) = (p[i + 2], p[i + 0]);
                if (!allowAlpha)
                {
                    p[i + 3] = 0xFF;
                }
            }
        }

        return rgba;
    }

    private struct DecodeConfigResult
    {
        public Config Config;
        public int Bpp;
        public bool TopDown;
        public bool AllowAlpha;
    }

    private static DecodeConfigResult DecodeConfigInternal(Stream r)
    {
        var result = new DecodeConfigResult();

        // We only support those BMP images with one of the following DIB headers:
        // - BITMAPINFOHEADER (40 bytes)
        // - BITMAPV4HEADER (108 bytes)
        // - BITMAPV5HEADER (124 bytes)
        const int fileHeaderLen = 14;
        const int infoHeaderLen = 40;
        const int v4InfoHeaderLen = 108;
        const int v5InfoHeaderLen = 124;

        byte[] b = new byte[1024];
        ReadFull(r, b.AsSpan(0, fileHeaderLen + 4));

        if (b[0] != 'B' || b[1] != 'M')
        {
            throw new InvalidDataException("bmp: invalid format");
        }

        uint offset = ReadUint32(b.AsSpan(10, 4));
        uint infoLen = ReadUint32(b.AsSpan(14, 4));
        if (infoLen != infoHeaderLen && infoLen != v4InfoHeaderLen && infoLen != v5InfoHeaderLen)
        {
            throw new NotSupportedException(ErrUnsupportedMessage);
        }

        ReadFull(r, b.AsSpan(fileHeaderLen + 4, (int)infoLen - 4));

        int width = (int)ReadUint32(b.AsSpan(18, 4));
        int height = (int)ReadUint32(b.AsSpan(22, 4));
        if (height < 0)
        {
            height = -height;
            result.TopDown = true;
        }

        if (width < 0 || height < 0)
        {
            throw new NotSupportedException(ErrUnsupportedMessage);
        }

        if ((width == 0) != (height == 0))
        {
            // We'll take 0x0, but Nx0 or 0xN is suspicious.
            throw new NotSupportedException(ErrUnsupportedMessage);
        }

        // Check that the image fits in memory.
        // This conservatively assumes 4 bytes per pixel,
        // rather than using the actual pixel size.
        if (!SafeMath.Mul3(width, height, 4).Ok)
        {
            throw new NotSupportedException(ErrUnsupportedMessage);
        }

        // We only support 1 plane and 8, 24 or 32 bits per pixel and no
        // compression.
        ushort planes = ReadUint16(b.AsSpan(26, 2));
        ushort bpp = ReadUint16(b.AsSpan(28, 2));
        uint compression = ReadUint32(b.AsSpan(30, 4));
        // if compression is set to BI_BITFIELDS, but the bitmask is set to the default bitmask
        // that would be used if compression was set to 0, we can continue as if compression was 0
        if (compression == 3 && infoLen > infoHeaderLen &&
            ReadUint32(b.AsSpan(54, 4)) == 0xff0000 && ReadUint32(b.AsSpan(58, 4)) == 0xff00 &&
            ReadUint32(b.AsSpan(62, 4)) == 0xff && ReadUint32(b.AsSpan(66, 4)) == 0xff000000)
        {
            compression = 0;
        }

        if (planes != 1 || compression != 0)
        {
            throw new NotSupportedException(ErrUnsupportedMessage);
        }

        switch (bpp)
        {
            case 1:
            case 2:
            case 4:
            case 8:
            {
                uint colorUsed = ReadUint32(b.AsSpan(46, 4));

                if (colorUsed == 0)
                {
                    colorUsed = (uint)(1 << bpp);
                }
                else if (colorUsed > (uint)(1 << bpp))
                {
                    throw new NotSupportedException(ErrUnsupportedMessage);
                }

                if (offset != fileHeaderLen + infoLen + colorUsed * 4)
                {
                    throw new NotSupportedException(ErrUnsupportedMessage);
                }

                ReadFull(r, b.AsSpan(0, (int)(colorUsed * 4)));

                IColor[] pcm = new IColor[colorUsed];
                for (int i = 0; i < pcm.Length; i++)
                {
                    // BMP images are stored in BGR order rather than RGB order.
                    // Every 4th byte is padding.
                    pcm[i] = new Rgba(b[4 * i + 2], b[4 * i + 1], b[4 * i + 0], 0xFF);
                }

                result.Config = new Config(new Palette(pcm), width, height);
                result.Bpp = bpp;
                return result;
            }

            case 24:
                if (offset != fileHeaderLen + infoLen)
                {
                    throw new NotSupportedException(ErrUnsupportedMessage);
                }

                result.Config = new Config(ColorModels.Rgba, width, height);
                result.Bpp = 24;
                return result;

            case 32:
                if (offset != fileHeaderLen + infoLen)
                {
                    throw new NotSupportedException(ErrUnsupportedMessage);
                }

                // 32 bits per pixel is possibly RGBX (X is padding) or RGBA (A is
                // alpha transparency). However, for BMP images, "Alpha is a
                // poorly-documented and inconsistently-used feature" says
                // https://source.chromium.org/chromium/chromium/src/+/bc0a792d7ebc587190d1a62ccddba10abeea274b:third_party/blink/renderer/platform/image-decoders/bmp/bmp_image_reader.cc;l=621
                //
                // That goes on to say "BITMAPV3HEADER+ have an alpha bitmask in the
                // info header... so we respect it at all times... [For earlier
                // (smaller) headers we] ignore alpha in Windows V3 BMPs except inside
                // ICO files".
                //
                // "Ignore" means to always set alpha to 0xFF (fully opaque):
                // https://source.chromium.org/chromium/chromium/src/+/bc0a792d7ebc587190d1a62ccddba10abeea274b:third_party/blink/renderer/platform/image-decoders/bmp/bmp_image_reader.h;l=272
                //
                // Confusingly, "Windows V3" does not correspond to BITMAPV3HEADER, but
                // instead corresponds to the earlier (smaller) BITMAPINFOHEADER:
                // https://source.chromium.org/chromium/chromium/src/+/bc0a792d7ebc587190d1a62ccddba10abeea274b:third_party/blink/renderer/platform/image-decoders/bmp/bmp_image_reader.cc;l=258
                //
                // This Go package does not support ICO files and the (infoLen >
                // infoHeaderLen) condition distinguishes BITMAPINFOHEADER (40 bytes)
                // vs later (larger) headers.
                result.Config = new Config(ColorModels.Rgba, width, height);
                result.Bpp = 32;
                result.AllowAlpha = infoLen > infoHeaderLen;
                return result;

            default:
                throw new NotSupportedException(ErrUnsupportedMessage);
        }
    }

    private static void ReadFull(Stream r, Span<byte> b)
    {
        int total = 0;
        Span<byte> dst = b;
        while (total < dst.Length)
        {
            int n = r.Read(dst[total..]);
            if (n <= 0)
            {
                throw new EndOfStreamException();
            }

            total += n;
        }
    }

    private static void ReadFull(Stream r, byte[] b)
    {
        ReadFull(r, b.AsSpan());
    }

    private static void EncodePaletted(Stream w, Memory<byte> pix, int dx, int dy, int stride, int step)
    {
        byte[]? padding = null;
        if (dx < step)
        {
            padding = new byte[step - dx];
        }

        for (int y = dy - 1; y >= 0; y--)
        {
            int min = y * stride;
            int max = y * stride + dx;
            w.Write(pix.Span.Slice(min, max - min));
            if (padding != null)
            {
                w.Write(padding, 0, padding.Length);
            }
        }
    }

    private static void EncodeRgba(Stream w, Memory<byte> pix, int dx, int dy, int stride, int step, bool opaque)
    {
        byte[] buf = new byte[step];
        Span<byte> p = pix.Span;
        if (opaque)
        {
            for (int y = dy - 1; y >= 0; y--)
            {
                int min = y * stride;
                int max = y * stride + dx * 4;
                int off = 0;
                for (int i = min; i < max; i += 4)
                {
                    buf[off + 2] = p[i + 0];
                    buf[off + 1] = p[i + 1];
                    buf[off + 0] = p[i + 2];
                    off += 3;
                }

                w.Write(buf, 0, buf.Length);
            }
        }
        else
        {
            for (int y = dy - 1; y >= 0; y--)
            {
                int min = y * stride;
                int max = y * stride + dx * 4;
                int off = 0;
                for (int i = min; i < max; i += 4)
                {
                    uint a = p[i + 3];
                    if (a == 0)
                    {
                        buf[off + 2] = 0;
                        buf[off + 1] = 0;
                        buf[off + 0] = 0;
                        buf[off + 3] = 0;
                        off += 4;
                        continue;
                    }
                    else if (a == 0xff)
                    {
                        buf[off + 2] = p[i + 0];
                        buf[off + 1] = p[i + 1];
                        buf[off + 0] = p[i + 2];
                        buf[off + 3] = 0xff;
                        off += 4;
                        continue;
                    }

                    buf[off + 2] = (byte)(((uint)p[i + 0] * 0xffff) / a >> 8);
                    buf[off + 1] = (byte)(((uint)p[i + 1] * 0xffff) / a >> 8);
                    buf[off + 0] = (byte)(((uint)p[i + 2] * 0xffff) / a >> 8);
                    buf[off + 3] = (byte)a;
                    off += 4;
                }

                w.Write(buf, 0, buf.Length);
            }
        }
    }

    private static void EncodeNrgba(Stream w, Memory<byte> pix, int dx, int dy, int stride, int step, bool opaque)
    {
        byte[] buf = new byte[step];
        Span<byte> p = pix.Span;
        if (opaque)
        {
            for (int y = dy - 1; y >= 0; y--)
            {
                int min = y * stride;
                int max = y * stride + dx * 4;
                int off = 0;
                for (int i = min; i < max; i += 4)
                {
                    buf[off + 2] = p[i + 0];
                    buf[off + 1] = p[i + 1];
                    buf[off + 0] = p[i + 2];
                    off += 3;
                }

                w.Write(buf, 0, buf.Length);
            }
        }
        else
        {
            for (int y = dy - 1; y >= 0; y--)
            {
                int min = y * stride;
                int max = y * stride + dx * 4;
                int off = 0;
                for (int i = min; i < max; i += 4)
                {
                    buf[off + 2] = p[i + 0];
                    buf[off + 1] = p[i + 1];
                    buf[off + 0] = p[i + 2];
                    buf[off + 3] = p[i + 3];
                    off += 4;
                }

                w.Write(buf, 0, buf.Length);
            }
        }
    }

    private static void EncodeGeneric(Stream w, IImage m, int step)
    {
        Rect b = m.Bounds();
        byte[] buf = new byte[step];
        for (int y = b.Max.Y - 1; y >= b.Min.Y; y--)
        {
            int off = 0;
            for (int x = b.Min.X; x < b.Max.X; x++)
            {
                (uint r, uint g, uint bl, _) = m.At(x, y).Rgba();
                buf[off + 2] = (byte)(r >> 8);
                buf[off + 1] = (byte)(g >> 8);
                buf[off + 0] = (byte)(bl >> 8);
                off += 3;
            }

            w.Write(buf, 0, buf.Length);
        }
    }

    private struct Header
    {
        public const int SizeInBytes = 54;

        public byte[] SigBm;
        public uint FileSize;
        public uint PixOffset;
        public uint DibHeaderSize;
        public uint Width;
        public uint Height;
        public ushort ColorPlane;
        public ushort Bpp;
        public uint Compression;
        public uint ImageSize;
        public uint XPixelsPerMeter;
        public uint YPixelsPerMeter;
        public uint ColorUse;
        public uint ColorImportant;

        public void WriteTo(byte[] b)
        {
            b[0] = SigBm[0];
            b[1] = SigBm[1];
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(2, 4), FileSize);
            b.AsSpan(6, 4).Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(10, 4), PixOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(14, 4), DibHeaderSize);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(18, 4), Width);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(22, 4), Height);
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(26, 2), ColorPlane);
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(28, 2), Bpp);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(30, 4), Compression);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(34, 4), ImageSize);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(38, 4), XPixelsPerMeter);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(42, 4), YPixelsPerMeter);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(46, 4), ColorUse);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(50, 4), ColorImportant);
        }
    }
}