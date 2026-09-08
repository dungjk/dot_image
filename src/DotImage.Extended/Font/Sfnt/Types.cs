using System;
using DotImage.Extended.Math.Fixed;

namespace DotImage.Extended.Font.Sfnt;

/// <summary>A glyph index in a font. Ported from sfnt.GlyphIndex.</summary>
public readonly struct GlyphIndex : IEquatable<GlyphIndex>, IComparable<GlyphIndex>
{
    public readonly ushort Value;

    public GlyphIndex(ushort value) { Value = value; }

    public static implicit operator GlyphIndex(int i) => new(unchecked((ushort)i));
    public static implicit operator GlyphIndex(ushort u) => new(u);
    public static explicit operator int(GlyphIndex g) => g.Value;

    public bool Equals(GlyphIndex other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is GlyphIndex other && Equals(other);
    public override int GetHashCode() => Value;
    public int CompareTo(GlyphIndex other) => Value.CompareTo(other.Value);
    public static bool operator ==(GlyphIndex a, GlyphIndex b) => a.Value == b.Value;
    public static bool operator !=(GlyphIndex a, GlyphIndex b) => a.Value != b.Value;
    public static bool operator <(GlyphIndex a, GlyphIndex b) => a.Value < b.Value;
    public static bool operator >(GlyphIndex a, GlyphIndex b) => a.Value > b.Value;
    public static bool operator <=(GlyphIndex a, GlyphIndex b) => a.Value <= b.Value;
    public static bool operator >=(GlyphIndex a, GlyphIndex b) => a.Value >= b.Value;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a name table entry. Ported from sfnt.NameID.</summary>
public readonly struct NameID : IEquatable<NameID>
{
    public readonly ushort Value;
    public NameID(ushort value) { Value = value; }

    public static implicit operator NameID(int i) => new(unchecked((ushort)i));
    public static implicit operator NameID(ushort u) => new(u);
    public static explicit operator int(NameID n) => n.Value;

    public bool Equals(NameID other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is NameID other && Equals(other);
    public override int GetHashCode() => Value;
    public static bool operator ==(NameID a, NameID b) => a.Value == b.Value;
    public static bool operator !=(NameID a, NameID b) => a.Value != b.Value;
}

public static class NameIDConstants
{
    public static readonly NameID Copyright = new(0);
    public static readonly NameID Family = new(1);
    public static readonly NameID Subfamily = new(2);
    public static readonly NameID UniqueIdentifier = new(3);
    public static readonly NameID Full = new(4);
    public static readonly NameID Version = new(5);
    public static readonly NameID PostScript = new(6);
    public static readonly NameID Trademark = new(7);
    public static readonly NameID Manufacturer = new(8);
    public static readonly NameID Designer = new(9);
    public static readonly NameID Description = new(10);
    public static readonly NameID VendorURL = new(11);
    public static readonly NameID DesignerURL = new(12);
    public static readonly NameID License = new(13);
    public static readonly NameID LicenseURL = new(14);
    public static readonly NameID TypographicFamily = new(16);
    public static readonly NameID TypographicSubfamily = new(17);
    public static readonly NameID CompatibleFull = new(18);
    public static readonly NameID SampleText = new(19);
    public static readonly NameID PostScriptCID = new(20);
    public static readonly NameID WWSFamily = new(21);
    public static readonly NameID WWSSubfamily = new(22);
    public static readonly NameID LightBackgroundPalette = new(23);
    public static readonly NameID DarkBackgroundPalette = new(24);
    public static readonly NameID VariationsPostScriptPrefix = new(25);
}

/// <summary>An integral number of "font units". Ported from sfnt.Units.</summary>
public readonly struct Units : IEquatable<Units>
{
    public readonly int Value;
    public Units(int value) { Value = value; }

    public static implicit operator Units(int i) => new(i);
    public static explicit operator int(Units u) => u.Value;

    public bool Equals(Units other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Units other && Equals(other);
    public override int GetHashCode() => Value;
    public static bool operator ==(Units a, Units b) => a.Value == b.Value;
    public static bool operator !=(Units a, Units b) => a.Value != b.Value;
}

/// <summary>A vector path segment's operator. Ported from sfnt.SegmentOp.</summary>
public enum SegmentOp : uint
{
    MoveTo = 0,
    LineTo = 1,
    QuadTo = 2,
    CubeTo = 3,
}

/// <summary>A segment of a vector path. Ported from sfnt.Segment.
/// The Y axis increases down.</summary>
public struct Segment : IEquatable<Segment>
{
    public SegmentOp Op;
    public Point26_6 P0;
    public Point26_6 P1;
    public Point26_6 P2;

    public Segment(SegmentOp op, Point26_6 p0)
    {
        Op = op;
        P0 = p0;
        P1 = default;
        P2 = default;
    }

    public Segment(SegmentOp op, Point26_6 p0, Point26_6 p1)
    {
        Op = op;
        P0 = p0;
        P1 = p1;
        P2 = default;
    }

    public Segment(SegmentOp op, Point26_6 p0, Point26_6 p1, Point26_6 p2)
    {
        Op = op;
        P0 = p0;
        P1 = p1;
        P2 = p2;
    }

    // Args(i) mirrors Go's Args[i]. For MoveTo and LineTo, only P0 (Args[0])
    // is meaningful.
    public Point26_6 Args(int i) =>
        i switch
        {
            0 => P0,
            1 => P1,
            _ => P2,
        };

    public void SetArgs(int i, Point26_6 p)
    {
        switch (i)
        {
            case 0: P0 = p; break;
            case 1: P1 = p; break;
            default: P2 = p; break;
        }
    }

    public bool Equals(Segment other) =>
        Op == other.Op && P0 == other.P0 && P1 == other.P1 && P2 == other.P2;
    public override bool Equals(object? obj) => obj is Segment other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Op, P0, P1, P2);
    public static bool operator ==(Segment a, Segment b) => a.Equals(b);
    public static bool operator !=(Segment a, Segment b) => !a.Equals(b);

    public override string ToString()
    {
        if (Op == SegmentOp.MoveTo || Op == SegmentOp.LineTo)
        {
            return string.Format("{0} {1}", Op, P0);
        }
        if (Op == SegmentOp.QuadTo)
        {
            return string.Format("{0} {1} {2}", Op, P0, P1);
        }
        return string.Format("{0} {1} {2} {3}", Op, P0, P1, P2);
    }
}

/// <summary>A slice of <see cref="Segment"/>, with a bounding-box helper.
/// Ported from sfnt.Segments.</summary>
public struct Segments
{
    private List<Segment>? _items;

    public List<Segment> Items => _items ??= new List<Segment>();

    internal void SetItems(List<Segment> items) => _items = items;

    public int Count => _items?.Count ?? 0;

    public void Clear()
    {
        _items?.Clear();
    }

    public void Add(Segment s)
    {
        Items.Add(s);
    }

    public Segment this[int i]
    {
        get => Items[i];
        set => Items[i] = value;
    }

    /// <summary>Returns the bounding box, or an empty rectangle if empty.</summary>
    public Rectangle26_6 Bounds()
    {
        if (Count == 0)
        {
            return default;
        }
        Point26_6 min = new(new Int26_6(int.MaxValue), new Int26_6(int.MaxValue));
        Point26_6 max = new(new Int26_6(int.MinValue), new Int26_6(int.MinValue));
        for (int i = 0; i < Count; i++)
        {
            var seg = Items[i];
            int n = 1;
            switch (seg.Op)
            {
                case SegmentOp.QuadTo: n = 2; break;
                case SegmentOp.CubeTo: n = 3; break;
            }
            for (int j = 0; j < n; j++)
            {
                var p = seg.Args(j);
                if (max.X < p.X) max = new Point26_6(p.X, max.Y);
                if (min.X > p.X) min = new Point26_6(p.X, min.Y);
                if (max.Y < p.Y) max = new Point26_6(max.X, p.Y);
                if (min.Y > p.Y) min = new Point26_6(min.X, p.Y);
            }
        }
        return new Rectangle26_6(min.X, min.Y, max.X, max.Y);
    }
}

internal static class SegmentMath
{
    internal static Point26_6 MidPoint(Point26_6 p, Point26_6 q) =>
        new(
            Int26_6.FromRaw((p.X.Raw + q.X.Raw) / 2),
            Int26_6.FromRaw((p.Y.Raw + q.Y.Raw) / 2));

    internal static Point26_6 Tform(short txx, short txy, short tyx, short tyy, Int26_6 dx, Int26_6 dy, Point26_6 p)
    {
        const long half = 1 << 13;
        long x = dx.Raw +
            ((p.X.Raw * txx + half) >> 14) +
            ((p.Y.Raw * tyx + half) >> 14);
        long y = dy.Raw +
            ((p.X.Raw * txy + half) >> 14) +
            ((p.Y.Raw * tyy + half) >> 14);
        return new Point26_6(Int26_6.FromRaw(x), Int26_6.FromRaw(y));
    }
}