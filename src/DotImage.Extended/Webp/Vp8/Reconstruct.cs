// Ported from Go golang.org/x/image/vp8/reconstruct.go

namespace DotImage.Extended.Webp.Vp8;

// This file implements decoding DCT/WHT residual coefficients and
// reconstructing YCbCr data equal to predicted values plus residuals.
//
// There are 1*16*16 + 2*8*8 + 1*4*4 coefficients per macroblock:
//	- 1*16*16 luma DCT coefficients,
//	- 2*8*8 chroma DCT coefficients, and
//	- 1*4*4 luma WHT coefficients.
// Coefficients are read in lots of 16, and the later coefficients in each lot
// are often zero.
public sealed partial class Decoder
{
    internal const int BCoeffBase = 1 * 16 * 16 + 0 * 8 * 8;
    internal const int RCoeffBase = 1 * 16 * 16 + 1 * 8 * 8;
    internal const int WhtCoeffBase = 1 * 16 * 16 + 2 * 8 * 8;

    internal const int YbrYx = 8;
    internal const int YbrYy = 1;
    internal const int YbrBx = 8;
    internal const int YbrBy = 18;
    internal const int YbrRx = 24;
    internal const int YbrRy = 18;

    // PrepareYbr prepares the {abcdefghij} elements of ybr.
    internal void PrepareYbr(int mbx, int mby)
    {
        if (mbx == 0)
        {
            for (int y = 0; y < 17; y++)
            {
                _ybr[y][7] = 0x81;
            }
            for (int y = 17; y < 26; y++)
            {
                _ybr[y][7] = 0x81;
                _ybr[y][23] = 0x81;
            }
        }
        else
        {
            for (int y = 0; y < 17; y++)
            {
                _ybr[y][7] = _ybr[y][7 + 16];
            }
            for (int y = 17; y < 26; y++)
            {
                _ybr[y][7] = _ybr[y][15];
                _ybr[y][23] = _ybr[y][31];
            }
        }
        if (mby == 0)
        {
            for (int x = 7; x < 28; x++)
            {
                _ybr[0][x] = 0x7f;
            }
            for (int x = 7; x < 16; x++)
            {
                _ybr[17][x] = 0x7f;
            }
            for (int x = 23; x < 32; x++)
            {
                _ybr[17][x] = 0x7f;
            }
        }
        else
        {
            for (int i = 0; i < 16; i++)
            {
                _ybr[0][8 + i] = _img!.Y.Span[(16 * mby - 1) * _img.YStride + 16 * mbx + i];
            }
            for (int i = 0; i < 8; i++)
            {
                _ybr[17][8 + i] = _img!.Cb.Span[(8 * mby - 1) * _img.CStride + 8 * mbx + i];
            }
            for (int i = 0; i < 8; i++)
            {
                _ybr[17][24 + i] = _img!.Cr.Span[(8 * mby - 1) * _img.CStride + 8 * mbx + i];
            }
            if (mbx == _mbw - 1)
            {
                for (int i = 16; i < 20; i++)
                {
                    _ybr[0][8 + i] = _img!.Y.Span[(16 * mby - 1) * _img.YStride + 16 * mbx + 15];
                }
            }
            else
            {
                for (int i = 16; i < 20; i++)
                {
                    _ybr[0][8 + i] = _img!.Y.Span[(16 * mby - 1) * _img.YStride + 16 * mbx + i];
                }
            }
        }
        for (int y = 4; y < 16; y += 4)
        {
            _ybr[y][24] = _ybr[0][24];
            _ybr[y][25] = _ybr[0][25];
            _ybr[y][26] = _ybr[0][26];
            _ybr[y][27] = _ybr[0][27];
        }
    }

    // Pack packs four 0/1 values into four bits of a uint32.
    internal static uint Pack(byte[] x, int shift)
    {
        uint u = (uint)x[0] << 0 | (uint)x[1] << 1 | (uint)x[2] << 2 | (uint)x[3] << 3;
        return u << shift;
    }

