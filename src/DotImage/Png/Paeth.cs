// Ported from Go src/image/png/paeth.go


namespace DotImage.Png;

public static class Paeth
{
    private static int Abs(int x)
    {
        int m = x >> 31;
        return (x ^ m) - m;
    }

    public static byte PaethPredictor(byte a, byte b, byte c)
    {
        int pc = c;
        int pa = b - pc;
        int pb = a - pc;
        pc = Abs(pa + pb);
        pa = Abs(pa);
        pb = Abs(pb);
        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }

    public static void FilterPaeth(Span<byte> cdat, ReadOnlySpan<byte> pdat, int bytesPerPixel)
    {
        int a, b, c, pa, pb, pc;
        for (int i = 0; i < bytesPerPixel; i++)
        {
            a = 0;
            c = 0;
            for (int j = i; j < cdat.Length; j += bytesPerPixel)
            {
                b = pdat[j];
                pa = b - c;
                pb = a - c;
                pc = Abs(pa + pb);
                pa = Abs(pa);
                pb = Abs(pb);
                if (pa <= pb && pa <= pc)
                {
                    // No-op.
                }
                else if (pb <= pc)
                {
                    a = b;
                }
                else
                {
                    a = c;
                }
                a += cdat[j];
                a &= 0xff;
                cdat[j] = (byte)a;
                c = b;
            }
        }
    }
}
