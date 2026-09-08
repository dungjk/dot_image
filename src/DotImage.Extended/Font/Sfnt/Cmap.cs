using System;

namespace DotImage.Extended.Font.Sfnt;

/// <summary>Ported from sfnt.glyphIndexFunc.</summary>
internal delegate GlyphIndex GlyphIndexFunc(Font f, Buffer b, int r);

internal static class Cmap
{
    // Platform IDs and Platform Specific IDs.
    internal const ushort PidUnicode = 0;
    internal const ushort PidMacintosh = 1;
    internal const ushort PidWindows = 3;

    internal const ushort PsidUnicode2BMPOnly = 3;
    internal const ushort PsidUnicode2FullRepertoire = 4;
    internal const ushort PsidMacintoshRoman = 0;
    internal const ushort PsidWindowsSymbol = 0;
    internal const ushort PsidWindowsUCS2 = 1;
    internal const ushort PsidWindowsUCS4 = 10;

    internal const int MaxCmapSegments = 20000;

    /// <summary>Number of bytes per character assumed by the platform
    /// encoding.</summary>
    internal static int PlatformEncodingWidth(ushort pid, ushort psid)
    {
        switch (pid)
        {
            case PidUnicode:
                switch (psid)
                {
                    case PsidUnicode2BMPOnly: return 2;
                    case PsidUnicode2FullRepertoire: return 4;
                }
                break;
            case PidMacintosh:
                switch (psid)
                {
                    case PsidMacintoshRoman: return 1;
                }
                break;
            case PidWindows:
                switch (psid)
                {
                    case PsidWindowsSymbol: return 2;
                    case PsidWindowsUCS2: return 2;
                    case PsidWindowsUCS4: return 4;
                }
                break;
        }
        return 0;
    }

    // Tests can temporarily override this to select a single cmap format.
    internal static Func<ushort, ushort, ushort, bool> SupportedCmapFormat = InitialSupportedCmapFormat;

    internal static bool InitialSupportedCmapFormat(ushort format, ushort pid, ushort psid)
    {
        switch (format)
        {
            case 0:
                return pid == PidMacintosh && psid == PsidMacintoshRoman;
            case 4:
            case 6:
            case 12:
                return true;
        }
        return false;
    }

