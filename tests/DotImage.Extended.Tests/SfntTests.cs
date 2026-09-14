using System;
using System.Collections.Generic;
using System.IO;
using DotImage.Extended.Font;
using DotImage.Extended.Math.Fixed;
using DotImage.Extended.Font.Sfnt;
using SfntFont = DotImage.Extended.Font.Sfnt.Font;
using SfntBuffer = DotImage.Extended.Font.Sfnt.Buffer;
using Xunit;

namespace DotImage.Extended.Tests;

// Ported from golang.org/x/image/font/sfnt/sfnt_test.go.
public class SfntTests
{
    private static readonly string TestDataDir = FindTestDataDir();
    private static readonly byte[] GoBold = LoadFont("sfnt/Go-Bold.ttf");
    private static readonly byte[] GoMono = LoadFont("sfnt/Go-Mono.ttf");
    private static readonly byte[] GoRegular = LoadFont("sfnt/Go-Regular.ttf");
    private static readonly byte[] CmapTest = LoadFont("testdata/cmapTest.ttf");
    private static readonly byte[] GlyfTest = LoadFont("testdata/glyfTest.ttf");
    private static readonly byte[] CFFTest = LoadFont("testdata/CFFTest.otf");

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

    private static Point26_6 Pt(int x, int y) => new(F6(x), F6(y));

    private static Int26_6 F6(int v) => Int26_6.FromRaw(v);

    private static Rectangle26_6 Rect(Point26_6 min, Point26_6 max) =>
        new(min.X, min.Y, max.X, max.Y);

    private static Segment MoveTo(int xa, int ya) =>
        new(SegmentOp.MoveTo, Pt(xa, ya));

    private static Segment LineTo(int xa, int ya) =>
        new(SegmentOp.LineTo, Pt(xa, ya));

    private static Segment QuadTo(int xa, int ya, int xb, int yb) =>
        new(SegmentOp.QuadTo, Pt(xa, ya), Pt(xb, yb));

    private static Segment CubeTo(int xa, int ya, int xb, int yb, int xc, int yc) =>
        new(SegmentOp.CubeTo, Pt(xa, ya), Pt(xb, yb), Pt(xc, yc));

    private static Segment Translate(int dx, int dy, Segment s)
    {
        for (int j = 0; j < 3; j++)
        {
            s.SetArgs(j, s.Args(j).Add(Pt(dx, dy)));
        }
        return s;
    }

    private static Segment Transform(int txx, int txy, int tyx, int tyy, int dx, int dy, Segment s)
    {
        const long half = 1L << 13;
        long ddx = F6(dx).Raw, ddy = F6(dy).Raw;
        for (int j = 0; j < 3; j++)
        {
            var p = s.Args(j);
            long x = p.X.Raw, y = p.Y.Raw;
            s.SetArgs(j, new Point26_6(
                Int26_6.FromRaw(ddx + ((x * txx + half) >> 14) + ((y * tyx + half) >> 14)),
                Int26_6.FromRaw(ddy + ((x * txy + half) >> 14) + ((y * tyy + half) >> 14))));
        }
        return s;
    }

    private static byte[] FontData(string name) => name switch
    {
        "gobold" => GoBold,
        "gomono" => GoMono,
        "goregular" => GoRegular,
        _ => throw new InvalidOperationException("unreachable"),
    };

    private static ushort GetBE16(byte[] b, int i) => (ushort)((b[i] << 8) | b[i + 1]);

    private static uint GetBE32(byte[] b, int i) =>
        ((uint)b[i] << 24) | ((uint)b[i + 1] << 16) | ((uint)b[i + 2] << 8) | b[i + 3];

    private static void PutBE16(byte[] b, int i, int v)
    {
        b[i] = (byte)(v >> 8);
        b[i + 1] = (byte)v;
    }

    private static void PutBE32(byte[] b, int i, uint v)
    {
        b[i] = (byte)(v >> 24);
        b[i + 1] = (byte)(v >> 16);
        b[i + 2] = (byte)(v >> 8);
        b[i + 3] = (byte)v;
    }

