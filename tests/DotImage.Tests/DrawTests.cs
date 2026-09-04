// Ported from Go src/image/draw/draw_test.go

using DotImage.Draw;

namespace DotImage.Tests;

public class DrawTests
{
    private sealed class SlowestRgba : IWritableImage, IRgba64Image
    {
        public Memory<byte> Pix = Memory<byte>.Empty;
        public int Stride;
        public Rect Rect;

        public IColorModel ColorModel() => Models.Rgba;
        public Rect Bounds() => Rect;
        public IColor At(int x, int y) => Rgba64At(x, y);

        public Rgba64 Rgba64At(int x, int y)
        {
            if (!new Point(x, y).In(Rect)) return new Rgba64(0, 0, 0, 0);
            int i = PixOffset(x, y);
            var s = Pix.Span.Slice(i, 4);
            ushort r = s[0], g = s[1], b = s[2], a = s[3];
            return new Rgba64((ushort)((r << 8) | r), (ushort)((g << 8) | g), (ushort)((b << 8) | b), (ushort)((a << 8) | a));
        }

        public void Set(int x, int y, IColor c)
        {
            if (!new Point(x, y).In(Rect)) return;
            int i = PixOffset(x, y);
            var c1 = (Rgba)Models.Rgba.Convert(c);
            var s = Pix.Span.Slice(i, 4);
            s[0] = c1.R;
            s[1] = c1.G;
            s[2] = c1.B;
            s[3] = c1.A;
        }

        public int PixOffset(int x, int y) =>
            (y - Rect.Min.Y) * Stride + (x - Rect.Min.X) * 4;

        public IImage SubImage(Rect r)
        {
            r = r.Intersect(Rect);
            if (r.Empty()) return new SlowestRgba();
            int i = PixOffset(r.Min.X, r.Min.Y);
            return new SlowestRgba { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
        }

        public bool Opaque() => true;
    }

    private sealed class SlowerRgba : IWritableImage, IRgba64Image, ISetRgba64Image
    {
        public Memory<byte> Pix = Memory<byte>.Empty;
        public int Stride;
        public Rect Rect;

        public IColorModel ColorModel() => Models.Rgba;
        public Rect Bounds() => Rect;
        public IColor At(int x, int y) => Rgba64At(x, y);

        public Rgba64 Rgba64At(int x, int y)
        {
            if (!new Point(x, y).In(Rect)) return new Rgba64(0, 0, 0, 0);
            int i = PixOffset(x, y);
            var s = Pix.Span.Slice(i, 4);
            ushort r = s[0], g = s[1], b = s[2], a = s[3];
            return new Rgba64((ushort)((r << 8) | r), (ushort)((g << 8) | g), (ushort)((b << 8) | b), (ushort)((a << 8) | a));
        }

        public void Set(int x, int y, IColor c)
        {
            if (!new Point(x, y).In(Rect)) return;
            int i = PixOffset(x, y);
            var c1 = (Rgba)Models.Rgba.Convert(c);
            var s = Pix.Span.Slice(i, 4);
            s[0] = c1.R;
            s[1] = c1.G;
            s[2] = c1.B;
            s[3] = c1.A;
        }

        public void SetRgba64(int x, int y, Rgba64 c)
        {
            if (!new Point(x, y).In(Rect)) return;
            int i = PixOffset(x, y);
            var s = Pix.Span.Slice(i, 4);
            s[0] = (byte)(c.R >> 8);
            s[1] = (byte)(c.G >> 8);
            s[2] = (byte)(c.B >> 8);
            s[3] = (byte)(c.A >> 8);
        }

        public int PixOffset(int x, int y) =>
            (y - Rect.Min.Y) * Stride + (x - Rect.Min.X) * 4;

        public IImage SubImage(Rect r)
        {
            r = r.Intersect(Rect);
            if (r.Empty()) return new SlowerRgba();
            int i = PixOffset(r.Min.X, r.Min.Y);
            return new SlowerRgba { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
        }

        public bool Opaque() => true;
    }

