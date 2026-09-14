using System;
using System.Globalization;
using DotImage.Extended.Math.Fixed;

namespace DotImage.Extended.Font.Sfnt;

// Ported from golang.org/x/image/font/sfnt/postscript.go.
//
// Compact Font Format (CFF) fonts are written in PostScript, a stack-based
// programming language. A DICT is a key-value map expressed in reverse Polish
// notation. The actual glyph vectors are encoded in Type 2 Charstrings.

internal static class CffConsts
{
    // 5176.CFF.pdf section 4 "DICT Data": "An operator may be preceded by up
    // to a maximum of 48 operands". 5177.Type2.pdf Appendix B: "Argument stack
    // 48".
    internal const int PsArgStackSize = 48;
    // 5177.Type2.pdf Appendix B "Subr nesting, stack limit 10".
    internal const int PsCallStackSize = 10;

    internal const int MaxNumSubroutines = 40000;
    internal const int MaxGlyphDataLength = 64 * 1024;
    internal const int MaxHintBits = 256;
    internal const int MaxNumFontDicts = 256;
    internal const int MaxRealNumberStrLen = 64;
    internal const int MaxNibbleDefsLength = 2; // len("E-")

    // 5176.CFF.pdf section 4 "DICT Data": "Two-byte operators have an initial
    // escape byte of 12".
    internal const byte EscapeByte = 12;

    // nibbleDefs encodes 5176.CFF.pdf Table 5 "Nibble Definitions".
    internal static readonly string[] NibbleDefs =
    {
        "0", "1", "2", "3", "4", "5", "6", "7",
        "8", "9", ".", "E", "E-", "", "-", "",
    };
}

/// <summary>Holds a CFF font's Font Dict Select data (5176.CFF.pdf section 19
/// "FDSelect").</summary>
internal struct FdSelect
{
    public byte Format;
    public ushort NumRanges;
    public int Offset;

    public int Lookup(Font f, GlyphIndex x)
    {
        switch (Format)
        {
            case 0:
                var b0 = Src.View(f.src, Offset + (int)x.Value, 1);
                return b0[0];
            case 3:
                int lo = 0, hi = NumRanges;
                while (lo < hi)
                {
                    int i = (lo + hi) / 2;
                    var buf = Src.View(f.src, Offset + 3 * i, 3 + 2);
                    // buf holds the range [xlo, xhi).
                    ushort xlo = Src.U16(buf, 0);
                    if (x.Value < xlo)
                    {
                        hi = i;
                        continue;
                    }
                    ushort xhi = Src.U16(buf, 3);
                    if (xhi <= x.Value)
                    {
                        lo = i + 1;
                        continue;
                    }
                    return buf[2];
                }
                break;
        }
        throw new SfntFormatException(SfntErrors.ErrNotFound);
    }
}

/// <summary>The parsed glyph data for a font. For PostScript fonts, the
/// charstring for the i'th glyph is in src[Locations[i]:Locations[i+1]], and
/// the bytecode for the i'th global or local subroutine is in
/// src[x[i]:x[i+1]]. The []int slices' lengths equal 1 plus the number of
/// glyphs / subroutines.</summary>
internal struct GlyphData
{
    public int[] Locations;
    public int[]? GSubrs;
    public int[]? SingleSubrs;
    public int[][]? MultiSubrs;
    public FdSelect FdSelect;
    public bool IsColorBitmap;
}

/// <summary>Parses a CFF table from an SFNT font. Ported from sfnt.cffParser.
/// Errors are thrown (they would otherwise be returned via err).</summary>
internal sealed class CffParser
{
    private readonly byte[] src;
    private readonly int baseOffset;
    private int offset;
    private readonly int end;

    private byte[] buf = Array.Empty<byte>();
    private readonly int[] locBuf = new int[2];

    private readonly PsInterpreter psi = new();

    public CffParser(byte[] src, int baseOffset, int end)
    {
        this.src = src;
        this.baseOffset = baseOffset;
        offset = baseOffset;
        this.end = end;
    }

    public static GlyphData Parse(Font f, int numGlyphs)
    {
        var p = new CffParser(f.src, (int)f.cff.Offset, (int)(f.cff.Offset + f.cff.Length));
        return p.ParseTopLevel(numGlyphs);
    }

