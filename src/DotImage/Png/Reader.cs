// Ported from Go src/image/png/reader.go


using System.Buffers.Binary;
using System.IO.Compression;
using DotImage.Color;

namespace DotImage.Png;

public static class PngReader
{
    private const byte CtGrayscale = 0;
    private const byte CtTrueColor = 2;
    private const byte CtPaletted = 3;
    private const byte CtGrayscaleAlpha = 4;
    private const byte CtTrueColorAlpha = 6;

    private const int CbInvalid = 0;
    private const int CbG1 = 1;
    private const int CbG2 = 2;
    private const int CbG4 = 3;
    private const int CbG8 = 4;
    private const int CbGA8 = 5;
    private const int CbTC8 = 6;
    private const int CbP1 = 7;
    private const int CbP2 = 8;
    private const int CbP4 = 9;
    private const int CbP8 = 10;
    private const int CbTCA8 = 11;
    private const int CbG16 = 12;
    private const int CbGA16 = 13;
    private const int CbTC16 = 14;
    private const int CbTCA16 = 15;

    private const int FtNone = 0;
    private const int FtSub = 1;
    private const int FtUp = 2;
    private const int FtAverage = 3;
    private const int FtPaeth = 4;

    private const int ItNone = 0;
    private const int ItAdam7 = 1;

    private const int DsStart = 0;
    private const int DsSeenIHDR = 1;
    private const int DsSeenPLTE = 2;
    private const int DsSeentRNS = 3;
    private const int DsSeenIDAT = 4;
    private const int DsSeenIEND = 5;

    private const uint ChunkIHDR = 0x49484452;
    private const uint ChunkPLTE = 0x504C5445;
    private const uint ChunkTRNS = 0x74524E53;
    private const uint ChunkIDAT = 0x49444154;
    private const uint ChunkIEND = 0x49454E44;

    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private static readonly (int XFactor, int YFactor, int XOffset, int YOffset)[] Interlacing =
    [
        (8, 8, 0, 0),
        (8, 8, 4, 0),
        (4, 8, 0, 4),
        (4, 4, 2, 0),
        (2, 4, 0, 2),
        (2, 2, 1, 0),
        (1, 2, 0, 1),
    ];

    private static bool CbPaletted(int cb) => cb is >= CbP1 and <= CbP8;
    private static bool CbTrueColor(int cb) => cb is CbTC8 or CbTC16;

    public static IImage Decode(Stream r) => new Decoder(r).Decode();

    public static Config DecodeConfig(Stream r) => new Decoder(r).DecodeConfig();

    private sealed class Decoder : Stream
    {
        private readonly Stream _r;
        private IImage? _img;
        private readonly Crc32Hasher _crc = new();
        private int _width, _height, _depth, _cb = CbInvalid, _stage = DsStart;
        private uint _idatLength;
        private Palette _palette = new([]);
        private readonly byte[] _tmp = new byte[3 * 256];
        private int _interlace;
        private bool _useTransparent;
        private readonly byte[] _transparent = new byte[6];
        private byte[] _rowCr = Array.Empty<byte>();
        private byte[] _rowPr = Array.Empty<byte>();

        public Decoder(Stream r) => _r = r;

        public IImage Decode()
        {
            CheckHeader();
            while (_stage != DsSeenIEND)
                ParseChunk(configOnly: false);
            return _img ?? throw new PngFormatException("no image data");
        }

        public Config DecodeConfig()
        {
            CheckHeader();
            while (true)
            {
                ParseChunk(configOnly: true);
                if (CbPaletted(_cb))
                {
                    if (_stage >= DsSeentRNS) break;
                }
                else if (_stage >= DsSeenIHDR)
                {
                    break;
                }
            }

            IColorModel cm = _cb switch
            {
                CbG1 or CbG2 or CbG4 or CbG8 => Models.Gray,
                CbGA8 => Models.Nrgba,
                CbTC8 => Models.Rgba,
                CbP1 or CbP2 or CbP4 or CbP8 => _palette,
                CbTCA8 => Models.Nrgba,
                CbG16 => Models.Gray16,
                CbGA16 => Models.Nrgba64,
                CbTC16 => Models.Rgba64,
                CbTCA16 => Models.Nrgba64,
                _ => Models.Rgba,
            };
            return new Config(cm, _width, _height);
        }

        private void CheckHeader()
        {
            Span<byte> hdr = stackalloc byte[PngHeader.Length];
            ReadExactly(_r, hdr);
            if (!hdr.SequenceEqual(PngHeader))
                throw new PngFormatException("not a PNG file");
        }

