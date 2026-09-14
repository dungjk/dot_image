using System;
using System.IO;
using DotImage;
using DotImage.Color;

namespace DotImage.Extended.Webp.Vp8l;

internal static class Decoder
{
    public const int NLiteralCodes = 256;
    public const int NLengthCodes = 24;
    public const int NDistanceCodes = 40;
    public const int NHuff = 5;

    public static readonly uint[] AlphabetSizes =
    {
        NLiteralCodes + NLengthCodes,
        NLiteralCodes,
        NLiteralCodes,
        NLiteralCodes,
        NDistanceCodes,
    };

    private const uint ColorCacheMultiplier = 0x1e35a7bd;

    private static readonly byte[] DistanceMapTable =
    {
        0x18, 0x07, 0x17, 0x19, 0x28, 0x06, 0x27, 0x29, 0x16, 0x1a,
        0x26, 0x2a, 0x38, 0x05, 0x37, 0x39, 0x15, 0x1b, 0x36, 0x3a,
        0x25, 0x2b, 0x48, 0x04, 0x47, 0x49, 0x14, 0x1c, 0x35, 0x3b,
        0x46, 0x4a, 0x24, 0x2c, 0x58, 0x45, 0x4b, 0x34, 0x3c, 0x03,
        0x57, 0x59, 0x13, 0x1d, 0x56, 0x5a, 0x23, 0x2d, 0x44, 0x4c,
        0x55, 0x5b, 0x33, 0x3d, 0x68, 0x02, 0x67, 0x69, 0x12, 0x1e,
        0x66, 0x6a, 0x22, 0x2e, 0x54, 0x5c, 0x43, 0x4d, 0x65, 0x6b,
        0x32, 0x3e, 0x78, 0x01, 0x77, 0x79, 0x53, 0x5d, 0x11, 0x1f,
        0x64, 0x6c, 0x42, 0x4e, 0x76, 0x7a, 0x21, 0x2f, 0x75, 0x7b,
        0x31, 0x3f, 0x63, 0x6d, 0x52, 0x5e, 0x00, 0x74, 0x7c, 0x41,
        0x4f, 0x10, 0x20, 0x62, 0x6e, 0x30, 0x73, 0x7d, 0x51, 0x5f,
        0x40, 0x72, 0x7e, 0x61, 0x6f, 0x50, 0x71, 0x7f, 0x60, 0x70,
    };

    private static int DistanceMap(int w, uint code)
    {
        if (code > DistanceMapTable.Length)
            return (int)code - DistanceMapTable.Length;
        int distCode = DistanceMapTable[code - 1];
        int yOffset = distCode >> 4;
        int xOffset = 8 - (distCode & 0xf);
        int d = yOffset * w + xOffset;
        return d >= 1 ? d : 1;
    }

    private static uint Lz77Param(BitReader d, uint symbol)
    {
        if (symbol < 4) return symbol + 1;
        uint extraBits = (symbol - 2) >> 1;
        uint offset = (2 + (symbol & 1)) << (int)extraBits;
        uint n = d.Read(extraBits);
        return offset + n + 1;
    }

    private static (Transform t, int newWidth) DecodeTransform(BitReader d, int w, int h)
    {
        var t = new Transform { OldWidth = w };
        t.TransformType = d.Read(2);
        switch (t.TransformType)
        {
            case TransformUtil.TransformTypePredictor:
            case TransformUtil.TransformTypeCrossColor:
                t.Bits = d.Read(3) + 2;
                t.Pix = DecodePix(d, TransformUtil.NTiles(w, t.Bits), TransformUtil.NTiles(h, t.Bits), 0, false);
                break;
            case TransformUtil.TransformTypeSubtractGreen:
                break;
            case TransformUtil.TransformTypeColorIndexing:
                uint nColors = d.Read(8) + 1;
                t.Bits = 0;
                if (nColors <= 2) t.Bits = 3;
                else if (nColors <= 4) t.Bits = 2;
                else if (nColors <= 16) t.Bits = 1;
                w = TransformUtil.NTiles(w, t.Bits);
                byte[] pix = DecodePix(d, (int)nColors, 1, 4 * 256, false);
                for (int p = 4; p < pix.Length; p += 4)
                {
                    pix[p + 0] += pix[p - 4];
                    pix[p + 1] += pix[p - 3];
                    pix[p + 2] += pix[p - 2];
                    pix[p + 3] += pix[p - 1];
                }
                t.Pix = new byte[4 * 256];
                Array.Copy(pix, t.Pix, System.Math.Min(pix.Length, 4 * 256));
                break;
        }
        return (t, w);
    }