    // Ports gpos_test.makeFontWithGPOSSubtable, generalized to append an
    // arbitrary new table (kept in ascending tag order) after the base font.
    private static byte[] MakeFontWithExtraTable(byte[] baseFont, uint tag, byte[] tableData)
    {
        int numTables = GetBE16(baseFont, 4);
        if (numTables != 14)
        {
            throw new InvalidOperationException($"expected 14 tables, got {numTables}");
        }

        // The new table is appended after the base font data, which is shifted
        // one table record (16 bytes) to make room for the extra record, and
        // 4-byte aligned.
        int newTableOffset = (int)(((uint)(baseFont.Length + 16 + 3)) & ~3u);
        int padding = newTableOffset - (baseFont.Length + 16);

        var newFont = new byte[newTableOffset + tableData.Length];
        Array.Copy(baseFont, 0, newFont, 0, 12);
        PutBE16(newFont, 4, numTables + 1);
        PutBE16(newFont, 6, 128);
        PutBE16(newFont, 8, 3);
        PutBE16(newFont, 10, 112);

        var records = new (uint tag, uint checksum, uint offset, uint length)[numTables + 1];
        for (int i = 0; i < numTables; i++)
        {
            int r = 12 + 16 * i;
            records[i] = (
                GetBE32(baseFont, r),
                GetBE32(baseFont, r + 4),
                GetBE32(baseFont, r + 8) + 16, // table data shifts by one record
                GetBE32(baseFont, r + 12));
        }
        records[numTables] = (tag, 0, (uint)newTableOffset, (uint)tableData.Length);
        Array.Sort(records, (a, b) => a.tag.CompareTo(b.tag));

        for (int i = 0; i < records.Length; i++)
        {
            int r = 12 + 16 * i;
            PutBE32(newFont, r, records[i].tag);
            PutBE32(newFont, r + 4, records[i].checksum);
            PutBE32(newFont, r + 8, records[i].offset);
            PutBE32(newFont, r + 12, records[i].length);
        }

        Array.Copy(baseFont, 236, newFont, 252, baseFont.Length - 236);
        for (int i = 0; i < padding; i++)
        {
            newFont[newTableOffset - padding + i] = 0;
        }
        Array.Copy(tableData, 0, newFont, newTableOffset, tableData.Length);
        return newFont;
    }

    private static void CheckSegmentsEqual(Segments gotSegs, Segment[] want)
    {
        var got = gotSegs.Items;

        // Flip got's Y axis; the test data uses Y-up coordinates (as given by
        // ttx), while the API returns Y-down coordinates.
        for (int i = 0; i < got.Count; i++)
        {
            var s = got[i];
            for (int j = 0; j < 3; j++)
            {
                var p = s.Args(j);
                s.SetArgs(j, new Point26_6(p.X, -p.Y));
            }
            got[i] = s;
        }

        Assert.True(got.Count == want.Length,
            $"got {got.Count} elements, want {want.Length}");
        for (int i = 0; i < got.Count; i++)
        {
            Assert.True(got[i] == want[i], $"element {i}:\ngot  {got[i]}\nwant {want[i]}");
        }

        // Check that every contour is closed.
        if (got.Count == 0)
        {
            return;
        }
        Assert.True(got[0].Op == SegmentOp.MoveTo, "segments do not start with a moveTo");
        Point26_6 first = default, last = default;
        int firstI = 0;
        for (int i = 0; i < got.Count; i++)
        {
            var g = got[i];
            switch (g.Op)
            {
                case SegmentOp.MoveTo:
                    if (i != 0)
                    {
                        Assert.True(first == last,
                            $"segments[{firstI}:{i}] not closed:\nfirst {first}\nlast  {last}");
                    }
                    firstI = i;
                    first = g.Args(0);
                    last = g.Args(0);
                    break;
                case SegmentOp.LineTo:
                    last = g.Args(0);
                    break;
                case SegmentOp.QuadTo:
                    last = g.Args(1);
                    break;
                case SegmentOp.CubeTo:
                    last = g.Args(2);
                    break;
            }
        }
        Assert.True(first == last,
            $"segments[{firstI}:{got.Count}] not closed:\nfirst {first}\nlast  {last}");
    }

    [Fact]
    public void TrueTypeParse()
    {
        var f = SfntFont.Parse(GoRegular);
        int unitsPerEm = f.UnitsPerEm.Value;
        Assert.Equal(2048, unitsPerEm);
        Assert.True(f.NumGlyphs > 650, $"NumGlyphs: got {f.NumGlyphs}, want > 650");

        using var ms = new MemoryStream();
        long n = f.WriteSourceTo(null, ms);
        byte[] gotSrc = ms.ToArray();

        long got = (n + 3) & ~3L;
        long have = ((long)GoRegular.Length + 3) & ~3L;

        Assert.True(n <= GoRegular.Length && got == have,
            $"WriteSourceTo: got {n}, want {GoRegular.Length} (with rounding)");
        Assert.Equal(gotSrc, GoRegular.AsSpan(0, (int)n).ToArray());
    }

    [Fact]
    public void Bounds()
    {
        var testCases = new[]
        {
            new { name = "gobold", want = Rect(Pt(-452, -2291), Pt(2190, 492)) },
            new { name = "gomono", want = Rect(Pt(0, -2291), Pt(1229, 432)) },
            new { name = "goregular", want = Rect(Pt(-440, -2291), Pt(2160, 543)) },
        };

        var b = new SfntBuffer();
        foreach (var tc in testCases)
        {
            var f = SfntFont.Parse(FontData(tc.name));
            Int26_6 ppem = Int26_6.FromRaw(f.UnitsPerEm.Value);
            var got = f.Bounds(b, ppem, Hinting.None);
            Assert.True(got == tc.want, $"name={tc.name}: Bounds: got {got}, want {tc.want}");
        }
    }