    private GlyphData ParseTopLevel(int numGlyphs)
    {
        // Parse the header.
        Read(4);
        if (buf[0] != 1 || buf[1] != 0 || buf[2] != 4)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedCFFVersion);
        }

        GlyphData ret = default;

        // Parse the Name INDEX.
        {
            var (count, offSize) = ParseIndexHeader();
            // The Name INDEX in the CFF must contain only one entry.
            if (count != 1)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            ParseIndexLocations(locBuf, count, offSize);
            offset = locBuf[1];
        }

        // Parse the Top DICT INDEX.
        psi.topDict.Initialize();
        {
            var (count, offSize) = ParseIndexHeader();
            // The count here should match the count of the Name INDEX, which
            // is 1.
            if (count != 1)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            ParseIndexLocations(locBuf, count, offSize);
            Read(locBuf[1] - locBuf[0]);
            psi.Run(PsContext.TopDict, buf.AsMemory(), 0, 0);
        }

        // Skip the String INDEX.
        {
            var (count, offSize) = ParseIndexHeader();
            if (count != 0)
            {
                // Read the last location. Locations are off by 1 byte.
                Skip(count * offSize);
                Read(offSize);
                int loc = unchecked((int)Src.BigEndian(buf, 0, offSize)) - 1;
                if (end - offset < loc)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
                }
                Skip(loc);
            }
        }

        // Parse the Global Subrs [Subroutines] INDEX.
        {
            var (count, offSize) = ParseIndexHeader();
            if (count != 0)
            {
                if (count > CffConsts.MaxNumSubroutines)
                {
                    throw new SfntFormatException(SfntErrors.ErrUnsupportedNumberOfSubroutines);
                }
                ret.GSubrs = new int[count + 1];
                ParseIndexLocations(ret.GSubrs, count, offSize);
            }
        }

        // Parse the CharStrings INDEX, whose location was found in the Top
        // DICT.
        {
            SeekFromBase(psi.topDict.charStringsOffset);
            var (count, offSize) = ParseIndexHeader();
            if (count == 0 || count != numGlyphs)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            ret.Locations = new int[count + 1];
            ParseIndexLocations(ret.Locations, count, offSize);
        }

        if (!psi.topDict.isCIDFont)
        {
            // Parse the Private DICT, whose location was found in the Top
            // DICT.
            ret.SingleSubrs = ParsePrivateDICT(psi.topDict.privateDictOffset, psi.topDict.privateDictLength);
        }
        else
        {
            // Parse the Font Dict Select data, whose location was found in the
            // Top DICT.
            ret.FdSelect = ParseFDSelect(numGlyphs);

            // Parse the Font Dicts. Each one contains its own Private DICT.
            SeekFromBase(psi.topDict.fdArray);
            var (count, offSize) = ParseIndexHeader();
            if (count > CffConsts.MaxNumFontDicts)
            {
                throw new SfntFormatException(SfntErrors.ErrUnsupportedNumberOfFontDicts);
            }

            var fdLocations = new int[count + 1];
            ParseIndexLocations(fdLocations, count, offSize);

            var privateDicts = new (int offset, int length)[count];
            for (int i = 0; i < privateDicts.Length; i++)
            {
                int length = fdLocations[i + 1] - fdLocations[i];
                Read(length);
                psi.topDict.Initialize();
                psi.Run(PsContext.TopDict, buf.AsMemory(), 0, 0);
                privateDicts[i] = (psi.topDict.privateDictOffset, psi.topDict.privateDictLength);
            }

            ret.MultiSubrs = new int[count][];
            for (int i = 0; i < privateDicts.Length; i++)
            {
                ret.MultiSubrs[i] = ParsePrivateDICT(privateDicts[i].offset, privateDicts[i].length) ?? null!;
            }
        }

        return ret;
    }

    // ParseFDSelect parses the Font Dict Select data.
    private FdSelect ParseFDSelect(int numGlyphs)
    {
        SeekFromBase(psi.topDict.fdSelect);
        Read(1);
        var ret = new FdSelect { Format = buf[0] };
        switch (ret.Format)
        {
            case 0:
                if (end - offset < numGlyphs)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
                }
                ret.Offset = offset;
                return ret;
            case 3:
                Read(2);
                ret.NumRanges = Src.U16(buf);
                if (end - offset < 3 * (int)ret.NumRanges + 2)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
                }
                ret.Offset = offset;
                return ret;
        }
        throw new SfntFormatException(SfntErrors.ErrUnsupportedCFFFDSelectTable);
    }

    private int[]? ParsePrivateDICT(int offs, int length)
    {
        psi.privateDict.Initialize();
        if (length != 0)
        {
            int fullLength = end - baseOffset;
            if (offs <= 0 || fullLength < offs || fullLength - offs < length || length < 0)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            offset = baseOffset + offs;
            Read(length);
            psi.Run(PsContext.PrivateDict, buf.AsMemory(), 0, 0);
        }

        // Parse the Local Subrs [Subroutines] INDEX, whose location was found
        // in the Private DICT.
        if (psi.privateDict.subrsOffset != 0)
        {
            SeekFromBase(offs + psi.privateDict.subrsOffset);
            var (count, offSize) = ParseIndexHeader();
            if (count != 0)
            {
                if (count > CffConsts.MaxNumSubroutines)
                {
                    throw new SfntFormatException(SfntErrors.ErrUnsupportedNumberOfSubroutines);
                }
                var subrs = new int[count + 1];
                ParseIndexLocations(subrs, count, offSize);
                return subrs;
            }
        }
        return null;
    }

    // Read sets buf to the n bytes from offset to offset+n, and advances
    // offset by n.
    private void Read(int n)
    {
        if (n < 0 || end - offset < n)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        buf = Src.View(src, offset, n);
        offset += n;
    }

    private void Skip(int n)
    {
        if (end - offset < n)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        offset += n;
    }

    private void SeekFromBase(int offs)
    {
        if (offs < 0 || end - baseOffset < offs)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        offset = baseOffset + offs;
    }

    private (int count, int offSize) ParseIndexHeader()
    {
        Read(2);
        int count = Src.U16(buf);
        // An empty INDEX is represented by a count field with a 0 value and no
        // additional fields. Thus, the total size of an empty INDEX is 2
        // bytes.
        if (count == 0)
        {
            return (0, 0);
        }
        Read(1);
        int offSize = buf[0];
        if (offSize < 1 || 4 < offSize)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        return (count, offSize);
    }

    private void ParseIndexLocations(int[] dst, int count, int offSize)
    {
        if (count == 0)
        {
            return;
        }
        Read(dst.Length * offSize);

        int prev = 0;
        for (int i = 0; i < dst.Length; i++)
        {
            int loc = unchecked((int)Src.BigEndian(buf, i * offSize, offSize));

            // Locations are off by 1 byte; offsets in the offset array are
            // relative to the byte that precedes the object data. This ensures
            // that every object has a corresponding offset which is always
            // nonzero.
            if (loc == 0)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            loc--;

            // "Therefore the first element of the offset array is always 1"
            // before correcting for the off-by-1.
            if (i == 0)
            {
                if (loc != 0)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
                }
            }
            else if (loc <= prev)
            {
                // Check that locations are increasing.
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }

            // Check that locations are in bounds.
            if (end - offset < loc)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }

            dst[i] = offset + loc;
            prev = loc;
        }
    }
}

