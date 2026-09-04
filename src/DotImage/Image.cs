// Ported from Go src/image/image.go

using DotImage.Color;

namespace DotImage;

public sealed class RgbaImage : IWritableImage, IRgba64Image, ISetRgba64Image
{
    public Memory<byte> Pix;
    public int Stride;
    public Rect Rect;

    public IColorModel ColorModel() => Models.Rgba;
    public Rect Bounds() => Rect;
    public IColor At(int x, int y) => RgbaAt(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new Rgba64(0, 0, 0, 0);
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 4);
        ushort r = s[0], g = s[1], b = s[2], a = s[3];
        return new Rgba64((ushort)((r << 8) | r), (ushort)((g << 8) | g), (ushort)((b << 8) | b), (ushort)((a << 8) | a));
    }

    public Rgba RgbaAt(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new Rgba(0, 0, 0, 0);
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 4);
        return new Rgba(s[0], s[1], s[2], s[3]);
    }

    public int PixOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * Stride + (x - this.Rect.Min.X) * 4;

    public void Set(int x, int y, IColor c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var c1 = (Rgba)Models.Rgba.Convert(c);
        var s = Pix.Span.Slice(i, 4);
        s[0] = c1.R;
        s[1] = c1.G;
        s[2] = c1.B;
        s[3] = c1.A;
    }

    public void SetRgba64(int x, int y, Rgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 4);
        s[0] = (byte)(c.R >> 8);
        s[1] = (byte)(c.G >> 8);
        s[2] = (byte)(c.B >> 8);
        s[3] = (byte)(c.A >> 8);
    }

    public void SetRgba(int x, int y, Rgba c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 4);
        s[0] = c.R;
        s[1] = c.G;
        s[2] = c.B;
        s[3] = c.A;
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty()) return new RgbaImage();
        int i = PixOffset(r.Min.X, r.Min.Y);
        return new RgbaImage { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
    }

    public bool Opaque()
    {
        if (this.Rect.Empty()) return true;
        int i0 = 3;
        int i1 = this.Rect.Dx() * 4;
        for (int y = this.Rect.Min.Y; y < this.Rect.Max.Y; y++)
        {
            for (int i = i0; i < i1; i += 4)
            {
                if (Pix.Span[i] != 0xff) return false;
            }
            i0 += Stride;
            i1 += Stride;
        }
        return true;
    }
}

public sealed class Rgba64Image : IWritableImage, IRgba64Image, ISetRgba64Image
{
    public Memory<byte> Pix;
    public int Stride;
    public Rect Rect;

    public IColorModel ColorModel() => Models.Rgba64;
    public Rect Bounds() => Rect;
    public IColor At(int x, int y) => Rgba64At(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new Rgba64(0, 0, 0, 0);
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 8);
        return new Rgba64(
            (ushort)(s[0] << 8 | s[1]),
            (ushort)(s[2] << 8 | s[3]),
            (ushort)(s[4] << 8 | s[5]),
            (ushort)(s[6] << 8 | s[7]));
    }

    public int PixOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * Stride + (x - this.Rect.Min.X) * 8;

    public void Set(int x, int y, IColor c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var c1 = (Rgba64)Models.Rgba64.Convert(c);
        var s = Pix.Span.Slice(i, 8);
        s[0] = (byte)(c1.R >> 8);
        s[1] = (byte)c1.R;
        s[2] = (byte)(c1.G >> 8);
        s[3] = (byte)c1.G;
        s[4] = (byte)(c1.B >> 8);
        s[5] = (byte)c1.B;
        s[6] = (byte)(c1.A >> 8);
        s[7] = (byte)c1.A;
    }

    public void SetRgba64(int x, int y, Rgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 8);
        s[0] = (byte)(c.R >> 8);
        s[1] = (byte)c.R;
        s[2] = (byte)(c.G >> 8);
        s[3] = (byte)c.G;
        s[4] = (byte)(c.B >> 8);
        s[5] = (byte)c.B;
        s[6] = (byte)(c.A >> 8);
        s[7] = (byte)c.A;
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty()) return new Rgba64Image();
        int i = PixOffset(r.Min.X, r.Min.Y);
        return new Rgba64Image { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
    }

    public bool Opaque()
    {
        if (this.Rect.Empty()) return true;
        int i0 = 6;
        int i1 = this.Rect.Dx() * 8;
        for (int y = this.Rect.Min.Y; y < this.Rect.Max.Y; y++)
        {
            for (int i = i0; i < i1; i += 8)
            {
                if (Pix.Span[i] != 0xff || Pix.Span[i + 1] != 0xff) return false;
            }
            i0 += Stride;
            i1 += Stride;
        }
        return true;
    }
}

