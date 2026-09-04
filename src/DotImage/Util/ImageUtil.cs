// Ported from Go src/image/internal/imageutil/impl.go


using DotImage.Color;

namespace DotImage.Util;

public static class ImageUtil
{
    /// <summary>
    /// Draws the YCbCr source image on the RGBA destination image with
    /// r.Min in dst aligned with sp in src. Returns whether the draw was
    /// successful. If it returns false, no dst pixels were changed.
    /// </summary>
    public static bool DrawYCbCr(RgbaImage dst, Rect r, YCbCrImage src, Point sp)
    {
        int x0 = (r.Min.X - dst.Rect.Min.X) * 4;
        int x1 = (r.Max.X - dst.Rect.Min.X) * 4;
        int y0 = r.Min.Y - dst.Rect.Min.Y;
        int y1 = r.Max.Y - dst.Rect.Min.Y;

        switch (src.SubsampleRatio)
        {
            case YCbCrSubsampleRatio.Ratio444:
                DrawYCbCr444(dst, src, sp, x0, x1, y0, y1);
                break;
            case YCbCrSubsampleRatio.Ratio422:
                DrawYCbCr422(dst, src, sp, x0, x1, y0, y1);
                break;
            case YCbCrSubsampleRatio.Ratio420:
                DrawYCbCr420(dst, src, sp, x0, x1, y0, y1);
                break;
            case YCbCrSubsampleRatio.Ratio440:
                DrawYCbCr440(dst, src, sp, x0, x1, y0, y1);
                break;
            default:
                return false;
        }

        return true;
    }

    private static void WriteYCbCrPixel(Span<byte> dpix, int x, byte y, byte cb, byte cr)
    {
        int yy1 = y * 0x10101;
        int cb1 = cb - 128;
        int cr1 = cr - 128;

        int rv = yy1 + 91881 * cr1;
        if (((uint)rv & 0xff000000) == 0)
            rv >>= 16;
        else
            rv = ~(rv >> 31);

        int gv = yy1 - 22554 * cb1 - 46802 * cr1;
        if (((uint)gv & 0xff000000) == 0)
            gv >>= 16;
        else
            gv = ~(gv >> 31);

        int bv = yy1 + 116130 * cb1;
        if (((uint)bv & 0xff000000) == 0)
            bv >>= 16;
        else
            bv = ~(bv >> 31);

        dpix[x] = (byte)rv;
        dpix[x + 1] = (byte)gv;
        dpix[x + 2] = (byte)bv;
        dpix[x + 3] = 255;
    }

    private static void DrawYCbCr444(RgbaImage dst, YCbCrImage src, Point sp, int x0, int x1, int y0, int y1)
    {
        var ySpan = src.Y.Span;
        var cbSpan = src.Cb.Span;
        var crSpan = src.Cr.Span;
        for (int y = y0, sy = sp.Y; y != y1; y++, sy++)
        {
            var dpix = dst.Pix.Span.Slice(y * dst.Stride);
            int yi = (sy - src.Rect.Min.Y) * src.YStride + (sp.X - src.Rect.Min.X);
            int ci = (sy - src.Rect.Min.Y) * src.CStride + (sp.X - src.Rect.Min.X);
            for (int x = x0; x != x1; x += 4, yi++, ci++)
                WriteYCbCrPixel(dpix, x, ySpan[yi], cbSpan[ci], crSpan[ci]);
        }
    }

    private static void DrawYCbCr422(RgbaImage dst, YCbCrImage src, Point sp, int x0, int x1, int y0, int y1)
    {
        var ySpan = src.Y.Span;
        var cbSpan = src.Cb.Span;
        var crSpan = src.Cr.Span;
        for (int y = y0, sy = sp.Y; y != y1; y++, sy++)
        {
            var dpix = dst.Pix.Span.Slice(y * dst.Stride);
            int yi = (sy - src.Rect.Min.Y) * src.YStride + (sp.X - src.Rect.Min.X);
            int ciBase = (sy - src.Rect.Min.Y) * src.CStride - src.Rect.Min.X / 2;
            for (int x = x0, sx = sp.X; x != x1; x += 4, sx++, yi++)
            {
                int ci = ciBase + sx / 2;
                WriteYCbCrPixel(dpix, x, ySpan[yi], cbSpan[ci], crSpan[ci]);
            }
        }
    }

    private static void DrawYCbCr420(RgbaImage dst, YCbCrImage src, Point sp, int x0, int x1, int y0, int y1)
    {
        var ySpan = src.Y.Span;
        var cbSpan = src.Cb.Span;
        var crSpan = src.Cr.Span;
        for (int y = y0, sy = sp.Y; y != y1; y++, sy++)
        {
            var dpix = dst.Pix.Span.Slice(y * dst.Stride);
            int yi = (sy - src.Rect.Min.Y) * src.YStride + (sp.X - src.Rect.Min.X);
            int ciBase = (sy / 2 - src.Rect.Min.Y / 2) * src.CStride - src.Rect.Min.X / 2;
            for (int x = x0, sx = sp.X; x != x1; x += 4, sx++, yi++)
            {
                int ci = ciBase + sx / 2;
                WriteYCbCrPixel(dpix, x, ySpan[yi], cbSpan[ci], crSpan[ci]);
            }
        }
    }

    private static void DrawYCbCr440(RgbaImage dst, YCbCrImage src, Point sp, int x0, int x1, int y0, int y1)
    {
        var ySpan = src.Y.Span;
        var cbSpan = src.Cb.Span;
        var crSpan = src.Cr.Span;
        for (int y = y0, sy = sp.Y; y != y1; y++, sy++)
        {
            var dpix = dst.Pix.Span.Slice(y * dst.Stride);
            int yi = (sy - src.Rect.Min.Y) * src.YStride + (sp.X - src.Rect.Min.X);
            int ci = (sy / 2 - src.Rect.Min.Y / 2) * src.CStride + (sp.X - src.Rect.Min.X);
            for (int x = x0; x != x1; x += 4, yi++, ci++)
                WriteYCbCrPixel(dpix, x, ySpan[yi], cbSpan[ci], crSpan[ci]);
        }
    }
}