    [Fact]
    public void Metrics()
    {
        var b = new SfntBuffer();

        var goregular = SfntFont.Parse(GoRegular);
        var ppem = Int26_6.FromRaw(goregular.UnitsPerEm.Value);
        var got = goregular.Metrics(b, ppem, Hinting.None);
        Assert.Equal(
            new FontMetrics(
                height: F6(2367), ascent: F6(1935), descent: F6(432),
                xHeight: F6(1086), capHeight: F6(1480),
                caretSlope: new Point(0, 1)),
            got);

        var cmap = SfntFont.Parse(CmapTest);
        ppem = Int26_6.FromRaw(cmap.UnitsPerEm.Value);
        got = cmap.Metrics(b, ppem, Hinting.None);
        // cmapTest.ttf has a non-zero lineGap.
        Assert.Equal(
            new FontMetrics(
                height: F6(1549), ascent: F6(1365), descent: F6(0),
                xHeight: F6(800), capHeight: F6(800),
                caretSlope: new Point(20, 100)),
            got);
    }

    [Fact]
    public void GlyphBounds()
    {
        var f = SfntFont.Parse(GoRegular);
        Int26_6 ppem = Int26_6.FromRaw(f.UnitsPerEm.Value);

        var testCases = new[]
        {
            new { r = (int)' ', wantBounds = Rect(Pt(0, 0), Pt(0, 0)), wantAdv = F6(569) },
            new { r = (int)'A', wantBounds = Rect(Pt(19, -1480), Pt(1342, 0)), wantAdv = F6(1366) },
            new { r = (int)'Á', wantBounds = Rect(Pt(19, -1935), Pt(1342, 0)), wantAdv = F6(1366) },
            new { r = (int)'Æ', wantBounds = Rect(Pt(19, -1480), Pt(1990, 0)), wantAdv = F6(2048) },
            new { r = (int)'i', wantBounds = Rect(Pt(144, -1500), Pt(361, 0)), wantAdv = F6(505) },
            new { r = (int)'j', wantBounds = Rect(Pt(-84, -1500), Pt(387, 419)), wantAdv = F6(519) },
            new { r = (int)'x', wantBounds = Rect(Pt(28, -1086), Pt(993, 0)), wantAdv = F6(1024) },
        };

        var b = new SfntBuffer();
        foreach (var tc in testCases)
        {
            var gi = f.GlyphIndex(b, tc.r);
            var (gotBounds, gotAdv) = f.GlyphBounds(b, gi, ppem, Hinting.None);
            Assert.True(gotBounds == tc.wantBounds, $"r={tc.r}: Bounds: got {gotBounds}, want {tc.wantBounds}");
            Assert.True(gotAdv == tc.wantAdv, $"r={tc.r}: Adv: got {gotAdv}, want {tc.wantAdv}");
        }
    }

    [Fact]
    public void GlyphAdvance()
    {
        var testCases = new[]
        {
            new { name = "gobold", cases = new[] {
                new { r = (int)' ', want = F6(569) },
                new { r = (int)'A', want = F6(1479) },
                new { r = (int)'Á', want = F6(1479) },
                new { r = (int)'Æ', want = F6(2048) },
                new { r = (int)'i', want = F6(592) },
                new { r = (int)'x', want = F6(1139) },
            } },
            new { name = "gomono", cases = new[] {
                new { r = (int)' ', want = F6(1229) },
                new { r = (int)'A', want = F6(1229) },
                new { r = (int)'Á', want = F6(1229) },
                new { r = (int)'Æ', want = F6(1229) },
                new { r = (int)'i', want = F6(1229) },
                new { r = (int)'x', want = F6(1229) },
            } },
            new { name = "goregular", cases = new[] {
                new { r = (int)' ', want = F6(569) },
                new { r = (int)'A', want = F6(1366) },
                new { r = (int)'Á', want = F6(1366) },
                new { r = (int)'Æ', want = F6(2048) },
                new { r = (int)'i', want = F6(505) },
                new { r = (int)'x', want = F6(1024) },
            } },
        };

        var b = new SfntBuffer();
        foreach (var name in testCases)
        {
            var f = SfntFont.Parse(FontData(name.name));
            Int26_6 ppem = Int26_6.FromRaw(f.UnitsPerEm.Value);
            foreach (var tc in name.cases)
            {
                var x = f.GlyphIndex(b, tc.r);
                var got = f.GlyphAdvance(b, x, ppem, Hinting.None);
                Assert.True(got == tc.want, $"name={name.name}, r={tc.r}: GlyphAdvance: got {got}, want {tc.want}");
            }
        }
    }

    [Fact]
    public void GoRegularGlyphIndex()
    {
        var f = SfntFont.Parse(GoRegular);

        var testCases = new[]
        {
            new { r = (int)'\u001f', want = 0 }, // U+001F <control>
            new { r = (int)'\u0200', want = 0 }, // U+0200 LATIN CAPITAL LETTER A WITH DOUBLE GRAVE
            new { r = (int)'\u2000', want = 0 }, // U+2000 EN QUAD

            new { r = (int)'\u0020', want = 3 },  // U+0020 SPACE
            new { r = (int)'\u0021', want = 4 },  // U+0021 EXCLAMATION MARK
            new { r = (int)'\u0022', want = 5 },  // U+0022 QUOTATION MARK
            new { r = (int)'\u0023', want = 6 },  // U+0023 NUMBER SIGN
            new { r = (int)'\u0024', want = 7 },  // U+0024 DOLLAR SIGN
            new { r = (int)'\u0025', want = 8 },  // U+0025 PERCENT SIGN
            new { r = (int)'\u0026', want = 9 },  // U+0026 AMPERSAND
            new { r = (int)'\u0027', want = 10 }, // U+0027 APOSTROPHE

            new { r = (int)'\u03bd', want = 413 }, // U+03BD GREEK SMALL LETTER NU
            new { r = (int)'\u03be', want = 414 }, // U+03BE GREEK SMALL LETTER XI
            new { r = (int)'\u03bf', want = 415 }, // U+03BF GREEK SMALL LETTER OMICRON
            new { r = (int)'\u03c0', want = 416 }, // U+03C0 GREEK SMALL LETTER PI
            new { r = (int)'\u03c1', want = 417 }, // U+03C1 GREEK SMALL LETTER RHO
            new { r = (int)'\u03c2', want = 418 }, // U+03C2 GREEK SMALL LETTER FINAL SIGMA
        };

        var b = new SfntBuffer();
        foreach (var tc in testCases)
        {
            var got = f.GlyphIndex(b, tc.r);
            Assert.True(got == (GlyphIndex)tc.want, $"r={tc.r}: got {got}, want {tc.want}");
        }
    }

