using System.Text;
using DotImage;
using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Font;
using DotImage.Extended.Math.Fixed;
using DotImage.Extended.Font.Basic;
using Xunit;

namespace DotImage.Extended.Tests;

public class BasicFontTests
{
    [Fact]
    public void Metrics()
    {
        var want = new FontMetrics(
            height: new Int26_6(832),
            ascent: new Int26_6(704),
            descent: new Int26_6(128),
            xHeight: new Int26_6(704),
            capHeight: new Int26_6(704),
            caretSlope: new Point(0, 1));

        Assert.Equal(want, Face7x13.Instance.Metrics);
    }

    [Fact]
    public void GlyphA()
    {
        var face = Face7x13.Instance;
        Assert.Equal(new Int26_6(448), face.GetGlyphAdvance(new Rune('A')));

        // GetGlyph geometry must match basicfont.Face.Glyph:
        // dot (0, 11px) -> dr {0,0,6,13}, maskp {0, 33*13}, advance 7px.
        var gm = face.GetGlyph(new Point26_6(new Int26_6(0), new Int26_6(11 << 6)), new Rune('A'));
        Assert.Equal(new Int26_6(7 << 6), gm.Advance);
        Assert.Equal(new Rect(new Point(0, 0), new Point(6, 13)), gm.Bounds);
        Assert.Equal(new Point(0, 33 * 13), gm.MaskOrigin);

        // The mask block for 'A' must equal the ported Go mask7x13 data
        // (rune 65: row index (65-0x20)+0 = 33 of the 96 6x13 glyphs).
        int off = 33 * 6 * 13;
        var mask = (AlphaImage)gm.Mask;
        for (int y = 0; y < 13; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                Assert.Equal(Mask7X13.Data[off + y * 6 + x], mask.AlphaAt(x, 33 * 13 + y).A);
            }
        }

        // Drawing onto an RGBA dst with an opaque source reproduces the mask.
        var dst = Images.NewRgba(new Rect(new Point(0, 0), new Point(6, 13)));
        var drawer = new Drawer
        {
            Dst = dst,
            Face = face,
            Src = new UniformImage(new Rgba(0xff, 0xff, 0xff, 0xff)),
            Dot = new Point26_6(new Int26_6(0), new Int26_6(11 << 6)),
        };
        drawer.DrawString("A");
        for (int y = 0; y < 13; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                Assert.Equal(Mask7X13.Data[off + y * 6 + x], dst.Rgba64At(x, y).A >> 8);
            }
        }
    }
}