// Ported from Go src/image/gif/writer_test.go


using DotImage.Gif;
using DotImage.Png;

namespace DotImage.Tests.Gif;

public class GifWriterTests
{
    private static string TestDataPath(string path) =>
        Path.Combine(AppContext.BaseDirectory, "testdata", path);

    private static IImage ReadImg(string filename)
    {
        using var f = File.OpenRead(TestDataPath(filename));
        return FormatRegistry.Decode(f).Image;
    }

    private static GifAnimation ReadGif(string filename)
    {
        using var f = File.OpenRead(TestDataPath(filename));
        return GifReader.DecodeAll(f);
    }

    private static long Delta(uint u0, uint u1)
    {
        long d = (long)u0 - u1;
        return d < 0 ? -d : d;
    }

    private static long AverageDelta(IImage m0, IImage m1)
    {
        var b = m0.Bounds();
        return AverageDeltaBound(m0, m1, b, b);
    }

    private static long AverageDeltaBound(IImage m0, IImage m1, Rect b0, Rect b1)
    {
        long sum = 0, n = 0;
        for (int y = b0.Min.Y; y < b0.Max.Y; y++)
        {
            for (int x = b0.Min.X; x < b0.Max.X; x++)
            {
                var (r0, g0, b0c, _) = m0.At(x, y).Rgba();
                var (r1, g1, b1c, _) = m1.At(x - b0.Min.X + b1.Min.X, y - b0.Min.Y + b1.Min.Y).Rgba();
                sum += Delta(r0, r1) + Delta(g0, g1) + Delta(b0c, b1c);
                n += 3;
            }
        }
        return sum / n;
    }

    public static TheoryData<string, long> WriterCases => new()
    {
        { "video-001.png", 1 << 12 },
        { "video-001.gif", 0 },
        { "video-001.interlaced.gif", 0 },
    };

    [Theory]
    [MemberData(nameof(WriterCases))]
    public void Writer_RoundTrip(string filename, long tolerance)
    {
        var m0 = ReadImg(filename);
        using var buf = new MemoryStream();
        GifWriter.Encode(buf, m0);
        buf.Position = 0;
        var m1 = GifReader.Decode(buf);
        Assert.Equal(m0.Bounds(), m1.Bounds());
        long avgDelta = AverageDelta(m0, m1);
        Assert.True(avgDelta <= tolerance, $"{filename}: average delta {avgDelta} > {tolerance}");
    }

    [Fact]
    public void Writer_SubImage()
    {
        var m0 = (PalettedImage)ReadImg("video-001.gif");
        m0 = (PalettedImage)m0.SubImage(Geometry.Rect(0, 0, 50, 30));
        using var buf = new MemoryStream();
        GifWriter.Encode(buf, m0);
        buf.Position = 0;
        var m1 = GifReader.Decode(buf);
        Assert.Equal(m0.Bounds(), m1.Bounds());
        Assert.Equal(0, AverageDelta(m0, m1));
    }

    private static bool PalettesEqual(Palette p, Palette q)
    {
        int n = Math.Min(p.Length, q.Length);
        for (int i = 0; i < n; i++)
        {
            var (pr, pg, pb, pa) = p[i].Rgba();
            var (qr, qg, qb, qa) = q[i].Rgba();
            if (pr != qr || pg != qg || pb != qb || pa != qa)
                return false;
        }
        for (int i = n; i < p.Length; i++)
        {
            var (r, g, b, a) = p[i].Rgba();
            if (r != 0 || g != 0 || b != 0 || a != 0xffff)
                return false;
        }
        for (int i = n; i < q.Length; i++)
        {
            var (r, g, b, a) = q[i].Rgba();
            if (r != 0 || g != 0 || b != 0 || a != 0xffff)
                return false;
        }
        return true;
    }

    private static readonly string[] Frames =
    [
        "video-001.gif",
        "video-005.gray.gif",
    ];

