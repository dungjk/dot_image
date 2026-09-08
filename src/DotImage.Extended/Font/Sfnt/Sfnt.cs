using System;
using DotImage.Extended.Math.Fixed;
using System.IO;

namespace DotImage.Extended.Font.Sfnt;

/// <summary>Hinting. Ported from font.Hinting (only None and Full are used).</summary>
public enum Hinting : int
{
    None = 0,
    Vertical = 1,
    Full = 2,
}

#region internal byte-level helpers

internal readonly struct Table
{
    public readonly uint Offset;
    public readonly uint Length;

    public Table(uint offset, uint length)
    {
        Offset = offset;
        Length = length;
    }
}

internal static class Src
{
    internal const uint MaxTableLength = 1u << 29;
    internal const uint MaxTableOffset = 1u << 29;

    internal static ushort U16(byte[] b, int i)
    {
        return (ushort)((b[i] << 8) | b[i + 1]);
    }

    internal static ushort U16(byte[] b)
    {
        return (ushort)((b[0] << 8) | b[1]);
    }

    internal static uint U32(byte[] b, int i)
    {
        return ((uint)b[i] << 24) | ((uint)b[i + 1] << 16) | ((uint)b[i + 2] << 8) | b[i + 3];
    }

    internal static uint U32(byte[] b)
    {
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    internal static ushort U16(ReadOnlySpan<byte> b, int i)
    {
        return (ushort)((b[i] << 8) | b[i + 1]);
    }

    internal static uint U32(ReadOnlySpan<byte> b, int i)
    {
        return ((uint)b[i] << 24) | ((uint)b[i + 1] << 16) | ((uint)b[i + 2] << 8) | b[i + 3];
    }

    /// <summary>Ports postscript.go bigEndian for 1-4 byte big-endian values.</summary>
    internal static uint BigEndian(byte[] b, int i, int n)
    {
        uint v = 0;
        for (int k = 0; k < n; k++)
        {
            v = (v << 8) | b[i + k];
        }
        return v;
    }

    internal static byte[] View(byte[] src, int offset, int length)
    {
        if (offset < 0 || offset > offset + length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidBounds);
        }
        if (offset + length > src.Length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidBounds);
        }
        var b = new byte[length];
        Array.Copy(src, offset, b, 0, length);
        return b;
    }

    internal static ushort TableU16(byte[] src, Table t, int i)
    {
        if (i < 0 || (uint)t.Length < (uint)(i + 2))
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidBounds);
        }
        return U16(src, (int)t.Offset + i);
    }

    internal static uint TableU32(byte[] src, Table t, int i)
    {
        if (i < 0 || (uint)t.Length < (uint)(i + 4))
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidBounds);
        }
        return U32(src, (int)t.Offset + i);
    }

    /// <summary>Ports source.varLenView (byte[] flavor).</summary>
    internal static (byte[] buf, int count) VarLenView(byte[] src, int offset, int staticLength, int countOffset, int itemLength)
    {
        if (offset < 0 || offset > offset + staticLength ||
            countOffset < 0 || countOffset + 1 >= staticLength)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidBounds);
        }
        var buf = View(src, offset, staticLength);
        int count = U16(buf, countOffset);
        buf = View(src, offset, staticLength + count * itemLength);
        return (buf, count);
    }
}

#endregion

/// <summary>An SFNT (TTF/OTF) font decoder. Ported from
/// golang.org/x/image/font/sfnt. Supports both TrueType ("glyf") outlines and
/// OpenType/CFF ("OTTO") outlines.</summary>
public sealed class Font
{
    internal byte[] src = Array.Empty<byte>();
    internal int initialOffset;

    internal Table cmap, head, hhea, hmtx, maxp, name, os2, post;
    internal Table glyf, loca;
    internal Table cff, cblc, gpos, kern;

    internal readonly Cached cached = new();

    internal sealed class Cached
    {
        public int ascent;
        public int capHeight;
        public int finalTableOffset;
        public GlyphData glyphData;
        public int[] glyphDataLocations = Array.Empty<int>();
        public GlyphIndexFunc glyphIndex = null!;
        public short[] bounds = new short[4];
        public int descent;
        public bool indexToLocFormat;
        public bool isColorBitmap;
        public bool isPostScript;
        public int kernNumPairs;
        public int kernOffset;
        public KernFunc[] kernFuncs = Array.Empty<KernFunc>();
        public int lineGap;
        public int numHMetrics;
        public PostTable? post;
        public int slope0;
        public int slope1;
        public Units unitsPerEm;
        public int xHeight;
    }

    /// <summary>Parses an SFNT font from a []byte data source.</summary>
    public static Font Parse(byte[] src)
    {
        var f = new Font { src = src };
        f.Initialize(0, false);
        return f;
    }

