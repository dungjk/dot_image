// Ported from Go src/image/png/reader_test.go


using System.Text;
using DotImage.Png;

namespace DotImage.Tests.Png;

public class PngReaderTests
{
    public static IEnumerable<object[]> Filenames =>
        FilenamesAll.Select(fn => new object[] { fn });

    private static readonly string[] FilenamesAll =
    [
        "basn0g01", "basn0g01-30", "basn0g02", "basn0g02-29", "basn0g04", "basn0g04-31",
        "basn0g08", "basn0g16", "basn2c08", "basn2c16", "basn3p01", "basn3p02", "basn3p04",
        "basn3p04-31i", "basn3p08", "basn3p08-trns", "basn4a08", "basn4a16", "basn6a08",
        "basn6a16", "ftbbn0g01", "ftbbn0g02", "ftbbn0g04", "ftbbn2c16", "ftbbn3p08",
        "ftbgn2c16", "ftbgn3p08", "ftbrn2c08", "ftbwn0g16", "ftbwn3p08", "ftbyn3p08",
        "ftp0n0g08", "ftp0n2c08", "ftp0n3p08", "ftp1n3p08",
    ];

    private static readonly string[] FilenamesShort =
        ["basn0g01", "basn0g04-31", "basn6a16"];

    public static readonly string[] FilenamesPaletted =
        ["basn3p01", "basn3p02", "basn3p04", "basn3p08", "basn3p08-trns"];

    private static string TestDataPath(string path) =>
        Path.Combine(AppContext.BaseDirectory, "testdata", path);

    public static IImage ReadPng(string path)
    {
        using var f = File.OpenRead(TestDataPath(path));
        return PngReader.Decode(f);
    }

    private static readonly Dictionary<string, string> FakeBKGDs = new()
    {
        ["ftbbn0g01"] = "bKGD {gray: 0;}\n",
        ["ftbbn0g02"] = "bKGD {gray: 0;}\n",
        ["ftbbn0g04"] = "bKGD {gray: 0;}\n",
        ["ftbbn2c16"] = "bKGD {red: 0;  green: 0;  blue: 65535;}\n",
        ["ftbbn3p08"] = "bKGD {index: 245}\n",
        ["ftbgn2c16"] = "bKGD {red: 0;  green: 65535;  blue: 0;}\n",
        ["ftbgn3p08"] = "bKGD {index: 245}\n",
        ["ftbrn2c08"] = "bKGD {red: 255;  green: 0;  blue: 0;}\n",
        ["ftbwn0g16"] = "bKGD {gray: 65535;}\n",
        ["ftbwn3p08"] = "bKGD {index: 0}\n",
        ["ftbyn3p08"] = "bKGD {index: 245}\n",
    };

    private static readonly Dictionary<string, string> FakeGAMAs = new()
    {
        ["ftbbn0g01"] = "",
        ["ftbbn0g02"] = "gAMA {0.45455}\n",
    };

    private static readonly Dictionary<string, string> FakeIHDRUsings = new()
    {
        ["ftbbn0g01"] = "    using grayscale;\n",
        ["ftbbn0g02"] = "    using grayscale;\n",
        ["ftbbn0g04"] = "    using grayscale;\n",
        ["ftbbn2c16"] = "    using color;\n",
        ["ftbgn2c16"] = "    using color;\n",
        ["ftbrn2c08"] = "    using color;\n",
        ["ftbwn0g16"] = "    using grayscale;\n",
    };

