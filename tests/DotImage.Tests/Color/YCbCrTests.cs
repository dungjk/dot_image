// Ported from go/src/image/color/ycbcr_test.go


using DotImage.Color;

namespace DotImage.Tests.Color;

public class YCbCrTests
{
    private static byte Delta(byte x, byte y) => x >= y ? (byte)(x - y) : (byte)(y - x);

    private static void AssertColorsEqual(IColor c0, IColor c1)
    {
        (uint r0, uint g0, uint b0, uint a0) = c0.Rgba();
        (uint r1, uint g1, uint b1, uint a1) = c1.Rgba();
        Assert.True(r0 == r1 && g0 == g1 && b0 == b1 && a0 == a1,
            $"got  0x{r0:x4} 0x{g0:x4} 0x{b0:x4} 0x{a0:x4}\n" +
            $"want 0x{r1:x4} 0x{g1:x4} 0x{b1:x4} 0x{a1:x4}");
    }

    [Fact]
    public void YCbCrRoundtrip()
    {
        for (int r = 0; r < 256; r += 7)
        {
            for (int g = 0; g < 256; g += 5)
            {
                for (int b = 0; b < 256; b += 3)
                {
                    byte r0 = (byte)r;
                    byte g0 = (byte)g;
                    byte b0 = (byte)b;
                    (byte y, byte cb, byte cr) = YCbCrUtil.RGBToYCbCr(r0, g0, b0);
                    (byte r1, byte g1, byte b1) = YCbCrUtil.YCbCrToRGB(y, cb, cr);
                    Assert.True(Delta(r0, r1) <= 2 && Delta(g0, g1) <= 2 && Delta(b0, b1) <= 2,
                        $"\nr0, g0, b0 = {r0}, {g0}, {b0}\ny,  cb, cr = {y}, {cb}, {cr}\nr1, g1, b1 = {r1}, {g1}, {b1}");
                }
            }
        }
    }

    [Fact]
    public void YCbCrToRGBConsistency()
    {
        for (int y = 0; y < 256; y += 7)
        {
            for (int cb = 0; cb < 256; cb += 5)
            {
                for (int cr = 0; cr < 256; cr += 3)
                {
                    var x = new YCbCr((byte)y, (byte)cb, (byte)cr);
                    (uint r0, uint g0, uint b0, _) = x.Rgba();
                    byte r1 = (byte)(r0 >> 8);
                    byte g1 = (byte)(g0 >> 8);
                    byte b1 = (byte)(b0 >> 8);
                    (byte r2, byte g2, byte b2) = YCbCrUtil.YCbCrToRGB(x.Y, x.Cb, x.Cr);
                    Assert.True(r1 == r2 && g1 == g2 && b1 == b2,
                        $"y, cb, cr = {y}, {cb}, {cr}\nr1, g1, b1 = {r1}, {g1}, {b1}\nr2, g2, b2 = {r2}, {g2}, {b2}");
                }
            }
        }
    }

    [Fact]
    public void YCbCrGray()
    {
        for (int i = 0; i < 256; i++)
        {
            var c0 = new YCbCr((byte)i, 0x80, 0x80);
            var c1 = new Gray((byte)i);
            AssertColorsEqual(c0, c1);
        }
    }

    [Fact]
    public void NYCbCrAAlpha()
    {
        for (int i = 0; i < 256; i++)
        {
            var c0 = new NYCbCrA(0xff, 0x80, 0x80, (byte)i);
            var c1 = new Alpha((byte)i);
            AssertColorsEqual(c0, c1);
        }
    }

    [Fact]
    public void NYCbCrAYCbCr()
    {
        for (int i = 0; i < 256; i++)
        {
            var c0 = new NYCbCrA((byte)i, 0x40, 0xc0, 0xff);
            var c1 = new YCbCr((byte)i, 0x40, 0xc0);
            AssertColorsEqual(c0, c1);
        }
    }

    [Fact]
    public void CMYKRoundtrip()
    {
        for (int r = 0; r < 256; r += 7)
        {
            for (int g = 0; g < 256; g += 5)
            {
                for (int b = 0; b < 256; b += 3)
                {
                    byte r0 = (byte)r;
                    byte g0 = (byte)g;
                    byte b0 = (byte)b;
                    (byte c, byte m, byte y, byte k) = YCbCrUtil.RGBToCMYK(r0, g0, b0);
                    (byte r1, byte g1, byte b1) = YCbCrUtil.CMYKToRGB(c, m, y, k);
                    Assert.True(Delta(r0, r1) <= 1 && Delta(g0, g1) <= 1 && Delta(b0, b1) <= 1,
                        $"\nr0, g0, b0 = {r0}, {g0}, {b0}\nc, m, y, k = {c}, {m}, {y}, {k}\nr1, g1, b1 = {r1}, {g1}, {b1}");
                }
            }
        }
    }

    [Fact]
    public void CMYKToRGBConsistency()
    {
        for (int c = 0; c < 256; c += 7)
        {
            for (int m = 0; m < 256; m += 5)
            {
                for (int y = 0; y < 256; y += 3)
                {
                    for (int k = 0; k < 256; k += 11)
                    {
                        var x = new Cmyk((byte)c, (byte)m, (byte)y, (byte)k);
                        (uint r0, uint g0, uint b0, _) = x.Rgba();
                        byte r1 = (byte)(r0 >> 8);
                        byte g1 = (byte)(g0 >> 8);
                        byte b1 = (byte)(b0 >> 8);
                        (byte r2, byte g2, byte b2) = YCbCrUtil.CMYKToRGB(x.C, x.M, x.Y, x.K);
                        Assert.True(r1 == r2 && g1 == g2 && b1 == b2,
                            $"c, m, y, k = {c}, {m}, {y}, {k}\nr1, g1, b1 = {r1}, {g1}, {b1}\nr2, g2, b2 = {r2}, {g2}, {b2}");
                    }
                }
            }
        }
    }

    [Fact]
    public void CMYKGray()
    {
        for (int i = 0; i < 256; i++)
        {
            AssertColorsEqual(new Cmyk(0x00, 0x00, 0x00, (byte)(255 - i)), new Gray((byte)i));
        }
    }
}