    private void TestEncodeAll(bool go1Dot5Fields, bool useGlobalColorModel)
    {
        const int width = 150, height = 103;
        var g0 = new GifAnimation
        {
            Image = [],
            Delay = [],
            LoopCount = 5,
        };
        foreach (var f in Frames)
        {
            var g = ReadGif(f);
            var m = g.Image[0];
            Assert.Equal(width, m.Bounds().Dx());
            Assert.Equal(height, m.Bounds().Dy());
            g0.Image.Add(m);
            g0.Delay.Add(0);
        }

        IColorModel globalColorModel = new Palette([]);
        byte backgroundIndex = 0;
        if (useGlobalColorModel)
        {
            globalColorModel = BuiltInPalettes.WebSafe;
            backgroundIndex = 1;
        }
        if (go1Dot5Fields)
        {
            var disposal = new byte[g0.Image.Count];
            Array.Fill(disposal, GifReader.DisposalNone);
            g0.Disposal = disposal;
            g0.Config = new Config(globalColorModel, width, height);
            g0.BackgroundIndex = backgroundIndex;
        }

        using var buf = new MemoryStream();
        GifWriter.EncodeAll(buf, g0);
        buf.Position = 0;
        var config = GifReader.DecodeConfig(buf);
        buf.Position = 0;
        var g1 = GifReader.DecodeAll(buf);

        Assert.Equal(g1.Config.Width, config.Width);
        Assert.Equal(g1.Config.Height, config.Height);
        if (go1Dot5Fields && g1.Config.ColorModel is Palette g1Pal)
            Assert.True(PalettesEqual(g1Pal, (Palette)globalColorModel));
        Assert.Equal(width, g1.Config.Width);
        Assert.Equal(height, g1.Config.Height);
        Assert.Equal(g0.LoopCount, g1.LoopCount);
        Assert.Equal(backgroundIndex, g1.BackgroundIndex);
        Assert.Equal(g0.Image.Count, g1.Image.Count);
        Assert.Equal(g1.Image.Count, g1.Delay.Count);
        Assert.Equal(g1.Image.Count, g1.Disposal?.Length ?? 0);

        for (int i = 0; i < g0.Image.Count; i++)
        {
            Assert.Equal(g0.Image[i].Bounds(), g1.Image[i].Bounds());
            Assert.Equal(g0.Delay[i], g1.Delay[i]);
            byte p0 = go1Dot5Fields ? GifReader.DisposalNone : (byte)0;
            Assert.Equal(p0, g1.Disposal![i]);
        }
    }

    [Fact] public void Writer_EncodeAllGo1Dot4() => TestEncodeAll(false, false);
    [Fact] public void Writer_EncodeAllGo1Dot5() => TestEncodeAll(true, false);
    [Fact] public void Writer_EncodeAllGo1Dot5GlobalColorModel() => TestEncodeAll(true, true);

    [Fact]
    public void Writer_EncodeMismatchDelay()
    {
        var images = new List<PalettedImage>
        {
            Images.NewPaletted(Geometry.Rect(0, 0, 5, 5), BuiltInPalettes.Plan9),
            Images.NewPaletted(Geometry.Rect(0, 0, 5, 5), BuiltInPalettes.Plan9),
        };
        Assert.Throws<InvalidOperationException>(() => GifWriter.EncodeAll(Stream.Null, new GifAnimation
        {
            Image = images,
            Delay = [0],
        }));

        Assert.Throws<InvalidOperationException>(() => GifWriter.EncodeAll(Stream.Null, new GifAnimation
        {
            Image = images,
            Delay = [0, 0],
            Disposal = [GifReader.DisposalNone],
        }));
    }

    [Fact]
    public void Writer_EncodeZeroGif()
    {
        Assert.Throws<InvalidOperationException>(() => GifWriter.EncodeAll(Stream.Null, new GifAnimation()));
    }

    [Fact]
    public void Writer_EncodeAllFramesOutOfBounds()
    {
        var images = new List<PalettedImage>
        {
            Images.NewPaletted(Geometry.Rect(0, 0, 5, 5), BuiltInPalettes.Plan9),
            Images.NewPaletted(Geometry.Rect(2, 2, 8, 8), BuiltInPalettes.Plan9),
            Images.NewPaletted(Geometry.Rect(3, 3, 4, 4), BuiltInPalettes.Plan9),
        };
        foreach (int upperBound in new[] { 6, 10 })
        {
            var g = new GifAnimation
            {
                Image = images,
                Delay = [0, 0, 0],
                Disposal = [0, 0, 0],
                Config = new Config(new Palette([]), upperBound, upperBound),
            };
            if (upperBound >= 8)
                GifWriter.EncodeAll(Stream.Null, g);
            else
                Assert.Throws<InvalidOperationException>(() => GifWriter.EncodeAll(Stream.Null, g));
        }
    }