    [Fact]
    public void GlyphIndex()
    {
        foreach (int format in new[] { -1, 0, 4, 12 })
        {
            TestGlyphIndex(CmapTest, format);
        }
    }

    private void TestGlyphIndex(byte[] data, int cmapFormat)
    {
        var originalSupportedCmapFormat = Cmap.SupportedCmapFormat;
        try
        {
            if (cmapFormat >= 0)
            {
                Cmap.SupportedCmapFormat = (format, pid, psid) =>
                    (int)format == cmapFormat && originalSupportedCmapFormat(format, pid, psid);
            }

            var f = SfntFont.Parse(data);
            var testCases = new[]
            {
                // Glyphs that aren't present in cmapTest.ttf.
                new { r = (int)'?', want = 0 },
                new { r = (int)'\ufffd', want = 0 },
                new { r = 0x1f4a9, want = 0 },
                new { r = (int)'/', want = 0 },
                new { r = (int)'0', want = 3 },
                new { r = (int)'1', want = 4 },
                new { r = (int)'2', want = 5 },
                new { r = (int)'3', want = 0 },
                new { r = (int)'@', want = 0 },
                new { r = (int)'A', want = 6 },
                new { r = (int)'B', want = 7 },
                new { r = (int)'C', want = 0 },
                new { r = (int)'`', want = 0 },
                new { r = (int)'a', want = 8 },
                new { r = (int)'b', want = 0 },
                new { r = (int)'\u00fe', want = 0 },
                new { r = (int)'\u00ff', want = 9 },
                new { r = (int)'\u0100', want = 10 },
                new { r = (int)'\u0101', want = 11 },
                new { r = (int)'\u0102', want = 0 },
                new { r = (int)'\u4e2c', want = 0 },
                new { r = (int)'\u4e2d', want = 12 },
                new { r = (int)'\u4e2e', want = 0 },
                new { r = 0x1f0a0, want = 0 },
                new { r = 0x1f0a1, want = 13 },
                new { r = 0x1f0a2, want = 0 },
                new { r = 0x1f0b0, want = 0 },
                new { r = 0x1f0b1, want = 14 },
                new { r = 0x1f0b2, want = 15 },
                new { r = 0x1f0b3, want = 0 },
            };

            var b = new SfntBuffer();
            foreach (var tc in testCases)
            {
                int want = tc.want;
                switch (cmapFormat)
                {
                    case 0 when tc.r > 0x7f && tc.r != 0xff:
                        // cmap format 0, Macintosh Roman, can only represent a
                        // limited set of non-ASCII runes, e.g. U+00FF.
                        want = 0;
                        break;
                    case 4 when tc.r > 0xffff:
                        // cmap format 4 only supports the BMP.
                        want = 0;
                        break;
                }

                var got = f.GlyphIndex(b, tc.r);
                Assert.True(got == (GlyphIndex)want, $"cmapFormat={cmapFormat}, r={tc.r}: got {got}, want {want}");
            }
        }
        finally
        {
            Cmap.SupportedCmapFormat = originalSupportedCmapFormat;
        }
    }