    private static void DecodeCodeLengths(BitReader d, uint[] dst, uint[] codeLengthCodeLengths)
    {
        var h = new HTree();
        h.Build(codeLengthCodeLengths);
        int maxSymbol = dst.Length;
        uint useLength = d.Read(1);
        if (useLength != 0)
        {
            uint n = d.Read(3);
            n = 2 + 2 * n;
            maxSymbol = (int)d.Read(n) + 2;
            if (maxSymbol > dst.Length)
                throw new InvalidOperationException("vp8l: invalid code lengths");
        }
        uint prevCodeLength = 8;
        for (int symbol = 0; symbol < dst.Length;)
        {
            if (maxSymbol == 0) break;
            maxSymbol--;
            uint codeLength = h.Next(d);
            if (codeLength < 16)
            {
                dst[symbol] = codeLength;
                symbol++;
                if (codeLength != 0) prevCodeLength = codeLength;
                continue;
            }
            uint repeat = d.Read(HuffmanTables.RepeatBits[codeLength - 16]) + HuffmanTables.RepeatOffsets[codeLength - 16];
            if (symbol + (int)repeat > dst.Length)
                throw new InvalidOperationException("vp8l: invalid code lengths");
            uint cl = codeLength == 16 ? prevCodeLength : 0;
            for (uint r = 0; r < repeat; r++)
                dst[symbol++] = cl;
        }
    }

    private static void DecodeHuffmanTree(BitReader d, HTree? h, uint alphabetSize)
    {
        uint useSimple = d.Read(1);
        if (useSimple != 0)
        {
            uint nSymbols = d.Read(1) + 1;
            uint firstSymbolLengthCode = d.Read(1);
            firstSymbolLengthCode = 7 * firstSymbolLengthCode + 1;
            var symbols = new uint[2];
            symbols[0] = d.Read(firstSymbolLengthCode);
            if (nSymbols == 2)
                symbols[1] = d.Read(8);
            if (h != null)
                h.BuildSimple(nSymbols, symbols, alphabetSize);
            return;
        }
        uint nCodes = d.Read(4) + 4;
        if (nCodes > HuffmanTables.CodeLengthCodeOrder.Length)
            throw new InvalidOperationException("vp8l: invalid Huffman tree");
        var codeLengthCodeLengths = new uint[19];
        for (uint i = 0; i < nCodes; i++)
            codeLengthCodeLengths[HuffmanTables.CodeLengthCodeOrder[i]] = d.Read(3);
        uint[] codeLengths = new uint[alphabetSize];
        DecodeCodeLengths(d, codeLengths, codeLengthCodeLengths);
        if (h != null)
            h.Build(codeLengths);
    }

