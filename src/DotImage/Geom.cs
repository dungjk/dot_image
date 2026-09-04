// Ported from Go src/image/geom.go

using DotImage.Color;

namespace DotImage;

public readonly struct Point(int x, int y)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public override string ToString() => $"({X},{Y})";

    public Point Add(Point q) => new(X + q.X, Y + q.Y);
    public Point Sub(Point q) => new(X - q.X, Y - q.Y);
    public Point Mul(int k) => new(X * k, Y * k);
    public Point Div(int k) => new(X / k, Y / k);

    public bool In(Rect r) =>
        r.Min.X <= X && X < r.Max.X &&
        r.Min.Y <= Y && Y < r.Max.Y;

    public Point Mod(Rect r)
    {
        int w = r.Dx();
        int h = r.Dy();
        var p = Sub(r.Min);
        int px = p.X % w;
        if (px < 0) px += w;
        int py = p.Y % h;
        if (py < 0) py += h;
        return new Point(px, py).Add(r.Min);
    }

    public bool Eq(Point q) => X == q.X && Y == q.Y;
}

public readonly struct Rect(Point min, Point max) : IImage, IRgba64Image
{
    public Point Min { get; } = min;
    public Point Max { get; } = max;

    public override string ToString() => $"{Min}-{Max}";

    public int Dx() => Max.X - Min.X;
    public int Dy() => Max.Y - Min.Y;
    public Point Size() => new(Dx(), Dy());

    public Rect Add(Point p) => new(
        new Point(Min.X + p.X, Min.Y + p.Y),
        new Point(Max.X + p.X, Max.Y + p.Y));

    public Rect Sub(Point p) => new(
        new Point(Min.X - p.X, Min.Y - p.Y),
        new Point(Max.X - p.X, Max.Y - p.Y));

    public Rect Inset(int n)
    {
        int minX = Min.X, maxX = Max.X, minY = Min.Y, maxY = Max.Y;
        if (Dx() < 2 * n)
        {
            minX = (minX + maxX) / 2;
            maxX = minX;
        }
        else
        {
            minX += n;
            maxX -= n;
        }
        if (Dy() < 2 * n)
        {
            minY = (minY + maxY) / 2;
            maxY = minY;
        }
        else
        {
            minY += n;
            maxY -= n;
        }
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    public Rect Intersect(Rect s)
    {
        var r = new Rect(
            new Point(Math.Max(Min.X, s.Min.X), Math.Max(Min.Y, s.Min.Y)),
            new Point(Math.Min(Max.X, s.Max.X), Math.Min(Max.Y, s.Max.Y)));
        if (r.Empty()) return default;
        return r;
    }

    public Rect Union(Rect s)
    {
        if (Empty()) return s;
        if (s.Empty()) return this;
        return new Rect(
            new Point(Math.Min(Min.X, s.Min.X), Math.Min(Min.Y, s.Min.Y)),
            new Point(Math.Max(Max.X, s.Max.X), Math.Max(Max.Y, s.Max.Y)));
    }

    public bool Empty() => Min.X >= Max.X || Min.Y >= Max.Y;

    public bool Eq(Rect s) => Equals(s) || (Empty() && s.Empty());

    public bool Overlaps(Rect s) =>
        !Empty() && !s.Empty() &&
        Min.X < s.Max.X && s.Min.X < Max.X &&
        Min.Y < s.Max.Y && s.Min.Y < Max.Y;

    public bool In(Rect s)
    {
        if (Empty()) return true;
        return s.Min.X <= Min.X && Max.X <= s.Max.X &&
               s.Min.Y <= Min.Y && Max.Y <= s.Max.Y;
    }

    public Rect Canon()
    {
        int minX = Min.X, maxX = Max.X, minY = Min.Y, maxY = Max.Y;
        if (maxX < minX) (minX, maxX) = (maxX, minX);
        if (maxY < minY) (minY, maxY) = (maxY, minY);
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    public IColor At(int x, int y) =>
        new Point(x, y).In(this) ? Colors.Opaque : Colors.Transparent;

    public Rgba64 Rgba64At(int x, int y) =>
        new Point(x, y).In(this)
            ? new Rgba64(0xffff, 0xffff, 0xffff, 0xffff)
            : new Rgba64(0, 0, 0, 0);

    public Rect Bounds() => this;

    public IColorModel ColorModel() => Models.Alpha16;
}

public static class Geometry
{
    public static Point Pt(int x, int y) => new(x, y);

    public static Rect Rect(int x0, int y0, int x1, int y1)
    {
        if (x0 > x1) (x0, x1) = (x1, x0);
        if (y0 > y1) (y0, y1) = (y1, y0);
        return new Rect(new Point(x0, y0), new Point(x1, y1));
    }
}

public static class PixelUtil
{
    public static int Mul3NonNeg(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0) return -1;
        ulong hi = Math.BigMul((ulong)x, (ulong)y, out ulong lo);
        if (hi != 0) return -1;
        hi = Math.BigMul(lo, (ulong)z, out lo);
        if (hi != 0) return -1;
        if (lo > int.MaxValue) return -1;
        int a = (int)lo;
        if (a < 0) return -1;
        return a;
    }

    public static int Add2NonNeg(int x, int y)
    {
        if (x < 0 || y < 0) return -1;
        int a = x + y;
        if (a < 0) return -1;
        return a;
    }
}

internal static class ImageBuffer
{
    internal static int PixelBufferLength(int bytesPerPixel, Rect r, string imageTypeName)
    {
        int totalLength = PixelUtil.Mul3NonNeg(bytesPerPixel, r.Dx(), r.Dy());
        if (totalLength < 0)
            throw new ArgumentException($"image: New{imageTypeName} Rectangle has huge or negative dimensions");
        return totalLength;
    }
}
