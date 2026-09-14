using System;
using DotImage.Extended.Math.Fixed;

namespace DotImage.Extended.Font.Sfnt;

internal readonly struct CompoundElem
{
    public readonly GlyphIndex GlyphIndex;
    public readonly short Dx;
    public readonly short Dy;
    public readonly bool HasTransform;
    public readonly short TransformXX;
    public readonly short TransformXY;
    public readonly short TransformYX;
    public readonly short TransformYY;

    public CompoundElem(GlyphIndex glyphIndex, short dx, short dy, bool hasTransform,
        short txx, short txy, short tyx, short tyy)
    {
        GlyphIndex = glyphIndex;
        Dx = dx;
        Dy = dy;
        HasTransform = hasTransform;
        TransformXX = txx;
        TransformXY = txy;
        TransformYX = tyx;
        TransformYY = tyy;
    }
}

internal static class TrueType
{
    internal const int MaxCompoundRecursionDepth = 8;
    internal const int MaxCompoundStackSize = 64;

    // Flags for simple (non-compound) glyphs.
    internal const byte FlagOnCurve = 1 << 0;
    internal const byte FlagXShortVector = 1 << 1;
    internal const byte FlagYShortVector = 1 << 2;
    internal const byte FlagRepeat = 1 << 3;
    internal const byte FlagPositiveXShortVector = 1 << 4;
    internal const byte FlagThisXIsSame = 1 << 4;
    internal const byte FlagPositiveYShortVector = 1 << 5;
    internal const byte FlagThisYIsSame = 1 << 5;

    // Flags for compound glyphs.
    internal const ushort FlagArg1And2AreWords = 1 << 0;
    internal const ushort FlagArgsAreXYValues = 1 << 1;
    internal const ushort FlagRoundXYToGrid = 1 << 2;
    internal const ushort FlagWeHaveAScale = 1 << 3;
    internal const ushort FlagMoreComponents = 1 << 5;
    internal const ushort FlagWeHaveAnXAndYScale = 1 << 6;
    internal const ushort FlagWeHaveATwoByTwo = 1 << 7;
    internal const ushort FlagWeHaveInstructions = 1 << 8;
    internal const ushort FlagUseMyMetrics = 1 << 9;
    internal const ushort FlagOverlapCompound = 1 << 10;
    internal const ushort FlagScaledComponentOffset = 1 << 11;
    internal const ushort FlagUnscaledComponentOffset = 1 << 12;