    private sealed class EmbeddedPaletted(IWritableImage inner) : IWritableImage, IRgba64Image
    {
        public IColorModel ColorModel() => inner.ColorModel();
        public Rect Bounds() => inner.Bounds();
        public IColor At(int x, int y) => inner.At(x, y);
        public Rgba64 Rgba64At(int x, int y) => ((IRgba64Image)inner).Rgba64At(x, y);
        public void Set(int x, int y, IColor c) => inner.Set(x, y, c);
        public IImage SubImage(Rect r) => inner.SubImage(r);
        public bool Opaque() => inner.Opaque();
    }

    private static bool Eq(IColor c0, IColor c1)
    {
        var (r0, g0, b0, a0) = c0.Rgba();
        var (r1, g1, b1, a1) = c1.Rgba();
        return r0 == r1 && g0 == g1 && b0 == b1 && a0 == a1;
    }

    private static IImage FillBlue(int alpha) =>
        UniformImages.NewUniform(new Rgba(0, 0, (byte)alpha, (byte)alpha));

    private static IImage FillAlpha(int alpha) =>
        UniformImages.NewUniform(new Alpha((byte)alpha));

    private static IImage VgradGreen(int alpha)
    {
        var m = Images.NewRgba(Geometry.Rect(0, 0, 16, 16));
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                m.Set(x, y, new Rgba(0, (byte)(y * alpha / 15), 0, (byte)alpha));
        return m;
    }

    private static IImage VgradAlpha(int alpha)
    {
        var m = Images.NewAlpha(Geometry.Rect(0, 0, 16, 16));
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                m.Set(x, y, new Alpha((byte)(y * alpha / 15)));
        return m;
    }

    private static IImage VgradGreenNrgba(int alpha)
    {
        var m = Images.NewNrgba(Geometry.Rect(0, 0, 16, 16));
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                m.Set(x, y, new Rgba(0, (byte)(y * 0x11), 0, (byte)alpha));
        return m;
    }

    private static IImage VgradCr()
    {
        var m = YCbCrImages.NewYCbCr(Geometry.Rect(0, 0, 16, 16), YCbCrSubsampleRatio.Ratio444);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                m.Cr.Span[y * m.CStride + x] = (byte)(y * 0x11);
        return m;
    }

    private static IImage VgradGray()
    {
        var m = Images.NewGray(Geometry.Rect(0, 0, 16, 16));
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                m.Set(x, y, new Gray((byte)(y * 0x11)));
        return m;
    }

    private static IImage VgradMagenta()
    {
        var m = Images.NewCmyk(Geometry.Rect(0, 0, 16, 16));
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                m.Set(x, y, new Cmyk(0, (byte)(y * 0x11), 0, 0x3f));
        return m;
    }

    private static IWritableImage HgradRed(int alpha)
    {
        var m = Images.NewRgba(Geometry.Rect(0, 0, 16, 16));
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                m.Set(x, y, new Rgba((byte)(x * alpha / 15), 0, 0, (byte)alpha));
        return m;
    }

    private static IWritableImage GradYellow(int alpha)
    {
        var m = Images.NewRgba(Geometry.Rect(0, 0, 16, 16));
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                m.Set(x, y, new Rgba((byte)(x * alpha / 15), (byte)(y * alpha / 15), 0, (byte)alpha));
        return m;
    }

    private static SlowestRgba ConvertToSlowestRgba(IImage m)
    {
        if (m is RgbaImage rgba)
            return new SlowestRgba
            {
                Pix = rgba.Pix.ToArray(),
                Stride = rgba.Stride,
                Rect = rgba.Rect,
            };
        var copy = Images.NewRgba(m.Bounds());
        DrawOps.Draw(copy, copy.Bounds(), m, m.Bounds().Min, Op.Src);
        return new SlowestRgba { Pix = copy.Pix, Stride = copy.Stride, Rect = copy.Rect };
    }

