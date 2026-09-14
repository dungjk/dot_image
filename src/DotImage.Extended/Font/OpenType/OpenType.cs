// Ported from golang.org/x/image/font/opentype.

using System;
using System.Text;
using DotImage;
using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Font.Sfnt;
using DotImage.Extended.Math.Fixed;
using DotImage.Extended.Vector;
using SfntFont = DotImage.Extended.Font.Sfnt.Font;
using SfntCollection = DotImage.Extended.Font.Sfnt.Collection;
using SfntBuffer = DotImage.Extended.Font.Sfnt.Buffer;

namespace DotImage.Extended.Font.OpenType;

/// <summary>Implements a glyph rasterizer for TTF (TrueType Fonts) and OTF
/// (OpenType Fonts), providing a high-level API centered on
/// <see cref="NewFace"/>, which implements the <see cref="IFontFace"/>
/// interface. The sibling DotImage.Extended.Font.Sfnt package provides a
/// low-level API.</summary>
public static class OpenTypeFont
{
    /// <summary>Parses an OpenType font, such as TTF or OTF data, from a byte[]
    /// data source.</summary>
    public static SfntFont Parse(byte[] src) => SfntFont.Parse(src);

    /// <summary>Parses an OpenType font collection, such as TTC or OTC data,
    /// from a byte[] data source. If passed data for a single font, a TTF or
    /// OTF instead of a TTC or OTC, it will return a collection containing 1
    /// font.</summary>
    public static SfntCollection ParseCollection(byte[] src) => SfntFont.ParseCollection(src);

    /// <summary>Returns a new <see cref="IFontFace"/> for the given Font.
    /// If opts is null, sensible defaults will be used.</summary>
    public static IFontFace NewFace(SfntFont f, FaceOptions? opts)
    {
        opts ??= DefaultFaceOptions();
        return new Face(f, opts);
    }

    internal static FaceOptions DefaultFaceOptions() => new()
    {
        Size = 12,
        DPI = 72,
        Hinting = Hinting.None,
    };
}

/// <summary>Describes the possible options given to <see cref="OpenTypeFont.NewFace"/>
/// when creating a new <see cref="IFontFace"/> from a font.</summary>
public class FaceOptions
{
    /// <summary>Size is the font size in points.</summary>
    public double Size;

    /// <summary>DPI is the dots per inch resolution.</summary>
    public double DPI;

    /// <summary>Hinting selects how to quantize a vector font's glyph nodes.</summary>
    public Hinting Hinting;
}

/// <summary>Implements the <see cref="IFontFace"/> interface for Font values. A
/// Face is not safe to use concurrently.</summary>
public sealed class Face : IFontFace
{
    private readonly SfntFont f;
    private readonly Hinting hinting;
    private readonly Int26_6 scale;

    private FontMetrics metrics;
    private bool metricsSet;

    private readonly SfntBuffer buf = new();
    private readonly Rasterizer rast = new();
    private readonly AlphaImage mask = new();
    private byte[] maskStore = [];

    internal Face(SfntFont f, FaceOptions opts)
    {
        this.f = f;
        hinting = opts.Hinting;
        scale = new Int26_6((int)(0.5 + opts.Size * opts.DPI * 64 / 72));
    }

    /// <summary>Close satisfies the <see cref="IFontFace"/> interface.</summary>
    public void Dispose()
    {
    }

    /// <summary>Metrics satisfies the <see cref="IFontFace"/> interface.</summary>
    public FontMetrics Metrics
    {
        get
        {
            if (!metricsSet)
            {
                try
                {
                    metrics = f.Metrics(buf, scale, hinting);
                }
                catch (Exception)
                {
                    metrics = default;
                }
                metricsSet = true;
            }
            return metrics;
        }
    }

    /// <summary>Kern satisfies the <see cref="IFontFace"/> interface.</summary>
    public Int26_6 GetKern(Rune r0, Rune r1)
    {
        var x0 = f.GlyphIndex(buf, r0.Value);
        var x1 = f.GlyphIndex(buf, r1.Value);
        Int26_6 k;
        try
        {
            k = f.Kern(buf, x0, x1, Int26_6.FromRaw(f.UnitsPerEm.Value), hinting);
        }
        catch (Exception)
        {
            return new Int26_6(0);
        }
        return k;
    }