internal enum PsContext : uint
{
    TopDict,
    PrivateDict,
    Type2Charstring,
}

internal struct PsCallStackEntry
{
    public readonly uint Offset;
    public readonly uint Length;

    public PsCallStackEntry(uint offset, uint length)
    {
        Offset = offset;
        Length = length;
    }
}

/// <summary>Contains fields specific to the Top DICT context.</summary>
internal sealed class PsTopDictData
{
    public int charStringsOffset;
    public int fdArray;
    public int fdSelect;
    public bool isCIDFont;
    public int privateDictOffset;
    public int privateDictLength;

    public void Initialize()
    {
        charStringsOffset = 0;
        fdArray = 0;
        fdSelect = 0;
        isCIDFont = false;
        privateDictOffset = 0;
        privateDictLength = 0;
    }
}

/// <summary>Contains fields specific to the Private DICT context.</summary>
internal sealed class PsPrivateDictData
{
    public int subrsOffset;

    public void Initialize() => subrsOffset = 0;
}

/// <summary>Contains fields specific to the Type 2 Charstrings context.</summary>
internal sealed class PsType2CharstringsData
{
    public Font f = null!;
    public Buffer b = null!;
    public int x;
    public int y;
    public int firstX;
    public int firstY;
    public int hintBits;
    public bool seenWidth;
    public bool ended;
    public GlyphIndex glyphIndex;
    // fdSelectIndexPlusOne is the result of the Font Dict Select lookup, plus
    // one, so that zero denotes either unused (for CFF fonts with a single
    // Font Dict) or lazily evaluated.
    public int fdSelectIndexPlusOne;

    public void Initialize(Font f, Buffer b, GlyphIndex glyphIndex)
    {
        this.f = f;
        this.b = b;
        x = 0;
        y = 0;
        firstX = 0;
        firstY = 0;
        hintBits = 0;
        seenWidth = false;
        ended = false;
        this.glyphIndex = glyphIndex;
        fdSelectIndexPlusOne = 0;
    }

    public void ClosePath()
    {
        if (x != firstX || y != firstY)
        {
            b.Segments.Add(new Segment(
                SegmentOp.LineTo,
                new Point26_6(Int26_6.FromRaw(firstX), Int26_6.FromRaw(firstY))));
        }
    }

    public void MoveTo(int dx, int dy)
    {
        ClosePath();
        x += dx;
        y += dy;
        b.Segments.Add(new Segment(
            SegmentOp.MoveTo,
            new Point26_6(Int26_6.FromRaw(x), Int26_6.FromRaw(y))));
        firstX = x;
        firstY = y;
    }

    public void LineTo(int dx, int dy)
    {
        x += dx;
        y += dy;
        b.Segments.Add(new Segment(
            SegmentOp.LineTo,
            new Point26_6(Int26_6.FromRaw(x), Int26_6.FromRaw(y))));
    }

    public void CubeTo(int dxa, int dya, int dxb, int dyb, int dxc, int dyc)
    {
        x += dxa;
        y += dya;
        int xa = x;
        int ya = y;
        x += dxb;
        y += dyb;
        int xb = x;
        int yb = y;
        x += dxc;
        y += dyc;
        int xc = x;
        int yc = y;
        b.Segments.Add(new Segment(
            SegmentOp.CubeTo,
            new Point26_6(Int26_6.FromRaw(xa), Int26_6.FromRaw(ya)),
            new Point26_6(Int26_6.FromRaw(xb), Int26_6.FromRaw(yb)),
            new Point26_6(Int26_6.FromRaw(xc), Int26_6.FromRaw(yc))));
    }
}

