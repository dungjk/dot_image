// Ported from Go golang.org/x/image/vp8/quant.go

namespace DotImage.Extended.Webp.Vp8;

// Quant are DC/AC quantization factors.
internal struct Quant
{
    internal ushort[] Y1;
    internal ushort[] Y2;
    internal ushort[] Uv;

    internal Quant(ushort[] y1, ushort[] y2, ushort[] uv)
    {
        Y1 = y1;
        Y2 = y2;
        Uv = uv;
    }
}

public sealed partial class Decoder
{
    // Clip clips x to the range [min, max] inclusive.
    internal static int Clip(int x, int min, int max)
    {
        if (x < min)
        {
            return min;
        }
        if (x > max)
        {
            return max;
        }
        return x;
    }

    // ParseQuant parses the quantization factors, as specified in section 9.6.
    internal void ParseQuant()
    {
        uint baseQ0 = _fp.ReadUint(Partition.UniformProb, 7);
        int dqy1DC = _fp.ReadOptionalInt(Partition.UniformProb, 4);
        const int dqy1AC = 0;
        int dqy2DC = _fp.ReadOptionalInt(Partition.UniformProb, 4);
        int dqy2AC = _fp.ReadOptionalInt(Partition.UniformProb, 4);
        int dquvDC = _fp.ReadOptionalInt(Partition.UniformProb, 4);
        int dquvAC = _fp.ReadOptionalInt(Partition.UniformProb, 4);
        for (int i = 0; i < NSegment; i++)
        {
            int q = (int)baseQ0;
            if (_segmentHeader.UseSegment)
            {
                if (_segmentHeader.RelativeDelta)
                {
                    q += _segmentHeader.Quantizer[i];
                }
                else
                {
                    q = _segmentHeader.Quantizer[i];
                }
            }
            _quant[i].Y1[0] = DequantTableDc[Clip(q + dqy1DC, 0, 127)];
            _quant[i].Y1[1] = DequantTableAc[Clip(q + dqy1AC, 0, 127)];
            _quant[i].Y2[0] = (ushort)(DequantTableDc[Clip(q + dqy2DC, 0, 127)] * 2);
            _quant[i].Y2[1] = (ushort)(DequantTableAc[Clip(q + dqy2AC, 0, 127)] * 155 / 100);
            if (_quant[i].Y2[1] < 8)
            {
                _quant[i].Y2[1] = 8;
            }
            // The 117 is not a typo. The dequant_init function in the spec's Reference
            // Decoder Source Code (http://tools.ietf.org/html/rfc6386#section-9.6 Page 145)
            // says to clamp the LHS value at 132, which is equal to dequantTableDC[117].
            _quant[i].Uv[0] = DequantTableDc[Clip(q + dquvDC, 0, 117)];
            _quant[i].Uv[1] = DequantTableAc[Clip(q + dquvAC, 0, 127)];
        }
    }

    // The dequantization tables are specified in section 14.1.
    internal static readonly ushort[] DequantTableDc =
    {
        4, 5, 6, 7, 8, 9, 10, 10,
        11, 12, 13, 14, 15, 16, 17, 17,
        18, 19, 20, 20, 21, 21, 22, 22,
        23, 23, 24, 25, 25, 26, 27, 28,
        29, 30, 31, 32, 33, 34, 35, 36,
        37, 37, 38, 39, 40, 41, 42, 43,
        44, 45, 46, 46, 47, 48, 49, 50,
        51, 52, 53, 54, 55, 56, 57, 58,
        59, 60, 61, 62, 63, 64, 65, 66,
        67, 68, 69, 70, 71, 72, 73, 74,
        75, 76, 76, 77, 78, 79, 80, 81,
        82, 83, 84, 85, 86, 87, 88, 89,
        91, 93, 95, 96, 98, 100, 101, 102,
        104, 106, 108, 110, 112, 114, 116, 118,
        122, 124, 126, 128, 130, 132, 134, 136,
        138, 140, 143, 145, 148, 151, 154, 157,
    };

    internal static readonly ushort[] DequantTableAc =
    {
        4, 5, 6, 7, 8, 9, 10, 11,
        12, 13, 14, 15, 16, 17, 18, 19,
        20, 21, 22, 23, 24, 25, 26, 27,
        28, 29, 30, 31, 32, 33, 34, 35,
        36, 37, 38, 39, 40, 41, 42, 43,
        44, 45, 46, 47, 48, 49, 50, 51,
        52, 53, 54, 55, 56, 57, 58, 60,
        62, 64, 66, 68, 70, 72, 74, 76,
        78, 80, 82, 84, 86, 88, 90, 92,
        94, 96, 98, 100, 102, 104, 106, 108,
        110, 112, 114, 116, 119, 122, 125, 128,
        131, 134, 137, 140, 143, 146, 149, 152,
        155, 158, 161, 164, 167, 170, 173, 177,
        181, 185, 189, 193, 197, 201, 205, 209,
        213, 217, 221, 225, 229, 234, 239, 245,
        249, 254, 259, 264, 269, 274, 279, 284,
    };
}