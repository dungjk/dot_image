// Ported from Go src/image/ycbcr.go


using DotImage.Color;

namespace DotImage;

public enum YCbCrSubsampleRatio
{
    Ratio444,
    Ratio422,
    Ratio420,
    Ratio440,
    Ratio411,
    Ratio410,
}

public sealed class YCbCrImage : IImage, IRgba64Image
{
    public Memory<byte> Y;
    public Memory<byte> Cb;
    public Memory<byte> Cr;
    public int YStride;
    public int CStride;
    public YCbCrSubsampleRatio SubsampleRatio;
    public Rect Rect;

    public IColorModel ColorModel() => ColorModels.YCbCr;
    public Rect Bounds() => this.Rect;
    public IColor At(int x, int y) => YCbCrAt(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        var (r, g, b, a) = YCbCrAt(x, y).Rgba();
        return new Rgba64((ushort)r, (ushort)g, (ushort)b, (ushort)a);
    }

    public YCbCr YCbCrAt(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new YCbCr(0, 0, 0);
        int yi = YOffset(x, y);
        int ci = COffset(x, y);
        return new YCbCr(Y.Span[yi], Cb.Span[ci], Cr.Span[ci]);
    }

    public int YOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * YStride + (x - this.Rect.Min.X);

    public int COffset(int x, int y)
    {
        switch (SubsampleRatio)
        {
            case YCbCrSubsampleRatio.Ratio422:
                return (y - this.Rect.Min.Y) * CStride + (x / 2 - this.Rect.Min.X / 2);
            case YCbCrSubsampleRatio.Ratio420:
                return (y / 2 - this.Rect.Min.Y / 2) * CStride + (x / 2 - this.Rect.Min.X / 2);
            case YCbCrSubsampleRatio.Ratio440:
                return (y / 2 - this.Rect.Min.Y / 2) * CStride + (x - this.Rect.Min.X);
            case YCbCrSubsampleRatio.Ratio411:
                return (y - this.Rect.Min.Y) * CStride + (x / 4 - this.Rect.Min.X / 4);
            case YCbCrSubsampleRatio.Ratio410:
                return (y / 2 - this.Rect.Min.Y / 2) * CStride + (x / 4 - this.Rect.Min.X / 4);
            default:
                return (y - this.Rect.Min.Y) * CStride + (x - this.Rect.Min.X);
        }
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty())
            return new YCbCrImage { SubsampleRatio = SubsampleRatio };
        int yi = YOffset(r.Min.X, r.Min.Y);
        int ci = COffset(r.Min.X, r.Min.Y);
        return new YCbCrImage
        {
            Y = Y.Slice(yi),
            Cb = Cb.Slice(ci),
            Cr = Cr.Slice(ci),
            SubsampleRatio = SubsampleRatio,
            YStride = YStride,
            CStride = CStride,
            Rect = r,
        };
    }

    public bool Opaque() => true;
}

public sealed class NYCbCrAImage : IImage, IRgba64Image
{
    public YCbCrImage YCbCrImage = new();
    public Memory<byte> A;
    public int AStride;

    public IColorModel ColorModel() => ColorModels.NYCbCrA;
    public Rect Bounds() => YCbCrImage.Rect;
    public IColor At(int x, int y) => NYCbCrAAt(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        var (r, g, b, a) = NYCbCrAAt(x, y).Rgba();
        return new Rgba64((ushort)r, (ushort)g, (ushort)b, (ushort)a);
    }

    public NYCbCrA NYCbCrAAt(int x, int y)
    {
        if (!new Point(x, y).In(YCbCrImage.Rect)) return new NYCbCrA(new YCbCr(0, 0, 0), 0);
        int yi = YCbCrImage.YOffset(x, y);
        int ci = YCbCrImage.COffset(x, y);
        int ai = AOffset(x, y);
        return new NYCbCrA(YCbCrImage.Y.Span[yi], YCbCrImage.Cb.Span[ci], YCbCrImage.Cr.Span[ci], A.Span[ai]);
    }

