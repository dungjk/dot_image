using System;

namespace DotImage.Extended.Font.Sfnt;

/// <summary>
/// SfntFormatException is thrown by the SFNT/OpenType decoder.
/// </summary>
public sealed class SfntFormatException : Exception
{
    public SfntFormatException(string message) : base(message) { }
}

/// <summary>
/// Error message constants, ported from golang.org/x/image/font/sfnt. They match
/// the original Go package-level error strings (e.g. "sfnt: not found").
/// </summary>
public static class SfntErrors
{
    public const string ErrColoredGlyph = "sfnt: colored glyph";
    public const string ErrNotFound = "sfnt: not found";

    internal const string ErrInvalidBounds = "sfnt: invalid bounds";
    internal const string ErrInvalidCmapTable = "sfnt: invalid cmap table";
    internal const string ErrInvalidDfont = "sfnt: invalid dfont";
    internal const string ErrInvalidFont = "sfnt: invalid font";
    internal const string ErrInvalidFontCollection = "sfnt: invalid font collection";
    internal const string ErrInvalidGPOSTable = "sfnt: invalid GPOS table";
    internal const string ErrInvalidGlyphData = "sfnt: invalid glyph data";
    internal const string ErrInvalidGlyphDataLength = "sfnt: invalid glyph data length";
    internal const string ErrInvalidHeadTable = "sfnt: invalid head table";
    internal const string ErrInvalidHheaTable = "sfnt: invalid hhea table";
    internal const string ErrInvalidHmtxTable = "sfnt: invalid hmtx table";
    internal const string ErrInvalidKernTable = "sfnt: invalid kern table";
    internal const string ErrInvalidLocaTable = "sfnt: invalid loca table";
    internal const string ErrInvalidLocationData = "sfnt: invalid location data";
    internal const string ErrInvalidMaxpTable = "sfnt: invalid maxp table";
    internal const string ErrInvalidNameTable = "sfnt: invalid name table";
    internal const string ErrInvalidOS2Table = "sfnt: invalid OS/2 table";
    internal const string ErrInvalidPostTable = "sfnt: invalid post table";
    internal const string ErrInvalidSingleFont = "sfnt: invalid single font (data is a font collection)";
    internal const string ErrInvalidSourceData = "sfnt: invalid source data";
    internal const string ErrInvalidTableOffset = "sfnt: invalid table offset";
    internal const string ErrInvalidTableTagOrder = "sfnt: invalid table tag order";
    internal const string ErrInvalidUCS2String = "sfnt: invalid UCS-2 string";

    internal const string ErrUnsupportedClassDefFormat = "sfnt: unsupported class definition format";
    internal const string ErrUnsupportedCmapEncodings = "sfnt: unsupported cmap encodings";
    internal const string ErrUnsupportedCollection = "sfnt: unsupported collection";
    internal const string ErrUnsupportedCompoundGlyph = "sfnt: unsupported compound glyph";
    internal const string ErrUnsupportedCoverageFormat = "sfnt: unsupported coverage format";
    internal const string ErrUnsupportedExtensionPosFormat = "sfnt: unsupported extension positioning format";
    internal const string ErrUnsupportedGPOSTable = "sfnt: unsupported GPOS table";
    internal const string ErrUnsupportedGlyphDataLength = "sfnt: unsupported glyph data length";
    internal const string ErrUnsupportedKernTable = "sfnt: unsupported kern table";
    internal const string ErrUnsupportedNumberOfCmapSegments = "sfnt: unsupported number of cmap segments";
    internal const string ErrUnsupportedNumberOfFonts = "sfnt: unsupported number of fonts";
    internal const string ErrUnsupportedNumberOfTables = "sfnt: unsupported number of tables";
    internal const string ErrUnsupportedPlatformEncoding = "sfnt: unsupported platform encoding";
    internal const string ErrUnsupportedPostTable = "sfnt: unsupported post table";
    internal const string ErrUnsupportedTableOffsetLength = "sfnt: unsupported table offset or length";

    internal const string ErrInvalidCFFTable = "sfnt: invalid CFF table";

    internal const string ErrUnsupportedCFFFDSelectTable = "sfnt: unsupported CFF FDSelect table";
    internal const string ErrUnsupportedCFFVersion = "sfnt: unsupported CFF version";
    internal const string ErrUnsupportedNumberOfFontDicts = "sfnt: unsupported number of font dicts";
    internal const string ErrUnsupportedNumberOfHints = "sfnt: unsupported number of hints";
    internal const string ErrUnsupportedNumberOfSubroutines = "sfnt: unsupported number of subroutines";
    internal const string ErrUnsupportedRealNumberEncoding = "sfnt: unsupported real number encoding";
    internal const string ErrUnsupportedType2Charstring = "sfnt: unsupported Type 2 Charstring";

    internal static void Check(bool ok, string message)
    {
        if (!ok)
        {
            throw new SfntFormatException(message);
        }
    }
}