    internal static int[] ParseLoca(byte[] src, Table loca, uint glyfOffset, bool indexToLocFormat, int numGlyphs)
    {
        if (indexToLocFormat)
        {
            if (loca.Length != 4 * (uint)(numGlyphs + 1))
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidLocaTable);
            }
        }
        else
        {
            if (loca.Length != 2 * (uint)(numGlyphs + 1))
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidLocaTable);
            }
        }

        var locations = new int[numGlyphs + 1];
        var buf = Src.View(src, (int)loca.Offset, (int)loca.Length);

        if (indexToLocFormat)
        {
            for (int i = 0; i < locations.Length; i++)
            {
                locations[i] = (int)(Src.U32(buf, 4 * i) + glyfOffset);
            }
        }
        else
        {
            for (int i = 0; i < locations.Length; i++)
            {
                locations[i] = 2 * Src.U16(buf, 2 * i) + (int)glyfOffset;
            }
        }
        return locations;
    }

    // "Each glyph begins with the following [10 byte] header".
    private const int GlyfHeaderLen = 10;

    internal static void LoadGlyf(Font f, Buffer b, GlyphIndex x, uint stackBottom, uint recursionDepth)
    {
        var (data, _, _) = f.ViewGlyphData(b, x);
        if (data.Length == 0)
        {
            return;
        }
        if (data.Length < GlyfHeaderLen)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
        }
        int index = GlyfHeaderLen;

        short numContours = unchecked((short)Src.U16(data));
        int numPoints = 0;
        switch (numContours)
        {
            case -1:
                break;
            case 0:
                return;
            default:
                if (numContours < 0)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
                }
                index += 2 * numContours;
                if (index > data.Length)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
                }
                // The +1 for numPoints is because the value in the file format
                // is inclusive, but slice[:index] semantics are exclusive.
                numPoints = 1 + Src.U16(data, index - 2);
                break;
        }

        if (numContours < 0)
        {
            LoadCompoundGlyf(f, b, Slice(data, GlyfHeaderLen), stackBottom, recursionDepth);
            return;
        }

        // Skip the hinting instructions.
        index += 2;
        if (index > data.Length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
        }
        int hintsLength = Src.U16(data, index - 2);
        index += hintsLength;
        if (index > data.Length)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
        }

        int flagIndex = index;
        var xy = FindXYIndexes(data, index, numPoints);
        if (!xy.ok)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
        }
        var g = new GlyfIter
        {
            Data = data,
            FlagIndex = flagIndex,
            XIndex = xy.xIndex,
            YIndex = xy.yIndex,
            EndIndex = GlyfHeaderLen,
            PrevEnd = -1,
            FinalEnd = numPoints - 1,
            NumContours = numContours,
        };
        while (g.NextContour())
        {
            while (g.NextSegment())
            {
                b.Segments.Add(g.Seg);
            }
        }
        if (g.Err != null)
        {
            throw g.Err;
        }
    }

    private static byte[] Slice(byte[] data, int start) =>
        Src.View(data, start, data.Length - start);

    private static (int xIndex, int yIndex, bool ok) FindXYIndexes(byte[] data, int index, int numPoints)
    {
        int xDataLen = 0;
        int yDataLen = 0;
        for (int i = 0; ; )
        {
            if (i > numPoints)
            {
                return (0, 0, false);
            }
            if (i == numPoints)
            {
                break;
            }

            int repeatCount = 1;
            if (index >= data.Length)
            {
                return (0, 0, false);
            }
            byte flag = data[index];
            index++;
            if ((flag & FlagRepeat) != 0)
            {
                if (index >= data.Length)
                {
                    return (0, 0, false);
                }
                repeatCount += data[index];
                index++;
            }

            int xSize = 0;
            if ((flag & FlagXShortVector) != 0)
            {
                xSize = 1;
            }
            else if ((flag & FlagThisXIsSame) == 0)
            {
                xSize = 2;
            }
            xDataLen += xSize * repeatCount;

            int ySize = 0;
            if ((flag & FlagYShortVector) != 0)
            {
                ySize = 1;
            }
            else if ((flag & FlagThisYIsSame) == 0)
            {
                ySize = 2;
            }
            yDataLen += ySize * repeatCount;

            i += repeatCount;
        }
        if (index + xDataLen + yDataLen > data.Length)
        {
            return (0, 0, false);
        }
        return (index, index + xDataLen, true);
    }

    internal static void LoadCompoundGlyf(Font f, Buffer b, byte[] data, uint stackBottom, uint recursionDepth)
    {
        recursionDepth++;
        if (recursionDepth == MaxCompoundRecursionDepth)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedCompoundGlyph);
        }

        uint stackTop = stackBottom;
        var elems = new CompoundElem[MaxCompoundStackSize];
        int elemCount = 0;
        while (true)
        {
            if (stackTop >= MaxCompoundStackSize)
            {
                throw new SfntFormatException(SfntErrors.ErrUnsupportedCompoundGlyph);
            }
            stackTop++;

            if (data.Length < 4)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
            }
            ushort flags = Src.U16(data);
            var glyphIndex = new GlyphIndex(Src.U16(data, 2));
            short dx, dy;
            if ((flags & FlagArg1And2AreWords) == 0)
            {
                if (data.Length < 6)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
                }
                dx = unchecked((sbyte)data[4]);
                dy = unchecked((sbyte)data[5]);
                data = Slice(data, 6);
            }
            else
            {
                if (data.Length < 8)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
                }
                dx = unchecked((short)Src.U16(data, 4));
                dy = unchecked((short)Src.U16(data, 6));
                data = Slice(data, 8);
            }

            if ((flags & FlagArgsAreXYValues) == 0)
            {
                throw new SfntFormatException(SfntErrors.ErrUnsupportedCompoundGlyph);
            }
            bool hasTransform = (flags & (FlagWeHaveAScale | FlagWeHaveAnXAndYScale | FlagWeHaveATwoByTwo)) != 0;
            short txx = 0, txy = 0, tyx = 0, tyy = 0;
            if (hasTransform)
            {
                if ((flags & FlagWeHaveAScale) != 0)
                {
                    if (data.Length < 2)
                    {
                        throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
                    }
                    txx = unchecked((short)Src.U16(data));
                    tyy = txx;
                    data = Slice(data, 2);
                }
                else if ((flags & FlagWeHaveAnXAndYScale) != 0)
                {
                    if (data.Length < 4)
                    {
                        throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
                    }
                    txx = unchecked((short)Src.U16(data, 0));
                    tyy = unchecked((short)Src.U16(data, 2));
                    data = Slice(data, 4);
                }
                else if ((flags & FlagWeHaveATwoByTwo) != 0)
                {
                    if (data.Length < 8)
                    {
                        throw new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
                    }
                    txx = unchecked((short)Src.U16(data, 0));
                    txy = unchecked((short)Src.U16(data, 2));
                    tyx = unchecked((short)Src.U16(data, 4));
                    tyy = unchecked((short)Src.U16(data, 6));
                    data = Slice(data, 8);
                }
            }
            elems[elemCount++] = new CompoundElem(glyphIndex, dx, dy, hasTransform, txx, txy, tyx, tyy);

            if ((flags & FlagMoreComponents) == 0)
            {
                break;
            }
        }

        for (int i = 0; i < elemCount; i++)
        {
            var elem = elems[i];
            int baseCount = b.Segments.Count;
            LoadGlyf(f, b, elem.GlyphIndex, stackTop, recursionDepth);
            var dx = Int26_6.FromRaw(elem.Dx);
            var dy = Int26_6.FromRaw(elem.Dy);
            for (int j = baseCount; j < b.Segments.Count; j++)
            {
                var s = b.Segments[j];
                if (elem.HasTransform)
                {
                    s.P0 = SegmentMath.Tform(elem.TransformXX, elem.TransformXY, elem.TransformYX, elem.TransformYY, dx, dy, s.P0);
                    s.P1 = SegmentMath.Tform(elem.TransformXX, elem.TransformXY, elem.TransformYX, elem.TransformYY, dx, dy, s.P1);
                    s.P2 = SegmentMath.Tform(elem.TransformXX, elem.TransformXY, elem.TransformYX, elem.TransformYY, dx, dy, s.P2);
                }
                else
                {
                    s.P0 = new Point26_6(s.P0.X + dx, s.P0.Y + dy);
                    s.P1 = new Point26_6(s.P1.X + dx, s.P1.Y + dy);
                    s.P2 = new Point26_6(s.P2.X + dx, s.P2.Y + dy);
                }
                b.Segments[j] = s;
            }
        }
    }

    private sealed class GlyfIter
    {
        public byte[] Data = Array.Empty<byte>();
        public Exception? Err;

        public int FlagIndex;
        public int XIndex;
        public int YIndex;

        public int EndIndex;
        public int PrevEnd;
        public int FinalEnd;

        public int C;
        public int NumContours;
        public int P;
        public int NPoints;

        public short X;
        public short Y;
        public bool On;
        public byte Flag;
        public byte Repeats;

        public bool Closing;
        public bool Closed;
        public bool FirstOnCurveValid;
        public bool FirstOffCurveValid;
        public bool LastOffCurveValid;
        public Point26_6 FirstOnCurve;
        public Point26_6 FirstOffCurve;
        public Point26_6 LastOffCurve;
        public Segment Seg;

        public bool NextContour()
        {
            if (C == NumContours)
            {
                if (PrevEnd != FinalEnd)
                {
Err = new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
                }
                return false;
            }
            C++;

            int end = Src.U16(Data, EndIndex);
            EndIndex += 2;
            if (end <= PrevEnd || FinalEnd < end)
            {
                Err = new SfntFormatException(SfntErrors.ErrInvalidGlyphData);
                return false;
            }
            NPoints = end - PrevEnd;
            P = 0;
            PrevEnd = end;

            Closing = false;
            Closed = false;
            FirstOnCurveValid = false;
            FirstOffCurveValid = false;
            LastOffCurveValid = false;

            return true;
        }

        public void Close()
        {
            if (!FirstOffCurveValid && !LastOffCurveValid)
            {
                Closed = true;
                Seg = new Segment(SegmentOp.LineTo, FirstOnCurve);
            }
            else if (!FirstOffCurveValid && LastOffCurveValid)
            {
                Closed = true;
                Seg = new Segment(SegmentOp.QuadTo, LastOffCurve, FirstOnCurve);
            }
            else if (FirstOffCurveValid && !LastOffCurveValid)
            {
                Closed = true;
                Seg = new Segment(SegmentOp.QuadTo, FirstOffCurve, FirstOnCurve);
            }
            else
            {
                LastOffCurveValid = false;
                Seg = new Segment(SegmentOp.QuadTo, LastOffCurve, SegmentMath.MidPoint(LastOffCurve, FirstOffCurve));
            }
        }

        public bool NextSegment()
        {
            while (!Closed)
            {
                if (Closing || !NextPoint())
                {
                    Closing = true;
                    Close();
                    return true;
                }

                var p = new Point26_6(Int26_6.FromRaw(X), Int26_6.FromRaw(Y));

                if (!FirstOnCurveValid)
                {
                    if (On)
                    {
                        FirstOnCurve = p;
                        FirstOnCurveValid = true;
                        Seg = new Segment(SegmentOp.MoveTo, p);
                        return true;
                    }
                    else if (!FirstOffCurveValid)
                    {
                        FirstOffCurve = p;
                        FirstOffCurveValid = true;
                        continue;
                    }
                    else
                    {
                        FirstOnCurve = SegmentMath.MidPoint(FirstOffCurve, p);
                        FirstOnCurveValid = true;
                        LastOffCurve = p;
                        LastOffCurveValid = true;
                        Seg = new Segment(SegmentOp.MoveTo, FirstOnCurve);
                        return true;
                    }
                }
                else if (!LastOffCurveValid)
                {
                    if (!On)
                    {
                        LastOffCurve = p;
                        LastOffCurveValid = true;
                        continue;
                    }
                    else
                    {
                        Seg = new Segment(SegmentOp.LineTo, p);
                        return true;
                    }
                }
                else
                {
                    if (!On)
                    {
                        Seg = new Segment(SegmentOp.QuadTo, LastOffCurve, SegmentMath.MidPoint(LastOffCurve, p));
                        LastOffCurve = p;
                        LastOffCurveValid = true;
                        return true;
                    }
                    else
                    {
                        Seg = new Segment(SegmentOp.QuadTo, LastOffCurve, p);
                        LastOffCurveValid = false;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool NextPoint()
        {
            if (P == NPoints)
            {
                return false;
            }
            P++;

            if (Repeats > 0)
            {
                Repeats--;
            }
            else
            {
                Flag = Data[FlagIndex];
                FlagIndex++;
                if ((Flag & FlagRepeat) != 0)
                {
                    Repeats = Data[FlagIndex];
                    FlagIndex++;
                }
            }

            if ((Flag & FlagXShortVector) != 0)
            {
                if ((Flag & FlagPositiveXShortVector) != 0)
                {
                    X += (short)Data[XIndex];
                }
                else
                {
                    X -= (short)Data[XIndex];
                }
                XIndex += 1;
            }
            else if ((Flag & FlagThisXIsSame) == 0)
            {
                X += unchecked((short)Src.U16(Data, XIndex));
                XIndex += 2;
            }

            if ((Flag & FlagYShortVector) != 0)
            {
                if ((Flag & FlagPositiveYShortVector) != 0)
                {
                    Y += (short)Data[YIndex];
                }
                else
                {
                    Y -= (short)Data[YIndex];
                }
                YIndex += 1;
            }
            else if ((Flag & FlagThisYIsSame) == 0)
            {
                Y += unchecked((short)Src.U16(Data, YIndex));
                YIndex += 2;
            }

            On = (Flag & FlagOnCurve) != 0;
            return true;
        }
    }
}