    [Fact]
    public void TrueTypeSegments()
    {
        // wants' vectors correspond 1-to-1 to glyfTest.ttf.
        var wants = new[]
        {
            new Segment[] {
                // .notdef
                MoveTo(68, 0), LineTo(68, 1365), LineTo(612, 1365), LineTo(612, 0), LineTo(68, 0),
                MoveTo(136, 68), LineTo(544, 68), LineTo(544, 1297), LineTo(136, 1297), LineTo(136, 68),
            },
            new Segment[] {
                // .null
            },
            new Segment[] {
                // nonmarkingreturn
            },
            new Segment[] {
                // zero
                MoveTo(614, 1434), QuadTo(369, 1434, 369, 614), QuadTo(369, 471, 435, 338),
                QuadTo(502, 205, 614, 205), QuadTo(860, 205, 860, 1024), QuadTo(860, 1167, 793, 1300),
                QuadTo(727, 1434, 614, 1434),
                MoveTo(614, 1638), QuadTo(1024, 1638, 1024, 819), QuadTo(1024, 0, 614, 0),
                QuadTo(205, 0, 205, 819), QuadTo(205, 1638, 614, 1638),
            },
            new Segment[] {
                // one
                MoveTo(205, 0), LineTo(205, 1638), LineTo(614, 1638), LineTo(614, 0), LineTo(205, 0),
            },
            new Segment[] {
                // five
                MoveTo(0, 0), LineTo(0, 100), LineTo(400, 100), LineTo(400, 0), LineTo(0, 0),
            },
            new Segment[] {
                // six
                MoveTo(0, 0), LineTo(0, 100), LineTo(400, 100), LineTo(400, 0), LineTo(0, 0),
                Translate(111, 234, MoveTo(205, 0)), Translate(111, 234, LineTo(205, 1638)),
                Translate(111, 234, LineTo(614, 1638)), Translate(111, 234, LineTo(614, 0)),
                Translate(111, 234, LineTo(205, 0)),
            },
            new Segment[] {
                // seven
                MoveTo(0, 0), LineTo(0, 100), LineTo(400, 100), LineTo(400, 0), LineTo(0, 0),
                Transform(1 << 13, 0, 0, 1 << 13, 56, 117, MoveTo(205, 0)),
                Transform(1 << 13, 0, 0, 1 << 13, 56, 117, LineTo(205, 1638)),
                Transform(1 << 13, 0, 0, 1 << 13, 56, 117, LineTo(614, 1638)),
                Transform(1 << 13, 0, 0, 1 << 13, 56, 117, LineTo(614, 0)),
                Transform(1 << 13, 0, 0, 1 << 13, 56, 117, LineTo(205, 0)),
            },
            new Segment[] {
                // eight
                MoveTo(0, 0), LineTo(0, 100), LineTo(400, 100), LineTo(400, 0), LineTo(0, 0),
                Transform(3 << 13, 0, 0, 1 << 13, 56, 117, MoveTo(205, 0)),
                Transform(3 << 13, 0, 0, 1 << 13, 56, 117, LineTo(205, 1638)),
                Transform(3 << 13, 0, 0, 1 << 13, 56, 117, LineTo(614, 1638)),
                Transform(3 << 13, 0, 0, 1 << 13, 56, 117, LineTo(614, 0)),
                Transform(3 << 13, 0, 0, 1 << 13, 56, 117, LineTo(205, 0)),
            },
            new Segment[] {
                // nine
                MoveTo(0, 0), LineTo(0, 100), LineTo(400, 100), LineTo(400, 0), LineTo(0, 0),
                Transform(22381, 8192, 5996, 14188, 237, 258, MoveTo(205, 0)),
                Transform(22381, 8192, 5996, 14188, 237, 258, LineTo(205, 1638)),
                Transform(22381, 8192, 5996, 14188, 237, 258, LineTo(614, 1638)),
                Transform(22381, 8192, 5996, 14188, 237, 258, LineTo(614, 0)),
                Transform(22381, 8192, 5996, 14188, 237, 258, LineTo(205, 0)),
            },
        };

        TestSegments(GlyfTest, "glyfTest", wants);
    }

    private void TestSegments(byte[] data, string name, Segment[][] wants)
    {
        var f = SfntFont.Parse(data);
        Int26_6 ppem = Int26_6.FromRaw(f.UnitsPerEm.Value);

        int ng = f.NumGlyphs;
        Assert.True(ng == wants.Length, $"NumGlyphs: got {ng}, want {wants.Length}");
        var b = new SfntBuffer();
        for (int i = 0; i < wants.Length; i++)
        {
            var got = f.LoadGlyph(b, (GlyphIndex)i, ppem, null);
            CheckSegmentsEqual(got, wants[i]);
        }
        var ex = Record.Exception(() => f.LoadGlyph(null, (GlyphIndex)0xffff, ppem, null));
        Assert.Equal(SfntErrors.ErrNotFound, ex?.Message);

        Assert.Equal(name, f.Name(null, NameIDConstants.Family));
    }