    /// <summary>Ported from Font.parseCmap (scanning part).</summary>
    internal static GlyphIndexFunc ParseCmap(Font f)
    {
        const uint headerSize = 4, entrySize = 8;
        if (f.cmap.Length < headerSize)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }
        ushort numSubtables = Src.TableU16(f.src, f.cmap, 2);
        if (f.cmap.Length < headerSize + entrySize * numSubtables)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }

        int bestWidth = 0;
        uint bestOffset = 0, bestLength = 0;
        ushort bestFormat = 0;

        for (int i = 0; i < numSubtables; i++)
        {
            int entryOffset = (int)(f.cmap.Offset + headerSize + entrySize * i);
            var ent = Src.View(f.src, entryOffset, (int)entrySize);
            ushort pid = Src.U16(ent);
            ushort psid = Src.U16(ent, 2);
            int width = PlatformEncodingWidth(pid, psid);
            if (width <= bestWidth)
            {
                continue;
            }
            uint offset = Src.U32(ent, 4);
            if (offset > f.cmap.Length - 4)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
            }
            var hdr = Src.View(f.src, (int)(f.cmap.Offset + offset), 4);
            ushort format = Src.U16(hdr);
            if (!SupportedCmapFormat(format, pid, psid))
            {
                continue;
            }
            uint length = Src.U16(hdr, 2);

            bestWidth = width;
            bestOffset = offset;
            bestLength = length;
            bestFormat = format;
        }

        if (bestWidth == 0)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedCmapEncodings);
        }
        return MakeCachedGlyphIndex(f, bestOffset, bestLength, bestFormat);
    }

    private static GlyphIndexFunc MakeCachedGlyphIndex(Font f, uint offset, uint length, ushort format)
    {
        switch (format)
        {
            case 0:
                return MakeCachedGlyphIndexFormat0(f, offset, length);
            case 4:
                return MakeCachedGlyphIndexFormat4(f, offset, length);
            case 6:
                return MakeCachedGlyphIndexFormat6(f, offset, length);
            case 12:
                return MakeCachedGlyphIndexFormat12(f, offset, length);
        }
        throw new InvalidOperationException("unreachable");
    }

    private static GlyphIndexFunc MakeCachedGlyphIndexFormat0(Font f, uint offset, uint length)
    {
        if (length != 6 + 256 || offset + length > f.cmap.Length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }
        var buf = Src.View(f.src, (int)(f.cmap.Offset + offset), (int)length);
        var table = new byte[256];
        Array.Copy(buf, 6, table, 0, 256);
        return (ff, b, r) =>
        {
            if (!MacRoman.EncodeRune(r, out byte x))
            {
                return 0;
            }
            return new GlyphIndex(table[x]);
        };
    }

    private static GlyphIndexFunc MakeCachedGlyphIndexFormat4(Font f, uint offset, uint length)
    {
        const uint headerSize = 14;
        if (offset + headerSize > f.cmap.Length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }
        var buf = Src.View(f.src, (int)(f.cmap.Offset + offset), (int)headerSize);
        offset += headerSize;

        int segCount = Src.U16(buf, 6);
        if ((segCount & 1) != 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }
        segCount /= 2;
        if (segCount > MaxCmapSegments)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedNumberOfCmapSegments);
        }

        var entries = new CmapEntry16[segCount];
        uint eLength = 8 * (uint)segCount + 2;
        if (offset + eLength > f.cmap.Length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }
        var ebuf = Src.View(f.src, (int)(f.cmap.Offset + offset), (int)eLength);
        offset += eLength;

        for (int i = 0; i < segCount; i++)
        {
            entries[i] = new CmapEntry16(
                end: Src.U16(ebuf, 0 * segCount + 0 + 2 * i),
                start: Src.U16(ebuf, 2 * segCount + 2 + 2 * i),
                delta: Src.U16(ebuf, 4 * segCount + 2 + 2 * i),
                offset: Src.U16(ebuf, 6 * segCount + 2 + 2 * i));
        }
        uint indexesBase = f.cmap.Offset + offset;
        uint indexesLength = f.cmap.Length - offset;

        return (ff, b, r) =>
        {
            if ((uint)r > 0xffff)
            {
                return 0;
            }
            int c = (ushort)r;
            int i = 0, j = entries.Length;
            while (i < j)
            {
                int h = i + (j - i) / 2;
                var entry = entries[h];
                if (c < entry.Start)
                {
                    j = h;
                }
                else if (entry.End < c)
                {
                    i = h + 1;
                }
                else if (entry.Offset == 0)
                {
                    unchecked
                    {
                        return new GlyphIndex((ushort)(c + entry.Delta));
                    }
                }
                else
                {
                    uint xoffset = (uint)entry.Offset + 2 * (uint)(h - entries.Length + (c - entry.Start));
                    if (xoffset > indexesLength || xoffset + 2 > indexesLength)
                    {
                        throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
                    }
                    b ??= new Buffer();
                    var x = Src.View(ff.src, (int)(indexesBase + xoffset), 2);
                    return new GlyphIndex(Src.U16(x));
                }
            }
            return 0;
        };
    }

    private static GlyphIndexFunc MakeCachedGlyphIndexFormat6(Font f, uint offset, uint length)
    {
        const uint headerSize = 10;
        if (offset + headerSize > f.cmap.Length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }
        var buf = Src.View(f.src, (int)(f.cmap.Offset + offset), (int)headerSize);
        offset += headerSize;

        int firstCode = Src.U16(buf, 6);
        uint entryCount = Src.U16(buf, 8);

        uint eLength = 2 * entryCount;
        if (offset + eLength > f.cmap.Length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }

        byte[] ebuf;
        if (entryCount != 0)
        {
            ebuf = Src.View(f.src, (int)(f.cmap.Offset + offset), (int)eLength);
        }
        else
        {
            ebuf = Array.Empty<byte>();
        }

        var entries = new ushort[entryCount];
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = Src.U16(ebuf, 2 * i);
        }

        return (ff, b, r) =>
        {
            if ((ushort)r < firstCode)
            {
                return 0;
            }
            int c = (ushort)r - firstCode;
            if (c >= entries.Length)
            {
                return 0;
            }
            return new GlyphIndex(entries[c]);
        };
    }

    private static GlyphIndexFunc MakeCachedGlyphIndexFormat12(Font f, uint offset, uint length)
    {
        const uint headerSize = 16;
        if (offset + headerSize > f.cmap.Length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }
        var buf = Src.View(f.src, (int)(f.cmap.Offset + offset), (int)headerSize);
        uint len = Src.U32(buf, 4);
        if (f.cmap.Length < offset || len > f.cmap.Length - offset)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }
        offset += headerSize;

        uint numGroups = Src.U32(buf, 12);
        if (numGroups > MaxCmapSegments)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedNumberOfCmapSegments);
        }

        if (headerSize + 12 * numGroups != len)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCmapTable);
        }
        var ebuf = Src.View(f.src, (int)(f.cmap.Offset + offset), (int)(12 * numGroups));

        var entries = new CmapEntry32[numGroups];
        for (int i = 0; i < numGroups; i++)
        {
            entries[i] = new CmapEntry32(
                start: Src.U32(ebuf, 12 * i),
                end: Src.U32(ebuf, 4 + 12 * i),
                delta: Src.U32(ebuf, 8 + 12 * i));
        }

        return (ff, b, r) =>
        {
            uint c = (uint)r;
            int i = 0, j = entries.Length;
            while (i < j)
            {
                int h = i + (j - i) / 2;
                var entry = entries[h];
                if (c < entry.Start)
                {
                    j = h;
                }
                else if (entry.End < c)
                {
                    i = h + 1;
                }
                else
                {
                    return new GlyphIndex((ushort)(c - entry.Start + entry.Delta));
                }
            }
            return 0;
        };
    }

    private readonly struct CmapEntry16
    {
        public readonly ushort End;
        public readonly ushort Start;
        public readonly ushort Delta;
        public readonly ushort Offset;

        public CmapEntry16(ushort end, ushort start, ushort delta, ushort offset)
        {
            End = end;
            Start = start;
            Delta = delta;
            Offset = offset;
        }
    }

    private readonly struct CmapEntry32
    {
        public readonly uint Start;
        public readonly uint End;
        public readonly uint Delta;

        public CmapEntry32(uint start, uint end, uint delta)
        {
            Start = start;
            End = end;
            Delta = delta;
        }
    }
}