// Ported from Go src/image/png/writer.go


using System.Buffers.Binary;
using System.IO.Compression;
using DotImage.Color;

namespace DotImage.Png;

public enum PngCompressionLevel
{
    DefaultCompression = 0,
    NoCompression = -1,
    BestSpeed = -2,
    BestCompression = -3,
}

public interface IPngEncoderBufferPool
{
    PngEncoderBuffer Get();
    void Put(PngEncoderBuffer buffer);
}

public sealed class PngEncoderBuffer;

public sealed class PngEncoder
{
    public PngCompressionLevel CompressionLevel { get; set; }
    public IPngEncoderBufferPool? BufferPool { get; set; }

    public void Encode(Stream w, IImage m) => PngWriter.Encode(w, m, this);
}

public static class PngWriter
{
    private const byte CtGrayscale = 0;
    private const byte CtTrueColor = 2;
    private const byte CtPaletted = 3;
    private const byte CtTrueColorAlpha = 6;

    private const int CbG8 = 4;
    private const int CbTC8 = 6;
    private const int CbP1 = 7;
    private const int CbP2 = 8;
    private const int CbP4 = 9;
    private const int CbP8 = 10;
    private const int CbTCA8 = 11;
    private const int CbG16 = 12;
    private const int CbTC16 = 14;
    private const int CbTCA16 = 15;

    private const int FtNone = 0;
    private const int FtSub = 1;
    private const int FtUp = 2;
    private const int FtAverage = 3;
    private const int FtPaeth = 4;
    private const int NFilter = 5;

    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    public static void Encode(Stream w, IImage m) => Encode(w, m, new PngEncoder());

    public static void Encode(Stream w, IImage m, PngEncoder enc)
    {
        var e = new EncoderState(enc);
        try
        {
            e.Encode(w, m);
        }
        finally
        {
            if (enc.BufferPool != null)
                enc.BufferPool.Put(e.ToBuffer());
        }
    }

    private sealed class EncoderState
    {
        private readonly PngEncoder _enc;
        private Stream _w = null!;
        private IImage _m = null!;
        private int _cb;
        private Exception? _err;
        private readonly byte[] _header = new byte[8];
        private readonly byte[] _footer = new byte[4];
        private readonly byte[] _tmp = new byte[4 * 256];
        private readonly byte[][] _cr = new byte[NFilter][];
        private byte[] _pr = [];
        private ZLibStream? _zw;
        private CompressionLevel _zwLevel = CompressionLevel.Optimal;

        public EncoderState(PngEncoder enc) => _enc = enc;

        public PngEncoderBuffer ToBuffer() => new();

        public void Encode(Stream w, IImage m)
        {
            _w = w;
            _m = m;
            var b = m.Bounds();
            long mw = b.Dx(), mh = b.Dy();
            if (mw <= 0 || mh <= 0 || mw >= 1L << 32 || mh >= 1L << 32)
                throw new PngFormatException($"invalid image size: {mw}x{mh}");

            Palette? pal = null;
            if (m is IPalettedImage && m.ColorModel() is Palette p)
                pal = p;
            if (pal != null)
            {
                _cb = pal.Length switch
                {
                    <= 2 => CbP1,
                    <= 4 => CbP2,
                    <= 16 => CbP4,
                    _ => CbP8,
                };
            }
            else
            {
                var cm = m.ColorModel();
                if (ReferenceEquals(cm, Models.Gray)) _cb = CbG8;
                else if (ReferenceEquals(cm, Models.Gray16)) _cb = CbG16;
                else if (ReferenceEquals(cm, Models.Rgba) || ReferenceEquals(cm, Models.Nrgba) || ReferenceEquals(cm, Models.Alpha))
                    _cb = m is IWritableImage wi && wi.Opaque() ? CbTC8 : CbTCA8;
                else
                    _cb = m is IWritableImage wi2 && wi2.Opaque() ? CbTC16 : CbTCA16;
            }

            _w.Write(PngHeader);
            WriteIHDR();
            if (pal != null) WritePLTEAndTRNS(pal);
            WriteIDATs();
            WriteIEND();
            if (_err != null) throw _err;
        }

