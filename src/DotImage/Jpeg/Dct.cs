// Ported from Go src/image/jpeg/dct.go


namespace DotImage.Jpeg;

internal struct Block
{
    public int[] Data;

    public Block()
    {
        Data = new int[JpegConstants.BlockSize];
    }

    public int this[int i]
    {
        get => Data[i];
        set => Data[i] = value;
    }

    public Block Clone() => new() { Data = (int[])Data.Clone() };
}

internal static class Dct
{
    private const long Cos1 = 1130768441178740757L;
    private const long Sin1 = 224923827593068887L;
    private const long Cos3 = 958619196450722178L;
    private const long Sin3 = 640528868967736374L;
    private const long Sqrt2 = 1630477228166597777L;
    private const long Sqrt2Cos6 = 623956622067911264L;
    private const long Sqrt2Sin6 = 1506364539328854985L;
    private const long Sqrt2Inv = 815238614083298888L;
    private const long Sqrt2InvCos6 = 311978311033955632L;
    private const long Sqrt2InvSin6 = 753182269664427492L;

    private static int C(long x, int bits) =>
        (int)((x + (1L << (59 - bits))) >> (60 - bits));

    private static (int y0, int y1) DctBox(int x0, int x1, int kcos, int ksin)
    {
        int ksum = kcos * (x0 + x1);
        int y0 = ksum + (ksin - kcos) * x1;
        int y1 = ksum - (kcos + ksin) * x0;
        return (y0, y1);
    }

    internal static void Fdct(ref Block b)
    {
        FdctCols(ref b);
        FdctRows(ref b);
    }

    private static void FdctCols(ref Block b)
    {
        for (int i = 0; i < 8; i++)
        {
            int x0 = b[0 * 8 + i];
            int x1 = b[1 * 8 + i];
            int x2 = b[2 * 8 + i];
            int x3 = b[3 * 8 + i];
            int x4 = b[4 * 8 + i];
            int x5 = b[5 * 8 + i];
            int x6 = b[6 * 8 + i];
            int x7 = b[7 * 8 + i];

            (x0, x7) = (x0 + x7, x0 - x7);
            (x1, x6) = (x1 + x6, x1 - x6);
            (x2, x5) = (x2 + x5, x2 - x5);
            (x3, x4) = (x3 + x4, x3 - x4);

            (x4, x7) = DctBox(x4, x7, C(Cos3, 18), C(Sin3, 18));
            (x5, x6) = DctBox(x5, x6, C(Cos1, 18), C(Sin1, 18));

            (x0, x3) = (x0 + x3, x0 - x3);
            (x1, x2) = (x1 + x2, x1 - x2);

            (x2, x3) = DctBox(x2, x3, C(Sqrt2Cos6, 18), C(Sqrt2Sin6, 18));

            (x0, x1) = (x0 + x1, x0 - x1);

            b[0 * 8 + i] = (x0 - 128 * 8) << 18;
            b[4 * 8 + i] = x1 << 18;
            b[2 * 8 + i] = x2;
            b[6 * 8 + i] = x3;

            (x4, x6) = (x4 + x6, x4 - x6);
            (x7, x5) = (x7 + x5, x7 - x5);

            x5 = (x5 >> 12) * C(Sqrt2, 12);
            x6 = (x6 >> 12) * C(Sqrt2, 12);
            (x7, x4) = (x7 + x4, x7 - x4);

            b[1 * 8 + i] = x7;
            b[3 * 8 + i] = x5;
            b[5 * 8 + i] = x6;
            b[7 * 8 + i] = x4;
        }
    }

    private static void FdctRows(ref Block b)
    {
        for (int i = 0; i < 8; i++)
        {
            int baseIdx = 8 * i;
            int x0 = b[baseIdx];
            int x1 = b[baseIdx + 1];
            int x2 = b[baseIdx + 2];
            int x3 = b[baseIdx + 3];
            int x4 = b[baseIdx + 4];
            int x5 = b[baseIdx + 5];
            int x6 = b[baseIdx + 6];
            int x7 = b[baseIdx + 7];

            (x0, x7) = (x0 + x7, x0 - x7);
            (x1, x6) = (x1 + x6, x1 - x6);
            (x2, x5) = (x2 + x5, x2 - x5);
            (x3, x4) = (x3 + x4, x3 - x4);

            (x4, x7) = DctBox(x4 >> 14, x7 >> 14, C(Cos3, 14), C(Sin3, 14));
            (x5, x6) = DctBox(x5 >> 14, x6 >> 14, C(Cos1, 14), C(Sin1, 14));
            (x0, x3) = (x0 + x3, x0 - x3);
            (x1, x2) = (x1 + x2, x1 - x2);

            (x2, x3) = DctBox(x2 >> 14, x3 >> 14, C(Sqrt2Cos6, 14), C(Sqrt2Sin6, 14));
            (x0, x1) = (x0 + x1, x0 - x1);
            (x4, x6) = (x4 + x6, x4 - x6);
            (x7, x5) = (x7 + x5, x7 - x5);

            x5 = (x5 >> 14) * C(Sqrt2, 14);
            x6 = (x6 >> 14) * C(Sqrt2, 14);
            (x7, x4) = (x7 + x4, x7 - x4);

            x0 = (x0 + (1 << 17)) >> 18;
            x1 = (x1 + (1 << 17)) >> 18;
            x2 = (x2 + (1 << 17)) >> 18;
            x3 = (x3 + (1 << 17)) >> 18;
            x4 = (x4 + (1 << 17)) >> 18;
            x5 = (x5 + (1 << 17)) >> 18;
            x6 = (x6 + (1 << 17)) >> 18;
            x7 = (x7 + (1 << 17)) >> 18;

            b[baseIdx] = x0;
            b[baseIdx + 1] = x7;
            b[baseIdx + 2] = x2;
            b[baseIdx + 3] = x5;
            b[baseIdx + 4] = x1;
            b[baseIdx + 5] = x6;
            b[baseIdx + 6] = x3;
            b[baseIdx + 7] = x4;
        }
    }