        private void ParseIHDR(uint length)
        {
            if (length != 13) throw new PngFormatException("bad IHDR length");
            ReadExactly(_r, _tmp.AsSpan(0, 13));
            _crc.Write(_tmp.AsSpan(0, 13));
            if (_tmp[10] != 0) throw new PngUnsupportedException("compression method");
            if (_tmp[11] != 0) throw new PngUnsupportedException("filter method");
            if (_tmp[12] != ItNone && _tmp[12] != ItAdam7)
                throw new PngFormatException("invalid interlace method");
            _interlace = _tmp[12];

            int w = BinaryPrimitives.ReadInt32BigEndian(_tmp.AsSpan(0, 4));
            int h = BinaryPrimitives.ReadInt32BigEndian(_tmp.AsSpan(4, 4));
            if (w <= 0 || h <= 0) throw new PngFormatException("non-positive dimension");
            long nPixels64 = (long)w * h;
            if (nPixels64 != (int)nPixels64) throw new PngUnsupportedException("dimension overflow");
            int nPixels = (int)nPixels64;
            if (nPixels != (nPixels * 8) / 8) throw new PngUnsupportedException("dimension overflow");

            _cb = CbInvalid;
            _depth = _tmp[8];
            switch (_depth)
            {
                case 1:
                    _cb = _tmp[9] switch { CtGrayscale => CbG1, CtPaletted => CbP1, _ => CbInvalid };
                    break;
                case 2:
                    _cb = _tmp[9] switch { CtGrayscale => CbG2, CtPaletted => CbP2, _ => CbInvalid };
                    break;
                case 4:
                    _cb = _tmp[9] switch { CtGrayscale => CbG4, CtPaletted => CbP4, _ => CbInvalid };
                    break;
                case 8:
                    _cb = _tmp[9] switch
                    {
                        CtGrayscale => CbG8,
                        CtTrueColor => CbTC8,
                        CtPaletted => CbP8,
                        CtGrayscaleAlpha => CbGA8,
                        CtTrueColorAlpha => CbTCA8,
                        _ => CbInvalid,
                    };
                    break;
                case 16:
                    _cb = _tmp[9] switch
                    {
                        CtGrayscale => CbG16,
                        CtTrueColor => CbTC16,
                        CtGrayscaleAlpha => CbGA16,
                        CtTrueColorAlpha => CbTCA16,
                        _ => CbInvalid,
                    };
                    break;
            }
            if (_cb == CbInvalid)
                throw new PngUnsupportedException($"bit depth {_tmp[8]}, color type {_tmp[9]}");
            _width = w;
            _height = h;
            VerifyChecksum();
        }

        private void ParsePLTE(uint length)
        {
            int np = (int)(length / 3);
            if (length % 3 != 0 || np <= 0 || np > 256 || np > (1 << _depth))
                throw new PngFormatException("bad PLTE length");
            ReadExactly(_r, _tmp.AsSpan(0, 3 * np));
            _crc.Write(_tmp.AsSpan(0, 3 * np));
            switch (_cb)
            {
                case CbP1 or CbP2 or CbP4 or CbP8:
                {
                    var colors = new IColor[256];
                    for (int i = 0; i < np; i++)
                        colors[i] = new Rgba(_tmp[3 * i], _tmp[3 * i + 1], _tmp[3 * i + 2], 0xff);
                    for (int i = np; i < 256; i++)
                        colors[i] = new Rgba(0, 0, 0, 0xff);
                    _palette = new Palette(colors[..np]);
                    break;
                }
                case CbTC8 or CbTCA8 or CbTC16 or CbTCA16:
                    break;
                default:
                    throw new PngFormatException("PLTE, color type mismatch");
            }
            VerifyChecksum();
        }

        private void ParseTRNS(uint length)
        {
            switch (_cb)
            {
                case CbG1 or CbG2 or CbG4 or CbG8 or CbG16:
                    if (length != 2) throw new PngFormatException("bad tRNS length");
                    ReadExactly(_r, _tmp.AsSpan(0, (int)length));
                    _crc.Write(_tmp.AsSpan(0, (int)length));
                    _transparent[0] = _tmp[0];
                    _transparent[1] = _tmp[1];
                    switch (_cb)
                    {
                        case CbG1: _transparent[1] *= 0xff; break;
                        case CbG2: _transparent[1] *= 0x55; break;
                        case CbG4: _transparent[1] *= 0x11; break;
                    }
                    _useTransparent = true;
                    break;
                case CbTC8 or CbTC16:
                    if (length != 6) throw new PngFormatException("bad tRNS length");
                    ReadExactly(_r, _tmp.AsSpan(0, (int)length));
                    _crc.Write(_tmp.AsSpan(0, (int)length));
                    Array.Copy(_tmp, _transparent, 6);
                    _useTransparent = true;
                    break;
                case CbP1 or CbP2 or CbP4 or CbP8:
                    if (length > 256) throw new PngFormatException("bad tRNS length");
                    ReadExactly(_r, _tmp.AsSpan(0, (int)length));
                    _crc.Write(_tmp.AsSpan(0, (int)length));
                    int trnsLen = (int)length;
                    int palLen = Math.Max(_palette.Length, trnsLen);
                    var colors = new IColor[palLen];
                    for (int i = 0; i < _palette.Length; i++)
                        colors[i] = _palette[i];
                    for (int i = 0; i < trnsLen; i++)
                    {
                        var rgba = (Rgba)Models.Rgba.Convert(_palette[i]);
                        colors[i] = new Nrgba(rgba.R, rgba.G, rgba.B, _tmp[i]);
                    }
                    _palette = new Palette(colors);
                    break;
                default:
                    throw new PngFormatException("tRNS, color type mismatch");
            }
            VerifyChecksum();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0) return 0;
            Span<byte> hdr = stackalloc byte[8];
            while (_idatLength == 0)
            {
                VerifyChecksum();
                ReadExactly(_r, hdr);
                _idatLength = BinaryPrimitives.ReadUInt32BigEndian(hdr);
                if (!hdr[4..8].SequenceEqual("IDAT"u8))
                    throw new PngFormatException("not enough pixel data");
                _crc.Reset();
                _crc.Write(hdr[4..8]);
            }
            if ((int)_idatLength < 0) throw new PngUnsupportedException("IDAT chunk length overflow");
            int toRead = Math.Min(buffer.Length, (int)_idatLength);
            int n = _r.Read(buffer[..toRead]);
            if (n > 0)
            {
                _crc.Write(buffer[..n]);
                _idatLength -= (uint)n;
            }
            return n;
        }

