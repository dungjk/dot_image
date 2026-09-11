// Generated from golang.org/x/image/colornames/table.go. DO NOT EDIT.
//
// Ported from golang.org/x/image/colornames

using DotImage.Color;

namespace DotImage.Extended.Colornames;

/// <summary>
/// Named colors as defined in the SVG 1.1 spec.
/// See https://www.w3.org/TR/SVG11/types.html#ColorKeywords
/// </summary>
public static class ColorNames
{
    /// <summary>Names contains the SVG 1.1 color names, in alphabetical order.</summary>
    public static readonly string[] Names =
    [
        "aliceblue",
        "antiquewhite",
        "aqua",
        "aquamarine",
        "azure",
        "beige",
        "bisque",
        "black",
        "blanchedalmond",
        "blue",
        "blueviolet",
        "brown",
        "burlywood",
        "cadetblue",
        "chartreuse",
        "chocolate",
        "coral",
        "cornflowerblue",
        "cornsilk",
        "crimson",
        "cyan",
        "darkblue",
        "darkcyan",
        "darkgoldenrod",
        "darkgray",
        "darkgreen",
        "darkgrey",
        "darkkhaki",
        "darkmagenta",
        "darkolivegreen",
        "darkorange",
        "darkorchid",
        "darkred",
        "darksalmon",
        "darkseagreen",
        "darkslateblue",
        "darkslategray",
        "darkslategrey",
        "darkturquoise",
        "darkviolet",
        "deeppink",
        "deepskyblue",
        "dimgray",
        "dimgrey",
        "dodgerblue",
        "firebrick",
        "floralwhite",
        "forestgreen",
        "fuchsia",
        "gainsboro",
        "ghostwhite",
        "gold",
        "goldenrod",
        "gray",
        "green",
        "greenyellow",
        "grey",
        "honeydew",
        "hotpink",
        "indianred",
        "indigo",
        "ivory",
        "khaki",
        "lavender",
        "lavenderblush",
        "lawngreen",
        "lemonchiffon",
        "lightblue",
        "lightcoral",
        "lightcyan",
        "lightgoldenrodyellow",
        "lightgray",
        "lightgreen",
        "lightgrey",
        "lightpink",
        "lightsalmon",
        "lightseagreen",
        "lightskyblue",
        "lightslategray",
        "lightslategrey",
        "lightsteelblue",
        "lightyellow",
        "lime",
        "limegreen",
        "linen",
        "magenta",
        "maroon",
        "mediumaquamarine",
        "mediumblue",
        "mediumorchid",
        "mediumpurple",
        "mediumseagreen",
        "mediumslateblue",
        "mediumspringgreen",
        "mediumturquoise",
        "mediumvioletred",
        "midnightblue",
        "mintcream",
        "mistyrose",
        "moccasin",
        "navajowhite",
        "navy",
        "oldlace",
        "olive",
        "olivedrab",
        "orange",
        "orangered",
        "orchid",
        "palegoldenrod",
        "palegreen",
        "paleturquoise",
        "palevioletred",
        "papayawhip",
        "peachpuff",
        "peru",
        "pink",
        "plum",
        "powderblue",
        "purple",
        "red",
        "rosybrown",
        "royalblue",
        "saddlebrown",
        "salmon",
        "sandybrown",
        "seagreen",
        "seashell",
        "sienna",
        "silver",
        "skyblue",
        "slateblue",
        "slategray",
        "slategrey",
        "snow",
        "springgreen",
        "steelblue",
        "tan",
        "teal",
        "thistle",
        "tomato",
        "turquoise",
        "violet",
        "wheat",
        "white",
        "whitesmoke",
        "yellow",
        "yellowgreen",
    ];

