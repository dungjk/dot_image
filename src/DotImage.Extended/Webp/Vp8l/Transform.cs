using System;

namespace DotImage.Extended.Webp.Vp8l;

internal static class TransformUtil
{
    public const int TransformTypePredictor = 0;
    public const int TransformTypeCrossColor = 1;
    public const int TransformTypeSubtractGreen = 2;
    public const int TransformTypeColorIndexing = 3;
    public const int NTransformTypes = 4;

    public static int NTiles(int size, uint bits) => (size + (1 << (int)bits) - 1) >> (int)bits;

    public static byte[] InverseTransform(int type, Transform t, byte[] pix, int h)
    {
        return type switch
        {
            TransformTypePredictor => InversePredictor(t, pix, h),
            TransformTypeCrossColor => InverseCrossColor(t, pix, h),
            TransformTypeSubtractGreen => InverseSubtractGreen(pix),
            TransformTypeColorIndexing => InverseColorIndexing(t, pix, h),
            _ => pix,
        };
    }

    private static byte[] InversePredictor(Transform t, byte[] pix, int h)
    {
        if (t.OldWidth == 0 || h == 0) return pix;
        pix[3] += 0xff;
        int p = 4;
        for (int x = 1; x < t.OldWidth; x++)
        {
            pix[p + 0] += pix[p - 4];
            pix[p + 1] += pix[p - 3];
            pix[p + 2] += pix[p - 2];
            pix[p + 3] += pix[p - 1];
            p += 4;
        }
        int top = 0;
        int tilesPerRow = NTiles(t.OldWidth, t.Bits);
        for (int y = 1; y < h; y++)
        {
            pix[p + 0] += pix[top + 0];
            pix[p + 1] += pix[top + 1];
            pix[p + 2] += pix[top + 2];
            pix[p + 3] += pix[top + 3];
            p += 4; top += 4;

            int q = 4 * (y >> (int)t.Bits) * tilesPerRow;
            int predictorMode = t.Pix[q + 1] & 0x0f;
            q += 4;
            int mask = (1 << (int)t.Bits) - 1;
            for (int x = 1; x < t.OldWidth; x++)
            {
                if ((x & mask) == 0)
                {
                    predictorMode = t.Pix[q + 1] & 0x0f;
                    q += 4;
                }
                switch (predictorMode)
                {
                    case 0: pix[p + 3] += 0xff; break;
                    case 1:
                        pix[p + 0] += pix[p - 4]; pix[p + 1] += pix[p - 3];
                        pix[p + 2] += pix[p - 2]; pix[p + 3] += pix[p - 1]; break;
                    case 2:
                        pix[p + 0] += pix[top + 0]; pix[p + 1] += pix[top + 1];
                        pix[p + 2] += pix[top + 2]; pix[p + 3] += pix[top + 3]; break;
                    case 3:
                        pix[p + 0] += pix[top + 4]; pix[p + 1] += pix[top + 5];
                        pix[p + 2] += pix[top + 6]; pix[p + 3] += pix[top + 7]; break;
                    case 4:
                        pix[p + 0] += pix[top - 4]; pix[p + 1] += pix[top - 3];
                        pix[p + 2] += pix[top - 2]; pix[p + 3] += pix[top - 1]; break;
                    case 5:
                        pix[p + 0] += Avg2(Avg2(pix[p - 4], pix[top + 4]), pix[top + 0]);
                        pix[p + 1] += Avg2(Avg2(pix[p - 3], pix[top + 5]), pix[top + 1]);
                        pix[p + 2] += Avg2(Avg2(pix[p - 2], pix[top + 6]), pix[top + 2]);
                        pix[p + 3] += Avg2(Avg2(pix[p - 1], pix[top + 7]), pix[top + 3]); break;
                    case 6:
                        pix[p + 0] += Avg2(pix[p - 4], pix[top - 4]);
                        pix[p + 1] += Avg2(pix[p - 3], pix[top - 3]);
                        pix[p + 2] += Avg2(pix[p - 2], pix[top - 2]);
                        pix[p + 3] += Avg2(pix[p - 1], pix[top - 1]); break;
                    case 7:
                        pix[p + 0] += Avg2(pix[p - 4], pix[top + 0]);
                        pix[p + 1] += Avg2(pix[p - 3], pix[top + 1]);
                        pix[p + 2] += Avg2(pix[p - 2], pix[top + 2]);
                        pix[p + 3] += Avg2(pix[p - 1], pix[top + 3]); break;
                    case 8:
                        pix[p + 0] += Avg2(pix[top - 4], pix[top + 0]);
                        pix[p + 1] += Avg2(pix[top - 3], pix[top + 1]);
                        pix[p + 2] += Avg2(pix[top - 2], pix[top + 2]);
                        pix[p + 3] += Avg2(pix[top - 1], pix[top + 3]); break;
                    case 9:
                        pix[p + 0] += Avg2(pix[top + 0], pix[top + 4]);
                        pix[p + 1] += Avg2(pix[top + 1], pix[top + 5]);
                        pix[p + 2] += Avg2(pix[top + 2], pix[top + 6]);
                        pix[p + 3] += Avg2(pix[top + 3], pix[top + 7]); break;
                    case 10:
                        pix[p + 0] += Avg2(Avg2(pix[p - 4], pix[top - 4]), Avg2(pix[top + 0], pix[top + 4]));
                        pix[p + 1] += Avg2(Avg2(pix[p - 3], pix[top - 3]), Avg2(pix[top + 1], pix[top + 5]));
                        pix[p + 2] += Avg2(Avg2(pix[p - 2], pix[top - 2]), Avg2(pix[top + 2], pix[top + 6]));
                        pix[p + 3] += Avg2(Avg2(pix[p - 1], pix[top - 1]), Avg2(pix[top + 3], pix[top + 7])); break;
                    case 11:
                    {
                        int l0 = pix[p - 4], l1 = pix[p - 3], l2 = pix[p - 2], l3 = pix[p - 1];
                        int c0 = pix[top - 4], c1 = pix[top - 3], c2 = pix[top - 2], c3 = pix[top - 1];
                        int t0 = pix[top + 0], t1 = pix[top + 1], t2 = pix[top + 2], t3 = pix[top + 3];
                        int l = Abs(c0 - t0) + Abs(c1 - t1) + Abs(c2 - t2) + Abs(c3 - t3);
                        int tTotal = Abs(c0 - l0) + Abs(c1 - l1) + Abs(c2 - l2) + Abs(c3 - l3);
                        if (l < tTotal) { pix[p + 0] += (byte)l0; pix[p + 1] += (byte)l1; pix[p + 2] += (byte)l2; pix[p + 3] += (byte)l3; }
                        else { pix[p + 0] += (byte)t0; pix[p + 1] += (byte)t1; pix[p + 2] += (byte)t2; pix[p + 3] += (byte)t3; }
                        break;
                    }
                    case 12:
                        pix[p + 0] += ClampAddSubtractFull(pix[p - 4], pix[top + 0], pix[top - 4]);
                        pix[p + 1] += ClampAddSubtractFull(pix[p - 3], pix[top + 1], pix[top - 3]);
                        pix[p + 2] += ClampAddSubtractFull(pix[p - 2], pix[top + 2], pix[top - 2]);
                        pix[p + 3] += ClampAddSubtractFull(pix[p - 1], pix[top + 3], pix[top - 1]); break;
                    case 13:
                        pix[p + 0] += ClampAddSubtractHalf(Avg2(pix[p - 4], pix[top + 0]), pix[top - 4]);
                        pix[p + 1] += ClampAddSubtractHalf(Avg2(pix[p - 3], pix[top + 1]), pix[top - 3]);
                        pix[p + 2] += ClampAddSubtractHalf(Avg2(pix[p - 2], pix[top + 2]), pix[top - 2]);
                        pix[p + 3] += ClampAddSubtractHalf(Avg2(pix[p - 1], pix[top + 3]), pix[top - 1]); break;
                }
                p += 4; top += 4;
            }
        }
        return pix;
    }