    /// <summary>Parses an SFNT font collection, such as TTC or OTC data. If
    /// passed data for a single font, the collection contains 1 font.</summary>
    public static Collection ParseCollection(byte[] src)
    {
        var c = new Collection { SrcData = src };
        c.Initialize();
        return c;
    }

    public int NumGlyphs => cached.glyphDataLocations.Length - 1;

    public Units UnitsPerEm => cached.unitsPerEm;

    public PostTable? PostTable => cached.post;

    internal void Initialize(int offset, bool isDfont)
    {
        int finalTableOffset;
        bool isPostScript;
        (finalTableOffset, isPostScript) = InitializeTables(offset, isDfont);

        // parse order matters; earlier parses feed later ones.
        var head = ParseHead();
        int numGlyphs = ParseMaxp(isPostScript);
        var glyphData = ParseGlyphData(numGlyphs, head.indexToLocFormat, isPostScript);
        var glyphIndex = ParseCmap();
        var kern = ParseKern();
        var gposKernFuncs = ParseGPOSKern();
        var hhea = ParseHhea(numGlyphs);
        ParseHmtx(numGlyphs, hhea.numHMetrics);
        var os2 = ParseOS2();
        var post = ParsePost(numGlyphs);

        cached.glyphData = glyphData;
        cached.ascent = hhea.ascent;
        cached.capHeight = os2.capHeight;
        cached.finalTableOffset = finalTableOffset;
        cached.glyphDataLocations = glyphData.Locations;
        cached.glyphIndex = glyphIndex;
        cached.bounds = head.bounds;
        cached.descent = hhea.descent;
        cached.indexToLocFormat = head.indexToLocFormat;
        cached.isColorBitmap = glyphData.IsColorBitmap;
        cached.isPostScript = isPostScript;
        cached.kernNumPairs = kern.kernNumPairs;
        cached.kernOffset = kern.kernOffset;
        cached.kernFuncs = gposKernFuncs;
        cached.lineGap = hhea.lineGap;
        cached.numHMetrics = hhea.numHMetrics;
        cached.post = post;
        cached.slope0 = hhea.run;
        cached.slope1 = hhea.rise;
        cached.unitsPerEm = head.unitsPerEm;
        cached.xHeight = os2.xHeight;

        if (!os2.hasXHeightCapHeight)
        {
            var (xh, ch) = InitOS2VersionBelow2();
            cached.xHeight = xh;
            cached.capHeight = ch;
        }
    }

