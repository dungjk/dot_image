// Ported from go/src/image/color/color_test.go


using DotImage.Color;

namespace DotImage.Tests.Color;

public class ColorTests
{
    [Fact]
    public void SqDiff_MatchesCanonicalImplementation()
    {
        static uint Orig(uint x, uint y)
        {
            uint d = x > y ? x - y : y - x;
            return (d * d) >> 2;
        }

        uint[] testCases =
        [
            0,
            1,
            2,
            0x0fffd,
            0x0fffe,
            0x0ffff,
            0x10000,
            0x10001,
            0x10002,
            0xfffffffd,
            0xfffffffe,
            0xffffffff,
        ];

        foreach (uint x in testCases)
        {
            foreach (uint y in testCases)
            {
                uint got = ColorUtil.SqDiff(x, y);
                uint want = Orig(x, y);
                Assert.Equal(want, got);
            }
        }

        var random = new Random(0);
        for (int i = 0; i < 10000; i++)
        {
            uint x = (uint)random.NextInt64(0, 1L << 32);
            uint y = (uint)random.NextInt64(0, 1L << 32);
            Assert.Equal(Orig(x, y), ColorUtil.SqDiff(x, y));
        }
    }

    [Fact]
    public void Palette_IndexAndConvert()
    {
        var p = new Palette(
        [
            new Rgba(0xff, 0xff, 0xff, 0xff),
            new Rgba(0x80, 0x00, 0x00, 0xff),
            new Rgba(0x7f, 0x00, 0x00, 0x7f),
            new Rgba(0x00, 0x00, 0x00, 0x7f),
            new Rgba(0x00, 0x00, 0x00, 0x00),
            new Rgba(0x40, 0x40, 0x40, 0x40),
        ]);

        for (int i = 0; i < p.Length; i++)
        {
            int j = p.Index(p[i]);
            Assert.Equal(i, j);
        }

        IColor got = p.Convert(new Rgba(0x80, 0x00, 0x00, 0x80))!;
        var want = new Rgba(0x7f, 0x00, 0x00, 0x7f);
        Assert.Equal(want, got);
    }
}