    private static HTree[][] DecodeHuffmanGroups(BitReader d, int w, int h, bool topLevel, uint ccBits,
        out byte[]? hPix, out int hBits)
    {
        hPix = null;
        hBits = 0;
        int maxHGroupIndex = 0;
        int numHGroups = 1;
        int[]? mapping = null;
        if (topLevel)
        {
            uint useMeta = d.Read(1);
            if (useMeta != 0)
            {
                hBits = (int)d.Read(3) + 2;
                int tileW = TransformUtil.NTiles(w, (uint)hBits);
                int tileH = TransformUtil.NTiles(h, (uint)hBits);
                hPix = DecodePix(d, tileW, tileH, 0, false);
                for (int p = 0; p < hPix.Length; p += 4)
                {
                    int i = (hPix[p] << 8) | hPix[p + 1];
                    if (maxHGroupIndex < i) maxHGroupIndex = i;
                }
                const int maxHuffImageSize = 2600;
                if (maxHGroupIndex >= maxHuffImageSize)
                    throw new InvalidOperationException("vp8l: too many Huffman trees");
                int numTiles = tileH * tileW;
                if (maxHGroupIndex >= 1000 || maxHGroupIndex >= numTiles)
                {
                    mapping = new int[maxHGroupIndex + 1];
                    for (int i = 0; i < mapping.Length; i++) mapping[i] = -1;
                    numHGroups = 0;
                    for (int p = 0; p < hPix.Length; p += 4)
                    {
                        int i = (hPix[p] << 8) | hPix[p + 1];
                        if (mapping[i] == -1)
                        {
                            mapping[i] = numHGroups;
                            numHGroups++;
                        }
                        hPix[p] = (byte)(mapping[i] >> 8);
                        hPix[p + 1] = (byte)mapping[i];
                    }
                }
                else
                {
                    numHGroups = maxHGroupIndex + 1;
                }
            }
        }

        var hGroups = new HTree[numHGroups][];
        for (int i = 0; i < numHGroups; i++) hGroups[i] = new HTree[NHuff];
        for (int i = 0; i <= maxHGroupIndex; i++)
        {
            HTree[]? hg = null;
            if (mapping == null) hg = hGroups[i];
            else if (mapping[i] != -1) hg = hGroups[mapping[i]];
            for (int j = 0; j < NHuff; j++)
            {
                uint alphabetSize = AlphabetSizes[j];
                if (j == 0 && ccBits > 0)
                    alphabetSize += 1u << (int)ccBits;
                if (hg != null && hg[j] == null) hg[j] = new HTree();
                DecodeHuffmanTree(d, hg?[j], alphabetSize);
            }
        }
        return hGroups;
    }

