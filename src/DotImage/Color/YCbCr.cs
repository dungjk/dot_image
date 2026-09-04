// Ported from go/src/image/color/ycbcr.go


namespace DotImage.Color;

/// <summary>
/// YCbCr conversion and color types.
/// </summary>
public static class YCbCrUtil
{
    /// <summary>
    /// Converts an RGB triple to a Y'CbCr triple.
    /// </summary>
    public static (byte Y, byte Cb, byte Cr) RGBToYCbCr(byte r, byte g, byte b)
    {
        int r1 = r;
        int g1 = g;
        int b1 = b;

        int yy = (19595 * r1 + 38470 * g1 + 7471 * b1 + (1 << 15)) >> 16;

        int cb = -11056 * r1 - 21712 * g1 + 32768 * b1 + (257 << 15);
        if (((uint)cb & 0xff000000) == 0)
        {
            cb >>= 16;
        }
        else
        {
            cb = ~(cb >> 31);
        }

        int cr = 32768 * r1 - 27440 * g1 - 5328 * b1 + (257 << 15);
        if (((uint)cr & 0xff000000) == 0)
        {
            cr >>= 16;
        }
        else
        {
            cr = ~(cr >> 31);
        }

        return ((byte)yy, (byte)cb, (byte)cr);
    }

    /// <summary>
    /// Converts a Y'CbCr triple to an RGB triple.
    /// </summary>
    public static (byte R, byte G, byte B) YCbCrToRGB(byte y, byte cb, byte cr)
    {
        int yy1 = y * 0x10101;
        int cb1 = cb - 128;
        int cr1 = cr - 128;

        int r = yy1 + 91881 * cr1;
        if (((uint)r & 0xff000000) == 0)
        {
            r >>= 16;
        }
        else
        {
            r = ~(r >> 31);
        }

        int g = yy1 - 22554 * cb1 - 46802 * cr1;
        if (((uint)g & 0xff000000) == 0)
        {
            g >>= 16;
        }
        else
        {
            g = ~(g >> 31);
        }

        int b = yy1 + 116130 * cb1;
        if (((uint)b & 0xff000000) == 0)
        {
            b >>= 16;
        }
        else
        {
            b = ~(b >> 31);
        }

        return ((byte)r, (byte)g, (byte)b);
    }

    /// <summary>
    /// Converts an RGB triple to a CMYK quadruple.
    /// </summary>
    public static (byte C, byte M, byte Y, byte K) RGBToCMYK(byte r, byte g, byte b)
    {
        uint rr = r;
        uint gg = g;
        uint bb = b;
        uint w = rr;
        if (w < gg)
        {
            w = gg;
        }

        if (w < bb)
        {
            w = bb;
        }

        if (w == 0)
        {
            return (0, 0, 0, 0xff);
        }

        byte c = (byte)((w - rr) * 0xff / w);
        byte m = (byte)((w - gg) * 0xff / w);
        byte y = (byte)((w - bb) * 0xff / w);
        return (c, m, y, (byte)(0xff - w));
    }

    /// <summary>
    /// Converts a CMYK quadruple to an RGB triple.
    /// </summary>
    public static (byte R, byte G, byte B) CMYKToRGB(byte c, byte m, byte y, byte k)
    {
        uint w = 0xffff - (uint)k * 0x101;
        uint r = (0xffff - (uint)c * 0x101) * w / 0xffff;
        uint g = (0xffff - (uint)m * 0x101) * w / 0xffff;
        uint b = (0xffff - (uint)y * 0x101) * w / 0xffff;
        return ((byte)(r >> 8), (byte)(g >> 8), (byte)(b >> 8));
    }
}

/// <summary>
/// Represents a fully opaque 24-bit Y'CbCr color, having 8 bits each for
/// one luma and two chroma components.
/// </summary>
public readonly struct YCbCr(byte y, byte cb, byte cr) : IColor
{
    public byte Y { get; } = y;
    public byte Cb { get; } = cb;
    public byte Cr { get; } = cr;

    public (uint R, uint G, uint B, uint A) Rgba()
    {
        int yy1 = Y * 0x10101;
        int cb1 = Cb - 128;
        int cr1 = Cr - 128;

        int r = yy1 + 91881 * cr1;
        if (((uint)r & 0xff000000) == 0)
        {
            r >>= 8;
        }
        else
        {
            r = ~(r >> 31) & 0xffff;
        }

        int g = yy1 - 22554 * cb1 - 46802 * cr1;
        if (((uint)g & 0xff000000) == 0)
        {
            g >>= 8;
        }
        else
        {
            g = ~(g >> 31) & 0xffff;
        }

        int b = yy1 + 116130 * cb1;
        if (((uint)b & 0xff000000) == 0)
        {
            b >>= 8;
        }
        else
        {
            b = ~(b >> 31) & 0xffff;
        }

        return ((uint)r, (uint)g, (uint)b, 0xffff);
    }
}

/// <summary>
/// Represents a non-alpha-premultiplied Y'CbCr-with-alpha color, having
/// 8 bits each for one luma, two chroma and one alpha component.
/// </summary>
public readonly struct NYCbCrA(byte y, byte cb, byte cr, byte a) : IColor
{
    public byte Y { get; } = y;
    public byte Cb { get; } = cb;
    public byte Cr { get; } = cr;
    public byte A { get; } = a;

    public NYCbCrA(YCbCr ycbcr, byte a) : this(ycbcr.Y, ycbcr.Cb, ycbcr.Cr, a)
    {
    }

    public (uint R, uint G, uint B, uint A) Rgba()
    {
        int yy1 = Y * 0x10101;
        int cb1 = Cb - 128;
        int cr1 = Cr - 128;

        int r = yy1 + 91881 * cr1;
        if (((uint)r & 0xff000000) == 0)
        {
            r >>= 8;
        }
        else
        {
            r = ~(r >> 31) & 0xffff;
        }

        int g = yy1 - 22554 * cb1 - 46802 * cr1;
        if (((uint)g & 0xff000000) == 0)
        {
            g >>= 8;
        }
        else
        {
            g = ~(g >> 31) & 0xffff;
        }

        int b = yy1 + 116130 * cb1;
        if (((uint)b & 0xff000000) == 0)
        {
            b >>= 8;
        }
        else
        {
            b = ~(b >> 31) & 0xffff;
        }

        uint alpha = (uint)A * 0x101;
        return ((uint)r * alpha / 0xffff, (uint)g * alpha / 0xffff, (uint)b * alpha / 0xffff, alpha);
    }
}

/// <summary>
/// Represents a fully opaque CMYK color, having 8 bits for each of cyan,
/// magenta, yellow and black.
/// </summary>
public readonly struct Cmyk(byte c, byte m, byte y, byte k) : IColor
{
    public byte C { get; } = c;
    public byte M { get; } = m;
    public byte Y { get; } = y;
    public byte K { get; } = k;

    public (uint R, uint G, uint B, uint A) Rgba()
    {
        uint w = 0xffff - (uint)K * 0x101;
        uint r = (0xffff - (uint)C * 0x101) * w / 0xffff;
        uint g = (0xffff - (uint)M * 0x101) * w / 0xffff;
        uint b = (0xffff - (uint)Y * 0x101) * w / 0xffff;
        return (r, g, b, 0xffff);
    }
}