    private static void Sng(TextWriter w, string filename, IImage png)
    {
        var bounds = png.Bounds();
        var cm = png.ColorModel();
        int bitdepth = ReferenceEquals(cm, Models.Rgba) || ReferenceEquals(cm, Models.Nrgba) ||
                       ReferenceEquals(cm, Models.Alpha) || ReferenceEquals(cm, Models.Gray) ? 8 : 16;
        Palette? cpm = cm as Palette;
        PalettedImage? paletted = png as PalettedImage;
        if (cpm != null)
        {
            bitdepth = cpm.Length switch
            {
                <= 2 => 1,
                <= 4 => 2,
                <= 16 => 4,
                _ => 8,
            };
            paletted = (PalettedImage)png;
        }

        w.Write($"#SNG: from {filename}.png\nIHDR {{\n");
        w.Write($"    width: {bounds.Dx()}; height: {bounds.Dy()}; bitdepth: {bitdepth};\n");
        if (FakeIHDRUsings.TryGetValue(filename, out var usingLine))
            w.Write(usingLine);
        else if (ReferenceEquals(cm, Models.Rgba) || ReferenceEquals(cm, Models.Rgba64))
            w.Write("    using color;\n");
        else if (ReferenceEquals(cm, Models.Nrgba) || ReferenceEquals(cm, Models.Nrgba64))
            w.Write("    using color alpha;\n");
        else if (ReferenceEquals(cm, Models.Gray) || ReferenceEquals(cm, Models.Gray16))
            w.Write("    using grayscale;\n");
        else if (cpm != null)
            w.Write("    using color palette;\n");
        else
            w.Write("unknown PNG decoder color model\n");
        w.Write("}\n");

        if (FakeGAMAs.TryGetValue(filename, out var gama))
            w.Write(gama);
        else
            w.Write("gAMA {1.0000}\n");

        bool useTransparent = false;
        if (cpm != null)
        {
            int lastAlpha = -1;
            w.Write("PLTE {\n");
            for (int i = 0; i < cpm.Length; i++)
            {
                var c = cpm[i];
                byte r, g, b, a;
                if (c is Rgba rgba)
                {
                    r = rgba.R; g = rgba.G; b = rgba.B; a = 0xff;
                }
                else if (c is Nrgba nrgba)
                {
                    r = nrgba.R; g = nrgba.G; b = nrgba.B; a = nrgba.A;
                }
                else throw new InvalidOperationException("unknown palette color type");
                if (a != 0xff) lastAlpha = i;
                w.Write($"    ({r,3},{g,3},{b,3})     # rgb = (0x{r:x2},0x{g:x2},0x{b:x2})\n");
            }
            w.Write("}\n");
            if (FakeBKGDs.TryGetValue(filename, out var bkgd))
                w.Write(bkgd);
            if (lastAlpha != -1)
            {
                w.Write("tRNS {\n");
                for (int i = 0; i <= lastAlpha; i++)
                {
                    var (_, _, _, alpha) = cpm[i].Rgba();
                    w.Write($" {alpha >> 8}");
                }
                w.Write("}\n");
            }
        }
        else if (filename.StartsWith("ft"))
        {
            if (FakeBKGDs.TryGetValue(filename, out var bkgd))
                w.Write(bkgd);
            var c = png.At(0, 0);
            if (c is Nrgba nrgba && nrgba.A == 0)
            {
                useTransparent = true;
                w.Write("tRNS {\n");
                if (filename is "ftbbn0g01" or "ftbbn0g02" or "ftbbn0g04")
                    w.Write($"    gray: {nrgba.R};\n");
                else
                    w.Write($"    red: {nrgba.R}; green: {nrgba.G}; blue: {nrgba.B};\n");
                w.Write("}\n");
            }
            else if (c is Nrgba64 nrgba64 && nrgba64.A == 0)
            {
                useTransparent = true;
                w.Write("tRNS {\n");
                if (filename == "ftbwn0g16")
                    w.Write($"    gray: {nrgba64.R};\n");
                else
                    w.Write($"    red: {nrgba64.R}; green: {nrgba64.G}; blue: {nrgba64.B};\n");
                w.Write("}\n");
            }
        }

        w.Write("IMAGE {\n    pixels hex\n");
        for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
        {
            if (ReferenceEquals(cm, Models.Gray))
            {
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                    w.Write($"{((Gray)png.At(x, y)).Y:x2}");
            }
            else if (ReferenceEquals(cm, Models.Gray16))
            {
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                    w.Write($"{((Gray16)png.At(x, y)).Y:x4} ");
            }
            else if (ReferenceEquals(cm, Models.Rgba))
            {
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                {
                    var rgba = (Rgba)png.At(x, y);
                    w.Write($"{rgba.R:x2}{rgba.G:x2}{rgba.B:x2} ");
                }
            }
            else if (ReferenceEquals(cm, Models.Rgba64))
            {
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                {
                    var rgba64 = (Rgba64)png.At(x, y);
                    w.Write($"{rgba64.R:x4}{rgba64.G:x4}{rgba64.B:x4} ");
                }
            }
            else if (ReferenceEquals(cm, Models.Nrgba))
            {
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                {
                    var nrgba = (Nrgba)png.At(x, y);
                    if (filename is "ftbbn0g01" or "ftbbn0g02" or "ftbbn0g04")
                        w.Write($"{nrgba.R:x2}");
                    else if (useTransparent)
                        w.Write($"{nrgba.R:x2}{nrgba.G:x2}{nrgba.B:x2} ");
                    else
                        w.Write($"{nrgba.R:x2}{nrgba.G:x2}{nrgba.B:x2}{nrgba.A:x2} ");
                }
            }
            else if (ReferenceEquals(cm, Models.Nrgba64))
            {
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                {
                    var nrgba64 = (Nrgba64)png.At(x, y);
                    if (filename == "ftbwn0g16")
                        w.Write($"{nrgba64.R:x4} ");
                    else if (useTransparent)
                        w.Write($"{nrgba64.R:x4}{nrgba64.G:x4}{nrgba64.B:x4} ");
                    else
                        w.Write($"{nrgba64.R:x4}{nrgba64.G:x4}{nrgba64.B:x4}{nrgba64.A:x4} ");
                }
            }
            else if (cpm != null && paletted != null)
            {
                int b = 0, c = 0;
                for (int x = bounds.Min.X; x < bounds.Max.X; x++)
                {
                    b = (b << bitdepth) | paletted.ColorIndexAt(x, y);
                    c++;
                    if (c == 8 / bitdepth)
                    {
                        w.Write($"{b:x2}");
                        b = 0;
                        c = 0;
                    }
                }
                if (c != 0)
                {
                    while (c != 8 / bitdepth)
                    {
                        b <<= bitdepth;
                        c++;
                    }
                    w.Write($"{b:x2}");
                }
            }
            w.Write('\n');
        }
        w.Write("}\n");
    }