        private void WriteChunk(ReadOnlySpan<byte> b, string name)
        {
            if (_err != null) return;
            uint n = (uint)b.Length;
            if ((int)n != b.Length)
            {
                _err = new PngUnsupportedException($"{name} chunk is too large: {b.Length}");
                return;
            }
            BinaryPrimitives.WriteUInt32BigEndian(_header.AsSpan(0, 4), n);
            _header[4] = (byte)name[0];
            _header[5] = (byte)name[1];
            _header[6] = (byte)name[2];
            _header[7] = (byte)name[3];
            var crc = new Crc32Hasher();
            crc.Write(_header.AsSpan(4, 4));
            crc.Write(b);
            BinaryPrimitives.WriteUInt32BigEndian(_footer.AsSpan(0, 4), crc.Sum32());
            try
            {
                _w.Write(_header);
                if (!b.IsEmpty) _w.Write(b);
                _w.Write(_footer);
            }
            catch (Exception ex) { _err = ex; }
        }

        private void WriteIHDR()
        {
            var b = _m.Bounds();
            BinaryPrimitives.WriteUInt32BigEndian(_tmp.AsSpan(0, 4), (uint)b.Dx());
            BinaryPrimitives.WriteUInt32BigEndian(_tmp.AsSpan(4, 4), (uint)b.Dy());
            switch (_cb)
            {
                case CbG8: _tmp[8] = 8; _tmp[9] = CtGrayscale; break;
                case CbTC8: _tmp[8] = 8; _tmp[9] = CtTrueColor; break;
                case CbP8: _tmp[8] = 8; _tmp[9] = CtPaletted; break;
                case CbP4: _tmp[8] = 4; _tmp[9] = CtPaletted; break;
                case CbP2: _tmp[8] = 2; _tmp[9] = CtPaletted; break;
                case CbP1: _tmp[8] = 1; _tmp[9] = CtPaletted; break;
                case CbTCA8: _tmp[8] = 8; _tmp[9] = CtTrueColorAlpha; break;
                case CbG16: _tmp[8] = 16; _tmp[9] = CtGrayscale; break;
                case CbTC16: _tmp[8] = 16; _tmp[9] = CtTrueColor; break;
                case CbTCA16: _tmp[8] = 16; _tmp[9] = CtTrueColorAlpha; break;
            }
            _tmp[10] = 0;
            _tmp[11] = 0;
            _tmp[12] = 0;
            WriteChunk(_tmp.AsSpan(0, 13), "IHDR");
        }

        private void WritePLTEAndTRNS(Palette p)
        {
            if (p.Length < 1 || p.Length > 256)
            {
                _err = new PngFormatException($"bad palette length: {p.Length}");
                return;
            }
            int last = -1;
            for (int i = 0; i < p.Length; i++)
            {
                var c1 = (Nrgba)Models.Nrgba.Convert(p[i]);
                _tmp[3 * i] = c1.R;
                _tmp[3 * i + 1] = c1.G;
                _tmp[3 * i + 2] = c1.B;
                if (c1.A != 0xff) last = i;
                _tmp[3 * 256 + i] = c1.A;
            }
            WriteChunk(_tmp.AsSpan(0, 3 * p.Length), "PLTE");
            if (last != -1)
                WriteChunk(_tmp.AsSpan(3 * 256, 1 + last), "tRNS");
        }

        private sealed class IdatWriter(EncoderState e) : Stream
        {
            public override void Write(byte[] buffer, int offset, int count) =>
                e.WriteIdatChunk(buffer.AsSpan(offset, count));

            public override void Write(ReadOnlySpan<byte> buffer) => e.WriteIdatChunk(buffer);
            public override bool CanWrite => true;
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override void Flush() { }
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }

        private void WriteIdatChunk(ReadOnlySpan<byte> b)
        {
            WriteChunk(b, "IDAT");
        }

        private static int Abs8(byte d) => d < 128 ? d : 256 - d;

