// Ported from Go src/image/draw/draw.go


using System.Runtime.CompilerServices;
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

        if (DrawMaskToRgba(dst, r, src, sp, mask, mp, op))
            return;

        if (DrawMaskToNrgba(dst, r, src, sp, mask, mp, op))
            return;

        if (DrawMaskToPaletted(dst, r, src, sp, mask, op))
            return;

        if (DrawMaskNrgba64Copy(dst, r, src, sp, mask, op))
            return;

        var (x0, x1, dx, y0, y1, dy) = GetDrawIterationBounds(dst, r, src, sp);

        if (DrawMaskToRgba64(dst, r, src, sp, mask, mp, op, x0, x1, dx, y0, y1, dy))
            return;

        if (DrawMaskGrayCopy(dst, r, src, sp, mask, op))
            return;

        if (DrawMaskAlphaCopy(dst, r, src, sp, mask, op))
            return;

        if (DrawMaskNrgbaFallback(dst, r, src, sp, mask, mp, op))
            return;

        DrawGeneric(dst, r, src, sp, mask, mp, op, x0, x1, dx, y0, y1, dy);
    }

    private static bool DrawMaskToRgba(
        IWritableImage dst, Rect r, IImage src, Point sp,
        IImage? mask, Point mp, Op op)
    {
        if (dst is not RgbaImage dst0)
            return false;

        if (op == Op.Over)
        {
            if (mask == null && TryDrawRgbaOverNoMask(dst0, r, src, sp))
                return true;
            if (mask is AlphaImage mask0 && TryDrawRgbaOverAlphaMask(dst0, r, src, sp, mask0, mp))
                return true;
        }
        else
        {
            if (mask == null && TryDrawRgbaSrcNoMask(dst0, r, src, sp))
                return true;
            if (mask is AlphaImage mask0 && TryDrawRgbaSrcAlphaMask(dst0, r, src, sp, mask0, mp))
                return true;
        }

        DrawRgba(dst0, r, src, sp, mask, mp, op);
        return true;
    }

    private static bool TryDrawRgbaOverNoMask(RgbaImage dst, Rect r, IImage src, Point sp)
    {
        switch (src)
        {
            case UniformImage srcUniform:
                var (sr, sg, sb, sa) = srcUniform.GetRgba();
                if (sa == 0xffff)
                    DrawFillSrc(dst, r, sr, sg, sb, sa);
                else
                    DrawFillOver(dst, r, sr, sg, sb, sa);
                return true;
            case RgbaImage srcRgba:
                DrawCopyOver(dst, r, srcRgba, sp);
                return true;
            case NrgbaImage srcNrgbaOver:
                DrawNrgbaOver(dst, r, srcNrgbaOver, sp);
                return true;
            case YCbCrImage srcYcbcr:
                return ImageUtil.DrawYCbCr(dst, r, srcYcbcr, sp);
            case GrayImage srcGray:
                DrawGray(dst, r, srcGray, sp);
                return true;
            case CmykImage srcCmyk:
                DrawCmyk(dst, r, srcCmyk, sp);
                return true;
            default:
                return false;
        }
    }

    private static bool TryDrawRgbaOverAlphaMask(
        RgbaImage dst, Rect r, IImage src, Point sp, AlphaImage mask, Point mp)
    {
        switch (src)
        {
            case UniformImage srcUniform:
                DrawGlyphOver(dst, r, srcUniform, mask, mp);
                return true;
            case RgbaImage srcRgba:
                DrawRgbaMaskOver(dst, r, srcRgba, sp, mask, mp);
                return true;
            case NrgbaImage srcNrgba:
                DrawNrgbaMaskOver(dst, r, srcNrgba, sp, mask, mp);
                return true;
            case GrayImage srcGray:
                DrawGrayMaskOver(dst, r, srcGray, sp, mask, mp);
                return true;
            case IRgba64Image srcRgba64:
                DrawRgba64ImageMaskOver(dst, r, srcRgba64, sp, mask, mp);
                return true;
            default:
                return false;
        }
    }

    private static bool TryDrawRgbaSrcNoMask(RgbaImage dst, Rect r, IImage src, Point sp)
    {
        switch (src)
        {
            case UniformImage srcUniform:
                var (sr, sg, sb, sa) = srcUniform.GetRgba();
                DrawFillSrc(dst, r, sr, sg, sb, sa);
                return true;
            case RgbaImage srcRgba:
                int d0 = dst.PixOffset(r.Min.X, r.Min.Y);
                int s0 = srcRgba.PixOffset(sp.X, sp.Y);
                DrawCopySrc(
                    dst.Pix.Span[d0..], dst.Stride, r,
                    srcRgba.Pix.Span[s0..], srcRgba.Stride, sp, 4 * r.Dx());
                return true;
            case NrgbaImage srcNrgbaCopy:
                DrawNrgbaSrc(dst, r, srcNrgbaCopy, sp);
                return true;
            case YCbCrImage srcYcbcr:
                return ImageUtil.DrawYCbCr(dst, r, srcYcbcr, sp);
            case GrayImage srcGray:
                DrawGray(dst, r, srcGray, sp);
                return true;
            case CmykImage srcCmyk:
                DrawCmyk(dst, r, srcCmyk, sp);
                return true;
            default:
                return false;
        }
    }

    private static bool TryDrawRgbaSrcAlphaMask(
        RgbaImage dst, Rect r, IImage src, Point sp, AlphaImage mask, Point mp)
    {
        switch (src)
        {
            case UniformImage srcUniform:
                var (sr, sg, sb, sa) = srcUniform.GetRgba();
                DrawGlyphMaskSrc(dst, r, sr, sg, sb, sa, mask, mp);
                return true;
            case RgbaImage srcRgba:
                DrawRgbaMaskSrc(dst, r, srcRgba, sp, mask, mp);
                return true;
            case NrgbaImage srcNrgba:
                DrawNrgbaMaskSrc(dst, r, srcNrgba, sp, mask, mp);
                return true;
            case GrayImage srcGray:
                DrawGrayMaskSrc(dst, r, srcGray, sp, mask, mp);
                return true;
            case IRgba64Image srcRgba64:
                DrawRgba64MaskSrc(dst, r, srcRgba64, sp, mask, mp);
                return true;
            default:
                return false;
        }
    }

    private static bool DrawMaskToNrgba(
        IWritableImage dst, Rect r, IImage src, Point sp,
        IImage? mask, Point mp, Op op)
    {
        if (dst is not NrgbaImage dstNrgba)
            return false;

        if (op == Op.Over)
        {
            if (mask == null && TryDrawNrgbaOverNoMask(dstNrgba, r, src, sp))
                return true;
            if (mask is AlphaImage mask0 && TryDrawNrgbaOverAlphaMask(dstNrgba, r, src, sp, mask0, mp))
                return true;
        }
        else
        {
            if (mask == null && TryDrawNrgbaSrcNoMask(dstNrgba, r, src, sp))
                return true;
            if (mask is AlphaImage mask0 && TryDrawNrgbaSrcAlphaMask(dstNrgba, r, src, sp, mask0, mp))
                return true;
        }

        return false;
    }

    private static bool TryDrawNrgbaOverNoMask(NrgbaImage dst, Rect r, IImage src, Point sp)
    {
        switch (src)
        {
            case UniformImage srcUniform:
                var (sr, sg, sb, sa) = srcUniform.GetRgba();
                DrawNrgbaFillOver(dst, r, sr, sg, sb, sa);
                return true;
            case NrgbaImage srcNrgba:
                DrawNrgbaOverNrgbaDst(dst, r, srcNrgba, sp);
                return true;
            case RgbaImage srcRgba:
                DrawRgbaOverNrgbaDst(dst, r, srcRgba, sp);
                return true;
            case GrayImage srcGray:
                DrawGrayOverNrgbaDst(dst, r, srcGray, sp);
                return true;
            default:
                return false;
        }
    }

    private static bool TryDrawNrgbaOverAlphaMask(
        NrgbaImage dst, Rect r, IImage src, Point sp, AlphaImage mask, Point mp)
    {
        if (src is NrgbaImage srcNrgba)
        {
            DrawNrgbaMaskOverNrgbaDst(dst, r, srcNrgba, sp, mask, mp);
            return true;
        }
        return false;
    }

    private static bool TryDrawNrgbaSrcNoMask(NrgbaImage dst, Rect r, IImage src, Point sp)
    {
        switch (src)
        {
            case UniformImage srcUniform:
                var (sr, sg, sb, sa) = srcUniform.GetRgba();
                DrawNrgbaFillSrc(dst, r, sr, sg, sb, sa);
                return true;
            case NrgbaImage srcNrgba:
                int d0 = dst.PixOffset(r.Min.X, r.Min.Y);
                int s0 = srcNrgba.PixOffset(sp.X, sp.Y);
                DrawCopySrc(
                    dst.Pix.Span[d0..], dst.Stride, r,
                    srcNrgba.Pix.Span[s0..], srcNrgba.Stride, sp, 4 * r.Dx());
                return true;
            case RgbaImage srcRgba:
                DrawRgbaSrcNrgbaDst(dst, r, srcRgba, sp);
                return true;
            case GrayImage srcGray:
                DrawGraySrcNrgbaDst(dst, r, srcGray, sp);
                return true;
            default:
                return false;
        }
    }

    private static bool TryDrawNrgbaSrcAlphaMask(
        NrgbaImage dst, Rect r, IImage src, Point sp, AlphaImage mask, Point mp)
    {
        if (src is NrgbaImage srcNrgba)
        {
            DrawNrgbaMaskSrcNrgbaDst(dst, r, srcNrgba, sp, mask, mp);
            return true;
        }
        return false;
    }

    private static bool DrawMaskToPaletted(
        IWritableImage dst, Rect r, IImage src, Point sp, IImage? mask, Op op)
    {
        if (dst is not PalettedImage dstPaletted || op != Op.Src || mask != null)
            return false;

        if (src is UniformImage srcUniform)
        {
            DrawPalettedUniformFill(dstPaletted, r, srcUniform);
            return true;
        }

        if (!ProcessBackward(dst, r, src, sp))
        {
            DrawPaletted(dst, r, src, sp, false);
            return true;
        }

        return false;
    }

    private static void DrawPalettedUniformFill(PalettedImage dst, Rect r, UniformImage src)
    {
        byte colorIndex = (byte)dst.Palette.Index(src.C);
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx();
        var pix = dst.Pix.Span;
        for (int y = r.Min.Y; y < r.Max.Y; y++)
        {
            for (int i = i0; i < i1; i++)
                pix[i] = colorIndex;
            i0 += dst.Stride;
            i1 += dst.Stride;
        }
    }

    private static bool DrawMaskNrgba64Copy(
        IWritableImage dst, Rect r, IImage src, Point sp, IImage? mask, Op op)
    {
        if (dst is not Nrgba64Image dstNrgba64 || op != Op.Src || mask != null || src is not Nrgba64Image srcNrgba64)
            return false;

        int d0 = dstNrgba64.PixOffset(r.Min.X, r.Min.Y);
        int s0 = srcNrgba64.PixOffset(sp.X, sp.Y);
        DrawCopySrc(
            dstNrgba64.Pix.Span[d0..], dstNrgba64.Stride, r,
            srcNrgba64.Pix.Span[s0..], srcNrgba64.Stride, sp, 8 * r.Dx());
        return true;
    }

    private static (int x0, int x1, int dx, int y0, int y1, int dy) GetDrawIterationBounds(
        IImage dst, Rect r, IImage src, Point sp)
    {
        int x0 = r.Min.X, x1 = r.Max.X, dx = 1;
        int y0 = r.Min.Y, y1 = r.Max.Y, dy = 1;
        if (ProcessBackward(dst, r, src, sp))
        {
            (x0, x1, dx) = (x1 - 1, x0 - 1, -1);
            (y0, y1, dy) = (y1 - 1, y0 - 1, -1);
        }
        return (x0, x1, dx, y0, y1, dy);
    }

    private static bool DrawMaskToRgba64(
        IWritableImage dst, Rect r, IImage src, Point sp,
        IImage? mask, Point mp, Op op,
        int x0, int x1, int dx, int y0, int y1, int dy)
    {
        if (dst is not IRgba64WritableImage dstRgba64 || src is not IRgba64Image srcRgba64)
            return false;

        if (mask == null)
        {
            if (dst is Rgba64Image dst64 && src is Rgba64Image src64)
            {
                if (op == Op.Src)
                {
                    int d0 = dst64.PixOffset(x0, y0);
                    int s0 = src64.PixOffset(sp.X + x0 - r.Min.X, sp.Y + y0 - r.Min.Y);
                    DrawCopySrc(
                        dst64.Pix.Span[d0..], dst64.Stride, r,
                        src64.Pix.Span[s0..], src64.Stride, sp, 8 * r.Dx());
                }
                else
                    DrawRgba64OverRgba64(dst64, src64, r, sp, x0, x1, dx, y0, y1, dy);
            }
            else if (dst is RgbaImage dstRgba && src is Rgba64Image srcFrom64)
                DrawRgba64OverRgba(dstRgba, srcFrom64, r, sp, op, x0, x1, dx, y0, y1, dy);
            else
                DrawRgba64NoMask(dstRgba64, srcRgba64, r, sp, op, x0, x1, dx, y0, y1, dy);
            return true;
        }

        if (mask is IRgba64Image maskRgba64)
        {
            DrawRgba64WithMask(dstRgba64, srcRgba64, maskRgba64, r, sp, mp, op, x0, x1, dx, y0, y1, dy);
            return true;
        }

        return false;
    }

    private static void DrawRgba64NoMask(
        IRgba64WritableImage dst, IRgba64Image src, Rect r, Point sp, Op op,
        int x0, int x1, int dx, int y0, int y1, int dy)
    {
        int sy = sp.Y + y0 - r.Min.Y;
        for (int y = y0; y != y1; y += dy, sy += dy)
        {
            int sx = sp.X + x0 - r.Min.X;
            for (int x = x0; x != x1; x += dx, sx += dx)
            {
                if (op == Op.Src)
                    dst.SetRgba64(x, y, src.Rgba64At(sx, sy));
                else
                {
                    var srgba = src.Rgba64At(sx, sy);
                    uint a = M - srgba.A;
                    var drgba = dst.Rgba64At(x, y);
                    dst.SetRgba64(x, y, new Rgba64(
                        (ushort)((drgba.R * a) / M + srgba.R),
                        (ushort)((drgba.G * a) / M + srgba.G),
                        (ushort)((drgba.B * a) / M + srgba.B),
                        (ushort)((drgba.A * a) / M + srgba.A)));
                }
            }
        }
    }

    private static void DrawRgba64OverRgba64(
        Rgba64Image dst, Rgba64Image src, Rect r, Point sp,
        int x0, int x1, int dx, int y0, int y1, int dy)
    {
        int sy = sp.Y + y0 - r.Min.Y;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        for (int y = y0; y != y1; y += dy, sy += dy)
        {
            int sx = sp.X + x0 - r.Min.X;
            for (int x = x0; x != x1; x += dx, sx += dx)
            {
                int di = dst.PixOffset(x, y);
                int si = src.PixOffset(sx, sy);
                ushort sr = (ushort)(spix[si] << 8 | spix[si + 1]);
                ushort sg = (ushort)(spix[si + 2] << 8 | spix[si + 3]);
                ushort sb = (ushort)(spix[si + 4] << 8 | spix[si + 5]);
                ushort sa = (ushort)(spix[si + 6] << 8 | spix[si + 7]);
                ushort dr = (ushort)(dpix[di] << 8 | dpix[di + 1]);
                ushort dg = (ushort)(dpix[di + 2] << 8 | dpix[di + 3]);
                ushort db = (ushort)(dpix[di + 4] << 8 | dpix[di + 5]);
                ushort da = (ushort)(dpix[di + 6] << 8 | dpix[di + 7]);
                uint a = M - sa;
                ushort or = (ushort)((dr * a) / M + sr);
                ushort og = (ushort)((dg * a) / M + sg);
                ushort ob = (ushort)((db * a) / M + sb);
                ushort oa = (ushort)((da * a) / M + sa);
                dpix[di] = (byte)(or >> 8);
                dpix[di + 1] = (byte)or;
                dpix[di + 2] = (byte)(og >> 8);
                dpix[di + 3] = (byte)og;
                dpix[di + 4] = (byte)(ob >> 8);
                dpix[di + 5] = (byte)ob;
                dpix[di + 6] = (byte)(oa >> 8);
                dpix[di + 7] = (byte)oa;
            }
        }
    }

    private static void DrawRgba64OverRgba(
        RgbaImage dst, Rgba64Image src, Rect r, Point sp, Op op,
        int x0, int x1, int dx, int y0, int y1, int dy)
    {
        int sy = sp.Y + y0 - r.Min.Y;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        for (int y = y0; y != y1; y += dy, sy += dy)
        {
            int sx = sp.X + x0 - r.Min.X;
            for (int x = x0; x != x1; x += dx, sx += dx)
            {
                int di = dst.PixOffset(x, y);
                int si = src.PixOffset(sx, sy);
                if (op == Op.Src)
                {
                    dpix[di] = spix[si];
                    dpix[di + 1] = spix[si + 2];
                    dpix[di + 2] = spix[si + 4];
                    dpix[di + 3] = spix[si + 7];
                }
                else
                {
                    ushort sa = (ushort)(spix[si + 6] << 8 | spix[si + 7]);
                    uint a = (M - sa) * 0x101;
                    ushort sr = (ushort)(spix[si] << 8 | spix[si + 1]);
                    ushort sg = (ushort)(spix[si + 2] << 8 | spix[si + 3]);
                    ushort sb = (ushort)(spix[si + 4] << 8 | spix[si + 5]);
                    dpix[di] = (byte)((dpix[di] * a / M + sr) >> 8);
                    dpix[di + 1] = (byte)((dpix[di + 1] * a / M + sg) >> 8);
                    dpix[di + 2] = (byte)((dpix[di + 2] * a / M + sb) >> 8);
                    dpix[di + 3] = (byte)((dpix[di + 3] * a / M + sa) >> 8);
                }
            }
        }
    }

    private static void DrawRgba64WithMask(
        IRgba64WritableImage dst, IRgba64Image src, IRgba64Image mask,
        Rect r, Point sp, Point mp, Op op,
        int x0, int x1, int dx, int y0, int y1, int dy)
    {
        int sy = sp.Y + y0 - r.Min.Y;
        int my = mp.Y + y0 - r.Min.Y;
        for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
        {
            int sx = sp.X + x0 - r.Min.X;
            int mx = mp.X + x0 - r.Min.X;
            for (int x = x0; x != x1; x += dx, sx += dx, mx += dx)
            {
                uint ma = mask.Rgba64At(mx, my).A;
                switch (ma)
                {
                    case 0:
                        if (op != Op.Over)
                            dst.SetRgba64(x, y, new Rgba64(0, 0, 0, 0));
                        break;
                    case M when op == Op.Src:
                        dst.SetRgba64(x, y, src.Rgba64At(sx, sy));
                        break;
                    default:
                        var srgba = src.Rgba64At(sx, sy);
                        if (op == Op.Over)
                        {
                            var drgba = dst.Rgba64At(x, y);
                            uint a = M - (srgba.A * ma / M);
                            dst.SetRgba64(x, y, new Rgba64(
                                (ushort)((drgba.R * a + srgba.R * ma) / M),
                                (ushort)((drgba.G * a + srgba.G * ma) / M),
                                (ushort)((drgba.B * a + srgba.B * ma) / M),
                                (ushort)((drgba.A * a + srgba.A * ma) / M)));
                        }
                        else
                        {
                            dst.SetRgba64(x, y, new Rgba64(
                                (ushort)(srgba.R * ma / M),
                                (ushort)(srgba.G * ma / M),
                                (ushort)(srgba.B * ma / M),
                                (ushort)(srgba.A * ma / M)));
                        }
                        break;
                }
            }
        }
    }

    private static bool DrawMaskGrayCopy(
        IWritableImage dst, Rect r, IImage src, Point sp, IImage? mask, Op op)
    {
        if (dst is not GrayImage dstGray || mask != null || src is not GrayImage srcGrayImg || op != Op.Src)
            return false;

        int d0 = dstGray.PixOffset(r.Min.X, r.Min.Y);
        int s0 = srcGrayImg.PixOffset(sp.X, sp.Y);
        DrawCopySrc(dstGray.Pix.Span[d0..], dstGray.Stride, r, srcGrayImg.Pix.Span[s0..], srcGrayImg.Stride, sp, r.Dx());
        return true;
    }

    private static bool DrawMaskAlphaCopy(
        IWritableImage dst, Rect r, IImage src, Point sp, IImage? mask, Op op)
    {
        if (dst is not AlphaImage dstAlpha || mask != null || src is not AlphaImage srcAlpha || op != Op.Src)
            return false;

        int d0 = dstAlpha.PixOffset(r.Min.X, r.Min.Y);
        int s0 = srcAlpha.PixOffset(sp.X, sp.Y);
        DrawCopySrc(dstAlpha.Pix.Span[d0..], dstAlpha.Stride, r, srcAlpha.Pix.Span[s0..], srcAlpha.Stride, sp, r.Dx());
        return true;
    }

    private static bool DrawMaskNrgbaFallback(
        IWritableImage dst, Rect r, IImage src, Point sp,
        IImage? mask, Point mp, Op op)
    {
        if (dst is not NrgbaImage dstNrgbaFallback || src is not NrgbaImage srcNrgbaFallback)
            return false;

        if (mask == null)
        {
            if (op == Op.Over)
                DrawNrgbaOverNrgbaDst(dstNrgbaFallback, r, srcNrgbaFallback, sp);
            else
            {
                int d0 = dstNrgbaFallback.PixOffset(r.Min.X, r.Min.Y);
                int s0 = srcNrgbaFallback.PixOffset(sp.X, sp.Y);
                DrawCopySrc(
                    dstNrgbaFallback.Pix.Span[d0..], dstNrgbaFallback.Stride, r,
                    srcNrgbaFallback.Pix.Span[s0..], srcNrgbaFallback.Stride, sp, 4 * r.Dx());
            }
            return true;
        }

        if (mask is AlphaImage alphaMaskFallback)
        {
            if (op == Op.Over)
                DrawNrgbaMaskOverNrgbaDst(dstNrgbaFallback, r, srcNrgbaFallback, sp, alphaMaskFallback, mp);
            else
                DrawNrgbaMaskSrcNrgbaDst(dstNrgbaFallback, r, srcNrgbaFallback, sp, alphaMaskFallback, mp);
            return true;
        }

        return false;
    }

    private static void DrawGeneric(
        IWritableImage dst, Rect r, IImage src, Point sp, IImage? mask, Point mp, Op op,
        int x0, int x1, int dx, int y0, int y1, int dy)
    {
        var outColor = new Rgba64(0, 0, 0, 0xffff);
        int sy = sp.Y + y0 - r.Min.Y;
        int my = mp.Y + y0 - r.Min.Y;
        for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
        {
            int sx = sp.X + x0 - r.Min.X;
            int mx = mp.X + x0 - r.Min.X;
            for (int x = x0; x != x1; x += dx, sx += dx, mx += dx)
            {
                uint ma = M;
                if (mask is AlphaImage alphaMask)
                    ma = (uint)alphaMask.AlphaAt(mx, my).A * 0x101;
                else if (mask is IRgba64Image maskRgba64)
                    ma = maskRgba64.Rgba64At(mx, my).A;
                else if (mask != null)
                    (_, _, _, ma) = mask.At(mx, my).GetRgba();

                switch (ma)
                {
                    case 0:
                        if (op != Op.Over)
                            dst.Set(x, y, Colors.Transparent);
                        break;
                    case M when op == Op.Src:
                        dst.Set(x, y, GetColor(src, sx, sy));
                        break;
                    default:
                        var (sr, sg, sb, sa) = GetRgba(src, sx, sy);
                        if (op == Op.Over)
                        {
                            var (dr, dg, db, da) = GetRgba(dst, x, y);
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

    private static IColor GetColor(IImage src, int x, int y) => src switch
    {
        NrgbaImage nrgba => nrgba.NrgbaAt(x, y),
        RgbaImage rgba => rgba.RgbaAt(x, y),
        GrayImage gray => gray.GrayAt(x, y),
        _ => src.At(x, y),
    };

    private static (uint R, uint G, uint B, uint A) GetRgba(IImage img, int x, int y) => img switch
    {
        NrgbaImage nrgba => nrgba.NrgbaAt(x, y).GetRgba(),
        RgbaImage rgba => rgba.RgbaAt(x, y).GetRgba(),
        GrayImage gray => gray.GrayAt(x, y).GetRgba(),
        IRgba64Image rgba64 => rgba64.Rgba64At(x, y).GetRgba(),
        _ => img.At(x, y).GetRgba(),
    };

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
        var (sr, sg, sb, sa) = src.GetRgba();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreNrgba(Span<byte> pix, int i, uint r, uint g, uint b, uint a)
    {
        if (a != 0 && a != 0xffff)
        {
            r = r * 0xffff / a;
            g = g * 0xffff / a;
            b = b * 0xffff / a;
        }
        pix[i] = (byte)(r >> 8);
        pix[i + 1] = (byte)(g >> 8);
        pix[i + 2] = (byte)(b >> 8);
        pix[i + 3] = (byte)(a >> 8);
    }

    private static void DrawNrgbaMaskOver(
        RgbaImage dst, Rect r, NrgbaImage src, Point sp, AlphaImage mask, Point mp)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        int si0 = src.PixOffset(sp.X, sp.Y);
        int mi0 = mask.PixOffset(mp.X, mp.Y);
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        var mpix = mask.Pix.Span;
        for (int y = r.Min.Y - dst.Rect.Min.Y; y != yMax; y++)
        {
            for (int i = i0, si = si0, mi = mi0; i < i1; i += 4, si += 4, mi++)
            {
                uint ma = mpix[mi];
                if (ma == 0) continue;
                ma |= ma << 8;
                uint sa = (uint)spix[si + 3] * 0x101;
                uint sr = (uint)spix[si] * sa / 0xff;
                uint sg = (uint)spix[si + 1] * sa / 0xff;
                uint sb = (uint)spix[si + 2] * sa / 0xff;
                uint a = (M - (sa * ma / M)) * 0x101;
                dpix[i] = (byte)((dpix[i] * a + sr * ma) / M >> 8);
                dpix[i + 1] = (byte)((dpix[i + 1] * a + sg * ma) / M >> 8);
                dpix[i + 2] = (byte)((dpix[i + 2] * a + sb * ma) / M >> 8);
                dpix[i + 3] = (byte)((dpix[i + 3] * a + sa * ma) / M >> 8);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
            si0 += src.Stride;
            mi0 += mask.Stride;
        }
    }

    private static void DrawGlyphMaskSrc(
        RgbaImage dst, Rect r, uint sr, uint sg, uint sb, uint sa, AlphaImage mask, Point mp)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        int mi0 = mask.PixOffset(mp.X, mp.Y);
        var dpix = dst.Pix.Span;
        var mpix = mask.Pix.Span;
        for (int y = r.Min.Y, my = mp.Y; y != r.Max.Y; y++, my++)
        {
            for (int i = i0, mi = mi0; i < i1; i += 4, mi++)
            {
                uint ma = mpix[mi];
                ma |= ma << 8;
                dpix[i] = (byte)(sr * ma / M >> 8);
                dpix[i + 1] = (byte)(sg * ma / M >> 8);
                dpix[i + 2] = (byte)(sb * ma / M >> 8);
                dpix[i + 3] = (byte)(sa * ma / M >> 8);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
            mi0 += mask.Stride;
        }
    }

    private static void DrawRgbaMaskSrc(
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
                dpix[i] = (byte)(sr * ma / M >> 8);
                dpix[i + 1] = (byte)(sg * ma / M >> 8);
                dpix[i + 2] = (byte)(sb * ma / M >> 8);
                dpix[i + 3] = (byte)(sa * ma / M >> 8);
            }
            i0 += dy * dst.Stride;
        }
    }

    private static void DrawNrgbaMaskSrc(
        RgbaImage dst, Rect r, NrgbaImage src, Point sp, AlphaImage mask, Point mp)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        int si0 = src.PixOffset(sp.X, sp.Y);
        int mi0 = mask.PixOffset(mp.X, mp.Y);
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        var mpix = mask.Pix.Span;
        for (int y = r.Min.Y - dst.Rect.Min.Y; y != yMax; y++)
        {
            for (int i = i0, si = si0, mi = mi0; i < i1; i += 4, si += 4, mi++)
            {
                uint ma = mpix[mi];
                ma |= ma << 8;
                uint sa = (uint)spix[si + 3] * 0x101;
                uint sr = (uint)spix[si] * sa / 0xff;
                uint sg = (uint)spix[si + 1] * sa / 0xff;
                uint sb = (uint)spix[si + 2] * sa / 0xff;
                dpix[i] = (byte)(sr * ma / M >> 8);
                dpix[i + 1] = (byte)(sg * ma / M >> 8);
                dpix[i + 2] = (byte)(sb * ma / M >> 8);
                dpix[i + 3] = (byte)(sa * ma / M >> 8);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
            si0 += src.Stride;
            mi0 += mask.Stride;
        }
    }

    private static void DrawGrayMaskSrc(
        RgbaImage dst, Rect r, GrayImage src, Point sp, AlphaImage mask, Point mp)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        int si0 = src.PixOffset(sp.X, sp.Y);
        int mi0 = mask.PixOffset(mp.X, mp.Y);
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        var mpix = mask.Pix.Span;
        for (int y = r.Min.Y - dst.Rect.Min.Y; y != yMax; y++)
        {
            for (int i = i0, si = si0, mi = mi0; i < i1; i += 4, si++, mi++)
            {
                uint ma = mpix[mi];
                ma |= ma << 8;
                uint g = spix[si];
                g |= g << 8;
                const uint sa = 0xffff;
                dpix[i] = (byte)(g * ma / M >> 8);
                dpix[i + 1] = (byte)(g * ma / M >> 8);
                dpix[i + 2] = (byte)(g * ma / M >> 8);
                dpix[i + 3] = (byte)(sa * ma / M >> 8);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
            si0 += src.Stride;
            mi0 += mask.Stride;
        }
    }

    private static void DrawRgba64MaskSrc(
        RgbaImage dst, Rect r, IRgba64Image src, Point sp, AlphaImage mask, Point mp)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        int mi0 = mask.PixOffset(mp.X, mp.Y);
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        var dpix = dst.Pix.Span;
        var mpix = mask.Pix.Span;
        for (int y = r.Min.Y - dst.Rect.Min.Y, sy = sp.Y, my = mp.Y; y != yMax; y++, sy++, my++)
        {
            int sx = sp.X;
            for (int i = i0, mi = mi0; i < i1; i += 4, mi++, sx++)
            {
                uint ma = mpix[mi];
                ma |= ma << 8;
                var srgba = src.Rgba64At(sx, sy);
                dpix[i] = (byte)(srgba.R * ma / M >> 8);
                dpix[i + 1] = (byte)(srgba.G * ma / M >> 8);
                dpix[i + 2] = (byte)(srgba.B * ma / M >> 8);
                dpix[i + 3] = (byte)(srgba.A * ma / M >> 8);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
            mi0 += mask.Stride;
        }
    }

    private static (uint R, uint G, uint B, uint A) RgbaPremultiplied(Rgba c)
    {
        uint r = c.R; r |= r << 8;
        uint g = c.G; g |= g << 8;
        uint b = c.B; b |= b << 8;
        uint a = c.A; a |= a << 8;
        return (r, g, b, a);
    }

    private static void DrawNrgbaFillOver(NrgbaImage dst, Rect r, uint sr, uint sg, uint sb, uint sa)
    {
        uint a16 = M - sa;
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        var pix = dst.Pix.Span;
        for (int y = r.Min.Y; y != r.Max.Y; y++)
        {
            for (int i = i0; i < i1; i += 4)
            {
                uint da = pix[i + 3];
                da |= da << 8;
                uint dr = (uint)pix[i] * da / 0xff;
                uint dg = (uint)pix[i + 1] * da / 0xff;
                uint db = (uint)pix[i + 2] * da / 0xff;
                StoreNrgba(pix, i,
                    (dr * a16) / M + sr,
                    (dg * a16) / M + sg,
                    (db * a16) / M + sb,
                    (da * a16) / M + sa);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
        }
    }

    private static void DrawNrgbaOverNrgbaDst(NrgbaImage dst, Rect r, NrgbaImage src, Point sp)
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
        int sx0 = sp.X + x0 - r.Min.X;
        int sx1 = sx0 + (x1 - x0);
        int i0 = dst.PixOffset(x0, y0);
        int di = dx * 4;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        for (int y = y0; y != y1; y += dy, sy += dy)
        {
            for (int i = i0, sx = sx0; sx != sx1; i += di, sx += dx)
            {
                int si = src.PixOffset(sx, sy);
                uint sa = spix[si + 3];
                sa |= sa << 8;
                uint sr = (uint)spix[si] * sa / 0xff;
                uint sg = (uint)spix[si + 1] * sa / 0xff;
                uint sb = (uint)spix[si + 2] * sa / 0xff;
                uint a16 = M - sa;
                uint da = dpix[i + 3];
                da |= da << 8;
                uint dr = (uint)dpix[i] * da / 0xff;
                uint dg = (uint)dpix[i + 1] * da / 0xff;
                uint db = (uint)dpix[i + 2] * da / 0xff;
                StoreNrgba(dpix, i,
                    (dr * a16) / M + sr,
                    (dg * a16) / M + sg,
                    (db * a16) / M + sb,
                    (da * a16) / M + sa);
            }
            i0 += dy * dst.Stride;
        }
    }

    private static void DrawRgbaOverNrgbaDst(NrgbaImage dst, Rect r, RgbaImage src, Point sp)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        int si0 = src.PixOffset(sp.X, sp.Y);
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        for (int y = r.Min.Y - dst.Rect.Min.Y; y != yMax; y++)
        {
            for (int i = i0, si = si0; i < i1; i += 4, si += 4)
            {
                uint sa = spix[si + 3];
                sa |= sa << 8;
                uint sr = spix[si];
                uint sg = spix[si + 1];
                uint sb = spix[si + 2];
                sr |= sr << 8;
                sg |= sg << 8;
                sb |= sb << 8;
                uint a16 = M - sa;
                uint da = dpix[i + 3];
                da |= da << 8;
                uint dr = (uint)dpix[i] * da / 0xff;
                uint dg = (uint)dpix[i + 1] * da / 0xff;
                uint db = (uint)dpix[i + 2] * da / 0xff;
                StoreNrgba(dpix, i,
                    (dr * a16) / M + sr,
                    (dg * a16) / M + sg,
                    (db * a16) / M + sb,
                    (da * a16) / M + sa);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
            si0 += src.Stride;
        }
    }

    private static void DrawGrayOverNrgbaDst(NrgbaImage dst, Rect r, GrayImage src, Point sp)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        int si0 = src.PixOffset(sp.X, sp.Y);
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        for (int y = r.Min.Y - dst.Rect.Min.Y; y != yMax; y++)
        {
            for (int i = i0, si = si0; i < i1; i += 4, si++)
            {
                uint gy = spix[si];
                gy |= gy << 8;
                const uint sa = 0xffff;
                uint a16 = M - sa;
                uint da = dpix[i + 3];
                da |= da << 8;
                uint dr = (uint)dpix[i] * da / 0xff;
                uint dg = (uint)dpix[i + 1] * da / 0xff;
                uint db = (uint)dpix[i + 2] * da / 0xff;
                StoreNrgba(dpix, i,
                    (dr * a16) / M + gy,
                    (dg * a16) / M + gy,
                    (db * a16) / M + gy,
                    (da * a16) / M + sa);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
            si0 += src.Stride;
        }
    }

    private static void DrawNrgbaFillSrc(NrgbaImage dst, Rect r, uint sr, uint sg, uint sb, uint sa)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        var pix = dst.Pix.Span;
        for (int y = r.Min.Y; y != r.Max.Y; y++)
        {
            for (int i = i0; i < i1; i += 4)
                StoreNrgba(pix, i, sr, sg, sb, sa);
            i0 += dst.Stride;
            i1 += dst.Stride;
        }
    }

    private static void DrawRgbaSrcNrgbaDst(NrgbaImage dst, Rect r, RgbaImage src, Point sp)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        int si0 = src.PixOffset(sp.X, sp.Y);
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        for (int y = r.Min.Y - dst.Rect.Min.Y; y != yMax; y++)
        {
            for (int i = i0, si = si0; i < i1; i += 4, si += 4)
            {
                uint sr = spix[si];
                uint sg = spix[si + 1];
                uint sb = spix[si + 2];
                uint sa = spix[si + 3];
                sr |= sr << 8;
                sg |= sg << 8;
                sb |= sb << 8;
                sa |= sa << 8;
                StoreNrgba(dpix, i, sr, sg, sb, sa);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
            si0 += src.Stride;
        }
    }

    private static void DrawGraySrcNrgbaDst(NrgbaImage dst, Rect r, GrayImage src, Point sp)
    {
        int i0 = dst.PixOffset(r.Min.X, r.Min.Y);
        int i1 = i0 + r.Dx() * 4;
        int si0 = src.PixOffset(sp.X, sp.Y);
        int yMax = r.Max.Y - dst.Rect.Min.Y;
        var dpix = dst.Pix.Span;
        var spix = src.Pix.Span;
        for (int y = r.Min.Y - dst.Rect.Min.Y; y != yMax; y++)
        {
            for (int i = i0, si = si0; i < i1; i += 4, si++)
            {
                uint gy = spix[si];
                gy |= gy << 8;
                StoreNrgba(dpix, i, gy, gy, gy, 0xffff);
            }
            i0 += dst.Stride;
            i1 += dst.Stride;
            si0 += src.Stride;
        }
    }

    private static void DrawNrgbaMaskOverNrgbaDst(
        NrgbaImage dst, Rect r, NrgbaImage src, Point sp, AlphaImage mask, Point mp)
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
                if (ma == 0) continue;
                ma |= ma << 8;
                int si = src.PixOffset(sx, sy);
                uint sa = spix[si + 3];
                sa |= sa << 8;
                uint sr = (uint)spix[si] * sa / 0xff;
                uint sg = (uint)spix[si + 1] * sa / 0xff;
                uint sb = (uint)spix[si + 2] * sa / 0xff;
                uint a16 = M - (sa * ma / M);
                uint da = dpix[i + 3];
                da |= da << 8;
                uint dr = (uint)dpix[i] * da / 0xff;
                uint dg = (uint)dpix[i + 1] * da / 0xff;
                uint db = (uint)dpix[i + 2] * da / 0xff;
                StoreNrgba(dpix, i,
                    (dr * a16 + sr * ma) / M,
                    (dg * a16 + sg * ma) / M,
                    (db * a16 + sb * ma) / M,
                    (da * a16 + sa * ma) / M);
            }
            i0 += dy * dst.Stride;
        }
    }

    private static void DrawNrgbaMaskSrcNrgbaDst(
        NrgbaImage dst, Rect r, NrgbaImage src, Point sp, AlphaImage mask, Point mp)
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
                uint sa = spix[si + 3];
                sa |= sa << 8;
                uint sr = (uint)spix[si] * sa / 0xff;
                uint sg = (uint)spix[si + 1] * sa / 0xff;
                uint sb = (uint)spix[si + 2] * sa / 0xff;
                StoreNrgba(dpix, i,
                    sr * ma / M,
                    sg * ma / M,
                    sb * ma / M,
                    sa * ma / M);
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

        if (src is NrgbaImage nrgbaSrc)
        {
            if (mask == null)
            {
                if (op == Op.Over) { DrawNrgbaOver(dst, r, nrgbaSrc, sp); return; }
                DrawNrgbaSrc(dst, r, nrgbaSrc, sp);
                return;
            }
            if (mask is AlphaImage alphaMask)
            {
                if (op == Op.Over) { DrawNrgbaMaskOver(dst, r, nrgbaSrc, sp, alphaMask, mp); return; }
                DrawNrgbaMaskSrc(dst, r, nrgbaSrc, sp, alphaMask, mp);
                return;
            }
        }
        if (src is RgbaImage rgbaSrc)
        {
            if (mask == null)
            {
                if (op == Op.Over) { DrawCopyOver(dst, r, rgbaSrc, sp); return; }
                int d0 = dst.PixOffset(r.Min.X, r.Min.Y);
                int s0 = rgbaSrc.PixOffset(sp.X, sp.Y);
                DrawCopySrc(dst.Pix.Span[d0..], dst.Stride, r, rgbaSrc.Pix.Span[s0..], rgbaSrc.Stride, sp, 4 * r.Dx());
                return;
            }
            if (mask is AlphaImage alphaMask)
            {
                if (op == Op.Over) { DrawRgbaMaskOver(dst, r, rgbaSrc, sp, alphaMask, mp); return; }
                DrawRgbaMaskSrc(dst, r, rgbaSrc, sp, alphaMask, mp);
                return;
            }
        }
        if (src is GrayImage graySrc)
        {
            if (mask == null)
            {
                DrawGray(dst, r, graySrc, sp);
                return;
            }
            if (mask is AlphaImage alphaMask)
            {
                if (op == Op.Over) { DrawGrayMaskOver(dst, r, graySrc, sp, alphaMask, mp); return; }
                DrawGrayMaskSrc(dst, r, graySrc, sp, alphaMask, mp);
                return;
            }
        }

        for (int y = y0; y != y1; y += dy, sy += dy, my += dy)
        {
            for (int i = i0, sx = sx0, mx = mx0; sx != sx1; i += di, sx += dx, mx += dx)
            {
                uint ma = M;
                if (mask is AlphaImage alphaMask)
                    ma = (uint)alphaMask.AlphaAt(mx, my).A * 0x101;
                else if (mask is IRgba64Image maskRgba64)
                    ma = maskRgba64.Rgba64At(mx, my).A;
                else if (mask != null)
                    (_, _, _, ma) = mask.At(mx, my).GetRgba();
                var (sr, sg, sb, sa) = GetRgba(src, sx, sy);
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
        int[]? palette = null;
        Span<byte> pix = default;
        int stride = 0;
        if (dst is PalettedImage p)
        {
            var flat = new int[4 * p.Palette.Length];
            for (int i = 0; i < p.Palette.Length; i++)
            {
                var (pr, pg, pb, pa) = p.Palette[i].GetRgba();
                int o = 4 * i;
                flat[o] = (int)pr;
                flat[o + 1] = (int)pg;
                flat[o + 2] = (int)pb;
                flat[o + 3] = (int)pa;
            }
            palette = flat;
            int offset = p.PixOffset(r.Min.X, r.Min.Y);
            pix = p.Pix.Span[offset..];
            stride = p.Stride;
        }

        int[]? quantErrorCurr = null;
        int[]? quantErrorNext = null;
        if (floydSteinberg)
        {
            quantErrorCurr = new int[4 * (r.Dx() + 2)];
            quantErrorNext = new int[4 * (r.Dx() + 2)];
        }

        var outColor = new Rgba64(0, 0, 0, 0xffff);
        for (int y = 0; y != r.Dy(); y++)
        {
            for (int x = 0; x != r.Dx(); x++)
            {
                var (sr, sg, sb, sa) = src switch
                {
                    RgbaImage rgba => RgbaPremultiplied(rgba.RgbaAt(sp.X + x, sp.Y + y)),
                    NrgbaImage nrgba => nrgba.NrgbaAt(sp.X + x, sp.Y + y).GetRgba(),
                    YCbCrImage ycbcr => ycbcr.YCbCrAt(sp.X + x, sp.Y + y).GetRgba(),
                    _ => src.At(sp.X + x, sp.Y + y).GetRgba(),
                };
                int er = (int)sr, eg = (int)sg, eb = (int)sb, ea = (int)sa;
                if (floydSteinberg)
                {
                    int q = 4 * (x + 1);
                    er = Clamp(er + quantErrorCurr![q] / 16);
                    eg = Clamp(eg + quantErrorCurr[q + 1] / 16);
                    eb = Clamp(eb + quantErrorCurr[q + 2] / 16);
                    ea = Clamp(ea + quantErrorCurr[q + 3] / 16);
                }

                if (palette != null)
                {
                    int bestIndex = 0;
                    uint bestSum = uint.MaxValue;
                    for (int index = 0; index < palette.Length; index += 4)
                    {
                        uint sum = SqDiff(er, palette[index]) +
                                   SqDiff(eg, palette[index + 1]) +
                                   SqDiff(eb, palette[index + 2]) +
                                   SqDiff(ea, palette[index + 3]);
                        if (sum < bestSum)
                        {
                            bestIndex = index >> 2;
                            bestSum = sum;
                            if (sum == 0) break;
                        }
                    }
                    pix[y * stride + x] = (byte)bestIndex;

                    if (!floydSteinberg) continue;

                    er -= palette[4 * bestIndex];
                    eg -= palette[4 * bestIndex + 1];
                    eb -= palette[4 * bestIndex + 2];
                    ea -= palette[4 * bestIndex + 3];
                }
                else
                {
                    outColor = new Rgba64((ushort)er, (ushort)eg, (ushort)eb, (ushort)ea);
                    dst.Set(r.Min.X + x, r.Min.Y + y, outColor);
                    if (!floydSteinberg) continue;
                    (sr, sg, sb, sa) = dst.At(r.Min.X + x, r.Min.Y + y).GetRgba();
                    er -= (int)sr;
                    eg -= (int)sg;
                    eb -= (int)sb;
                    ea -= (int)sa;
                }

                int nx = 4 * x;
                quantErrorNext![nx] += er * 3;
                quantErrorNext[nx + 1] += eg * 3;
                quantErrorNext[nx + 2] += eb * 3;
                quantErrorNext[nx + 3] += ea * 3;
                quantErrorNext[nx + 4] += er * 5;
                quantErrorNext[nx + 5] += eg * 5;
                quantErrorNext[nx + 6] += eb * 5;
                quantErrorNext[nx + 7] += ea * 5;
                quantErrorNext[nx + 8] += er;
                quantErrorNext[nx + 9] += eg;
                quantErrorNext[nx + 10] += eb;
                quantErrorNext[nx + 11] += ea;
                int cx = 4 * (x + 2);
                quantErrorCurr![cx] += er * 7;
                quantErrorCurr[cx + 1] += eg * 7;
                quantErrorCurr[cx + 2] += eb * 7;
                quantErrorCurr[cx + 3] += ea * 7;
            }

            if (floydSteinberg)
            {
                (quantErrorCurr, quantErrorNext) = (quantErrorNext, quantErrorCurr);
                Array.Clear(quantErrorNext!);
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
