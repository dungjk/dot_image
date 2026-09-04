// Ported from Go src/image/ycbcr_test.go

namespace DotImage.Tests;

public class YCbCrImageTests
{
    private static readonly Rect[] Rects =
    [
        Geometry.Rect(0, 0, 16, 16),
        Geometry.Rect(1, 0, 16, 16),
        Geometry.Rect(0, 1, 16, 16),
        Geometry.Rect(1, 1, 16, 16),
        Geometry.Rect(1, 1, 15, 16),
        Geometry.Rect(1, 1, 16, 15),
        Geometry.Rect(1, 1, 15, 15),
        Geometry.Rect(2, 3, 14, 15),
        Geometry.Rect(7, 0, 7, 16),
        Geometry.Rect(0, 8, 16, 8),
        Geometry.Rect(0, 0, 10, 11),
        Geometry.Rect(5, 6, 16, 16),
        Geometry.Rect(7, 7, 8, 8),
        Geometry.Rect(7, 8, 8, 9),
        Geometry.Rect(8, 7, 9, 8),
        Geometry.Rect(8, 8, 9, 9),
        Geometry.Rect(7, 7, 17, 17),
        Geometry.Rect(8, 8, 17, 17),
        Geometry.Rect(9, 9, 17, 17),
        Geometry.Rect(10, 10, 17, 17),
    ];

    private static readonly YCbCrSubsampleRatio[] SubsampleRatios =
    [
        YCbCrSubsampleRatio.Ratio444,
        YCbCrSubsampleRatio.Ratio422,
        YCbCrSubsampleRatio.Ratio420,
        YCbCrSubsampleRatio.Ratio440,
        YCbCrSubsampleRatio.Ratio411,
        YCbCrSubsampleRatio.Ratio410,
    ];

    private static readonly Point[] Deltas =
    [
        Geometry.Pt(0, 0),
        Geometry.Pt(1000, 1001),
        Geometry.Pt(5001, -400),
        Geometry.Pt(-701, -801),
    ];

    [Fact]
    public void YCbCr_SubImageEquivalence()
    {
        foreach (var r in Rects)
        {
            foreach (var subsampleRatio in SubsampleRatios)
            {
                foreach (var delta in Deltas)
                    TestYCbCr(r, subsampleRatio, delta);
            }
        }
    }

    private static void TestYCbCr(Rect r, YCbCrSubsampleRatio subsampleRatio, Point delta)
    {
        var r1 = r.Add(delta);
        var m = YCbCrImages.NewYCbCr(r1, subsampleRatio);

        if (m.Y.Length > 100 * 100)
        {
            Assert.Fail($"r={r}, subsampleRatio={subsampleRatio}, delta={delta}: image buffer is too large");
            return;
        }

        for (int y = r1.Min.Y; y < r1.Max.Y; y++)
        {
            for (int x = r1.Min.X; x < r1.Max.X; x++)
            {
                int yi = m.YOffset(x, y);
                int ci = m.COffset(x, y);
                m.Y.Span[yi] = (byte)(16 * y + x);
                m.Cb.Span[ci] = (byte)(y + 16 * x);
                m.Cr.Span[ci] = (byte)(y + 16 * x);
            }
        }

        for (int y0 = delta.Y + 3; y0 < delta.Y + 7; y0++)
        {
            for (int y1 = delta.Y + 8; y1 < delta.Y + 13; y1++)
            {
                for (int x0 = delta.X + 3; x0 < delta.X + 7; x0++)
                {
                    for (int x1 = delta.X + 8; x1 < delta.X + 13; x1++)
                    {
                        var subRect = Geometry.Rect(x0, y0, x1, y1);
                        var sub = (YCbCrImage)m.SubImage(subRect);

                        for (int y = sub.Rect.Min.Y; y < sub.Rect.Max.Y; y++)
                        {
                            for (int x = sub.Rect.Min.X; x < sub.Rect.Max.X; x++)
                            {
                                var color0 = (YCbCr)m.At(x, y);
                                var color1 = (YCbCr)sub.At(x, y);
                                Assert.Equal(color0, color1);
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void YCbCr_SlicesDontOverlap()
    {
        var m = YCbCrImages.NewYCbCr(Geometry.Rect(0, 0, 8, 8), YCbCrSubsampleRatio.Ratio420);
        var slices = new[] { m.Y, m.Cb, m.Cr };
        for (int i = 0; i < slices.Length; i++)
        {
            byte want = (byte)(10 + i);
            slices[i].Span.Fill(want);
        }
        for (int i = 0; i < slices.Length; i++)
        {
            byte want = (byte)(10 + i);
            foreach (byte got in slices[i].Span)
                Assert.Equal(want, got);
        }
    }
}
