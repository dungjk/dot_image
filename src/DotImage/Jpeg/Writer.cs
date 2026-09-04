// Ported from Go src/image/jpeg/writer.go


using DotImage.Color;

namespace DotImage.Jpeg;

public sealed class JpegOptions
{
    public int Quality { get; set; } = JpegWriter.DefaultQuality;
}

public static class JpegWriter
{
    public const int DefaultQuality = 75;

    private enum QuantIndex
    {
        Luminance,
        Chrominance,
        NQuantIndex,
    }

    private enum HuffIndex
    {
        LuminanceDC,
        LuminanceAC,
        ChrominanceDC,
        ChrominanceAC,
        NHuffIndex,
    }

    private static readonly byte[][] UnscaledQuant =
    [
        [
            16, 11, 12, 14, 12, 10, 16, 14,
            13, 14, 18, 17, 16, 19, 24, 40,
            26, 24, 22, 22, 24, 49, 35, 37,
            29, 40, 58, 51, 61, 60, 57, 51,
            56, 55, 64, 72, 92, 78, 64, 68,
            87, 69, 55, 56, 80, 109, 81, 87,
            95, 98, 103, 104, 103, 62, 77, 113,
            121, 112, 100, 120, 92, 101, 103, 99,
        ],
        [
            17, 18, 18, 24, 21, 24, 47, 26,
            26, 47, 99, 66, 56, 66, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
        ],
    ];

    private sealed class HuffmanSpec
    {
        public byte[] Count = new byte[16];
        public byte[] Value = [];
    }

    private static readonly HuffmanSpec[] TheHuffmanSpec =
    [
        new()
        {
            Count = [0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0],
            Value = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
        },
        new()
        {
            Count = [0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 125],
            Value =
            [
                0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12,
                0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
                0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08,
                0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0,
                0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16,
                0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28,
                0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
                0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
                0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
                0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
                0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79,
                0x7a, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
                0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
                0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7,
                0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6,
                0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5,
                0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4,
                0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
                0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea,
                0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
                0xf9, 0xfa,
            ],
        },
        new()
        {
            Count = [0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0],
            Value = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
        },
        new()
        {
            Count = [0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 119],
            Value =
            [
                0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21,
                0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
                0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91,
                0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0,
                0x15, 0x62, 0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34,
                0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26,
                0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37, 0x38,
                0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
                0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
                0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
                0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
                0x79, 0x7a, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
                0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96,
                0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5,
                0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4,
                0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3,
                0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2,
                0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
                0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9,
                0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
                0xf9, 0xfa,
            ],
        },
    ];

    private static readonly uint[][] TheHuffmanLUT = InitHuffmanLUT();

    private static uint[][] InitHuffmanLUT()
    {
        var luts = new uint[(int)HuffIndex.NHuffIndex][];
        for (int i = 0; i < (int)HuffIndex.NHuffIndex; i++)
            luts[i] = InitHuffmanLUT(TheHuffmanSpec[i]);
        return luts;
    }

    private static uint[] InitHuffmanLUT(HuffmanSpec s)
    {
        int maxValue = 0;
        foreach (byte v in s.Value)
        {
            if (v > maxValue)
                maxValue = v;
        }
        var lut = new uint[maxValue + 1];
        uint code = 0;
        int k = 0;
        for (int i = 0; i < s.Count.Length; i++)
        {
            uint nBits = (uint)(i + 1) << 24;
            for (int j = 0; j < s.Count[i]; j++)
            {
                lut[s.Value[k]] = nBits | code;
                code++;
                k++;
            }
            code <<= 1;
        }
        return lut;
    }

    private static readonly byte[] BitCount =
    [
        0, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4,
        5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
    ];

    private static readonly byte[] SosHeaderY =
    [
        0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00,
    ];

    private static readonly byte[] SosHeaderYCbCr =
    [
        0xff, 0xda, 0x00, 0x0c, 0x03, 0x01, 0x00, 0x02,
        0x11, 0x03, 0x11, 0x00, 0x3f, 0x00,
    ];

