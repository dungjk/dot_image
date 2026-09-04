// Ported from Go src/image/draw/draw.go


using DotImage.Color;
using DotImage.Util;

namespace DotImage.Draw;

/// <summary>
/// Porter-Duff compositing operator.
/// </summary>
public enum Op
{
    /// <summary>(src in mask) over dst.</summary>
    Over,
    /// <summary>src in mask.</summary>
    Src,
}

public interface IDrawer
{
    /// <summary>
    /// Draw aligns r.Min in dst with sp in src and then replaces the
    /// rectangle r in dst with the result of drawing src on dst.
    /// </summary>
    void Draw(IWritableImage dst, Rect r, IImage src, Point sp);
}

public interface IRgba64WritableImage : IWritableImage, IRgba64Image, ISetRgba64Image;

public static class DrawOps
{
    private const uint M = 0xffff;

    public static readonly IDrawer FloydSteinberg = new FloydSteinbergDrawer();

    public static void Draw(IWritableImage dst, Rect r, IImage src, Point sp, Op op) =>
        DrawMask(dst, r, src, sp, null, default, op);

    public static void DrawMask(
        IWritableImage dst, Rect r, IImage src, Point sp,
        IImage? mask, Point mp, Op op)
    {
        Clip(dst, ref r, src, ref sp, mask, ref mp);
        if (r.Empty()) return;

        if (dst is RgbaImage dst0)
        {
            if (op == Op.Over)
            {
                if (mask == null)
                {
                    switch (src)
                    {
                        case UniformImage srcUniform:
                            var (sr, sg, sb, sa) = srcUniform.Rgba();
                            if (sa == 0xffff)
                                DrawFillSrc(dst0, r, sr, sg, sb, sa);
                            else
                                DrawFillOver(dst0, r, sr, sg, sb, sa);
                            return;
                        case RgbaImage srcRgba:
                            DrawCopyOver(dst0, r, srcRgba, sp);
                            return;
                        case NrgbaImage srcNrgbaOver:
                            DrawNrgbaOver(dst0, r, srcNrgbaOver, sp);
                            return;
                        case YCbCrImage srcYcbcr:
                            if (ImageUtil.DrawYCbCr(dst0, r, srcYcbcr, sp)) return;
                            break;
                        case GrayImage srcGray:
                            DrawGray(dst0, r, srcGray, sp);
                            return;
                        case CmykImage srcCmyk:
                            DrawCmyk(dst0, r, srcCmyk, sp);
                            return;
                    }
                }
                else if (mask is AlphaImage mask0)
                {
                    switch (src)
                    {
                        case UniformImage srcUniform:
                            DrawGlyphOver(dst0, r, srcUniform, mask0, mp);
                            return;
                        case RgbaImage srcRgba:
                            DrawRgbaMaskOver(dst0, r, srcRgba, sp, mask0, mp);
                            return;
                        case GrayImage srcGray:
                            DrawGrayMaskOver(dst0, r, srcGray, sp, mask0, mp);
                            return;
                        case IRgba64Image srcRgba64:
                            DrawRgba64ImageMaskOver(dst0, r, srcRgba64, sp, mask0, mp);
                            return;
                    }
                }
            }
            else
            {
                if (mask == null)
                {
                    switch (src)
                    {
                        case UniformImage srcUniform:
                            var (sr, sg, sb, sa) = srcUniform.Rgba();
                            DrawFillSrc(dst0, r, sr, sg, sb, sa);
                            return;
                        case RgbaImage srcRgba:
                            int d0 = dst0.PixOffset(r.Min.X, r.Min.Y);
                            int s0 = srcRgba.PixOffset(sp.X, sp.Y);
                            DrawCopySrc(
                                dst0.Pix.Span[d0..], dst0.Stride, r,
                                srcRgba.Pix.Span[s0..], srcRgba.Stride, sp, 4 * r.Dx());
                            return;
                        case NrgbaImage srcNrgbaCopy:
                            DrawNrgbaSrc(dst0, r, srcNrgbaCopy, sp);
                            return;
                        case YCbCrImage srcYcbcr:
                            if (ImageUtil.DrawYCbCr(dst0, r, srcYcbcr, sp)) return;
                            break;
                        case GrayImage srcGray:
                            DrawGray(dst0, r, srcGray, sp);
                            return;
                        case CmykImage srcCmyk:
                            DrawCmyk(dst0, r, srcCmyk, sp);
                            return;
                    }
                }
            }

            DrawRgba(dst0, r, src, sp, mask, mp, op);
            return;
        }

        if (dst is PalettedImage dstPaletted && op == Op.Src && mask == null)
        {
            if (src is UniformImage srcUniform)
            {
                byte colorIndex = (byte)dstPaletted.Palette.Index(srcUniform.C);
                int i0 = dstPaletted.PixOffset(r.Min.X, r.Min.Y);
                int i1 = i0 + r.Dx();
                var pix = dstPaletted.Pix.Span;
                for (int y = r.Min.Y; y < r.Max.Y; y++)
                {
                    for (int i = i0; i < i1; i++)
                        pix[i] = colorIndex;
                    i0 += dstPaletted.Stride;
                    i1 += dstPaletted.Stride;
                }
                return;
            }

            if (!ProcessBackward(dst, r, src, sp))
            {
                DrawPaletted(dst, r, src, sp, false);
                return;
            }
        }

        if (dst is NrgbaImage dstNrgba && op == Op.Src && mask == null && src is NrgbaImage srcNrgba)
        {
            int d0 = dstNrgba.PixOffset(r.Min.X, r.Min.Y);
            int s0 = srcNrgba.PixOffset(sp.X, sp.Y);
            DrawCopySrc(
                dstNrgba.Pix.Span[d0..], dstNrgba.Stride, r,
                srcNrgba.Pix.Span[s0..], srcNrgba.Stride, sp, 4 * r.Dx());
            return;
        }

        if (dst is Nrgba64Image dstNrgba64 && op == Op.Src && mask == null && src is Nrgba64Image srcNrgba64)
        {
            int d0 = dstNrgba64.PixOffset(r.Min.X, r.Min.Y);
            int s0 = srcNrgba64.PixOffset(sp.X, sp.Y);
            DrawCopySrc(
                dstNrgba64.Pix.Span[d0..], dstNrgba64.Stride, r,
                srcNrgba64.Pix.Span[s0..], srcNrgba64.Stride, sp, 8 * r.Dx());
            return;
        }

        int x0 = r.Min.X, x1 = r.Max.X, dx = 1;
        int y0 = r.Min.Y, y1 = r.Max.Y, dy = 1;
        if (ProcessBackward(dst, r, src, sp))
        {
            (x0, x1, dx) = (x1 - 1, x0 - 1, -1);
            (y0, y1, dy) = (y1 - 1, y0 - 1, -1);
        }

        if (dst is IRgba64WritableImage dstRgba64)
        {
            if (src is IRgba64Image srcRgba64)
            {
                if (mask == null)
                {
                    int sy = sp.Y + y0 - r.Min.Y;
                    for (int y = y0; y != y1; y += dy, sy += dy)
                    {
                        int sx = sp.X + x0 - r.Min.X;
                        for (int x = x0; x != x1; x += dx, sx += dx)
                        {
                            if (op == Op.Src)
                                dstRgba64.SetRgba64(x, y, srcRgba64.Rgba64At(sx, sy));
                            else
                            {
                                var srgba = srcRgba64.Rgba64At(sx, sy);
                                uint a = M - srgba.A;
                                var drgba = dstRgba64.Rgba64At(x, y);
                                dstRgba64.SetRgba64(x, y, new Rgba64(
                                    (ushort)((drgba.R * a) / M + srgba.R),
                                    (ushort)((drgba.G * a) / M + srgba.G),
                                    (ushort)((drgba.B * a) / M + srgba.B),
                                    (ushort)((drgba.A * a) / M + srgba.A)));
                            }
                        }
                    }
                    return;
                }

                if (mask is IRgba64Image maskRgba64)
                {
                    int sy = sp.Y + y0 - r.Min.Y;
                    int my = mp.Y + y0 - r.Min.Y;
                    for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
                    {
                        int sx = sp.X + x0 - r.Min.X;
                        int mx = mp.X + x0 - r.Min.X;
                        for (int x = x0; x != x1; x += dx, sx += dx, mx += dx)
                        {
                            uint ma = maskRgba64.Rgba64At(mx, my).A;
                            switch (ma)
                            {
                                case 0:
                                    if (op != Op.Over)
                                        dstRgba64.SetRgba64(x, y, new Rgba64(0, 0, 0, 0));
                                    break;
                                case M when op == Op.Src:
                                    dstRgba64.SetRgba64(x, y, srcRgba64.Rgba64At(sx, sy));
                                    break;
                                default:
                                    var srgba = srcRgba64.Rgba64At(sx, sy);
                                    if (op == Op.Over)
                                    {
                                        var drgba = dstRgba64.Rgba64At(x, y);
                                        uint a = M - (srgba.A * ma / M);
                                        dstRgba64.SetRgba64(x, y, new Rgba64(
                                            (ushort)((drgba.R * a + srgba.R * ma) / M),
                                            (ushort)((drgba.G * a + srgba.G * ma) / M),
                                            (ushort)((drgba.B * a + srgba.B * ma) / M),
                                            (ushort)((drgba.A * a + srgba.A * ma) / M)));
                                    }
                                    else
                                    {
                                        dstRgba64.SetRgba64(x, y, new Rgba64(
                                            (ushort)(srgba.R * ma / M),
                                            (ushort)(srgba.G * ma / M),
                                            (ushort)(srgba.B * ma / M),
                                            (ushort)(srgba.A * ma / M)));
                                    }
                                    break;
                            }
                        }
                    }
                    return;
                }
            }
        }

        var outColor = new Rgba64(0, 0, 0, 0xffff);
        {
            int sy = sp.Y + y0 - r.Min.Y;
            int my = mp.Y + y0 - r.Min.Y;
            for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
            {
                int sx = sp.X + x0 - r.Min.X;
                int mx = mp.X + x0 - r.Min.X;
                for (int x = x0; x != x1; x += dx, sx += dx, mx += dx)
                {
                    uint ma = M;
                    if (mask != null)
                        (_, _, _, ma) = mask.At(mx, my).Rgba();

                    switch (ma)
                    {
                        case 0:
                            if (op != Op.Over)
                                dst.Set(x, y, Colors.Transparent);
                            break;
                        case M when op == Op.Src:
                            dst.Set(x, y, src.At(sx, sy));
                            break;
                        default:
                            var (sr, sg, sb, sa) = src.At(sx, sy).Rgba();
                            if (op == Op.Over)
                            {
                                var (dr, dg, db, da) = dst.At(x, y).Rgba();
                                uint a = M - (sa * ma / M);
                                outColor = new Rgba64(
                                    (ushort)((dr * a + sr * ma) / M),
                                    (ushort)((dg * a + sg * ma) / M),
                                    (ushort)((db * a + sb * ma) / M),
                                    (ushort)((da * a + sa * ma) / M));
                            }
                            else
                            {
                                outColor = new Rgba64(
                                    (ushort)(sr * ma / M),
                                    (ushort)(sg * ma / M),
                                    (ushort)(sb * ma / M),
                                    (ushort)(sa * ma / M));
                            }
                            dst.Set(x, y, outColor);
                            break;
                    }
                }
            }
        }
    }

