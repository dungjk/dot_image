using DotImage;
using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Vector;
using Xunit;

namespace DotImage.Extended.Tests;

public class VectorRasterizeTests
{
    // Ported from golang.org/x/image/vector vector_test.go (basicMask): the
    // mask produced for the path MoveTo(2,2) LineTo(8,2) QuadTo(14,2,14,14)
    // CubeTo(8,2,5,20,2,8) ClosePath on a 16x16 Rasterizer.
    private static readonly byte[] BasicMask =
    [
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe3, 0xaa, 0x3e, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xfa, 0x5f, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xfc, 0x24, 0x00, 0x00, 0x00,
        0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xa1, 0x00, 0x00, 0x00,
        0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xfc, 0x14, 0x00, 0x00,
        0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x4a, 0x00, 0x00,
        0x00, 0x00, 0xcc, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x81, 0x00, 0x00,
        0x00, 0x00, 0x66, 0xff, 0xff, 0xff, 0xff, 0xff, 0xef, 0xe4, 0xff, 0xff, 0xff, 0xb6, 0x00, 0x00,
        0x00, 0x00, 0x0c, 0xf2, 0xff, 0xff, 0xfe, 0x9e, 0x15, 0x00, 0x15, 0x96, 0xff, 0xce, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x88, 0xfc, 0xe3, 0x43, 0x00, 0x00, 0x00, 0x00, 0x06, 0xcd, 0xdc, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x10, 0x0f, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x25, 0xde, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x56, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    private static readonly IImage Opaque = new UniformImage(new Rgba(0xff, 0xff, 0xff, 0xff));

    private static void AssertBytesWithinTwo(string prefix, byte[] got, byte[] want)
    {
        Assert.Equal(want.Length, got.Length);
        for (int i = 0; i < got.Length; i++)
        {
            int delta = got[i] - want[i];
            if (delta < -2 || +2 < delta)
            {
                Assert.Fail($"{prefix}: i={i}: got {got[i]:X2}, want {want[i]:X2}");
            }
        }
    }

    private static void AddBasicPath(Rasterizer z)
    {
        z.MoveTo(2, 2);
        z.LineTo(8, 2);
        z.QuadTo(14, 2, 14, 14);
        z.CubeTo(8, 2, 5, 20, 2, 8);
        z.ClosePath();
    }

    [Theory]
    // background, op-over? (over vs src), xPadding
    [InlineData(0x00, true, 0)]
    [InlineData(0x00, true, 7)]
    [InlineData(0x00, false, 0)]
    [InlineData(0x00, false, 7)]
    [InlineData(0x80, true, 0)]
    [InlineData(0x80, true, 7)]
    [InlineData(0x80, false, 0)]
    [InlineData(0x80, false, 7)]
    public void BasicPathDstAlpha(byte background, bool over, int xPadding)
    {
        Op op = over ? Op.Over : Op.Src;
        var dst = Images.NewAlpha(Geometry.Rect(0, 0, 16 + xPadding, 16));
        dst.Pix.Span.Fill(background);

        byte[] want = dst.Pix.Span.ToArray();
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                byte ma = BasicMask[16 * y + x];
                int i = dst.PixOffset(x, y);
                want[i] = op == Op.Over && background == 0x80
                    ? (byte)(0xff - (0xff - ma) / 2)
                    : ma;
            }
        }

        var z = Rasterizer.NewRasterizer(16, 16);
        AddBasicPath(z);
        z.DrawOp = op;
        z.Draw(dst, z.Bounds(), Opaque, new Point(0, 0));

