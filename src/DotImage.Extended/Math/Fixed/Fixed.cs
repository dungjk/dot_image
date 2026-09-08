// Ported from golang.org/x/image/math/fixed/fixed.go

namespace DotImage.Extended.Math.Fixed;

/// <summary>
/// Returns the integer value i as an <see cref="Int26_6"/>.
/// For example, passing the integer value 2 yields Int26_6(128).
/// </summary>
public static class FixedPoint
{
    public static Int26_6 I(int i) => new(i << 6);
    public static Int52_12 I52(int i) => new((long)i << 12);

    /// <summary>
    /// Returns the integer values x and y as a <see cref="Point26_6"/>.
    /// </summary>
    public static Point26_6 P(int x, int y) => new(new Int26_6(x << 6), new Int26_6(y << 6));

    /// <summary>
    /// Returns the integer values minX, minY, maxX, maxY as a
    /// <see cref="Rectangle26_6"/>. Like image.Rect, the returned rectangle has
    /// minimum and maximum coordinates swapped if necessary so that it is
    /// well-formed.
    /// </summary>
    public static Rectangle26_6 R(int minX, int minY, int maxX, int maxY)
    {
        if (minX > maxX)
        {
            (minX, maxX) = (maxX, minX);
        }

        if (minY > maxY)
        {
            (minY, maxY) = (maxY, minY);
        }

        return new Rectangle26_6(
            new Point26_6(new Int26_6(minX << 6), new Int26_6(minY << 6)),
            new Point26_6(new Int26_6(maxX << 6), new Int26_6(maxY << 6)));
    }
}

/// <summary>
/// A signed 26.6 fixed-point number. The integer part ranges from -33554432 to
/// 33554431, inclusive. The fractional part has 6 bits of precision. For
/// example, one-and-a-quarter is Int26_6(1&lt;&lt;6 + 1&lt;&lt;4).
/// </summary>
public readonly struct Int26_6(int raw) : IEquatable<Int26_6>
{
    public readonly int Raw = raw;

    public int Floor() => (Raw + 0x00) >> 6;
    public int Round() => (Raw + 0x20) >> 6;
    public int Ceil() => (Raw + 0x3f) >> 6;

    public Int26_6 Mul(Int26_6 y) =>
        new((int)(((long)Raw * y.Raw + (1 << 5)) >> 6));

    /// <summary>Returns the integer i as an <see cref="Int26_6"/> (i &lt;&lt; 6).</summary>
    public static Int26_6 FromInt(int i) => new(i << 6);

    /// <summary>Returns a 26.6 value from a raw numeral (a plain 1/64 fixed value).</summary>
    public static Int26_6 FromRaw(int raw) => new(raw);

    /// <summary>Returns a 26.6 value from a raw numeral represented as a long.</summary>
    public static Int26_6 FromRaw(long raw) => new((int)raw);

    public static Int26_6 operator -(Int26_6 a) => new(-a.Raw);

    /// <summary>Multiplies the raw numerals. This is NOT a fixed-point product; use
    /// <see cref="Mul"/> for that. It matches the raw-numeral arithmetic used by
    /// the sfnt scaler.</summary>
    public static Int26_6 operator *(Int26_6 a, Int26_6 b) => new(unchecked(a.Raw * b.Raw));

    public static bool operator <(Int26_6 a, Int26_6 b) => a.Raw < b.Raw;
    public static bool operator >(Int26_6 a, Int26_6 b) => a.Raw > b.Raw;
    public static bool operator <=(Int26_6 a, Int26_6 b) => a.Raw <= b.Raw;
    public static bool operator >=(Int26_6 a, Int26_6 b) => a.Raw >= b.Raw;

    public override string ToString()
    {
        const int shift = 6;
        const int mask = (1 << 6) - 1;
        if (Raw >= 0)
        {
            return $"{Raw >> shift}:{Raw & mask:D2}";
        }

        int x = -Raw;
        if (x >= 0)
        {
            return $"-{x >> shift}:{x & mask:D2}";
        }

        return "-33554432:00";
    }

    public override bool Equals(object? obj) => obj is Int26_6 other && Equals(other);
    public bool Equals(Int26_6 other) => Raw == other.Raw;
    public override int GetHashCode() => Raw.GetHashCode();
    public static bool operator ==(Int26_6 a, Int26_6 b) => a.Raw == b.Raw;
    public static bool operator !=(Int26_6 a, Int26_6 b) => a.Raw != b.Raw;
    public static Int26_6 operator +(Int26_6 a, Int26_6 b) => new(a.Raw + b.Raw);
    public static Int26_6 operator -(Int26_6 a, Int26_6 b) => new(a.Raw - b.Raw);
}