    public static void Clip(
        IImage dst, ref Rect r, IImage src, ref Point sp,
        IImage? mask, ref Point mp)
    {
        Point orig = r.Min;
        r = r.Intersect(dst.Bounds());
        r = r.Intersect(src.Bounds().Add(orig.Sub(sp)));
        if (mask != null)
            r = r.Intersect(mask.Bounds().Add(orig.Sub(mp)));
        int dx = r.Min.X - orig.X;
        int dy = r.Min.Y - orig.Y;
        if (dx == 0 && dy == 0) return;
        sp = sp.Add(new Point(dx, dy));
        if (mask != null)
            mp = mp.Add(new Point(dx, dy));
    }

    internal static bool ProcessBackward(IImage dst, Rect r, IImage src, Point sp) =>
        ReferenceEquals(dst, src) &&
        r.Overlaps(r.Add(sp.Sub(r.Min))) &&
        (sp.Y < r.Min.Y || (sp.Y == r.Min.Y && sp.X < r.Min.X));

    public static uint SqDiff(int x, int y)
    {
        uint d = (uint)(x - y);
        return (d * d) >> 2;
    }

    private static int Clamp(int i)
    {
        if (i < 0) return 0;
        if (i > 0xffff) return 0xffff;
        return i;
    }

    private static void DrawFillOver(RgbaImage dst, Rect r, uint sr, uint sg, uint sb, uint sa)
    {
        uint a = (M - sa) * 0x101;
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        var pix = dst.Pix.Span;
        for (int y = r.Min.Y; y != r.Max.Y; y++)
        {
            for (int i = i0; i < i1; i += 4)
            {
                pix[i] = (byte)((pix[i] * a / M + sr) >> 8);
                pix[i + 1] = (byte)((pix[i + 1] * a / M + sg) >> 8);
                pix[i + 2] = (byte)((pix[i + 2] * a / M + sb) >> 8);
                pix[i + 3] = (byte)((pix[i + 3] * a / M + sa) >> 8);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
        }
    }

    private static void DrawFillSrc(RgbaImage dst, Rect r, uint sr, uint sg, uint sb, uint sa)
    {
        byte sr8 = (byte)(sr >> 8);
        byte sg8 = (byte)(sg >> 8);
        byte sb8 = (byte)(sb >> 8);
        byte sa8 = (byte)(sa >> 8);
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        var pix = dst.Pix.Span;
        for (int y = r.Min.Y; y < r.Max.Y; y++)
        {
            for (int i = i0; i < i1; i += 4)
            {
                pix[i] = sr8;
                pix[i + 1] = sg8;
                pix[i + 2] = sb8;
                pix[i + 3] = sa8;
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
        }
    }

    private static void DrawCopyOver(RgbaImage dst, Rect r, RgbaImage src, Point sp)
    {
        int dx = r.Dx(), dy = r.Dy();
        int d0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int s0 = src.PixOffset(sp.X, sp.Y);
        int ddelta, sdelta, i0, i1, idelta;
        if (r.Min.Y < sp.Y || (r.Min.Y == sp.Y && r.Min.X <= sp.X))
        {
            ddelta = dst.Stride;
            sdelta = src.Stride;
            (i0, i1, idelta) = (0, dx * 4, 4);
        }
        else
        {
            d0 += (dy - 1) * dst.Stride;
            s0 += (dy - 1) * src.Stride;
            ddelta = -dst.Stride;
            sdelta = -src.Stride;
            (i0, i1, idelta) = ((dx - 1) * 4, -4, -4);
        }

        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        for (; dy > 0; dy--)
        {
            var drow = dpix.Slice(d0);
            var srow = spix.Slice(s0);
            for (int i = i0; i != i1; i += idelta)
            {
                uint sr = (uint)srow[i] * 0x101;
                uint sg = (uint)srow[i + 1] * 0x101;
                uint sb = (uint)srow[i + 2] * 0x101;
                uint sa = (uint)srow[i + 3] * 0x101;
                uint a = (M - sa) * 0x101;
                drow[i] = (byte)((drow[i] * a / M + sr) >> 8);
                drow[i + 1] = (byte)((drow[i + 1] * a / M + sg) >> 8);
                drow[i + 2] = (byte)((drow[i + 2] * a / M + sb) >> 8);
                drow[i + 3] = (byte)((drow[i + 3] * a / M + sa) >> 8);
            }
            d0 += ddelta;
            s0 += sdelta;
        }
    }

    private static void DrawCopySrc(
        Span<byte> dstPix, int dstStride, Rect r,
        Span<byte> srcPix, int srcStride, Point sp, int bytesPerRow)
    {
        int d0 = 0, s0 = 0, ddelta = dstStride, sdelta = srcStride, dy = r.Dy();
        if (r.Min.Y > sp.Y)
        {
            d0 = (dy - 1) * dstStride;
            s0 = (dy - 1) * srcStride;
            ddelta = -dstStride;
            sdelta = -srcStride;
        }
        for (; dy > 0; dy--)
        {
            srcPix.Slice(s0, bytesPerRow).CopyTo(dstPix.Slice(d0, bytesPerRow));
            d0 += ddelta;
            s0 += sdelta;
        }
    }

    private static void DrawNrgbaOver(RgbaImage dst, Rect r, NrgbaImage src, Point sp)
    {
        int i0 = (r.Min.X - dst.Rect.Min.X) * 4;
        int i1 = (r.Max.X - dst.Rect.Min.X) * 4;
        int si0 = (sp.X - src.Rect.Min.X) * 4;
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        for (int y = r.Min.Y - dst.Rect.Min.Y, sy = sp.Y - src.Rect.Min.Y; y != yMax; y++, sy++)
        {
            var dpix = dst.Pix.Span.Slice(y * dst.Stride);
            var spix = src.Pix.Span.Slice(sy * src.Stride);
            for (int i = i0, si = si0; i < i1; i += 4, si += 4)
            {
                uint sa = (uint)spix[si + 3] * 0x101;
                uint sr = (uint)spix[si] * sa / 0xff;
                uint sg = (uint)spix[si + 1] * sa / 0xff;
                uint sb = (uint)spix[si + 2] * sa / 0xff;
                uint a = (M - sa) * 0x101;
                dpix[i] = (byte)((dpix[i] * a / M + sr) >> 8);
                dpix[i + 1] = (byte)((dpix[i + 1] * a / M + sg) >> 8);
                dpix[i + 2] = (byte)((dpix[i + 2] * a / M + sb) >> 8);
                dpix[i + 3] = (byte)((dpix[i + 3] * a / M + sa) >> 8);
            }
        }
    }

    private static void DrawNrgbaSrc(RgbaImage dst, Rect r, NrgbaImage src, Point sp)
    {
        int i0 = (r.Min.X - dst.Rect.Min.X) * 4;
        int i1 = (r.Max.X - dst.Rect.Min.X) * 4;
        int si0 = (sp.X - src.Rect.Min.X) * 4;
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        for (int y = r.Min.Y - dst.Rect.Min.Y, sy = sp.Y - src.Rect.Min.Y; y != yMax; y++, sy++)
        {
            var dpix = dst.Pix.Span.Slice(y * dst.Stride);
            var spix = src.Pix.Span.Slice(sy * src.Stride);
            for (int i = i0, si = si0; i < i1; i += 4, si += 4)
            {
                uint sa = (uint)spix[si + 3] * 0x101;
                uint sr = (uint)spix[si] * sa / 0xff;
                uint sg = (uint)spix[si + 1] * sa / 0xff;
                uint sb = (uint)spix[si + 2] * sa / 0xff;
                dpix[i] = (byte)(sr >> 8);
                dpix[i + 1] = (byte)(sg >> 8);
                dpix[i + 2] = (byte)(sb >> 8);
                dpix[i + 3] = (byte)(sa >> 8);
            }
        }
    }

    private static void DrawGray(RgbaImage dst, Rect r, GrayImage src, Point sp)
    {
        int i0 = (r.Min.X - dst.Rect.Min.X) * 4;
        int i1 = (r.Max.X - dst.Rect.Min.X) * 4;
        int si0 = sp.X - src.Rect.Min.X;
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        for (int y = r.Min.Y - dst.Rect.Min.Y, sy = sp.Y - src.Rect.Min.Y; y != yMax; y++, sy++)
        {
            var dpix = dst.Pix.Span.Slice(y * dst.Stride);
            var spix = src.Pix.Span.Slice(sy * src.Stride);
            for (int i = i0, si = si0; i < i1; i += 4, si++)
            {
                byte p = spix[si];
                dpix[i] = p;
                dpix[i + 1] = p;
                dpix[i + 2] = p;
                dpix[i + 3] = 255;
            }
        }
    }

    private static void DrawCmyk(RgbaImage dst, Rect r, CmykImage src, Point sp)
    {
        int i0 = (r.Min.X - dst.Rect.Min.X) * 4;
        int i1 = (r.Max.X - dst.Rect.Min.X) * 4;
        int si0 = (sp.X - src.Rect.Min.X) * 4;
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        for (int y = r.Min.Y - dst.Rect.Min.Y, sy = sp.Y - src.Rect.Min.Y; y != yMax; y++, sy++)
        {
            var dpix = dst.Pix.Span.Slice(y * dst.Stride);
            var spix = src.Pix.Span.Slice(sy * src.Stride);
            for (int i = i0, si = si0; i < i1; i += 4, si += 4)
            {
                (dpix[i], dpix[i + 1], dpix[i + 2]) =
                    YCbCrUtil.CMYKToRGB(spix[si], spix[si + 1], spix[si + 2], spix[si + 3]);
                dpix[i + 3] = 255;
            }
        }
    }

    private static void DrawGlyphOver(RgbaImage dst, Rect r, UniformImage src, AlphaImage mask, Point mp)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        int mi0 = mask.PixOffset(mp.X, mp.Y);
        var (sr, sg, sb, sa) = src.Rgba();
        var dpix = dst.Pix.Span;
        var mpix = mask.Pix.Span;
        for (int y = r.Min.Y, my = mp.Y; y != r.Max.Y; y++, my++)
        {
            for (int i = i0, mi = mi0; i < i1; i += 4, mi++)
            {
                uint ma = mpix[mi];
                if (ma == 0) continue;
                ma |= ma << 8;
                uint a = (M - (sa * ma / M)) * 0x101;
                dpix[i] = (byte)((dpix[i] * a + sr * ma) / M >> 8);
                dpix[i + 1] = (byte)((dpix[i + 1] * a + sg * ma) / M >> 8);
                dpix[i + 2] = (byte)((dpix[i + 2] * a + sb * ma) / M >> 8);
                dpix[i + 3] = (byte)((dpix[i + 3] * a + sa * ma) / M >> 8);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
            mi0 += mask.Stride;
        }
    }

    private static void DrawGrayMaskOver(
        RgbaImage dst, Rect r, GrayImage src, Point sp, AlphaImage mask, Point mp)
    {
        int x0 = r.Min.X, x1 = r.Max.X, dx = 1;
        int y0 = r.Min.Y, y1 = r.Max.Y, dy = 1;
        if (r.Overlaps(r.Add(sp.Sub(r.Min))))
        {
            if (sp.Y < r.Min.Y || (sp.Y == r.Min.Y && sp.X < r.Min.X))
            {
                (x0, x1, dx) = (x1 - 1, x0 - 1, -1);
                (y0, y1, dy) = (y1 - 1, y0 - 1, -1);
            }
        }

        int sy = sp.Y + y0 - r.Min.Y;
        int my = mp.Y + y0 - r.Min.Y;
        int sx0 = sp.X + x0 - r.Min.X;
        int mx0 = mp.X + x0 - r.Min.X;
        int sx1 = sx0 + (x1 - x0);
        int i0 = dst.PixOffset(x0, y0);
        int di = dx * 4;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        var mpix = mask.Pix.Span;
        for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
        {
            for (int i = i0, sx = sx0, mx = mx0; sx != sx1; i += di, sx += dx, mx += dx)
            {
                uint ma = mpix[mask.PixOffset(mx, my)];
                ma |= ma << 8;
                uint syv = spix[src.PixOffset(sx, sy)];
                syv |= syv << 8;
                const uint sa = 0xffff;
                uint a = (M - (sa * ma / M)) * 0x101;
                dpix[i] = (byte)((dpix[i] * a + syv * ma) / M >> 8);
                dpix[i + 1] = (byte)((dpix[i + 1] * a + syv * ma) / M >> 8);
                dpix[i + 2] = (byte)((dpix[i + 2] * a + syv * ma) / M >> 8);
                dpix[i + 3] = (byte)((dpix[i + 3] * a + sa * ma) / M >> 8);
            }
            i0 += dy * dst.Stride;
        }
    }

    private static void DrawRgbaMaskOver(
        RgbaImage dst, Rect r, RgbaImage src, Point sp, AlphaImage mask, Point mp)
    {
        int x0 = r.Min.X, x1 = r.Max.X, dx = 1;
        int y0 = r.Min.Y, y1 = r.Max.Y, dy = 1;
        if (ReferenceEquals(dst, src) && r.Overlaps(r.Add(sp.Sub(r.Min))))
        {
            if (sp.Y < r.Min.Y || (sp.Y == r.Min.Y && sp.X < r.Min.X))
            {
                (x0, x1, dx) = (x1 - 1, x0 - 1, -1);
                (y0, y1, dy) = (y1 - 1, y0 - 1, -1);
            }
        }

        int sy = sp.Y + y0 - r.Min.Y;
        int my = mp.Y + y0 - r.Min.Y;
        int sx0 = sp.X + x0 - r.Min.X;
        int mx0 = mp.X + x0 - r.Min.X;
        int sx1 = sx0 + (x1 - x0);
        int i0 = dst.PixOffset(x0, y0);
        int di = dx * 4;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        var mpix = mask.Pix.Span;
        for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
        {
            for (int i = i0, sx = sx0, mx = mx0; sx != sx1; i += di, sx += dx, mx += dx)
            {
                uint ma = mpix[mask.PixOffset(mx, my)];
                ma |= ma << 8;
                int si = src.PixOffset(sx, sy);
                uint sr = spix[si];
                uint sg = spix[si + 1];
                uint sb = spix[si + 2];
                uint sa = spix[si + 3];
                sr |= sr << 8;
                sg |= sg << 8;
                sb |= sb << 8;
                sa |= sa << 8;
                uint a = (M - (sa * ma / M)) * 0x101;
                dpix[i] = (byte)((dpix[i] * a + sr * ma) / M >> 8);
                dpix[i + 1] = (byte)((dpix[i + 1] * a + sg * ma) / M >> 8);
                dpix[i + 2] = (byte)((dpix[i + 2] * a + sb * ma) / M >> 8);
                dpix[i + 3] = (byte)((dpix[i + 3] * a + sa * ma) / M >> 8);
            }
            i0 += dy * dst.Stride;
        }
    }

    private static void DrawRgba64ImageMaskOver(
        RgbaImage dst, Rect r, IRgba64Image src, Point sp, AlphaImage mask, Point mp)
    {
        int x0 = r.Min.X, x1 = r.Max.X, dx = 1;
        int y0 = r.Min.Y, y1 = r.Max.Y, dy = 1;
        if (ReferenceEquals(dst, src) && r.Overlaps(r.Add(sp.Sub(r.Min))))
        {
            if (sp.Y < r.Min.Y || (sp.Y == r.Min.Y && sp.X < r.Min.X))
            {
                (x0, x1, dx) = (x1 - 1, x0 - 1, -1);
                (y0, y1, dy) = (y1 - 1, y0 - 1, -1);
            }
        }

        int sy = sp.Y + y0 - r.Min.Y;
        int my = mp.Y + y0 - r.Min.Y;
        int sx0 = sp.X + x0 - r.Min.X;
        int mx0 = mp.X + x0 - r.Min.X;
        int sx1 = sx0 + (x1 - x0);
        int i0 = dst.PixOffset(x0, y0);
        int di = dx * 4;
        var dpix = dst.Pix.Span;
        var mpix = mask.Pix.Span;
        for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
        {
            for (int i = i0, sx = sx0, mx = mx0; sx != sx1; i += di, sx += dx, mx += dx)
            {
                uint ma = mpix[mask.PixOffset(mx, my)];
                ma |= ma << 8;
                var srgba = src.Rgba64At(sx, sy);
                uint a = (M - (srgba.A * ma / M)) * 0x101;
                dpix[i] = (byte)((dpix[i] * a + srgba.R * ma) / M >> 8);
                dpix[i + 1] = (byte)((dpix[i + 1] * a + srgba.G * ma) / M >> 8);
                dpix[i + 2] = (byte)((dpix[i + 2] * a + srgba.B * ma) / M >> 8);
                dpix[i + 3] = (byte)((dpix[i + 3] * a + srgba.A * ma) / M >> 8);
            }
            i0 += dy * dst.Stride;
        }
    }

    private static void DrawRgba(
        RgbaImage dst, Rect r, IImage src, Point sp, IImage? mask, Point mp, Op op)
    {
        int x0 = r.Min.X, x1 = r.Max.X, dx = 1;
        int y0 = r.Min.Y, y1 = r.Max.Y, dy = 1;
        if (ReferenceEquals(dst, src) && r.Overlaps(r.Add(sp.Sub(r.Min))))
        {
            if (sp.Y < r.Min.Y || (sp.Y == r.Min.Y && sp.X < r.Min.X))
            {
                (x0, x1, dx) = (x1 - 1, x0 - 1, -1);
                (y0, y1, dy) = (y1 - 1, y0 - 1, -1);
            }
        }

        int sy = sp.Y + y0 - r.Min.Y;
        int my = mp.Y + y0 - r.Min.Y;
        int sx0 = sp.X + x0 - r.Min.X;
        int mx0 = mp.X + x0 - r.Min.X;
        int sx1 = sx0 + (x1 - x0);
        int i0 = dst.PixOffset(x0, y0);
        int di = dx * 4;
        var dpix = dst.Pix.Span;

        if (src is IRgba64Image srcRgba64)
        {
            if (mask == null)
            {
                if (op == Op.Over)
                {
                    for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
                    {
                        for (int i = i0, sx = sx0, mx = mx0; sx != sx1; i += di, sx += dx, mx += dx)
                        {
                            var srgba = srcRgba64.Rgba64At(sx, sy);
                            uint a = (M - srgba.A) * 0x101;
                            dpix[i] = (byte)((dpix[i] * a / M + srgba.R) >> 8);
                            dpix[i + 1] = (byte)((dpix[i + 1] * a / M + srgba.G) >> 8);
                            dpix[i + 2] = (byte)((dpix[i + 2] * a / M + srgba.B) >> 8);
                            dpix[i + 3] = (byte)((dpix[i + 3] * a / M + srgba.A) >> 8);
                        }
                        i0 += dy * dst.Stride;
                    }
                }
                else
                {
                    for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
                    {
                        for (int i = i0, sx = sx0, mx = mx0; sx != sx1; i += di, sx += dx, mx += dx)
                        {
                            var srgba = srcRgba64.Rgba64At(sx, sy);
                            dpix[i] = (byte)(srgba.R >> 8);
                            dpix[i + 1] = (byte)(srgba.G >> 8);
                            dpix[i + 2] = (byte)(srgba.B >> 8);
                            dpix[i + 3] = (byte)(srgba.A >> 8);
                        }
                        i0 += dy * dst.Stride;
                    }
                }
                return;
            }

            if (mask is IRgba64Image maskRgba64)
            {
                if (op == Op.Over)
                {
                    for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
                    {
                        for (int i = i0, sx = sx0, mx = mx0; sx != sx1; i += di, sx += dx, mx += dx)
                        {
                            uint ma = maskRgba64.Rgba64At(mx, my).A;
                            var srgba = srcRgba64.Rgba64At(sx, sy);
                            uint a = (M - (srgba.A * ma / M)) * 0x101;
                            dpix[i] = (byte)((dpix[i] * a + srgba.R * ma) / M >> 8);
                            dpix[i + 1] = (byte)((dpix[i + 1] * a + srgba.G * ma) / M >> 8);
                            dpix[i + 2] = (byte)((dpix[i + 2] * a + srgba.B * ma) / M >> 8);
                            dpix[i + 3] = (byte)((dpix[i + 3] * a + srgba.A * ma) / M >> 8);
                        }
                        i0 += dy * dst.Stride;
                    }
                }
                else
                {
                    for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
                    {
                        for (int i = i0, sx = sx0, mx = mx0; sx != sx1; i += di, sx += dx, mx += dx)
                        {
                            uint ma = maskRgba64.Rgba64At(mx, my).A;
                            var srgba = srcRgba64.Rgba64At(sx, sy);
                            dpix[i] = (byte)(srgba.R * ma / M >> 8);
                            dpix[i + 1] = (byte)(srgba.G * ma / M >> 8);
                            dpix[i + 2] = (byte)(srgba.B * ma / M >> 8);
                            dpix[i + 3] = (byte)(srgba.A * ma / M >> 8);
                        }
                        i0 += dy * dst.Stride;
                    }
                }
                return;
            }
        }

        for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
        {
            for (int i = i0, sx = sx0, mx = mx0; sx != sx1; i += di, sx += dx, mx += dx)
            {
                uint ma = M;
                if (mask != null)
                    (_, _, _, ma) = mask.At(mx, my).Rgba();
                var (sr, sg, sb, sa) = src.At(sx, sy).Rgba();
                if (op == Op.Over)
                {
                    uint a = (M - (sa * ma / M)) * 0x101;
                    dpix[i] = (byte)((dpix[i] * a + sr * ma) / M >> 8);
                    dpix[i + 1] = (byte)((dpix[i + 1] * a + sg * ma) / M >> 8);
                    dpix[i + 2] = (byte)((dpix[i + 2] * a + sb * ma) / M >> 8);
                    dpix[i + 3] = (byte)((dpix[i + 3] * a + sa * ma) / M >> 8);
                }
                else
                {
                    dpix[i] = (byte)(sr * ma / M >> 8);
                    dpix[i + 1] = (byte)(sg * ma / M >> 8);
                    dpix[i + 2] = (byte)(sb * ma / M >> 8);
                    dpix[i + 3] = (byte)(sa * ma / M >> 8);
                }
            }
            i0 += dy * dst.Stride;
        }
    }

    internal static void DrawPaletted(IWritableImage dst, Rect r, IImage src, Point sp, bool floydSteinberg)
    {
        int[][]? palette = null;
        Span<byte> pix = default;
        int stride = 0;
        if (dst is PalettedImage p)
        {
            palette = new int[p.Palette.Length][];
            for (int i = 0; i < p.Palette.Length; i++)
            {
                var (pr, pg, pb, pa) = p.Palette[i].Rgba();
                palette[i] = [(int)pr, (int)pg, (int)pb, (int)pa];
            }
            int offset = p.PixOffset(r.Min.X, r.Min.Y);
            pix = p.Pix.Span[offset..];
            stride = p.Stride;
        }

        int[][]? quantErrorCurr = null;
        int[][]? quantErrorNext = null;
        if (floydSteinberg)
        {
            quantErrorCurr = new int[r.Dx() + 2][];
            quantErrorNext = new int[r.Dx() + 2][];
            for (int i = 0; i < quantErrorCurr.Length; i++)
            {
                quantErrorCurr[i] = new int[4];
                quantErrorNext[i] = new int[4];
            }
        }

        Func<int, int, (uint R, uint G, uint B, uint A)> pxRgba = (x, y) => src.At(x, y).Rgba();
        switch (src)
        {
            case RgbaImage rgba:
                pxRgba = (x, y) => ((IColor)rgba.RgbaAt(x, y)).Rgba();
                break;
            case NrgbaImage nrgba:
                pxRgba = (x, y) => nrgba.NrgbaAt(x, y).Rgba();
                break;
            case YCbCrImage ycbcr:
                pxRgba = (x, y) => ycbcr.YCbCrAt(x, y).Rgba();
                break;
        }

        var outColor = new Rgba64(0, 0, 0, 0xffff);
        for (int y = 0; y != r.Dy(); y++)
        {
            for (int x = 0; x != r.Dx(); x++)
            {
                var (sr, sg, sb, sa) = pxRgba(sp.X + x, sp.Y + y);
                int er = (int)sr, eg = (int)sg, eb = (int)sb, ea = (int)sa;
                if (floydSteinberg)
                {
                    er = Clamp(er + quantErrorCurr![x + 1][0] / 16);
                    eg = Clamp(eg + quantErrorCurr[x + 1][1] / 16);
                    eb = Clamp(eb + quantErrorCurr[x + 1][2] / 16);
                    ea = Clamp(ea + quantErrorCurr[x + 1][3] / 16);
                }

                if (palette != null)
                {
                    int bestIndex = 0;
                    uint bestSum = uint.MaxValue;
                    for (int index = 0; index < palette.Length; index++)
                    {
                        uint sum = SqDiff(er, palette[index][0]) +
                                   SqDiff(eg, palette[index][1]) +
                                   SqDiff(eb, palette[index][2]) +
                                   SqDiff(ea, palette[index][3]);
                        if (sum < bestSum)
                        {
                            bestIndex = index;
                            bestSum = sum;
                            if (sum == 0) break;
                        }
                    }
                    pix[y * stride + x] = (byte)bestIndex;

                    if (!floydSteinberg) continue;

                    er -= palette[bestIndex][0];
                    eg -= palette[bestIndex][1];
                    eb -= palette[bestIndex][2];
                    ea -= palette[bestIndex][3];
                }
                else
                {
                    outColor = new Rgba64((ushort)er, (ushort)eg, (ushort)eb, (ushort)ea);
                    dst.Set(r.Min.X + x, r.Min.Y + y, outColor);
                    if (!floydSteinberg) continue;
                    (sr, sg, sb, sa) = dst.At(r.Min.X + x, r.Min.Y + y).Rgba();
                    er -= (int)sr;
                    eg -= (int)sg;
                    eb -= (int)sb;
                    ea -= (int)sa;
                }

                quantErrorNext![x + 0][0] += er * 3;
                quantErrorNext[x + 0][1] += eg * 3;
                quantErrorNext[x + 0][2] += eb * 3;
                quantErrorNext[x + 0][3] += ea * 3;
                quantErrorNext[x + 1][0] += er * 5;
                quantErrorNext[x + 1][1] += eg * 5;
                quantErrorNext[x + 1][2] += eb * 5;
                quantErrorNext[x + 1][3] += ea * 5;
                quantErrorNext[x + 2][0] += er;
                quantErrorNext[x + 2][1] += eg;
                quantErrorNext[x + 2][2] += eb;
                quantErrorNext[x + 2][3] += ea;
                quantErrorCurr![x + 2][0] += er * 7;
                quantErrorCurr[x + 2][1] += eg * 7;
                quantErrorCurr[x + 2][2] += eb * 7;
                quantErrorCurr[x + 2][3] += ea * 7;
            }

            if (floydSteinberg)
            {
                (quantErrorCurr, quantErrorNext) = (quantErrorNext, quantErrorCurr);
                for (int i = 0; i < quantErrorNext!.Length; i++)
                    Array.Clear(quantErrorNext[i]);
            }
        }
    }
}

file sealed class FloydSteinbergDrawer : IDrawer
{
    public void Draw(IWritableImage dst, Rect r, IImage src, Point sp)
    {
        Point mp = default;
        DrawOps.Clip(dst, ref r, src, ref sp, null, ref mp);
        if (r.Empty()) return;
        DrawOps.DrawPaletted(dst, r, src, sp, true);
    }
}

public static class OpExtensions
{
    public static void Draw(this Op op, IWritableImage dst, Rect r, IImage src, Point sp) =>
        DrawOps.DrawMask(dst, r, src, sp, null, default, op);
}