    private (int finalTableOffset, bool isPostScript) InitializeTables(int offset, bool isDfont)
    {
        initialOffset = offset;

        var buf = Src.View(src, offset, 12);
        bool isPostScript = false;
        switch (Src.U32(buf))
        {
            case 0x00010000: // 0x10000
            case 0x74727565: // "true"
                break;
            case 0x4f54544f: // "OTTO"
                isPostScript = true;
                break;
            case 0x74746366: // "ttcf"
                throw new SfntFormatException(SfntErrors.ErrInvalidSingleFont);
            default:
                throw new SfntFormatException(SfntErrors.ErrInvalidFont);
        }

        int numTables = Src.U16(buf, 4);
        if (numTables > 256)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedNumberOfTables);
        }

        buf = Src.View(src, offset + 12, 16 * numTables);
        int finalTableOffset = 0;
        uint prevTag = 0;
        bool first = true;
        for (int i = 0; i < numTables; i++)
        {
            int b = 16 * i;
            uint tag = Src.U32(buf, b);
            if (first)
            {
                first = false;
            }
            else if (tag <= prevTag)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidTableTagOrder);
            }
            prevTag = tag;

            uint o = Src.U32(buf, b + 8);
            uint n = Src.U32(buf, b + 12);
            if (isDfont)
            {
                uint origO = o;
                o += (uint)offset;
                if (o < origO)
                {
                    throw new SfntFormatException(SfntErrors.ErrUnsupportedTableOffsetLength);
                }
            }
            if (o > Src.MaxTableOffset || n > Src.MaxTableLength)
            {
                throw new SfntFormatException(SfntErrors.ErrUnsupportedTableOffsetLength);
            }
            if ((o & 3) != 0)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidTableOffset);
            }
            if (finalTableOffset < (int)(o + n))
            {
                finalTableOffset = (int)(o + n);
            }

            var t = new Table(o, n);
            switch (tag)
            {
                case 0x43424c43: cblc = t; break; // "CBLC"
                case 0x43464620: cff = t; break; // "CFF "
                case 0x4f532f32: os2 = t; break; // "OS/2"
                case 0x636d6170: cmap = t; break; // "cmap"
                case 0x676c7966: glyf = t; break; // "glyf"
                case 0x47504f53: gpos = t; break; // "GPOS"
                case 0x68656164: head = t; break; // "head"
                case 0x68686561: hhea = t; break; // "hhea"
                case 0x686d7478: hmtx = t; break; // "hmtx"
                case 0x6b65726e: kern = t; break; // "kern"
                case 0x6c6f6361: loca = t; break; // "loca"
                case 0x6d617870: maxp = t; break; // "maxp"
                case 0x6e616d65: name = t; break; // "name"
                case 0x706f7374: post = t; break; // "post"
            }
        }

        if (finalTableOffset > src.Length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidSourceData);
        }
        return (finalTableOffset, isPostScript);
    }

    private GlyphIndexFunc ParseCmap() => Cmap.ParseCmap(this);

    private KernFunc[] ParseGPOSKern() => Gpos.ParseGPOSKern(this);

    private (short[] bounds, bool indexToLocFormat, Units unitsPerEm) ParseHead()
    {
        if (head.Length != 54)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidHeadTable);
        }
        ushort u = Src.TableU16(src, head, 18);
        if (u == 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidHeadTable);
        }
        Units unitsPerEm = (Units)u;

        var bounds = new short[4];
        for (int i = 0; i < 4; i++)
        {
            ushort b = Src.TableU16(src, head, 36 + 2 * i);
            bounds[i] = unchecked((short)b);
        }

        u = Src.TableU16(src, head, 50);
        bool indexToLocFormat = u != 0;
        return (bounds, indexToLocFormat, unitsPerEm);
    }

    private int ParseMaxp(bool isPostScript)
    {
        if (isPostScript)
        {
            if (maxp.Length != 6)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidMaxpTable);
            }
        }
        else
        {
            if (maxp.Length != 32)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidMaxpTable);
            }
        }
        return Src.TableU16(src, maxp, 4);
    }

    private GlyphData ParseGlyphData(int numGlyphs, bool indexToLocFormat, bool isPostScript)
    {
        var glyphData = default(GlyphData);
        if (isPostScript)
        {
            glyphData = CffParser.Parse(this, numGlyphs);
        }
        else if (loca.Length != 0)
        {
            glyphData.Locations = TrueType.ParseLoca(src, loca, glyf.Offset, indexToLocFormat, numGlyphs);
        }
        else if (cblc.Length != 0)
        {
            glyphData.IsColorBitmap = true;
            glyphData.Locations = new int[numGlyphs + 1];
        }
        else
        {
            glyphData.Locations = new int[numGlyphs + 1];
        }

        if (glyphData.Locations.Length != numGlyphs + 1)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidLocationData);
        }
        return glyphData;
    }

    private (int ascent, int descent, int lineGap, int run, int rise, int numHMetrics) ParseHhea(int numGlyphs)
    {
        if (hhea.Length != 36)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidHheaTable);
        }
        ushort u = Src.TableU16(src, hhea, 34);
        if ((int)u > numGlyphs || u == 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidHheaTable);
        }
        ushort a = Src.TableU16(src, hhea, 4);
        ushort d = Src.TableU16(src, hhea, 6);
        ushort l = Src.TableU16(src, hhea, 8);
        ushort ru = Src.TableU16(src, hhea, 20);
        ushort ri = Src.TableU16(src, hhea, 18);
        return (unchecked((short)a), unchecked((short)d), unchecked((short)l),
            unchecked((short)ru), unchecked((short)ri), u);
    }

    private void ParseHmtx(int numGlyphs, int numHMetrics)
    {
        // Some fonts in the wild omit the "2*(nGlyphs-nHMetrics)" tail.
        if (hmtx.Length != (uint)(4 * numHMetrics) &&
            hmtx.Length != (uint)(4 * numHMetrics + 2 * (numGlyphs - numHMetrics)))
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidHmtxTable);
        }
    }

    private (int kernNumPairs, int kernOffset) ParseKern()
    {
        if (kern.Length == 0)
        {
            return (0, 0);
        }
        const int headerSize = 4;
        if (kern.Length < headerSize)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidKernTable);
        }
        var buf = Src.View(src, (int)kern.Offset, headerSize);
        int offset = (int)kern.Offset + headerSize;
        int length = (int)kern.Length - headerSize;

        switch (Src.U16(buf))
        {
            case 0:
                if (Src.U16(buf, 2) == 0)
                {
                    return (0, 0);
                }
                return ParseKernVersion0(offset, length);
            case 1:
                if (buf[2] != 0 || buf[3] != 0)
                {
                    throw new SfntFormatException(SfntErrors.ErrUnsupportedKernTable);
                }
                return (0, 0);
        }
        throw new SfntFormatException(SfntErrors.ErrUnsupportedKernTable);
    }

    private (int kernNumPairs, int kernOffset) ParseKernVersion0(int offset, int length)
    {
        const int headerSize = 6;
        if (length < headerSize)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidKernTable);
        }
        var buf = Src.View(src, offset, headerSize);
        if (Src.U16(buf) != 0)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedKernTable);
        }
        ushort subtableLengthU16 = Src.U16(buf, 2);
        if (subtableLengthU16 < headerSize || length < subtableLengthU16)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidKernTable);
        }
        if (buf[5] != 0x01)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedKernTable);
        }
        offset += headerSize;
        length -= headerSize;
        subtableLengthU16 -= (ushort)headerSize;

        switch (buf[4])
        {
            case 0:
                return ParseKernFormat0(offset, length, subtableLengthU16);
            case 2:
                break;
        }
        throw new SfntFormatException(SfntErrors.ErrUnsupportedKernTable);
    }

    private (int kernNumPairs, int kernOffset) ParseKernFormat0(int offset, int length, ushort subtableLengthU16)
    {
        const int headerSize = 8, entrySize = 6;
        if (length < headerSize)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidKernTable);
        }
        var buf = Src.View(src, offset, headerSize);
        int kernNumPairs = Src.U16(buf);

        int n = headerSize + entrySize * kernNumPairs;
        if (length < n || subtableLengthU16 != (ushort)n)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidKernTable);
        }
        return (kernNumPairs, offset + headerSize);
    }

    private (bool hasXHeightCapHeight, int xHeight, int capHeight) ParseOS2()
    {
        if (os2.Length == 0)
        {
            return (false, 0, 0);
        }
        else if (os2.Length < 2)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidOS2Table);
        }
        ushort vers = Src.TableU16(src, os2, 0);
        if (vers < 2)
        {
            if (os2.Length < 68)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidOS2Table);
            }
            return (false, 0, 0);
        }
        if (os2.Length < 96)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidOS2Table);
        }
        ushort xh = Src.TableU16(src, os2, 86);
        ushort ch = Src.TableU16(src, os2, 88);
        return (true, unchecked((short)xh), unchecked((short)ch));
    }

    private PostTable ParsePost(int numGlyphs)
    {
        const int headerSize = 32;
        if (post.Length < headerSize)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidPostTable);
        }
        uint u = Src.TableU32(src, post, 0);
        switch (u)
        {
            case 0x00010000:
                break;
            case 0x00020000:
                if (post.Length < headerSize + 2 + 2 * (uint)numGlyphs)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidPostTable);
                }
                break;
            case 0x00030000:
                break;
            default:
                throw new SfntFormatException(SfntErrors.ErrUnsupportedPostTable);
        }

        uint ang = Src.TableU32(src, post, 4);
        ushort up = Src.TableU16(src, post, 8);
        ushort ut = Src.TableU16(src, post, 10);
        uint fp = Src.TableU32(src, post, 12);
        return new PostTable
        {
            Version = u,
            ItalicAngle = unchecked((int)ang) / (double)0x10000,
            UnderlinePosition = unchecked((short)up),
            UnderlineThickness = unchecked((short)ut),
            IsFixedPitch = fp != 0,
        };
    }

    private (int xHeight, int capHeight) InitOS2VersionBelow2()
    {
        var ppem = Int26_6.FromRaw(UnitsPerEm.Value);
        int xh = GlyphTopOS2(ppem, 'x');
        int ch = GlyphTopOS2(ppem, 'H');
        return (xh, ch);
    }

    private int GlyphTopOS2(Int26_6 ppem, int r)
    {
        GlyphIndex ind;
        try
        {
            ind = GlyphIndex(null, r);
        }
        catch (SfntFormatException ex) when (ex.Message == SfntErrors.ErrNotFound)
        {
            ind = 0;
        }
        if (ind.Value == 0)
        {
            return 0;
        }
        var seg = LoadGlyph(null, ind, ppem, null);
        long min = long.MaxValue;
        for (int i = 0; i < seg.Count; i++)
        {
            var s = seg[i];
            int n = s.Op switch
            {
                SegmentOp.QuadTo => 2,
                SegmentOp.CubeTo => 3,
                _ => 1,
            };
            for (int j = 0; j < n; j++)
            {
                var p = s.Args(j);
                if (p.Y.Raw < min)
                {
                    min = p.Y.Raw;
                }
            }
        }
        return (int)min;
    }

    /// <summary>Returns the glyph index for the given rune.
    /// Returns (0, nil semantics) if there is no glyph for r.</summary>
    public GlyphIndex GlyphIndex(Buffer? b, int r)
    {
        return cached.glyphIndex(this, b, r);
    }

    internal (byte[] data, uint offset, uint length) ViewGlyphData(Buffer? b, GlyphIndex x)
    {
        int xx = x.Value;
        if (NumGlyphs <= xx)
        {
            throw new SfntFormatException(SfntErrors.ErrNotFound);
        }
        int i = cached.glyphDataLocations[xx];
        int j = cached.glyphDataLocations[xx + 1];
        if (j < i)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidGlyphDataLength);
        }
        if (j - i > 64 * 1024)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedGlyphDataLength);
        }
        byte[] data = Src.View(src, i, j - i);
        return (data, (uint)i, (uint)(j - i));
    }

    // Segments exposes the underlying list so that tests and glyphBounds can
    // iterate it. It aliases b's reusable segment storage.
    internal List<Segment> LoadGlyphSegments(Buffer b, GlyphIndex x, Int26_6 ppem, object? opts)
    {
        b.Segments.Clear();
        if (cached.isColorBitmap)
        {
            throw new SfntFormatException(SfntErrors.ErrColoredGlyph);
        }
        if (cached.isPostScript)
        {
            var (data, offset, length) = ViewGlyphData(b, x);
            b.Psi.type2Charstrings.Initialize(this, b, x);
            b.Psi.Run(PsContext.Type2Charstring, data.AsMemory(), offset, length);
            if (!b.Psi.type2Charstrings.ended)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
        }
        else
        {
            TrueType.LoadGlyf(this, b, x, 0, 0);
        }

        for (int i = 0; i < b.Segments.Count; i++)
        {
            var s = b.Segments[i];
            for (int j = 0; j < 3; j++)
            {
                var p = s.Args(j);
                s.SetArgs(j, new Point26_6(Scale(p.X* ppem, cached.unitsPerEm), -Scale(p.Y* ppem, cached.unitsPerEm)));
            }
            b.Segments[i] = s;
        }
        return b.Segments.Items;
    }

    /// <summary>Loads the vector segments for the x'th glyph. The returned list
    /// aliases b's storage and becomes invalid once b is re-used. The Y axis
    /// increases down.</summary>
    public Segments LoadGlyph(Buffer? b, GlyphIndex x, Int26_6 ppem, object? opts)
    {
        b ??= new Buffer();
        var list = LoadGlyphSegments(b, x, ppem, opts);
        var s = new Segments();
        s.SetItems(list);
        return s;
    }

    /// <summary>Returns the union of the font's glyphs' bounds. The Y axis
    /// increases down.</summary>
    public Rectangle26_6 Bounds(Buffer? b, Int26_6 ppem, Hinting h)
    {
        // 0, 3, 2, 1 swap indices to flip Y.
        var r = new Rectangle26_6(
            Scale(Int26_6.FromRaw(cached.bounds[0]) * ppem, cached.unitsPerEm),
            -Scale(Int26_6.FromRaw(cached.bounds[3]) * ppem, cached.unitsPerEm),
            Scale(Int26_6.FromRaw(cached.bounds[2]) * ppem, cached.unitsPerEm),
            -Scale(Int26_6.FromRaw(cached.bounds[1]) * ppem, cached.unitsPerEm));
        if (h == Hinting.Full)
        {
            r = new Rectangle26_6(
                QuantizeDown(r.MinX), QuantizeDown(r.MinY),
                QuantizeUp(r.MaxX), QuantizeUp(r.MaxY));
        }
        return r;
    }

    /// <summary>Returns the bounding box and advance width of the x'th glyph.
    /// The glyph's ascent and descent equal -Min.Y and +Max.Y.</summary>
    public (Rectangle26_6 bounds, Int26_6 advance) GlyphBounds(Buffer? b, GlyphIndex x, Int26_6 ppem, Hinting h)
    {
        if (x.Value >= NumGlyphs)
        {
            throw new SfntFormatException(SfntErrors.ErrNotFound);
        }
        b ??= new Buffer();

        GlyphIndex metricIndex = x;
        if (x > (GlyphIndex)(cached.numHMetrics - 1))
        {
            metricIndex = (GlyphIndex)(cached.numHMetrics - 1);
        }

        var buf = Src.View(src, (int)hmtx.Offset + 4 * (int)metricIndex.Value, 2);
        var advance = Int26_6.FromRaw(Src.U16(buf));
        advance = Scale(advance * ppem, cached.unitsPerEm);
        if (h == Hinting.Full)
        {
            advance = QuantizeHalf(advance);
        }

        var segments = LoadGlyph(b, x, ppem, null);
        return (segments.Bounds(), advance);
    }

    /// <summary>Returns the advance width for the x'th glyph.</summary>
    public Int26_6 GlyphAdvance(Buffer? b, GlyphIndex x, Int26_6 ppem, Hinting h)
    {
        if (x.Value >= NumGlyphs)
        {
            throw new SfntFormatException(SfntErrors.ErrNotFound);
        }
        b ??= new Buffer();

        if (x > (GlyphIndex)(cached.numHMetrics - 1))
        {
            x = (GlyphIndex)(cached.numHMetrics - 1);
        }

        var buf = Src.View(src, (int)hmtx.Offset + 4 * (int)x.Value, 2);
        var adv = Int26_6.FromRaw(Src.U16(buf));
        adv = Scale(adv * ppem, cached.unitsPerEm);
        if (h == Hinting.Full)
        {
            adv = QuantizeHalf(adv);
        }
        return adv;
    }

    /// <summary>Returns the horizontal adjustment for the kerning pair
    /// (x0, x1). A positive kern moves glyphs apart.</summary>
    public Int26_6 Kern(Buffer? b, GlyphIndex x0, GlyphIndex x1, Int26_6 ppem, Hinting h)
    {
        // Use GPOS kern tables if available.
        if (cached.kernFuncs.Length != 0)
        {
            foreach (var kf in cached.kernFuncs)
            {
                int adv;
                try
                {
                    adv = kf(x0, x1);
                }
                catch (SfntFormatException ex) when (ex.Message == SfntErrors.ErrNotFound)
                {
                    continue;
                }
                var kern = Int26_6.FromRaw(adv);
                kern = Scale(kern * ppem, cached.unitsPerEm);
                if (h == Hinting.Full)
                {
                    kern = QuantizeHalf(kern);
                }
                return kern;
            }
            throw new SfntFormatException(SfntErrors.ErrNotFound);
        }

        // Fallback to kern table.
        if (x0.Value >= NumGlyphs || x1.Value >= NumGlyphs)
        {
            throw new SfntFormatException(SfntErrors.ErrNotFound);
        }
        if (cached.kernNumPairs == 0)
        {
            return Int26_6.FromRaw(0);
        }
        b ??= new Buffer();

        uint key = ((uint)x0.Value << 16) | x1.Value;
        int lo = 0, hi = cached.kernNumPairs;
        while (lo < hi)
        {
            int i = (lo + hi) / 2;
            var kb = Src.View(src, cached.kernOffset + i * 6, 6);
            uint k = Src.U32(kb);
            if (k < key)
            {
                lo = i + 1;
            }
            else if (k > key)
            {
                hi = i;
            }
            else
            {
                var kern = Int26_6.FromRaw(unchecked((short)Src.U16(kb, 4)));
                kern = Scale(kern * ppem, cached.unitsPerEm);
                if (h == Hinting.Full)
                {
                    kern = QuantizeHalf(kern);
                }
                return kern;
            }
        }
        return Int26_6.FromRaw(0);
    }

    /// <summary>Returns the metrics of this font.</summary>
    public FontMetrics Metrics(Buffer? b, Int26_6 ppem, Hinting h)
    {
        var m = new FontMetrics(
            Scale(Int26_6.FromRaw(cached.ascent - cached.descent + cached.lineGap) * ppem, cached.unitsPerEm),
            Scale(Int26_6.FromRaw(cached.ascent) * ppem, cached.unitsPerEm),
            -Scale(Int26_6.FromRaw(cached.descent) * ppem, cached.unitsPerEm),
            Scale(Int26_6.FromRaw(cached.xHeight) * ppem, cached.unitsPerEm),
            Scale(Int26_6.FromRaw(cached.capHeight) * ppem, cached.unitsPerEm),
            new Point(cached.slope0, cached.slope1));
        if (h == Hinting.Full)
        {
            m = new FontMetrics(
                QuantizeUp(m.Height), QuantizeUp(m.Ascent), QuantizeUp(m.Descent),
                QuantizeUp(m.XHeight), QuantizeUp(m.CapHeight), m.CaretSlope);
        }
        return m;
    }

    /// <summary>Returns the name value keyed by the given NameID.</summary>
    public string Name(Buffer? b, NameID id)
    {
        b ??= new Buffer();

        const int headerSize = 6, entrySize = 12;
        if (name.Length < headerSize)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidNameTable);
        }
        var buf = Src.View(src, (int)name.Offset, headerSize);
        ushort numSubtables = Src.U16(buf, 2);
        if (name.Length < (uint)(headerSize + entrySize * numSubtables))
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidNameTable);
        }
        ushort stringOffset = Src.U16(buf, 4);

        bool seen = false;
        for (int i = 0, n = numSubtables; i < n; i++)
        {
            var ent = Src.View(src, (int)name.Offset + headerSize + entrySize * i, entrySize);
            if (Src.U16(ent, 6) != id.Value)
            {
                continue;
            }
            seen = true;

            Func<byte[], string> stringify;
            switch (Src.U32(ent))
            {
                default:
                    continue;
                case 0x00010000: // pidMacintosh<<16 | psidMacintoshRoman
                    stringify = StringifyMacintosh;
                    break;
                case 0x00030001: // pidWindows<<16 | psidWindowsUCS2
                    stringify = StringifyUCS2;
                    break;
            }

            ushort nameLength = Src.U16(ent, 8);
            ushort nameOffset = Src.U16(ent, 10);
            var nb = Src.View(src, (int)name.Offset + nameOffset + stringOffset, nameLength);
            return stringify(nb);
        }

        if (seen)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedPlatformEncoding);
        }
        throw new SfntFormatException(SfntErrors.ErrNotFound);
    }

    private static string StringifyMacintosh(byte[] b)
    {
        foreach (byte c in b)
        {
            if (c >= 0x80)
            {
                return MacRoman.Decode(b);
            }
        }
        return System.Text.Encoding.ASCII.GetString(b);
    }

    private static string StringifyUCS2(byte[] b)
    {
        if ((b.Length & 1) != 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidUCS2String);
        }
        var sb = new System.Text.StringBuilder(b.Length / 2);
        for (int i = 0; i < b.Length; i += 2)
        {
            unchecked
            {
                sb.Append((char)Src.U16(b, i));
            }
        }
        return sb.ToString();
    }

    internal string GlyphNameFormat10(GlyphIndex x)
    {
        if (x.Value >= SfntData.NumBuiltInPostNames)
        {
            throw new SfntFormatException(SfntErrors.ErrNotFound);
        }
        int i = SfntData.BuiltInPostNamesOffsets[x.Value];
        int j = SfntData.BuiltInPostNamesOffsets[x.Value + 1];
        return SfntData.BuiltInPostNamesData[i..j];
    }

    /// <summary>Returns the name of the x'th glyph, or "" if not present.</summary>
    public string GlyphName(Buffer? b, GlyphIndex x)
    {
        if (x.Value >= NumGlyphs)
        {
            throw new SfntFormatException(SfntErrors.ErrNotFound);
        }
        if (cached.post == null)
        {
            return "";
        }
        switch (cached.post.Version)
        {
            case 0x00010000:
                return GlyphNameFormat10(x);
            case 0x00020000:
                if (b == null)
                {
                    b = new Buffer();
                }
                return GlyphNameFormat20Scan(b, x);
            default:
                return "";
        }
    }

    private string GlyphNameFormat20Scan(Buffer b, GlyphIndex x)
    {
        const int glyphNameIndexOffset = 34;
        var buf = Src.View(src, (int)post.Offset + glyphNameIndexOffset + 2 * (int)x.Value, 2);
        ushort u = Src.U16(buf);
        if (u < SfntData.NumBuiltInPostNames)
        {
            int i = SfntData.BuiltInPostNamesOffsets[u];
            int j = SfntData.BuiltInPostNamesOffsets[u + 1];
            return SfntData.BuiltInPostNamesData[i..j];
        }
        if (u > 32767)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedPostTable);
        }
        u -= (ushort)SfntData.NumBuiltInPostNames;

        int offset = glyphNameIndexOffset + 2 * NumGlyphs;
        int remaining = (int)post.Length - offset;
        int readOffset = (int)post.Offset + offset;
        while (true)
        {
            if (remaining == 0)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidPostTable);
            }
            var head = Src.View(src, readOffset, System.Math.Min(1, remaining));
            int n = 1 + head[0];
            if (remaining < n)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidPostTable);
            }
            if (u == 0)
            {
                var name = Src.View(src, readOffset + 1, n - 1);
                return System.Text.Encoding.ASCII.GetString(name);
            }
            readOffset += n;
            remaining -= n;
            u--;
        }
    }

    /// <summary>Writes the source data to w, returning the number of bytes
    /// written (the final table offset on success).</summary>
    public long WriteSourceTo(Buffer? b, Stream w)
    {
        if (initialOffset != 0)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedCollection);
        }
        int finalTableOffset = cached.finalTableOffset;
        w.Write(src, 0, finalTableOffset);
        return finalTableOffset;
    }

    internal static Int26_6 Scale(Int26_6 x, Units unitsPerEm)
    {
        // Go: h := fixed.Int26_6(unitsPerEm/2); x += h; x / fixed.Int26_6(unitsPerEm).
        // All arithmetic is on the raw 26.6 numerals, which are identical in
        // C# and Go. The caller supplies x = Mul(coord, ppem).
        long v = x.Raw;
        int upm = unitsPerEm.Value;
        if (v >= 0)
        {
            v += upm / 2;
        }
        else
        {
            v -= upm / 2;
        }
        return Int26_6.FromRaw(v / upm);
    }

    internal static Int26_6 QuantizeDown(Int26_6 v) => Int26_6.FromRaw((long)(v.Raw & ~63));
    internal static Int26_6 QuantizeUp(Int26_6 v) => Int26_6.FromRaw((long)((v.Raw + 63) & ~63));
    internal static Int26_6 QuantizeHalf(Int26_6 v) => Int26_6.FromRaw((long)((v.Raw + 32) & ~63));
}