    [Theory]
    [InlineData(-8, -9)]
    [InlineData(-4, -4)]
    [InlineData(-3, 3)]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    public void Writer_EncodeNonZeroMinPoint(int px, int py)
    {
        var p = new Point(px, py);
        var src = Images.NewPaletted(
            Geometry.Rect(p.X, p.Y, p.X + 6, p.Y + 6),
            BuiltInPalettes.Plan9);
        using var buf = new MemoryStream();
        GifWriter.Encode(buf, src);
        buf.Position = 0;
        var m = GifReader.Decode(buf);
        Assert.Equal(Geometry.Rect(0, 0, 6, 6), m.Bounds());
    }

    [Fact]
    public void Writer_EncodeNonZeroMinPointGrayDiagonal()
    {
        var p = new Point(2, 2);
        var src = Images.NewRgba(Geometry.Rect(p.X, p.Y, p.X + 6, p.Y + 6));
        src.SetRgba(2, 2, new Rgba(0x22, 0x22, 0x22, 0xff));
        src.SetRgba(3, 3, new Rgba(0x33, 0x33, 0x33, 0xff));
        src.SetRgba(4, 4, new Rgba(0x44, 0x44, 0x44, 0xff));
        src.SetRgba(5, 5, new Rgba(0x55, 0x55, 0x55, 0xff));
        src.SetRgba(6, 6, new Rgba(0x66, 0x66, 0x66, 0xff));
        src.SetRgba(7, 7, new Rgba(0x77, 0x77, 0x77, 0xff));

        using var buf = new MemoryStream();
        GifWriter.Encode(buf, src);
        buf.Position = 0;
        var m = GifReader.Decode(buf);
        Assert.Equal(Geometry.Rect(0, 0, 6, 6), m.Bounds());
        Assert.Equal(0x22u, m.At(0, 0).Rgba().R >> 8);
        Assert.Equal(0x77u, m.At(5, 5).Rgba().R >> 8);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Writer_EncodeImplicitConfigSize(int lowerBound)
    {
        var images = new List<PalettedImage>
        {
            Images.NewPaletted(Geometry.Rect(lowerBound, lowerBound, 4, 4), BuiltInPalettes.Plan9),
        };
        var g = new GifAnimation { Image = images, Delay = [0] };
        if (lowerBound >= 0)
            GifWriter.EncodeAll(Stream.Null, g);
        else
            Assert.Throws<InvalidOperationException>(() => GifWriter.EncodeAll(Stream.Null, g));
    }

    [Fact]
    public void Writer_EncodePalettes()
    {
        const int w = 5, h = 5;
        Palette[] pals =
        [
            new Palette([new Rgba(0, 0, 0, 0xff), new Rgba(1, 0, 0, 0xff), new Rgba(2, 0, 0, 0xff)]),
            new Palette([new Rgba(0, 0, 0, 0xff), new Rgba(0, 1, 0, 0xff)]),
            new Palette([new Rgba(0, 0, 3, 0xff), new Rgba(0, 0, 2, 0xff), new Rgba(0, 0, 1, 0xff), new Rgba(0, 0, 0, 0xff)]),
            new Palette([
                new Rgba(0x10, 0x07, 0xf0, 0xff), new Rgba(0x20, 0x07, 0xf0, 0xff),
                new Rgba(0x30, 0x07, 0xf0, 0xff), new Rgba(0x40, 0x07, 0xf0, 0xff),
                new Rgba(0x50, 0x07, 0xf0, 0xff),
            ]),
        ];
        var g0 = new GifAnimation
        {
            Image = pals.Select(p => Images.NewPaletted(Geometry.Rect(0, 0, w, h), p)).ToList(),
            Delay = [0, 0, 0, 0],
            Disposal = [0, 0, 0, 0],
            Config = new Config(pals[2], w, h),
        };
        using var buf = new MemoryStream();
        GifWriter.EncodeAll(buf, g0);
        buf.Position = 0;
        var g1 = GifReader.DecodeAll(buf);
        for (int i = 0; i < pals.Length; i++)
            Assert.True(PalettesEqual(g1.Image[i].Palette, pals[i]));
    }

    [Theory]
    [InlineData(256, false)]
    [InlineData(257, false)]
    [InlineData(256, true)]
    [InlineData(257, true)]
    public void Writer_EncodeBadPalettes(int n, bool nilColors)
    {
        const int w = 5, h = 5;
        var palColors = new IColor[n];
        if (!nilColors)
        {
            for (int i = 0; i < n; i++)
                palColors[i] = Colors.Black;
        }
        var pal = new Palette(palColors);
        var g = new GifAnimation
        {
            Image = [Images.NewPaletted(Geometry.Rect(0, 0, w, h), pal)],
            Delay = [0],
            Disposal = [0],
            Config = new Config(pal, w, h),
        };
        bool gotErr = false;
        try { GifWriter.EncodeAll(Stream.Null, g); }
        catch { gotErr = true; }
        bool wantErr = n > 256 || nilColors;
        Assert.Equal(wantErr, gotErr);
    }

    [Fact]
    public void Writer_ColorTablesMatch()
    {
        const int trIdx = 100;
        var global = BuiltInPalettes.Plan9;
        var rgb = (Rgba)global[trIdx];
        Assert.False(rgb.R == 0 && rgb.G == 0 && rgb.B == 0);

        var localColors = new IColor[global.Length];
        for (int i = 0; i < global.Length; i++)
            localColors[i] = global[i];
        localColors[trIdx] = new Rgba(0, 0, 0, 0);
        var local = new Palette(localColors);

        const int testLen = 3 * 256;
        const int padded = 7;
        Span<byte> globalTable = stackalloc byte[testLen];
        Span<byte> localTable = stackalloc byte[testLen];
        Assert.Equal(testLen, GifWriter.EncodeColorTable(globalTable, global, padded));
        Assert.Equal(testLen, GifWriter.EncodeColorTable(localTable, local, padded));
        Assert.False(globalTable[..testLen].SequenceEqual(localTable[..testLen]));

        var enc = new GifAnimation
        {
            Image = [Images.NewPaletted(Geometry.Rect(0, 0, 1, 1), local)],
            Delay = [0],
            Config = new Config(global, 1, 1),
        };
        using var buf = new MemoryStream();
        GifWriter.EncodeAll(buf, enc);
    }

    [Fact]
    public void Writer_EncodeCroppedSubImages()
    {
        var whole = Images.NewPaletted(Geometry.Rect(0, 0, 100, 100), BuiltInPalettes.Plan9);
        Rect[] subImages =
        [
            Geometry.Rect(0, 0, 50, 50),
            Geometry.Rect(50, 0, 100, 50),
            Geometry.Rect(0, 50, 50, 50),
            Geometry.Rect(50, 50, 100, 100),
            Geometry.Rect(25, 25, 75, 75),
            Geometry.Rect(0, 0, 100, 50),
            Geometry.Rect(0, 50, 100, 100),
            Geometry.Rect(0, 0, 50, 100),
            Geometry.Rect(50, 0, 100, 100),
        ];
        foreach (var sr in subImages)
        {
            var si = whole.SubImage(sr);
            using var buf = new MemoryStream();
            GifWriter.Encode(buf, si);
            buf.Position = 0;
            GifReader.Decode(buf);
        }
    }

    private sealed class OffsetImage(IImage inner, Rect rect) : IImage
    {
        public IColorModel ColorModel() => inner.ColorModel();
        public Rect Bounds() => rect;
        public IColor At(int x, int y) => inner.At(x, y);
    }

    [Fact]
    public void Writer_EncodeWrappedImage()
    {
        var m0 = ReadImg("video-001.gif");
        using var buf = new MemoryStream();
        GifWriter.Encode(buf, new OffsetImage(m0, m0.Bounds()));
        buf.Position = 0;
        var w1 = GifReader.Decode(buf);
        Assert.Equal(0, AverageDelta(m0, w1));

        var b0 = Geometry.Rect(128, 64, 256, 128);
        using var buf2 = new MemoryStream();
        GifWriter.Encode(buf2, new OffsetImage(m0, b0));
        buf2.Position = 0;
        var w2 = GifReader.Decode(buf2);
        Assert.Equal(0, AverageDeltaBound(m0, w2, b0, Geometry.Rect(0, 0, 128, 64)));
    }
}