    [Theory]
    [MemberData(nameof(Filenames))]
    public void Reader_PngSuite(string fn)
    {
        var img = ReadPng($"pngsuite/{fn}.png");

        if (fn == "basn4a16")
        {
            var c = (Nrgba64)img.At(2, 1);
            Assert.Equal(0x11a7, c.R);
            Assert.Equal(0x11a7, c.G);
            Assert.Equal(0x11a7, c.B);
            Assert.Equal(0x1085, c.A);
            return;
        }

        using var sw = new StringWriter();
        Sng(sw, fn, img);
        var produced = new StringReader(sw.ToString());

        using var sf = File.OpenText(TestDataPath($"pngsuite/{fn}.sng"));
        while (true)
        {
            var ps = produced.ReadLine();
            var ss = sf.ReadLine();
            if (ps == null && ss == null) break;
            if (ps == null || ss == null)
            {
                Assert.Fail($"{fn}: Different sizes");
                return;
            }
            if (ss.Contains("# rgb = (") && !ss.EndsWith(')'))
            {
                int i = ss.LastIndexOf(") ", StringComparison.Ordinal);
                if (i >= 0) ss = ss[..(i + 1)];
            }
            Assert.True(ps == ss, $"{fn}: Mismatch\n{ps}\nversus\n{ss}");
        }
    }