public sealed class NrgbaImage : IWritableImage, IRgba64Image, ISetRgba64Image
{
    public Memory<byte> Pix;
    public int Stride;
    public Rect Rect;

    public IColorModel ColorModel() => Models.Nrgba;
    public Rect Bounds() => Rect;
    public IColor At(int x, int y) => NrgbaAt(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        var (r, g, b, a) = NrgbaAt(x, y).Rgba();
        return new Rgba64((ushort)r, (ushort)g, (ushort)b, (ushort)a);
    }

    public Nrgba NrgbaAt(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new Nrgba(0, 0, 0, 0);
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 4);
        return new Nrgba(s[0], s[1], s[2], s[3]);
    }

    public int PixOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * Stride + (x - this.Rect.Min.X) * 4;

    public void Set(int x, int y, IColor c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var c1 = (Nrgba)Models.Nrgba.Convert(c);
        var s = Pix.Span.Slice(i, 4);
        s[0] = c1.R;
        s[1] = c1.G;
        s[2] = c1.B;
        s[3] = c1.A;
    }

    public void SetRgba64(int x, int y, Rgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        uint r = c.R, g = c.G, b = c.B, a = c.A;
        if (a != 0 && a != 0xffff)
        {
            r = r * 0xffff / a;
            g = g * 0xffff / a;
            b = b * 0xffff / a;
        }
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 4);
        s[0] = (byte)(r >> 8);
        s[1] = (byte)(g >> 8);
        s[2] = (byte)(b >> 8);
        s[3] = (byte)(a >> 8);
    }

    public void SetNrgba(int x, int y, Nrgba c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 4);
        s[0] = c.R;
        s[1] = c.G;
        s[2] = c.B;
        s[3] = c.A;
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty()) return new NrgbaImage();
        int i = PixOffset(r.Min.X, r.Min.Y);
        return new NrgbaImage { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
    }

    public bool Opaque()
    {
        if (this.Rect.Empty()) return true;
        int i0 = 3;
        int i1 = this.Rect.Dx() * 4;
        for (int y = this.Rect.Min.Y; y < this.Rect.Max.Y; y++)
        {
            for (int i = i0; i < i1; i += 4)
            {
                if (Pix.Span[i] != 0xff) return false;
            }
            i0 += Stride;
            i1 += Stride;
        }
        return true;
    }
}

public sealed class Nrgba64Image : IWritableImage, IRgba64Image, ISetRgba64Image
{
    public Memory<byte> Pix;
    public int Stride;
    public Rect Rect;

    public IColorModel ColorModel() => Models.Nrgba64;
    public Rect Bounds() => Rect;
    public IColor At(int x, int y) => Nrgba64At(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        var (r, g, b, a) = Nrgba64At(x, y).Rgba();
        return new Rgba64((ushort)r, (ushort)g, (ushort)b, (ushort)a);
    }