    private static SlowerRgba ConvertToSlowerRgba(IImage m)
    {
        if (m is RgbaImage rgba)
            return new SlowerRgba
            {
                Pix = rgba.Pix.ToArray(),
                Stride = rgba.Stride,
                Rect = rgba.Rect,
            };
        var copy = Images.NewRgba(m.Bounds());
        DrawOps.Draw(copy, copy.Bounds(), m, m.Bounds().Min, Op.Src);
        return new SlowerRgba { Pix = copy.Pix, Stride = copy.Stride, Rect = copy.Rect };
    }

    private sealed record DrawTestCase(string Desc, IImage Src, IImage? Mask, Op Op, IColor Expected);

    private static readonly DrawTestCase[] DrawTestCases =
    [
        new("nop", VgradGreen(255), FillAlpha(0), Op.Over, new Rgba(136, 0, 0, 255)),
        new("clear", VgradGreen(255), FillAlpha(0), Op.Src, new Rgba(0, 0, 0, 0)),
        new("fill", FillBlue(90), FillAlpha(255), Op.Over, new Rgba(88, 0, 90, 255)),
        new("fillSrc", FillBlue(90), FillAlpha(255), Op.Src, new Rgba(0, 0, 90, 90)),
        new("fillAlpha", FillBlue(90), FillAlpha(192), Op.Over, new Rgba(100, 0, 68, 255)),
        new("fillAlphaSrc", FillBlue(90), FillAlpha(192), Op.Src, new Rgba(0, 0, 68, 68)),
        new("fillNil", FillBlue(90), null, Op.Over, new Rgba(88, 0, 90, 255)),
        new("fillNilSrc", FillBlue(90), null, Op.Src, new Rgba(0, 0, 90, 90)),
        new("copy", VgradGreen(90), FillAlpha(255), Op.Over, new Rgba(88, 48, 0, 255)),
        new("copySrc", VgradGreen(90), FillAlpha(255), Op.Src, new Rgba(0, 48, 0, 90)),
        new("copyAlpha", VgradGreen(90), FillAlpha(192), Op.Over, new Rgba(100, 36, 0, 255)),
        new("copyAlphaSrc", VgradGreen(90), FillAlpha(192), Op.Src, new Rgba(0, 36, 0, 68)),
        new("copyNil", VgradGreen(90), null, Op.Over, new Rgba(88, 48, 0, 255)),
        new("copyNilSrc", VgradGreen(90), null, Op.Src, new Rgba(0, 48, 0, 90)),
        new("nrgba", VgradGreenNrgba(90), FillAlpha(255), Op.Over, new Rgba(88, 46, 0, 255)),
        new("nrgbaSrc", VgradGreenNrgba(90), FillAlpha(255), Op.Src, new Rgba(0, 46, 0, 90)),
        new("nrgbaAlpha", VgradGreenNrgba(90), FillAlpha(192), Op.Over, new Rgba(100, 34, 0, 255)),
        new("nrgbaAlphaSrc", VgradGreenNrgba(90), FillAlpha(192), Op.Src, new Rgba(0, 34, 0, 68)),
        new("nrgbaNil", VgradGreenNrgba(90), null, Op.Over, new Rgba(88, 46, 0, 255)),
        new("nrgbaNilSrc", VgradGreenNrgba(90), null, Op.Src, new Rgba(0, 46, 0, 90)),
        new("ycbcr", VgradCr(), FillAlpha(255), Op.Over, new Rgba(11, 38, 0, 255)),
        new("ycbcrSrc", VgradCr(), FillAlpha(255), Op.Src, new Rgba(11, 38, 0, 255)),
        new("ycbcrAlpha", VgradCr(), FillAlpha(192), Op.Over, new Rgba(42, 28, 0, 255)),
        new("ycbcrAlphaSrc", VgradCr(), FillAlpha(192), Op.Src, new Rgba(8, 28, 0, 192)),
        new("ycbcrNil", VgradCr(), null, Op.Over, new Rgba(11, 38, 0, 255)),
        new("ycbcrNilSrc", VgradCr(), null, Op.Src, new Rgba(11, 38, 0, 255)),
        new("gray", VgradGray(), FillAlpha(255), Op.Over, new Rgba(136, 136, 136, 255)),
        new("graySrc", VgradGray(), FillAlpha(255), Op.Src, new Rgba(136, 136, 136, 255)),
        new("grayAlpha", VgradGray(), FillAlpha(192), Op.Over, new Rgba(136, 102, 102, 255)),
        new("grayAlphaSrc", VgradGray(), FillAlpha(192), Op.Src, new Rgba(102, 102, 102, 192)),
        new("grayNil", VgradGray(), null, Op.Over, new Rgba(136, 136, 136, 255)),
        new("grayNilSrc", VgradGray(), null, Op.Src, new Rgba(136, 136, 136, 255)),
        new("graySlower", ConvertToSlowerRgba(VgradGray()), FillAlpha(255), Op.Over, new Rgba(136, 136, 136, 255)),
        new("graySrcSlower", ConvertToSlowerRgba(VgradGray()), FillAlpha(255), Op.Src, new Rgba(136, 136, 136, 255)),
        new("grayAlphaSlower", ConvertToSlowerRgba(VgradGray()), FillAlpha(192), Op.Over, new Rgba(136, 102, 102, 255)),
        new("grayAlphaSrcSlower", ConvertToSlowerRgba(VgradGray()), FillAlpha(192), Op.Src, new Rgba(102, 102, 102, 192)),
        new("grayNilSlower", ConvertToSlowerRgba(VgradGray()), null, Op.Over, new Rgba(136, 136, 136, 255)),
        new("grayNilSrcSlower", ConvertToSlowerRgba(VgradGray()), null, Op.Src, new Rgba(136, 136, 136, 255)),
        new("graySlowest", ConvertToSlowestRgba(VgradGray()), FillAlpha(255), Op.Over, new Rgba(136, 136, 136, 255)),
        new("graySrcSlowest", ConvertToSlowestRgba(VgradGray()), FillAlpha(255), Op.Src, new Rgba(136, 136, 136, 255)),
        new("grayAlphaSlowest", ConvertToSlowestRgba(VgradGray()), FillAlpha(192), Op.Over, new Rgba(136, 102, 102, 255)),
        new("grayAlphaSrcSlowest", ConvertToSlowestRgba(VgradGray()), FillAlpha(192), Op.Src, new Rgba(102, 102, 102, 192)),
        new("grayNilSlowest", ConvertToSlowestRgba(VgradGray()), null, Op.Over, new Rgba(136, 136, 136, 255)),
        new("grayNilSrcSlowest", ConvertToSlowestRgba(VgradGray()), null, Op.Src, new Rgba(136, 136, 136, 255)),
        new("cmyk", VgradMagenta(), FillAlpha(255), Op.Over, new Rgba(192, 89, 192, 255)),
        new("cmykSrc", VgradMagenta(), FillAlpha(255), Op.Src, new Rgba(192, 89, 192, 255)),
        new("cmykAlpha", VgradMagenta(), FillAlpha(192), Op.Over, new Rgba(178, 67, 145, 255)),
        new("cmykAlphaSrc", VgradMagenta(), FillAlpha(192), Op.Src, new Rgba(145, 67, 145, 192)),
        new("cmykNil", VgradMagenta(), null, Op.Over, new Rgba(192, 89, 192, 255)),
        new("cmykNilSrc", VgradMagenta(), null, Op.Src, new Rgba(192, 89, 192, 255)),
        new("generic", FillBlue(255), VgradAlpha(192), Op.Over, new Rgba(81, 0, 102, 255)),
        new("genericSrc", FillBlue(255), VgradAlpha(192), Op.Src, new Rgba(0, 0, 102, 102)),
        new("genericSlower", FillBlue(255), ConvertToSlowerRgba(VgradAlpha(192)), Op.Over, new Rgba(81, 0, 102, 255)),
        new("genericSrcSlower", FillBlue(255), ConvertToSlowerRgba(VgradAlpha(192)), Op.Src, new Rgba(0, 0, 102, 102)),
        new("genericSlowest", FillBlue(255), ConvertToSlowestRgba(VgradAlpha(192)), Op.Over, new Rgba(81, 0, 102, 255)),
        new("genericSrcSlowest", FillBlue(255), ConvertToSlowestRgba(VgradAlpha(192)), Op.Src, new Rgba(0, 0, 102, 102)),
        new("rgbaVariableMaskOver", VgradGreen(90), VgradAlpha(192), Op.Over, new Rgba(117, 19, 0, 255)),
        new("grayVariableMaskOver", VgradGray(), VgradAlpha(192), Op.Over, new Rgba(136, 54, 54, 255)),
    ];