        private IImage DecodeImage()
        {
            using var zlib = new ZLibStream(this, CompressionMode.Decompress, leaveOpen: true);
            IImage img;
            if (_interlace == ItNone)
            {
                img = ReadImagePass(zlib, 0, allocateOnly: false)
                    ?? throw new PngFormatException("no image data");
            }
            else
            {
                img = ReadImagePass(null, 0, allocateOnly: true)
                    ?? throw new PngFormatException("no image data");
                for (int pass = 0; pass < 7; pass++)
                {
                    var imagePass = ReadImagePass(zlib, pass, allocateOnly: false);
                    if (imagePass != null)
                        MergePassInto(img, imagePass, pass);
                }
            }

            int n = 0;
            Exception? err = null;
            for (int i = 0; n == 0 && err == null && i < 100; i++)
            {
                try
                {
                    n = zlib.Read(_tmp.AsSpan(0, 1));
                }
                catch (Exception ex)
                {
                    err = ex;
                }
            }
            if (err != null && err is not EndOfStreamException)
                throw new PngFormatException(err.Message);
            if (n != 0 || _idatLength != 0)
                throw new PngFormatException("too much pixel data");
            return img;
        }

        private void EnsureRowBuffers(int rowSize)
        {
            if (_rowCr.Length < rowSize)
            {
                _rowCr = new byte[rowSize];
                _rowPr = new byte[rowSize];
            }
        }