/// <summary>An SFNT font collection (TTC). Ported from sfnt.Collection.</summary>
public sealed class Collection
{
    internal byte[] SrcData = Array.Empty<byte>();
    internal uint[] Offsets = Array.Empty<uint>();
    internal bool IsDfont;

    internal const uint DfontResourceDataOffset = 0x00000100;

    public int NumFonts => Offsets.Length;

    internal void Initialize()
    {
        var buf = Src.View(SrcData, 0, 16);
        switch (Src.U32(buf))
        {
            default:
                throw new SfntFormatException(SfntErrors.ErrInvalidFontCollection);
            case DfontResourceDataOffset:
                ParseDfont(buf, Src.U32(buf, 4), Src.U32(buf, 12));
                break;
            case 0x00010000:
            case 0x4f54544f: // "OTTO"
            case 0x74727565: // "true"
                Offsets = new uint[] { 0 };
                break;
            case 0x74746366: // "ttcf"
                uint numFonts = Src.U32(buf, 8);
                if (numFonts == 0 || numFonts > 256)
                {
                    throw new SfntFormatException(SfntErrors.ErrUnsupportedNumberOfFonts);
                }
                buf = Src.View(SrcData, 12, (int)(4 * numFonts));
                Offsets = new uint[numFonts];
                for (int i = 0; i < Offsets.Length; i++)
                {
                    uint o = Src.U32(buf, 4 * i);
                    if (o > Src.MaxTableOffset)
                    {
                        throw new SfntFormatException(SfntErrors.ErrUnsupportedTableOffsetLength);
                    }
                    Offsets[i] = o;
                }
                break;
        }
    }

