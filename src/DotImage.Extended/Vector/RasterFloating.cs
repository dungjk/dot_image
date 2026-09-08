// Ported from golang.org/x/image/vector/raster_floating.go
// Floating-point math implementation of the vector graphics rasterizer.

namespace DotImage.Extended.Vector;

internal static class RasterFloating
{
    // almost256 scales a floating point value in the range [0, 1] to a uint8
    // value in the range [0x00, 0xff].
    internal const float Almost256 = 255.99998f;

    // almost65536 scales a floating point value in the range [0, 1] to a
    // uint16 value in the range [0x0000, 0xffff].
    internal const float Almost65536 = Almost256 * 256;

    internal static int FloatingFloor(float x) => (int)System.MathF.Floor(x);
    internal static int FloatingCeil(float x) => (int)System.MathF.Ceiling(x);

    internal static void FloatingLineTo(this Rasterizer z, float bx, float by)
    {
        float ax = z.penX, ay = z.penY;
        z.penX = bx;
        z.penY = by;
        float dir = 1;
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

        float x = ax;
        int y = FloatingFloor(ay);
        int yMax = FloatingCeil(by);
        if (yMax > z.size.Y)
        {
            yMax = z.size.Y;
        }
        int width = z.size.X;

        for (; y < yMax; y++)
        {
            float dy = System.MathF.Min(y + 1, by) - System.MathF.Max(y, ay);

            // The explicit float cast disables FMA for bit-exact results.
            float xNext = x + (float)(dy * dxdy);
            if (y < 0)
            {
                x = xNext;
                continue;
            }
            Span<float> buf = z.bufF32.AsSpan(y * width);
            float d = dy * dir;
            float x0 = x, x1 = xNext;
            if (x > xNext)
            {
                x0 = xNext;
                x1 = x;
            }
            int x0i = FloatingFloor(x0);
            float x0Floor = x0i;
            int x1i = FloatingCeil(x1);
            float x1Ceil = x1i;

            if (x1i <= x0i + 1)
            {
                float xmf = 0.5f * (x + xNext) - x0Floor;
                uint i;
                i = Clamp(x0i + 0, width);
                if (i < (uint)buf.Length)
                {
                    buf[(int)i] += d - (float)(d * xmf);
                }
                i = Clamp(x0i + 1, width);
                if (i < (uint)buf.Length)
                {
                    buf[(int)i] += (float)(d * xmf);
                }
            }
            else
            {
                float s = 1 / (x1 - x0);
                float x0f = x0 - x0Floor;
                float oneMinusX0f = 1 - x0f;
                float a0 = 0.5f * s * oneMinusX0f * oneMinusX0f;
                float x1f = x1 - x1Ceil + 1;
                float am = 0.5f * s * x1f * x1f;

                uint i;
                i = Clamp(x0i, width);
                if (i < (uint)buf.Length)
                {
                    buf[(int)i] += (float)(d * a0);
                }

                if (x1i == x0i + 2)
                {
                    i = Clamp(x0i + 1, width);
                    if (i < (uint)buf.Length)
                    {
                        buf[(int)i] += (float)(d * (1 - a0 - am));
                    }
                }
                else
                {
                    float a1 = s * (1.5f - x0f);
                    i = Clamp(x0i + 1, width);
                    if (i < (uint)buf.Length)
                    {
                        buf[(int)i] += (float)(d * (a1 - a0));
                    }
                    float dTimesS = (float)(d * s);
                    for (int xi = x0i + 2; xi < x1i - 1; xi++)
                    {
                        i = Clamp(xi, width);
                        if (i < (uint)buf.Length)
                        {
                            buf[(int)i] += dTimesS;
                        }
                    }
                    float a2 = a1 + (float)(s * (float)(x1i - x0i - 3));
                    i = Clamp(x1i - 1, width);
                    if (i < (uint)buf.Length)
                    {
                        buf[(int)i] += (float)(d * (1 - a2 - am));
                    }
                }

                i = Clamp(x1i, width);
                if (i < (uint)buf.Length)
                {
                    buf[(int)i] += (float)(d * am);
                }
            }

            x = xNext;
        }
    }

    internal static void FloatingAccumulateOpOver(Span<byte> dst, Span<float> src)
    {
        if (dst.Length < src.Length)
        {
            return;
        }

        float acc = 0;
        for (int i = 0; i < src.Length; i++)
        {
            acc += src[i];
            float a = acc;
            if (a < 0)
            {
                a = -a;
            }
            if (a > 1)
            {
                a = 1;
            }
            uint dstA = (uint)dst[i] * 0x101;
            uint maskA = (uint)(Almost65536 * a);
            uint outA = dstA * (0xffff - maskA) / 0xffff + maskA;
            dst[i] = (byte)(outA >> 8);
        }
    }

    internal static void FloatingAccumulateOpSrc(Span<byte> dst, Span<float> src)
    {
        if (dst.Length < src.Length)
        {
            return;
        }

        float acc = 0;
        for (int i = 0; i < src.Length; i++)
        {
            acc += src[i];
            float a = acc;
            if (a < 0)
            {
                a = -a;
            }
            if (a > 1)
            {
                a = 1;
            }
            dst[i] = (byte)(Almost256 * a);
        }
    }

    internal static void FloatingAccumulateMask(Span<uint> dst, Span<float> src)
    {
        if (dst.Length < src.Length)
        {
            return;
        }

        float acc = 0;
        for (int i = 0; i < src.Length; i++)
        {
            acc += src[i];
            float a = acc;
            if (a < 0)
            {
                a = -a;
            }
            if (a > 1)
            {
                a = 1;
            }
            dst[i] = (uint)(Almost65536 * a);
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