/// <summary>
/// A signed 52.12 fixed-point number. The integer part ranges from
/// -2251799813685248 to 2251799813685247, inclusive. The fractional part has
/// 12 bits of precision. For example, one-and-a-quarter is
/// Int52_12(1&lt;&lt;12 + 1&lt;&lt;10).
/// </summary>
public readonly struct Int52_12(long raw) : IEquatable<Int52_12>
{
    public readonly long Raw = raw;

    public int Floor() => (int)((Raw + 0x000) >> 12);
    public int Round() => (int)((Raw + 0x800) >> 12);
    public int Ceil() => (int)((Raw + 0xfff) >> 12);

    public Int52_12 Mul(Int52_12 y)
    {
        const int M = 52;
        const int N = 12;
        var (lo, hi) = Muli64(Raw, y.Raw);
        long ret = (long)((hi << M) | (lo >> N));
        ret += (long)((lo >> (N - 1)) & 1);
        return new Int52_12(ret);
    }

    public override string ToString()
    {
        const int shift = 12;
        const long mask = (1L << 12) - 1;
        if (Raw >= 0)
        {
            return $"{Raw >> shift}:{Raw & mask:D4}";
        }

        long x = -Raw;
        if (x >= 0)
        {
            return $"-{x >> shift}:{x & mask:D4}";
        }

        return "-2251799813685248:0000";
    }

    public override bool Equals(object? obj) => obj is Int52_12 other && Equals(other);
    public bool Equals(Int52_12 other) => Raw == other.Raw;
    public override int GetHashCode() => Raw.GetHashCode();
    public static bool operator ==(Int52_12 a, Int52_12 b) => a.Raw == b.Raw;
    public static bool operator !=(Int52_12 a, Int52_12 b) => a.Raw != b.Raw;
    public static Int52_12 operator +(Int52_12 a, Int52_12 b) => new(a.Raw + b.Raw);
    public static Int52_12 operator -(Int52_12 a, Int52_12 b) => new(a.Raw - b.Raw);

    /// <summary>
    /// Multiplies two int64 values, returning the 128-bit signed integer result
    /// as two ulong values (lo, hi). Adapted from Hacker's Delight.
    /// </summary>
    private static (ulong Lo, ulong Hi) Muli64(long u, long v)
    {
        const int s = 32;
        const ulong mask = (1UL << s) - 1;

        ulong u1 = (ulong)(u >> s);
        ulong u0 = (ulong)u & mask;
        ulong v1 = (ulong)(v >> s);
        ulong v0 = (ulong)v & mask;

        ulong w0 = u0 * v0;
        ulong t = u1 * v0 + (w0 >> s);
        ulong w1 = t & mask;
        ulong w2 = (ulong)((long)t >> s);
        w1 += u0 * v1;
        ulong lo = (ulong)u * (ulong)v;
        ulong hi = u1 * v1 + w2 + (ulong)((long)w1 >> s);
        return (lo, hi);
    }
}