    public Nrgba64 Nrgba64At(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new Nrgba64(0, 0, 0, 0);
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 8);
        return new Nrgba64(
            (ushort)(s[0] << 8 | s[1]),
            (ushort)(s[2] << 8 | s[3]),
            (ushort)(s[4] << 8 | s[5]),
            (ushort)(s[6] << 8 | s[7]));
    }

    public int PixOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * Stride + (x - this.Rect.Min.X) * 8;

    public void Set(int x, int y, IColor c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var c1 = (Nrgba64)Models.Nrgba64.Convert(c);
        var s = Pix.Span.Slice(i, 8);
        s[0] = (byte)(c1.R >> 8);
        s[1] = (byte)c1.R;
        s[2] = (byte)(c1.G >> 8);
        s[3] = (byte)c1.G;
        s[4] = (byte)(c1.B >> 8);
        s[5] = (byte)c1.B;
        s[6] = (byte)(c1.A >> 8);
        s[7] = (byte)c1.A;
    }

    public void SetRgba64(int x, int y, Rgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        uint r = c.R, g = c.G, b = c.B, a = c.A;
        if (a != 0 && a != 0xffff)
        {
            r = r * 0xffff / a;
            g = g * 0xffff / a;
            b = b * 0xffff / a;
        }
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 8);
        s[0] = (byte)(r >> 8);
        s[1] = (byte)r;
        s[2] = (byte)(g >> 8);
        s[3] = (byte)g;
        s[4] = (byte)(b >> 8);
        s[5] = (byte)b;
        s[6] = (byte)(a >> 8);
        s[7] = (byte)a;
    }

    public void SetNrgba64(int x, int y, Nrgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 8);
        s[0] = (byte)(c.R >> 8);
        s[1] = (byte)c.R;
        s[2] = (byte)(c.G >> 8);
        s[3] = (byte)c.G;
        s[4] = (byte)(c.B >> 8);
        s[5] = (byte)c.B;
        s[6] = (byte)(c.A >> 8);
        s[7] = (byte)c.A;
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty()) return new Nrgba64Image();
        int i = PixOffset(r.Min.X, r.Min.Y);
        return new Nrgba64Image { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
    }

    public bool Opaque()
    {
        if (this.Rect.Empty()) return true;
        int i0 = 6;
        int i1 = this.Rect.Dx() * 8;
        for (int y = this.Rect.Min.Y; y < this.Rect.Max.Y; y++)
        {
            for (int i = i0; i < i1; i += 8)
            {
                if (Pix.Span[i] != 0xff || Pix.Span[i + 1] != 0xff) return false;
            }
            i0 += Stride;
            i1 += Stride;
        }
        return true;
    }
}

public sealed class AlphaImage : IWritableImage, IRgba64Image, ISetRgba64Image
{
    public Memory<byte> Pix;
    public int Stride;
    public Rect Rect;

    public IColorModel ColorModel() => Models.Alpha;
    public Rect Bounds() => Rect;
    public IColor At(int x, int y) => AlphaAt(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        ushort a = AlphaAt(x, y).A;
        a |= (ushort)(a << 8);
        return new Rgba64(a, a, a, a);
    }

    public Alpha AlphaAt(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new Alpha(0);
        return new Alpha(Pix.Span[PixOffset(x, y)]);
    }

    public int PixOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * Stride + (x - this.Rect.Min.X);

    public void Set(int x, int y, IColor c)
    {
        if (!new Point(x, y).In(Rect)) return;
        Pix.Span[PixOffset(x, y)] = ((Alpha)Models.Alpha.Convert(c)).A;
    }

    public void SetRgba64(int x, int y, Rgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        Pix.Span[PixOffset(x, y)] = (byte)(c.A >> 8);
    }

    public void SetAlpha(int x, int y, Alpha c)
    {
        if (!new Point(x, y).In(Rect)) return;
        Pix.Span[PixOffset(x, y)] = c.A;
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty()) return new AlphaImage();
        int i = PixOffset(r.Min.X, r.Min.Y);
        return new AlphaImage { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
    }

    public bool Opaque()
    {
        if (this.Rect.Empty()) return true;
        int i0 = 0;
        int i1 = this.Rect.Dx();
        for (int y = this.Rect.Min.Y; y < this.Rect.Max.Y; y++)
        {
            for (int i = i0; i < i1; i++)
            {
                if (Pix.Span[i] != 0xff) return false;
            }
            i0 += Stride;
            i1 += Stride;
        }
        return true;
    }
}

public sealed class Alpha16Image : IWritableImage, IRgba64Image, ISetRgba64Image
{
    public Memory<byte> Pix;
    public int Stride;
    public Rect Rect;

