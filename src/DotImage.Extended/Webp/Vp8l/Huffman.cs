using System;

namespace DotImage.Extended.Webp.Vp8l;

internal static class HuffmanTables
{
    public static readonly byte[] ReverseBits = GenerateReverseBits();

    private static byte[] GenerateReverseBits()
    {
        var r = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            int v = i;
            int result = 0;
            for (int j = 0; j < 8; j++)
            {
                result = (result << 1) | (v & 1);
                v >>= 1;
            }
            r[i] = (byte)result;
        }
        return r;
    }

    public static readonly uint[] CodeLengthCodeOrder =
    {
        17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
    };

    public static readonly byte[] RepeatBits = { 2, 3, 7 };
    public static readonly byte[] RepeatOffsets = { 3, 3, 11 };
}

internal sealed class HTree
{
    private const int LeafNode = -1;
    private const int LutSize = 7;
    private const int LutMask = (1 << LutSize) - 1;

    private struct HNode
    {
        public uint Symbol;
        public int Children;
    }

    private HNode[] _nodes;
    private readonly uint[] _lut = new uint[1 << LutSize];

    public HTree()
    {
        _nodes = Array.Empty<HNode>();
    }

    public uint Next(BitReader d)
    {
        uint n = 0;
        if (d._nBits < LutSize)
        {
            int c = d._r.ReadByte();
            if (c >= 0)
            {
                d._bits |= (uint)c << (int)d._nBits;
                d._nBits += 8;
            }
        }
        if (d._nBits >= LutSize)
        {
            n = _lut[d._bits & LutMask];
            uint b = n & 0xff;
            if (b != 0)
            {
                b--;
                d._bits >>= (int)b;
                d._nBits -= b;
                return n >> 8;
            }
            n >>= 8;
            d._bits >>= LutSize;
            d._nBits -= LutSize;
        }

        while (_nodes[n].Children != LeafNode)
        {
            if (d._nBits == 0)
            {
                int c = d._r.ReadByte();
                if (c < 0)
                    throw new EndOfStreamException();
                d._bits = (uint)c;
                d._nBits = 8;
            }
            n = (uint)_nodes[n].Children + (d._bits & 1);
            d._bits >>= 1;
            d._nBits--;
        }
        return _nodes[n].Symbol;
    }

    public void Build(uint[] codeLengths)
    {
        uint nSymbols = 0, lastSymbol = 0;
        for (uint symbol = 0; symbol < codeLengths.Length; symbol++)
        {
            if (codeLengths[symbol] != 0)
            {
                nSymbols++;
                lastSymbol = symbol;
            }
        }
        if (nSymbols == 0)
            throw new InvalidOperationException("vp8l: invalid Huffman tree");

        _nodes = new HNode[2 * nSymbols - 1];
        _nodes[0] = default;

        if (nSymbols == 1)
        {
            Insert(lastSymbol, 0, 0);
            return;
        }

        uint[] codes = CodeLengthsToCodes(codeLengths);
        for (uint symbol = 0; symbol < codeLengths.Length; symbol++)
        {
            if (codeLengths[symbol] > 0)
                Insert(symbol, codes[symbol], codeLengths[symbol]);
        }
    }

    public void BuildSimple(uint nSymbols, uint[] symbols, uint alphabetSize)
    {
        _nodes = new HNode[2 * nSymbols - 1];
        _nodes[0] = default;
        for (uint i = 0; i < nSymbols; i++)
        {
            if (symbols[i] >= alphabetSize)
                throw new InvalidOperationException("vp8l: invalid Huffman tree");
            Insert(symbols[i], i, nSymbols - 1);
        }
    }

    private void Insert(uint symbol, uint code, uint codeLength)
    {
        uint baseCode = 0;
        if (codeLength > LutSize)
        {
            baseCode = (uint)(HuffmanTables.ReverseBits[(code >> (int)(codeLength - LutSize)) & 0xff] >> (8 - LutSize));
        }
        else
        {
            baseCode = (uint)(HuffmanTables.ReverseBits[code & 0xff] >> (8 - (int)codeLength));
            for (int i = 0; i < 1 << (LutSize - (int)codeLength); i++)
                _lut[baseCode | (uint)i << (int)codeLength] = symbol << 8 | (codeLength + 1);
        }

        uint n = 0;
        int jump = LutSize;
        while (codeLength > 0)
        {
            codeLength--;
            if ((int)n > _nodes.Length)
                throw new InvalidOperationException("vp8l: invalid Huffman tree");
            switch (_nodes[n].Children)
            {
                case LeafNode:
                    throw new InvalidOperationException("vp8l: invalid Huffman tree");
                case 0:
                    _nodes[n].Children = (int)_nodes.Length;
                    Array.Resize(ref _nodes, _nodes.Length + 2);
                    break;
            }
            n = (uint)_nodes[n].Children + ((code >> (int)codeLength) & 1);
            jump--;
            if (jump == 0 && _lut[baseCode] == 0)
                _lut[baseCode] = n << 8;
        }

        switch (_nodes[n].Children)
        {
            case LeafNode:
                break;
            case 0:
                _nodes[n].Children = LeafNode;
                break;
            default:
                throw new InvalidOperationException("vp8l: invalid Huffman tree");
        }
        _nodes[n].Symbol = symbol;
    }

    private static uint[] CodeLengthsToCodes(uint[] codeLengths)
    {
        uint maxCodeLength = 0;
        foreach (uint cl in codeLengths)
            if (maxCodeLength < cl) maxCodeLength = cl;
        const uint maxAllowedCodeLength = 15;
        if (codeLengths.Length == 0 || maxCodeLength > maxAllowedCodeLength)
            throw new InvalidOperationException("vp8l: invalid Huffman tree");

        var histogram = new uint[maxAllowedCodeLength + 1];
        foreach (uint cl in codeLengths)
            histogram[cl]++;

        uint currCode = 0;
        var nextCodes = new uint[maxAllowedCodeLength + 1];
        for (int cl = 1; cl < nextCodes.Length; cl++)
        {
            currCode = (currCode + histogram[cl - 1]) << 1;
            nextCodes[cl] = currCode;
        }

        var codes = new uint[codeLengths.Length];
        for (uint symbol = 0; symbol < codeLengths.Length; symbol++)
        {
            uint cl = codeLengths[symbol];
            if (cl > 0)
            {
                codes[symbol] = nextCodes[cl];
                nextCodes[cl]++;
            }
        }
        return codes;
    }
}
