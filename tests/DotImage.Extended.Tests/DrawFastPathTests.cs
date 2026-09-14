// Verifies the generated fast-path leaves stay in sync with the generator
// (drift test) and match the generic fallback leaves bit-for-bit.

using DotImage;
using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Draw;
using DotImage.Extended.Math.F64;
using Xunit;

namespace DotImage.Extended.Tests;

public class DrawFastPathTests
{
    private static Point Pt(int x, int y) => Geometry.Pt(x, y);

    private static Rect R(int x0, int y0, int x1, int y1) => Geometry.Rect(x0, y0, x1, y1);

    // A generic IImage wrapper. The dispatch sees it as an unknown source type and
    // routes to the generic *_Image_Image_* fallback leaves instead of the fast
    // leaves.
    private sealed class OpaqueImage(IImage m) : IImage
    {
        public IColor At(int x, int y) => m.At(x, y);
        public Rect Bounds() => m.Bounds();
        public IColorModel ColorModel() => m.ColorModel();
    }

    private static RgbaImage MakePattern(int w, int h)
    {
        var m = Images.NewRgba(R(0, 0, w, h));
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                m.SetRgba(x, y, new Rgba(
                    (byte)((x * 31 + y * 7) & 0xff),
                    (byte)((x * 13 + y * 29) & 0xff),
                    (byte)((x * 5 + y * 11) & 0xff),
                    (byte)(0x10 + (x * 3 + y * 5) % 0x80)));
            }
        }
        return m;
    }

    private static Aff3 TransformMatrix(double scale, double tx, double ty)
    {
        const double cos30 = 0.866025404;
        const double sin30 = 0.5;
        return new Aff3(
            scale * cos30, -scale * sin30, tx,
            scale * sin30, scale * cos30, ty);
    }

    private static void AssertPixEqual(RgbaImage want, RgbaImage got, string what)
    {
        Assert.Equal(want.Bounds(), got.Bounds());
        for (int y = want.Bounds().Min.Y; y < want.Bounds().Max.Y; y++)
        {
            for (int x = want.Bounds().Min.X; x < want.Bounds().Max.X; x++)
            {
                Assert.Equal(want.RgbaAt(x, y), got.RgbaAt(x, y));
            }
        }
    }

    [Fact]
    public void ScaleLeafGeneratedIsUpToDate()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "src", "DotImage.Extended", "Draw", "ScaleLeaf.Generated.cs")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        Assert.NotNull(dir);

        string committed = File.ReadAllText(Path.Combine(dir, "src", "DotImage.Extended", "Draw", "ScaleLeaf.Generated.cs"));
        string generated = DotImage.GenDraw.Generator.Generate();
        Assert.True(committed == generated,
            "ScaleLeaf.Generated.cs is out of date; run 'dotnet run --project eng/GenDraw' to regenerate.");
    }

    [Theory]
    [InlineData("scale", true, true, 3.75)]
    [InlineData("scale", true, false, 3.75)]
    [InlineData("scale", false, true, 0.6)]
    [InlineData("scale", false, false, 0.6)]
    [InlineData("transform", true, true, 2.5)]
    [InlineData("transform", true, false, 2.5)]
    [InlineData("transform", false, true, 0.5)]
    [InlineData("transform", false, false, 0.5)]
    public void FastPathsMatchGenericFallback(string mode, bool up, bool over, double factor)
    {
        var src = MakePattern(23, 19);
        RgbaImage Fast(Interpolator q)
        {
            var dst = Images.NewRgba(R(0, 0, 48, 48));
            if (mode == "scale")
            {
                q.Scale(dst, dst.Bounds(), src, src.Bounds(), over ? Op.Over : Op.Src, null);
            }
            else
            {
                q.Transform(dst, TransformMatrix(up ? factor : factor, 3, 2), src, src.Bounds(), over ? Op.Over : Op.Src, null);
            }
            return dst;
        }

        RgbaImage Generic(Interpolator q)
        {
            var ws = new OpaqueImage(src);
            var dst = Images.NewRgba(R(0, 0, 48, 48));
            if (mode == "scale")
            {
                q.Scale(dst, dst.Bounds(), ws, src.Bounds(), over ? Op.Over : Op.Src, null);
            }
            else
            {
                q.Transform(dst, TransformMatrix(up ? factor : factor, 3, 2), ws, src.Bounds(), over ? Op.Over : Op.Src, null);
            }
            return dst;
        }

        foreach (var q in new Interpolator[] { Interpolators.NearestNeighbor, Interpolators.ApproxBiLinear })
        {
            AssertPixEqual(Generic(q), Fast(q), $"mode={mode} up={up} over={over} f={factor} q={q.GetType().Name} srcMask/dstMask none");
        }
    }

    [Theory]
    [InlineData("scale", true)]
    [InlineData("transform", true)]
    [InlineData("scale", false)]
    [InlineData("transform", false)]
    public void FastPathsMatchGenericFallbackWithDstMask(string mode, bool over)
    {
        var src = MakePattern(23, 19);
        var dstMask = Images.NewRgba(R(0, 0, 48, 48));
        for (int y = 0; y < 48; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                int a = (x * 3 + y * 5) & 0xff;
                dstMask.SetRgba(x, y, new Rgba(0x00, 0x00, 0x00, (byte)a));
            }
        }

        RgbaImage Run(Interpolator q, IImage s, bool genericDst)
        {
            var dst = Images.NewRgba(R(0, 0, 48, 48));
            if (mode == "scale")
            {
                q.Scale(dst, dst.Bounds(), s, src.Bounds(), over ? Op.Over : Op.Src, new Options { DstMask = dstMask, DstMaskP = Pt(0, 0) });
            }
            else
            {
                q.Transform(dst, TransformMatrix(2.5, 3, 2), s, src.Bounds(), over ? Op.Over : Op.Src, new Options { DstMask = dstMask, DstMaskP = Pt(0, 0) });
            }
            return dst;
        }

        foreach (var q in new Interpolator[] { Interpolators.NearestNeighbor, Interpolators.ApproxBiLinear })
        {
            AssertPixEqual(Run(q, new OpaqueImage(src), genericDst: true), Run(q, src, genericDst: false),
                $"mode={mode} over={over} q={q.GetType().Name}");
        }
    }
}