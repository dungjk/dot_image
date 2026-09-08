// Ported from golang.org/x/image/draw/scale.go and golang.org/x/image/draw/impl.go

using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Math.F64;
using DotMath = System.Math;

namespace DotImage.Extended.Draw;

internal static partial class ScaleImpl
{
    // abs is like math.Abs, but it doesn't care about negative zero, infinities
    // or NaNs.
    internal static double Abs(double f) => f < 0 ? -f : f;

    // ftou converts the range [0.0, 1.0] to [0, 0xffff].
    internal static ushort Ftou(double f)
    {
        int i = (int)(0xffff * f + 0.5);
        if (i > 0xffff) return 0xffff;
        if (i > 0) return (ushort)i;
        return 0;
    }

    // fffftou converts the range [0.0, 65535.0] to [0, 0xffff].
    internal static ushort Fffftou(double f)
    {
        int i = (int)(f + 0.5);
        if (i > 0xffff) return 0xffff;
        if (i > 0) return (ushort)i;
        return 0;
    }

    // invert returns the inverse of m.
    internal static Aff3 Invert(Aff3 m)
    {
        double m00 = +m[3 * 1 + 1];
        double m01 = -m[3 * 0 + 1];
        double m02 = +m[3 * 1 + 2] * m[3 * 0 + 1] - m[3 * 1 + 1] * m[3 * 0 + 2];
        double m10 = -m[3 * 1 + 0];
        double m11 = +m[3 * 0 + 0];
        double m12 = +m[3 * 1 + 0] * m[3 * 0 + 2] - m[3 * 1 + 2] * m[3 * 0 + 0];

        double det = m00 * m11 - m10 * m01;

        return new Aff3(m00 / det, m01 / det, m02 / det, m10 / det, m11 / det, m12 / det);
    }

    internal static Aff3 MatMul(Aff3 p, Aff3 q) =>
        new Aff3(
            p[0] * q[0] + p[1] * q[3],
            p[0] * q[1] + p[1] * q[4],
            p[0] * q[2] + p[1] * q[5] + p[2],
            p[3] * q[0] + p[4] * q[3],
            p[3] * q[1] + p[4] * q[4],
            p[3] * q[2] + p[4] * q[5] + p[5]);

    // transformRect returns a rectangle dr that contains sr transformed by s2d.
    internal static Rect TransformRect(Aff3 s2d, Rect sr)
    {
        var ps = new[]
        {
            new Point(sr.Min.X, sr.Min.Y),
            new Point(sr.Max.X, sr.Min.Y),
            new Point(sr.Min.X, sr.Max.Y),
            new Point(sr.Max.X, sr.Max.Y),
        };
        Rect dr = default;
        for (int i = 0; i < 4; i++)
        {
            double sxf = ps[i].X;
            double syf = ps[i].Y;
            int dx = (int)DotMath.Floor(s2d[0] * sxf + s2d[1] * syf + s2d[2]);
            int dy = (int)DotMath.Floor(s2d[3] * sxf + s2d[4] * syf + s2d[5]);

            if (i == 0)
            {
                dr = new Rect(new Point(dx, dy), new Point(dx + 1, dy + 1));
                continue;
            }

            if (dr.Min.X > dx) dr = new Rect(new Point(dx, dr.Min.Y), dr.Max);
            dx++;
            if (dr.Max.X < dx) dr = new Rect(dr.Min, new Point(dx, dr.Max.Y));

            if (dr.Min.Y > dy) dr = new Rect(new Point(dr.Min.X, dy), dr.Max);
            dy++;
            if (dr.Max.Y < dy) dr = new Rect(dr.Min, new Point(dr.Max.X, dy));
        }
        return dr;
    }

    internal static void TransformUniform(IWritableImage dst, Rect dr, Rect adr, Aff3 d2s, UniformImage src, Rect sr, Point bias, Op op)
    {
        (uint pr, uint pg, uint pb, uint pa) = src.C.Rgba();
        uint pa1;
        if (op == Op.Over)
        {
            pa1 = 0xffff - pa;
        }
        else
        {
            pa1 = 0;
        }

        for (int dy = adr.Min.Y; dy < adr.Max.Y; dy++)
        {
            double dyf = dr.Min.Y + dy + 0.5;
            for (int dx = adr.Min.X; dx < adr.Max.X; dx++)
            {
                double dxf = dr.Min.X + dx + 0.5;
                int sx0 = (int)(d2s[0] * dxf + d2s[1] * dyf + d2s[2]) + bias.X;
                int sy0 = (int)(d2s[3] * dxf + d2s[4] * dyf + d2s[5]) + bias.Y;
                if (!new Point(sx0, sy0).In(sr))
                {
                    continue;
                }

                if (op == Op.Src)
                {
                    dst.Set(dr.Min.X + dx, dr.Min.Y + dy, new Rgba64((ushort)pr, (ushort)pg, (ushort)pb, (ushort)pa));
                }
                else
                {
                    (uint qr, uint qg, uint qb, uint qa) = dst.At(dr.Min.X + dx, dr.Min.Y + dy).Rgba();
                    dst.Set(dr.Min.X + dx, dr.Min.Y + dy, new Rgba64(
                        (ushort)(qr * pa1 / 0xffff + pr),
                        (ushort)(qg * pa1 / 0xffff + pg),
                        (ushort)(qb * pa1 / 0xffff + pb),
                        (ushort)(qa * pa1 / 0xffff + pa)));
                }
            }
        }
    }

}
