// Ported from golang.org/x/image/vector/vector.go
// A 2-D vector graphics rasterizer.

using DotImage;
using DotImage.Color;
using DotImage.Draw;

namespace DotImage.Extended.Vector;

public class Rasterizer
{
    // floatingPointMathThreshold is the width or height above which the rasterizer
    // chooses to use floating point math instead of fixed point math.
    internal const int FloatingPointMathThreshold = 512;

    internal float[] bufF32 = [];
    internal uint[] bufU32 = [];

    internal bool useFloatingPointMath;

    internal Point size;
    internal float firstX;
    internal float firstY;
    internal float penX;
    internal float penY;

    /// <summary>
    /// DrawOp is the operator used for the Draw method.
    /// The zero value is Op.Over.
    /// </summary>
    public Op DrawOp;

    public Rasterizer()
    {
        DrawOp = Op.Over;
    }

    public Rasterizer(int w, int h) : this()
    {
        Reset(w, h);
    }

    /// <summary>
    /// NewRasterizer returns a new Rasterizer whose rendered mask image is bounded
    /// by the given width and height.
    /// </summary>
    public static Rasterizer NewRasterizer(int w, int h)
    {
        return new Rasterizer(w, h);
    }

    /// <summary>
    /// Reset resets a Rasterizer as if it was just returned by NewRasterizer.
    /// This includes setting DrawOp to Op.Over.
    /// </summary>
    public void Reset(int w, int h)
    {
        size = new Point(w, h);
        firstX = 0;
        firstY = 0;
        penX = 0;
        penY = 0;
        DrawOp = Op.Over;

        SetUseFloatingPointMath(w > FloatingPointMathThreshold || h > FloatingPointMathThreshold);
    }

    private void SetUseFloatingPointMath(bool b)
    {
        useFloatingPointMath = b;

        if (useFloatingPointMath)
        {
            int n = size.X * size.Y;
            if (n > bufF32.Length)
            {
                bufF32 = new float[n];
            }
            else
            {
                bufF32.AsSpan(0, n).Clear();
            }
        }
        else
        {
            int n = size.X * size.Y;
            if (n > bufU32.Length)
            {
                bufU32 = new uint[n];
            }
            else
            {
                bufU32.AsSpan(0, n).Clear();
            }
        }
    }

    /// <summary>
    /// Size returns the width and height passed to NewRasterizer or Reset.
    /// </summary>
    public Point Size() => size;

    /// <summary>
    /// Bounds returns the rectangle from (0, 0) to the width and height passed to
    /// NewRasterizer or Reset.
    /// </summary>
    public Rect Bounds() => new Rect(default, size);

    /// <summary>
    /// Pen returns the location of the path-drawing pen: the last argument to the
    /// most recent XxxTo call.
    /// </summary>
    public (float X, float Y) Pen() => (penX, penY);

    /// <summary>
    /// ClosePath closes the current path.
    /// </summary>
    public void ClosePath()
    {
        LineTo(firstX, firstY);
    }

    /// <summary>
    /// MoveTo starts a new path and moves the pen to (ax, ay).
    /// The coordinates are allowed to be out of the Rasterizer's bounds.
    /// </summary>
    public void MoveTo(float ax, float ay)
    {
        firstX = ax;
        firstY = ay;
        penX = ax;
        penY = ay;
    }

    /// <summary>
    /// LineTo adds a line segment, from the pen to (bx, by), and moves the pen to
    /// (bx, by).
    /// The coordinates are allowed to be out of the Rasterizer's bounds.
    /// </summary>
    public void LineTo(float bx, float by)
    {
        if (useFloatingPointMath)
        {
            RasterFloating.FloatingLineTo(this, bx, by);
        }
        else
        {
            RasterFixed.FixedLineTo(this, bx, by);
        }
    }

