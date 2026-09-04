// Ported from Go src/image/png/paeth_test.go


using DotImage.Png;

namespace DotImage.Tests.Png;

public class PaethTests
{
    private static int SlowAbs(int x) => x < 0 ? -x : x;

    private static byte SlowPaeth(byte a, byte b, byte c)
    {
        int p = a + b - c;
        int pa = SlowAbs(p - a);
        int pb = SlowAbs(p - b);
        int pc = SlowAbs(p - c);
        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }

    private static void SlowFilterPaeth(Span<byte> cdat, ReadOnlySpan<byte> pdat, int bytesPerPixel)
    {
        for (int i = 0; i < bytesPerPixel; i++)
            cdat[i] += SlowPaeth(0, pdat[i], 0);
        for (int i = bytesPerPixel; i < cdat.Length; i++)
            cdat[i] += SlowPaeth(cdat[i - bytesPerPixel], pdat[i], pdat[i - bytesPerPixel]);
    }

    [Fact]
    public void Paeth_MatchesSlowImplementation()
    {
        for (int a = 0; a < 256; a += 15)
        {
            for (int b = 0; b < 256; b += 15)
            {
                for (int c = 0; c < 256; c += 15)
                {
                    byte got = Paeth.PaethPredictor((byte)a, (byte)b, (byte)c);
                    byte want = SlowPaeth((byte)a, (byte)b, (byte)c);
                    Assert.Equal(want, got);
                }
            }
        }
    }

    [Fact]
    public void FilterPaeth_MatchesSlowImplementation()
    {
        var pdat0 = new byte[32];
        var pdat1 = new byte[32];
        var pdat2 = new byte[32];
        var cdat0 = new byte[32];
        var cdat1 = new byte[32];
        var cdat2 = new byte[32];
        var rng = new Random(1);
        for (int bytesPerPixel = 1; bytesPerPixel <= 8; bytesPerPixel++)
        {
            for (int i = 0; i < 100; i++)
            {
                for (int j = 0; j < pdat0.Length; j++)
                {
                    pdat0[j] = (byte)rng.Next(256);
                    cdat0[j] = (byte)rng.Next(256);
                }
                pdat0.AsSpan().CopyTo(pdat1);
                pdat0.AsSpan().CopyTo(pdat2);
                cdat0.AsSpan().CopyTo(cdat1);
                cdat0.AsSpan().CopyTo(cdat2);
                Paeth.FilterPaeth(cdat1, pdat1, bytesPerPixel);
                SlowFilterPaeth(cdat2, pdat2, bytesPerPixel);
                Assert.True(cdat1.AsSpan().SequenceEqual(cdat2));
            }
        }
    }
}