    private static byte[] InverseCrossColor(Transform t, byte[] pix, int h)
    {
        int greenToRed = 0, greenToBlue = 0, redToBlue = 0;
        int p = 0;
        int mask = (1 << (int)t.Bits) - 1;
        int tilesPerRow = NTiles(t.OldWidth, t.Bits);
        for (int y = 0; y < h; y++)
        {
            int q = 4 * (y >> (int)t.Bits) * tilesPerRow;
            for (int x = 0; x < t.OldWidth; x++)
            {
                if ((x & mask) == 0)
                {
                    redToBlue = (sbyte)t.Pix[q + 0];
                    greenToBlue = (sbyte)t.Pix[q + 1];
                    greenToRed = (sbyte)t.Pix[q + 2];
                    q += 4;
                }
                byte red = pix[p + 0];
                byte green = pix[p + 1];
                byte blue = pix[p + 2];
                red += (byte)((uint)(greenToRed * (sbyte)green) >> 5);
                blue += (byte)((uint)(greenToBlue * (sbyte)green) >> 5);
                blue += (byte)((uint)(redToBlue * (sbyte)red) >> 5);
                pix[p + 0] = red;
                pix[p + 2] = blue;
                p += 4;
            }
        }
        return pix;
    }

    private static byte[] InverseSubtractGreen(byte[] pix)
    {
        for (int p = 0; p < pix.Length; p += 4)
        {
            byte green = pix[p + 1];
            pix[p + 0] += green;
            pix[p + 2] += green;
        }
        return pix;
    }