    /// <summary>Glyph satisfies the <see cref="IFontFace"/> interface.</summary>
    public GlyphMetrics GetGlyph(Point26_6 dot, Rune r)
    {
        var x = f.GlyphIndex(buf, r.Value);

        // Call f.GlyphAdvance before f.LoadGlyph because the LoadGlyph docs say
        // this about the buf argument: the segments become invalid to use once
        // the buffer is re-used.

        Int26_6 advance;
        try
        {
            advance = f.GlyphAdvance(buf, x, scale, hinting);
        }
        catch (Exception)
        {
            return default;
        }

        Segments segments;
        try
        {
            segments = f.LoadGlyph(buf, x, scale, null);
        }
        catch (Exception)
        {
            return default;
        }

        // Numerical notation used below:
        //  - 2    is an integer, "two"
        //  - 2:16 is a 26.6 fixed point number, "two and a quarter"
        //  - 2.5  is a float32 number, "two and a half"
        // Using 26.6 fixed point numbers means that there are 64 sub-pixel units
        // in 1 integer pixel unit.

        // Translate the sub-pixel bounding box from glyph space (where the glyph
        // origin is at (0:00, 0:00)) to dst space (where the glyph origin is at
        // the dot). dst space is the coordinate space that contains both the dot
        // (a sub-pixel position) and dr (an integer-pixel rectangle).
        var dBounds = segments.Bounds().Add(dot);

        // Quantize the sub-pixel bounds (dBounds) to integer-pixel bounds (dr).
        int drMinX = dBounds.MinX.Floor();
        int drMinY = dBounds.MinY.Floor();
        int drMaxX = dBounds.MaxX.Ceil();
        int drMaxY = dBounds.MaxY.Ceil();
        int width = drMaxX - drMinX;
        int height = drMaxY - drMinY;
        if (width < 0 || height < 0)
        {
            return default;
        }

        // Calculate the sub-pixel bias to convert from glyph space to rasterizer
        // space. In glyph space, the segments may be to the left or right and
        // above or below the glyph origin. In rasterizer space, the segments
        // should only be right and below (or equal to) the top-left corner (0.0,
        // 0.0). They should also be left and above (or equal to) the bottom-right
        // corner (width, height), as the rasterizer should enclose the glyph
        // bounding box.
        //
        // For example, suppose that dot.X was at the sub-pixel position 25:48,
        // three quarters of the way into the 26th pixel, and that bounds.Min.X was
        // 1:20. We then have dBounds.Min.X = 1:20 + 25:48 = 27:04, dr.Min.X = 27
        // and biasX = 25:48 - 27:00 = -1:16. A vertical stroke at 1:20 in glyph
        // space becomes (1:20 + -1:16) = 0:04 in rasterizer space. 0:04 as a
        // fixed.Int26_6 value is float32(4)/64.0 = 0.0625 as a float32 value.
        var biasX = dot.X - new Int26_6(drMinX << 6);
        var biasY = dot.Y - new Int26_6(drMinY << 6);

        // Configure the mask image, re-allocating its buffer if necessary.
        int nPixels = width * height;
        if (maskStore.Length < nPixels)
        {
            maskStore = new byte[2 * nPixels];
        }
        mask.Pix = maskStore.AsMemory(0, nPixels);
        mask.Stride = width;
        mask.Rect = new Rect(new Point(0, 0), new Point(width, height));

        // Rasterize the biased segments, converting from fixed.Int26_6 to float32.
        rast.Reset(width, height);
        rast.DrawOp = Op.Src;
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            switch (seg.Op)
            {
                case SegmentOp.MoveTo:
                    rast.MoveTo(
                        (seg.Args(0).X + biasX).Raw / 64f,
                        (seg.Args(0).Y + biasY).Raw / 64f);
                    break;
                case SegmentOp.LineTo:
                    rast.LineTo(
                        (seg.Args(0).X + biasX).Raw / 64f,
                        (seg.Args(0).Y + biasY).Raw / 64f);
                    break;
                case SegmentOp.QuadTo:
                    rast.QuadTo(
                        (seg.Args(0).X + biasX).Raw / 64f,
                        (seg.Args(0).Y + biasY).Raw / 64f,
                        (seg.Args(1).X + biasX).Raw / 64f,
                        (seg.Args(1).Y + biasY).Raw / 64f);
                    break;
                case SegmentOp.CubeTo:
                    rast.CubeTo(
                        (seg.Args(0).X + biasX).Raw / 64f,
                        (seg.Args(0).Y + biasY).Raw / 64f,
                        (seg.Args(1).X + biasX).Raw / 64f,
                        (seg.Args(1).Y + biasY).Raw / 64f,
                        (seg.Args(2).X + biasX).Raw / 64f,
                        (seg.Args(2).Y + biasY).Raw / 64f);
                    break;
            }
        }
        rast.Draw(mask, mask.Bounds(), UniformImages.Opaque, new Point());

        return new GlyphMetrics(
            new Rect(new Point(drMinX, drMinY), new Point(drMaxX, drMaxY)),
            mask,
            mask.Rect.Min,
            advance,
            x.Value != 0);
    }

    /// <summary>GlyphBounds satisfies the <see cref="IFontFace"/> interface.
    /// The glyph's ascent and descent equal -Min.Y and +Max.Y.</summary>
    public GlyphBounds GetGlyphBounds(Rune r)
    {
        var x = f.GlyphIndex(buf, r.Value);
        try
        {
            var (bounds, advance) = f.GlyphBounds(buf, x, scale, hinting);
            return new GlyphBounds(bounds, advance, x.Value != 0);
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>GlyphAdvance satisfies the <see cref="IFontFace"/> interface.</summary>
    public Int26_6 GetGlyphAdvance(Rune r)
    {
        var x = f.GlyphIndex(buf, r.Value);
        try
        {
            return f.GlyphAdvance(buf, x, scale, hinting);
        }
        catch (Exception)
        {
            return new Int26_6(0);
        }
    }
}