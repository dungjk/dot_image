using System;
using System.IO;
using System.Text;
using DotImage;
using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Font;
using DotImage.Extended.Font.OpenType;
using DotImage.Extended.Font.Sfnt;
using DotImage.Extended.Math.Fixed;
using SfntFont = DotImage.Extended.Font.Sfnt.Font;
using Xunit;

namespace DotImage.Extended.Tests;

// Ported from golang.org/x/image/font/opentype/opentype_test.go and
// golang.org/x/image/font/opentype/example_test.go.
public class OpenTypeTests
{
    private static readonly string TestDataDir = FindTestDataDir();
    private static readonly byte[] GoRegular = LoadFont("testdata/goregular.ttf");
    private static readonly byte[] GoItalic = LoadFont("testdata/goitalic.ttf");

    private static readonly IFontFace Regular;

    static OpenTypeTests()
    {
        var font = SfntFont.Parse(GoRegular);
        Regular = OpenTypeFont.NewFace(font, null);
    }

    private static string FindTestDataDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "TestData")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine(dir ?? throw new InvalidOperationException("TestData directory not found"), "TestData");
    }

    private static byte[] LoadFont(string rel)
    {
        string path = Path.Combine(TestDataDir, rel);
        return File.ReadAllBytes(path);
    }

    private static Int26_6 F6(int v) => Int26_6.FromRaw(v);

    private readonly record struct RuneTest(Rune R, Int26_6 Advance, Rect Dr);

    private static readonly RuneTest[] RuneTests =
    {
        new(new Rune(' '), F6(213), new Rect(new Point(0, 0), new Point(0, 0))),
        new(new Rune('A'), F6(512), new Rect(new Point(0, -9), new Point(8, 0))),
        new(new Rune('Á'), F6(512), new Rect(new Point(0, -12), new Point(8, 0))),
        new(new Rune('Æ'), F6(768), new Rect(new Point(0, -9), new Point(12, 0))),
        new(new Rune('i'), F6(189), new Rect(new Point(0, -9), new Point(3, 0))),
        new(new Rune('x'), F6(384), new Rect(new Point(0, -7), new Point(6, 0))),
    };

    [Fact]
    public void FaceGlyphAdvance()
    {
        foreach (var test in RuneTests)
        {
            Int26_6 got = Regular.GetGlyphAdvance(test.R);
            if (got != test.Advance)
            {
                Assert.Fail($"U+{test.R.Value:X4}: glyph advance width={got}. want={test.Advance}");
            }
        }
    }

    [Fact]
    public void FaceGlyphBounds()
    {
        foreach (var test in RuneTests)
        {
            var gb = Regular.GetGlyphBounds(test.R);
            var bounds = gb.Bounds;
            var advance = gb.Advance;
            bool found = gb.Found;
            if (!found)
            {
                Assert.Fail($"U+{test.R.Value:X4}: could not get glyph bounds");
            }

            // bounds must fit inside the draw rect.
            var testFixedBounds = new Rectangle26_6(Int26_6.FromInt(test.Dr.Min.X), Int26_6.FromInt(test.Dr.Min.Y), Int26_6.FromInt(test.Dr.Max.X), Int26_6.FromInt(test.Dr.Max.Y));
            if (!bounds.In(testFixedBounds))
            {
                Assert.Fail($"U+{test.R.Value:X4}: glyph bounds {bounds} must be inside {testFixedBounds}");
            }
            if (advance != test.Advance)
            {
                Assert.Fail($"U+{test.R.Value:X4}: glyph advance width={advance}. want={test.Advance}");
            }
        }
    }

    [Fact]
    public void FaceGlyph()
    {
        var dot = new Point(200, 500);
        var fixedDot = new Point26_6(Int26_6.FromInt(dot.X), Int26_6.FromInt(dot.Y));

        foreach (var test in RuneTests)
        {
            var gm = Regular.GetGlyph(fixedDot, test.R);
            if (!gm.Found)
            {
                Assert.Fail($"U+{test.R.Value:X4}: could not get glyph");
            }
            var wantDr = test.Dr.Add(dot);
            if (!gm.Bounds.Eq(wantDr))
            {
                Assert.Fail($"U+{test.R.Value:X4}: glyph draw rectangle={gm.Bounds}. want={wantDr}");
            }
            var wantMaskBounds = new Rect(new Point(0, 0), new Point(test.Dr.Dx(), test.Dr.Dy()));
            if (!gm.Mask!.Bounds().Eq(wantMaskBounds))
            {
                Assert.Fail($"U+{test.R.Value:X4}: glyph mask rectangle={gm.Mask.Bounds()}. want={wantMaskBounds}");
            }
            if (!gm.MaskOrigin.Eq(new Point()))
            {
                Assert.Fail($"U+{test.R.Value:X4}: glyph maskp={gm.MaskOrigin}. want=(0,0)");
            }
            if (gm.Advance != test.Advance)
            {
                Assert.Fail($"U+{test.R.Value:X4}: glyph advance width={gm.Advance}. want={test.Advance}");
            }
        }
    }

    [Fact]
    public void FaceKern()
    {
        // There is no kerning with gofont/goregular.
        foreach (var (r1, r2, want) in new[]
        {
            ('A', 'A', 0),
            ('A', 'V', 0),
            ('V', 'A', 0),
            ('A', 'v', 0),
            ('W', 'a', 0),
            ('W', 'i', 0),
            ('Y', 'i', 0),
            ('f', '(', 0),
            ('f', 'f', 0),
            ('f', 'i', 0),
            ('T', 'a', 0),
            ('T', 'e', 0),
        })
        {
            Int26_6 got = Regular.GetKern(new Rune(r1), new Rune(r2));
            if (got != F6(want))
            {
                Assert.Fail($"({r1}, {r2}): glyph kerning={got}. want={want}");
            }
        }
    }

    [Fact]
    public void FaceMetrics()
    {
        var want = new FontMetrics(F6(888), F6(726), F6(162), F6(407), F6(555), new Point(0, 1));
        Assert.Equal(want, Regular.Metrics);
    }

    [Fact]
    public void ExampleNewFace()
    {
        const int width = 72;
        const int height = 36;
        const int startingDotX = 6;
        const int startingDotY = 28;

        var f = OpenTypeFont.Parse(GoItalic);
        var face = OpenTypeFont.NewFace(f, new FaceOptions
        {
            Size = 32,
            DPI = 72,
            Hinting = Hinting.None,
        });

        var dst = Images.NewGray(new Rect(new Point(0, 0), new Point(width, height)));
        var d = new Drawer
        {
            Dst = dst,
            Src = UniformImages.White,
            Face = face,
            Dot = new Point26_6(Int26_6.FromInt(startingDotX), Int26_6.FromInt(startingDotY)),
        };
        var dot0 = $"The dot is at {d.Dot}";
        d.DrawString("jel");
        var dot1 = $"The dot is at {d.Dot}";
        d.Src = UniformImages.NewUniform(new Gray(0x7f));
        d.DrawString("ly");
        var dot2 = $"The dot is at {d.Dot}";

        const string asciiArt = ".++8";
        var buf = new StringBuilder();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char c = asciiArt[dst.GrayAt(x, y).Y >> 6];
                if (c != '.')
                {
                    // No-op.
                }
                else if (x == startingDotX - 1)
                {
                    c = ']';
                }
                else if (y == startingDotY - 1)
                {
                    c = '_';
                }
                buf.Append(c);
            }
            buf.Append('\n');
        }

        string want =
            "The dot is at (6:00, 28:00)\n" +
            "The dot is at (41:32, 28:00)\n" +
            "The dot is at (66:48, 28:00)\n" +
            ".....]..................................................................\n" +
            ".....]..................................................................\n" +
            ".....]..................................................................\n" +
            ".....]..................................+++......+++....................\n" +
            ".....]........+++.......................888......+++....................\n" +
            ".....].......+88+......................+88+......+++....................\n" +
            ".....].......888+......................+88+.....+++.....................\n" +
            ".....].......888+......................+88+.....+++.....................\n" +
            ".....].................................888......+++.....................\n" +
            ".....].................................888......+++.....................\n" +
            ".....]....................++..........+88+......+++.....................\n" +
            ".....]......+88+.......+888888+.......+88+.....+++....+++..........++...\n" +
            ".....]......888......+888888888+......+88+.....+++....++++........+++...\n" +
            ".....]......888.....+888+...+888......888......+++.....+++........++....\n" +
            ".....].....+888....+888......+88+.....888......+++.....+++.......+++....\n" +
            ".....].....+88+....888.......+88+....+88+......+++.....+++......+++.....\n" +
            ".....].....+88+...+888.......+88+....+88+.....+++......+++......+++.....\n" +
            ".....].....888....888+++++++++88+....+88+.....+++......+++.....+++......\n" +
            ".....].....888....88888888888888+....888......+++......++++....++.......\n" +
            ".....]....+888...+88888888888888.....888......+++.......+++...+++.......\n" +
            ".....]....+88+...+888...............+888......+++.......+++..+++........\n" +
            ".....]....+88+...+888...............+88+.....+++........+++..+++........\n" +
            ".....]....888....+888...............+88+.....+++........+++.+++.........\n" +
            ".....]....888....+888...............888......+++........++++++..........\n" +
            ".....]...+888.....888+..............888......+++........++++++..........\n" +
            ".....]...+88+.....+8888+....++8.....888+.....++++........++++...........\n" +
            ".....]...+88+......+8888888888+.....+8888....+++++.......++++...........\n" +
            "_____]___888________+88888888++______+888_____++++_______+++____________\n" +
            ".....]...888...........+++.............++................+++............\n" +
            ".....]..+88+............................................+++.............\n" +
            ".....]..+88+...........................................+++..............\n" +
            ".....].+888............................................+++..............\n" +
            "....888888............................................+++...............\n" +
            "....88888............................................++++...............\n" +
            "....+++.................................................................\n" +
            ".....]..................................................................\n";
        Assert.Equal(want, dot0 + "\n" + dot1 + "\n" + dot2 + "\n" + buf);
    }
}