        AssertBytesWithinTwo($"background={background & 0xff:X2}, op={op}, xPadding={xPadding}",
            dst.Pix.Span.ToArray(), want);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 7)]
    [InlineData(false, 0)]
    [InlineData(false, 7)]
    public void BasicPathDstRgba(bool over, int xPadding)
    {
        Op op = over ? Op.Over : Op.Src;
        var blue = new UniformImage(new Rgba(0x00, 0x00, 0xff, 0xff));
        var dst = Images.NewRgba(Geometry.Rect(0, 0, 16 + xPadding, 16));
        for (int y = 0; y < dst.Bounds().Max.Y; y++)
        {
            for (int x = 0; x < dst.Bounds().Max.X; x++)
            {
                dst.SetRgba(x, y, new Rgba((byte)(y * 0x07), (byte)(x * 0x05), 0x00, 0x80));
            }
        }

        byte[] want = dst.Pix.Span.ToArray();
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                byte ma = BasicMask[16 * y + x];
                int i = dst.PixOffset(x, y);
                if (op == Op.Over)
                {
                    want[i + 0] = (byte)((uint)(0xff - ma) * (uint)(y * 0x07) / 0xff);
                    want[i + 1] = (byte)((uint)(0xff - ma) * (uint)(x * 0x05) / 0xff);
                    want[i + 2] = ma;
                    want[i + 3] = (byte)(ma / 2 + 0x80);
                }
                else
                {
                    want[i + 0] = 0x00;
                    want[i + 1] = 0x00;
                    want[i + 2] = ma;
                    want[i + 3] = ma;
                }
            }
        }

        var z = Rasterizer.NewRasterizer(16, 16);
        AddBasicPath(z);
        z.DrawOp = op;
        z.Draw(dst, z.Bounds(), blue, new Point(0, 0));

        AssertBytesWithinTwo($"op={op}, xPadding={xPadding}", dst.Pix.Span.ToArray(), want);
    }

    [Fact]
    public void RasterizeOutOfBounds()
    {
        const int center = 16, radius = 20, n = 16;
        var z = new Rasterizer();
        for (int i = 0; i < n; i++)
        {
            for (int j = 1; j < n / 2; j++)
            {
                z.Reset(2 * center, 2 * center);
                z.MoveTo(1 * center, 1 * center);
                (float px1, float py1) = PointOnCircle(center, radius, i + 0, n);
                z.LineTo(px1, py1);
                (float px2, float py2) = PointOnCircle(center, radius, i + j, n);
                z.LineTo(px2, py2);
                z.ClosePath();

                z.MoveTo(0 * center, 0 * center);
                z.LineTo(0 * center, 2 * center);
                z.LineTo(2 * center, 2 * center);
                z.LineTo(2 * center, 0 * center);
                z.ClosePath();

                var dst = Images.NewAlpha(z.Bounds());
                z.Draw(dst, dst.Bounds(), Opaque, new Point(0, 0));
            }
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void RasterizePolygon(int radius)
    {
        var z = new Rasterizer();
        for (int n = 3; n <= 19; n += 4)
        {
            z.Reset(2 * radius, 2 * radius);
            z.MoveTo(2 * radius, 1 * radius);
            for (int i = 1; i < n; i++)
            {
                (float px, float py) = PointOnCircle(radius, radius, i, n);
                z.LineTo(px, py);
            }

            z.ClosePath();

            var dst = Images.NewAlpha(z.Bounds());
            z.Draw(dst, dst.Bounds(), Opaque, new Point(0, 0));

            string? err = CheckCornersCenter(dst);
            Assert.Null(err);
        }
    }

    [Fact]
    public void RasterizeAlmostAxisAligned()
    {
        var z = Rasterizer.NewRasterizer(8, 8);
        z.MoveTo(2, 2);
        z.LineTo(6, MathF.BitDecrement(2f));
        z.LineTo(6, 6);
        z.LineTo(MathF.BitDecrement(2f), 6);
        z.ClosePath();

        var dst = Images.NewAlpha(z.Bounds());
        z.Draw(dst, dst.Bounds(), Opaque, new Point(0, 0));

        Assert.Null(CheckCornersCenter(dst));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void RasterizeWideAlmostHorizontalLines(int i)
    {
        float x = (float)(1 << i);
        var z = new Rasterizer();

        z.Reset(8, 8);
        z.MoveTo(-x, 3);
        z.LineTo(+x, 4);
        z.LineTo(+x, 6);
        z.LineTo(-x, 6);
        z.ClosePath();

        var dst = Images.NewAlpha(z.Bounds());
        z.Draw(dst, dst.Bounds(), Opaque, new Point(0, 0));

        Assert.Null(CheckCornersCenter(dst));
    }

    [Fact]
    public void Rasterize30Degrees()
    {
        var z = Rasterizer.NewRasterizer(8, 8);
        z.MoveTo(4, 4);
        z.LineTo(8, 4);
        z.LineTo(4, 6);
        z.ClosePath();

        var dst = Images.NewAlpha(z.Bounds());
        z.Draw(dst, dst.Bounds(), Opaque, new Point(0, 0));

        Assert.Null(CheckCornersCenter(dst));
    }

    [Fact]
    public void RasterizeRandomLineTos()
    {
        var z = new Rasterizer();
        for (int i = 5; i < 50; i++)
        {
            var rng = new Random(i);

            z.Reset(i + 2, i + 2);
            z.MoveTo(i / 2f, i / 2f);
            while (rng.Next(16) != 0)
            {
                int x = 1 + rng.Next(i);
                int y = 1 + rng.Next(i);
                z.LineTo(x, y);
            }
            z.ClosePath();

            var dst = Images.NewAlpha(z.Bounds());
            z.Draw(dst, dst.Bounds(), Opaque, new Point(0, 0));

            string? err = CheckCorners(dst);
            Assert.Null(err);
        }
    }

    private static (float X, float Y) PointOnCircle(int center, int radius, int index, int number)
    {
        double c = center;
        double r = radius;
        double i = index;
        double n = number;
        return ((float)(c + r * System.Math.Cos(2 * System.Math.PI * i / n)),
            (float)(c + r * System.Math.Sin(2 * System.Math.PI * i / n)));
    }

    private static string? CheckCornersCenter(AlphaImage m)
    {
        string? cornersErr = CheckCorners(m);
        if (cornersErr != null)
        {
            return cornersErr;
        }

        int sizeX = m.Bounds().Dx();
        int sizeY = m.Bounds().Dy();
        byte center = m.Pix.Span[(sizeY / 2) * m.Stride + sizeX / 2];
        if (center != 0xff)
        {
            return $"center: got {center:X2}, want ff";
        }

        return null;
    }

    private static string? CheckCorners(AlphaImage m)
    {
        int sizeX = m.Bounds().Dx();
        int sizeY = m.Bounds().Dy();
        Span<byte> pix = m.Pix.Span;
        byte[] corners =
        [
            pix[0 * m.Stride + 0],
            pix[0 * m.Stride + sizeX - 1],
            pix[(sizeY - 1) * m.Stride + 0],
            pix[(sizeY - 1) * m.Stride + sizeX - 1],
        ];
        if (corners[0] != 0 || corners[1] != 0 || corners[2] != 0 || corners[3] != 0)
        {
            return $"corners were not all zero: {string.Join(", ", corners)}";
        }

        return null;
    }
}