    public IColorModel ColorModel() => Models.Alpha16;
    public Rect Bounds() => Rect;
    public IColor At(int x, int y) => Alpha16At(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        ushort a = Alpha16At(x, y).A;
        return new Rgba64(a, a, a, a);
    }

    public Alpha16 Alpha16At(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new Alpha16(0);
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 2);
        return new Alpha16((ushort)(s[0] << 8 | s[1]));
    }

    public int PixOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * Stride + (x - this.Rect.Min.X) * 2;

    public void Set(int x, int y, IColor c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var c1 = (Alpha16)Models.Alpha16.Convert(c);
        var s = Pix.Span.Slice(i, 2);
        s[0] = (byte)(c1.A >> 8);
        s[1] = (byte)c1.A;
    }

    public void SetRgba64(int x, int y, Rgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 2);
        s[0] = (byte)(c.A >> 8);
        s[1] = (byte)c.A;
    }

    public void SetAlpha16(int x, int y, Alpha16 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 2);
        s[0] = (byte)(c.A >> 8);
        s[1] = (byte)c.A;
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty()) return new Alpha16Image();
        int i = PixOffset(r.Min.X, r.Min.Y);
        return new Alpha16Image { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
    }

    public bool Opaque()
    {
        if (this.Rect.Empty()) return true;
        int i0 = 0;
        int i1 = this.Rect.Dx() * 2;
        for (int y = this.Rect.Min.Y; y < this.Rect.Max.Y; y++)
        {
            for (int i = i0; i < i1; i += 2)
            {
                if (Pix.Span[i] != 0xff || Pix.Span[i + 1] != 0xff) return false;
            }
            i0 += Stride;
            i1 += Stride;
        }
        return true;
    }
}

public sealed class GrayImage : IWritableImage, IRgba64Image, ISetRgba64Image
{
    public Memory<byte> Pix;
    public int Stride;
    public Rect Rect;

    public IColorModel ColorModel() => Models.Gray;
    public Rect Bounds() => Rect;
    public IColor At(int x, int y) => GrayAt(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        ushort gray = GrayAt(x, y).Y;
        gray |= (ushort)(gray << 8);
        return new Rgba64(gray, gray, gray, 0xffff);
    }

    public Gray GrayAt(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new Gray(0);
        return new Gray(Pix.Span[PixOffset(x, y)]);
    }

    public int PixOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * Stride + (x - this.Rect.Min.X);

    public void Set(int x, int y, IColor c)
    {
        if (!new Point(x, y).In(Rect)) return;
        Pix.Span[PixOffset(x, y)] = ((Gray)Models.Gray.Convert(c)).Y;
    }

    public void SetRgba64(int x, int y, Rgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        uint gray = (19595 * (uint)c.R + 38470 * (uint)c.G + 7471 * (uint)c.B + 1 << 15) >> 24;
        Pix.Span[PixOffset(x, y)] = (byte)gray;
    }

    public void SetGray(int x, int y, Gray c)
    {
        if (!new Point(x, y).In(Rect)) return;
        Pix.Span[PixOffset(x, y)] = c.Y;
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty()) return new GrayImage();
        int i = PixOffset(r.Min.X, r.Min.Y);
        return new GrayImage { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
    }

    public bool Opaque() => true;
}

public sealed class Gray16Image : IWritableImage, IRgba64Image, ISetRgba64Image
{
    public Memory<byte> Pix;
    public int Stride;
    public Rect Rect;

    public IColorModel ColorModel() => Models.Gray16;
    public Rect Bounds() => Rect;
    public IColor At(int x, int y) => Gray16At(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        ushort gray = Gray16At(x, y).Y;
        return new Rgba64(gray, gray, gray, 0xffff);
    }