    public int AOffset(int x, int y) =>
        (y - YCbCrImage.Rect.Min.Y) * AStride + (x - YCbCrImage.Rect.Min.X);

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(YCbCrImage.Rect);
        if (r.Empty())
            return new NYCbCrAImage { YCbCrImage = new YCbCrImage { SubsampleRatio = YCbCrImage.SubsampleRatio } };
        int yi = YCbCrImage.YOffset(r.Min.X, r.Min.Y);
        int ci = YCbCrImage.COffset(r.Min.X, r.Min.Y);
        int ai = AOffset(r.Min.X, r.Min.Y);
        return new NYCbCrAImage
        {
            YCbCrImage = new YCbCrImage
            {
                Y = YCbCrImage.Y.Slice(yi),
                Cb = YCbCrImage.Cb.Slice(ci),
                Cr = YCbCrImage.Cr.Slice(ci),
                SubsampleRatio = YCbCrImage.SubsampleRatio,
                YStride = YCbCrImage.YStride,
                CStride = YCbCrImage.CStride,
                Rect = r,
            },
            A = A.Slice(ai),
            AStride = AStride,
        };
    }

    public bool Opaque()
    {
        if (YCbCrImage.Rect.Empty()) return true;
        int i0 = 0;
        int i1 = YCbCrImage.Rect.Dx();
        for (int y = YCbCrImage.Rect.Min.Y; y < YCbCrImage.Rect.Max.Y; y++)
        {
            var span = A.Span.Slice(i0, i1 - i0);
            foreach (byte a in span)
            {
                if (a != 0xff) return false;
            }
            i0 += AStride;
            i1 += AStride;
        }
        return true;
    }
}

public static class YCbCrImages
{
    private static (int w, int h, int cw, int ch) YCbCrSize(Rect r, YCbCrSubsampleRatio subsampleRatio)
    {
        int w = r.Dx();
        int h = r.Dy();
        int cw, ch;
        switch (subsampleRatio)
        {
            case YCbCrSubsampleRatio.Ratio422:
                cw = (r.Max.X + 1) / 2 - r.Min.X / 2;
                ch = h;
                break;
            case YCbCrSubsampleRatio.Ratio420:
                cw = (r.Max.X + 1) / 2 - r.Min.X / 2;
                ch = (r.Max.Y + 1) / 2 - r.Min.Y / 2;
                break;
            case YCbCrSubsampleRatio.Ratio440:
                cw = w;
                ch = (r.Max.Y + 1) / 2 - r.Min.Y / 2;
                break;
            case YCbCrSubsampleRatio.Ratio411:
                cw = (r.Max.X + 3) / 4 - r.Min.X / 4;
                ch = h;
                break;
            case YCbCrSubsampleRatio.Ratio410:
                cw = (r.Max.X + 3) / 4 - r.Min.X / 4;
                ch = (r.Max.Y + 1) / 2 - r.Min.Y / 2;
                break;
            default:
                cw = w;
                ch = h;
                break;
        }
        return (w, h, cw, ch);
    }

    public static YCbCrImage NewYCbCr(Rect r, YCbCrSubsampleRatio subsampleRatio)
    {
        var (w, h, cw, ch) = YCbCrSize(r, subsampleRatio);
        int totalLength = PixelUtil.Add2NonNeg(
            PixelUtil.Mul3NonNeg(1, w, h),
            PixelUtil.Mul3NonNeg(2, cw, ch));
        if (totalLength < 0)
            throw new ArgumentException("image: NewYCbCr Rectangle has huge or negative dimensions");

        int i0 = w * h;
        int i1 = i0 + cw * ch;
        int i2 = i1 + cw * ch;
        var b = new byte[i2];
        return new YCbCrImage
        {
            Y = new Memory<byte>(b, 0, i0),
            Cb = new Memory<byte>(b, i0, cw * ch),
            Cr = new Memory<byte>(b, i1, cw * ch),
            SubsampleRatio = subsampleRatio,
            YStride = w,
            CStride = cw,
            Rect = r,
        };
    }

    public static NYCbCrAImage NewNYCbCrA(Rect r, YCbCrSubsampleRatio subsampleRatio)
    {
        var (w, h, cw, ch) = YCbCrSize(r, subsampleRatio);
        int totalLength = PixelUtil.Add2NonNeg(
            PixelUtil.Mul3NonNeg(2, w, h),
            PixelUtil.Mul3NonNeg(2, cw, ch));
        if (totalLength < 0)
            throw new ArgumentException("image: NewNYCbCrA Rectangle has huge or negative dimension");

        int i0 = w * h;
        int i1 = i0 + cw * ch;
        int i2 = i1 + cw * ch;
        int i3 = 2 * w * h + 2 * cw * ch;
        var b = new byte[i3];
        return new NYCbCrAImage
        {
            YCbCrImage = new YCbCrImage
            {
                Y = new Memory<byte>(b, 0, i0),
                Cb = new Memory<byte>(b, i0, cw * ch),
                Cr = new Memory<byte>(b, i1, cw * ch),
                SubsampleRatio = subsampleRatio,
                YStride = w,
                CStride = cw,
                Rect = r,
            },
            A = new Memory<byte>(b, i2, w * h),
            AStride = w,
        };
    }
}