    private void ParseDfont(byte[] buf, uint resourceMapOffset, uint resourceMapLength)
    {
        if (resourceMapOffset > Src.MaxTableOffset || resourceMapLength > Src.MaxTableLength)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedTableOffsetLength);
        }
        const int headerSize = 28;
        if (resourceMapLength < headerSize)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidDfont);
        }
        buf = Src.View(SrcData, (int)(resourceMapOffset + 24), 2);
        int typeListOffset = (short)Src.U16(buf);

        if (typeListOffset < headerSize || resourceMapLength < (uint)typeListOffset + 2)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidDfont);
        }
        buf = Src.View(SrcData, (int)resourceMapOffset + typeListOffset, 2);
        int typeCount = (short)Src.U16(buf);

        const int tSize = 8;
        if (typeCount < 0 || (uint)(tSize * typeCount) > resourceMapLength - (uint)typeListOffset - 2)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidDfont);
        }
        buf = Src.View(SrcData, (int)resourceMapOffset + typeListOffset + 2, tSize * typeCount);
        int resourceCount = 0, resourceListOffset = 0;
        for (int i = 0; i < typeCount; i++)
        {
            if (Src.U32(buf, tSize * i) != 0x73666e74) // "sfnt"
            {
                continue;
            }
            resourceCount = (short)Src.U16(buf, tSize * i + 4);
            if (resourceCount < 0)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidDfont);
            }
            resourceCount++;
            resourceListOffset = (short)Src.U16(buf, tSize * i + 6);
            if (resourceListOffset < 0)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidDfont);
            }
            break;
        }
        if (resourceCount == 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidDfont);
        }
        if (resourceCount > 256)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedNumberOfFonts);
        }

        const int rSize = 12;
        uint o = (uint)(typeListOffset + resourceListOffset);
        uint n = (uint)(rSize * resourceCount);
        if (o > resourceMapLength || n > resourceMapLength - o)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidDfont);
        }
        buf = Src.View(SrcData, (int)(resourceMapOffset + o), (int)n);
        Offsets = new uint[resourceCount];
        for (int i = 0; i < Offsets.Length; i++)
        {
            uint off = 0xffffff & Src.U32(buf, rSize * i + 4);
            off += DfontResourceDataOffset + 4;
            if (off > Src.MaxTableOffset)
            {
                throw new SfntFormatException(SfntErrors.ErrUnsupportedTableOffsetLength);
            }
            Offsets[i] = off;
        }
        IsDfont = true;
    }

    public Font FontAt(int i)
    {
        if (i < 0 || Offsets.Length <= i)
        {
            throw new SfntFormatException(SfntErrors.ErrNotFound);
        }
        var f = new Font { src = SrcData };
        f.Initialize((int)Offsets[i], IsDfont);
        return f;
    }
}

/// <summary>Holds re-usable buffers for repeated Font method calls. Ported from
/// sfnt.Buffer.</summary>
public sealed class Buffer
{
    internal Segments Segments = new();
    internal CompoundElem[] CompoundStack = new CompoundElem[TrueType.MaxCompoundStackSize];
    internal PsInterpreter Psi = new();
}

/// <summary>PostScript font information ("post" table).</summary>
public sealed class PostTable
{
    public uint Version;
    public double ItalicAngle;
    public short UnderlinePosition;
    public short UnderlineThickness;
    public bool IsFixedPitch;
}