    private static IImage MakeGolden(
        IImage dst, Rect r, IImage src, Point sp, IImage? mask, Point mp, Op op)
    {
        Rect b = dst.Bounds();
        Rect sbounds = src.Bounds();
        Rect mb = new(new Point(-1_000_000_000, -1_000_000_000), new Point(1_000_000_000, 1_000_000_000));
        if (mask != null) mb = mask.Bounds();
        var golden = Images.NewRgba(Geometry.Rect(0, 0, b.Max.X, b.Max.Y));
        const uint M = 0xffff;
        for (int y = r.Min.Y; y < r.Max.Y; y++)
        {
            int sy = y + sp.Y - r.Min.Y;
            int my = y + mp.Y - r.Min.Y;
            for (int x = r.Min.X; x < r.Max.X; x++)
            {
                if (!new Point(x, y).In(b)) continue;
                int sx = x + sp.X - r.Min.X;
                if (!new Point(sx, sy).In(sbounds)) continue;
                int mx = x + mp.X - r.Min.X;
                if (!new Point(mx, my).In(mb)) continue;

                uint dr = 0, dg = 0, db = 0, da = 0;
                if (op == Op.Over)
                    (dr, dg, db, da) = dst.At(x, y).Rgba();
                var (sr, sg, sbl, sa) = src.At(sx, sy).Rgba();
                uint ma = M;
                if (mask != null)
                    (_, _, _, ma) = mask.At(mx, my).Rgba();
                uint a = M - (sa * ma / M);
                golden.Set(x, y, new Rgba64(
                    (ushort)((dr * a + sr * ma) / M),
                    (ushort)((dg * a + sg * ma) / M),
                    (ushort)((db * a + sbl * ma) / M),
                    (ushort)((da * a + sa * ma) / M)));
            }
        }
        return golden.SubImage(b);
    }

