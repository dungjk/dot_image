// Ported from Go src/image/image_test.go

using DotImage.Color;

namespace DotImage.Tests;

public class ImageTests
{
    private static bool Cmp(IColorModel cm, IColor c0, IColor c1)
    {
        var (r0, g0, b0, a0) = cm.Convert(c0).Rgba();
        var (r1, g1, b1, a1) = cm.Convert(c1).Rgba();
        return r0 == r1 && g0 == g1 && b0 == b1 && a0 == a1;
    }

    private static readonly (string Name, Func<IWritableImage> Factory)[] TestImageFactories =
    [
        ("rgba", () => Images.NewRgba(Geometry.Rect(0, 0, 10, 10))),
        ("rgba64", () => Images.NewRgba64(Geometry.Rect(0, 0, 10, 10))),
        ("nrgba", () => Images.NewNrgba(Geometry.Rect(0, 0, 10, 10))),
        ("nrgba64", () => Images.NewNrgba64(Geometry.Rect(0, 0, 10, 10))),
        ("alpha", () => Images.NewAlpha(Geometry.Rect(0, 0, 10, 10))),
        ("alpha16", () => Images.NewAlpha16(Geometry.Rect(0, 0, 10, 10))),
        ("gray", () => Images.NewGray(Geometry.Rect(0, 0, 10, 10))),
        ("gray16", () => Images.NewGray16(Geometry.Rect(0, 0, 10, 10))),
        ("paletted", () => Images.NewPaletted(Geometry.Rect(0, 0, 10, 10), new Palette([
            Colors.Transparent,
            Colors.Opaque,
        ]))),
    ];

    [Fact]
    public void Image_BasicOperations()
    {
        foreach (var (name, factory) in TestImageFactories)
        {
            var m = factory();
            Assert.True(Geometry.Rect(0, 0, 10, 10).Eq(m.Bounds()),
                $"{name}: want bounds (0,0)-(10,10), got {m.Bounds()}");
            Assert.True(Cmp(m.ColorModel(), Colors.Transparent, m.At(6, 3)),
                $"{name}: at (6, 3), want a zero color");
            m.Set(6, 3, UniformImages.Opaque.C);
            Assert.True(Cmp(m.ColorModel(), Colors.Opaque, m.At(6, 3)),
                $"{name}: at (6, 3), want a non-zero color");
            Assert.True(((IWritableImage)m.SubImage(Geometry.Rect(6, 3, 7, 4))).Opaque(),
                $"{name}: at (6, 3) was not opaque");
            m = (IWritableImage)m.SubImage(Geometry.Rect(3, 2, 9, 8));
            Assert.True(Geometry.Rect(3, 2, 9, 8).Eq(m.Bounds()),
                $"{name}: sub-image want bounds (3,2)-(9,8), got {m.Bounds()}");
            Assert.True(Cmp(m.ColorModel(), Colors.Opaque, m.At(6, 3)),
                $"{name}: sub-image at (6, 3), want a non-zero color");
            Assert.True(Cmp(m.ColorModel(), Colors.Transparent, m.At(3, 3)),
                $"{name}: sub-image at (3, 3), want a zero color");
            m.Set(3, 3, UniformImages.Opaque.C);
            Assert.True(Cmp(m.ColorModel(), Colors.Opaque, m.At(3, 3)),
                $"{name}: sub-image at (3, 3), want a non-zero color");
            m.SubImage(Geometry.Rect(0, 0, 0, 0));
            m.SubImage(Geometry.Rect(10, 0, 10, 0));
            m.SubImage(Geometry.Rect(0, 10, 0, 10));
            m.SubImage(Geometry.Rect(10, 10, 10, 10));
        }
    }

    [Fact]
    public void NewXxx_BadRectangle()
    {
        var testCases = new (string Name, Action<Rect> Factory)[]
        {
            ("RGBA", r => Images.NewRgba(r)),
            ("RGBA64", r => Images.NewRgba64(r)),
            ("NRGBA", r => Images.NewNrgba(r)),
            ("NRGBA64", r => Images.NewNrgba64(r)),
            ("Alpha", r => Images.NewAlpha(r)),
            ("Alpha16", r => Images.NewAlpha16(r)),
            ("Gray", r => Images.NewGray(r)),
            ("Gray16", r => Images.NewGray16(r)),
            ("CMYK", r => Images.NewCmyk(r)),
            ("Paletted", r => Images.NewPaletted(r, new Palette([Colors.Black, Colors.White]))),
            ("YCbCr", r => YCbCrImages.NewYCbCr(r, YCbCrSubsampleRatio.Ratio422)),
            ("NYCbCrA", r => YCbCrImages.NewNYCbCrA(r, YCbCrSubsampleRatio.Ratio444)),
        };

        foreach (var (name, factory) in testCases)
        {
            foreach (var negDx in new[] { false, true })
            {
                foreach (var negDy in new[] { false, true })
                {
                    var r = new Rect(new Point(15, 28), new Point(16, 29));
                    if (negDx) r = new Rect(r.Min, new Point(14, r.Max.Y));
                    if (negDy) r = new Rect(r.Min, new Point(r.Max.X, 27));
                    bool want = !negDx && !negDy;
                    bool got = Call(factory, r);
                    Assert.Equal(want, got);
                }
            }

            int maxInt = int.MaxValue;
            Assert.False(Call(factory, new Rect(new Point(0, 0), new Point(maxInt, maxInt))));
        }
    }