        private IImage? ReadImagePass(Stream? r, int pass, bool allocateOnly)
        {
            int bitsPerPixel = 0;
            int pixOffset = 0;
            GrayImage? gray = null;
            RgbaImage? rgba = null;
            PalettedImage? paletted = null;
            NrgbaImage? nrgba = null;
            Gray16Image? gray16 = null;
            Rgba64Image? rgba64 = null;
            Nrgba64Image? nrgba64 = null;
            IImage? img = null;

            int width = _width, height = _height;
            if (_interlace == ItAdam7 && !allocateOnly)
            {
                var p = Interlacing[pass];
                width = (width - p.XOffset + p.XFactor - 1) / p.XFactor;
                height = (height - p.YOffset + p.YFactor - 1) / p.YFactor;
                if (width == 0 || height == 0) return null;
            }

            var rect = Geometry.Rect(0, 0, width, height);
            switch (_cb)
                {
                    case CbG1 or CbG2 or CbG4 or CbG8:
                        bitsPerPixel = _depth;
                        if (_useTransparent) { nrgba = Images.NewNrgba(rect); img = nrgba; }
                        else { gray = Images.NewGray(rect); img = gray; }
                        break;
                    case CbGA8:
                        bitsPerPixel = 16;
                        nrgba = Images.NewNrgba(rect);
                        img = nrgba;
                        break;
                    case CbTC8:
                        bitsPerPixel = 24;
                        if (_useTransparent) { nrgba = Images.NewNrgba(rect); img = nrgba; }
                        else { rgba = Images.NewRgba(rect); img = rgba; }
                        break;
                    case CbP1 or CbP2 or CbP4 or CbP8:
                        bitsPerPixel = _depth;
                        paletted = Images.NewPaletted(rect, _palette);
                        img = paletted;
                        break;
                    case CbTCA8:
                        bitsPerPixel = 32;
                        nrgba = Images.NewNrgba(rect);
                        img = nrgba;
                        break;
                    case CbG16:
                        bitsPerPixel = 16;
                        if (_useTransparent) { nrgba64 = Images.NewNrgba64(rect); img = nrgba64; }
                        else { gray16 = Images.NewGray16(rect); img = gray16; }
                        break;
                    case CbGA16:
                        bitsPerPixel = 32;
                        nrgba64 = Images.NewNrgba64(rect);
                        img = nrgba64;
                        break;
                    case CbTC16:
                        bitsPerPixel = 48;
                        if (_useTransparent) { nrgba64 = Images.NewNrgba64(rect); img = nrgba64; }
                        else { rgba64 = Images.NewRgba64(rect); img = rgba64; }
                        break;
                    case CbTCA16:
                        bitsPerPixel = 64;
                    nrgba64 = Images.NewNrgba64(rect);
                    img = nrgba64;
                    break;
            }
            if (allocateOnly) return img;
            ArgumentNullException.ThrowIfNull(r, nameof(r));

            int bytesPerPixel = (bitsPerPixel + 7) / 8;
            long rowSize = 1 + ((long)bitsPerPixel * width + 7) / 8;
            if (rowSize != (int)rowSize) throw new PngUnsupportedException("dimension overflow");
            EnsureRowBuffers((int)rowSize);
            var cr = _rowCr;
            var pr = _rowPr;

            for (int y = 0; y < height; y++)
            {
                ReadExactly(r, cr);
                var cdat = cr.AsSpan(1);
                var pdat = pr.AsSpan(1);
                switch (cr[0])
                {
                    case FtNone: break;
                    case FtSub:
                        for (int i = bytesPerPixel; i < cdat.Length; i++)
                            cdat[i] += cdat[i - bytesPerPixel];
                        break;
                    case FtUp:
                        for (int i = 0; i < cdat.Length; i++)
                            cdat[i] += pdat[i];
                        break;
                    case FtAverage:
                        for (int i = 0; i < bytesPerPixel; i++)
                            cdat[i] += (byte)(pdat[i] / 2);
                        for (int i = bytesPerPixel; i < cdat.Length; i++)
                            cdat[i] += (byte)((cdat[i - bytesPerPixel] + pdat[i]) / 2);
                        break;
                    case FtPaeth:
                        Paeth.FilterPaeth(cdat, pdat, bytesPerPixel);
                        break;
                    default:
                        throw new PngFormatException("bad filter type");
                }

                switch (_cb)
                {
                    case CbG1: DecodeG1(cdat, width, gray, nrgba, ref pixOffset); break;
                    case CbG2: DecodeG2(cdat, width, gray, nrgba, ref pixOffset); break;
                    case CbG4: DecodeG4(cdat, width, gray, nrgba, ref pixOffset); break;
                    case CbG8: DecodeG8(cdat, width, gray, nrgba, ref pixOffset); break;
                    case CbGA8: DecodeGA8(cdat, width, nrgba!, ref pixOffset); break;
                    case CbTC8: DecodeTC8(cdat, width, rgba, nrgba, ref pixOffset); break;
                    case CbP1: DecodeP1(cdat, width, paletted!, ref pixOffset); break;
                    case CbP2: DecodeP2(cdat, width, paletted!, ref pixOffset); break;
                    case CbP4: DecodeP4(cdat, width, paletted!, ref pixOffset); break;
                    case CbP8: DecodeP8(cdat, width, paletted!, ref pixOffset); break;
                    case CbTCA8: DecodeTCA8(cdat, ref pixOffset, nrgba!); break;
                    case CbG16: DecodeG16(cdat, width, gray16, nrgba64, ref pixOffset); break;
                    case CbGA16: DecodeGA16(cdat, width, nrgba64!, ref pixOffset); break;
                    case CbTC16: DecodeTC16(cdat, width, rgba64, nrgba64, ref pixOffset); break;
                    case CbTCA16: DecodeTCA16(cdat, width, nrgba64!, ref pixOffset); break;
                }
                (pr, cr) = (cr, pr);
            }
            Array.Clear(cr, 0, (int)rowSize);
            Array.Clear(pr, 0, (int)rowSize);
            return img;
        }

        private void DecodeG1(ReadOnlySpan<byte> cdat, int width, GrayImage? gray, NrgbaImage? nrgba, ref int pixOffset)
        {
            if (_useTransparent)
            {
                byte ty = _transparent[1];
                var row = nrgba!.Pix.Span.Slice(pixOffset, width * 4);
                int di = 0;
                for (int x = 0; x < width; x += 8)
                {
                    byte b = cdat[x / 8];
                    for (int x2 = 0; x2 < 8 && x + x2 < width; x2++)
                    {
                        byte ycol = (byte)((b >> 7) * 0xff);
                        row[di++] = ycol;
                        row[di++] = ycol;
                        row[di++] = ycol;
                        row[di++] = ycol == ty ? (byte)0 : (byte)0xff;
                        b <<= 1;
                    }
                }
                pixOffset += nrgba.Stride;
            }
            else
            {
                var row = gray!.Pix.Span.Slice(pixOffset, width);
                int di = 0;
                for (int x = 0; x < width; x += 8)
                {
                    byte b = cdat[x / 8];
                    for (int x2 = 0; x2 < 8 && x + x2 < width; x2++)
                    {
                        row[di++] = (byte)((b >> 7) * 0xff);
                        b <<= 1;
                    }
                }
                pixOffset += gray.Stride;
            }
        }