        private static int ChooseFilter(byte[][] cr, byte[] pr, int bpp)
        {
            var cdat0 = cr[0].AsSpan(1);
            var cdat1 = cr[1].AsSpan(1);
            var cdat2 = cr[2].AsSpan(1);
            var cdat3 = cr[3].AsSpan(1);
            var cdat4 = cr[4].AsSpan(1);
            var pdat = pr.AsSpan(1);
            int n = cdat0.Length;

            int sum = 0;
            for (int i = 0; i < n; i++)
            {
                cdat2[i] = (byte)(cdat0[i] - pdat[i]);
                sum += Abs8(cdat2[i]);
            }
            int best = sum;
            int filter = FtUp;

            sum = 0;
            for (int i = 0; i < bpp; i++)
            {
                cdat4[i] = (byte)(cdat0[i] - pdat[i]);
                sum += Abs8(cdat4[i]);
            }
            for (int i = bpp; i < n; i++)
            {
                cdat4[i] = (byte)(cdat0[i] - Paeth.PaethPredictor(cdat0[i - bpp], pdat[i], pdat[i - bpp]));
                sum += Abs8(cdat4[i]);
                if (sum >= best) break;
            }
            if (sum < best) { best = sum; filter = FtPaeth; }

            sum = 0;
            for (int i = 0; i < n; i++)
            {
                sum += Abs8(cdat0[i]);
                if (sum >= best) break;
            }
            if (sum < best) { best = sum; filter = FtNone; }

            sum = 0;
            for (int i = 0; i < bpp; i++)
            {
                cdat1[i] = cdat0[i];
                sum += Abs8(cdat1[i]);
            }
            for (int i = bpp; i < n; i++)
            {
                cdat1[i] = (byte)(cdat0[i] - cdat0[i - bpp]);
                sum += Abs8(cdat1[i]);
                if (sum >= best) break;
            }
            if (sum < best) { best = sum; filter = FtSub; }

            sum = 0;
            for (int i = 0; i < bpp; i++)
            {
                cdat3[i] = (byte)(cdat0[i] - pdat[i] / 2);
                sum += Abs8(cdat3[i]);
            }
            for (int i = bpp; i < n; i++)
            {
                cdat3[i] = (byte)(cdat0[i] - (cdat0[i - bpp] + pdat[i]) / 2);
                sum += Abs8(cdat3[i]);
                if (sum >= best) break;
            }
            if (sum < best) filter = FtAverage;

            return filter;
        }