    private static byte[] InverseColorIndexing(Transform t, byte[] pix, int h)
    {
        if (t.Bits == 0)
        {
            for (int pp = 0; pp < pix.Length; pp += 4)
            {
                uint i = 4u * pix[pp + 1];
                pix[pp + 0] = t.Pix[i + 0];
                pix[pp + 1] = t.Pix[i + 1];
                pix[pp + 2] = t.Pix[i + 2];
                pix[pp + 3] = t.Pix[i + 3];
            }
            return pix;
        }

        uint vMask = 0, bitsPerPixel = (uint)(8 >> (int)t.Bits);
        int xMask = 0;
        switch (t.Bits)
        {
            case 1: vMask = 0x0f; xMask = 0x01; break;
            case 2: vMask = 0x03; xMask = 0x03; break;
            case 3: vMask = 0x01; xMask = 0x07; break;
        }

        Guards.EnsureDecodeSize(t.OldWidth, h, 4);
        var dst = new byte[4 * t.OldWidth * h];
        int d = 0, p = 0;
        uint v = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < t.OldWidth; x++)
            {
                if ((x & xMask) == 0)
                {
                    v = pix[p + 1];
                    p += 4;
                }
                uint i = 4 * (v & vMask);
                dst[d + 0] = t.Pix[i + 0];
                dst[d + 1] = t.Pix[i + 1];
                dst[d + 2] = t.Pix[i + 2];
                dst[d + 3] = t.Pix[i + 3];
                d += 4;
                v >>= (int)bitsPerPixel;
            }
        }
        return dst;
    }

    private static int Abs(int x) => x < 0 ? -x : x;

    private static byte Avg2(byte a, byte b) => (byte)((a + b) / 2);

    private static byte ClampAddSubtractFull(byte a, byte b, byte c)
    {
        int x = a + b - c;
        return (byte)(x < 0 ? 0 : x > 255 ? 255 : x);
    }

    private static byte ClampAddSubtractHalf(byte a, byte b)
    {
        int x = a + (a - b) / 2;
        return (byte)(x < 0 ? 0 : x > 255 ? 255 : x);
    }
}

internal sealed class Transform
{
    public uint TransformType;
    public int OldWidth;
    public uint Bits;
    public byte[] Pix = Array.Empty<byte>();
}