        private void DecodeG2(ReadOnlySpan<byte> cdat, int width, GrayImage? gray, NrgbaImage? nrgba, ref int pixOffset)
        {
            if (_useTransparent)
            {
                byte ty = _transparent[1];
                var row = nrgba!.Pix.Span.Slice(pixOffset, width * 4);
                int di = 0;
                for (int x = 0; x < width; x += 4)
                {
                    byte b = cdat[x / 4];
                    for (int x2 = 0; x2 < 4 && x + x2 < width; x2++)
                    {
                        byte ycol = (byte)((b >> 6) * 0x55);
                        row[di++] = ycol;
                        row[di++] = ycol;
                        row[di++] = ycol;
                        row[di++] = ycol == ty ? (byte)0 : (byte)0xff;
                        b <<= 2;
                    }
                }
                pixOffset += nrgba.Stride;
            }
            else
            {
                var row = gray!.Pix.Span.Slice(pixOffset, width);
                int di = 0;
                for (int x = 0; x < width; x += 4)
                {
                    byte b = cdat[x / 4];
                    for (int x2 = 0; x2 < 4 && x + x2 < width; x2++)
                    {
                        row[di++] = (byte)((b >> 6) * 0x55);
                        b <<= 2;
                    }
                }
                pixOffset += gray.Stride;
            }
        }

        private void DecodeG4(ReadOnlySpan<byte> cdat, int width, GrayImage? gray, NrgbaImage? nrgba, ref int pixOffset)
        {
            if (_useTransparent)
            {
                byte ty = _transparent[1];
                var row = nrgba!.Pix.Span.Slice(pixOffset, width * 4);
                int di = 0;
                for (int x = 0; x < width; x += 2)
                {
                    byte b = cdat[x / 2];
                    for (int x2 = 0; x2 < 2 && x + x2 < width; x2++)
                    {
                        byte ycol = (byte)((b >> 4) * 0x11);
                        row[di++] = ycol;
                        row[di++] = ycol;
                        row[di++] = ycol;
                        row[di++] = ycol == ty ? (byte)0 : (byte)0xff;
                        b <<= 4;
                    }
                }
                pixOffset += nrgba.Stride;
            }
            else
            {
                var row = gray!.Pix.Span.Slice(pixOffset, width);
                int di = 0;
                for (int x = 0; x < width; x += 2)
                {
                    byte b = cdat[x / 2];
                    for (int x2 = 0; x2 < 2 && x + x2 < width; x2++)
                    {
                        row[di++] = (byte)((b >> 4) * 0x11);
                        b <<= 4;
                    }
                }
                pixOffset += gray.Stride;
            }
        }

        private void DecodeG8(ReadOnlySpan<byte> cdat, int width, GrayImage? gray, NrgbaImage? nrgba, ref int pixOffset)
        {
            if (_useTransparent)
            {
                byte ty = _transparent[1];
                var row = nrgba!.Pix.Span.Slice(pixOffset, width * 4);
                for (int x = 0; x < width; x++)
                {
                    int i = 4 * x;
                    byte ycol = cdat[x];
                    row[i] = ycol;
                    row[i + 1] = ycol;
                    row[i + 2] = ycol;
                    row[i + 3] = ycol == ty ? (byte)0 : (byte)0xff;
                }
                pixOffset += nrgba.Stride;
            }
            else
            {
                cdat.CopyTo(gray!.Pix.Span[pixOffset..]);
                pixOffset += gray.Stride;
            }
        }

        private static void DecodeGA8(ReadOnlySpan<byte> cdat, int width, NrgbaImage nrgba, ref int pixOffset)
        {
            var row = nrgba.Pix.Span.Slice(pixOffset, width * 4);
            for (int x = 0; x < width; x++)
            {
                int i = 4 * x;
                byte ycol = cdat[2 * x];
                row[i] = ycol;
                row[i + 1] = ycol;
                row[i + 2] = ycol;
                row[i + 3] = cdat[2 * x + 1];
            }
            pixOffset += nrgba.Stride;
        }