    internal static void Idct(ref Block b)
    {
        IdctRows(ref b);
        IdctCols(ref b);
    }

    private static void IdctRows(ref Block b)
    {
        for (int i = 0; i < 8; i++)
        {
            int baseIdx = 8 * i;
            int x0 = b[baseIdx];
            int x7 = b[baseIdx + 1];
            int x2 = b[baseIdx + 2];
            int x5 = b[baseIdx + 3];
            int x1 = b[baseIdx + 4];
            int x6 = b[baseIdx + 5];
            int x3 = b[baseIdx + 6];
            int x4 = b[baseIdx + 7];

            x0 <<= 17;
            x1 <<= 17;
            (x0, x1) = (x0 + x1, x0 - x1);

            (x2, x3) = DctBox(x2, x3, C(Sqrt2InvCos6, 18), -C(Sqrt2InvSin6, 18));
            (x1, x2) = (x1 + x2, x1 - x2);
            (x0, x3) = (x0 + x3, x0 - x3);

            x4 <<= 7;
            x7 <<= 7;
            (x7, x4) = (x7 + x4, x7 - x4);

            x6 = x6 * C(Sqrt2Inv, 8);
            x5 = x5 * C(Sqrt2Inv, 8);

            (x7, x5) = (x7 + x5, x7 - x5);
            (x4, x6) = (x4 + x6, x4 - x6);

            (x4, x7) = DctBox(x4 >> 2, x7 >> 2, C(Cos3, 12), -C(Sin3, 12));
            (x5, x6) = DctBox(x5 >> 2, x6 >> 2, C(Cos1, 12), -C(Sin1, 12));

            (x0, x7) = (x0 + x7, x0 - x7);
            (x1, x6) = (x1 + x6, x1 - x6);
            (x2, x5) = (x2 + x5, x2 - x5);
            (x3, x4) = (x3 + x4, x3 - x4);

            b[baseIdx] = x0;
            b[baseIdx + 1] = x1;
            b[baseIdx + 2] = x2;
            b[baseIdx + 3] = x3;
            b[baseIdx + 4] = x4;
            b[baseIdx + 5] = x5;
            b[baseIdx + 6] = x6;
            b[baseIdx + 7] = x7;
        }
    }

    private static void IdctCols(ref Block b)
    {
        for (int i = 0; i < 8; i++)
        {
            int x0 = b[0 * 8 + i];
            int x7 = b[1 * 8 + i];
            int x2 = b[2 * 8 + i];
            int x5 = b[3 * 8 + i];
            int x1 = b[4 * 8 + i];
            int x6 = b[5 * 8 + i];
            int x3 = b[6 * 8 + i];
            int x4 = b[7 * 8 + i];

            x0 += 1 << 19;

            (x0, x1) = ((x0 + x1) >> 2, (x0 - x1) >> 2);
            (x2, x3) = DctBox(x2 >> 13, x3 >> 13, C(Sqrt2InvCos6, 12), -C(Sqrt2InvSin6, 12));
            (x1, x2) = (x1 + x2, x1 - x2);
            (x0, x3) = (x0 + x3, x0 - x3);

            (x7, x4) = (x7 + x4, x7 - x4);

            x5 = (x5 >> 13) * C(Sqrt2Inv, 14);
            x6 = (x6 >> 13) * C(Sqrt2Inv, 14);

            (x7, x5) = (x7 + x5, x7 - x5);
            (x4, x6) = (x4 + x6, x4 - x6);

            (x4, x7) = DctBox(x4 >> 14, x7 >> 14, C(Cos3, 12), -C(Sin3, 12));
            (x5, x6) = DctBox(x5 >> 14, x6 >> 14, C(Cos1, 12), -C(Sin1, 12));

            (x0, x7) = (x0 + x7, x0 - x7);
            (x1, x6) = (x1 + x6, x1 - x6);
            (x2, x5) = (x2 + x5, x2 - x5);
            (x3, x4) = (x3 + x4, x3 - x4);

            x0 >>= 18;
            x1 >>= 18;
            x2 >>= 18;
            x3 >>= 18;
            x4 >>= 18;
            x5 >>= 18;
            x6 >>= 18;
            x7 >>= 18;

            b[0 * 8 + i] = x0;
            b[1 * 8 + i] = x1;
            b[2 * 8 + i] = x2;
            b[3 * 8 + i] = x3;
            b[4 * 8 + i] = x4;
            b[5 * 8 + i] = x5;
            b[6 * 8 + i] = x6;
            b[7 * 8 + i] = x7;
        }
    }
}
