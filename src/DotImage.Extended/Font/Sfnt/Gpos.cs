using System;
using System.Collections.Generic;

namespace DotImage.Extended.Font.Sfnt;

/// <summary>Returns the unscaled kerning value for the kerning pair a+b, or
/// throws <see cref="SfntErrors.ErrNotFound"/> if no kerning is specified.
/// Ported from sfnt.kernFunc.</summary>
internal delegate short KernFunc(GlyphIndex a, GlyphIndex b);

/// <summary>Returns the index into a PairPos table for the provided glyph, or
/// (0, false) if not covered. Ported from sfnt.indexLookupFunc.</summary>
internal delegate (int index, bool found) IndexLookupFunc(GlyphIndex gi);

/// <summary>Returns the class ID for the provided glyph (0 = default class).
/// Ported from sfnt.classLookupFunc.</summary>
internal delegate int ClassLookupFunc(GlyphIndex gi);

internal static class Gpos
{
    internal const uint HexScriptLatn = 0x6c61746e; // "latn"
    internal const uint HexScriptDFLT = 0x44464c54; // "DFLT"
    internal const uint HexFeatureKern = 0x6b65726e; // "kern"

    internal static KernFunc[] ParseGPOSKern(Font f)
    {
        if (f.gpos.Length == 0)
        {
            return Array.Empty<KernFunc>();
        }
        const int headerSize = 10;
        if (f.gpos.Length < headerSize)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidGPOSTable);
        }

        int gposBase = (int)f.gpos.Offset;
        var buf = Src.View(f.src, gposBase, headerSize);

        if (Src.U16(buf) != 1 || Src.U16(buf, 2) > 1)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedGPOSTable);
        }
        int scriptListOffset = Src.U16(buf, 4);
        int featureListOffset = Src.U16(buf, 6);
        int lookupListOffset = Src.U16(buf, 8);

        var featureIdxs = ParseGPOSScriptFeatures(f, gposBase + scriptListOffset, HexScriptLatn);
        if (featureIdxs.Count == 0)
        {
            featureIdxs = ParseGPOSScriptFeatures(f, gposBase + scriptListOffset, HexScriptDFLT);
            if (featureIdxs.Count == 0)
            {
                return Array.Empty<KernFunc>();
            }
        }

        var lookupIdx = ParseGPOSFeaturesLookup(f, gposBase + featureListOffset, featureIdxs, HexFeatureKern);

        (buf, int numLookupTables) = Src.VarLenView(f.src, gposBase + lookupListOffset, 2, 0, 2);

        var kernFuncs = new List<KernFunc>();
        foreach (int n in lookupIdx)
        {
            if (n > numLookupTables)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidGPOSTable);
            }
            int tableOffset = gposBase + lookupListOffset + Src.U16(buf, 2 + n * 2);

            (buf, int numSubTables) = Src.VarLenView(f.src, tableOffset, 8, 4, 2);
            ushort flags = Src.U16(buf, 2);

            var subTableOffsets = new int[numSubTables];
            for (int i = 0; i < numSubTables; i++)
            {
                subTableOffsets[i] = tableOffset + Src.U16(buf, 6 + i * 2);
            }

            switch (Src.U16(buf))
            {
                case 2: // PairPos table
                    break;
                case 9:
                    // Extension Positioning, add an additional u32 offset.
                    for (int i = 0; i < subTableOffsets.Length; i++)
                    {
                        buf = Src.View(f.src, subTableOffsets[i], 8);
                        if (Src.U16(buf) != 1)
                        {
                            throw new SfntFormatException(SfntErrors.ErrUnsupportedExtensionPosFormat);
                        }
                        if (Src.U16(buf, 2) != 2)
                        {
                            goto nextLookup;
                        }
                        subTableOffsets[i] += (int)Src.U32(buf, 4);
                    }
                    break;
                default:
                    goto nextLookup;
            }

            if ((flags & 0x0010) > 0)
            {
                // useMarkFilteringSet enabled; not supported.
                goto nextLookup;
            }

            foreach (int subTableOffset in subTableOffsets)
            {
                buf = Src.View(f.src, subTableOffset, 4);
                ushort format = Src.U16(buf);

                (buf, var lookupIndex) = MakeCachedCoverageLookup(f, subTableOffset + Src.U16(buf, 2));

                switch (format)
                {
                    case 1: // Adjustments for Glyph Pairs
                        var kern1 = ParsePairPosFormat1(f, subTableOffset, lookupIndex);
                        if (kern1 != null)
                        {
                            kernFuncs.Add(kern1);
                        }
                        break;
                    case 2: // Class Pair Adjustment
                        var kern2 = ParsePairPosFormat2(f, subTableOffset, lookupIndex);
                        if (kern2 != null)
                        {
                            kernFuncs.Add(kern2);
                        }
                        break;
                }
            }
            continue;

        nextLookup:
            ;
        }

        return kernFuncs.ToArray();
    }

    private static KernFunc? ParsePairPosFormat1(Font f, int offset, IndexLookupFunc lookupIndex)
    {
        (var buf, int nPairs) = Src.VarLenView(f.src, offset, 10, 8, 2);
        if (Src.U16(buf, 4) != 0x04 || Src.U16(buf, 6) != 0x00)
        {
            return null;
        }

        int lastPairSetOffset = 0;
        for (int n = 0; n < nPairs; n++)
        {
            int pairOffset = Src.U16(buf, 10 + n * 2);
            if (pairOffset > lastPairSetOffset)
            {
                lastPairSetOffset = pairOffset;
            }
        }
        buf = Src.View(f.src, offset + lastPairSetOffset, 2);
        int pairValueCount = Src.U16(buf);
        int lastPairSetLength = 2 + pairValueCount * 4;

        int length = lastPairSetOffset + lastPairSetLength;
        buf = Src.View(f.src, offset, length);

        return MakeCachedPairPosGlyph(lookupIndex, nPairs, buf);
    }

    private static KernFunc? ParsePairPosFormat2(Font f, int offset, IndexLookupFunc lookupIndex)
    {
        var buf = Src.View(f.src, offset, 16);
        if (Src.U16(buf, 4) != 0x04 || Src.U16(buf, 6) != 0x00)
        {
            return null;
        }
        int numClass1 = Src.U16(buf, 12);
        int numClass2 = Src.U16(buf, 14);
        int cdef1Offset = offset + Src.U16(buf, 8);
        int cdef2Offset = offset + Src.U16(buf, 10);

        (buf, var cdef1) = MakeCachedClassLookup(f, cdef1Offset);
        (buf, var cdef2) = MakeCachedClassLookup(f, cdef2Offset);

        buf = Src.View(f.src, offset + 16, numClass1 * numClass2 * 2);
        return MakeCachedPairPosClass(lookupIndex, numClass1, numClass2, cdef1, cdef2, buf);
    }

    private static List<int> ParseGPOSScriptFeatures(Font f, int offset, uint script)
    {
        (var buf, int numScriptTables) = Src.VarLenView(f.src, offset, 2, 0, 6);

        ushort scriptTableOffset = 0;
        for (int i = 0; i < numScriptTables; i++)
        {
            uint scriptTag = Src.U32(buf, 2 + i * 6);
            if (scriptTag == script)
            {
                scriptTableOffset = Src.U16(buf, 2 + i * 6 + 4);
                break;
            }
        }
        if (scriptTableOffset == 0)
        {
            return new List<int>();
        }

        buf = Src.View(f.src, offset + scriptTableOffset, 2);
        ushort defaultLangSysOffset = Src.U16(buf);
        if (defaultLangSysOffset == 0)
        {
            return new List<int>();
        }

        (buf, int numFeatures) = Src.VarLenView(f.src, offset + scriptTableOffset + defaultLangSysOffset, 6, 4, 2);
        var featureIdxs = new List<int>(numFeatures);
        for (int i = 0; i < numFeatures; i++)
        {
            featureIdxs.Add(Src.U16(buf, 6 + i * 2));
        }
        return featureIdxs;
    }

    private static List<int> ParseGPOSFeaturesLookup(Font f, int offset, List<int> featureIdxs, uint feature)
    {
        (var buf, int numFeatureTables) = Src.VarLenView(f.src, offset, 2, 0, 6);

        var lookupIdx = new List<int>(4);
        foreach (int fidx in featureIdxs)
        {
            if (fidx > numFeatureTables)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidGPOSTable);
            }
            uint featureTag = Src.U32(buf, 2 + fidx * 6);
            if (featureTag != feature)
            {
                continue;
            }
            int featureOffset = Src.U16(buf, 2 + fidx * 6 + 4);

            (buf, int numLookups) = Src.VarLenView(f.src, offset + featureOffset, 4, 2, 2);
            for (int i = 0; i < numLookups; i++)
            {
                lookupIdx.Add(Src.U16(buf, 4 + i * 2));
            }
        }
        return lookupIdx;
    }

    private static KernFunc MakeCachedPairPosGlyph(IndexLookupFunc cov, int num, byte[] buf)
    {
        var glyphs = new byte[buf.Length];
        Array.Copy(buf, glyphs, buf.Length);
        return (a, b) =>
        {
            var (idx, found) = cov(a);
            if (!found)
            {
                throw new SfntFormatException(SfntErrors.ErrNotFound);
            }
            if (idx >= num)
            {
                throw new SfntFormatException(SfntErrors.ErrNotFound);
            }
            int offset = Src.U16(glyphs, 10 + idx * 2);
            if (offset + 1 >= glyphs.Length)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidGPOSTable);
            }

            int count = Src.U16(glyphs, offset);
            for (int i = 0; i < count; i++)
            {
                if (offset + 2 + i * 4 + 4 > glyphs.Length)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidGPOSTable);
                }
                int secondGlyphIndex = Src.U16(glyphs, offset + 2 + i * 4);
                if (secondGlyphIndex == b.Value)
                {
                    return unchecked((short)Src.U16(glyphs, offset + 2 + i * 4 + 2));
                }
                if (secondGlyphIndex > b.Value)
                {
                    throw new SfntFormatException(SfntErrors.ErrNotFound);
                }
            }
            throw new SfntFormatException(SfntErrors.ErrNotFound);
        };
    }

    private static KernFunc MakeCachedPairPosClass(IndexLookupFunc cov, int num1, int num2, ClassLookupFunc cdef1, ClassLookupFunc cdef2, byte[] buf)
    {
        var glyphs = new byte[buf.Length];
        Array.Copy(buf, glyphs, buf.Length);
        return (a, b) =>
        {
            var (_, found) = cov(a);
            if (!found)
            {
                throw new SfntFormatException(SfntErrors.ErrNotFound);
            }
            int idxa = cdef1(a);
            int idxb = cdef2(b);
            if (idxa < 0 || idxa >= num1 || idxb < 0 || idxb >= num2)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidGPOSTable);
            }
            return unchecked((short)Src.U16(glyphs, (idxb + idxa * num2) * 2));
        };
    }

    private static (byte[] buf, IndexLookupFunc lookup) MakeCachedCoverageLookup(Font f, int offset)
    {
        var buf = Src.View(f.src, offset, 2);
        switch (Src.U16(buf))
        {
            case 1:
                (buf, _) = Src.VarLenView(f.src, offset, 4, 2, 2);
                return (buf, MakeCachedCoverageList(Slice(buf, 2)));
            case 2:
                (buf, _) = Src.VarLenView(f.src, offset, 4, 2, 6);
                return (buf, MakeCachedCoverageRange(Slice(buf, 2)));
            default:
                throw new SfntFormatException(SfntErrors.ErrUnsupportedCoverageFormat);
        }
    }

    private static IndexLookupFunc MakeCachedCoverageList(byte[] buf)
    {
        int num = Src.U16(buf);
        var list = new byte[buf.Length - 2];
        Array.Copy(buf, 2, list, 0, list.Length);
        return gi =>
        {
            int idx = SortSearch(num, i => gi.Value <= Src.U16(list, i * 2));
            if (idx < num && Src.U16(list, idx * 2) == gi.Value)
            {
                return (idx, true);
            }
            return (0, false);
        };
    }

    private static IndexLookupFunc MakeCachedCoverageRange(byte[] buf)
    {
        int num = Src.U16(buf);
        var ranges = new byte[buf.Length - 2];
        Array.Copy(buf, 2, ranges, 0, ranges.Length);
        return gi =>
        {
            if (num == 0)
            {
                return (0, false);
            }

            int idx = SortSearch(num, i => gi.Value <= Src.U16(ranges, i * 6));
            if (idx < num)
            {
                ushort start = Src.U16(ranges, idx * 6);
                if (gi.Value == start)
                {
                    return (Src.U16(ranges, idx * 6 + 4), true);
                }
            }
            if (idx > 0)
            {
                idx--;
                ushort start = Src.U16(ranges, idx * 6);
                ushort end = Src.U16(ranges, idx * 6 + 2);
                if (gi.Value >= start && gi.Value <= end)
                {
                    return (Src.U16(ranges, idx * 6 + 4) + gi.Value - start, true);
                }
            }
            return (0, false);
        };
    }

    private static (byte[] buf, ClassLookupFunc lookup) MakeCachedClassLookup(Font f, int offset)
    {
        var buf = Src.View(f.src, offset, 2);
        switch (Src.U16(buf))
        {
            case 1:
                (buf, _) = Src.VarLenView(f.src, offset, 6, 4, 2);
                return (buf, MakeCachedClassLookupFormat1(buf));
            case 2:
                (buf, _) = Src.VarLenView(f.src, offset, 4, 2, 6);
                return (buf, MakeCachedClassLookupFormat2(buf));
            default:
                throw new SfntFormatException(SfntErrors.ErrUnsupportedClassDefFormat);
        }
    }

    private static ClassLookupFunc MakeCachedClassLookupFormat1(byte[] buf)
    {
        int startGI = Src.U16(buf, 2);
        int num = Src.U16(buf, 4);
        var classIDs = new byte[buf.Length - 4];
        Array.Copy(buf, 6, classIDs, 0, buf.Length - 6);
        return gi =>
        {
            if (gi.Value < startGI || gi.Value >= startGI + num)
            {
                return 0;
            }
            return Src.U16(classIDs, ((int)gi.Value - startGI) * 2);
        };
    }

    private static ClassLookupFunc MakeCachedClassLookupFormat2(byte[] buf)
    {
        int num = Src.U16(buf, 2);
        var classRanges = new byte[buf.Length - 2];
        Array.Copy(buf, 4, classRanges, 0, buf.Length - 4);
        return gi =>
        {
            if (num == 0)
            {
                return 0;
            }

            int idx = SortSearch(num, i => gi.Value <= Src.U16(classRanges, i * 6));
            if (idx < num)
            {
                ushort start = Src.U16(classRanges, idx * 6);
                if (gi.Value == start)
                {
                    return Src.U16(classRanges, idx * 6 + 4);
                }
            }
            if (idx > 0)
            {
                idx--;
                ushort start = Src.U16(classRanges, idx * 6);
                ushort end = Src.U16(classRanges, idx * 6 + 2);
                if (gi.Value >= start && gi.Value <= end)
                {
                    return Src.U16(classRanges, idx * 6 + 4);
                }
            }
            return 0;
        };
    }

    private static byte[] Slice(byte[] data, int start) =>
        Src.View(data, start, data.Length - start);

    // Binary search mirroring sort.Search(n, pred): smallest i with pred(i).
    private static int SortSearch(int n, Func<int, bool> pred)
    {
        int i = 0, j = n;
        while (i < j)
        {
            int h = i + (j - i) / 2;
            if (pred(h))
            {
                j = h;
            }
            else
            {
                i = h + 1;
            }
        }
        return i;
    }
}