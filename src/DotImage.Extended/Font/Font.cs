using DotImage;
using DotImage.Color;
using DotImage.Draw;
using System.Text;

using DotImage.Extended.Math.Fixed;

namespace DotImage.Extended.Font;

public readonly struct FontMetrics : IEquatable<FontMetrics>
{
    public readonly Int26_6 Height;
    public readonly Int26_6 Ascent;
    public readonly Int26_6 Descent;
    public readonly Int26_6 XHeight;
    public readonly Int26_6 CapHeight;
    public readonly Point CaretSlope;

    public FontMetrics(Int26_6 height, Int26_6 ascent, Int26_6 descent,
        Int26_6 xHeight, Int26_6 capHeight, Point caretSlope)
    {
        Height = height;
        Ascent = ascent;
        Descent = descent;
        XHeight = xHeight;
        CapHeight = capHeight;
        CaretSlope = caretSlope;
    }

    public bool Equals(FontMetrics other) =>
        Height == other.Height &&
        Ascent == other.Ascent &&
        Descent == other.Descent &&
        XHeight == other.XHeight &&
        CapHeight == other.CapHeight &&
        CaretSlope.X == other.CaretSlope.X &&
        CaretSlope.Y == other.CaretSlope.Y;

    public override bool Equals(object? obj) => obj is FontMetrics other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Height, Ascent, Descent, XHeight, CapHeight, CaretSlope);
}

public readonly struct GlyphMetrics
{
    public readonly Rect Bounds;
    public readonly IImage Mask;
    public readonly Point MaskOrigin;
    public readonly Int26_6 Advance;
    public readonly bool Found;

    public GlyphMetrics(Rect bounds, IImage mask, Point maskOrigin, Int26_6 advance, bool found)
    {
        Bounds = bounds;
        Mask = mask;
        MaskOrigin = maskOrigin;
        Advance = advance;
        Found = found;
    }
}

public readonly struct GlyphBounds
{
    public readonly Rectangle26_6 Bounds;
    public readonly Int26_6 Advance;
    public readonly bool Found;

    public GlyphBounds(Rectangle26_6 bounds, Int26_6 advance, bool found)
    {
        Bounds = bounds;
        Advance = advance;
        Found = found;
    }
}

public interface IFontFace : IDisposable
{
    GlyphMetrics GetGlyph(Point26_6 dot, Rune r);
    GlyphBounds GetGlyphBounds(Rune r);
    Int26_6 GetGlyphAdvance(Rune r);
    Int26_6 GetKern(Rune r0, Rune r1);
    FontMetrics Metrics { get; }
}

public static class FontUtil
{
    public static (Rectangle26_6 Bounds, Int26_6 Advance) BoundString(IFontFace face, string s)
    {
        Rectangle26_6 bounds = default;
        Int26_6 advance = new(0);
        int prevC = -1;
        foreach (Rune c in s.EnumerateRunes())
        {
            if (prevC >= 0)
            {
                advance += face.GetKern(new Rune(prevC), c);
            }
            var gb = face.GetGlyphBounds(c);
            if (!gb.Bounds.IsEmpty)
            {
                var b = new Rectangle26_6(gb.Bounds.MinX + advance, gb.Bounds.MinY, gb.Bounds.MaxX + advance, gb.Bounds.MaxY);
                bounds = bounds.Union(b);
            }
            advance += gb.Advance;
            prevC = c.Value;
        }
        return (bounds, advance);
    }

    public static Int26_6 MeasureString(IFontFace face, string s)
    {
        Int26_6 advance = new(0);
        int prevC = -1;
        foreach (Rune c in s.EnumerateRunes())
        {
            if (prevC >= 0)
            {
                advance += face.GetKern(new Rune(prevC), c);
            }
            advance += face.GetGlyphAdvance(c);
            prevC = c.Value;
        }
        return advance;
    }
}

public class Drawer
{
    public IWritableImage Dst { get; set; } = null!;
    public IImage Src { get; set; } = null!;
    public IFontFace Face { get; set; } = null!;
    public Point26_6 Dot { get; set; }

    public void DrawString(string s)
    {
        if (Face == null || Dst == null || Src == null)
            return;

        int prevC = -1;
        foreach (Rune c in s.EnumerateRunes())
        {
            if (prevC >= 0)
            {
                Dot = new Point26_6(Dot.X + Face.GetKern(new Rune(prevC), c), Dot.Y);
            }
            var gm = Face.GetGlyph(Dot, c);
            if (!gm.Bounds.Empty())
            {
                DrawOps.DrawMask(Dst, gm.Bounds, Src, default, gm.Mask, gm.MaskOrigin, Op.Over);
            }
            Dot = new Point26_6(Dot.X + gm.Advance, Dot.Y);
            prevC = c.Value;
        }
    }

    public (Rectangle26_6 Bounds, Int26_6 Advance) BoundString(string s)
    {
        var (bounds, advance) = FontUtil.BoundString(Face, s);
        return (bounds.Add(Dot), advance);
    }

    public Int26_6 MeasureString(string s) => FontUtil.MeasureString(Face, s);
}