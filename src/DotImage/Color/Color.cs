// Ported from go/src/image/color/color.go


namespace DotImage.Color;

/// <summary>
/// Color can convert itself to alpha-premultiplied 16-bits per channel RGBA.
/// The conversion may be lossy.
/// </summary>
public interface IColor
{
    /// <summary>
    /// Returns the alpha-premultiplied red, green, blue and alpha values
    /// for the color. Each value ranges within [0, 0xffff], but is represented
    /// by a uint so that multiplying by a blend factor up to 0xffff will not
    /// overflow.
    /// </summary>
    (uint R, uint G, uint B, uint A) Rgba();
}

/// <summary>
/// Model can convert any <see cref="IColor"/> to one from its own color model.
/// The conversion may be lossy.
/// </summary>
public interface IColorModel
{
    IColor Convert(IColor c);
}

/// <summary>
/// Alias for <see cref="IColorModel"/> used by the image package.
/// </summary>
public interface IModel : IColorModel;

/// <summary>
/// Represents a traditional 32-bit alpha-premultiplied color, having 8
/// bits for each of red, green, blue and alpha.
/// </summary>
public readonly struct Rgba(byte r, byte g, byte b, byte a) : IColor
{
    public byte R { get; } = r;
    public byte G { get; } = g;
    public byte B { get; } = b;
    public byte A { get; } = a;

    (uint R, uint G, uint B, uint A) IColor.Rgba()
    {
        uint r = R;
        r |= r << 8;
        uint g = G;
        g |= g << 8;
        uint b = B;
        b |= b << 8;
        uint a = A;
        a |= a << 8;
        return (r, g, b, a);
    }
}

/// <summary>
/// Represents a 64-bit alpha-premultiplied color, having 16 bits for
/// each of red, green, blue and alpha.
/// </summary>
public readonly struct Rgba64(ushort r, ushort g, ushort b, ushort a) : IColor
{
    public ushort R { get; } = r;
    public ushort G { get; } = g;
    public ushort B { get; } = b;
    public ushort A { get; } = a;

    public (uint R, uint G, uint B, uint A) Rgba() => (R, G, B, A);
}

/// <summary>
/// Represents a non-alpha-premultiplied 32-bit color.
/// </summary>
public readonly struct Nrgba(byte r, byte g, byte b, byte a) : IColor
{
    public byte R { get; } = r;
    public byte G { get; } = g;
    public byte B { get; } = b;
    public byte A { get; } = a;

    public (uint R, uint G, uint B, uint A) Rgba()
    {
        uint r = R;
        r |= r << 8;
        r *= A;
        r /= 0xff;
        uint g = G;
        g |= g << 8;
        g *= A;
        g /= 0xff;
        uint b = B;
        b |= b << 8;
        b *= A;
        b /= 0xff;
        uint a = A;
        a |= a << 8;
        return (r, g, b, a);
    }
}

/// <summary>
/// Represents a non-alpha-premultiplied 64-bit color,
/// having 16 bits for each of red, green, blue and alpha.
/// </summary>
public readonly struct Nrgba64(ushort r, ushort g, ushort b, ushort a) : IColor
{
    public ushort R { get; } = r;
    public ushort G { get; } = g;
    public ushort B { get; } = b;
    public ushort A { get; } = a;

    public (uint R, uint G, uint B, uint A) Rgba()
    {
        uint r = R;
        r *= A;
        r /= 0xffff;
        uint g = G;
        g *= A;
        g /= 0xffff;
        uint b = B;
        b *= A;
        b /= 0xffff;
        uint a = A;
        return (r, g, b, a);
    }
}

/// <summary>
/// Represents an 8-bit alpha color.
/// </summary>
public readonly struct Alpha(byte a) : IColor
{
    public byte A { get; } = a;

    public (uint R, uint G, uint B, uint A) Rgba()
    {
        uint a = A;
        a |= a << 8;
        return (a, a, a, a);
    }
}

/// <summary>
/// Represents a 16-bit alpha color.
/// </summary>
public readonly struct Alpha16(ushort a) : IColor
{
    public ushort A { get; } = a;

    public (uint R, uint G, uint B, uint A) Rgba()
    {
        uint a = A;
        return (a, a, a, a);
    }
}

/// <summary>
/// Represents an 8-bit grayscale color.
/// </summary>
public readonly struct Gray(byte y) : IColor
{
    public byte Y { get; } = y;

    public (uint R, uint G, uint B, uint A) Rgba()
    {
        uint y = Y;
        y |= y << 8;
        return (y, y, y, 0xffff);
    }
}