        private void WriteImage(Stream w, IImage m, int cb, CompressionLevel level)
        {
            if (_zw == null || _zwLevel != level)
            {
                _zw?.Dispose();
                _zw = new ZLibStream(w, level, leaveOpen: true);
                _zwLevel = level;
            }
            else
            {
                _zw.Dispose();
                _zw = new ZLibStream(w, level, leaveOpen: true);
            }

            using (_zw)
            {
                int bitsPerPixel = cb switch
                {
                    CbG8 => 8,
                    CbTC8 => 24,
                    CbP8 => 8,
                    CbP4 => 4,
                    CbP2 => 2,
                    CbP1 => 1,
                    CbTCA8 => 32,
                    CbTC16 => 48,
                    CbTCA16 => 64,
                    CbG16 => 16,
                    _ => 0,
                };

                var b = m.Bounds();
                int sz = 1 + (bitsPerPixel * b.Dx() + 7) / 8;
                for (int i = 0; i < NFilter; i++)
                {
                    if (_cr[i] == null || _cr[i].Length != sz)
                        _cr[i] = new byte[sz];
                    _cr[i][0] = (byte)i;
                }
                if (_pr == null || _pr.Length < sz) _pr = new byte[sz];
                else if (_pr.Length > sz) _pr = _pr[..sz];
                else Array.Clear(_pr);

                var gray = m as GrayImage;
                var rgba = m as RgbaImage;
                var paletted = m as PalettedImage;
                var nrgba = m as NrgbaImage;

                for (int y = b.Min.Y; y < b.Max.Y; y++)
                {
                    int i = 1;
                    switch (cb)
                    {
                        case CbG8:
                            if (gray != null)
                            {
                                int offset = (y - b.Min.Y) * gray.Stride;
                                gray.Pix.Span.Slice(offset, b.Dx()).CopyTo(_cr[0].AsSpan(1));
                            }
                            else
                            {
                                for (int x = b.Min.X; x < b.Max.X; x++)
                                {
                                    _cr[0][i++] = ((Gray)Models.Gray.Convert(m.At(x, y))).Y;
                                }
                            }
                            break;
                        case CbTC8:
                        {
                            var cr0 = _cr[0];
                            if (rgba != null)
                            {
                                int j0 = (y - b.Min.Y) * rgba.Stride;
                                int j1 = j0 + b.Dx() * 4;
                                for (int j = j0; j < j1; j += 4)
                                {
                                    cr0[i++] = rgba.Pix.Span[j];
                                    cr0[i++] = rgba.Pix.Span[j + 1];
                                    cr0[i++] = rgba.Pix.Span[j + 2];
                                }
                            }
                            else if (nrgba != null)
                            {
                                int j0 = (y - b.Min.Y) * nrgba.Stride;
                                int j1 = j0 + b.Dx() * 4;
                                for (int j = j0; j < j1; j += 4)
                                {
                                    cr0[i++] = nrgba.Pix.Span[j];
                                    cr0[i++] = nrgba.Pix.Span[j + 1];
                                    cr0[i++] = nrgba.Pix.Span[j + 2];
                                }
                            }
                            else
                            {
                                for (int x = b.Min.X; x < b.Max.X; x++)
                                {
                                    var (r, g, bl, _) = m.At(x, y).Rgba();
                                    cr0[i++] = (byte)(r >> 8);
                                    cr0[i++] = (byte)(g >> 8);
                                    cr0[i++] = (byte)(bl >> 8);
                                }
                            }
                            break;
                        }
                        case CbP8:
                            if (paletted != null)
                            {
                                int offset = (y - b.Min.Y) * paletted.Stride;
                                paletted.Pix.Span.Slice(offset, b.Dx()).CopyTo(_cr[0].AsSpan(1));
                            }
                            else if (m is IPalettedImage pi)
                            {
                                for (int x = b.Min.X; x < b.Max.X; x++)
                                    _cr[0][i++] = pi.ColorIndexAt(x, y);
                            }
                            break;
                        case CbP4 or CbP2 or CbP1:
                        {
                            var pi = (IPalettedImage)m;
                            int pixelsPerByte = 8 / bitsPerPixel;
                            byte a = 0;
                            int c = 0;
                            for (int x = b.Min.X; x < b.Max.X; x++)
                            {
                                a = (byte)((a << bitsPerPixel) | pi.ColorIndexAt(x, y));
                                c++;
                                if (c == pixelsPerByte)
                                {
                                    _cr[0][i++] = a;
                                    a = 0;
                                    c = 0;
                                }
                            }
                            if (c != 0)
                            {
                                while (c != pixelsPerByte)
                                {
                                    a = (byte)(a << bitsPerPixel);
                                    c++;
                                }
                                _cr[0][i++] = a;
                            }
                            break;
                        }
                        case CbTCA8:
                            if (nrgba != null)
                            {
                                int offset = (y - b.Min.Y) * nrgba.Stride;
                                nrgba.Pix.Span.Slice(offset, b.Dx() * 4).CopyTo(_cr[0].AsSpan(1));
                            }
                            else if (rgba != null)
                            {
                                var dst = _cr[0].AsSpan(1);
                                var src = rgba.Pix.Span[rgba.PixOffset(b.Min.X, y)..rgba.PixOffset(b.Max.X, y)];
                                for (int si = 0; si < src.Length; si += 4)
                                {
                                    byte sa = src[si + 3];
                                    if (sa == 0)
                                    {
                                        dst[0] = dst[1] = dst[2] = dst[3] = 0;
                                    }
                                    else if (sa == 0xff)
                                    {
                                        src.Slice(si, 4).CopyTo(dst);
                                    }
                                    else
                                    {
                                        const uint mul = 0x101 * 0xffff;
                                        uint a = (uint)sa * 0x101;
                                        dst[0] = (byte)((uint)src[si] * mul / a >> 8);
                                        dst[1] = (byte)((uint)src[si + 1] * mul / a >> 8);
                                        dst[2] = (byte)((uint)src[si + 2] * mul / a >> 8);
                                        dst[3] = sa;
                                    }
                                    dst = dst[4..];
                                }
                            }
                            else
                            {
                                for (int x = b.Min.X; x < b.Max.X; x++)
                                {
                                    var c = (Nrgba)Models.Nrgba.Convert(m.At(x, y));
                                    _cr[0][i++] = c.R;
                                    _cr[0][i++] = c.G;
                                    _cr[0][i++] = c.B;
                                    _cr[0][i++] = c.A;
                                }
                            }
                            break;
                        case CbG16:
                            for (int x = b.Min.X; x < b.Max.X; x++)
                            {
                                ushort yy = ((Gray16)Models.Gray16.Convert(m.At(x, y))).Y;
                                _cr[0][i++] = (byte)(yy >> 8);
                                _cr[0][i++] = (byte)yy;
                            }
                            break;
                        case CbTC16:
                            for (int x = b.Min.X; x < b.Max.X; x++)
                            {
                                var (r, g, bl, _) = m.At(x, y).Rgba();
                                _cr[0][i++] = (byte)(r >> 8);
                                _cr[0][i++] = (byte)r;
                                _cr[0][i++] = (byte)(g >> 8);
                                _cr[0][i++] = (byte)g;
                                _cr[0][i++] = (byte)(bl >> 8);
                                _cr[0][i++] = (byte)bl;
                            }
                            break;
                        case CbTCA16:
                            for (int x = b.Min.X; x < b.Max.X; x++)
                            {
                                var c = (Nrgba64)Models.Nrgba64.Convert(m.At(x, y));
                                _cr[0][i++] = (byte)(c.R >> 8);
                                _cr[0][i++] = (byte)c.R;
                                _cr[0][i++] = (byte)(c.G >> 8);
                                _cr[0][i++] = (byte)c.G;
                                _cr[0][i++] = (byte)(c.B >> 8);
                                _cr[0][i++] = (byte)c.B;
                                _cr[0][i++] = (byte)(c.A >> 8);
                                _cr[0][i++] = (byte)c.A;
                            }
                            break;
                    }

                    int f = FtNone;
                    var zlibLevel = LevelToZlib(_enc.CompressionLevel);
                    if (zlibLevel != CompressionLevel.NoCompression && cb != CbP8 && cb != CbP4 && cb != CbP2 && cb != CbP1)
                    {
                        int bpp = bitsPerPixel / 8;
                        f = ChooseFilter(_cr, _pr, bpp);
                    }
                    _zw.Write(_cr[f].AsSpan(0, sz));
                    (_pr, _cr[0]) = (_cr[0], _pr);
                }
            }
        }

        private void WriteIDATs()
        {
            if (_err != null) return;
            var idat = new IdatWriter(this);
            using var bw = new BufferedStream(idat, 1 << 15);
            try
            {
                WriteImage(bw, _m, _cb, LevelToZlib(_enc.CompressionLevel));
                bw.Flush();
            }
            catch (Exception ex) { _err = ex; }
        }

        private void WriteIEND() => WriteChunk(ReadOnlySpan<byte>.Empty, "IEND");

        private static CompressionLevel LevelToZlib(PngCompressionLevel l) => l switch
        {
            PngCompressionLevel.DefaultCompression => CompressionLevel.Optimal,
            PngCompressionLevel.NoCompression => CompressionLevel.NoCompression,
            PngCompressionLevel.BestSpeed => CompressionLevel.Fastest,
            PngCompressionLevel.BestCompression => CompressionLevel.SmallestSize,
            _ => CompressionLevel.Optimal,
        };
    }
}
