using System;
using System.Text;
using DotImage.Extended.Font;
using DotImage.Extended.Math.Fixed;
using Xunit;

namespace DotImage.Extended.Tests;

public class FontTests
{
    private static readonly Int26_6 ToyAdvance = new(10 << 6);

    private sealed class ToyFace : IFontFace
    {
        public void Dispose() { }
        public GlyphMetrics GetGlyph(Point26_6 dot, Rune r) => throw new NotImplementedException();
        public GlyphBounds GetGlyphBounds(Rune r) => new(
            new Rectangle26_6(
                new Int26_6(2 << 6), new Int26_6(0),
                new Int26_6(6 << 6), new Int26_6(1 << 6)),
            ToyAdvance, true);
        public Int26_6 GetGlyphAdvance(Rune r) => ToyAdvance;
        public Int26_6 GetKern(Rune r0, Rune r1) => new(0);
        public FontMetrics Metrics => default;
    }

    [Fact]
    public void Bound()
    {
        var wantBounds = new Rectangle26_6[]
        {
            new(new Int26_6(0), new Int26_6(0), new Int26_6(0), new Int26_6(0)),
            new(new Int26_6(2 << 6), new Int26_6(0), new Int26_6(6 << 6), new Int26_6(1 << 6)),
            new(new Int26_6(2 << 6), new Int26_6(0), new Int26_6(16 << 6), new Int26_6(1 << 6)),
            new(new Int26_6(2 << 6), new Int26_6(0), new Int26_6(26 << 6), new Int26_6(1 << 6)),
        };

        var face = new ToyFace();
        for (int i = 0; i < wantBounds.Length; i++)
        {
            string s = new string('x', i);
            var (gotBounds, gotAdvance) = FontUtil.BoundString(face, s);
            Assert.Equal(wantBounds[i], gotBounds);

            Int26_6 wantAdvance = ToyAdvance * new Int26_6(i);
            Assert.Equal(wantAdvance, gotAdvance);
        }
    }
}