    private static bool Call(Action<Rect> f, Rect r)
    {
        try
        {
            f(r);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [Fact]
    public void SixteenBitsPerColorChannel()
    {
        IColorModel[] testColorModels =
        [
            ColorModels.Rgba64,
            ColorModels.Nrgba64,
            ColorModels.Alpha16,
            ColorModels.Gray16,
        ];
        foreach (var cm in testColorModels)
        {
            var c = cm.Convert(new Rgba64(0x1234, 0x1234, 0x1234, 0x1234));
            var (r, _, _, _) = c.Rgba();
            Assert.Equal((uint)0x1234, r);
        }

        IWritableImage[] testImages =
        [
            Images.NewRgba64(Geometry.Rect(0, 0, 10, 10)),
            Images.NewNrgba64(Geometry.Rect(0, 0, 10, 10)),
            Images.NewAlpha16(Geometry.Rect(0, 0, 10, 10)),
            Images.NewGray16(Geometry.Rect(0, 0, 10, 10)),
        ];
        foreach (var m in testImages)
        {
            m.Set(1, 2, new Nrgba64(0xffff, 0xffff, 0xffff, 0x1357));
            var (r, _, _, _) = m.At(1, 2).Rgba();
            Assert.Equal((uint)0x1357, r);
        }
    }

    [Fact]
    public void Rgba64Image_Equivalence()
    {
        var r = Geometry.Rect(0, 0, 3, 2);
        IImage[] testCases =
        [
            Images.NewAlpha(r),
            Images.NewAlpha16(r),
            Images.NewCmyk(r),
            Images.NewGray(r),
            Images.NewGray16(r),
            Images.NewNrgba(r),
            Images.NewNrgba64(r),
            YCbCrImages.NewNYCbCrA(r, YCbCrSubsampleRatio.Ratio444),
            Images.NewPaletted(r, BuiltInPalettes.Plan9),
            Images.NewRgba(r),
            Images.NewRgba64(r),
            UniformImages.NewUniform(new Rgba64(0, 0, 0, 0)),
            YCbCrImages.NewYCbCr(r, YCbCrSubsampleRatio.Ratio444),
            r,
        ];

        foreach (var tc in testCases)
        {
            switch (tc)
            {
                case ISetRgba64Image setRgba64:
                    setRgba64.SetRgba64(1, 1, new Rgba64(0x7FFF, 0x3FFF, 0x0000, 0x7FFF));
                    break;
                case NYCbCrAImage nycbcra:
                    Memset(nycbcra.YCbCrImage.Y, 0x77);
                    Memset(nycbcra.YCbCrImage.Cb, 0x88);
                    Memset(nycbcra.YCbCrImage.Cr, 0x99);
                    Memset(nycbcra.A, 0xAA);
                    break;
                case UniformImage uniform:
                    uniform.C = new Rgba64(0x7FFF, 0x3FFF, 0x0000, 0x7FFF);
                    break;
                case YCbCrImage ycbcr:
                    Memset(ycbcr.Y, 0x77);
                    Memset(ycbcr.Cb, 0x88);
                    Memset(ycbcr.Cr, 0x99);
                    break;
                case Rect:
                    break;
                default:
                    Assert.Fail($"could not initialize pixels for {tc.GetType()}");
                    continue;
            }

            Assert.True(tc is IRgba64Image, $"{tc.GetType()} is not an IRgba64Image");
            var rgba64Image = (IRgba64Image)tc;
            var got = rgba64Image.Rgba64At(1, 1);
            var (wantR, wantG, wantB, wantA) = tc.At(1, 1).Rgba();
            Assert.Equal((uint)wantR, (uint)got.R);
            Assert.Equal((uint)wantG, (uint)got.G);
            Assert.Equal((uint)wantB, (uint)got.B);
            Assert.Equal((uint)wantA, (uint)got.A);
        }
    }

    private static void Memset(Memory<byte> memory, byte v)
    {
        memory.Span.Fill(v);
    }
}