internal struct PsOperator
{
    // numPop is the number of stack values to pop. -1 means "array" and -2
    // means "delta" as per 5176.CFF.pdf Table 6 "Operand Types".
    public int NumPop;
    // name is the operator name. A null name means an unrecognized operator.
    public string? Name;
    // Run implements the operator; null means we ignore the operator, other
    // than popping its arguments off the stack.
    public Action<PsInterpreter>? Run;
}

/// <summary>A PostScript interpreter. Ported from sfnt.psInterpreter.</summary>
internal sealed class PsInterpreter
{
    private PsContext ctx;
    private ReadOnlyMemory<byte> instructions;
    private uint instrOffset;
    private uint instrLength;
    private readonly int[] argStack = new int[CffConsts.PsArgStackSize];
    private int argStackTop;
    private readonly PsCallStackEntry[] callStack = new PsCallStackEntry[CffConsts.PsCallStackSize];
    private int callStackTop;
    private readonly char[] parseNumberBuf = new char[CffConsts.MaxRealNumberStrLen];

    internal readonly PsTopDictData topDict = new();
    internal readonly PsPrivateDictData privateDict = new();
    internal readonly PsType2CharstringsData type2Charstrings = new();

    private static readonly PsOperator[][][] PsOperators = BuildPsOperators();

    public bool HasMoreInstructions()
    {
        if (instructions.Length != 0)
        {
            return true;
        }
        for (int i = 0; i < callStackTop; i++)
        {
            if (callStack[i].Length != 0)
            {
                return true;
            }
        }
        return false;
    }

    // Run runs the instructions in the given PostScript context. For the
    // Type2Charstring context, offset and length give the location of the
    // instructions in type2Charstrings.f.src.
    public void Run(PsContext ctx, ReadOnlyMemory<byte> instructions, uint offset, uint length)
    {
        this.ctx = ctx;
        this.instructions = instructions;
        instrOffset = offset;
        instrLength = length;
        argStackTop = 0;
        callStackTop = 0;

        while (this.instructions.Length > 0)
        {
            // Push a numeric operand on the stack, if applicable.
            if (ParseNumber())
            {
                continue;
            }

            // Otherwise, execute an operator.
            byte b = this.instructions.Span[0];
            this.instructions = this.instructions.Slice(1);

            bool escaped = false;
            var ops = PsOperators[(int)ctx][0];
            while (true)
            {
                if (b == CffConsts.EscapeByte && !escaped)
                {
                    if (this.instructions.Length <= 0)
                    {
                        throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
                    }
                    b = this.instructions.Span[0];
                    this.instructions = this.instructions.Slice(1);
                    escaped = true;
                    ops = PsOperators[(int)ctx][1];
                    continue;
                }

                var op = ops[b];
                if (op.Name != null)
                {
                    if (argStackTop < op.NumPop)
                    {
                        throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
                    }
                    op.Run?.Invoke(this);
                    if (op.NumPop < 0)
                    {
                        argStackTop = 0;
                    }
                    else
                    {
                        argStackTop -= op.NumPop;
                    }
                    break;
                }

                throw new Exception(string.Format(
                    escaped
                        ? "sfnt: unrecognized CFF 2-byte operator (12 {0})"
                        : "sfnt: unrecognized CFF 1-byte operator ({0})",
                    b));
            }
        }
    }