    // Unpack unpacks four 0/1 values from a four-bit value.
    internal static readonly byte[][] Unpack =
    [
        [0, 0, 0, 0],
        [1, 0, 0, 0],
        [0, 1, 0, 0],
        [1, 1, 0, 0],
        [0, 0, 1, 0],
        [1, 0, 1, 0],
        [0, 1, 1, 0],
        [1, 1, 1, 0],
        [0, 0, 0, 1],
        [1, 0, 0, 1],
        [0, 1, 0, 1],
        [1, 1, 0, 1],
        [0, 0, 1, 1],
        [1, 0, 1, 1],
        [0, 1, 1, 1],
        [1, 1, 1, 1],
    ];

    // The mapping from 4x4 region position to band is specified in section 13.3.
    internal static readonly byte[] Bands = { 0, 1, 2, 3, 6, 4, 5, 6, 6, 6, 6, 6, 6, 6, 6, 7, 0 };
    // Category probabilities are specified in section 13.2.
    // Decoding categories 1 and 2 are done inline.
    internal static readonly byte[][] Cat3456 =
    [
        [173, 148, 140, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [176, 155, 140, 135, 0, 0, 0, 0, 0, 0, 0, 0],
        [180, 157, 141, 134, 130, 0, 0, 0, 0, 0, 0, 0],
        [254, 254, 243, 230, 196, 177, 153, 140, 133, 130, 129, 0],
    ];
    // The zigzag order is:
    //	0  1  5  6
    //	2  4  7 12
    //	3  8 11 13
    //	9 10 14 15
    internal static readonly byte[] Zigzag = { 0, 1, 4, 8, 5, 2, 3, 6, 9, 12, 13, 10, 7, 11, 14, 15 };

    // ParseResiduals4 parses a 4x4 region of residual coefficients, as specified
    // in section 13.3, and returns a 0/1 value indicating whether there was at
    // least one non-zero coefficient.
    // r is the partition to read bits from.
    // plane and context describe which token probability table to use. context is
    // either 0, 1 or 2, and equals how many of the macroblock left and macroblock
    // above have non-zero coefficients.
    // quant are the DC/AC quantization factors.
    // skipFirstCoeff is whether the DC coefficient has already been parsed.
    // coeffBase is the base index of d.coeff to write to.
    internal byte ParseResiduals4(Partition r, int plane, byte context, ushort[] quant, bool skipFirstCoeff, int coeffBase)
    {
        byte[][][] prob = _tokenProb[plane];
        int n = skipFirstCoeff ? 1 : 0;
        byte[] p = prob[Bands[n]][context];
        if (!r.ReadBit(p[0]))
        {
            return 0;
        }
        while (n != 16)
        {
            n++;
            if (!r.ReadBit(p[1]))
            {
                p = prob[Bands[n]][0];
                continue;
            }
            uint v;
            if (!r.ReadBit(p[2]))
            {
                v = 1;
                p = prob[Bands[n]][1];
            }
            else
            {
                if (!r.ReadBit(p[3]))
                {
                    if (!r.ReadBit(p[4]))
                    {
                        v = 2;
                    }
                    else
                    {
                        v = 3 + r.ReadUint(p[5], 1);
                    }
                }
                else if (!r.ReadBit(p[6]))
                {
                    if (!r.ReadBit(p[7]))
                    {
                        // Category 1.
                        v = 5 + r.ReadUint((byte)159, 1);
                    }
                    else
                    {
                        // Category 2.
                        v = 7 + 2 * r.ReadUint((byte)165, 1) + r.ReadUint((byte)145, 1);
                    }
                }
                else
                {
                    // Categories 3, 4, 5 or 6.
                    uint b1 = r.ReadUint(p[8], 1);
                    uint b0 = r.ReadUint(p[9 + b1], 1);
                    uint cat = 2 * b1 + b0;
                    byte[] tab = Cat3456[cat];
                    v = 0;
                    for (int i = 0; tab[i] != 0; i++)
                    {
                        v *= 2;
                        v += r.ReadUint(tab[i], 1);
                    }
                    v += 3 + (8u << (int)cat);
                }
                p = prob[Bands[n]][2];
            }
            int z = Zigzag[n - 1];
            int c = (int)(v * quant[BoolToU8(z > 0)]);
            if (r.ReadBit(Partition.UniformProb))
            {
                c = -c;
            }
            _coeff[coeffBase + z] = (short)c;
            if (n == 16 || !r.ReadBit(p[0]))
            {
                return 1;
            }
        }
        return 1;
    }

    // ParseResiduals parses the residuals and returns whether inner loop filtering
    // should be skipped for this macroblock.
    internal bool ParseResiduals(int mbx, int mby)
    {
        Partition partition = _op[mby & (_nOP - 1)];
        int plane = PlaneY1SansY2;
        Quant quant = _quant[_segment];

        // Parse the DC coefficient of each 4x4 luma region.
        if (_usePredY16)
        {
            byte nz = ParseResiduals4(partition, PlaneY2, (byte)(_leftMB.NzY16 + _upMB[mbx].NzY16), quant.Y2, false, WhtCoeffBase);
            _leftMB.NzY16 = nz;
            _upMB[mbx].NzY16 = nz;
            InverseWht16();
            plane = PlaneY1WithY2;
        }

        var nzDC = new byte[4];
        var nzAC = new byte[4];
        uint nzDCMask = 0, nzACMask = 0;
        int coeffBase = 0;

        // Parse the luma coefficients.
        byte[] lnz = (byte[])Unpack[_leftMB.NzMask & 0x0f].Clone();
        byte[] unz = (byte[])Unpack[_upMB[mbx].NzMask & 0x0f].Clone();
        for (int y = 0; y < 4; y++)
        {
            byte nz = lnz[y];
            for (int x = 0; x < 4; x++)
            {
                nz = ParseResiduals4(partition, plane, (byte)(nz + unz[x]), quant.Y1, _usePredY16, coeffBase);
                unz[x] = nz;
                nzAC[x] = nz;
                nzDC[x] = BoolToU8(_coeff[coeffBase] != 0);
                coeffBase += 16;
            }
            lnz[y] = nz;
            nzDCMask |= Pack(nzDC, y * 4);
            nzACMask |= Pack(nzAC, y * 4);
        }
        byte lnzMask = (byte)Pack(lnz, 0);
        byte unzMask = (byte)Pack(unz, 0);

        // Parse the chroma coefficients.
        lnz = (byte[])Unpack[_leftMB.NzMask >> 4].Clone();
        unz = (byte[])Unpack[_upMB[mbx].NzMask >> 4].Clone();
        for (int c = 0; c < 4; c += 2)
        {
            for (int y = 0; y < 2; y++)
            {
                byte nz = lnz[y + c];
                for (int x = 0; x < 2; x++)
                {
                    nz = ParseResiduals4(partition, PlaneUv, (byte)(nz + unz[x + c]), quant.Uv, false, coeffBase);
                    unz[x + c] = nz;
                    nzAC[y * 2 + x] = nz;
                    nzDC[y * 2 + x] = BoolToU8(_coeff[coeffBase] != 0);
                    coeffBase += 16;
                }
                lnz[y + c] = nz;
            }
            nzDCMask |= Pack(nzDC, 16 + c * 2);
            nzACMask |= Pack(nzAC, 16 + c * 2);
        }
        lnzMask |= (byte)Pack(lnz, 4);
        unzMask |= (byte)Pack(unz, 4);

        // Save decoder state.
        _leftMB.NzMask = lnzMask;
        _upMB[mbx].NzMask = unzMask;
        _nzDCMask = nzDCMask;
        _nzACMask = nzACMask;

        // Section 15.1 of the spec says that "Steps 2 and 4 [of the loop filter]
        // are skipped... [if] there is no DCT coefficient coded for the whole
        // macroblock."
        return nzDCMask == 0 && nzACMask == 0;
    }

    // ReconstructMacroblock applies the predictor functions and adds the inverse-
    // DCT transformed residuals to recover the YCbCr data.
    internal void ReconstructMacroblock(int mbx, int mby)
    {
        if (_usePredY16)
        {
            byte p = CheckTopLeftPred(mbx, mby, _predY16);
            PredFunc16[p](this, 1, 8);
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    int n = 4 * j + i;
                    int y = 4 * j + 1;
                    int x = 4 * i + 8;
                    uint mask = 1u << n;
                    if ((_nzACMask & mask) != 0)
                    {
                        InverseDct4(y, x, 16 * n);
                    }
                    else if ((_nzDCMask & mask) != 0)
                    {
                        InverseDct4DcOnly(y, x, 16 * n);
                    }
                }
            }
        }
        else
        {
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    int n = 4 * j + i;
                    int y = 4 * j + 1;
                    int x = 4 * i + 8;
                    PredFunc4[_predY4[j, i]](this, y, x);
                    uint mask = 1u << n;
                    if ((_nzACMask & mask) != 0)
                    {
                        InverseDct4(y, x, 16 * n);
                    }
                    else if ((_nzDCMask & mask) != 0)
                    {
                        InverseDct4DcOnly(y, x, 16 * n);
                    }
                }
            }
        }
        byte p8 = CheckTopLeftPred(mbx, mby, _predC8);
        PredFunc8[p8](this, YbrBy, YbrBx);
        if ((_nzACMask & 0x0f0000) != 0)
        {
            InverseDct8(YbrBy, YbrBx, BCoeffBase);
        }
        else if ((_nzDCMask & 0x0f0000) != 0)
        {
            InverseDct8DcOnly(YbrBy, YbrBx, BCoeffBase);
        }
        PredFunc8[p8](this, YbrRy, YbrRx);
        if ((_nzACMask & 0xf00000) != 0)
        {
            InverseDct8(YbrRy, YbrRx, RCoeffBase);
        }
        else if ((_nzDCMask & 0xf00000) != 0)
        {
            InverseDct8DcOnly(YbrRy, YbrRx, RCoeffBase);
        }
    }

    // Reconstruct reconstructs one macroblock and returns whether inner loop
    // filtering should be skipped for it.
    internal bool Reconstruct(int mbx, int mby)
    {
        if (_segmentHeader.UpdateMap)
        {
            if (!_fp.ReadBit(_segmentHeader.Prob[0]))
            {
                _segment = (int)_fp.ReadUint(_segmentHeader.Prob[1], 1);
            }
            else
            {
                _segment = (int)_fp.ReadUint(_segmentHeader.Prob[2], 1) + 2;
            }
        }
        bool skip = false;
        if (_useSkipProb)
        {
            skip = _fp.ReadBit(_skipProb);
        }
        // Prepare the workspace.
        for (int i = 0; i < _coeff.Length; i++)
        {
            _coeff[i] = 0;
        }
        PrepareYbr(mbx, mby);
        // Parse the predictor modes.
        _usePredY16 = _fp.ReadBit(145);
        if (_usePredY16)
        {
            ParsePredModeY16(mbx);
        }
        else
        {
            ParsePredModeY4(mbx);
        }
        ParsePredModeC8();
        // Parse the residuals.
        if (!skip)
        {
            skip = ParseResiduals(mbx, mby);
        }
        else
        {
            if (_usePredY16)
            {
                _leftMB.NzY16 = 0;
                _upMB[mbx].NzY16 = 0;
            }
            _leftMB.NzMask = 0;
            _upMB[mbx].NzMask = 0;
            _nzDCMask = 0;
            _nzACMask = 0;
        }
        // Reconstruct the YCbCr data and copy it to the image.
        ReconstructMacroblock(mbx, mby);
        for (int i = (mby * _img!.YStride + mbx) * 16, y = 0; y < 16; i += _img.YStride, y++)
        {
            _ybr[YbrYy + y].AsSpan(YbrYx, 16).CopyTo(_img.Y.Span.Slice(i, 16));
        }
        for (int i = (mby * _img.CStride + mbx) * 8, y = 0; y < 8; i += _img.CStride, y++)
        {
            _ybr[YbrBy + y].AsSpan(YbrBx, 8).CopyTo(_img.Cb.Span.Slice(i, 8));
            _ybr[YbrRy + y].AsSpan(YbrRx, 8).CopyTo(_img.Cr.Span.Slice(i, 8));
        }
        return skip;
    }
}