        private void DecodeTC8(ReadOnlySpan<byte> cdat, int width, RgbaImage? rgba, NrgbaImage? nrgba, ref int pixOffset)
        {
            if (_useTransparent)
            {
                byte tr = _transparent[1], tg = _transparent[3], tb = _transparent[5];
                var pix = nrgba!.Pix.Span;
                int i = pixOffset, j = 0;
                for (int x = 0; x < width; x++)
                {
                    byte r = cdat[j], g = cdat[j + 1], b = cdat[j + 2];
                    byte a = r == tr && g == tg && b == tb ? (byte)0 : (byte)0xff;
                    pix[i] = r; pix[i + 1] = g; pix[i + 2] = b; pix[i + 3] = a;
                    i += 4; j += 3;
                }
                pixOffset += nrgba.Stride;
            }
            else
            {
                var pix = rgba!.Pix.Span;
                int i = pixOffset, j = 0;
                for (int x = 0; x < width; x++)
                {
                    pix[i] = cdat[j]; pix[i + 1] = cdat[j + 1]; pix[i + 2] = cdat[j + 2]; pix[i + 3] = 0xff;
                    i += 4; j += 3;
                }
                pixOffset += rgba.Stride;
            }
        }

        private static void DecodeP1(ReadOnlySpan<byte> cdat, int width, PalettedImage paletted, ref int pixOffset)
        {
            var row = paletted.Pix.Span.Slice(pixOffset, width);
            int di = 0;
            for (int x = 0; x < width; x += 8)
            {
                byte b = cdat[x / 8];
                for (int x2 = 0; x2 < 8 && x + x2 < width; x2++)
                {
                    byte idx = (byte)(b >> 7);
                    EnsurePaletteSize(paletted, idx + 1);
                    row[di++] = idx;
                    b <<= 1;
                }
            }
            pixOffset += paletted.Stride;
        }

        private static void DecodeP2(ReadOnlySpan<byte> cdat, int width, PalettedImage paletted, ref int pixOffset)
        {
            var row = paletted.Pix.Span.Slice(pixOffset, width);
            int di = 0;
            for (int x = 0; x < width; x += 4)
            {
                byte b = cdat[x / 4];
                for (int x2 = 0; x2 < 4 && x + x2 < width; x2++)
                {
                    byte idx = (byte)(b >> 6);
                    EnsurePaletteSize(paletted, idx + 1);
                    row[di++] = idx;
                    b <<= 2;
                }
            }
            pixOffset += paletted.Stride;
        }

        private static void DecodeP4(ReadOnlySpan<byte> cdat, int width, PalettedImage paletted, ref int pixOffset)
        {
            var row = paletted.Pix.Span.Slice(pixOffset, width);
            int di = 0;
            for (int x = 0; x < width; x += 2)
            {
                byte b = cdat[x / 2];
                for (int x2 = 0; x2 < 2 && x + x2 < width; x2++)
                {
                    byte idx = (byte)(b >> 4);
                    EnsurePaletteSize(paletted, idx + 1);
                    row[di++] = idx;
                    b <<= 4;
                }
            }
            pixOffset += paletted.Stride;
        }

        private static void DecodeP8(ReadOnlySpan<byte> cdat, int width, PalettedImage paletted, ref int pixOffset)
        {
            if (paletted.Palette.Length != 256)
            {
                for (int x = 0; x < width; x++)
                    EnsurePaletteSize(paletted, cdat[x] + 1);
            }
            cdat.CopyTo(paletted.Pix.Span[pixOffset..]);
            pixOffset += paletted.Stride;
        }

        private static void DecodeTCA8(ReadOnlySpan<byte> cdat, ref int pixOffset, NrgbaImage nrgba)
        {
            cdat.CopyTo(nrgba.Pix.Span[pixOffset..]);
            pixOffset += nrgba.Stride;
        }

        private void DecodeG16(ReadOnlySpan<byte> cdat, int width, Gray16Image? gray16, Nrgba64Image? nrgba64, ref int pixOffset)
        {
            if (_useTransparent)
            {
                ushort ty = (ushort)(_transparent[0] << 8 | _transparent[1]);
                var row = nrgba64!.Pix.Span.Slice(pixOffset, width * 8);
                for (int x = 0; x < width; x++)
                {
                    int i = 8 * x;
                    ushort ycol = (ushort)(cdat[2 * x] << 8 | cdat[2 * x + 1]);
                    ushort acol = ycol == ty ? (ushort)0 : (ushort)0xffff;
                    row[i] = (byte)(ycol >> 8);
                    row[i + 1] = (byte)ycol;
                    row[i + 2] = (byte)(ycol >> 8);
                    row[i + 3] = (byte)ycol;
                    row[i + 4] = (byte)(ycol >> 8);
                    row[i + 5] = (byte)ycol;
                    row[i + 6] = (byte)(acol >> 8);
                    row[i + 7] = (byte)acol;
                }
                pixOffset += nrgba64.Stride;
            }
            else
            {
                cdat.CopyTo(gray16!.Pix.Span.Slice(pixOffset, width * 2));
                pixOffset += gray16.Stride;
            }
        }