    /// <summary>Map maps SVG 1.1 color names to their RGBA colors.</summary>
    public static readonly Dictionary<string, Rgba> Map = new(StringComparer.Ordinal)
    {
        ["aliceblue"] = new Rgba(0xf0, 0xf8, 0xff, 0xff), // rgb(240, 248, 255)
        ["antiquewhite"] = new Rgba(0xfa, 0xeb, 0xd7, 0xff), // rgb(250, 235, 215)
        ["aqua"] = new Rgba(0x0, 0xff, 0xff, 0xff), // rgb(0, 255, 255)
        ["aquamarine"] = new Rgba(0x7f, 0xff, 0xd4, 0xff), // rgb(127, 255, 212)
        ["azure"] = new Rgba(0xf0, 0xff, 0xff, 0xff), // rgb(240, 255, 255)
        ["beige"] = new Rgba(0xf5, 0xf5, 0xdc, 0xff), // rgb(245, 245, 220)
        ["bisque"] = new Rgba(0xff, 0xe4, 0xc4, 0xff), // rgb(255, 228, 196)
        ["black"] = new Rgba(0x0, 0x0, 0x0, 0xff), // rgb(0, 0, 0)
        ["blanchedalmond"] = new Rgba(0xff, 0xeb, 0xcd, 0xff), // rgb(255, 235, 205)
        ["blue"] = new Rgba(0x0, 0x0, 0xff, 0xff), // rgb(0, 0, 255)
        ["blueviolet"] = new Rgba(0x8a, 0x2b, 0xe2, 0xff), // rgb(138, 43, 226)
        ["brown"] = new Rgba(0xa5, 0x2a, 0x2a, 0xff), // rgb(165, 42, 42)
        ["burlywood"] = new Rgba(0xde, 0xb8, 0x87, 0xff), // rgb(222, 184, 135)
        ["cadetblue"] = new Rgba(0x5f, 0x9e, 0xa0, 0xff), // rgb(95, 158, 160)
        ["chartreuse"] = new Rgba(0x7f, 0xff, 0x0, 0xff), // rgb(127, 255, 0)
        ["chocolate"] = new Rgba(0xd2, 0x69, 0x1e, 0xff), // rgb(210, 105, 30)
        ["coral"] = new Rgba(0xff, 0x7f, 0x50, 0xff), // rgb(255, 127, 80)
        ["cornflowerblue"] = new Rgba(0x64, 0x95, 0xed, 0xff), // rgb(100, 149, 237)
        ["cornsilk"] = new Rgba(0xff, 0xf8, 0xdc, 0xff), // rgb(255, 248, 220)
        ["crimson"] = new Rgba(0xdc, 0x14, 0x3c, 0xff), // rgb(220, 20, 60)
        ["cyan"] = new Rgba(0x0, 0xff, 0xff, 0xff), // rgb(0, 255, 255)
        ["darkblue"] = new Rgba(0x0, 0x0, 0x8b, 0xff), // rgb(0, 0, 139)
        ["darkcyan"] = new Rgba(0x0, 0x8b, 0x8b, 0xff), // rgb(0, 139, 139)
        ["darkgoldenrod"] = new Rgba(0xb8, 0x86, 0xb, 0xff), // rgb(184, 134, 11)
        ["darkgray"] = new Rgba(0xa9, 0xa9, 0xa9, 0xff), // rgb(169, 169, 169)
        ["darkgreen"] = new Rgba(0x0, 0x64, 0x0, 0xff), // rgb(0, 100, 0)
        ["darkgrey"] = new Rgba(0xa9, 0xa9, 0xa9, 0xff), // rgb(169, 169, 169)
        ["darkkhaki"] = new Rgba(0xbd, 0xb7, 0x6b, 0xff), // rgb(189, 183, 107)
        ["darkmagenta"] = new Rgba(0x8b, 0x0, 0x8b, 0xff), // rgb(139, 0, 139)
        ["darkolivegreen"] = new Rgba(0x55, 0x6b, 0x2f, 0xff), // rgb(85, 107, 47)
        ["darkorange"] = new Rgba(0xff, 0x8c, 0x0, 0xff), // rgb(255, 140, 0)
        ["darkorchid"] = new Rgba(0x99, 0x32, 0xcc, 0xff), // rgb(153, 50, 204)
        ["darkred"] = new Rgba(0x8b, 0x0, 0x0, 0xff), // rgb(139, 0, 0)
        ["darksalmon"] = new Rgba(0xe9, 0x96, 0x7a, 0xff), // rgb(233, 150, 122)
        ["darkseagreen"] = new Rgba(0x8f, 0xbc, 0x8f, 0xff), // rgb(143, 188, 143)
        ["darkslateblue"] = new Rgba(0x48, 0x3d, 0x8b, 0xff), // rgb(72, 61, 139)
        ["darkslategray"] = new Rgba(0x2f, 0x4f, 0x4f, 0xff), // rgb(47, 79, 79)
        ["darkslategrey"] = new Rgba(0x2f, 0x4f, 0x4f, 0xff), // rgb(47, 79, 79)
        ["darkturquoise"] = new Rgba(0x0, 0xce, 0xd1, 0xff), // rgb(0, 206, 209)
        ["darkviolet"] = new Rgba(0x94, 0x0, 0xd3, 0xff), // rgb(148, 0, 211)
        ["deeppink"] = new Rgba(0xff, 0x14, 0x93, 0xff), // rgb(255, 20, 147)
        ["deepskyblue"] = new Rgba(0x0, 0xbf, 0xff, 0xff), // rgb(0, 191, 255)
        ["dimgray"] = new Rgba(0x69, 0x69, 0x69, 0xff), // rgb(105, 105, 105)
        ["dimgrey"] = new Rgba(0x69, 0x69, 0x69, 0xff), // rgb(105, 105, 105)
        ["dodgerblue"] = new Rgba(0x1e, 0x90, 0xff, 0xff), // rgb(30, 144, 255)
        ["firebrick"] = new Rgba(0xb2, 0x22, 0x22, 0xff), // rgb(178, 34, 34)
        ["floralwhite"] = new Rgba(0xff, 0xfa, 0xf0, 0xff), // rgb(255, 250, 240)
        ["forestgreen"] = new Rgba(0x22, 0x8b, 0x22, 0xff), // rgb(34, 139, 34)
        ["fuchsia"] = new Rgba(0xff, 0x0, 0xff, 0xff), // rgb(255, 0, 255)
        ["gainsboro"] = new Rgba(0xdc, 0xdc, 0xdc, 0xff), // rgb(220, 220, 220)
        ["ghostwhite"] = new Rgba(0xf8, 0xf8, 0xff, 0xff), // rgb(248, 248, 255)
        ["gold"] = new Rgba(0xff, 0xd7, 0x0, 0xff), // rgb(255, 215, 0)
        ["goldenrod"] = new Rgba(0xda, 0xa5, 0x20, 0xff), // rgb(218, 165, 32)
        ["gray"] = new Rgba(0x80, 0x80, 0x80, 0xff), // rgb(128, 128, 128)
        ["green"] = new Rgba(0x0, 0x80, 0x0, 0xff), // rgb(0, 128, 0)
        ["greenyellow"] = new Rgba(0xad, 0xff, 0x2f, 0xff), // rgb(173, 255, 47)
        ["grey"] = new Rgba(0x80, 0x80, 0x80, 0xff), // rgb(128, 128, 128)
        ["honeydew"] = new Rgba(0xf0, 0xff, 0xf0, 0xff), // rgb(240, 255, 240)
        ["hotpink"] = new Rgba(0xff, 0x69, 0xb4, 0xff), // rgb(255, 105, 180)
        ["indianred"] = new Rgba(0xcd, 0x5c, 0x5c, 0xff), // rgb(205, 92, 92)
        ["indigo"] = new Rgba(0x4b, 0x0, 0x82, 0xff), // rgb(75, 0, 130)
        ["ivory"] = new Rgba(0xff, 0xff, 0xf0, 0xff), // rgb(255, 255, 240)
        ["khaki"] = new Rgba(0xf0, 0xe6, 0x8c, 0xff), // rgb(240, 230, 140)
        ["lavender"] = new Rgba(0xe6, 0xe6, 0xfa, 0xff), // rgb(230, 230, 250)
        ["lavenderblush"] = new Rgba(0xff, 0xf0, 0xf5, 0xff), // rgb(255, 240, 245)
        ["lawngreen"] = new Rgba(0x7c, 0xfc, 0x0, 0xff), // rgb(124, 252, 0)
        ["lemonchiffon"] = new Rgba(0xff, 0xfa, 0xcd, 0xff), // rgb(255, 250, 205)
        ["lightblue"] = new Rgba(0xad, 0xd8, 0xe6, 0xff), // rgb(173, 216, 230)
        ["lightcoral"] = new Rgba(0xf0, 0x80, 0x80, 0xff), // rgb(240, 128, 128)
        ["lightcyan"] = new Rgba(0xe0, 0xff, 0xff, 0xff), // rgb(224, 255, 255)
        ["lightgoldenrodyellow"] = new Rgba(0xfa, 0xfa, 0xd2, 0xff), // rgb(250, 250, 210)
        ["lightgray"] = new Rgba(0xd3, 0xd3, 0xd3, 0xff), // rgb(211, 211, 211)
        ["lightgreen"] = new Rgba(0x90, 0xee, 0x90, 0xff), // rgb(144, 238, 144)
        ["lightgrey"] = new Rgba(0xd3, 0xd3, 0xd3, 0xff), // rgb(211, 211, 211)
        ["lightpink"] = new Rgba(0xff, 0xb6, 0xc1, 0xff), // rgb(255, 182, 193)
        ["lightsalmon"] = new Rgba(0xff, 0xa0, 0x7a, 0xff), // rgb(255, 160, 122)
        ["lightseagreen"] = new Rgba(0x20, 0xb2, 0xaa, 0xff), // rgb(32, 178, 170)
        ["lightskyblue"] = new Rgba(0x87, 0xce, 0xfa, 0xff), // rgb(135, 206, 250)
        ["lightslategray"] = new Rgba(0x77, 0x88, 0x99, 0xff), // rgb(119, 136, 153)
        ["lightslategrey"] = new Rgba(0x77, 0x88, 0x99, 0xff), // rgb(119, 136, 153)
        ["lightsteelblue"] = new Rgba(0xb0, 0xc4, 0xde, 0xff), // rgb(176, 196, 222)
        ["lightyellow"] = new Rgba(0xff, 0xff, 0xe0, 0xff), // rgb(255, 255, 224)
        ["lime"] = new Rgba(0x0, 0xff, 0x0, 0xff), // rgb(0, 255, 0)
        ["limegreen"] = new Rgba(0x32, 0xcd, 0x32, 0xff), // rgb(50, 205, 50)
        ["linen"] = new Rgba(0xfa, 0xf0, 0xe6, 0xff), // rgb(250, 240, 230)
        ["magenta"] = new Rgba(0xff, 0x0, 0xff, 0xff), // rgb(255, 0, 255)
        ["maroon"] = new Rgba(0x80, 0x0, 0x0, 0xff), // rgb(128, 0, 0)
        ["mediumaquamarine"] = new Rgba(0x66, 0xcd, 0xaa, 0xff), // rgb(102, 205, 170)
        ["mediumblue"] = new Rgba(0x0, 0x0, 0xcd, 0xff), // rgb(0, 0, 205)
        ["mediumorchid"] = new Rgba(0xba, 0x55, 0xd3, 0xff), // rgb(186, 85, 211)
        ["mediumpurple"] = new Rgba(0x93, 0x70, 0xdb, 0xff), // rgb(147, 112, 219)
        ["mediumseagreen"] = new Rgba(0x3c, 0xb3, 0x71, 0xff), // rgb(60, 179, 113)
        ["mediumslateblue"] = new Rgba(0x7b, 0x68, 0xee, 0xff), // rgb(123, 104, 238)
        ["mediumspringgreen"] = new Rgba(0x0, 0xfa, 0x9a, 0xff), // rgb(0, 250, 154)
        ["mediumturquoise"] = new Rgba(0x48, 0xd1, 0xcc, 0xff), // rgb(72, 209, 204)
        ["mediumvioletred"] = new Rgba(0xc7, 0x15, 0x85, 0xff), // rgb(199, 21, 133)
        ["midnightblue"] = new Rgba(0x19, 0x19, 0x70, 0xff), // rgb(25, 25, 112)
        ["mintcream"] = new Rgba(0xf5, 0xff, 0xfa, 0xff), // rgb(245, 255, 250)
        ["mistyrose"] = new Rgba(0xff, 0xe4, 0xe1, 0xff), // rgb(255, 228, 225)
        ["moccasin"] = new Rgba(0xff, 0xe4, 0xb5, 0xff), // rgb(255, 228, 181)
        ["navajowhite"] = new Rgba(0xff, 0xde, 0xad, 0xff), // rgb(255, 222, 173)
        ["navy"] = new Rgba(0x0, 0x0, 0x80, 0xff), // rgb(0, 0, 128)
        ["oldlace"] = new Rgba(0xfd, 0xf5, 0xe6, 0xff), // rgb(253, 245, 230)
        ["olive"] = new Rgba(0x80, 0x80, 0x0, 0xff), // rgb(128, 128, 0)
        ["olivedrab"] = new Rgba(0x6b, 0x8e, 0x23, 0xff), // rgb(107, 142, 35)
        ["orange"] = new Rgba(0xff, 0xa5, 0x0, 0xff), // rgb(255, 165, 0)
        ["orangered"] = new Rgba(0xff, 0x45, 0x0, 0xff), // rgb(255, 69, 0)
        ["orchid"] = new Rgba(0xda, 0x70, 0xd6, 0xff), // rgb(218, 112, 214)
        ["palegoldenrod"] = new Rgba(0xee, 0xe8, 0xaa, 0xff), // rgb(238, 232, 170)
        ["palegreen"] = new Rgba(0x98, 0xfb, 0x98, 0xff), // rgb(152, 251, 152)
        ["paleturquoise"] = new Rgba(0xaf, 0xee, 0xee, 0xff), // rgb(175, 238, 238)
        ["palevioletred"] = new Rgba(0xdb, 0x70, 0x93, 0xff), // rgb(219, 112, 147)
        ["papayawhip"] = new Rgba(0xff, 0xef, 0xd5, 0xff), // rgb(255, 239, 213)
        ["peachpuff"] = new Rgba(0xff, 0xda, 0xb9, 0xff), // rgb(255, 218, 185)
        ["peru"] = new Rgba(0xcd, 0x85, 0x3f, 0xff), // rgb(205, 133, 63)
        ["pink"] = new Rgba(0xff, 0xc0, 0xcb, 0xff), // rgb(255, 192, 203)
        ["plum"] = new Rgba(0xdd, 0xa0, 0xdd, 0xff), // rgb(221, 160, 221)
        ["powderblue"] = new Rgba(0xb0, 0xe0, 0xe6, 0xff), // rgb(176, 224, 230)
        ["purple"] = new Rgba(0x80, 0x0, 0x80, 0xff), // rgb(128, 0, 128)
        ["red"] = new Rgba(0xff, 0x0, 0x0, 0xff), // rgb(255, 0, 0)
        ["rosybrown"] = new Rgba(0xbc, 0x8f, 0x8f, 0xff), // rgb(188, 143, 143)
        ["royalblue"] = new Rgba(0x41, 0x69, 0xe1, 0xff), // rgb(65, 105, 225)
        ["saddlebrown"] = new Rgba(0x8b, 0x45, 0x13, 0xff), // rgb(139, 69, 19)
        ["salmon"] = new Rgba(0xfa, 0x80, 0x72, 0xff), // rgb(250, 128, 114)
        ["sandybrown"] = new Rgba(0xf4, 0xa4, 0x60, 0xff), // rgb(244, 164, 96)
        ["seagreen"] = new Rgba(0x2e, 0x8b, 0x57, 0xff), // rgb(46, 139, 87)
        ["seashell"] = new Rgba(0xff, 0xf5, 0xee, 0xff), // rgb(255, 245, 238)
        ["sienna"] = new Rgba(0xa0, 0x52, 0x2d, 0xff), // rgb(160, 82, 45)
        ["silver"] = new Rgba(0xc0, 0xc0, 0xc0, 0xff), // rgb(192, 192, 192)
        ["skyblue"] = new Rgba(0x87, 0xce, 0xeb, 0xff), // rgb(135, 206, 235)
        ["slateblue"] = new Rgba(0x6a, 0x5a, 0xcd, 0xff), // rgb(106, 90, 205)
        ["slategray"] = new Rgba(0x70, 0x80, 0x90, 0xff), // rgb(112, 128, 144)
        ["slategrey"] = new Rgba(0x70, 0x80, 0x90, 0xff), // rgb(112, 128, 144)
        ["snow"] = new Rgba(0xff, 0xfa, 0xfa, 0xff), // rgb(255, 250, 250)
        ["springgreen"] = new Rgba(0x0, 0xff, 0x7f, 0xff), // rgb(0, 255, 127)
        ["steelblue"] = new Rgba(0x46, 0x82, 0xb4, 0xff), // rgb(70, 130, 180)
        ["tan"] = new Rgba(0xd2, 0xb4, 0x8c, 0xff), // rgb(210, 180, 140)
        ["teal"] = new Rgba(0x0, 0x80, 0x80, 0xff), // rgb(0, 128, 128)
        ["thistle"] = new Rgba(0xd8, 0xbf, 0xd8, 0xff), // rgb(216, 191, 216)
        ["tomato"] = new Rgba(0xff, 0x63, 0x47, 0xff), // rgb(255, 99, 71)
        ["turquoise"] = new Rgba(0x40, 0xe0, 0xd0, 0xff), // rgb(64, 224, 208)
        ["violet"] = new Rgba(0xee, 0x82, 0xee, 0xff), // rgb(238, 130, 238)
        ["wheat"] = new Rgba(0xf5, 0xde, 0xb3, 0xff), // rgb(245, 222, 179)
        ["white"] = new Rgba(0xff, 0xff, 0xff, 0xff), // rgb(255, 255, 255)
        ["whitesmoke"] = new Rgba(0xf5, 0xf5, 0xf5, 0xff), // rgb(245, 245, 245)
        ["yellow"] = new Rgba(0xff, 0xff, 0x0, 0xff), // rgb(255, 255, 0)
        ["yellowgreen"] = new Rgba(0x9a, 0xcd, 0x32, 0xff), // rgb(154, 205, 50)
    };