    public Gray16 Gray16At(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new Gray16(0);
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 2);
        return new Gray16((ushort)(s[0] << 8 | s[1]));
    }

    public int PixOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * Stride + (x - this.Rect.Min.X) * 2;

    public void Set(int x, int y, IColor c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var c1 = (Gray16)Models.Gray16.Convert(c);
        var s = Pix.Span.Slice(i, 2);
        s[0] = (byte)(c1.Y >> 8);
        s[1] = (byte)c1.Y;
    }

    public void SetRgba64(int x, int y, Rgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        uint gray = (19595 * (uint)c.R + 38470 * (uint)c.G + 7471 * (uint)c.B + 1 << 15) >> 16;
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 2);
        s[0] = (byte)(gray >> 8);
        s[1] = (byte)gray;
    }

    public void SetGray16(int x, int y, Gray16 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 2);
        s[0] = (byte)(c.Y >> 8);
        s[1] = (byte)c.Y;
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty()) return new Gray16Image();
        int i = PixOffset(r.Min.X, r.Min.Y);
        return new Gray16Image { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
    }

    public bool Opaque() => true;
}

public sealed class CmykImage : IWritableImage, IRgba64Image, ISetRgba64Image
{
    public Memory<byte> Pix;
    public int Stride;
    public Rect Rect;

    public IColorModel ColorModel() => Models.Cmyk;
    public Rect Bounds() => Rect;
    public IColor At(int x, int y) => CmykAt(x, y);

    public Rgba64 Rgba64At(int x, int y)
    {
        var (r, g, b, a) = CmykAt(x, y).Rgba();
        return new Rgba64((ushort)r, (ushort)g, (ushort)b, (ushort)a);
    }

    public Cmyk CmykAt(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return new Cmyk(0, 0, 0, 0);
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 4);
        return new Cmyk(s[0], s[1], s[2], s[3]);
    }

    public int PixOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * Stride + (x - this.Rect.Min.X) * 4;

    public void Set(int x, int y, IColor c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var c1 = (Cmyk)Models.Cmyk.Convert(c);
        var s = Pix.Span.Slice(i, 4);
        s[0] = c1.C;
        s[1] = c1.M;
        s[2] = c1.Y;
        s[3] = c1.K;
    }

    public void SetRgba64(int x, int y, Rgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        var (cc, mm, yy, kk) = YCbCrUtil.RGBToCMYK((byte)(c.R >> 8), (byte)(c.G >> 8), (byte)(c.B >> 8));
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 4);
        s[0] = cc;
        s[1] = mm;
        s[2] = yy;
        s[3] = kk;
    }

    public void SetCmyk(int x, int y, Cmyk c)
    {
        if (!new Point(x, y).In(Rect)) return;
        int i = PixOffset(x, y);
        var s = Pix.Span.Slice(i, 4);
        s[0] = c.C;
        s[1] = c.M;
        s[2] = c.Y;
        s[3] = c.K;
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty()) return new CmykImage();
        int i = PixOffset(r.Min.X, r.Min.Y);
        return new CmykImage { Pix = Pix.Slice(i), Stride = Stride, Rect = r };
    }

    public bool Opaque() => true;
}

public sealed class PalettedImage : IWritableImage, IRgba64Image, IPalettedImage, ISetRgba64Image
{
    public Memory<byte> Pix;
    public int Stride;
    public Rect Rect;
    public Palette Palette = new([]);

    public IColorModel ColorModel() => Palette;
    public Rect Bounds() => Rect;

    public IColor At(int x, int y)
    {
        if (Palette.Length == 0) return Colors.Transparent;
        if (!new Point(x, y).In(Rect)) return Palette[0];
        return Palette[Pix.Span[PixOffset(x, y)]];
    }

    public Rgba64 Rgba64At(int x, int y)
    {
        if (Palette.Length == 0) return new Rgba64(0, 0, 0, 0);
        IColor c = !new Point(x, y).In(Rect)
            ? Palette[0]
            : Palette[Pix.Span[PixOffset(x, y)]];
        var (r, g, b, a) = c.Rgba();
        return new Rgba64((ushort)r, (ushort)g, (ushort)b, (ushort)a);
    }

    public int PixOffset(int x, int y) =>
        (y - this.Rect.Min.Y) * Stride + (x - this.Rect.Min.X);

    public void Set(int x, int y, IColor c)
    {
        if (!new Point(x, y).In(Rect)) return;
        Pix.Span[PixOffset(x, y)] = (byte)Palette.Index(c);
    }