        private static void DecodeGA16(ReadOnlySpan<byte> cdat, int width, Nrgba64Image nrgba64, ref int pixOffset)
        {
            var row = nrgba64.Pix.Span.Slice(pixOffset, width * 8);
            for (int x = 0; x < width; x++)
            {
                int i = 8 * x;
                int j = 4 * x;
                row[i] = cdat[j];
                row[i + 1] = cdat[j + 1];
                row[i + 2] = cdat[j];
                row[i + 3] = cdat[j + 1];
                row[i + 4] = cdat[j];
                row[i + 5] = cdat[j + 1];
                row[i + 6] = cdat[j + 2];
                row[i + 7] = cdat[j + 3];
            }
            pixOffset += nrgba64.Stride;
        }

        private void DecodeTC16(ReadOnlySpan<byte> cdat, int width, Rgba64Image? rgba64, Nrgba64Image? nrgba64, ref int pixOffset)
        {
            if (_useTransparent)
            {
                ushort tr = (ushort)(_transparent[0] << 8 | _transparent[1]);
                ushort tg = (ushort)(_transparent[2] << 8 | _transparent[3]);
                ushort tb = (ushort)(_transparent[4] << 8 | _transparent[5]);
                var row = nrgba64!.Pix.Span.Slice(pixOffset, width * 8);
                for (int x = 0; x < width; x++)
                {
                    int i = 8 * x;
                    int j = 6 * x;
                    ushort rcol = (ushort)(cdat[j] << 8 | cdat[j + 1]);
                    ushort gcol = (ushort)(cdat[j + 2] << 8 | cdat[j + 3]);
                    ushort bcol = (ushort)(cdat[j + 4] << 8 | cdat[j + 5]);
                    ushort acol = rcol == tr && gcol == tg && bcol == tb ? (ushort)0 : (ushort)0xffff;
                    row[i] = cdat[j];
                    row[i + 1] = cdat[j + 1];
                    row[i + 2] = cdat[j + 2];
                    row[i + 3] = cdat[j + 3];
                    row[i + 4] = cdat[j + 4];
                    row[i + 5] = cdat[j + 5];
                    row[i + 6] = (byte)(acol >> 8);
                    row[i + 7] = (byte)acol;
                }
                pixOffset += nrgba64.Stride;
            }
            else
            {
                var row = rgba64!.Pix.Span.Slice(pixOffset, width * 8);
                for (int x = 0; x < width; x++)
                {
                    int i = 8 * x;
                    int j = 6 * x;
                    row[i] = cdat[j];
                    row[i + 1] = cdat[j + 1];
                    row[i + 2] = cdat[j + 2];
                    row[i + 3] = cdat[j + 3];
                    row[i + 4] = cdat[j + 4];
                    row[i + 5] = cdat[j + 5];
                    row[i + 6] = 0xff;
                    row[i + 7] = 0xff;
                }
                pixOffset += rgba64.Stride;
            }
        }

        private static void DecodeTCA16(ReadOnlySpan<byte> cdat, int width, Nrgba64Image nrgba64, ref int pixOffset)
        {
            cdat.CopyTo(nrgba64.Pix.Span.Slice(pixOffset, width * 8));
            pixOffset += nrgba64.Stride;
        }

        private static void EnsurePaletteSize(PalettedImage img, int size)
        {
            if (img.Palette.Length >= size) return;
            var colors = new IColor[size];
            for (int i = 0; i < img.Palette.Length; i++)
                colors[i] = img.Palette[i];
            for (int i = img.Palette.Length; i < size; i++)
                colors[i] = new Rgba(0, 0, 0, 0xff);
            img.Palette = new Palette(colors);
        }

        private static void MergePassInto(IImage dst, IImage src, int pass)
        {
            var p = Interlacing[pass];
            switch (dst)
            {
                case AlphaImage alpha:
                    MergePass(alpha.Pix.Span, alpha.Stride, alpha.Rect, src, p, 1);
                    break;
                case Alpha16Image alpha16:
                    MergePass(alpha16.Pix.Span, alpha16.Stride, alpha16.Rect, src, p, 2);
                    break;
                case GrayImage gray:
                    MergePass(gray.Pix.Span, gray.Stride, gray.Rect, src, p, 1);
                    break;
                case Gray16Image gray16:
                    MergePass(gray16.Pix.Span, gray16.Stride, gray16.Rect, src, p, 2);
                    break;
                case NrgbaImage nrgba:
                    MergePass(nrgba.Pix.Span, nrgba.Stride, nrgba.Rect, src, p, 4);
                    break;
                case Nrgba64Image nrgba64:
                    MergePass(nrgba64.Pix.Span, nrgba64.Stride, nrgba64.Rect, src, p, 8);
                    break;
                case PalettedImage paletted:
                {
                    if (src is PalettedImage srcPal && srcPal.Palette.Length > paletted.Palette.Length)
                        paletted.Palette = srcPal.Palette;
                    MergePass(paletted.Pix.Span, paletted.Stride, paletted.Rect, src, p, 1);
                    break;
                }
                case RgbaImage rgba:
                    MergePass(rgba.Pix.Span, rgba.Stride, rgba.Rect, src, p, 4);
                    break;
                case Rgba64Image rgba64:
                    MergePass(rgba64.Pix.Span, rgba64.Stride, rgba64.Rect, src, p, 8);
                    break;
            }
        }