    private sealed class Encoder
    {
        public Stream W = Stream.Null;
        public Exception? Err;
        public readonly byte[] Buf = new byte[16];
        public uint Bits, NBits;
        public readonly byte[][] Quant = new byte[(int)QuantIndex.NQuantIndex][];

        public Encoder()
        {
            for (int i = 0; i < (int)QuantIndex.NQuantIndex; i++)
                Quant[i] = new byte[JpegConstants.BlockSize];
        }

        public void Write(ReadOnlySpan<byte> p)
        {
            if (Err != null) return;
            try { W.Write(p); }
            catch (Exception ex) { Err = ex; }
        }

        public void WriteByte(byte b)
        {
            if (Err != null) return;
            try { W.WriteByte(b); }
            catch (Exception ex) { Err = ex; }
        }

        public void Emit(uint bits, uint nBits)
        {
            nBits += NBits;
            bits <<= (int)(32 - nBits);
            bits |= Bits;
            while (nBits >= 8)
            {
                byte b = (byte)(bits >> 24);
                WriteByte(b);
                if (b == 0xff)
                    WriteByte(0x00);
                bits <<= 8;
                nBits -= 8;
            }
            Bits = bits;
            NBits = nBits;
        }

        public void EmitHuff(HuffIndex h, int value)
        {
            uint x = TheHuffmanLUT[(int)h][value];
            Emit(x & ((1u << 24) - 1), x >> 24);
        }

        public void EmitHuffRLE(HuffIndex h, int runLength, int value)
        {
            int a = value, b = value;
            if (a < 0)
            {
                a = -value;
                b = value - 1;
            }
            uint nBits;
            if (a < 0x100)
                nBits = BitCount[a];
            else
                nBits = (uint)(8 + BitCount[a >> 8]);
            EmitHuff(h, runLength << 4 | (int)nBits);
            if (nBits > 0)
                Emit((uint)b & ((1u << (int)nBits) - 1), nBits);
        }

        public void WriteMarkerHeader(byte marker, int markerLen)
        {
            Buf[0] = 0xff;
            Buf[1] = marker;
            Buf[2] = (byte)(markerLen >> 8);
            Buf[3] = (byte)(markerLen & 0xff);
            Write(Buf.AsSpan(0, 4));
        }

        public void WriteDQT()
        {
            const int markerLen = 2 + (int)QuantIndex.NQuantIndex * (1 + JpegConstants.BlockSize);
            WriteMarkerHeader(JpegConstants.DqtMarker, markerLen);
            for (int i = 0; i < (int)QuantIndex.NQuantIndex; i++)
            {
                WriteByte((byte)i);
                Write(Quant[i]);
            }
        }

        public void WriteSOF0(Point size, int nComponent)
        {
            int markerLen = 8 + 3 * nComponent;
            WriteMarkerHeader(JpegConstants.Sof0Marker, markerLen);
            Buf[0] = 8;
            Buf[1] = (byte)(size.Y >> 8);
            Buf[2] = (byte)(size.Y & 0xff);
            Buf[3] = (byte)(size.X >> 8);
            Buf[4] = (byte)(size.X & 0xff);
            Buf[5] = (byte)nComponent;
            if (nComponent == 1)
            {
                Buf[6] = 1;
                Buf[7] = 0x11;
                Buf[8] = 0x00;
                Write(Buf.AsSpan(0, 9));
            }
            else
            {
                ReadOnlySpan<byte> hv = "\x22\x11\x11"u8;
                ReadOnlySpan<byte> tq = "\x00\x01\x01"u8;
                for (int i = 0; i < nComponent; i++)
                {
                    Buf[3 * i + 6] = (byte)(i + 1);
                    Buf[3 * i + 7] = hv[i];
                    Buf[3 * i + 8] = tq[i];
                }
                Write(Buf.AsSpan(0, 3 * (nComponent - 1) + 9));
            }
        }

        public void WriteDHT(int nComponent)
        {
            int markerLen = 2;
            var specs = TheHuffmanSpec.AsSpan();
            if (nComponent == 1)
                specs = specs[..2];
            foreach (var s in specs)
                markerLen += 1 + 16 + s.Value.Length;
            WriteMarkerHeader(JpegConstants.DhtMarker, markerLen);
            ReadOnlySpan<byte> ids = "\x00\x10\x01\x11"u8;
            for (int i = 0; i < specs.Length; i++)
            {
                WriteByte(ids[i]);
                Write(specs[i].Count);
                Write(specs[i].Value);
            }
        }