    [Fact]
    public void PostScriptSegments()
    {
        // wants' vectors correspond 1-to-1 to what's in the CFFTest.sfd file,
        // although OpenType/CFF and FontForge's SFD have reversed orders.
        // https://fontforge.github.io/validation.html says that "All paths must be
        // drawn in a consistent direction. Clockwise for external paths,
        // anti-clockwise for internal paths. (Actually PostScript requires the
        // exact opposite, but FontForge reverses PostScript contours when it loads
        // them so that everything is consistent internally -- and reverses them
        // again when it saves them, of course)."
        //
        // The .notdef glyph isn't explicitly in the SFD file, but for some unknown
        // reason, FontForge generates it in the OpenType/CFF file.
        var wants = new[]
        {
            new Segment[]
            {
                // .notdef
                // - contour #0
                MoveTo(50, 0), LineTo(450, 0), LineTo(450, 533), LineTo(50, 533), LineTo(50, 0),
                // - contour #1
                MoveTo(100, 50), LineTo(100, 483), LineTo(400, 483), LineTo(400, 50), LineTo(100, 50),
            },
            new Segment[]
            {
                // zero
                // - contour #0
                MoveTo(300, 700), CubeTo(380, 700, 420, 580, 420, 500), CubeTo(420, 350, 390, 100, 300, 100),
                CubeTo(220, 100, 180, 220, 180, 300), CubeTo(180, 450, 210, 700, 300, 700),
                // - contour #1
                MoveTo(300, 800), CubeTo(200, 800, 100, 580, 100, 400), CubeTo(100, 220, 200, 0, 300, 0),
                CubeTo(400, 0, 500, 220, 500, 400), CubeTo(500, 580, 400, 800, 300, 800),
            },
            new Segment[]
            {
                // one
                // - contour #0
                MoveTo(100, 0), LineTo(300, 0), LineTo(300, 800), LineTo(100, 800), LineTo(100, 0),
            },
            new Segment[]
            {
                // Q
                // - contour #0
                MoveTo(657, 237), LineTo(289, 387), LineTo(519, 615), LineTo(657, 237),
                // - contour #1
                MoveTo(792, 169), CubeTo(867, 263, 926, 502, 791, 665), CubeTo(645, 840, 380, 831, 228, 673),
                CubeTo(71, 509, 110, 231, 242, 93), CubeTo(369, -39, 641, 18, 722, 93),
                LineTo(802, 3), LineTo(864, 83), LineTo(792, 169),
            },
            new Segment[]
            {
                // uni4E2D
                // - contour #0
                MoveTo(141, 520), LineTo(137, 356), LineTo(245, 400), LineTo(331, 26), LineTo(355, 414),
                LineTo(463, 434), LineTo(453, 620), LineTo(341, 592), LineTo(331, 758), LineTo(243, 752),
                LineTo(235, 562), LineTo(141, 520),
            },
        };

        TestSegments(CFFTest, "CFFTest", wants);
    }

    [Fact]
    public void PPEM()
    {
        var f = SfntFont.Parse(GlyfTest);
        var b = new SfntBuffer();
        var x = f.GlyphIndex(b, '1');
        Assert.True(x != 0, "GlyphIndex: no glyph index found for the rune '1'");

        var testCases = new[]
        {
            new {
                ppem = new Int26_6(12 << 6),
                want = new[]
                {
                    MoveTo(77, 0), LineTo(77, 614),
                    LineTo(230, 614), LineTo(230, 0),
                    LineTo(77, 0),
                },
            },
            new {
                ppem = F6(2048),
                want = new[]
                {
                    MoveTo(205, 0), LineTo(205, 1638), LineTo(614, 1638), LineTo(614, 0), LineTo(205, 0),
                },
            },
        };

        for (int i = 0; i < testCases.Length; i++)
        {
            var got = f.LoadGlyph(b, x, testCases[i].ppem, null);
            CheckSegmentsEqual(got, testCases[i].want);
        }
    }

    [Fact]
    public void PostInfo()
    {
        var f = SfntFont.Parse(GlyfTest);
        var post = f.PostTable!;
        Assert.Equal(-11.25, post.ItalicAngle);
        Assert.Equal(-255, post.UnderlinePosition);
        Assert.Equal(102, post.UnderlineThickness);
        Assert.False(post.IsFixedPitch);
    }

    [Fact]
    public void GlyphName()
    {
        var f = SfntFont.Parse(GoRegular);

        var testCases = new[]
        {
            new { r = (int)'\x00', want = "uni0000" },
            new { r = (int)'!', want = "exclam" },
            new { r = (int)'A', want = "A" },
            new { r = (int)'{', want = "braceleft" },
            new { r = (int)'\u00c4', want = "Adieresis" }, // U+00C4 LATIN CAPITAL LETTER A WITH DIAERESIS
            new { r = (int)'\u2020', want = "dagger" },    // U+2020 DAGGER
            new { r = (int)'\u2660', want = "spade" },     // U+2660 BLACK SPADE SUIT
            new { r = (int)'\uf800', want = "gopher" },    // U+F800 <Private Use>
            new { r = (int)'\ufffe', want = ".notdef" },   // Not in Go Regular.
        };

        var b = new SfntBuffer();
        foreach (var tc in testCases)
        {
            var x = f.GlyphIndex(b, tc.r);
            var got = f.GlyphName(b, x);
            Assert.True(got == tc.want, $"r={tc.r}: got {got}, want {tc.want}");
        }
    }

    [Fact]
    public void BuiltInPostNames()
    {
        var testCases = new[]
        {
            new { x = 0, want = ".notdef" },
            new { x = 1, want = ".null" },
            new { x = 2, want = "nonmarkingreturn" },
            new { x = 13, want = "asterisk" },
            new { x = 36, want = "A" },
            new { x = 93, want = "z" },
            new { x = 123, want = "ocircumflex" },
            new { x = 202, want = "Edieresis" },
            new { x = 255, want = "Ccaron" },
            new { x = 256, want = "ccaron" },
            new { x = 257, want = "dcroat" },
            new { x = 258, want = "" },
            new { x = 999, want = "" },
            new { x = 0xffff, want = "" },
        };

        foreach (var tc in testCases)
        {
            if (tc.x >= SfntData.NumBuiltInPostNames)
            {
                continue;
            }
            int i = SfntData.BuiltInPostNamesOffsets[tc.x];
            int j = SfntData.BuiltInPostNamesOffsets[tc.x + 1];
            string got = SfntData.BuiltInPostNamesData.Substring(i, j - i);
            Assert.True(got == tc.want, $"x={tc.x}: got {got}, want {tc.want}");
        }
    }

