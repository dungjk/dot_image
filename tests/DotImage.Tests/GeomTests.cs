// Ported from Go src/image/geom_test.go

namespace DotImage.Tests;

public class GeomTests
{
    private static string? In(Rect f, Rect g)
    {
        if (!f.In(g))
            return $"f={f}, f.In({g}): got false, want true";
        for (int y = f.Min.Y; y < f.Max.Y; y++)
        {
            for (int x = f.Min.X; x < f.Max.X; x++)
            {
                var p = new Point(x, y);
                if (!p.In(g))
                    return $"p={p}, p.In({g}): got false, want true";
            }
        }
        return null;
    }

    private static readonly Rect[] Rects =
    [
        Geometry.Rect(0, 0, 10, 10),
        Geometry.Rect(10, 0, 20, 10),
        Geometry.Rect(1, 2, 3, 4),
        Geometry.Rect(4, 6, 10, 10),
        Geometry.Rect(2, 3, 12, 5),
        Geometry.Rect(-1, -2, 0, 0),
        Geometry.Rect(-1, -2, 4, 6),
        Geometry.Rect(-10, -20, 30, 40),
        Geometry.Rect(8, 8, 8, 8),
        Geometry.Rect(88, 88, 88, 88),
        Geometry.Rect(6, 5, 4, 3),
    ];

    [Fact]
    public void Rectangle_EqIntersectUnion()
    {
        foreach (var r in Rects)
        {
            foreach (var s in Rects)
            {
                bool got = r.Eq(s);
                bool want = In(r, s) == null && In(s, r) == null;
                Assert.Equal(want, got);
            }
        }

        foreach (var r in Rects)
        {
            foreach (var s in Rects)
            {
                var a = r.Intersect(s);
                Assert.Null(In(a, r));
                Assert.Null(In(a, s));
                bool isZero = a.Equals(default(Rect));
                bool overlaps = r.Overlaps(s);
                Assert.Equal(isZero, !overlaps);

                var largerThanA = new Rect[] { a, a, a, a };
                largerThanA[0] = new Rect(new Point(a.Min.X - 1, a.Min.Y), a.Max);
                largerThanA[1] = new Rect(new Point(a.Min.X, a.Min.Y - 1), a.Max);
                largerThanA[2] = new Rect(a.Min, new Point(a.Max.X + 1, a.Max.Y));
                largerThanA[3] = new Rect(a.Min, new Point(a.Max.X, a.Max.Y + 1));
                for (int i = 0; i < largerThanA.Length; i++)
                {
                    var b = largerThanA[i];
                    if (b.Empty()) continue;
                    if (In(b, r) == null && In(b, s) == null)
                        Assert.Fail($"Intersect: r={r}, s={s}, a={a}, b={b}, i={i}: intersection could be larger");
                }
            }
        }

        foreach (var r in Rects)
        {
            foreach (var s in Rects)
            {
                var a = r.Union(s);
                Assert.Null(In(r, a));
                Assert.Null(In(s, a));
                if (a.Empty()) continue;

                var smallerThanA = new Rect[] { a, a, a, a };
                smallerThanA[0] = new Rect(new Point(a.Min.X + 1, a.Min.Y), a.Max);
                smallerThanA[1] = new Rect(new Point(a.Min.X, a.Min.Y + 1), a.Max);
                smallerThanA[2] = new Rect(a.Min, new Point(a.Max.X - 1, a.Max.Y));
                smallerThanA[3] = new Rect(a.Min, new Point(a.Max.X, a.Max.Y - 1));
                for (int i = 0; i < smallerThanA.Length; i++)
                {
                    var b = smallerThanA[i];
                    if (In(r, b) == null && In(s, b) == null)
                        Assert.Fail($"Union: r={r}, s={s}, a={a}, b={b}, i={i}: union could be smaller");
                }
            }
        }
    }
}