    [Theory]
    [InlineData("invalid-zlib.png", "compression")]
    [InlineData("invalid-crc32.png", "invalid checksum")]
    [InlineData("invalid-noend.png", "past the end")]
    [InlineData("invalid-trunc.png", "past the end")]
    public void Reader_Errors(string file, string errPart)
    {
        try
        {
            ReadPng(file);
            Assert.Fail($"decoding {file}: missing error");
        }
        catch (Exception ex)
        {
            Assert.Contains(errPart, ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(PalettedFilenames))]
    public void Reader_PalettedDecodeConfig(string fn)
    {
        using var f = File.OpenRead(TestDataPath($"pngsuite/{fn}.png"));
        var cfg = PngReader.DecodeConfig(f);
        Assert.IsType<Palette>(cfg.ColorModel);
        Assert.NotNull(cfg.ColorModel);
    }

    public static IEnumerable<object[]> PalettedFilenames =>
        FilenamesPaletted.Select(fn => new object[] { fn });

    [Fact]
    public void Reader_Interlaced()
    {
        var a = ReadPng("gray-gradient.png");
        var b = ReadPng("gray-gradient.interlaced.png");
        Assert.Equal(a.Bounds(), b.Bounds());
        var bounds = a.Bounds();
        for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
        {
            for (int x = bounds.Min.X; x < bounds.Max.X; x++)
            {
                var (r0, g0, b0, a0) = a.At(x, y).Rgba();
                var (r1, g1, b1, a1) = b.At(x, y).Rgba();
                Assert.True(r0 == r1 && g0 == g1 && b0 == b1 && a0 == a1, $"pixel ({x},{y}) differs");
            }
        }
    }

    [Fact]
    public void Reader_IncompleteIDATOnRowBoundary()
    {
        byte[] data = Encoding.Latin1.GetBytes(
            "\x89PNG\r\n\x1a\n" +
            "\x00\x00\x00\x0dIHDR\x00\x00\x00\x01\x00\x00\x00\x02\x08\x00\x00\x00\x00\xbc\xea\xe9\xfb" +
            "\x00\x00\x00\x0eIDAT\x78\x9c\x62\x62\x00\x04\x00\x00\xff\xff\x00\x06\x00\x03\xfa\xd0\x59\xae" +
            "\x00\x00\x00\x00IEND\xae\x42\x60\x82");
        Assert.ThrowsAny<Exception>(() => PngReader.Decode(new MemoryStream(data)));
    }

    [Fact]
    public void Reader_TrailingIDATChunks()
    {
        const string ihdr = "\x00\x00\x00\x0dIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x00\x00\x00\x00\x3a\x7e\x9b\x55";
        const string idatWhite = "\x00\x00\x00\x0eIDAT\x78\x9c\x62\xfa\x0f\x08\x00\x00\xff\xff\x01\x05\x01\x02\x5a\xdd\x39\xcd";
        const string idatZero = "\x00\x00\x00\x00IDAT\x35\xaf\x06\x1e";
        const string iend = "\x00\x00\x00\x00IEND\xae\x42\x60\x82";
        var valid = Encoding.Latin1.GetBytes("\x89PNG\r\n\x1a\n" + ihdr + idatWhite + idatZero + iend);
        PngReader.Decode(new MemoryStream(valid));

        const string idatBlack = "\x00\x00\x00\x0eIDAT\x78\x9c\x62\x62\x00\x04\x00\x00\xff\xff\x00\x06\x00\x03\xfa\xd0\x59\xae";
        var trailing = Encoding.Latin1.GetBytes("\x89PNG\r\n\x1a\n" + ihdr + idatWhite + idatBlack + iend);
        var img = PngReader.Decode(new MemoryStream(trailing));
        Assert.False(img.At(0, 0) is Gray g && g.Y == 0);
    }

    [Fact]
    public void Reader_MultipletRNSChunks()
    {
        const string ihdr = "\x00\x00\x00\x0dIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x03\x00\x00\x00\x28\xcb\x34\xbb";
        const string plte = "\x00\x00\x00\x03PLTE\xff\x00\x00\x19\xe2\x09\x37";
        const string trns = "\x00\x00\x00\x01tRNS\x7f\x80\x5c\xb4\xcb";
        const string idat = "\x00\x00\x00\x0eIDAT\x78\x9c\x62\x62\x00\x04\x00\x00\xff\xff\x00\x06\x00\x03\xfa\xd0\x59\xae";
        const string iend = "\x00\x00\x00\x00IEND\xae\x42\x60\x82";
        for (int i = 0; i < 4; i++)
        {
            var b = new MemoryStream();
            b.Write(Encoding.Latin1.GetBytes("\x89PNG\r\n\x1a\n"));
            b.Write(Encoding.Latin1.GetBytes(ihdr));
            b.Write(Encoding.Latin1.GetBytes(plte));
            for (int j = 0; j < i; j++)
                b.Write(Encoding.Latin1.GetBytes(trns));
            b.Write(Encoding.Latin1.GetBytes(idat));
            b.Write(Encoding.Latin1.GetBytes(iend));
            b.Position = 0;
            if (i < 2)
            {
                var m = PngReader.Decode(b);
                IColor want = i == 0
                    ? new Rgba(0xff, 0, 0, 0xff)
                    : new Nrgba(0xff, 0, 0, 0x7f);
                Assert.True(EqualsColor(m.At(0, 0), want), $"{i} tRNS chunks");
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => PngReader.Decode(b));
            }
        }
    }

