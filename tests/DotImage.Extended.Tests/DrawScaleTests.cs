// Ported from golang.org/x/image/draw/scale_test.go

using DotImage;
using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Draw;
using DotImage.Extended.Math.F64;
using Xunit;

namespace DotImage.Extended.Tests;

public class DrawScaleTests
{
    private static Point Pt(int x, int y) => Geometry.Pt(x, y);

    private static Rect R(int x0, int y0, int x1, int y1) => Geometry.Rect(x0, y0, x1, y1);

    private static Aff3 TransformMatrix(double scale, double tx, double ty)
    {
        const double cos30 = 0.866025404;
        const double sin30 = 0.5;
        return new Aff3(
            scale * cos30, -scale * sin30, tx,
            scale * sin30, scale * cos30, ty);
    }

    private static Aff3 MatMul(Aff3 p, Aff3 q) => new(
        p[0] * q[0] + p[1] * q[3],
        p[0] * q[1] + p[1] * q[4],
        p[0] * q[2] + p[1] * q[5] + p[2],
        p[3] * q[0] + p[4] * q[3],
        p[3] * q[1] + p[4] * q[4],
        p[3] * q[2] + p[4] * q[5] + p[5]);

    private static RgbaImage MakePattern(int w, int h, bool opaque)
    {
        var m = Images.NewRgba(R(0, 0, w, h));
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int a = opaque ? 0xff : 0x10 + (x * 3 + y * 5) % 0x80;
                m.SetRgba(x, y, new Rgba(
                    (byte)((x * 31 + y * 7) & 0xff),
                    (byte)((x * 13 + y * 29) & 0xff),
                    (byte)((x * 5 + y * 11) & 0xff),
                    (byte)a));
            }
        }
        return m;
    }

    private static Interpolator[] Kernels() =>
    [
        Interpolators.NearestNeighbor,
        Interpolators.ApproxBiLinear,
        Interpolators.CatmullRom,
    ];

    [Fact]
    public void TestOps()
    {
        IImage blue = UniformImages.NewUniform(new Rgba(0x00, 0x00, 0xff, 0xff));
        foreach (var (op, want) in new[]
                 {
                     (Op.Over, new Rgba(0x7f, 0x00, 0x80, 0xff)),
                     (Op.Src, new Rgba(0x7f, 0x00, 0x00, 0x7f)),
                 })
        {
            var dst = Images.NewRgba(R(0, 0, 2, 2));
            ScaleOps.Copy(dst, Pt(0, 0), blue, dst.Bounds(), Op.Src, null);

            var src = Images.NewRgba(R(0, 0, 1, 1));
            src.SetRgba(0, 0, new Rgba(0x7f, 0x00, 0x00, 0x7f));

            Interpolators.NearestNeighbor.Scale(dst, dst.Bounds(), src, src.Bounds(), op, null);

            Assert.Equal(want, dst.RgbaAt(0, 0));
        }
    }

    [Fact]
    public void TestNegativeWeights()
    {
        var src = Images.NewRgba(R(0, 0, 16, 16));
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                int a = y * 0x11;
                src.SetRgba(x, y, new Rgba((byte)(x * 0x11 * a / 0xff), 0, 0, (byte)a));
            }
        }
        CheckPremultiplied(src, "src image");

        var dst = Images.NewRgba(R(0, 0, 32, 32));
        Interpolators.CatmullRom.Scale(dst, dst.Bounds(), src, src.Bounds(), Op.Over, null);
        CheckPremultiplied(dst, "dst image");

        static void CheckPremultiplied(RgbaImage m, string name)
        {
            Rect b = m.Bounds();
            for (int y = b.Min.Y; y < b.Max.Y; y++)
            {
                for (int x = b.Min.X; x < b.Max.X; x++)
                {
                    var c = m.RgbaAt(x, y);
                    if (c.R > c.A || c.G > c.A || c.B > c.A)
                    {
                        Assert.Fail($"{name}: invalid color.RGBA at ({x}, {y}): ({c.R}, {c.G}, {c.B}, {c.A})");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestInterpClipCommute()
    {
        var src = Images.NewRgba(R(0, 0, 20, 20));
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                int v = (x * 31 + y * 57 + x * y * 13) & 0xff;
                src.SetRgba(x, y, new Rgba((byte)v, (byte)(v >> 1), (byte)(v >> 2), 0x7f));
            }
        }

        Rect outer = R(1, 1, 8, 5);
        Rect inner = R(2, 3, 6, 5);
        Interpolator[] qs = Kernels();
        foreach (bool transform in new[] { false, true })
        {
            foreach (var q in qs)
            {
                var dst0 = Images.NewRgba(R(1, 1, 10, 10));
                var dst1 = Images.NewRgba(R(1, 1, 10, 10));
                for (int i = 0; i < dst0.Pix.Span.Length; i++)
                {
                    dst0.Pix.Span[i] = (byte)(i / 4);
                    dst1.Pix.Span[i] = (byte)(i / 4);
                }

                void Interp(RgbaImage dst)
                {
                    if (transform)
                    {
                        q.Transform(dst, TransformMatrix(3.75, 2, 1), src, src.Bounds(), Op.Over, null);
                    }
                    else
                    {
                        q.Scale(dst, outer, src, src.Bounds(), Op.Over, null);
                    }
                }

                Interp(dst0);
                RgbaImage sub0 = (RgbaImage)dst0.SubImage(inner);

                RgbaImage sub1 = (RgbaImage)dst1.SubImage(inner);
                Interp(sub1);

                for (int y = inner.Min.Y; y < inner.Max.Y; y++)
                {
                    for (int x = inner.Min.X; x < inner.Max.X; x++)
                    {
                        Assert.Equal(sub1.RgbaAt(x, y), sub0.RgbaAt(x, y));
                    }
                }
            }
        }
    }

    // TranslatedImage is an image m translated by t.
    private sealed class TranslatedImage(IImage m, Point t) : IImage
    {
        public IColor At(int x, int y) => m.At(x - t.X, y - t.Y);
        public Rect Bounds() => m.Bounds().Add(t);
        public IColorModel ColorModel() => m.ColorModel();
    }

    [Fact]
    public void TestSrcTranslationInvariance()
    {
        var src = MakePattern(20, 20, opaque: false);
        Rect sr = R(2, 3, 16, 12);
        Assert.True(sr.In(src.Bounds()));

        Interpolator[] qs = Kernels();
        Point[] deltas =
        [
            Pt(+0, +0), Pt(+0, +5), Pt(+0, -5),
            Pt(+5, +0), Pt(-5, +0),
            Pt(+8, +8), Pt(+8, -8), Pt(-8, +8), Pt(-8, -8),
        ];
        Aff3 m00 = TransformMatrix(3.75, 0, 0);

        foreach (bool transform in new[] { false, true })
        {
            foreach (var q in qs)
            {
                var want = Images.NewRgba(R(0, 0, 20, 20));
                if (transform)
                {
                    q.Transform(want, m00, src, sr, Op.Over, null);
                }
                else
                {
                    q.Scale(want, want.Bounds(), src, sr, Op.Over, null);
                }

                foreach (var delta in deltas)
                {
                    var tsrc = new TranslatedImage(src, delta);
                    var got = Images.NewRgba(R(0, 0, 20, 20));
                    if (transform)
                    {
                        var m = MatMul(m00, new Aff3(1, 0, -delta.X, 0, 1, -delta.Y));
                        q.Transform(got, m, tsrc, sr.Add(delta), Op.Over, null);
                    }
                    else
                    {
                        q.Scale(got, got.Bounds(), tsrc, sr.Add(delta), Op.Over, null);
                    }
                    Assert.True(got.Pix.Span.SequenceEqual(want.Pix.Span),
                        $"pix differ for delta=({delta.X},{delta.Y}), transform={transform}, interp={q.GetType().Name}");
                }
            }
        }
    }

    [Fact]
    public void TestSrcMask()
    {
        var srcMask = Images.NewRgba(R(0, 0, 23, 1));
        srcMask.SetRgba(19, 0, new Rgba(0x00, 0x00, 0x00, 0x7f));
        srcMask.SetRgba(20, 0, new Rgba(0x00, 0x00, 0x00, 0xff));
        srcMask.SetRgba(21, 0, new Rgba(0x00, 0x00, 0x00, 0x3f));
        srcMask.SetRgba(22, 0, new Rgba(0x00, 0x00, 0x00, 0x00));
        IImage red = UniformImages.NewUniform(new Rgba(0xff, 0x00, 0x00, 0xff));
        IImage blue = UniformImages.NewUniform(new Rgba(0x00, 0x00, 0xff, 0xff));

        var dst = Images.NewRgba(R(0, 0, 6, 1));
        ScaleOps.Copy(dst, Pt(0, 0), blue, dst.Bounds(), Op.Src, null);
        Interpolators.NearestNeighbor.Scale(dst, dst.Bounds(), red, R(0, 0, 3, 1), Op.Over, new Options
        {
            SrcMask = srcMask,
            SrcMaskP = Pt(20, 0),
        });

        var got = new[]
        {
            dst.RgbaAt(0, 0), dst.RgbaAt(1, 0), dst.RgbaAt(2, 0),
            dst.RgbaAt(3, 0), dst.RgbaAt(4, 0), dst.RgbaAt(5, 0),
        };
        Rgba[] want =
        [
            new(0xff, 0x00, 0x00, 0xff),
            new(0xff, 0x00, 0x00, 0xff),
            new(0x3f, 0x00, 0xc0, 0xff),
            new(0x3f, 0x00, 0xc0, 0xff),
            new(0x00, 0x00, 0xff, 0xff),
            new(0x00, 0x00, 0xff, 0xff),
        ];
        Assert.Equal(want, got);
    }

    [Fact]
    public void TestDstMask()
    {
        var dstMask = Images.NewRgba(R(0, 0, 23, 1));
        dstMask.SetRgba(19, 0, new Rgba(0x00, 0x00, 0x00, 0x7f));
        dstMask.SetRgba(20, 0, new Rgba(0x00, 0x00, 0x00, 0xff));
        dstMask.SetRgba(21, 0, new Rgba(0x00, 0x00, 0x00, 0x3f));
        dstMask.SetRgba(22, 0, new Rgba(0x00, 0x00, 0x00, 0x00));

        var red = Images.NewRgba(R(0, 0, 1, 1));
        red.SetRgba(0, 0, new Rgba(0xff, 0x00, 0x00, 0xff));
        IImage blue = UniformImages.NewUniform(new Rgba(0x00, 0x00, 0xff, 0xff));

        foreach (var q in Kernels())
        {
            var dst = Images.NewRgba(R(0, 0, 3, 1));
            ScaleOps.Copy(dst, Pt(0, 0), blue, dst.Bounds(), Op.Src, null);
            q.Scale(dst, dst.Bounds(), red, red.Bounds(), Op.Over, new Options
            {
                DstMask = dstMask,
                DstMaskP = Pt(20, 0),
            });

            var got = new[] { dst.RgbaAt(0, 0), dst.RgbaAt(1, 0), dst.RgbaAt(2, 0) };
            Rgba[] want =
            [
                new(0xff, 0x00, 0x00, 0xff),
                new(0x3f, 0x00, 0xc0, 0xff),
                new(0x00, 0x00, 0xff, 0xff),
            ];
            Assert.Equal(want, got);
        }
    }

    private static void Clear(IWritableImage dst)
    {
        ScaleOps.Copy(dst, Pt(0, 0), UniformImages.Transparent, dst.Bounds(), Op.Src, null);
    }

    [Fact]
    public void TestSimpleTransforms()
    {
        var src = MakePattern(100, 100, opaque: true);

        foreach (string op in new[] { "scale/copy", "tform/copy", "tform/scale" })
        {
            foreach (double epsilon in new[] { 0.0, 1e-50, 1e-1 })
            {
                var dst0 = Images.NewRgba(R(0, 0, 120, 150));
                var dst1 = Images.NewRgba(R(0, 0, 120, 150));
                Clear(dst0);
                Clear(dst1);

                switch (op)
                {
                    case "scale/copy":
                    {
                        Rect dr = R(10, 30, 10 + 100, 30 + 100);
                        if (epsilon > 1e-10)
                        {
                            dr = new Rect(dr.Min, new Point(dr.Max.X + 1, dr.Max.Y));
                        }
                        ScaleOps.Copy(dst0, Pt(10, 30), src, src.Bounds(), Op.Src, null);
                        Interpolators.ApproxBiLinear.Scale(dst1, dr, src, src.Bounds(), Op.Src, null);
                        break;
                    }
                    case "tform/copy":
                        ScaleOps.Copy(dst0, Pt(10, 30), src, src.Bounds(), Op.Src, null);
                        Interpolators.ApproxBiLinear.Transform(dst1, new Aff3(1, 0 + epsilon, 10, 0, 1, 30), src, src.Bounds(), Op.Src, null);
                        break;
                    case "tform/scale":
                        Interpolators.ApproxBiLinear.Scale(dst0, R(10, 50, 10 + 50, 50 + 50), src, src.Bounds(), Op.Src, null);
                        Interpolators.ApproxBiLinear.Transform(dst1, new Aff3(0.5, 0.0 + epsilon, 10, 0.0, 0.5, 50), src, src.Bounds(), Op.Src, null);
                        break;
                }

                bool differ = !dst0.Pix.Span.SequenceEqual(dst1.Pix.Span);
                if (epsilon > 1e-10)
                {
                    Assert.True(differ, $"{op} yielded same pixels, want different pixels: epsilon={epsilon}");
                }
                else
                {
                    Assert.False(differ, $"{op} yielded different pixels, want same pixels: epsilon={epsilon}");
                }
            }
        }
    }

    private sealed class MaskWrap : IImage
    {
        private readonly IImage _m;
        public MaskWrap(IImage m) => _m = m;
        public IColor At(int x, int y) => _m.At(x, y);
        public Rect Bounds() => _m.Bounds();
        public IColorModel ColorModel() => _m.ColorModel();
    }

    [Fact]
    public void TestRectDstMask()
    {
        var src = MakePattern(100, 100, opaque: true);
        Aff3 m00 = TransformMatrix(1, 0, 0);

        Rect bounds = R(0, 0, 50, 50);
        var dstOutside = Images.NewRgba(bounds);
        for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
        {
            for (int x = bounds.Min.X; x < bounds.Max.X; x++)
            {
                dstOutside.SetRgba(x, y, new Rgba((byte)(5 * x), (byte)(5 * y), 0x00, 0xff));
            }
        }

        RgbaImage Mk(Transformer q, IImage? dstMask, Point dstMaskP)
        {
            var m = Images.NewRgba(bounds);
            ScaleOps.Copy(m, bounds.Min, dstOutside, bounds, Op.Src, null);
            q.Transform(m, m00, src, src.Bounds(), Op.Over, new Options { DstMask = dstMask, DstMaskP = dstMaskP });
            return m;
        }

        Interpolator[] qs = Kernels();
        Point[] dstMaskPs = [Pt(0, 0), Pt(5, 7), Pt(-3, 0)];
        Rect rect = R(10, 10, 30, 40);

        foreach (var q in qs)
        {
            foreach (var dstMaskP in dstMaskPs)
            {
                var dstInside = Mk(q, null, Pt(0, 0));
                foreach (bool wrap in new[] { false, true })
                {
                    IImage dstMask = rect;
                    if (wrap)
                    {
                        dstMask = new MaskWrap(dstMask);
                    }
                    var dst = Mk(q, dstMask, dstMaskP);

                    int nError = 0;
                    for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
                    {
                        for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                        {
                            var which = (new Point(x, y)).Add(dstMaskP).In(rect) ? dstInside : dstOutside;
                            var got = dst.RgbaAt(x, y);
                            var want = which.RgbaAt(x, y);
                            if (!got.Equals(want))
                            {
                                if (nError == 10)
                                {
                                    Assert.Fail($"q={q.GetType().Name} dmp={dstMaskP} wrap={wrap}: ...and more errors");
                                    return;
                                }
                                nError++;
                                Assert.True(false, $"q={q.GetType().Name} dmp={dstMaskP} wrap={wrap} dmp={dstMaskP}: x={x} y={y}: got ({got.R},{got.G},{got.B},{got.A}), want ({want.R},{want.G},{want.B},{want.A})");
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestDstMaskSameSizeCopy()
    {
        Rect bounds = R(0, 0, 42, 42);
        IImage src = UniformImages.Opaque;
        var dst = Images.NewRgba(bounds);
        var mask = Images.NewRgba(bounds);

        ScaleOps.Copy(dst, Pt(0, 0), src, bounds, Op.Src, new Options { DstMask = mask });
    }
}