/// <summary>
/// Represents a 16-bit grayscale color.
/// </summary>
public readonly struct Gray16(ushort y) : IColor
{
    public ushort Y { get; } = y;

    public (uint R, uint G, uint B, uint A) Rgba()
    {
        uint y = Y;
        return (y, y, y, 0xffff);
    }
}

/// <summary>
/// Returns a <see cref="IColorModel"/> that invokes f to implement the conversion.
/// </summary>
public sealed class ModelFunc(Func<IColor, IColor> f) : IModel
{
    private readonly Func<IColor, IColor> _f = f;

    public IColor Convert(IColor c) => _f(c);
}

/// <summary>
/// Models for the standard color types.
/// </summary>
public static class ColorModels
{
    public static IModel Rgba { get; } = new ModelFunc(RgbaModel);
    public static IModel Rgba64 { get; } = new ModelFunc(Rgba64Model);
    public static IModel Nrgba { get; } = new ModelFunc(NrgbaModel);
    public static IModel Nrgba64 { get; } = new ModelFunc(Nrgba64Model);
    public static IModel Alpha { get; } = new ModelFunc(AlphaModel);
    public static IModel Alpha16 { get; } = new ModelFunc(Alpha16Model);
    public static IModel Gray { get; } = new ModelFunc(GrayModel);
    public static IModel Gray16 { get; } = new ModelFunc(Gray16Model);
    public static IModel YCbCr { get; } = new ModelFunc(YCbCrModel);
    public static IModel NYCbCrA { get; } = new ModelFunc(NYCbCrAModel);
    public static IModel Cmyk { get; } = new ModelFunc(CmykModel);

    private static IColor RgbaModel(IColor c)
    {
        if (c is Rgba)
        {
            return c;
        }

        (uint r, uint g, uint b, uint a) = c.Rgba();
        return new Rgba((byte)(r >> 8), (byte)(g >> 8), (byte)(b >> 8), (byte)(a >> 8));
    }

    private static IColor Rgba64Model(IColor c)
    {
        if (c is Rgba64)
        {
            return c;
        }

        (uint r, uint g, uint b, uint a) = c.Rgba();
        return new Rgba64((ushort)r, (ushort)g, (ushort)b, (ushort)a);
    }

    private static IColor NrgbaModel(IColor c)
    {
        if (c is Nrgba)
        {
            return c;
        }

        (uint r, uint g, uint b, uint a) = c.Rgba();
        if (a == 0xffff)
        {
            return new Nrgba((byte)(r >> 8), (byte)(g >> 8), (byte)(b >> 8), 0xff);
        }

        if (a == 0)
        {
            return new Nrgba(0, 0, 0, 0);
        }

        r = r * 0xffff / a;
        g = g * 0xffff / a;
        b = b * 0xffff / a;
        return new Nrgba((byte)(r >> 8), (byte)(g >> 8), (byte)(b >> 8), (byte)(a >> 8));
    }

    private static IColor Nrgba64Model(IColor c)
    {
        if (c is Nrgba64)
        {
            return c;
        }

        (uint r, uint g, uint b, uint a) = c.Rgba();
        if (a == 0xffff)
        {
            return new Nrgba64((ushort)r, (ushort)g, (ushort)b, 0xffff);
        }

        if (a == 0)
        {
            return new Nrgba64(0, 0, 0, 0);
        }

        r = r * 0xffff / a;
        g = g * 0xffff / a;
        b = b * 0xffff / a;
        return new Nrgba64((ushort)r, (ushort)g, (ushort)b, (ushort)a);
    }

    private static IColor AlphaModel(IColor c)
    {
        if (c is Alpha)
        {
            return c;
        }

        (_, _, _, uint a) = c.Rgba();
        return new Alpha((byte)(a >> 8));
    }

    private static IColor Alpha16Model(IColor c)
    {
        if (c is Alpha16)
        {
            return c;
        }

        (_, _, _, uint a) = c.Rgba();
        return new Alpha16((ushort)a);
    }

    private static IColor GrayModel(IColor c)
    {
        if (c is Gray)
        {
            return c;
        }

        (uint r, uint g, uint b, _) = c.Rgba();
        uint y = (19595 * r + 38470 * g + 7471 * b + (1 << 15)) >> 24;
        return new Gray((byte)y);
    }

    private static IColor Gray16Model(IColor c)
    {
        if (c is Gray16)
        {
            return c;
        }

        (uint r, uint g, uint b, _) = c.Rgba();
        uint y = (19595 * r + 38470 * g + 7471 * b + (1 << 15)) >> 16;
        return new Gray16((ushort)y);
    }