    // See 5176.CFF.pdf section 4 "DICT Data".
    private bool ParseNumber()
    {
        int number = 0;
        bool hasResult = false;
        var span = instructions.Span;
        byte b = span[0];
        if (b == 28)
        {
            if (instructions.Length < 3)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            number = (int)(short)Src.U16(span, 1);
            instructions = instructions.Slice(3);
            hasResult = true;
        }
        else if (b == 29 && ctx != PsContext.Type2Charstring)
        {
            if (instructions.Length < 5)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            number = unchecked((int)Src.U32(span, 1));
            instructions = instructions.Slice(5);
            hasResult = true;
        }
        else if (b == 30 && ctx != PsContext.Type2Charstring)
        {
            // Parse a real number. This isn't listed in 5176.CFF.pdf Table 3
            // "Operand Encoding" (which lists integer encodings) but further
            // down the page it says "A real number operand is provided in
            // addition to integer operands. This operand begins with a byte
            // value of 30 followed by a variable-length sequence of bytes."

            instructions = instructions.Slice(1);
            int n = 0;
            bool done = false;
            while (!done)
            {
                if (instructions.Length == 0)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
                }
                b = instructions.Span[0];
                instructions = instructions.Slice(1);
                // Process b's two nibbles, high then low.
                for (int j = 0; j < 2; j++)
                {
                    int nib = b >> 4;
                    b = (byte)(b << 4);
                    if (nib == 0x0f)
                    {
                        float f;
                        try
                        {
                            f = float.Parse(
                                new string(parseNumberBuf, 0, n),
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture);
                        }
                        catch (FormatException)
                        {
                            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
                        }
                        number = unchecked((int)BitConverter.SingleToInt32Bits(f));
                        hasResult = true;
                        done = true;
                        break;
                    }
                    if (nib == 0x0d)
                    {
                        throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
                    }
                    if (n + CffConsts.MaxNibbleDefsLength > parseNumberBuf.Length)
                    {
                        throw new SfntFormatException(SfntErrors.ErrUnsupportedRealNumberEncoding);
                    }
                    foreach (char ch in CffConsts.NibbleDefs[nib])
                    {
                        parseNumberBuf[n++] = ch;
                    }
                }
            }
        }
        else if (b < 32)
        {
            // No-op.
        }
        else if (b < 247)
        {
            instructions = instructions.Slice(1);
            number = b - 139;
            hasResult = true;
        }
        else if (b < 251)
        {
            if (instructions.Length < 2)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            int b1 = instructions.Span[1];
            instructions = instructions.Slice(2);
            number = +(b - 247) * 256 + b1 + 108;
            hasResult = true;
        }
        else if (b < 255)
        {
            if (instructions.Length < 2)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            int b1 = instructions.Span[1];
            instructions = instructions.Slice(2);
            number = -(b - 251) * 256 - b1 - 108;
            hasResult = true;
        }
        else if (b == 255 && ctx == PsContext.Type2Charstring)
        {
            if (instructions.Length < 5)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            number = unchecked((int)Src.U32(span, 1));
            instructions = instructions.Slice(5);
            // 5177.Type2.pdf section 3.2 "Charstring Number Encoding": "If the
            // charstring byte contains the value 255... [this] number is
            // interpreted as a Fixed; that is, a signed number with 16 bits of
            // fraction". Round the 16.16 fixed point number to the closest
            // integer value.
            number = (number >> 16) + (1 & (number >> 15));
            hasResult = true;
        }