        private static void MergePass(Span<byte> dstPix, int stride, Rect rect, IImage src, (int XFactor, int YFactor, int XOffset, int YOffset) p, int bytesPerPixel)
        {
            ReadOnlySpan<byte> srcPix = src switch
            {
                AlphaImage a => a.Pix.Span,
                Alpha16Image a => a.Pix.Span,
                GrayImage g => g.Pix.Span,
                Gray16Image g => g.Pix.Span,
                NrgbaImage n => n.Pix.Span,
                Nrgba64Image n => n.Pix.Span,
                PalettedImage pl => pl.Pix.Span,
                RgbaImage r => r.Pix.Span,
                Rgba64Image r => r.Pix.Span,
                _ => ReadOnlySpan<byte>.Empty,
            };
            var bounds = src.Bounds();
            int s = 0;
            for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
            {
                int dBase = (y * p.YFactor + p.YOffset - rect.Min.Y) * stride + (p.XOffset - rect.Min.X) * bytesPerPixel;
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                {
                    int d = dBase + x * p.XFactor * bytesPerPixel;
                    srcPix.Slice(s, bytesPerPixel).CopyTo(dstPix[d..]);
                    s += bytesPerPixel;
                }
            }
        }

        private void ParseIDAT(uint length)
        {
            _idatLength = length;
            _img = DecodeImage();
            VerifyChecksum();
        }

        private void ParseIEND(uint length)
        {
            if (length != 0) throw new PngFormatException("bad IEND length");
            VerifyChecksum();
        }

        private void ParseChunk(bool configOnly)
        {
            Span<byte> hdr = stackalloc byte[8];
            ReadExactly(_r, hdr);
            uint length = BinaryPrimitives.ReadUInt32BigEndian(hdr);
            _crc.Reset();
            _crc.Write(hdr[4..8]);
            uint chunkType = BinaryPrimitives.ReadUInt32BigEndian(hdr[4..8]);

            switch (chunkType)
            {
                case ChunkIHDR:
                    if (_stage != DsStart) throw new PngFormatException("chunk out of order");
                    _stage = DsSeenIHDR;
                    ParseIHDR(length);
                    return;
                case ChunkPLTE:
                    if (_stage != DsSeenIHDR) throw new PngFormatException("chunk out of order");
                    _stage = DsSeenPLTE;
                    ParsePLTE(length);
                    return;
                case ChunkTRNS:
                    if (CbPaletted(_cb))
                    {
                        if (_stage != DsSeenPLTE) throw new PngFormatException("chunk out of order");
                    }
                    else if (CbTrueColor(_cb))
                    {
                        if (_stage != DsSeenIHDR && _stage != DsSeenPLTE)
                            throw new PngFormatException("chunk out of order");
                    }
                    else if (_stage != DsSeenIHDR)
                    {
                        throw new PngFormatException("chunk out of order");
                    }
                    _stage = DsSeentRNS;
                    ParseTRNS(length);
                    return;
                case ChunkIDAT:
                    if (_stage < DsSeenIHDR || _stage > DsSeenIDAT ||
                        (_stage == DsSeenIHDR && CbPaletted(_cb)))
                        throw new PngFormatException("chunk out of order");
                    if (_stage == DsSeenIDAT) break;
                    _stage = DsSeenIDAT;
                    if (configOnly) return;
                    ParseIDAT(length);
                    return;
                case ChunkIEND:
                    if (_stage != DsSeenIDAT) throw new PngFormatException("chunk out of order");
                    _stage = DsSeenIEND;
                    ParseIEND(length);
                    return;
            }

            if (length > 0x7fffffff)
                throw new PngFormatException($"Bad chunk length: {length}");
            Span<byte> ignored = stackalloc byte[4096];
            while (length > 0)
            {
                int n = (int)Math.Min(ignored.Length, length);
                ReadExactly(_r, ignored[..n]);
                _crc.Write(ignored[..n]);
                length -= (uint)n;
            }
            VerifyChecksum();
        }

        private void VerifyChecksum()
        {
            Span<byte> crcBytes = stackalloc byte[4];
            ReadExactly(_r, crcBytes);
            if (BinaryPrimitives.ReadUInt32BigEndian(crcBytes) != _crc.Sum32())
                throw new PngFormatException("invalid checksum");
        }

        private static void ReadExactly(Stream s, Span<byte> buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int n = s.Read(buffer[offset..]);
                if (n == 0)
                    throw new EndOfStreamException();
                offset += n;
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