    [Fact]
    public void Reader_UnknownChunkLengthUnderflow()
    {
        byte[] data =
        [
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0xff, 0xff,
            0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x06, 0xf4, 0x7c, 0x55, 0x04, 0x1a,
            0xd3, 0x11, 0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e, 0x00, 0x00,
            0x01, 0x00, 0xff, 0xff, 0xff, 0xff, 0x07, 0xf4, 0x7c, 0x55, 0x04, 0x1a,
            0xd3,
        ];
        Assert.ThrowsAny<Exception>(() => PngReader.Decode(new MemoryStream(data)));
    }

    [Fact]
    public void Reader_Paletted8OutOfRangePixel()
    {
        var img = ReadPng("invalid-palette.png");
        var want = new Rgba(0, 0, 0, 0xff);
        Assert.True(EqualsColor(img.At(15, 15), want));
    }

    [Fact]
    public void Reader_Gray8Transparent()
    {
        byte[] data =
        [
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x0f, 0x00, 0x00, 0x00, 0x0b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x85, 0x2c, 0x88,
            0x80, 0x00, 0x00, 0x00, 0x02, 0x74, 0x52, 0x4e, 0x53, 0x00, 0xff, 0x5b, 0x91, 0x22, 0xb5, 0x00,
            0x00, 0x00, 0x02, 0x62, 0x4b, 0x47, 0x44, 0x00, 0xff, 0x87, 0x8f, 0xcc, 0xbf, 0x00, 0x00, 0x00,
            0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0a, 0xf0, 0x00, 0x00, 0x0a, 0xf0, 0x01, 0x42, 0xac,
            0x34, 0x98, 0x00, 0x00, 0x00, 0x07, 0x74, 0x49, 0x4d, 0x45, 0x07, 0xd5, 0x04, 0x02, 0x12, 0x11,
            0x11, 0xf7, 0x65, 0x3d, 0x8b, 0x00, 0x00, 0x00, 0x4f, 0x49, 0x44, 0x41, 0x54, 0x08, 0xd7, 0x63,
            0xf8, 0xff, 0xff, 0xff, 0xb9, 0xbd, 0x70, 0xf0, 0x8c, 0x01, 0xc8, 0xaf, 0x6e, 0x99, 0x02, 0x05,
            0xd9, 0x7b, 0xc1, 0xfc, 0x6b, 0xff, 0xa1, 0xa0, 0x87, 0x30, 0xff, 0xd9, 0xde, 0xbd, 0xd5, 0x4b,
            0xf7, 0xee, 0xfd, 0x0e, 0xe3, 0xef, 0xcd, 0x06, 0x19, 0x14, 0xf5, 0x1e, 0xce, 0xef, 0x01, 0x31,
            0x92, 0xd7, 0x82, 0x41, 0x31, 0x9c, 0x3f, 0x07, 0x02, 0xee, 0xa1, 0xaa, 0xff, 0xff, 0x9f, 0xe1,
            0xd9, 0x56, 0x30, 0xf8, 0x0e, 0xe5, 0x03, 0x00, 0xa9, 0x42, 0x84, 0x3d, 0xdf, 0x8f, 0xa6, 0x8f,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44, 0xae, 0x42, 0x60, 0x82,
        ];
        var m = PngReader.Decode(new MemoryStream(data));
        const string want =
            ".. .. .. ce bd bd bd bd bd bd bd bd bd bd e6 \n" +
            ".. .. .. 7b 84 94 94 94 94 94 94 94 94 6b bd \n" +
            ".. .. .. 7b d6 .. .. .. .. .. .. .. .. 8c bd \n" +
            ".. .. .. 7b d6 .. .. .. .. .. .. .. .. 8c bd \n" +
            ".. .. .. 7b d6 .. .. .. .. .. .. .. .. 8c bd \n" +
            "e6 bd bd 7b a5 bd bd f7 .. .. .. .. .. 8c bd \n" +
            "bd 6b 94 94 94 94 5a ef .. .. .. .. .. 8c bd \n" +
            "bd 8c .. .. .. .. 63 ad ad ad ad ad ad 73 bd \n" +
            "bd 8c .. .. .. .. 63 9c 9c 9c 9c 9c 9c 9c de \n" +
            "bd 6b 94 94 94 94 5a ef .. .. .. .. .. .. .. \n" +
            "e6 b5 b5 b5 b5 b5 b5 f7 .. .. .. .. .. .. .. \n";
        const string hex = "0123456789abcdef";
        var got = new StringBuilder();
        var bounds = m.Bounds();
        for (int y = bounds.Min.Y; y < bounds.Max.Y; y++)
        {
            for (int x = bounds.Min.X; x < bounds.Max.X; x++)
            {
                var rgba = m.At(x, y).Rgba();
                if (rgba.A != 0)
                    got.Append($"{hex[(int)((rgba.R >> 12) & 0x0f)]}{hex[(int)((rgba.R >> 8) & 0x0f)]} ");
                else
                    got.Append(".. ");
            }
            got.Append('\n');
        }
        Assert.Equal(want, got.ToString());
    }