        public int WriteBlock(ref Block b, QuantIndex q, int prevDC)
        {
            Dct.Fdct(ref b);
            int dc = Div(b[0], 8 * Quant[(int)q][0]);
            EmitHuffRLE((HuffIndex)((int)q * 2), 0, dc - prevDC);
            var h = (HuffIndex)((int)q * 2 + 1);
            int runLength = 0;
            for (int zig = 1; zig < JpegConstants.BlockSize; zig++)
            {
                int ac = Div(b[JpegConstants.Unzig[zig]], 8 * Quant[(int)q][zig]);
                if (ac == 0)
                {
                    runLength++;
                }
                else
                {
                    while (runLength > 15)
                    {
                        EmitHuff(h, 0xf0);
                        runLength -= 16;
                    }
                    EmitHuffRLE(h, runLength, ac);
                    runLength = 0;
                }
            }
            if (runLength > 0)
                EmitHuff(h, 0x00);
            return dc;
        }

        public void WriteSOS(IImage m)
        {
            if (m is GrayImage)
                Write(SosHeaderY);
            else
                Write(SosHeaderYCbCr);

        var b = new Block();
        var cb = new[] { new Block(), new Block(), new Block(), new Block() };
        var cr = new[] { new Block(), new Block(), new Block(), new Block() };
            int prevDCY = 0, prevDCCb = 0, prevDCCr = 0;
            var bounds = m.Bounds();
            if (m is GrayImage gray)
            {
                for (int y = bounds.Min.Y; y < bounds.Max.Y; y += 8)
                {
                    for (int x = bounds.Min.X; x < bounds.Max.X; x += 8)
                    {
                        GrayToY(gray, new Point(x, y), ref b);
                        prevDCY = WriteBlock(ref b, QuantIndex.Luminance, prevDCY);
                    }
                }
            }
            else
            {
                var rgba = m as RgbaImage;
                var ycbcr = m as YCbCrImage;
                for (int y = bounds.Min.Y; y < bounds.Max.Y; y += 16)
                {
                    for (int x = bounds.Min.X; x < bounds.Max.X; x += 16)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            int xOff = (i & 1) * 8;
                            int yOff = (i & 2) * 4;
                            var p = new Point(x + xOff, y + yOff);
                            if (rgba != null)
                                RgbaToYCbCr(rgba, p, ref b, ref cb[i], ref cr[i]);
                            else if (ycbcr != null)
                                YCbCrToYCbCr(ycbcr, p, ref b, ref cb[i], ref cr[i]);
                            else
                                ToYCbCr(m, p, ref b, ref cb[i], ref cr[i]);
                            prevDCY = WriteBlock(ref b, QuantIndex.Luminance, prevDCY);
                        }
                        Scale(ref b, cb);
                        prevDCCb = WriteBlock(ref b, QuantIndex.Chrominance, prevDCCb);
                        Scale(ref b, cr);
                        prevDCCr = WriteBlock(ref b, QuantIndex.Chrominance, prevDCCr);
                    }
                }
            }
            Emit(0x7f, 7);
        }
    }

    private static int Div(int a, int b)
    {
        if (a >= 0)
            return (a + (b >> 1)) / b;
        return -((-a + (b >> 1)) / b);
    }

    private static void ToYCbCr(IImage m, Point p, ref Block yBlock, ref Block cbBlock, ref Block crBlock)
    {
        var b = m.Bounds();
        int xmax = b.Max.X - 1;
        int ymax = b.Max.Y - 1;
        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                var c = m.At(Math.Min(p.X + i, xmax), Math.Min(p.Y + j, ymax));
                (uint r, uint g, uint b1, _) = c.Rgba();
                var (yy, cb, cr) = YCbCrUtil.RGBToYCbCr((byte)(r >> 8), (byte)(g >> 8), (byte)(b1 >> 8));
                yBlock[8 * j + i] = yy;
                cbBlock[8 * j + i] = cb;
                crBlock[8 * j + i] = cr;
            }
        }
    }

    private static void GrayToY(GrayImage m, Point p, ref Block yBlock)
    {
        var b = m.Bounds();
        int xmax = b.Max.X - 1;
        int ymax = b.Max.Y - 1;
        var pix = m.Pix.Span;
        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                int idx = m.PixOffset(Math.Min(p.X + i, xmax), Math.Min(p.Y + j, ymax));
                yBlock[8 * j + i] = pix[idx];
            }
        }
    }

    private static void RgbaToYCbCr(RgbaImage m, Point p, ref Block yBlock, ref Block cbBlock, ref Block crBlock)
    {
        var b = m.Bounds();
        int xmax = b.Max.X - 1;
        int ymax = b.Max.Y - 1;
        for (int j = 0; j < 8; j++)
        {
            int sj = p.Y + j;
            if (sj > ymax) sj = ymax;
            int offset = (sj - b.Min.Y) * m.Stride - b.Min.X * 4;
            for (int i = 0; i < 8; i++)
            {
                int sx = p.X + i;
                if (sx > xmax) sx = xmax;
                var pix = m.Pix.Span.Slice(offset + sx * 4, 4);
                var (yy, cb, cr) = YCbCrUtil.RGBToYCbCr(pix[0], pix[1], pix[2]);
                yBlock[8 * j + i] = yy;
                cbBlock[8 * j + i] = cb;
                crBlock[8 * j + i] = cr;
            }
        }
    }

    private static void YCbCrToYCbCr(YCbCrImage m, Point p, ref Block yBlock, ref Block cbBlock, ref Block crBlock)
    {
        var b = m.Bounds();
        int xmax = b.Max.X - 1;
        int ymax = b.Max.Y - 1;
        for (int j = 0; j < 8; j++)
        {
            int sy = p.Y + j;
            if (sy > ymax) sy = ymax;
            for (int i = 0; i < 8; i++)
            {
                int sx = p.X + i;
                if (sx > xmax) sx = xmax;
                int yi = m.YOffset(sx, sy);
                int ci = m.COffset(sx, sy);
                yBlock[8 * j + i] = m.Y.Span[yi];
                cbBlock[8 * j + i] = m.Cb.Span[ci];
                crBlock[8 * j + i] = m.Cr.Span[ci];
            }
        }
    }

    private static void Scale(ref Block dst, Block[] src)
    {
        for (int i = 0; i < 4; i++)
        {
            int dstOff = (i & 2) << 4 | (i & 1) << 2;
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int j = 16 * y + 2 * x;
                    int sum = src[i][j] + src[i][j + 1] + src[i][j + 8] + src[i][j + 9];
                    dst[8 * y + x + dstOff] = (sum + 2) >> 2;
                }
            }
        }
    }

    public static void Encode(Stream w, IImage m, JpegOptions? o = null)
    {
        var b = m.Bounds();
        if (b.Dx() >= 1 << 16 || b.Dy() >= 1 << 16)
            throw new InvalidOperationException("jpeg: image is too large to encode");

        var e = new Encoder { W = w };
        int quality = o?.Quality ?? DefaultQuality;
        if (quality < 1) quality = 1;
        else if (quality > 100) quality = 100;

        int scale = quality < 50 ? 5000 / quality : 200 - quality * 2;
        for (int i = 0; i < (int)QuantIndex.NQuantIndex; i++)
        {
            for (int j = 0; j < JpegConstants.BlockSize; j++)
            {
                int x = UnscaledQuant[i][j];
                x = (x * scale + 50) / 100;
                if (x < 1) x = 1;
                else if (x > 255) x = 255;
                e.Quant[i][j] = (byte)x;
            }
        }

        int nComponent = m is GrayImage ? 1 : 3;
        e.Write([0xff, JpegConstants.SoiMarker]);
        e.WriteDQT();
        e.WriteSOF0(b.Size(), nComponent);
        e.WriteDHT(nComponent);
        e.WriteSOS(m);
        e.Write([0xff, JpegConstants.EoiMarker]);
        if (e.Err != null)
            throw e.Err;
    }
}