    public static readonly Rgba Aliceblue = new(0xf0, 0xf8, 0xff, 0xff);
    public static readonly Rgba Antiquewhite = new(0xfa, 0xeb, 0xd7, 0xff);
    public static readonly Rgba Aqua = new(0x0, 0xff, 0xff, 0xff);
    public static readonly Rgba Aquamarine = new(0x7f, 0xff, 0xd4, 0xff);
    public static readonly Rgba Azure = new(0xf0, 0xff, 0xff, 0xff);
    public static readonly Rgba Beige = new(0xf5, 0xf5, 0xdc, 0xff);
    public static readonly Rgba Bisque = new(0xff, 0xe4, 0xc4, 0xff);
    public static readonly Rgba Black = new(0x0, 0x0, 0x0, 0xff);
    public static readonly Rgba Blanchedalmond = new(0xff, 0xeb, 0xcd, 0xff);
    public static readonly Rgba Blue = new(0x0, 0x0, 0xff, 0xff);
    public static readonly Rgba Blueviolet = new(0x8a, 0x2b, 0xe2, 0xff);
    public static readonly Rgba Brown = new(0xa5, 0x2a, 0x2a, 0xff);
    public static readonly Rgba Burlywood = new(0xde, 0xb8, 0x87, 0xff);
    public static readonly Rgba Cadetblue = new(0x5f, 0x9e, 0xa0, 0xff);
    public static readonly Rgba Chartreuse = new(0x7f, 0xff, 0x0, 0xff);
    public static readonly Rgba Chocolate = new(0xd2, 0x69, 0x1e, 0xff);
    public static readonly Rgba Coral = new(0xff, 0x7f, 0x50, 0xff);
    public static readonly Rgba Cornflowerblue = new(0x64, 0x95, 0xed, 0xff);
    public static readonly Rgba Cornsilk = new(0xff, 0xf8, 0xdc, 0xff);
    public static readonly Rgba Crimson = new(0xdc, 0x14, 0x3c, 0xff);
    public static readonly Rgba Cyan = new(0x0, 0xff, 0xff, 0xff);
    public static readonly Rgba Darkblue = new(0x0, 0x0, 0x8b, 0xff);
    public static readonly Rgba Darkcyan = new(0x0, 0x8b, 0x8b, 0xff);
    public static readonly Rgba Darkgoldenrod = new(0xb8, 0x86, 0xb, 0xff);
    public static readonly Rgba Darkgray = new(0xa9, 0xa9, 0xa9, 0xff);
    public static readonly Rgba Darkgreen = new(0x0, 0x64, 0x0, 0xff);
    public static readonly Rgba Darkgrey = new(0xa9, 0xa9, 0xa9, 0xff);
    public static readonly Rgba Darkkhaki = new(0xbd, 0xb7, 0x6b, 0xff);
    public static readonly Rgba Darkmagenta = new(0x8b, 0x0, 0x8b, 0xff);
    public static readonly Rgba Darkolivegreen = new(0x55, 0x6b, 0x2f, 0xff);
    public static readonly Rgba Darkorange = new(0xff, 0x8c, 0x0, 0xff);
    public static readonly Rgba Darkorchid = new(0x99, 0x32, 0xcc, 0xff);
    public static readonly Rgba Darkred = new(0x8b, 0x0, 0x0, 0xff);
    public static readonly Rgba Darksalmon = new(0xe9, 0x96, 0x7a, 0xff);
    public static readonly Rgba Darkseagreen = new(0x8f, 0xbc, 0x8f, 0xff);
    public static readonly Rgba Darkslateblue = new(0x48, 0x3d, 0x8b, 0xff);
    public static readonly Rgba Darkslategray = new(0x2f, 0x4f, 0x4f, 0xff);
    public static readonly Rgba Darkslategrey = new(0x2f, 0x4f, 0x4f, 0xff);
    public static readonly Rgba Darkturquoise = new(0x0, 0xce, 0xd1, 0xff);
    public static readonly Rgba Darkviolet = new(0x94, 0x0, 0xd3, 0xff);
    public static readonly Rgba Deeppink = new(0xff, 0x14, 0x93, 0xff);
    public static readonly Rgba Deepskyblue = new(0x0, 0xbf, 0xff, 0xff);
    public static readonly Rgba Dimgray = new(0x69, 0x69, 0x69, 0xff);
    public static readonly Rgba Dimgrey = new(0x69, 0x69, 0x69, 0xff);
    public static readonly Rgba Dodgerblue = new(0x1e, 0x90, 0xff, 0xff);
    public static readonly Rgba Firebrick = new(0xb2, 0x22, 0x22, 0xff);
    public static readonly Rgba Floralwhite = new(0xff, 0xfa, 0xf0, 0xff);
    public static readonly Rgba Forestgreen = new(0x22, 0x8b, 0x22, 0xff);
    public static readonly Rgba Fuchsia = new(0xff, 0x0, 0xff, 0xff);
    public static readonly Rgba Gainsboro = new(0xdc, 0xdc, 0xdc, 0xff);
    public static readonly Rgba Ghostwhite = new(0xf8, 0xf8, 0xff, 0xff);
    public static readonly Rgba Gold = new(0xff, 0xd7, 0x0, 0xff);
    public static readonly Rgba Goldenrod = new(0xda, 0xa5, 0x20, 0xff);
    public static readonly Rgba Gray = new(0x80, 0x80, 0x80, 0xff);
    public static readonly Rgba Green = new(0x0, 0x80, 0x0, 0xff);
    public static readonly Rgba Greenyellow = new(0xad, 0xff, 0x2f, 0xff);
    public static readonly Rgba Grey = new(0x80, 0x80, 0x80, 0xff);
    public static readonly Rgba Honeydew = new(0xf0, 0xff, 0xf0, 0xff);
    public static readonly Rgba Hotpink = new(0xff, 0x69, 0xb4, 0xff);
    public static readonly Rgba Indianred = new(0xcd, 0x5c, 0x5c, 0xff);
    public static readonly Rgba Indigo = new(0x4b, 0x0, 0x82, 0xff);
    public static readonly Rgba Ivory = new(0xff, 0xff, 0xf0, 0xff);
    public static readonly Rgba Khaki = new(0xf0, 0xe6, 0x8c, 0xff);
    public static readonly Rgba Lavender = new(0xe6, 0xe6, 0xfa, 0xff);
    public static readonly Rgba Lavenderblush = new(0xff, 0xf0, 0xf5, 0xff);
    public static readonly Rgba Lawngreen = new(0x7c, 0xfc, 0x0, 0xff);
    public static readonly Rgba Lemonchiffon = new(0xff, 0xfa, 0xcd, 0xff);
    public static readonly Rgba Lightblue = new(0xad, 0xd8, 0xe6, 0xff);
    public static readonly Rgba Lightcoral = new(0xf0, 0x80, 0x80, 0xff);
    public static readonly Rgba Lightcyan = new(0xe0, 0xff, 0xff, 0xff);
    public static readonly Rgba Lightgoldenrodyellow = new(0xfa, 0xfa, 0xd2, 0xff);
    public static readonly Rgba Lightgray = new(0xd3, 0xd3, 0xd3, 0xff);
    public static readonly Rgba Lightgreen = new(0x90, 0xee, 0x90, 0xff);
    public static readonly Rgba Lightgrey = new(0xd3, 0xd3, 0xd3, 0xff);
    public static readonly Rgba Lightpink = new(0xff, 0xb6, 0xc1, 0xff);
    public static readonly Rgba Lightsalmon = new(0xff, 0xa0, 0x7a, 0xff);
    public static readonly Rgba Lightseagreen = new(0x20, 0xb2, 0xaa, 0xff);
    public static readonly Rgba Lightskyblue = new(0x87, 0xce, 0xfa, 0xff);
    public static readonly Rgba Lightslategray = new(0x77, 0x88, 0x99, 0xff);
    public static readonly Rgba Lightslategrey = new(0x77, 0x88, 0x99, 0xff);
    public static readonly Rgba Lightsteelblue = new(0xb0, 0xc4, 0xde, 0xff);
    public static readonly Rgba Lightyellow = new(0xff, 0xff, 0xe0, 0xff);
    public static readonly Rgba Lime = new(0x0, 0xff, 0x0, 0xff);
    public static readonly Rgba Limegreen = new(0x32, 0xcd, 0x32, 0xff);
    public static readonly Rgba Linen = new(0xfa, 0xf0, 0xe6, 0xff);
    public static readonly Rgba Magenta = new(0xff, 0x0, 0xff, 0xff);
    public static readonly Rgba Maroon = new(0x80, 0x0, 0x0, 0xff);
    public static readonly Rgba Mediumaquamarine = new(0x66, 0xcd, 0xaa, 0xff);
    public static readonly Rgba Mediumblue = new(0x0, 0x0, 0xcd, 0xff);
    public static readonly Rgba Mediumorchid = new(0xba, 0x55, 0xd3, 0xff);
    public static readonly Rgba Mediumpurple = new(0x93, 0x70, 0xdb, 0xff);
    public static readonly Rgba Mediumseagreen = new(0x3c, 0xb3, 0x71, 0xff);
    public static readonly Rgba Mediumslateblue = new(0x7b, 0x68, 0xee, 0xff);
    public static readonly Rgba Mediumspringgreen = new(0x0, 0xfa, 0x9a, 0xff);
    public static readonly Rgba Mediumturquoise = new(0x48, 0xd1, 0xcc, 0xff);
    public static readonly Rgba Mediumvioletred = new(0xc7, 0x15, 0x85, 0xff);
    public static readonly Rgba Midnightblue = new(0x19, 0x19, 0x70, 0xff);
    public static readonly Rgba Mintcream = new(0xf5, 0xff, 0xfa, 0xff);
    public static readonly Rgba Mistyrose = new(0xff, 0xe4, 0xe1, 0xff);
    public static readonly Rgba Moccasin = new(0xff, 0xe4, 0xb5, 0xff);
    public static readonly Rgba Navajowhite = new(0xff, 0xde, 0xad, 0xff);
    public static readonly Rgba Navy = new(0x0, 0x0, 0x80, 0xff);
    public static readonly Rgba Oldlace = new(0xfd, 0xf5, 0xe6, 0xff);
    public static readonly Rgba Olive = new(0x80, 0x80, 0x0, 0xff);
    public static readonly Rgba Olivedrab = new(0x6b, 0x8e, 0x23, 0xff);
    public static readonly Rgba Orange = new(0xff, 0xa5, 0x0, 0xff);
    public static readonly Rgba Orangered = new(0xff, 0x45, 0x0, 0xff);
    public static readonly Rgba Orchid = new(0xda, 0x70, 0xd6, 0xff);
    public static readonly Rgba Palegoldenrod = new(0xee, 0xe8, 0xaa, 0xff);
    public static readonly Rgba Palegreen = new(0x98, 0xfb, 0x98, 0xff);
    public static readonly Rgba Paleturquoise = new(0xaf, 0xee, 0xee, 0xff);
    public static readonly Rgba Palevioletred = new(0xdb, 0x70, 0x93, 0xff);
    public static readonly Rgba Papayawhip = new(0xff, 0xef, 0xd5, 0xff);
    public static readonly Rgba Peachpuff = new(0xff, 0xda, 0xb9, 0xff);
    public static readonly Rgba Peru = new(0xcd, 0x85, 0x3f, 0xff);
    public static readonly Rgba Pink = new(0xff, 0xc0, 0xcb, 0xff);
    public static readonly Rgba Plum = new(0xdd, 0xa0, 0xdd, 0xff);
    public static readonly Rgba Powderblue = new(0xb0, 0xe0, 0xe6, 0xff);
    public static readonly Rgba Purple = new(0x80, 0x0, 0x80, 0xff);
    public static readonly Rgba Red = new(0xff, 0x0, 0x0, 0xff);
    public static readonly Rgba Rosybrown = new(0xbc, 0x8f, 0x8f, 0xff);
    public static readonly Rgba Royalblue = new(0x41, 0x69, 0xe1, 0xff);
    public static readonly Rgba Saddlebrown = new(0x8b, 0x45, 0x13, 0xff);
    public static readonly Rgba Salmon = new(0xfa, 0x80, 0x72, 0xff);
    public static readonly Rgba Sandybrown = new(0xf4, 0xa4, 0x60, 0xff);
    public static readonly Rgba Seagreen = new(0x2e, 0x8b, 0x57, 0xff);
    public static readonly Rgba Seashell = new(0xff, 0xf5, 0xee, 0xff);
    public static readonly Rgba Sienna = new(0xa0, 0x52, 0x2d, 0xff);
    public static readonly Rgba Silver = new(0xc0, 0xc0, 0xc0, 0xff);
    public static readonly Rgba Skyblue = new(0x87, 0xce, 0xeb, 0xff);
    public static readonly Rgba Slateblue = new(0x6a, 0x5a, 0xcd, 0xff);
    public static readonly Rgba Slategray = new(0x70, 0x80, 0x90, 0xff);
    public static readonly Rgba Slategrey = new(0x70, 0x80, 0x90, 0xff);
    public static readonly Rgba Snow = new(0xff, 0xfa, 0xfa, 0xff);
    public static readonly Rgba Springgreen = new(0x0, 0xff, 0x7f, 0xff);
    public static readonly Rgba Steelblue = new(0x46, 0x82, 0xb4, 0xff);
    public static readonly Rgba Tan = new(0xd2, 0xb4, 0x8c, 0xff);
    public static readonly Rgba Teal = new(0x0, 0x80, 0x80, 0xff);
    public static readonly Rgba Thistle = new(0xd8, 0xbf, 0xd8, 0xff);
    public static readonly Rgba Tomato = new(0xff, 0x63, 0x47, 0xff);
    public static readonly Rgba Turquoise = new(0x40, 0xe0, 0xd0, 0xff);
    public static readonly Rgba Violet = new(0xee, 0x82, 0xee, 0xff);
    public static readonly Rgba Wheat = new(0xf5, 0xde, 0xb3, 0xff);
    public static readonly Rgba White = new(0xff, 0xff, 0xff, 0xff);
    public static readonly Rgba Whitesmoke = new(0xf5, 0xf5, 0xf5, 0xff);
    public static readonly Rgba Yellow = new(0xff, 0xff, 0x0, 0xff);
    public static readonly Rgba Yellowgreen = new(0x9a, 0xcd, 0x32, 0xff);
}