        if (hasResult)
        {
            if (argStackTop == CffConsts.PsArgStackSize)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            argStack[argStackTop] = number;
            argStackTop++;
        }
        return hasResult;
    }

    private static PsOperator[][][] BuildPsOperators()
    {
        var table = new PsOperator[3][][];
        for (int c = 0; c < 3; c++)
        {
            table[c] = new PsOperator[2][];
            table[c][0] = new PsOperator[256];
            table[c][1] = new PsOperator[256];
        }
        PsOperator O(int numPop, string name, Action<PsInterpreter>? run = null) =>
            new() { NumPop = numPop, Name = name, Run = run };

        var topDict = table[(int)PsContext.TopDict];
        // 1-byte operators, from 5176.CFF.pdf Table 9 and Table 10.
        topDict[0][0] = O(1, "version");
        topDict[0][1] = O(1, "Notice");
        topDict[0][2] = O(1, "FullName");
        topDict[0][3] = O(1, "FamilyName");
        topDict[0][4] = O(1, "Weight");
        topDict[0][5] = O(-1, "FontBBox");
        topDict[0][13] = O(1, "UniqueID");
        topDict[0][14] = O(-1, "XUID");
        topDict[0][15] = O(1, "charset");
        topDict[0][16] = O(1, "Encoding");
        topDict[0][17] = O(1, "CharStrings", p => p.topDict.charStringsOffset = p.argStack[p.argStackTop - 1]);
        topDict[0][18] = O(2, "Private", p =>
        {
            p.topDict.privateDictLength = p.argStack[p.argStackTop - 2];
            p.topDict.privateDictOffset = p.argStack[p.argStackTop - 1];
        });
        // 2-byte operators. The first byte is the escape byte.
        topDict[1][0] = O(1, "Copyright");
        topDict[1][1] = O(1, "isFixedPitch");
        topDict[1][2] = O(1, "ItalicAngle");
        topDict[1][3] = O(1, "UnderlinePosition");
        topDict[1][4] = O(1, "UnderlineThickness");
        topDict[1][5] = O(1, "PaintType");
        topDict[1][6] = O(1, "CharstringType");
        topDict[1][7] = O(-1, "FontMatrix");
        topDict[1][8] = O(1, "StrokeWidth");
        topDict[1][20] = O(1, "SyntheticBase");
        topDict[1][21] = O(1, "PostScript");
        topDict[1][22] = O(1, "BaseFontName");
        topDict[1][23] = O(-2, "BaseFontBlend");
        topDict[1][30] = O(3, "ROS", p => p.topDict.isCIDFont = true);
        topDict[1][31] = O(1, "CIDFontVersion");
        topDict[1][32] = O(1, "CIDFontRevision");
        topDict[1][33] = O(1, "CIDFontType");
        topDict[1][34] = O(1, "CIDCount");
        topDict[1][35] = O(1, "UIDBase");
        topDict[1][36] = O(1, "FDArray", p => p.topDict.fdArray = p.argStack[p.argStackTop - 1]);
        topDict[1][37] = O(1, "FDSelect", p => p.topDict.fdSelect = p.argStack[p.argStackTop - 1]);
        topDict[1][38] = O(1, "FontName");

        var privateDict = table[(int)PsContext.PrivateDict];
        // 1-byte operators, from 5176.CFF.pdf Table 23.
        privateDict[0][6] = O(-2, "BlueValues");
        privateDict[0][7] = O(-2, "OtherBlues");
        privateDict[0][8] = O(-2, "FamilyBlues");
        privateDict[0][9] = O(-2, "FamilyOtherBlues");
        privateDict[0][10] = O(1, "StdHW");
        privateDict[0][11] = O(1, "StdVW");
        privateDict[0][19] = O(1, "Subrs", p => p.privateDict.subrsOffset = p.argStack[p.argStackTop - 1]);
        privateDict[0][20] = O(1, "defaultWidthX");
        privateDict[0][21] = O(1, "nominalWidthX");
        // 2-byte operators. The first byte is the escape byte.
        privateDict[1][9] = O(1, "BlueScale");
        privateDict[1][10] = O(1, "BlueShift");
        privateDict[1][11] = O(1, "BlueFuzz");
        privateDict[1][12] = O(-2, "StemSnapH");
        privateDict[1][13] = O(-2, "StemSnapV");
        privateDict[1][14] = O(1, "ForceBold");
        privateDict[1][17] = O(1, "LanguageGroup");
        privateDict[1][18] = O(1, "ExpansionFactor");
        privateDict[1][19] = O(1, "initialRandomSeed");

        var type2 = table[(int)PsContext.Type2Charstring];
        // 1-byte operators, from 5177.Type2.pdf Appendix A.
        type2[0][1] = O(-1, "hstem", T2CStem);
        type2[0][3] = O(-1, "vstem", T2CStem);
        type2[0][4] = O(-1, "vmoveto", T2CVmoveto);
        type2[0][5] = O(-1, "rlineto", T2CRlineto);
        type2[0][6] = O(-1, "hlineto", T2CHlineto);
        type2[0][7] = O(-1, "vlineto", T2CVlineto);
        type2[0][8] = O(-1, "rrcurveto", T2CRrcurveto);
        type2[0][10] = O(1, "callsubr", T2CCallsubr);
        type2[0][11] = O(0, "return", T2CReturn);
        type2[0][14] = O(-1, "endchar", T2CEndchar);
        type2[0][18] = O(-1, "hstemhm", T2CStem);
        type2[0][19] = O(-1, "hintmask", T2CMask);
        type2[0][20] = O(-1, "cntrmask", T2CMask);
        type2[0][21] = O(-1, "rmoveto", T2CRmoveto);
        type2[0][22] = O(-1, "hmoveto", T2CHmoveto);
        type2[0][23] = O(-1, "vstemhm", T2CStem);
        type2[0][24] = O(-1, "rcurveline", T2CRcurveline);
        type2[0][25] = O(-1, "rlinecurve", T2CRlinecurve);
        type2[0][26] = O(-1, "vvcurveto", T2CVvcurveto);
        type2[0][27] = O(-1, "hhcurveto", T2CHhcurveto);
        type2[0][29] = O(1, "callgsubr", T2CCallgsubr);
        type2[0][30] = O(-1, "vhcurveto", T2CVhcurveto);
        type2[0][31] = O(-1, "hvcurveto", T2CHvcurveto);
        // 2-byte operators. The first byte is the escape byte.
        type2[1][34] = O(7, "hflex", T2CHflex);
        type2[1][36] = O(9, "hflex1", T2CHflex1);

        return table;
    }

    // T2CReadWidth reads the optional width adjustment. If present, it is on
    // the bottom of the arg stack. nArgs is the expected number of arguments
    // on the stack. A negative nArgs means a multiple of 2.
    private static void T2CReadWidth(PsInterpreter p, int nArgs)
    {
        if (p.type2Charstrings.seenWidth)
        {
            return;
        }
        p.type2Charstrings.seenWidth = true;
        if (nArgs >= 0)
        {
            if (p.argStackTop != nArgs + 1)
            {
                return;
            }
        }
        else if ((p.argStackTop & 1) == 0)
        {
            return;
        }
        // Width values are already stored in the hmtx table for an OpenType
        // font, so we ignore the width value here and just remove it from the
        // bottom of the argStack.
        Array.Copy(p.argStack, 1, p.argStack, 0, p.argStackTop - 1);
        p.argStackTop--;
    }

    private static void T2CStem(PsInterpreter p)
    {
        T2CReadWidth(p, -1);
        if (p.argStackTop % 2 != 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        // Update the number of hintBits needed to parse hintmask and cntrmask,
        // but ignore the stem hints otherwise.
        p.type2Charstrings.hintBits += p.argStackTop / 2;
        if (p.type2Charstrings.hintBits > CffConsts.MaxHintBits)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedNumberOfHints);
        }
    }

    private static void T2CMask(PsInterpreter p)
    {
        // If a hintmask is given any arguments (i.e. the argStack is
        // non-empty), we run an implicit vstem operator.
        if (p.argStackTop != 0)
        {
            T2CStem(p);
        }
        else if (!p.type2Charstrings.seenWidth)
        {
            p.type2Charstrings.seenWidth = true;
        }

        int hintBytes = (p.type2Charstrings.hintBits + 7) / 8;
        if (p.instructions.Length < hintBytes)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        p.instructions = p.instructions.Slice(hintBytes);
    }

    private static void T2CHmoveto(PsInterpreter p)
    {
        T2CReadWidth(p, 1);
        if (p.argStackTop != 1)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        p.type2Charstrings.MoveTo(p.argStack[0], 0);
    }

    private static void T2CVmoveto(PsInterpreter p)
    {
        T2CReadWidth(p, 1);
        if (p.argStackTop != 1)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        p.type2Charstrings.MoveTo(0, p.argStack[0]);
    }

    private static void T2CRmoveto(PsInterpreter p)
    {
        T2CReadWidth(p, 2);
        if (p.argStackTop != 2)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        p.type2Charstrings.MoveTo(p.argStack[0], p.argStack[1]);
    }

    private static void T2CHlineto(PsInterpreter p) => T2CLineto(p, false);

    private static void T2CVlineto(PsInterpreter p) => T2CLineto(p, true);

    private static void T2CLineto(PsInterpreter p, bool vertical)
    {
        if (!p.type2Charstrings.seenWidth || p.argStackTop < 1)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        for (int i = 0; i < p.argStackTop; i++, vertical = !vertical)
        {
            int dx = p.argStack[i], dy = 0;
            if (vertical)
            {
                (dx, dy) = (dy, dx);
            }
            p.type2Charstrings.LineTo(dx, dy);
        }
    }

    private static void T2CRlineto(PsInterpreter p)
    {
        if (!p.type2Charstrings.seenWidth || p.argStackTop < 2 || p.argStackTop % 2 != 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        for (int i = 0; i < p.argStackTop; i += 2)
        {
            p.type2Charstrings.LineTo(p.argStack[i], p.argStack[i + 1]);
        }
    }

    // rcurveline is: {dxa dya dxb dyb dxc dyc}+ dxd dyd
    private static void T2CRcurveline(PsInterpreter p)
    {
        if (!p.type2Charstrings.seenWidth || p.argStackTop < 8 || p.argStackTop % 6 != 2)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        int i = 0;
        for (int iMax = p.argStackTop - 2; i < iMax; i += 6)
        {
            p.type2Charstrings.CubeTo(
                p.argStack[i], p.argStack[i + 1], p.argStack[i + 2],
                p.argStack[i + 3], p.argStack[i + 4], p.argStack[i + 5]);
        }
        p.type2Charstrings.LineTo(p.argStack[i], p.argStack[i + 1]);
    }

    // rlinecurve is: {dxa dya}+ dxb dyb dxc dyc dxd dyd
    private static void T2CRlinecurve(PsInterpreter p)
    {
        if (!p.type2Charstrings.seenWidth || p.argStackTop < 8 || p.argStackTop % 2 != 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        int i = 0;
        for (int iMax = p.argStackTop - 6; i < iMax; i += 2)
        {
            p.type2Charstrings.LineTo(p.argStack[i], p.argStack[i + 1]);
        }
        p.type2Charstrings.CubeTo(
            p.argStack[i], p.argStack[i + 1], p.argStack[i + 2],
            p.argStack[i + 3], p.argStack[i + 4], p.argStack[i + 5]);
    }

    private static void T2CHhcurveto(PsInterpreter p) => T2CCurveto(p, false, false);
    private static void T2CVvcurveto(PsInterpreter p) => T2CCurveto(p, false, true);
    private static void T2CHvcurveto(PsInterpreter p) => T2CCurveto(p, true, false);
    private static void T2CVhcurveto(PsInterpreter p) => T2CCurveto(p, true, true);

    // T2CCurveto implements the hh / vv / hv / vh xxcurveto operators.
    private static void T2CCurveto(PsInterpreter p, bool swap, bool vertical)
    {
        if (!p.type2Charstrings.seenWidth || p.argStackTop < 4)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }

        int i = 0;
        switch (p.argStackTop & 3)
        {
            case 0:
                // No-op.
                break;
            case 1:
                if (swap)
                {
                    break;
                }
                i = 1;
                if (vertical)
                {
                    p.type2Charstrings.x += p.argStack[0];
                }
                else
                {
                    p.type2Charstrings.y += p.argStack[0];
                }
                break;
            default:
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }

        while (i != p.argStackTop)
        {
            i = T2CCurveto4(p, swap, vertical, i);
            if (i < 0)
            {
                throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
            }
            if (swap)
            {
                vertical = !vertical;
            }
        }
    }

    private static int T2CCurveto4(PsInterpreter p, bool swap, bool vertical, int i)
    {
        if (i + 4 > p.argStackTop)
        {
            return -1;
        }
        int dxa = p.argStack[i];
        int dya = 0;
        int dxb = p.argStack[i + 1];
        int dyb = p.argStack[i + 2];
        int dxc = p.argStack[i + 3];
        int dyc = 0;
        i += 4;

        if (vertical)
        {
            (dxa, dya) = (dya, dxa);
        }

        if (swap)
        {
            if (i + 1 == p.argStackTop)
            {
                dyc = p.argStack[i];
                i++;
            }
        }

        if (swap != vertical)
        {
            (dxc, dyc) = (dyc, dxc);
        }

        p.type2Charstrings.CubeTo(dxa, dya, dxb, dyb, dxc, dyc);
        return i;
    }

    private static void T2CRrcurveto(PsInterpreter p)
    {
        if (!p.type2Charstrings.seenWidth || p.argStackTop < 6 || p.argStackTop % 6 != 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        for (int i = 0; i != p.argStackTop; i += 6)
        {
            p.type2Charstrings.CubeTo(
                p.argStack[i], p.argStack[i + 1], p.argStack[i + 2],
                p.argStack[i + 3], p.argStack[i + 4], p.argStack[i + 5]);
        }
    }

    // For the flex operators, we ignore the flex depth and always produce
    // cubic segments, not linear segments.
    private static void T2CHflex(PsInterpreter p)
    {
        p.type2Charstrings.CubeTo(p.argStack[0], 0, p.argStack[1], p.argStack[2], p.argStack[3], 0);
        p.type2Charstrings.CubeTo(p.argStack[4], 0, p.argStack[5], -p.argStack[2], p.argStack[6], 0);
    }

    private static void T2CHflex1(PsInterpreter p)
    {
        int dy1 = p.argStack[1];
        int dy2 = p.argStack[3];
        int dy5 = p.argStack[7];
        int dy6 = -dy1 - dy2 - dy5;
        p.type2Charstrings.CubeTo(p.argStack[0], dy1, p.argStack[2], dy2, p.argStack[4], 0);
        p.type2Charstrings.CubeTo(p.argStack[5], 0, p.argStack[6], dy5, p.argStack[8], dy6);
    }

    // SubrBias returns the subroutine index bias (5177.Type2.pdf section 4.7).
    private static int SubrBias(int numSubroutines)
    {
        if (numSubroutines < 1240)
        {
            return 107;
        }
        if (numSubroutines < 33900)
        {
            return 1131;
        }
        return 32768;
    }

    private static void T2CCallgsubr(PsInterpreter p) =>
        T2CCall(p, p.type2Charstrings.f.cached.glyphData.GSubrs);

    private static void T2CCallsubr(PsInterpreter p)
    {
        var t = p.type2Charstrings;
        var d = t.f.cached.glyphData;
        int[]? subrs = d.SingleSubrs;
        if (d.MultiSubrs != null)
        {
            if (t.fdSelectIndexPlusOne == 0)
            {
                int index = d.FdSelect.Lookup(t.f, t.glyphIndex);
                if (index < 0 || d.MultiSubrs.Length <= index)
                {
                    throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
                }
                t.fdSelectIndexPlusOne = index + 1;
            }
            subrs = d.MultiSubrs[t.fdSelectIndexPlusOne - 1];
        }
        T2CCall(p, subrs);
    }

    private static void T2CCall(PsInterpreter p, int[]? subrs)
    {
        if (p.callStackTop == CffConsts.PsCallStackSize || subrs == null || subrs.Length == 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        uint length = (uint)p.instructions.Length;
        p.callStack[p.callStackTop] = new PsCallStackEntry(p.instrOffset + p.instrLength - length, length);
        p.callStackTop++;

        int subrIndex = p.argStack[p.argStackTop - 1] + SubrBias(subrs.Length - 1);
        if (subrIndex < 0 || subrs.Length - 1 <= subrIndex)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        int i = subrs[subrIndex];
        int j = subrs[subrIndex + 1];
        if (j < i)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        if (j - i > CffConsts.MaxGlyphDataLength)
        {
            throw new SfntFormatException(SfntErrors.ErrUnsupportedGlyphDataLength);
        }
        var buf = Src.View(p.type2Charstrings.f.src, i, j - i);
        p.instructions = buf.AsMemory();
        p.instrOffset = (uint)i;
        p.instrLength = (uint)(j - i);
    }

    private static void T2CReturn(PsInterpreter p)
    {
        if (p.callStackTop <= 0)
        {
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        p.callStackTop--;
        uint o = p.callStack[p.callStackTop].Offset;
        uint n = p.callStack[p.callStackTop].Length;
        var buf = Src.View(p.type2Charstrings.f.src, (int)o, (int)n);
        p.instructions = buf.AsMemory();
        p.instrOffset = o;
        p.instrLength = n;
    }

    private static void T2CEndchar(PsInterpreter p)
    {
        T2CReadWidth(p, 0);
        if (p.argStackTop != 0 || p.HasMoreInstructions())
        {
            if (p.argStackTop == 4)
            {
                // The implicit "seac" command (5177.Type2.pdf Appendix C).
                throw new SfntFormatException(SfntErrors.ErrUnsupportedType2Charstring);
            }
            throw new SfntFormatException(SfntErrors.ErrInvalidCFFTable);
        }
        p.type2Charstrings.ClosePath();
        p.type2Charstrings.ended = true;
    }
}