    private static byte[] DecodePix(BitReader d, int w, int h, int minCap, bool topLevel)
    {
        Guards.EnsureDecodeSize(w, h, 4);
        uint ccBits = 0, ccShift = 0;
        uint[]? ccEntries = null;
        uint useColorCache = d.Read(1);
        if (useColorCache != 0)
        {
            ccBits = d.Read(4);
            if (ccBits < 1 || ccBits > 11)
                throw new InvalidOperationException("vp8l: invalid color cache parameters");
            ccShift = 32 - ccBits;
            ccEntries = new uint[1 << (int)ccBits];
        }
        var hGroups = DecodeHuffmanGroups(d, w, h, topLevel, ccBits, out byte[]? hPix, out int hBits);
        int hMask = hBits != 0 ? (1 << hBits) - 1 : 0;
        int tilesPerRow = hBits != 0 ? TransformUtil.NTiles(w, (uint)hBits) : 0;
        var pix = new byte[4 * w * h];
        int p = 0, cachedP = 0, x = 0, y = 0;
        var hg = hGroups[0];
        bool lookupHG = hMask != 0;
        while (p < pix.Length)
        {
            if (lookupHG)
            {
                int i = 4 * (tilesPerRow * (y >> hBits) + (x >> hBits));
                int idx = (hPix![i] << 8) | hPix[i + 1];
                hg = hGroups[idx];
            }
            uint green = hg[0].Next(d);
            if (green < NLiteralCodes)
            {
                uint red = hg[1].Next(d);
                uint blue = hg[2].Next(d);
                uint alpha = hg[3].Next(d);
                pix[p + 0] = (byte)red;
                pix[p + 1] = (byte)green;
                pix[p + 2] = (byte)blue;
                pix[p + 3] = (byte)alpha;
                p += 4;
                x++;
                if (x == w) { x = 0; y++; }
                lookupHG = hMask != 0 && (x & hMask) == 0;
            }
            else if (green < NLiteralCodes + NLengthCodes)
            {
                uint length = Lz77Param(d, green - NLiteralCodes);
                uint distSym = hg[4].Next(d);
                uint distCode = Lz77Param(d, distSym);
                int dist = DistanceMap(w, distCode);
                int pEnd = p + 4 * (int)length;
                int q = p - 4 * dist;
                int qEnd = pEnd - 4 * dist;
                if (p < 0 || pix.Length < pEnd || q < 0 || pix.Length < qEnd)
                    throw new InvalidOperationException("vp8l: invalid LZ77 parameters");
                for (; p < pEnd; p++, q++)
                    pix[p] = pix[q];
                x += (int)length;
                while (x >= w) { x -= w; y++; }
                lookupHG = hMask != 0;
            }
            else
            {
                for (; cachedP < p; cachedP += 4)
                {
                    uint argb = (uint)(pix[cachedP + 0]) << 16 |
                                (uint)(pix[cachedP + 1]) << 8 |
                                (uint)(pix[cachedP + 2]) |
                                (uint)(pix[cachedP + 3]) << 24;
                    ccEntries![(argb * ColorCacheMultiplier) >> (int)ccShift] = argb;
                }
                uint idx = green - NLiteralCodes - NLengthCodes;
                if (idx >= ccEntries!.Length)
                    throw new InvalidOperationException("vp8l: invalid color cache index");
                uint argbVal = ccEntries[idx];
                pix[p + 0] = (byte)(argbVal >> 16);
                pix[p + 1] = (byte)(argbVal >> 8);
                pix[p + 2] = (byte)(argbVal);
                pix[p + 3] = (byte)(argbVal >> 24);
                p += 4;
                x++;
                if (x == w) { x = 0; y++; }
                lookupHG = hMask != 0 && (x & hMask) == 0;
            }
        }
        return pix;
    }

    public static void DecodeHeader(BitReader d, out int w, out int h)
    {
        uint magic = d.Read(8);
        if (magic != 0x2f)
            throw new InvalidOperationException("vp8l: invalid header");
        w = (int)d.Read(14) + 1;
        h = (int)d.Read(14) + 1;
        d.Read(1); // hasAlpha
        uint version = d.Read(3);
        if (version != 0)
            throw new InvalidOperationException("vp8l: invalid version");
    }

    public static Config DecodeConfig(Stream r)
    {
        var d = new BitReader(r);
        DecodeHeader(d, out int w, out int h);
        return new Config(ColorModels.Nrgba, w, h);
    }

    public static NrgbaImage Decode(Stream r)
    {
        var d = new BitReader(r);
        DecodeHeader(d, out int w, out int h);
        int nTransforms = 0;
        var transforms = new Transform[TransformUtil.NTransformTypes];
        bool[] transformsSeen = new bool[TransformUtil.NTransformTypes];
        int originalW = w;
        while (true)
        {
            uint more = d.Read(1);
            if (more == 0) break;
            var (t, newWidth) = DecodeTransform(d, w, h);
            if (transformsSeen[t.TransformType])
                throw new InvalidOperationException("vp8l: repeated transform");
            transformsSeen[t.TransformType] = true;
            transforms[nTransforms++] = t;
            w = newWidth;
        }
        byte[] pix = DecodePix(d, w, h, 0, true);
        for (int i = nTransforms - 1; i >= 0; i--)
            pix = TransformUtil.InverseTransform((int)transforms[i].TransformType, transforms[i], pix, h);
        var rect = new Rect(new Point(0, 0), new Point(originalW, h));
        var img = Images.NewNrgba(rect);
        pix.AsSpan(0, System.Math.Min(pix.Length, img.Pix.Length)).CopyTo(img.Pix.Span);
        return img;
    }
}