    /// <summary>
    /// QuadTo adds a quadratic Bezier segment, from the pen via (bx, by) to (cx,
    /// cy), and moves the pen to (cx, cy).
    /// The coordinates are allowed to be out of the Rasterizer's bounds.
    /// </summary>
    public void QuadTo(float bx, float by, float cx, float cy)
    {
        float ax = penX, ay = penY;
        float devsq = DevSquared(ax, ay, bx, by, cx, cy);
        if (devsq >= 0.333f)
        {
            const double tol = 3;
            int n = 1 + (int)System.Math.Sqrt(System.Math.Sqrt(tol * devsq));
            float t = 0, nInv = 1f / n;
            for (int i = 0; i < n - 1; i++)
            {
                t += nInv;
                float abx, aby;
                abx = Lerp(t, ax, bx);
                aby = Lerp(t, ay, by);
                float bcx, bcy;
                bcx = Lerp(t, bx, cx);
                bcy = Lerp(t, by, cy);
                LineTo(Lerp(t, abx, bcx), Lerp(t, aby, bcy));
            }
        }
        LineTo(cx, cy);
    }

    /// <summary>
    /// CubeTo adds a cubic Bezier segment, from the pen via (bx, by) and (cx, cy)
    /// to (dx, dy), and moves the pen to (dx, dy).
    /// The coordinates are allowed to be out of the Rasterizer's bounds.
    /// </summary>
    public void CubeTo(float bx, float by, float cx, float cy, float dx, float dy)
    {
        float ax = penX, ay = penY;
        float devsq = DevSquared(ax, ay, bx, by, dx, dy);
        float devsqAlt = DevSquared(ax, ay, cx, cy, dx, dy);
        if (devsq < devsqAlt)
        {
            devsq = devsqAlt;
        }
        if (devsq >= 0.333f)
        {
            const double tol = 3;
            int n = 1 + (int)System.Math.Sqrt(System.Math.Sqrt(tol * devsq));
            float t = 0, nInv = 1f / n;
            for (int i = 0; i < n - 1; i++)
            {
                t += nInv;
                float abx = Lerp(t, ax, bx);
                float aby = Lerp(t, ay, by);
                float bcx = Lerp(t, bx, cx);
                float bcy = Lerp(t, by, cy);
                float cdx = Lerp(t, cx, dx);
                float cdy = Lerp(t, cy, dy);
                float abcx = Lerp(t, abx, bcx);
                float abcy = Lerp(t, aby, bcy);
                float bcdx = Lerp(t, bcx, cdx);
                float bcdy = Lerp(t, bcy, cdy);
                LineTo(Lerp(t, abcx, bcdx), Lerp(t, abcy, bcdy));
            }
        }
        LineTo(dx, dy);
    }

    /// <summary>
    /// Draw implements the Drawer interface from the standard library's image/draw
    /// package. The vector paths previously added via the XxxTo calls become the
    /// mask for drawing src onto dst.
    /// </summary>
    public void Draw(IWritableImage dst, Rect r, IImage src, Point sp)
    {
        if (src is UniformImage u)
        {
            var (srcR, srcG, srcB, srcA) = u.Rgba();
            switch (dst)
            {
                case AlphaImage alphaDst:
                    // Fast path for glyph rendering.
                    if (srcA == 0xffff)
                    {
                        if (DrawOp == Op.Over)
                        {
                            RasterizeDstAlphaSrcOpaqueOpOver(alphaDst, r);
                        }
                        else
                        {
                            RasterizeDstAlphaSrcOpaqueOpSrc(alphaDst, r);
                        }
                        return;
                    }
                    break;
                case RgbaImage rgbaDst:
                    if (DrawOp == Op.Over)
                    {
                        RasterizeDstRGBASrcUniformOpOver(rgbaDst, r, srcR, srcG, srcB, srcA);
                    }
                    else
                    {
                        RasterizeDstRGBASrcUniformOpSrc(rgbaDst, r, srcR, srcG, srcB, srcA);
                    }
                    return;
            }
        }

        if (DrawOp == Op.Over)
        {
            RasterizeOpOver(dst, r, src, sp);
        }
        else
        {
            RasterizeOpSrc(dst, r, src, sp);
        }
    }