    [Fact]
    public void Reader_DecodePalettedWithTransparency()
    {
        byte[] src =
        [
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x20, 0x04, 0x03, 0x00, 0x00, 0x00, 0x81, 0x54, 0x67,
            0xc7, 0x00, 0x00, 0x00, 0x30, 0x50, 0x4c, 0x54, 0x45, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0x0e,
            0x00, 0x23, 0x27, 0x7b, 0xb1, 0x2d, 0x0a, 0x49, 0x3f, 0x19, 0x78, 0x5f, 0xcd, 0xe4, 0x69, 0x69,
            0xe4, 0x71, 0x59, 0x53, 0x80, 0x11, 0x14, 0x8b, 0x00, 0xa9, 0x8d, 0x95, 0xcb, 0x99, 0x2f, 0x6b,
            0xd7, 0x29, 0x91, 0xd7, 0x7b, 0xba, 0xff, 0xe3, 0xd7, 0x13, 0xc6, 0xd3, 0x58, 0x00, 0x00, 0x00,
            0x01, 0x74, 0x52, 0x4e, 0x53, 0x00, 0x40, 0xe6, 0xd8, 0x66, 0x00, 0x00, 0x00, 0xfd, 0x49, 0x44,
            0x41, 0x54, 0x28, 0xcf, 0x63, 0x60, 0x00, 0x83, 0x55, 0x0c, 0x68, 0x60, 0x9d, 0x02, 0x9a, 0x80,
            0xde, 0x23, 0x74, 0x15, 0xef, 0x50, 0x94, 0x70, 0x2d, 0xd2, 0x7b, 0x87, 0xa2, 0x84, 0xeb, 0xee,
            0xbb, 0x77, 0x6f, 0x51, 0x94, 0xe8, 0xbd, 0x7d, 0xf7, 0xee, 0x12, 0xb2, 0x80, 0xd2, 0x3d, 0x54,
            0x01, 0x26, 0x10, 0x1f, 0x59, 0x40, 0x0f, 0xc8, 0xd7, 0x7e, 0x84, 0x70, 0x1c, 0xd7, 0xba, 0xb7,
            0x4a, 0xda, 0xda, 0x77, 0x11, 0xf6, 0xac, 0x5a, 0xa5, 0xf4, 0xf9, 0xbf, 0xfd, 0x3d, 0x24, 0x6b,
            0x98, 0x94, 0xf4, 0xff, 0x7f, 0x52, 0x42, 0x16, 0x30, 0x0e, 0xd9, 0xed, 0x6a, 0x8c, 0xec, 0x10,
            0x65, 0x53, 0x97, 0x60, 0x23, 0x64, 0x1d, 0x8a, 0x2e, 0xc6, 0x2e, 0x42, 0x08, 0x3d, 0x4c, 0xca,
            0x81, 0xc1, 0x82, 0xa6, 0xa2, 0x46, 0x08, 0x3d, 0x4a, 0xa1, 0x82, 0xc6, 0x82, 0xa1, 0x4a, 0x08,
            0x3d, 0xfa, 0xa6, 0x81, 0xa1, 0xa2, 0xc1, 0x9f, 0x10, 0x66, 0xd4, 0x2b, 0x87, 0x0a, 0x86, 0x1a,
            0x7d, 0x57, 0x80, 0x9b, 0x99, 0xaf, 0x62, 0x1a, 0x1a, 0xec, 0xf0, 0x0d, 0x66, 0x2a, 0x7b, 0x5a,
            0xba, 0xd2, 0x64, 0x63, 0x4b, 0xa6, 0xb2, 0xb4, 0x02, 0xa8, 0x12, 0xb5, 0x24, 0xa5, 0x99, 0x2e,
            0x33, 0x95, 0xd4, 0x92, 0x10, 0xee, 0xd0, 0x59, 0xb9, 0x6a, 0xd6, 0x21, 0x24, 0xb7, 0x33, 0x9d,
            0x01, 0x01, 0x64, 0xbf, 0xac, 0x59, 0xb2, 0xca, 0xeb, 0x14, 0x92, 0x80, 0xd6, 0x9a, 0x53, 0x4a,
            0x6b, 0x4e, 0x2d, 0x42, 0x52, 0xa1, 0x73, 0x28, 0x54, 0xe7, 0x90, 0x6a, 0x00, 0x92, 0x92, 0x45,
            0xa1, 0x40, 0x84, 0x2c, 0xe0, 0xc4, 0xa0, 0xb2, 0x28, 0x14, 0xc1, 0x67, 0xe9, 0x50, 0x60, 0x60,
            0xea, 0x70, 0x40, 0x12, 0x00, 0x79, 0x54, 0x09, 0x22, 0x00, 0x00, 0x30, 0xf3, 0x52, 0x87, 0xc6,
            0xe4, 0xbd, 0x70, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44, 0xae, 0x42, 0x60, 0x82,
        ];
        var cfg = PngReader.DecodeConfig(new MemoryStream(src));
        var (_, _, _, alpha) = ((Palette)cfg.ColorModel)[0].Rgba();
        Assert.Equal(0u, alpha);
        var img = PngReader.Decode(new MemoryStream(src));
        var (_, _, _, alpha2) = ((Palette)img.ColorModel())[0].Rgba();
        Assert.Equal(0u, alpha2);
    }

    private static bool EqualsColor(IColor got, IColor want)
    {
        var g = got.Rgba();
        var w = want.Rgba();
        return g == w;
    }
}