    [Fact]
    public void Draw()
    {
        Rect[] rects =
        [
            Geometry.Rect(0, 0, 0, 0),
            Geometry.Rect(0, 0, 16, 16),
            Geometry.Rect(3, 5, 12, 10),
            Geometry.Rect(0, 0, 9, 9),
            Geometry.Rect(8, 8, 16, 16),
            Geometry.Rect(8, 0, 9, 16),
            Geometry.Rect(0, 8, 16, 9),
            Geometry.Rect(8, 8, 9, 9),
            Geometry.Rect(8, 8, 8, 8),
        ];
        foreach (Rect r in rects)
        {
            foreach (DrawTestCase test in DrawTestCases)
            {
                for (int i = 0; i < 3; i++)
                {
                    IWritableImage dst = (IWritableImage)HgradRed(255).SubImage(r);
                    switch (i)
                    {
                        case 1:
                            dst = ConvertToSlowerRgba(dst);
                            break;
                        case 2:
                            dst = ConvertToSlowestRgba(dst);
                            break;
                    }

                    IImage golden = MakeGolden(dst, Geometry.Rect(0, 0, 16, 16), test.Src, default, test.Mask, default, test.Op);
                    Rect b = dst.Bounds();
                    Assert.True(b.Eq(golden.Bounds()),
                        $"draw {r} {test.Desc} on {dst.GetType().Name}: bounds {b} versus {golden.Bounds()}");

                    DrawOps.DrawMask(dst, Geometry.Rect(0, 0, 16, 16), test.Src, default, test.Mask, default, test.Op);

                    if (new Point(8, 8).In(r))
                    {
                        Assert.True(Eq(dst.At(8, 8), test.Expected),
                            $"draw {r} {test.Desc} on {dst.GetType().Name}: at (8, 8) {dst.At(8, 8)} versus {test.Expected}");
                    }

                    bool mismatch = false;
                    for (int y = b.Min.Y; y < b.Max.Y && !mismatch; y++)
                    {
                        for (int x = b.Min.X; x < b.Max.X; x++)
                        {
                            if (!Eq(dst.At(x, y), golden.At(x, y)))
                            {
                                Assert.Fail($"draw {r} {test.Desc} on {dst.GetType().Name}: at ({x}, {y}), {dst.At(x, y)} versus golden {golden.At(x, y)}");
                                mismatch = true;
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void DrawOverlap()
    {
        foreach (Op op in new[] { Op.Over, Op.Src })
        {
            for (int yoff = -2; yoff <= 2; yoff++)
            {
                for (int xoff = -2; xoff <= 2; xoff++)
                {
                    var m = (RgbaImage)GradYellow(127);
                    var dst = (RgbaImage)m.SubImage(Geometry.Rect(5, 5, 10, 10));
                    var src = (RgbaImage)m.SubImage(Geometry.Rect(5 + xoff, 5 + yoff, 10 + xoff, 10 + yoff));
                    Rect b = dst.Bounds();
                    IImage golden = MakeGolden(dst, b, src, src.Bounds().Min, null, default, op);
                    Assert.True(b.Eq(golden.Bounds()),
                        $"drawOverlap xoff={xoff},yoff={yoff}: bounds {b} versus {golden.Bounds()}");
                    DrawOps.DrawMask(dst, b, src, src.Bounds().Min, null, default, op);
                    for (int y = b.Min.Y; y < b.Max.Y; y++)
                    {
                        for (int x = b.Min.X; x < b.Max.X; x++)
                        {
                            Assert.True(Eq(dst.At(x, y), golden.At(x, y)),
                                $"drawOverlap xoff={xoff},yoff={yoff}: at ({x}, {y}), {dst.At(x, y)} versus golden {golden.At(x, y)}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void NonZeroSrcPt()
    {
        var a = Images.NewRgba(Geometry.Rect(0, 0, 1, 1));
        var b = Images.NewRgba(Geometry.Rect(0, 0, 2, 2));
        b.Set(0, 0, new Rgba(0, 0, 0, 5));
        b.Set(1, 0, new Rgba(0, 0, 5, 5));
        b.Set(0, 1, new Rgba(0, 5, 0, 5));
        b.Set(1, 1, new Rgba(5, 0, 0, 5));
        DrawOps.Draw(a, Geometry.Rect(0, 0, 1, 1), b, Geometry.Pt(1, 1), Op.Over);
        Assert.True(Eq(new Rgba(5, 0, 0, 5), a.At(0, 0)),
            $"non-zero src pt: want {new Rgba(5, 0, 0, 5)} got {a.At(0, 0)}");
    }

    [Fact]
    public void Fill()
    {
        Rect[] rects =
        [
            Geometry.Rect(0, 0, 0, 0),
            Geometry.Rect(0, 0, 40, 30),
            Geometry.Rect(10, 0, 40, 30),
            Geometry.Rect(0, 20, 40, 30),
            Geometry.Rect(10, 20, 40, 30),
            Geometry.Rect(10, 20, 15, 25),
            Geometry.Rect(10, 0, 35, 30),
            Geometry.Rect(0, 15, 40, 16),
            Geometry.Rect(24, 24, 25, 25),
            Geometry.Rect(23, 23, 26, 26),
            Geometry.Rect(22, 22, 27, 27),
            Geometry.Rect(21, 21, 28, 28),
            Geometry.Rect(20, 20, 29, 29),
        ];
        foreach (Rect r in rects)
        {
            var m = (RgbaImage)Images.NewRgba(Geometry.Rect(0, 0, 40, 30)).SubImage(r);
            Rect b = m.Bounds();
            void Check(string desc, IColor c)
            {
                for (int y = b.Min.Y; y < b.Max.Y; y++)
                {
                    for (int x = b.Min.X; x < b.Max.X; x++)
                    {
                        if (!Eq(c, m.At(x, y)))
                        {
                            Assert.Fail($"{desc} fill: at ({x}, {y}), sub-image bounds={r}: want {c} got {m.At(x, y)}");
                            return;
                        }
                    }
                }
            }

            var c = new Rgba(11, 0, 0, 255);
            var src = UniformImages.NewUniform(c);
            for (int y = b.Min.Y; y < b.Max.Y; y++)
                for (int x = b.Min.X; x < b.Max.X; x++)
                    DrawOps.DrawMask(m, Geometry.Rect(x, y, x + 1, y + 1), src, default, null, default, Op.Src);
            Check("pixel", c);

            c = new Rgba(0, 22, 0, 255);
            src = UniformImages.NewUniform(c);
            for (int y = b.Min.Y; y < b.Max.Y; y++)
                DrawOps.DrawMask(m, Geometry.Rect(b.Min.X, y, b.Max.X, y + 1), src, default, null, default, Op.Src);
            Check("row", c);

            c = new Rgba(0, 0, 33, 255);
            src = UniformImages.NewUniform(c);
            for (int x = b.Min.X; x < b.Max.X; x++)
                DrawOps.DrawMask(m, Geometry.Rect(x, b.Min.Y, x + 1, b.Max.Y), src, default, null, default, Op.Src);
            Check("column", c);

            c = new Rgba(44, 55, 66, 77);
            src = UniformImages.NewUniform(c);
            DrawOps.DrawMask(m, b, src, default, null, default, Op.Src);
            Check("whole", c);
        }
    }

    [Fact]
    public void DrawSrcNonpremultiplied()
    {
        var opaqueGray = new Nrgba(0x99, 0x99, 0x99, 0xff);
        var transparentBlue = new Nrgba(0x00, 0x00, 0xff, 0x00);
        var transparentGreen = new Nrgba(0x00, 0xff, 0x00, 0x00);
        var transparentRed = new Nrgba(0xff, 0x00, 0x00, 0x00);
        var opaqueGray64 = new Nrgba64(0x9999, 0x9999, 0x9999, 0xffff);
        var transparentPurple64 = new Nrgba64(0xfedc, 0x0000, 0x7654, 0x0000);

        {
            var dst = Images.NewNrgba(Geometry.Rect(0, 10, 3, 11));
            dst.SetNrgba(0, 10, opaqueGray);
            var src = Images.NewNrgba(Geometry.Rect(1, 20, 4, 21));
            src.SetNrgba(1, 20, transparentBlue);
            src.SetNrgba(2, 20, transparentGreen);
            src.SetNrgba(3, 20, transparentRed);
            DrawOps.Draw(dst, Geometry.Rect(1, 10, 3, 11), src, Geometry.Pt(1, 20), Op.Src);
            Assert.True(Eq(opaqueGray, dst.At(0, 10)), $"At(0, 10): got {dst.At(0, 10)} want {opaqueGray}");
            Assert.True(Eq(transparentBlue, dst.At(1, 10)), $"At(1, 10): got {dst.At(1, 10)} want {transparentBlue}");
            Assert.True(Eq(transparentGreen, dst.At(2, 10)), $"At(2, 10): got {dst.At(2, 10)} want {transparentGreen}");
        }

        {
            var dst = Images.NewNrgba64(Geometry.Rect(0, 0, 1, 1));
            dst.SetNrgba64(0, 0, opaqueGray64);
            var src = Images.NewNrgba64(Geometry.Rect(0, 0, 1, 1));
            src.SetNrgba64(0, 0, transparentPurple64);
            DrawOps.Draw(dst, dst.Bounds(), src, default, Op.Src);
            Assert.True(Eq(transparentPurple64, dst.At(0, 0)), $"At(0, 0): got {dst.At(0, 0)} want {transparentPurple64}");
        }
    }

    [Fact]
    public void FloydSteinbergCheckerboard()
    {
        Rect b = Geometry.Rect(0, 0, 640, 480);
        var src = UniformImages.NewUniform(new Gray16(0x7fff));
        var dst = Images.NewPaletted(b, new Palette([Colors.Black, Colors.White]));
        DrawOps.FloydSteinberg.Draw(dst, b, src, default);
        int nErr = 0;
        for (int y = b.Min.Y; y < b.Max.Y; y++)
        {
            for (int x = b.Min.X; x < b.Max.X; x++)
            {
                byte got = dst.Pix.Span[dst.PixOffset(x, y)];
                byte want = (byte)((x + y) % 2);
                if (got != want)
                {
                    Assert.Fail($"at ({x}, {y}): got {got}, want {want}");
                    if (++nErr == 10)
                        throw new Exception("there may be more errors");
                }
            }
        }
    }

    private static IImage MakeSampleImage(Rect b)
    {
        var m = Images.NewRgba(b);
        for (int y = b.Min.Y; y < b.Max.Y; y++)
            for (int x = b.Min.X; x < b.Max.X; x++)
                m.Set(x, y, new Rgba((byte)x, (byte)y, (byte)((x + y) / 2), 255));
        return m;
    }

    [Fact]
    public void Paletted()
    {
        Rect bounds = Geometry.Rect(0, 0, 64, 48);
        IImage video001 = MakeSampleImage(bounds);
        var cgaPalette = new Palette([
            new Rgba(0x00, 0x00, 0x00, 0xff),
            new Rgba(0x55, 0xff, 0xff, 0xff),
            new Rgba(0xff, 0x55, 0xff, 0xff),
            new Rgba(0xff, 0xff, 0xff, 0xff),
        ]);
        var drawers = new Dictionary<string, IDrawer>
        {
            ["src"] = new OpDrawer(Op.Src),
            ["floyd-steinberg"] = DrawOps.FloydSteinberg,
        };
        var sources = new Dictionary<string, IImage>
        {
            ["uniform"] = UniformImages.NewUniform(new Rgba(0xff, 0x7f, 0xff, 0xff)),
            ["video001"] = video001,
        };

        foreach (var (dName, drawer) in drawers)
        {
            foreach (var (sName, src) in sources)
            {
                var dst0 = Images.NewPaletted(bounds, cgaPalette);
                var dst1 = Images.NewPaletted(bounds, cgaPalette);
                drawer.Draw(dst0, bounds, src, default);
                drawer.Draw(new EmbeddedPaletted(dst1), bounds, src, default);
                for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
                {
                    for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                    {
                        if (!Eq(dst0.At(x, y), dst1.At(x, y)))
                            Assert.Fail($"{dName} / {sName}: at ({x}, {y}), {dst0.At(x, y)} versus {dst1.At(x, y)}");
                    }
                }
            }
        }
    }

    [Fact]
    public void SqDiff()
    {
        uint Orig(int x, int y)
        {
            int d = x > y ? x - y : y - x;
            return (uint)(d * d) >> 2;
        }

        int[] testCases =
        [
            0, 1, 2, 0x0fffd, 0x0fffe, 0x0ffff, 0x10000, 0x10001, 0x10002,
            0x7ffffffd, 0x7ffffffe, 0x7fffffff,
            -0x7ffffffd, -0x7ffffffe, unchecked((int)0x80000000),
        ];
        foreach (int x in testCases)
        {
            foreach (int y in testCases)
            {
                Assert.Equal(Orig(x, y), ColorUtil.SqDiff((uint)x, (uint)y));
            }
        }
    }

    private sealed class OpDrawer(Op op) : IDrawer
    {
        public void Draw(IWritableImage dst, Rect r, IImage src, Point sp) =>
            DrawOps.DrawMask(dst, r, src, sp, null, default, op);
    }
}