    // Ported from gpos_test.TestGPOSFormatErrors. Both malformed PairPos
    // subtables are inside bounds so that parsing succeeds; the error is only
    // hit when the kerning value is actually looked up.
    private static readonly byte[] GposHeader = new byte[]
    {
        0x00, 0x01, 0x00, 0x00, // Version 1.0
        0x00, 0x0a, // ScriptListOffset = 10
        0x00, 0x1e, // FeatureListOffset = 30
        0x00, 0x2c, // LookupListOffset = 44
        0x00, 0x01, // scriptCount = 1
        0x44, 0x46, 0x4c, 0x54, // scriptTag = 'DFLT'
        0x00, 0x08, // scriptOffset = 8
        0x00, 0x04, // defaultLangSysOffset = 4
        0x00, 0x00, // langSysCount = 0
        0x00, 0x00, // lookupOrder = 0
        0xff, 0xff, // requiredFeatureIndex = 0xffff
        0x00, 0x01, // featureIndexCount = 1
        0x00, 0x00, // featureIndices[0] = 0
        0x00, 0x01, // featureCount = 1
        0x6b, 0x65, 0x72, 0x6e, // featureTag = 'kern'
        0x00, 0x08, // featureOffset = 8
        0x00, 0x00, // featureParamsOffset = 0
        0x00, 0x01, // lookupCount = 1
        0x00, 0x00, // lookupListIndices[0] = 0
        0x00, 0x01, // lookupCount = 1
        0x00, 0x04, // lookupOffsets[0] = 4
        0x00, 0x02, // lookupType = 2 (PairPos)
        0x00, 0x00, // lookupFlag = 0
        0x00, 0x01, // subTableCount = 1
        0x00, 0x08, // subTableOffsets[0] = 8
    };

    private static byte[] AppendGposHeader(byte[] subtable)
    {
        var data = new byte[GposHeader.Length + subtable.Length];
        Array.Copy(GposHeader, data, GposHeader.Length);
        Array.Copy(subtable, 0, data, GposHeader.Length, subtable.Length);
        return data;
    }

    public static IEnumerable<object[]> GposSubtableCases()
    {
        yield return new object[]
        {
            // PairPos format 1.
            new byte[]
            {
                0x00, 0x01, // posFormat = 1
                0x00, 0x0e, // coverageOffset = 14
                0x00, 0x04, // valueFormat1 = 0x0004
                0x00, 0x00, // valueFormat2 = 0
                0x00, 0x02, // pairSetCount = 2
                0x00, 0x16, // pairSetOffset[0] = 22
                0x00, 0x18, // pairSetOffset[1] = 24
                0x00, 0x01, // coverage format = 1
                0x00, 0x02, // glyphCount = 2
                0x00, 0x00, // glyphArray[0] = 0
                0x00, 0x01, // glyphArray[1] = 1
                0x03, 0xe8, // pairValueCount = 1000 (overruns the subtable)
                0x00, 0x00, // last PairSet pairValueCount = 0
            },
        };
        yield return new object[]
        {
            // PairPos format 2.
            new byte[]
            {
                0x00, 0x02, // posFormat = 2
                0x00, 0x18, // coverageOffset = 24
                0x00, 0x04, // valueFormat1 = 0x0004
                0x00, 0x00, // valueFormat2 = 0
                0x00, 0x22, // classDef1Offset = 34
                0x00, 0x2a, // classDef2Offset = 42
                0x00, 0x02, // class1Count = 2
                0x00, 0x02, // class2Count = 2
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // class1Records
                0x00, 0x01, // coverage format = 1
                0x00, 0x01, // glyphCount = 1
                0x00, 0x00, // glyphArray[0] = 0
                0x00, 0x00, 0x00, 0x00, // pad to offset 34
                0x00, 0x01, // ClassDef1 format = 1
                0x00, 0x00, // startGlyph = 0
                0x00, 0x01, // glyphCount = 1
                0x03, 0xe7, // classValueArray[0] = 999 (out of class range)
                0x00, 0x01, // ClassDef2 format = 1
                0x00, 0x00, // startGlyph = 0
                0x00, 0x01, // glyphCount = 1
                0x03, 0xe7, // classValueArray[0] = 999
            },
        };
    }

    [Theory]
    [MemberData(nameof(GposSubtableCases))]
    public void GposFormatErrors(byte[] subtable)
    {
        byte[] fontData = MakeFontWithExtraTable(GoRegular, 0x47504f53u, AppendGposHeader(subtable));

        var f = SfntFont.Parse(fontData);

        // Querying glyphs 0, 0, which maps to a malformed pair table, should
        // throw ErrInvalidGPOSTable.
        var ex = Record.Exception(() =>
            f.Kern(new SfntBuffer(), (GlyphIndex)0, (GlyphIndex)0, F6(1280), Hinting.None));
        Assert.True(ex != null, "Kern succeeded; want an error");
        Assert.Equal(SfntErrors.ErrInvalidGPOSTable, ex?.Message);
    }