    private void AccumulateMask()
    {
        int n = size.X * size.Y;
        if (useFloatingPointMath)
        {
            if (n > bufU32.Length)
            {
                bufU32 = new uint[n];
            }
            RasterFloating.FloatingAccumulateMask(bufU32.AsSpan(0, n), bufF32.AsSpan(0, n));
        }
        else
        {
            RasterFixed.FixedAccumulateMask(bufU32.AsSpan(0, n));
        }
    }

    private void RasterizeDstAlphaSrcOpaqueOpOver(AlphaImage dst, Rect r)
    {
        if (r.Eq(dst.Bounds()) && r.Eq(Bounds()))
        {
            int n = size.X * size.Y;
            if (useFloatingPointMath)
            {
                RasterFloating.FloatingAccumulateOpOver(dst.Pix.Span, bufF32.AsSpan(0, n));
            }
            else
            {
                RasterFixed.FixedAccumulateOpOver(dst.Pix.Span, bufU32.AsSpan(0, n));
            }
            return;
        }

        AccumulateMask();
        Span<byte> pix = dst.Pix.Span;
        int dstStride = dst.Stride;
        int yMax = r.Max.Y - r.Min.Y;
        int xMax = r.Max.X - r.Min.X;
        int baseOffset = dst.PixOffset(r.Min.X, r.Min.Y);
        for (int y = 0; y < yMax; y++)
        {
            int rowOff = baseOffset + y * dstStride;
            for (int x = 0; x < xMax; x++)
            {
                uint ma = bufU32[y * size.X + x];
                int i = rowOff + x;

                // This formula is like rasterizeOpOver's, simplified for the
                // concrete dst type and opaque src assumption.
                uint a = 0xffff - ma;
                pix[i] = (byte)((uint)(pix[i] * 0x101 * a / 0xffff + ma) >> 8);
            }
        }
    }

    private void RasterizeDstAlphaSrcOpaqueOpSrc(AlphaImage dst, Rect r)
    {
        if (r.Eq(dst.Bounds()) && r.Eq(Bounds()))
        {
            int n = size.X * size.Y;
            if (useFloatingPointMath)
            {
                RasterFloating.FloatingAccumulateOpSrc(dst.Pix.Span, bufF32.AsSpan(0, n));
            }
            else
            {
                RasterFixed.FixedAccumulateOpSrc(dst.Pix.Span, bufU32.AsSpan(0, n));
            }
            return;
        }

        AccumulateMask();
        Span<byte> pix = dst.Pix.Span;
        int dstStride = dst.Stride;
        int yMax = r.Max.Y - r.Min.Y;
        int xMax = r.Max.X - r.Min.X;
        int baseOffset = dst.PixOffset(r.Min.X, r.Min.Y);
        for (int y = 0; y < yMax; y++)
        {
            int rowOff = baseOffset + y * dstStride;
            for (int x = 0; x < xMax; x++)
            {
                uint ma = bufU32[y * size.X + x];

                // This formula is like rasterizeOpSrc's, simplified for the
                // concrete dst type and opaque src assumption.
                pix[rowOff + x] = (byte)(ma >> 8);
            }
        }
    }

    private void RasterizeDstRGBASrcUniformOpOver(RgbaImage dst, Rect r, uint sr, uint sg, uint sb, uint sa)
    {
        AccumulateMask();
        Span<byte> pix = dst.Pix.Span;
        int dstStride = dst.Stride;
        int yMax = r.Max.Y - r.Min.Y;
        int xMax = r.Max.X - r.Min.X;
        int baseOffset = dst.PixOffset(r.Min.X, r.Min.Y);
        for (int y = 0; y < yMax; y++)
        {
            int rowOff = baseOffset + y * dstStride;
            for (int x = 0; x < xMax; x++)
            {
                uint ma = bufU32[y * size.X + x];

                // This formula is like rasterizeOpOver's, simplified for the
                // concrete dst type and uniform src assumption.
                uint a = 0xffff - (sa * ma / 0xffff);
                int i = rowOff + 4 * x;
                pix[i + 0] = (byte)(((uint)pix[i + 0] * 0x101 * a + sr * ma) / 0xffff >> 8);
                pix[i + 1] = (byte)(((uint)pix[i + 1] * 0x101 * a + sg * ma) / 0xffff >> 8);
                pix[i + 2] = (byte)(((uint)pix[i + 2] * 0x101 * a + sb * ma) / 0xffff >> 8);
                pix[i + 3] = (byte)(((uint)pix[i + 3] * 0x101 * a + sa * ma) / 0xffff >> 8);
            }
        }
    }