/// <summary>
/// A 26.6 fixed-point coordinate pair, analogous to image.Point.
/// </summary>
public readonly struct Point26_6(Int26_6 x, Int26_6 y) : IEquatable<Point26_6>
{
    public Int26_6 X { get; } = x;
    public Int26_6 Y { get; } = y;

    public Point26_6 Add(Point26_6 q) => new(X + q.X, Y + q.Y);
    public Point26_6 Sub(Point26_6 q) => new(X - q.X, Y - q.Y);
    public Point26_6 Mul(Int26_6 k) => new(new Int26_6(X.Raw * k.Raw / 64), new Int26_6(Y.Raw * k.Raw / 64));
    public Point26_6 Div(Int26_6 k) => new(new Int26_6(X.Raw * 64 / k.Raw), new Int26_6(Y.Raw * 64 / k.Raw));

    public bool In(Rectangle26_6 r) =>
        r.Min.X.Raw <= X.Raw && X.Raw < r.Max.X.Raw &&
        r.Min.Y.Raw <= Y.Raw && Y.Raw < r.Max.Y.Raw;

    public bool Equals(Point26_6 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Point26_6 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(Point26_6 a, Point26_6 b) => a.Equals(b);
    public static bool operator !=(Point26_6 a, Point26_6 b) => !a.Equals(b);
    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// A 52.12 fixed-point coordinate pair, analogous to image.Point.
/// </summary>
public readonly struct Point52_12(Int52_12 x, Int52_12 y)
{
    public Int52_12 X { get; } = x;
    public Int52_12 Y { get; } = y;

    public Point52_12 Add(Point52_12 q) => new(X + q.X, Y + q.Y);
    public Point52_12 Sub(Point52_12 q) => new(X - q.X, Y - q.Y);
    public Point52_12 Mul(Int52_12 k) => new(new Int52_12(X.Raw * k.Raw / 4096), new Int52_12(Y.Raw * k.Raw / 4096));
    public Point52_12 Div(Int52_12 k) => new(new Int52_12(X.Raw * 4096 / k.Raw), new Int52_12(Y.Raw * 4096 / k.Raw));

    public bool In(Rectangle52_12 r) =>
        r.Min.X.Raw <= X.Raw && X.Raw < r.Max.X.Raw &&
        r.Min.Y.Raw <= Y.Raw && Y.Raw < r.Max.Y.Raw;
}

/// <summary>
/// A 26.6 fixed-point coordinate rectangle. The Min bound is inclusive and the
/// Max bound is exclusive.
/// </summary>
public readonly struct Rectangle26_6(Point26_6 min, Point26_6 max) : IEquatable<Rectangle26_6>
{
    public Point26_6 Min { get; } = min;
    public Point26_6 Max { get; } = max;

    public Rectangle26_6(Int26_6 minX, Int26_6 minY, Int26_6 maxX, Int26_6 maxY)
        : this(new Point26_6(minX, minY), new Point26_6(maxX, maxY))
    {
    }

    public Int26_6 MinX => Min.X;
    public Int26_6 MinY => Min.Y;
    public Int26_6 MaxX => Max.X;
    public Int26_6 MaxY => Max.Y;

    public Rectangle26_6 Add(Point26_6 p) => new(Min.Add(p), Max.Add(p));
    public Rectangle26_6 Sub(Point26_6 p) => new(Min.Sub(p), Max.Sub(p));

    public Rectangle26_6 Intersect(Rectangle26_6 s)
    {
        Point26_6 rMin = Min, rMax = Max;
        if (rMin.X.Raw < s.Min.X.Raw) rMin = new Point26_6(s.Min.X, rMin.Y);
        if (rMin.Y.Raw < s.Min.Y.Raw) rMin = new Point26_6(rMin.X, s.Min.Y);
        if (rMax.X.Raw > s.Max.X.Raw) rMax = new Point26_6(s.Max.X, rMax.Y);
        if (rMax.Y.Raw > s.Max.Y.Raw) rMax = new Point26_6(rMax.X, s.Max.Y);
        var r = new Rectangle26_6(rMin, rMax);
        return r.Empty() ? new Rectangle26_6() : r;
    }

    public Rectangle26_6 Union(Rectangle26_6 s)
    {
        if (Empty()) return s;
        if (s.Empty()) return this;
        Point26_6 rMin = Min, rMax = Max;
        if (rMin.X.Raw > s.Min.X.Raw) rMin = new Point26_6(s.Min.X, rMin.Y);
        if (rMin.Y.Raw > s.Min.Y.Raw) rMin = new Point26_6(rMin.X, s.Min.Y);
        if (rMax.X.Raw < s.Max.X.Raw) rMax = new Point26_6(s.Max.X, rMax.Y);
        if (rMax.Y.Raw < s.Max.Y.Raw) rMax = new Point26_6(rMax.X, s.Max.Y);
        return new Rectangle26_6(rMin, rMax);
    }

    public bool Empty() => Min.X.Raw >= Max.X.Raw || Min.Y.Raw >= Max.Y.Raw;

    public bool IsEmpty => Empty();

    public bool In(Rectangle26_6 s)
    {
        if (Empty()) return true;
        return s.Min.X.Raw <= Min.X.Raw && Max.X.Raw <= s.Max.X.Raw &&
               s.Min.Y.Raw <= Min.Y.Raw && Max.Y.Raw <= s.Max.Y.Raw;
    }

    public bool Equals(Rectangle26_6 other) => Min == other.Min && Max == other.Max;
    public override bool Equals(object? obj) => obj is Rectangle26_6 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Min, Max);
    public static bool operator ==(Rectangle26_6 a, Rectangle26_6 b) => a.Equals(b);
    public static bool operator !=(Rectangle26_6 a, Rectangle26_6 b) => !a.Equals(b);
    public override string ToString() => $"[{MinX}, {MinY}, {MaxX}, {MaxY}]";
}

/// <summary>
/// A 52.12 fixed-point coordinate rectangle. The Min bound is inclusive and the
/// Max bound is exclusive.
/// </summary>
public readonly struct Rectangle52_12(Point52_12 min, Point52_12 max)
{
    public Point52_12 Min { get; } = min;
    public Point52_12 Max { get; } = max;

    public Rectangle52_12 Add(Point52_12 p) => new(Min.Add(p), Max.Add(p));
    public Rectangle52_12 Sub(Point52_12 p) => new(Min.Sub(p), Max.Sub(p));

    public Rectangle52_12 Intersect(Rectangle52_12 s)
    {
        Point52_12 rMin = Min, rMax = Max;
        if (rMin.X.Raw < s.Min.X.Raw) rMin = new Point52_12(s.Min.X, rMin.Y);
        if (rMin.Y.Raw < s.Min.Y.Raw) rMin = new Point52_12(rMin.X, s.Min.Y);
        if (rMax.X.Raw > s.Max.X.Raw) rMax = new Point52_12(s.Max.X, rMax.Y);
        if (rMax.Y.Raw > s.Max.Y.Raw) rMax = new Point52_12(rMax.X, s.Max.Y);
        var r = new Rectangle52_12(rMin, rMax);
        return r.Empty() ? new Rectangle52_12() : r;
    }

    public Rectangle52_12 Union(Rectangle52_12 s)
    {
        if (Empty()) return s;
        if (s.Empty()) return this;
        Point52_12 rMin = Min, rMax = Max;
        if (rMin.X.Raw > s.Min.X.Raw) rMin = new Point52_12(s.Min.X, rMin.Y);
        if (rMin.Y.Raw > s.Min.Y.Raw) rMin = new Point52_12(rMin.X, s.Min.Y);
        if (rMax.X.Raw < s.Max.X.Raw) rMax = new Point52_12(s.Max.X, rMax.Y);
        if (rMax.Y.Raw < s.Max.Y.Raw) rMax = new Point52_12(rMax.X, s.Max.Y);
        return new Rectangle52_12(rMin, rMax);
    }

    public bool Empty() => Min.X.Raw >= Max.X.Raw || Min.Y.Raw >= Max.Y.Raw;

    public bool In(Rectangle52_12 s)
    {
        if (Empty()) return true;
        return s.Min.X.Raw <= Min.X.Raw && Max.X.Raw <= s.Max.X.Raw &&
               s.Min.Y.Raw <= Min.Y.Raw && Max.Y.Raw <= s.Max.Y.Raw;
    }
}