    private static IColor YCbCrModel(IColor c)
    {
        if (c is YCbCr)
        {
            return c;
        }

        (uint r, uint g, uint b, _) = c.Rgba();
        (byte y, byte cb, byte cr) = YCbCrUtil.RGBToYCbCr((byte)(r >> 8), (byte)(g >> 8), (byte)(b >> 8));
        return new YCbCr(y, cb, cr);
    }

    private static IColor NYCbCrAModel(IColor c)
    {
        switch (c)
        {
            case NYCbCrA nycbcra:
                return nycbcra;
            case YCbCr ycbcr:
                return new NYCbCrA(ycbcr, 0xff);
        }

        (uint r, uint g, uint b, uint a) = c.Rgba();

        if (a != 0)
        {
            r = r * 0xffff / a;
            g = g * 0xffff / a;
            b = b * 0xffff / a;
        }

        (byte y, byte cb, byte cr) = YCbCrUtil.RGBToYCbCr((byte)(r >> 8), (byte)(g >> 8), (byte)(b >> 8));
        return new NYCbCrA(y, cb, cr, (byte)(a >> 8));
    }

    private static IColor CmykModel(IColor c)
    {
        if (c is Cmyk)
        {
            return c;
        }

        (uint r, uint g, uint b, _) = c.Rgba();
        (byte cc, byte mm, byte yy, byte kk) = YCbCrUtil.RGBToCMYK((byte)(r >> 8), (byte)(g >> 8), (byte)(b >> 8));
        return new Cmyk(cc, mm, yy, kk);
    }
}

/// <summary>
/// Alias for <see cref="ColorModels"/>.
/// </summary>
public static class Models
{
    public static IModel Rgba => ColorModels.Rgba;
    public static IModel Rgba64 => ColorModels.Rgba64;
    public static IModel Nrgba => ColorModels.Nrgba;
    public static IModel Nrgba64 => ColorModels.Nrgba64;
    public static IModel Alpha => ColorModels.Alpha;
    public static IModel Alpha16 => ColorModels.Alpha16;
    public static IModel Gray => ColorModels.Gray;
    public static IModel Gray16 => ColorModels.Gray16;
    public static IModel YCbCr => ColorModels.YCbCr;
    public static IModel NYCbCrA => ColorModels.NYCbCrA;
    public static IModel Cmyk => ColorModels.Cmyk;
}

/// <summary>
/// A palette of colors.
/// </summary>
public class Palette : IModel
{
    private readonly IColor[] _colors;

    public Palette(IColor[] colors) => _colors = colors;

    public int Length => _colors.Length;

    public IColor this[int index] => _colors[index];

    /// <summary>
    /// Returns the palette color closest to c in Euclidean R,G,B space.
    /// </summary>
    public IColor Convert(IColor c)
    {
        if (_colors.Length == 0)
        {
            return c;
        }

        return _colors[Index(c)];
    }

    /// <summary>
    /// Returns the index of the palette color closest to c in Euclidean
    /// R,G,B,A space.
    /// </summary>
    public int Index(IColor c)
    {
        (uint cr, uint cg, uint cb, uint ca) = c.Rgba();
        int ret = 0;
        uint bestSum = uint.MaxValue;
        for (int i = 0; i < _colors.Length; i++)
        {
            (uint vr, uint vg, uint vb, uint va) = _colors[i].Rgba();
            uint sum = ColorUtil.SqDiff(cr, vr) + ColorUtil.SqDiff(cg, vg) + ColorUtil.SqDiff(cb, vb) + ColorUtil.SqDiff(ca, va);
            if (sum < bestSum)
            {
                if (sum == 0)
                {
                    return i;
                }

                ret = i;
                bestSum = sum;
            }
        }

        return ret;
    }
}

/// <summary>
/// Alias for <see cref="Palette"/>.
/// </summary>
public sealed class ColorPalette : Palette
{
    public ColorPalette(IColor[] colors) : base(colors)
    {
    }
}

/// <summary>
/// Color utility functions.
/// </summary>
public static class ColorUtil
{
    /// <summary>
    /// Returns the squared-difference of x and y, shifted by 2 so that
    /// adding four of those won't overflow a uint.
    /// </summary>
    public static uint SqDiff(uint x, uint y)
    {
        uint d = x - y;
        return (d * d) >> 2;
    }
}

/// <summary>
/// Standard colors.
/// </summary>
public static class Colors
{
    public static readonly Gray16 Black = new(0);
    public static readonly Gray16 White = new(0xffff);
    public static readonly Alpha16 Transparent = new(0);
    public static readonly Alpha16 Opaque = new(0xffff);
}