    public void SetRgba64(int x, int y, Rgba64 c)
    {
        if (!new Point(x, y).In(Rect)) return;
        Pix.Span[PixOffset(x, y)] = (byte)Palette.Index(c);
    }

    public byte ColorIndexAt(int x, int y)
    {
        if (!new Point(x, y).In(Rect)) return 0;
        return Pix.Span[PixOffset(x, y)];
    }

    public void SetColorIndex(int x, int y, byte index)
    {
        if (!new Point(x, y).In(Rect)) return;
        Pix.Span[PixOffset(x, y)] = index;
    }

    public IImage SubImage(Rect r)
    {
        r = r.Intersect(Rect);
        if (r.Empty()) return new PalettedImage { Palette = Palette };
        int i = PixOffset(r.Min.X, r.Min.Y);
        return new PalettedImage
        {
            Pix = Pix.Slice(i),
            Stride = Stride,
            Rect = Rect.Intersect(r),
            Palette = Palette,
        };
    }

    public bool Opaque()
    {
        Span<bool> present = stackalloc bool[256];
        int i0 = 0;
        int i1 = this.Rect.Dx();
        for (int y = this.Rect.Min.Y; y < this.Rect.Max.Y; y++)
        {
            foreach (byte c in Pix.Span.Slice(i0, i1 - i0))
                present[c] = true;
            i0 += Stride;
            i1 += Stride;
        }
        for (int i = 0; i < Palette.Length; i++)
        {
            if (!present[i]) continue;
            var (_, _, _, a) = Palette[i].Rgba();
            if (a != 0xffff) return false;
        }
        return true;
    }
}

public static class Images
{
    public static RgbaImage NewRgba(Rect r) => new()
    {
        Pix = new byte[ImageBuffer.PixelBufferLength(4, r, "RGBA")],
        Stride = 4 * r.Dx(),
        Rect = r,
    };

    public static Rgba64Image NewRgba64(Rect r) => new()
    {
        Pix = new byte[ImageBuffer.PixelBufferLength(8, r, "RGBA64")],
        Stride = 8 * r.Dx(),
        Rect = r,
    };

    public static NrgbaImage NewNrgba(Rect r) => new()
    {
        Pix = new byte[ImageBuffer.PixelBufferLength(4, r, "NRGBA")],
        Stride = 4 * r.Dx(),
        Rect = r,
    };

    public static Nrgba64Image NewNrgba64(Rect r) => new()
    {
        Pix = new byte[ImageBuffer.PixelBufferLength(8, r, "NRGBA64")],
        Stride = 8 * r.Dx(),
        Rect = r,
    };

    public static AlphaImage NewAlpha(Rect r) => new()
    {
        Pix = new byte[ImageBuffer.PixelBufferLength(1, r, "Alpha")],
        Stride = r.Dx(),
        Rect = r,
    };

    public static Alpha16Image NewAlpha16(Rect r) => new()
    {
        Pix = new byte[ImageBuffer.PixelBufferLength(2, r, "Alpha16")],
        Stride = 2 * r.Dx(),
        Rect = r,
    };

    public static GrayImage NewGray(Rect r) => new()
    {
        Pix = new byte[ImageBuffer.PixelBufferLength(1, r, "Gray")],
        Stride = r.Dx(),
        Rect = r,
    };

    public static Gray16Image NewGray16(Rect r) => new()
    {
        Pix = new byte[ImageBuffer.PixelBufferLength(2, r, "Gray16")],
        Stride = 2 * r.Dx(),
        Rect = r,
    };

    public static CmykImage NewCmyk(Rect r) => new()
    {
        Pix = new byte[ImageBuffer.PixelBufferLength(4, r, "CMYK")],
        Stride = 4 * r.Dx(),
        Rect = r,
    };

    public static PalettedImage NewPaletted(Rect r, Palette palette) => new()
    {
        Pix = new byte[ImageBuffer.PixelBufferLength(1, r, "Paletted")],
        Stride = r.Dx(),
        Rect = r,
        Palette = palette,
    };
}
