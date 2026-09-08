using DotImage;
using DotImage.Color;
using System.Text;

using DotImage.Extended.Math.Fixed;

namespace DotImage.Extended.Font.Basic;

public readonly struct Range
{
    // Low is inclusive, High is exclusive.
    public readonly Rune Low;
    public readonly Rune High;
    public readonly int Offset;

    public Range(Rune low, Rune high, int offset)
    {
        Low = low;
        High = high;
        Offset = offset;
    }
}

public class Face : IFontFace
{
    public int Advance;
    public int Width;
    public int Height;
    public int Ascent;
    public int Descent;
    public int Left;

    public IImage Mask = null!;
    public Range[] Ranges = [];

    public Face() { }

    public Face(int advance, int width, int height, int ascent, int descent, int left, IImage mask, Range[] ranges)
    {
        Advance = advance;
        Width = width;
        Height = height;
        Ascent = ascent;
        Descent = descent;
        Left = left;
        Mask = mask;
        Ranges = ranges;
    }

    public void Dispose() { }

    public void Close() { }

    public Int26_6 GetKern(Rune r0, Rune r1) => new(0);

    public FontMetrics Metrics => new(
        Int26_6.FromInt(Height),
        Int26_6.FromInt(Ascent),
        Int26_6.FromInt(Descent),
        Int26_6.FromInt(Ascent),
        Int26_6.FromInt(Ascent),
        new Point(0, 1));

    public GlyphMetrics GetGlyph(Point26_6 dot, Rune r)
    {
        if (Find(r, out Rune found, out Range rng))
        {
            int maskpY = ((int)(found.Value - rng.Low.Value) + rng.Offset) * (Ascent + Descent);
            int x = ((dot.X.Raw + 32) >> 6) + Left;
            int y = (dot.Y.Raw + 32) >> 6;
            var dr = new Rect(
                new Point(x, y - Ascent),
                new Point(x + Width, y + Descent));

            return new GlyphMetrics(dr, Mask, new Point(0, maskpY), Int26_6.FromInt(Advance), r.Value == found.Value);
        }
        return default;
    }

    public GlyphBounds GetGlyphBounds(Rune r)
    {
        if (Find(r, out Rune found, out _))
        {
            return new GlyphBounds(
                new Rectangle26_6(
                    Int26_6.FromInt(0),
                    Int26_6.FromInt(-Ascent),
                    Int26_6.FromInt(Width),
                    Int26_6.FromInt(Descent)),
                Int26_6.FromInt(Advance),
                r.Value == found.Value);
        }
        return default;
    }

    public Int26_6 GetGlyphAdvance(Rune r)
    {
        if (Find(r, out Rune found, out _))
        {
            return Int26_6.FromInt(Advance);
        }
        return new(0);
    }

    public bool HasGlyph(Rune r) => Find(r, out _, out _);

    private bool Find(Rune r, out Rune found, out Range rng)
    {
        while (true)
        {
            foreach (Range candidate in Ranges)
            {
                if (candidate.Low.Value <= r.Value && r.Value < candidate.High.Value)
                {
                    found = r;
                    rng = candidate;
                    return true;
                }
            }
            if (r.Value == '\ufffd')
            {
                found = default;
                rng = default;
                return false;
            }
            r = new Rune('\ufffd');
        }
    }
}

public static class Face7x13
{
    private static readonly AlphaImage Mask = new()
    {
        Pix = Mask7X13.Data,
        Stride = 6,
        Rect = new Rect(default, new Point(6, 96 * 13)),
    };

    public static readonly Face Instance = new()
    {
        Advance = 7,
        Width = 6,
        Height = 13,
        Ascent = 11,
        Descent = 2,
        Left = 0,
        Mask = Mask,
        Ranges =
        [
            new Range(new Rune('\u0020'), new Rune('\u007f'), 0),
            new Range(new Rune('\ufffd'), new Rune('\ufffe'), 95),
        ],
    };
}