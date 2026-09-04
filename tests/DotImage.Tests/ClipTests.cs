// Ported from Go src/image/draw/clip_test.go

using DotImage.Draw;

namespace DotImage.Tests;

public class ClipTests
{
    private sealed record ClipTestCase(
        string Desc,
        Rect R, Rect Dr, Rect Sr, Rect Mr,
        Point Sp, Point Mp,
        bool NilMask,
        Rect R0, Point Sp0, Point Mp0);

    private static readonly ClipTestCase[] ClipTestCases =
    [
        new("basic",
            Geometry.Rect(0, 0, 100, 100), Geometry.Rect(0, 0, 100, 100), Geometry.Rect(0, 0, 100, 100), default,
            default, default, true,
            Geometry.Rect(0, 0, 100, 100), default, default),
        new("clip dr",
            Geometry.Rect(0, 0, 100, 100), Geometry.Rect(40, 40, 60, 60), Geometry.Rect(0, 0, 100, 100), default,
            default, default, true,
            Geometry.Rect(40, 40, 60, 60), Geometry.Pt(40, 40), default),
        new("clip sr",
            Geometry.Rect(0, 0, 100, 100), Geometry.Rect(0, 0, 100, 100), Geometry.Rect(20, 20, 80, 80), default,
            default, default, true,
            Geometry.Rect(20, 20, 80, 80), Geometry.Pt(20, 20), default),
        new("clip dr and sr",
            Geometry.Rect(0, 0, 100, 100), Geometry.Rect(0, 0, 50, 100), Geometry.Rect(20, 20, 80, 80), default,
            default, default, true,
            Geometry.Rect(20, 20, 50, 80), Geometry.Pt(20, 20), default),
        new("clip dr and sr, sp outside sr (top-left)",
            Geometry.Rect(0, 0, 100, 100), Geometry.Rect(0, 0, 50, 100), Geometry.Rect(20, 20, 80, 80), default,
            Geometry.Pt(15, 8), default, true,
            Geometry.Rect(5, 12, 50, 72), Geometry.Pt(20, 20), default),
        new("clip dr and sr, sp outside sr (middle-left)",
            Geometry.Rect(0, 0, 100, 100), Geometry.Rect(0, 0, 50, 100), Geometry.Rect(20, 20, 80, 80), default,
            Geometry.Pt(15, 66), default, true,
            Geometry.Rect(5, 0, 50, 14), Geometry.Pt(20, 66), default),
        new("clip dr and sr, sp outside sr (bottom-left)",
            Geometry.Rect(0, 0, 100, 100), Geometry.Rect(0, 0, 50, 100), Geometry.Rect(20, 20, 80, 80), default,
            Geometry.Pt(15, 91), default, true,
            default, Geometry.Pt(15, 91), default),
        new("clip dr and sr, sp inside sr",
            Geometry.Rect(0, 0, 100, 100), Geometry.Rect(0, 0, 50, 100), Geometry.Rect(20, 20, 80, 80), default,
            Geometry.Pt(44, 33), default, true,
            Geometry.Rect(0, 0, 36, 47), Geometry.Pt(44, 33), default),
        new("basic mask",
            Geometry.Rect(0, 0, 80, 80), Geometry.Rect(20, 0, 100, 80), Geometry.Rect(0, 0, 50, 49), Geometry.Rect(0, 0, 46, 47),
            default, default, false,
            Geometry.Rect(20, 0, 46, 47), Geometry.Pt(20, 0), Geometry.Pt(20, 0)),
        new("clip sr and mr",
            Geometry.Rect(0, 0, 100, 100), Geometry.Rect(0, 0, 100, 100), Geometry.Rect(23, 23, 55, 86), Geometry.Rect(44, 44, 87, 58),
            Geometry.Pt(10, 10), Geometry.Pt(11, 11), false,
            Geometry.Rect(33, 33, 45, 47), Geometry.Pt(43, 43), Geometry.Pt(44, 44)),
    ];

    [Fact]
    public void Clip()
    {
        var dst0 = Images.NewRgba(Geometry.Rect(0, 0, 100, 100));
        var src0 = Images.NewRgba(Geometry.Rect(0, 0, 100, 100));
        var mask0 = Images.NewRgba(Geometry.Rect(0, 0, 100, 100));
        foreach (var c in ClipTestCases)
        {
            var dst = (RgbaImage)dst0.SubImage(c.Dr);
            var src = (RgbaImage)src0.SubImage(c.Sr);
            Rect r = c.R;
            Point sp = c.Sp;
            Point mp = c.Mp;
            if (c.NilMask)
                DrawOps.Clip(dst, ref r, src, ref sp, null, ref mp);
            else
                DrawOps.Clip(dst, ref r, src, ref sp, mask0.SubImage(c.Mr), ref mp);

            Assert.True(c.R0.Eq(r), $"{c.Desc}: clip rectangle want {c.R0} got {r}");
            Assert.True(c.Sp0.Eq(sp), $"{c.Desc}: sp want {c.Sp0} got {sp}");
            if (!c.NilMask)
                Assert.True(c.Mp0.Eq(mp), $"{c.Desc}: mp want {c.Mp0} got {mp}");

            Assert.True(r.In(c.Dr), $"{c.Desc}: c.dr {c.Dr} does not contain r {r}");
            Rect sr = r.Add(c.Sp.Sub(c.Dr.Min));
            Assert.True(sr.In(c.Sr), $"{c.Desc}: c.sr {c.Sr} does not contain sr {sr}");
            if (!c.NilMask)
            {
                Rect mr = r.Add(c.Mp.Sub(c.Dr.Min));
                Assert.True(mr.In(c.Mr), $"{c.Desc}: c.mr {c.Mr} does not contain mr {mr}");
            }
        }
    }
}
