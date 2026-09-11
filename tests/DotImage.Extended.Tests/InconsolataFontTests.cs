using System.Text;
using DotImage;
using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Font;
using DotImage.Extended.Font.Inconsolata;
using DotImage.Extended.Math.Fixed;
using Range = DotImage.Extended.Font.Basic.Range;
using Xunit;

namespace DotImage.Extended.Tests;

public class InconsolataFontTests
{
    [Fact]
    public void Metrics()
    {
        var want = new FontMetrics(
            height: new Int26_6(16 << 6),
            ascent: new Int26_6(14 << 6),
            descent: new Int26_6(3 << 6),
            xHeight: new Int26_6(14 << 6),
            capHeight: new Int26_6(14 << 6),
            caretSlope: new Point(0, 1));

        Assert.Equal(want, Inconsolata.Regular8x16.Metrics);
        Assert.Equal(want, Inconsolata.Bold8x16.Metrics);
    }

    [Fact]
    public void GlyphMaskFidelity()
    {
        // The mask sub-image reported by GetGlyph (MaskOrigin) must line up with
        // the ported Go data -- the (rune - low + offset) layout of the
        // 289-glyph strip of height 17.
        int h = 17;

        foreach (var (digits, face, maskData, stride, width, left) in new[]
        {
            ("regular", Inconsolata.Regular8x16, Regular8x16Data.Data, 9, 9, 0),
            ("bold", Inconsolata.Bold8x16, Bold8x16Data.Data, 10, 10, -1),
        })
        {
            foreach (var rng in Inconsolata.Regular8x16.Ranges)
            {
                for (int r = rng.Low.Value; r < rng.High.Value; r++)
                {
                    var gm = face.GetGlyph(new Point26_6(new Int26_6(0), new Int26_6(14 << 6)), new Rune(r));
                    Assert.True(gm.Found, $"{digits}: rune U+{r:X4}");
                    Assert.Equal(new Int26_6(8 << 6), gm.Advance);
                    int row = (r - rng.Low.Value + rng.Offset) * h;
                    Assert.Equal(new Rect(new Point(left, 0), new Point(left + width, h)), gm.Bounds);
                    Assert.Equal(new Point(0, row), gm.MaskOrigin);

                    var mask = (AlphaImage)gm.Mask;
                    Assert.Equal(row * stride, mask.PixOffset(0, row));
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < stride; x++)
                        {
                            int off = row * stride + y * stride + x;
                            Assert.True(mask.AlphaAt(x, row + y).A == maskData[off],
                                $"{digits} U+{r:X4} x={x} y={y}: got {mask.AlphaAt(x, row + y).A:X2}, want {maskData[off]:X2}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void GlyphGeometry()
    {
        // Regular 'A' has a 9 wide glyph sitting at the dot. Bold 'A' is one
        // pixel wider and shifted left by 1 (Left == -1).
        var regular = Inconsolata.Regular8x16.GetGlyph(
            new Point26_6(new Int26_6(0), new Int26_6(14 << 6)), new Rune('A'));
        Assert.Equal(new Rect(new Point(0, 0), new Point(9, 17)), regular.Bounds);
        Assert.Equal(new Point(0, 33 * 17), regular.MaskOrigin);

        var bold = Inconsolata.Bold8x16.GetGlyph(
            new Point26_6(new Int26_6(0), new Int26_6(14 << 6)), new Rune('A'));
        Assert.Equal(new Rect(new Point(-1, 0), new Point(9, 17)), bold.Bounds);
        Assert.Equal(new Point(0, 33 * 17), bold.MaskOrigin);

        Assert.Equal(
            new Rectangle26_6(Int26_6.FromInt(0), Int26_6.FromInt(-14), Int26_6.FromInt(9), Int26_6.FromInt(3)),
            Inconsolata.Regular8x16.GetGlyphBounds(new Rune('A')).Bounds);
        Assert.Equal(
            new Rectangle26_6(Int26_6.FromInt(0), Int26_6.FromInt(-14), Int26_6.FromInt(10), Int26_6.FromInt(3)),
            Inconsolata.Bold8x16.GetGlyphBounds(new Rune('A')).Bounds);
    }

    [Fact]
    public void DrawGlyph()
    {
        // Drawing an opaque 'A' onto an RGBA dst reproduces the mask bytes.
        var gm = Inconsolata.Regular8x16.GetGlyph(
            new Point26_6(new Int26_6(0), new Int26_6(14 << 6)), new Rune('A'));
        var dst = Images.NewRgba(new Rect(new Point(0, 0), new Point(gm.Bounds.Dx(), gm.Bounds.Dy())));
        var drawer = new Drawer
        {
            Dst = dst,
            Face = Inconsolata.Regular8x16,
            Src = new UniformImage(new Rgba(0xff, 0xff, 0xff, 0xff)),
            Dot = new Point26_6(new Int26_6(0), new Int26_6(14 << 6)),
        };
        drawer.DrawString("A");

        var mask = (AlphaImage)gm.Mask;
        for (int y = 0; y < gm.Bounds.Dy(); y++)
        {
            for (int x = 0; x < gm.Bounds.Dx(); x++)
            {
                Assert.Equal(
                    mask.AlphaAt(x, 33 * 17 + y).A,
                    dst.Rgba64At(x, y).A >> 8);
            }
        }
    }

    [Fact]
    public void AllRangesResolve()
    {
        // Every rune in every declared range must resolve to its glyph, and the
        // mask sub-image must line up with the (rune - low) + offset layout.
        foreach (var face in new[] { Inconsolata.Regular8x16, Inconsolata.Bold8x16 })
        {
            foreach (var rng in Inconsolata.Regular8x16.Ranges)
            {
                for (int r = rng.Low.Value; r < rng.High.Value; r++)
                {
                    var gm = face.GetGlyph(new Point26_6(new Int26_6(0), new Int26_6(14 << 6)), new Rune(r));
                    Assert.True(gm.Found, $"rune U+{r:X4}");
                    int want = (r - rng.Low.Value + rng.Offset) * 17;
                    Assert.Equal(new Point(0, want), gm.MaskOrigin);
                }
            }
        }
    }
}