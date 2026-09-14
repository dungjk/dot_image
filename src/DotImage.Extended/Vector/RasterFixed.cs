// Ported from golang.org/x/image/vector/raster_fixed.go
// Fixed-point math implementation of the vector graphics rasterizer.

namespace DotImage.Extended.Vector;

internal static class RasterFixed
{
    // ϕ is the number of binary digits after the fixed point.
    internal const int Phi = 9;

    internal const int FxOne = 1 << Phi;
    internal const int FxOneAndAHalf = (1 << Phi) + (1 << (Phi - 1));
    internal const int FxOneMinusIota = (1 << Phi) - 1; // Used for rounding up.

    internal static int FixedFloor(int x) => x >> Phi;
    internal static int FixedCeil(int x) => (x + FxOneMinusIota) >> Phi;

    internal static void FixedLineTo(this Rasterizer z, float bx, float by)
    {
        float ax = z.penX, ay = z.penY;
        z.penX = bx;
        z.penY = by;
        int dir = 1;
        if (ay > by)
        {
            dir = -1;
            float tmp;
            tmp = ax; ax = bx; bx = tmp;
            tmp = ay; ay = by; by = tmp;
        }
        // Horizontal line segments yield no change in coverage.
        if (by - ay <= 0.000001f)
        {
            return;
        }
        float dxdy = (bx - ax) / (by - ay);

        int ayPhi = (int)(ay * FxOne);
        int byPhi = (int)(by * FxOne);

        int x = (int)(ax * FxOne);
        int y = FixedFloor(ayPhi);
        int yMax = FixedCeil(byPhi);
        if (yMax > z.size.Y)
        {
            yMax = z.size.Y;
        }
        int width = z.size.X;

        for (; y < yMax; y++)
        {
            int dy = System.Math.Min((y + 1) << Phi, byPhi) - System.Math.Max(y << Phi, ayPhi);
            int xNext = x + (int)(dy * dxdy);
            if (y < 0)
            {
                x = xNext;
                continue;
            }
            Span<uint> buf = z.bufU32.AsSpan(y * width);
            int d = dy * dir;
            int x0 = x, x1 = xNext;
            if (x > xNext)
            {
                x0 = xNext;
                x1 = x;
            }
            int x0i = FixedFloor(x0);
            int x0Floor = x0i << Phi;
            int x1i = FixedCeil(x1);
            int x1Ceil = x1i << Phi;

            if (x1i <= x0i + 1)
            {
                int xmf = ((x + xNext) >> 1) - x0Floor;
                uint i;
                i = Clamp(x0i + 0, width);
                if (i < (uint)buf.Length)
                {
                    buf[(int)i] += (uint)(d * (FxOne - xmf));
                }
                i = Clamp(x0i + 1, width);
                if (i < (uint)buf.Length)
                {
                    buf[(int)i] += (uint)(d * xmf);
                }
            }
            else
            {
                int oneOverS = x1 - x0;
                int twoOverS = 2 * oneOverS;
                int x0f = x0 - x0Floor;
                int oneMinusX0f = FxOne - x0f;
                int oneMinusX0fSquared = oneMinusX0f * oneMinusX0f;
                int x1f = x1 - x1Ceil + FxOne;
                int x1fSquared = x1f * x1f;

                uint i;
                i = Clamp(x0i, width);
                if (i < (uint)buf.Length)
                {
                    int D = oneMinusX0fSquared;
                    D *= d;
                    D /= twoOverS;
                    buf[(int)i] += (uint)D;
                }

                if (x1i == x0i + 2)
                {
                    i = Clamp(x0i + 1, width);
                    if (i < (uint)buf.Length)
                    {
                        int D = (twoOverS << Phi) - oneMinusX0fSquared - x1fSquared;
                        D *= d;
                        D /= twoOverS;
                        buf[(int)i] += (uint)D;
                    }
                }
                else
                {
                    i = Clamp(x0i + 1, width);
                    if (i < (uint)buf.Length)
                    {
                        int D = ((FxOneAndAHalf - x0f) << (Phi + 1)) - oneMinusX0fSquared;
                        D *= d;
                        D /= twoOverS;
                        buf[(int)i] += (uint)D;
                    }
                    uint dTimesS = (uint)((d << (2 * Phi)) / oneOverS);
                    for (int xi = x0i + 2; xi < x1i - 1; xi++)
                    {
                        i = Clamp(xi, width);
                        if (i < (uint)buf.Length)
                        {
                            buf[(int)i] += dTimesS;
                        }
                    }

                    const int C = (1 << (Phi + 2)) - (FxOneAndAHalf << 1);
                    i = Clamp(x1i - 1, width);
                    if (i < (uint)buf.Length)
                    {
                        int D = (x1f << 1) + C;
                        D <<= Phi;
                        D -= x1fSquared;
                        D *= d;
                        D /= twoOverS;
                        buf[(int)i] += (uint)D;
                    }
                }

                i = Clamp(x1i, width);
                if (i < (uint)buf.Length)
                {
                    int D = x1fSquared;
                    D *= d;
                    D /= twoOverS;
                    buf[(int)i] += (uint)D;
                }
            }

            x = xNext;
        }
    }

    internal static void FixedAccumulateOpOver(Span<byte> dst, Span<uint> src)
    {
        if (dst.Length < src.Length)
        {
            return;
        }

        int acc = 0;
        for (int i = 0; i < src.Length; i++)
        {
            acc += (int)src[i];
            int a = acc;
            if (a < 0)
            {
                a = -a;
            }
            a >>= 2 * Phi - 16;
            if (a > 0xffff)
            {
                a = 0xffff;
            }
            uint dstA = (uint)dst[i] * 0x101;
            uint maskA = (uint)a;
            uint outA = dstA * (0xffff - maskA) / 0xffff + maskA;
            dst[i] = (byte)(outA >> 8);
        }
    }

    internal static void FixedAccumulateOpSrc(Span<byte> dst, Span<uint> src)
    {
        if (dst.Length < src.Length)
        {
            return;
        }

        int acc = 0;
        for (int i = 0; i < src.Length; i++)
        {
            acc += (int)src[i];
            int a = acc;
            if (a < 0)
            {
                a = -a;
            }
            a >>= 2 * Phi - 8;
            if (a > 0xff)
            {
                a = 0xff;
            }
            dst[i] = (byte)a;
        }
    }

    internal static void FixedAccumulateMask(Span<uint> buf)
    {
        int acc = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            acc += (int)buf[i];
            int a = acc;
            if (a < 0)
            {
                a = -a;
            }
            a >>= 2 * Phi - 16;
            if (a > 0xffff)
            {
                a = 0xffff;
            }
            buf[i] = (uint)a;
        }
    }

    private static uint Clamp(int i, int width)
    {
        if (i < 0)
        {
            return 0;
        }
        if (i < width)
        {
            return (uint)i;
        }
        return (uint)width;
    }
}