    private void RasterizeDstRGBASrcUniformOpSrc(RgbaImage dst, Rect r, uint sr, uint sg, uint sb, uint sa)
    {
        AccumulateMask();
        Span<byte> pix = dst.Pix.Span;
        int dstStride = dst.Stride;
        int yMax = r.Max.Y - r.Min.Y;
        int xMax = r.Max.X - r.Min.X;
        int baseOffset = dst.PixOffset(r.Min.X, r.Min.Y);
        for (int y = 0; y < yMax; y++)
        {
            int rowOff = baseOffset + y * dstStride;
            for (int x = 0; x < xMax; x++)
            {
                uint ma = bufU32[y * size.X + x];

                // This formula is like rasterizeOpSrc's, simplified for the
                // concrete dst type and uniform src assumption.
                int i = rowOff + 4 * x;
                pix[i + 0] = (byte)((sr * ma / 0xffff) >> 8);
                pix[i + 1] = (byte)((sg * ma / 0xffff) >> 8);
                pix[i + 2] = (byte)((sb * ma / 0xffff) >> 8);
                pix[i + 3] = (byte)((sa * ma / 0xffff) >> 8);
            }
        }
    }

    private void RasterizeOpOver(IWritableImage dst, Rect r, IImage src, Point sp)
    {
        AccumulateMask();
        var outColor = new Rgba64(0, 0, 0, 0);
        int yMax = r.Max.Y - r.Min.Y;
        int xMax = r.Max.X - r.Min.X;
        for (int y = 0; y < yMax; y++)
        {
            for (int x = 0; x < xMax; x++)
            {
                var (sr, sg, sb, sa) = src.At(sp.X + x, sp.Y + y).Rgba();
                uint ma = bufU32[y * size.X + x];

                // This algorithm comes from the standard library's image/draw package.
                var (dr, dg, db, da) = dst.At(r.Min.X + x, r.Min.Y + y).Rgba();
                uint a = 0xffff - (sa * ma / 0xffff);
                outColor = new Rgba64(
                    (ushort)((dr * a + sr * ma) / 0xffff),
                    (ushort)((dg * a + sg * ma) / 0xffff),
                    (ushort)((db * a + sb * ma) / 0xffff),
                    (ushort)((da * a + sa * ma) / 0xffff));

                dst.Set(r.Min.X + x, r.Min.Y + y, outColor);
            }
        }
    }

    private void RasterizeOpSrc(IWritableImage dst, Rect r, IImage src, Point sp)
    {
        AccumulateMask();
        var outColor = new Rgba64(0, 0, 0, 0);
        int yMax = r.Max.Y - r.Min.Y;
        int xMax = r.Max.X - r.Min.X;
        for (int y = 0; y < yMax; y++)
        {
            for (int x = 0; x < xMax; x++)
            {
                var (sr, sg, sb, sa) = src.At(sp.X + x, sp.Y + y).Rgba();
                uint ma = bufU32[y * size.X + x];

                // This algorithm comes from the standard library's image/draw package.
                outColor = new Rgba64(
                    (ushort)(sr * ma / 0xffff),
                    (ushort)(sg * ma / 0xffff),
                    (ushort)(sb * ma / 0xffff),
                    (ushort)(sa * ma / 0xffff));

                dst.Set(r.Min.X + x, r.Min.Y + y, outColor);
            }
        }
    }

    private static float Lerp(float t, float px, float py) => px + t * (py - px);

    private static float DevSquared(float ax, float ay, float bx, float by, float cx, float cy)
    {
        float devx = ax - 2 * bx + cx;
        float devy = ay - 2 * by + cy;
        return devx * devx + devy * devy;
    }
}