    [Fact]
    public void KernTable()
    {
        // Synthesize a kern table (version 0, format 0) with two pairs and
        // inject it into Go-Regular. Glyph indexes: space=3, exclam=4,
        // quotedbl=5.
        int n = 2;
        var kern = new byte[18 + 6 * n]; // 4 (header) + 6 + 2 + 6 + 6n
        PutBE16(kern, 0, 0);             // version = 0
        PutBE16(kern, 2, 1);             // nTables = 1
        PutBE16(kern, 4, 0);             // subtable version = 0
        PutBE16(kern, 6, 14 + 6 * n);    // subtable length
        PutBE16(kern, 8, 0x0001);        // coverage: horizontal, format 0
        PutBE16(kern, 10, n);            // nPairs
        PutBE16(kern, 12, 0);            // searchRange (unused by the parser)
        PutBE16(kern, 14, 0);            // entrySelector
        PutBE16(kern, 16, 0);            // rangeShift

        PutBE16(kern, 18, 3); PutBE16(kern, 20, 4); PutBE16(kern, 22, 15);  // (space, exclam) -> +15
        PutBE16(kern, 24, 3); PutBE16(kern, 26, 5); PutBE16(kern, 28, -7);  // (space, quotedbl) -> -7

        var f = SfntFont.Parse(MakeFontWithExtraTable(GoRegular, 0x6b65726eu, kern));
        var b = new SfntBuffer();
        Int26_6 upm = Int26_6.FromRaw(f.UnitsPerEm.Value);

        Assert.True(f.Kern(b, (GlyphIndex)3, (GlyphIndex)4, upm, Hinting.None) == F6(15),
            "kern(space, exclam) at full size");
        Assert.True(f.Kern(b, (GlyphIndex)3, (GlyphIndex)5, upm, Hinting.None) == F6(-7),
            "kern(space, quotedbl) at full size");

        // Half the units per em scales the kern by a half.
        Assert.True(f.Kern(b, (GlyphIndex)3, (GlyphIndex)4, F6(1024), Hinting.None) == F6(8),
            "kern(space, exclam) at half size");
        Assert.True(f.Kern(b, (GlyphIndex)3, (GlyphIndex)5, F6(1024), Hinting.None) == F6(-4),
            "kern(space, quotedbl) at half size");

        // A pair that is not present in the table has zero kern.
        Assert.True(f.Kern(b, (GlyphIndex)3, (GlyphIndex)6, upm, Hinting.None) == F6(0),
            "kern(space, numbersign) not found");

        // Hinting.Full quantizes the kern to the pixel grid (15 raw units -> 0).
        Assert.True(f.Kern(b, (GlyphIndex)3, (GlyphIndex)4, upm, Hinting.Full) == F6(0),
            "kern(space, exclam) quantized");

        // An empty version-0 kern table and an Apple version-1 header are both
        // accepted (the latter is deliberately ignored) and produce zero kern.
        foreach (byte[] header in new[]
        {
            new byte[] { 0x00, 0x00, 0x00, 0x00 }, // version 0, nTables = 0
            new byte[] { 0x00, 0x01, 0x00, 0x00 }, // version 1
        })
        {
            var g = SfntFont.Parse(MakeFontWithExtraTable(GoRegular, 0x6b65726eu, header));
            Assert.True(g.Kern(b, (GlyphIndex)3, (GlyphIndex)4, upm, Hinting.None) == F6(0),
                "kern with an ignored kern table header");
        }
    }

    [Fact]
    public void InvalidGlyphData()
    {
        // Corrupt glyph 0's number-of-contours field to -2 in a copy of
        // glyfTest.ttf, then check that loading it throws ErrInvalidGlyphData.
        var modified = (byte[])GlyfTest.Clone();
        uint glyfOffset = 0, locaOffset = 0, headOffset = 0;
        int numTables = GetBE16(modified, 4);
        for (int i = 0; i < numTables; i++)
        {
            int r = 12 + 16 * i;
            uint tag = GetBE32(modified, r);
            if (tag == 0x676c7966) { glyfOffset = GetBE32(modified, r + 8); }     // "glyf"
            else if (tag == 0x6c6f6361) { locaOffset = GetBE32(modified, r + 8); } // "loca"
            else if (tag == 0x68656164) { headOffset = GetBE32(modified, r + 8); } // "head"
        }

        bool indexToLocFormat = GetBE16(modified, (int)headOffset + 50) != 0;
        int loca0 = indexToLocFormat
            ? (int)GetBE32(modified, (int)locaOffset)
            : 2 * GetBE16(modified, (int)locaOffset);
        int glyph0Offset = (int)glyfOffset + loca0;

        // numContours = -2: not -1 (empty) and not >= 0 (simple glyph).
        modified[glyph0Offset] = 0xff;
        modified[glyph0Offset + 1] = 0xfe;

        var f = SfntFont.Parse(modified);
        var b = new SfntBuffer();
        Int26_6 upm = Int26_6.FromRaw(f.UnitsPerEm.Value);
        var ex = Record.Exception(() => f.LoadGlyph(b, (GlyphIndex)0, upm, null));
        Assert.Equal(SfntErrors.ErrInvalidGlyphData, ex?.Message);
    }
}