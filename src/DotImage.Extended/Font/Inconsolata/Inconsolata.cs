using DotImage;
using DotImage.Color;
using System.Text;

namespace DotImage.Extended.Font.Inconsolata;

/// <summary>Provides pre-rendered bitmap versions of the Inconsolata
/// font family. Ported from golang.org/x/image/font/inconsolata.</summary>
public static class Inconsolata
{
    private static readonly Basic.Range[] Ranges =
    [
        new Basic.Range(new Rune(0X0020), new Rune(0X007F), 0),
        new Basic.Range(new Rune(0X008E), new Rune(0X008F), 95),
        new Basic.Range(new Rune(0X009E), new Rune(0X009F), 96),
        new Basic.Range(new Rune(0X00A0), new Rune(0X0100), 97),
        new Basic.Range(new Rune(0X0102), new Rune(0X0108), 193),
        new Basic.Range(new Rune(0X010C), new Rune(0X0112), 199),
        new Basic.Range(new Rune(0X0118), new Rune(0X011C), 205),
        new Basic.Range(new Rune(0X011E), new Rune(0X0120), 209),
        new Basic.Range(new Rune(0X0130), new Rune(0X0132), 211),
        new Basic.Range(new Rune(0X0138), new Rune(0X013B), 213),
        new Basic.Range(new Rune(0X013D), new Rune(0X013F), 216),
        new Basic.Range(new Rune(0X0141), new Rune(0X0145), 218),
        new Basic.Range(new Rune(0X0147), new Rune(0X0149), 222),
        new Basic.Range(new Rune(0X014A), new Rune(0X014C), 224),
        new Basic.Range(new Rune(0X014D), new Rune(0X014E), 226),
        new Basic.Range(new Rune(0X0150), new Rune(0X0156), 227),
        new Basic.Range(new Rune(0X0158), new Rune(0X015C), 233),
        new Basic.Range(new Rune(0X015E), new Rune(0X0166), 237),
        new Basic.Range(new Rune(0X016E), new Rune(0X0172), 245),
        new Basic.Range(new Rune(0X0178), new Rune(0X017F), 249),
        new Basic.Range(new Rune(0X0192), new Rune(0X0193), 256),
        new Basic.Range(new Rune(0X0237), new Rune(0X0238), 257),
        new Basic.Range(new Rune(0X02BC), new Rune(0X02BD), 258),
        new Basic.Range(new Rune(0X02C6), new Rune(0X02C8), 259),
        new Basic.Range(new Rune(0X02C9), new Rune(0X02CA), 261),
        new Basic.Range(new Rune(0X02CB), new Rune(0X02CC), 262),
        new Basic.Range(new Rune(0X02D8), new Rune(0X02DE), 263),
        new Basic.Range(new Rune(0X2018), new Rune(0X201B), 269),
        new Basic.Range(new Rune(0X201C), new Rune(0X201F), 272),
        new Basic.Range(new Rune(0X2020), new Rune(0X2023), 275),
        new Basic.Range(new Rune(0X2026), new Rune(0X2027), 278),
        new Basic.Range(new Rune(0X2039), new Rune(0X203B), 279),
        new Basic.Range(new Rune(0X2044), new Rune(0X2045), 281),
        new Basic.Range(new Rune(0X2074), new Rune(0X2075), 282),
        new Basic.Range(new Rune(0X20AC), new Rune(0X20AD), 283),
        new Basic.Range(new Rune(0X2122), new Rune(0X2123), 284),
        new Basic.Range(new Rune(0X2191), new Rune(0X2192), 285),
        new Basic.Range(new Rune(0X2193), new Rune(0X2194), 286),
        new Basic.Range(new Rune(0X2212), new Rune(0X2213), 287),
        new Basic.Range(new Rune(0X2423), new Rune(0X2424), 288),
    ];

    private static readonly AlphaImage RegularMask = new()
    {
        Pix = Regular8x16Data.Data,
        Stride = 9,
        Rect = new Rect(default, new Point(9, 289 * 17)),
    };

    /// <summary>A regular weight, 8x16 font face.</summary>
    public static readonly Basic.Face Regular8x16 = new()
    {
        Advance = 8,
        Width = 9,
        Height = 16,
        Ascent = 14,
        Descent = 3,
        Left = 0,
        Mask = RegularMask,
        Ranges = Ranges,
    };

    private static readonly AlphaImage BoldMask = new()
    {
        Pix = Bold8x16Data.Data,
        Stride = 10,
        Rect = new Rect(default, new Point(10, 289 * 17)),
    };

    /// <summary>A bold weight, 8x16 font face.</summary>
    public static readonly Basic.Face Bold8x16 = new()
    {
        Advance = 8,
        Width = 10,
        Height = 16,
        Ascent = 14,
        Descent = 3,
        Left = -1,
        Mask = BoldMask,
        Ranges = Ranges,
    };
}