// Ported from Go golang.org/x/image/vp8/pred.go and predfunc.go

namespace DotImage.Extended.Webp.Vp8;

// This file implements parsing the predictor modes, as specified in chapter
// 11, and the prediction functions, as specified in chapter 12.
//
// For each macroblock (of 1x16x16 luma and 2x8x8 chroma coefficients), the
// luma values are either predicted as one large 16x16 region or 16 separate
// 4x4 regions. The chroma values are always predicted as one 8x8 region.

public sealed partial class Decoder
{
    // nPred is the number of predictor modes, not including the Top/Left versions
    // of the DC predictor mode.
    internal const int NPred = 10;

    internal const byte PredDC = 0;
    internal const byte PredTM = 1;
    internal const byte PredVE = 2;
    internal const byte PredHE = 3;
    internal const byte PredRD = 4;
    internal const byte PredVR = 5;
    internal const byte PredLD = 6;
    internal const byte PredVL = 7;
    internal const byte PredHD = 8;
    internal const byte PredHU = 9;
    internal const byte PredDCTop = 10;
    internal const byte PredDCLeft = 11;
    internal const byte PredDCTopLeft = 12;

    internal void ParsePredModeY16(int mbx)
    {
        byte p;
        if (!_fp.ReadBit(156))
        {
            if (!_fp.ReadBit(163))
            {
                p = PredDC;
            }
            else
            {
                p = PredVE;
            }
        }
        else if (!_fp.ReadBit(128))
        {
            p = PredHE;
        }
        else
        {
            p = PredTM;
        }
        for (int i = 0; i < 4; i++)
        {
            _upMB[mbx].Pred[i] = p;
            _leftMB.Pred[i] = p;
        }
        _predY16 = p;
    }

    internal void ParsePredModeC8()
    {
        if (!_fp.ReadBit(142))
        {
            _predC8 = PredDC;
        }
        else if (!_fp.ReadBit(114))
        {
            _predC8 = PredVE;
        }
        else if (!_fp.ReadBit(183))
        {
            _predC8 = PredHE;
        }
        else
        {
            _predC8 = PredTM;
        }
    }

    internal void ParsePredModeY4(int mbx)
    {
        for (int j = 0; j < 4; j++)
        {
            byte p = _leftMB.Pred[j];
            for (int i = 0; i < 4; i++)
            {
                byte[] prob = PredProb[_upMB[mbx].Pred[i]][p];
                if (!_fp.ReadBit(prob[0]))
                {
                    p = PredDC;
                }
                else if (!_fp.ReadBit(prob[1]))
                {
                    p = PredTM;
                }
                else if (!_fp.ReadBit(prob[2]))
                {
                    p = PredVE;
                }
                else if (!_fp.ReadBit(prob[3]))
                {
                    if (!_fp.ReadBit(prob[4]))
                    {
                        p = PredHE;
                    }
                    else if (!_fp.ReadBit(prob[5]))
                    {
                        p = PredRD;
                    }
                    else
                    {
                        p = PredVR;
                    }
                }
                else if (!_fp.ReadBit(prob[6]))
                {
                    p = PredLD;
                }
                else if (!_fp.ReadBit(prob[7]))
                {
                    p = PredVL;
                }
                else if (!_fp.ReadBit(prob[8]))
                {
                    p = PredHD;
                }
                else
                {
                    p = PredHU;
                }
                _predY4[j, i] = p;
                _upMB[mbx].Pred[i] = p;
            }
            _leftMB.Pred[j] = p;
        }
    }

    // PredProb are the probabilities to decode a 4x4 region's predictor mode given
    // the predictor modes of the regions above and left of it.
    // These values are specified in section 11.5.
    internal static readonly byte[][][] PredProb =
    [
        [
            [231, 120, 48, 89, 115, 113, 120, 152, 112],
            [152, 179, 64, 126, 170, 118, 46, 70, 95],
            [175, 69, 143, 80, 85, 82, 72, 155, 103],
            [56, 58, 10, 171, 218, 189, 17, 13, 152],
            [114, 26, 17, 163, 44, 195, 21, 10, 173],
            [121, 24, 80, 195, 26, 62, 44, 64, 85],
            [144, 71, 10, 38, 171, 213, 144, 34, 26],
            [170, 46, 55, 19, 136, 160, 33, 206, 71],
            [63, 20, 8, 114, 114, 208, 12, 9, 226],
            [81, 40, 11, 96, 182, 84, 29, 16, 36],
        ],
        [
            [134, 183, 89, 137, 98, 101, 106, 165, 148],
            [72, 187, 100, 130, 157, 111, 32, 75, 80],
            [66, 102, 167, 99, 74, 62, 40, 234, 128],
            [41, 53, 9, 178, 241, 141, 26, 8, 107],
            [74, 43, 26, 146, 73, 166, 49, 23, 157],
            [65, 38, 105, 160, 51, 52, 31, 115, 128],
            [104, 79, 12, 27, 217, 255, 87, 17, 7],
            [87, 68, 71, 44, 114, 51, 15, 186, 23],
            [47, 41, 14, 110, 182, 183, 21, 17, 194],
            [66, 45, 25, 102, 197, 189, 23, 18, 22],
        ],
        [
            [88, 88, 147, 150, 42, 46, 45, 196, 205],
            [43, 97, 183, 117, 85, 38, 35, 179, 61],
            [39, 53, 200, 87, 26, 21, 43, 232, 171],
            [56, 34, 51, 104, 114, 102, 29, 93, 77],
            [39, 28, 85, 171, 58, 165, 90, 98, 64],
            [34, 22, 116, 206, 23, 34, 43, 166, 73],
            [107, 54, 32, 26, 51, 1, 81, 43, 31],
            [68, 25, 106, 22, 64, 171, 36, 225, 114],
            [34, 19, 21, 102, 132, 188, 16, 76, 124],
            [62, 18, 78, 95, 85, 57, 50, 48, 51],
        ],
        [
            [193, 101, 35, 159, 215, 111, 89, 46, 111],
            [60, 148, 31, 172, 219, 228, 21, 18, 111],
            [112, 113, 77, 85, 179, 255, 38, 120, 114],
            [40, 42, 1, 196, 245, 209, 10, 25, 109],
            [88, 43, 29, 140, 166, 213, 37, 43, 154],
            [61, 63, 30, 155, 67, 45, 68, 1, 209],
            [100, 80, 8, 43, 154, 1, 51, 26, 71],
            [142, 78, 78, 16, 255, 128, 34, 197, 171],
            [41, 40, 5, 102, 211, 183, 4, 1, 221],
            [51, 50, 17, 168, 209, 192, 23, 25, 82],
        ],
        [
            [138, 31, 36, 171, 27, 166, 38, 44, 229],
            [67, 87, 58, 169, 82, 115, 26, 59, 179],
            [63, 59, 90, 180, 59, 166, 93, 73, 154],
            [40, 40, 21, 116, 143, 209, 34, 39, 175],
            [47, 15, 16, 183, 34, 223, 49, 45, 183],
            [46, 17, 33, 183, 6, 98, 15, 32, 183],
            [57, 46, 22, 24, 128, 1, 54, 17, 37],
            [65, 32, 73, 115, 28, 128, 23, 128, 205],
            [40, 3, 9, 115, 51, 192, 18, 6, 223],
            [87, 37, 9, 115, 59, 77, 64, 21, 47],
        ],
        [
            [104, 55, 44, 218, 9, 54, 53, 130, 226],
            [64, 90, 70, 205, 40, 41, 23, 26, 57],
            [54, 57, 112, 184, 5, 41, 38, 166, 213],
            [30, 34, 26, 133, 152, 116, 10, 32, 134],
            [39, 19, 53, 221, 26, 114, 32, 73, 255],
            [31, 9, 65, 234, 2, 15, 1, 118, 73],
            [75, 32, 12, 51, 192, 255, 160, 43, 51],
            [88, 31, 35, 67, 102, 85, 55, 186, 85],
            [56, 21, 23, 111, 59, 205, 45, 37, 192],
            [55, 38, 70, 124, 73, 102, 1, 34, 98],
        ],
        [
            [125, 98, 42, 88, 104, 85, 117, 175, 82],
            [95, 84, 53, 89, 128, 100, 113, 101, 45],
            [75, 79, 123, 47, 51, 128, 81, 171, 1],
            [57, 17, 5, 71, 102, 57, 53, 41, 49],
            [38, 33, 13, 121, 57, 73, 26, 1, 85],
            [41, 10, 67, 138, 77, 110, 90, 47, 114],
            [115, 21, 2, 10, 102, 255, 166, 23, 6],
            [101, 29, 16, 10, 85, 128, 101, 196, 26],
            [57, 18, 10, 102, 102, 213, 34, 20, 43],
            [117, 20, 15, 36, 163, 128, 68, 1, 26],
        ],
        [
            [102, 61, 71, 37, 34, 53, 31, 243, 192],
            [69, 60, 71, 38, 73, 119, 28, 222, 37],
            [68, 45, 128, 34, 1, 47, 11, 245, 171],
            [62, 17, 19, 70, 146, 85, 55, 62, 70],
            [37, 43, 37, 154, 100, 163, 85, 160, 1],
            [63, 9, 92, 136, 28, 64, 32, 201, 85],
            [75, 15, 9, 9, 64, 255, 184, 119, 16],
            [86, 6, 28, 5, 64, 255, 25, 248, 1],
            [56, 8, 17, 132, 137, 255, 55, 116, 128],
            [58, 15, 20, 82, 135, 57, 26, 121, 40],
        ],
        [
            [164, 50, 31, 137, 154, 133, 25, 35, 218],
            [51, 103, 44, 131, 131, 123, 31, 6, 158],
            [86, 40, 64, 135, 148, 224, 45, 183, 128],
            [22, 26, 17, 131, 240, 154, 14, 1, 209],
            [45, 16, 21, 91, 64, 222, 7, 1, 197],
            [56, 21, 39, 155, 60, 138, 23, 102, 213],
            [83, 12, 13, 54, 192, 255, 68, 47, 28],
            [85, 26, 85, 85, 128, 128, 32, 146, 171],
            [18, 11, 7, 63, 144, 171, 4, 4, 246],
            [35, 27, 10, 146, 174, 171, 12, 26, 128],
        ],
        [
            [190, 80, 35, 99, 180, 80, 126, 54, 45],
            [85, 126, 47, 87, 176, 51, 41, 20, 32],
            [101, 75, 128, 139, 118, 146, 116, 128, 85],
            [56, 41, 15, 176, 236, 85, 37, 9, 62],
            [71, 30, 17, 119, 118, 255, 17, 18, 138],
            [101, 38, 60, 138, 55, 70, 43, 26, 142],
            [146, 36, 19, 30, 171, 255, 97, 27, 20],
            [138, 45, 61, 62, 219, 1, 81, 188, 64],
            [32, 41, 20, 117, 151, 142, 20, 21, 163],
            [112, 19, 12, 61, 195, 128, 48, 4, 24],
        ],
    ];

    internal delegate void PredFunc(Decoder d, int y, int x);

    internal static readonly PredFunc[] PredFunc4 =
    [
        PredFunc4Dc, PredFunc4Tm, PredFunc4Ve, PredFunc4He, PredFunc4Rd, PredFunc4Vr, PredFunc4Ld, PredFunc4Vl, PredFunc4Hd, PredFunc4Hu,
        null!, null!, null!,
    ];

    internal static readonly PredFunc[] PredFunc8 =
    [
        PredFunc8Dc, PredFunc8Tm, PredFunc8Ve, PredFunc8He,
        null!, null!, null!, null!, null!, null!,
        PredFunc8DcTop, PredFunc8DcLeft, PredFunc8DcTopLeft,
    ];

    internal static readonly PredFunc[] PredFunc16 =
    [
        PredFunc16Dc, PredFunc16Tm, PredFunc16Ve, PredFunc16He,
        null!, null!, null!, null!, null!, null!,
        PredFunc16DcTop, PredFunc16DcLeft, PredFunc16DcTopLeft,
    ];

    internal static byte CheckTopLeftPred(int mbx, int mby, byte p)
    {
        if (p != PredDC)
        {
            return p;
        }
        if (mbx == 0)
        {
            if (mby == 0)
            {
                return PredDCTopLeft;
            }
            return PredDCLeft;
        }
        if (mby == 0)
        {
            return PredDCTop;
        }
        return PredDC;
    }

    internal static void PredFunc4Dc(Decoder z, int y, int x)
    {
        uint sum = 4;
        for (int i = 0; i < 4; i++)
        {
            sum += z._ybr[y - 1][x + i];
        }
        for (int j = 0; j < 4; j++)
        {
            sum += z._ybr[y + j][x - 1];
        }
        byte avg = (byte)(sum / 8);
        for (int j = 0; j < 4; j++)
        {
            for (int i = 0; i < 4; i++)
            {
                z._ybr[y + j][x + i] = avg;
            }
        }
    }

    internal static void PredFunc4Tm(Decoder z, int y, int x)
    {
        int delta0 = -z._ybr[y - 1][x - 1];
        for (int j = 0; j < 4; j++)
        {
            int delta1 = delta0 + z._ybr[y + j][x - 1];
            for (int i = 0; i < 4; i++)
            {
                int delta2 = delta1 + z._ybr[y - 1][x + i];
                z._ybr[y + j][x + i] = (byte)System.Math.Clamp(delta2, 0, 255);
            }
        }
    }

    internal static void PredFunc4Ve(Decoder z, int y, int x)
    {
        int a = z._ybr[y - 1][x - 1];
        int b = z._ybr[y - 1][x + 0];
        int c = z._ybr[y - 1][x + 1];
        int d = z._ybr[y - 1][x + 2];
        int e = z._ybr[y - 1][x + 3];
        int f = z._ybr[y - 1][x + 4];
        byte abc = (byte)((a + 2 * b + c + 2) / 4);
        byte bcd = (byte)((b + 2 * c + d + 2) / 4);
        byte cde = (byte)((c + 2 * d + e + 2) / 4);
        byte def = (byte)((d + 2 * e + f + 2) / 4);
        for (int j = 0; j < 4; j++)
        {
            z._ybr[y + j][x + 0] = abc;
            z._ybr[y + j][x + 1] = bcd;
            z._ybr[y + j][x + 2] = cde;
            z._ybr[y + j][x + 3] = def;
        }
    }

    internal static void PredFunc4He(Decoder z, int y, int x)
    {
        int s = z._ybr[y + 3][x - 1];
        int r = z._ybr[y + 2][x - 1];
        int q = z._ybr[y + 1][x - 1];
        int p = z._ybr[y + 0][x - 1];
        int a = z._ybr[y - 1][x - 1];
        byte ssr = (byte)((s + 2 * s + r + 2) / 4);
        byte srq = (byte)((s + 2 * r + q + 2) / 4);
        byte rqp = (byte)((r + 2 * q + p + 2) / 4);
        byte apq = (byte)((a + 2 * p + q + 2) / 4);
        for (int i = 0; i < 4; i++)
        {
            z._ybr[y + 0][x + i] = apq;
            z._ybr[y + 1][x + i] = rqp;
            z._ybr[y + 2][x + i] = srq;
            z._ybr[y + 3][x + i] = ssr;
        }
    }

    internal static void PredFunc4Rd(Decoder z, int y, int x)
    {
        int s = z._ybr[y + 3][x - 1];
        int r = z._ybr[y + 2][x - 1];
        int q = z._ybr[y + 1][x - 1];
        int p = z._ybr[y + 0][x - 1];
        int a = z._ybr[y - 1][x - 1];
        int b = z._ybr[y - 1][x + 0];
        int c = z._ybr[y - 1][x + 1];
        int d = z._ybr[y - 1][x + 2];
        int e = z._ybr[y - 1][x + 3];
        byte srq = (byte)((s + 2 * r + q + 2) / 4);
        byte rqp = (byte)((r + 2 * q + p + 2) / 4);
        byte qpa = (byte)((q + 2 * p + a + 2) / 4);
        byte pab = (byte)((p + 2 * a + b + 2) / 4);
        byte abc = (byte)((a + 2 * b + c + 2) / 4);
        byte bcd = (byte)((b + 2 * c + d + 2) / 4);
        byte cde = (byte)((c + 2 * d + e + 2) / 4);
        z._ybr[y + 0][x + 0] = pab;
        z._ybr[y + 0][x + 1] = abc;
        z._ybr[y + 0][x + 2] = bcd;
        z._ybr[y + 0][x + 3] = cde;
        z._ybr[y + 1][x + 0] = qpa;
        z._ybr[y + 1][x + 1] = pab;
        z._ybr[y + 1][x + 2] = abc;
        z._ybr[y + 1][x + 3] = bcd;
        z._ybr[y + 2][x + 0] = rqp;
        z._ybr[y + 2][x + 1] = qpa;
        z._ybr[y + 2][x + 2] = pab;
        z._ybr[y + 2][x + 3] = abc;
        z._ybr[y + 3][x + 0] = srq;
        z._ybr[y + 3][x + 1] = rqp;
        z._ybr[y + 3][x + 2] = qpa;
        z._ybr[y + 3][x + 3] = pab;
    }

    internal static void PredFunc4Vr(Decoder z, int y, int x)
    {
        int r = z._ybr[y + 2][x - 1];
        int q = z._ybr[y + 1][x - 1];
        int p = z._ybr[y + 0][x - 1];
        int a = z._ybr[y - 1][x - 1];
        int b = z._ybr[y - 1][x + 0];
        int c = z._ybr[y - 1][x + 1];
        int d = z._ybr[y - 1][x + 2];
        int e = z._ybr[y - 1][x + 3];
        byte ab = (byte)((a + b + 1) / 2);
        byte bc = (byte)((b + c + 1) / 2);
        byte cd = (byte)((c + d + 1) / 2);
        byte de = (byte)((d + e + 1) / 2);
        byte rqp = (byte)((r + 2 * q + p + 2) / 4);
        byte qpa = (byte)((q + 2 * p + a + 2) / 4);
        byte pab = (byte)((p + 2 * a + b + 2) / 4);
        byte abc = (byte)((a + 2 * b + c + 2) / 4);
        byte bcd = (byte)((b + 2 * c + d + 2) / 4);
        byte cde = (byte)((c + 2 * d + e + 2) / 4);
        z._ybr[y + 0][x + 0] = ab;
        z._ybr[y + 0][x + 1] = bc;
        z._ybr[y + 0][x + 2] = cd;
        z._ybr[y + 0][x + 3] = de;
        z._ybr[y + 1][x + 0] = pab;
        z._ybr[y + 1][x + 1] = abc;
        z._ybr[y + 1][x + 2] = bcd;
        z._ybr[y + 1][x + 3] = cde;
        z._ybr[y + 2][x + 0] = qpa;
        z._ybr[y + 2][x + 1] = ab;
        z._ybr[y + 2][x + 2] = bc;
        z._ybr[y + 2][x + 3] = cd;
        z._ybr[y + 3][x + 0] = rqp;
        z._ybr[y + 3][x + 1] = pab;
        z._ybr[y + 3][x + 2] = abc;
        z._ybr[y + 3][x + 3] = bcd;
    }

    internal static void PredFunc4Ld(Decoder z, int y, int x)
    {
        int a = z._ybr[y - 1][x + 0];
        int b = z._ybr[y - 1][x + 1];
        int c = z._ybr[y - 1][x + 2];
        int d = z._ybr[y - 1][x + 3];
        int e = z._ybr[y - 1][x + 4];
        int f = z._ybr[y - 1][x + 5];
        int g = z._ybr[y - 1][x + 6];
        int h = z._ybr[y - 1][x + 7];
        byte abc = (byte)((a + 2 * b + c + 2) / 4);
        byte bcd = (byte)((b + 2 * c + d + 2) / 4);
        byte cde = (byte)((c + 2 * d + e + 2) / 4);
        byte def = (byte)((d + 2 * e + f + 2) / 4);
        byte efg = (byte)((e + 2 * f + g + 2) / 4);
        byte fgh = (byte)((f + 2 * g + h + 2) / 4);
        byte ghh = (byte)((g + 2 * h + h + 2) / 4);
        z._ybr[y + 0][x + 0] = abc;
        z._ybr[y + 0][x + 1] = bcd;
        z._ybr[y + 0][x + 2] = cde;
        z._ybr[y + 0][x + 3] = def;
        z._ybr[y + 1][x + 0] = bcd;
        z._ybr[y + 1][x + 1] = cde;
        z._ybr[y + 1][x + 2] = def;
        z._ybr[y + 1][x + 3] = efg;
        z._ybr[y + 2][x + 0] = cde;
        z._ybr[y + 2][x + 1] = def;
        z._ybr[y + 2][x + 2] = efg;
        z._ybr[y + 2][x + 3] = fgh;
        z._ybr[y + 3][x + 0] = def;
        z._ybr[y + 3][x + 1] = efg;
        z._ybr[y + 3][x + 2] = fgh;
        z._ybr[y + 3][x + 3] = ghh;
    }

    internal static void PredFunc4Vl(Decoder z, int y, int x)
    {
        int a = z._ybr[y - 1][x + 0];
        int b = z._ybr[y - 1][x + 1];
        int c = z._ybr[y - 1][x + 2];
        int d = z._ybr[y - 1][x + 3];
        int e = z._ybr[y - 1][x + 4];
        int f = z._ybr[y - 1][x + 5];
        int g = z._ybr[y - 1][x + 6];
        int h = z._ybr[y - 1][x + 7];
        byte ab = (byte)((a + b + 1) / 2);
        byte bc = (byte)((b + c + 1) / 2);
        byte cd = (byte)((c + d + 1) / 2);
        byte de = (byte)((d + e + 1) / 2);
        byte abc = (byte)((a + 2 * b + c + 2) / 4);
        byte bcd = (byte)((b + 2 * c + d + 2) / 4);
        byte cde = (byte)((c + 2 * d + e + 2) / 4);
        byte def = (byte)((d + 2 * e + f + 2) / 4);
        byte efg = (byte)((e + 2 * f + g + 2) / 4);
        byte fgh = (byte)((f + 2 * g + h + 2) / 4);
        z._ybr[y + 0][x + 0] = ab;
        z._ybr[y + 0][x + 1] = bc;
        z._ybr[y + 0][x + 2] = cd;
        z._ybr[y + 0][x + 3] = de;
        z._ybr[y + 1][x + 0] = abc;
        z._ybr[y + 1][x + 1] = bcd;
        z._ybr[y + 1][x + 2] = cde;
        z._ybr[y + 1][x + 3] = def;
        z._ybr[y + 2][x + 0] = bc;
        z._ybr[y + 2][x + 1] = cd;
        z._ybr[y + 2][x + 2] = de;
        z._ybr[y + 2][x + 3] = efg;
        z._ybr[y + 3][x + 0] = bcd;
        z._ybr[y + 3][x + 1] = cde;
        z._ybr[y + 3][x + 2] = def;
        z._ybr[y + 3][x + 3] = fgh;
    }

    internal static void PredFunc4Hd(Decoder z, int y, int x)
    {
        int s = z._ybr[y + 3][x - 1];
        int r = z._ybr[y + 2][x - 1];
        int q = z._ybr[y + 1][x - 1];
        int p = z._ybr[y + 0][x - 1];
        int a = z._ybr[y - 1][x - 1];
        int b = z._ybr[y - 1][x + 0];
        int c = z._ybr[y - 1][x + 1];
        int d = z._ybr[y - 1][x + 2];
        byte sr = (byte)((s + r + 1) / 2);
        byte rq = (byte)((r + q + 1) / 2);
        byte qp = (byte)((q + p + 1) / 2);
        byte pa = (byte)((p + a + 1) / 2);
        byte srq = (byte)((s + 2 * r + q + 2) / 4);
        byte rqp = (byte)((r + 2 * q + p + 2) / 4);
        byte qpa = (byte)((q + 2 * p + a + 2) / 4);
        byte pab = (byte)((p + 2 * a + b + 2) / 4);
        byte abc = (byte)((a + 2 * b + c + 2) / 4);
        byte bcd = (byte)((b + 2 * c + d + 2) / 4);
        z._ybr[y + 0][x + 0] = pa;
        z._ybr[y + 0][x + 1] = pab;
        z._ybr[y + 0][x + 2] = abc;
        z._ybr[y + 0][x + 3] = bcd;
        z._ybr[y + 1][x + 0] = qp;
        z._ybr[y + 1][x + 1] = qpa;
        z._ybr[y + 1][x + 2] = pa;
        z._ybr[y + 1][x + 3] = pab;
        z._ybr[y + 2][x + 0] = rq;
        z._ybr[y + 2][x + 1] = rqp;
        z._ybr[y + 2][x + 2] = qp;
        z._ybr[y + 2][x + 3] = qpa;
        z._ybr[y + 3][x + 0] = sr;
        z._ybr[y + 3][x + 1] = srq;
        z._ybr[y + 3][x + 2] = rq;
        z._ybr[y + 3][x + 3] = rqp;
    }

    internal static void PredFunc4Hu(Decoder z, int y, int x)
    {
        int s = z._ybr[y + 3][x - 1];
        int r = z._ybr[y + 2][x - 1];
        int q = z._ybr[y + 1][x - 1];
        int p = z._ybr[y + 0][x - 1];
        byte pq = (byte)((p + q + 1) / 2);
        byte qr = (byte)((q + r + 1) / 2);
        byte rs = (byte)((r + s + 1) / 2);
        byte pqr = (byte)((p + 2 * q + r + 2) / 4);
        byte qrs = (byte)((q + 2 * r + s + 2) / 4);
        byte rss = (byte)((r + 2 * s + s + 2) / 4);
        byte sss = (byte)s;
        z._ybr[y + 0][x + 0] = pq;
        z._ybr[y + 0][x + 1] = pqr;
        z._ybr[y + 0][x + 2] = qr;
        z._ybr[y + 0][x + 3] = qrs;
        z._ybr[y + 1][x + 0] = qr;
        z._ybr[y + 1][x + 1] = qrs;
        z._ybr[y + 1][x + 2] = rs;
        z._ybr[y + 1][x + 3] = rss;
        z._ybr[y + 2][x + 0] = rs;
        z._ybr[y + 2][x + 1] = rss;
        z._ybr[y + 2][x + 2] = sss;
        z._ybr[y + 2][x + 3] = sss;
        z._ybr[y + 3][x + 0] = sss;
        z._ybr[y + 3][x + 1] = sss;
        z._ybr[y + 3][x + 2] = sss;
        z._ybr[y + 3][x + 3] = sss;
    }

    internal static void PredFunc8Dc(Decoder z, int y, int x)
    {
        uint sum = 8;
        for (int i = 0; i < 8; i++)
        {
            sum += z._ybr[y - 1][x + i];
        }
        for (int j = 0; j < 8; j++)
        {
            sum += z._ybr[y + j][x - 1];
        }
        byte avg = (byte)(sum / 16);
        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                z._ybr[y + j][x + i] = avg;
            }
        }
    }

    internal static void PredFunc8Tm(Decoder z, int y, int x)
    {
        int delta0 = -z._ybr[y - 1][x - 1];
        for (int j = 0; j < 8; j++)
        {
            int delta1 = delta0 + z._ybr[y + j][x - 1];
            for (int i = 0; i < 8; i++)
            {
                int delta2 = delta1 + z._ybr[y - 1][x + i];
                z._ybr[y + j][x + i] = (byte)System.Math.Clamp(delta2, 0, 255);
            }
        }
    }

    internal static void PredFunc8Ve(Decoder z, int y, int x)
    {
        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                z._ybr[y + j][x + i] = z._ybr[y - 1][x + i];
            }
        }
    }

    internal static void PredFunc8He(Decoder z, int y, int x)
    {
        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                z._ybr[y + j][x + i] = z._ybr[y + j][x - 1];
            }
        }
    }

    internal static void PredFunc8DcTop(Decoder z, int y, int x)
    {
        uint sum = 4;
        for (int j = 0; j < 8; j++)
        {
            sum += z._ybr[y + j][x - 1];
        }
        byte avg = (byte)(sum / 8);
        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                z._ybr[y + j][x + i] = avg;
            }
        }
    }

    internal static void PredFunc8DcLeft(Decoder z, int y, int x)
    {
        uint sum = 4;
        for (int i = 0; i < 8; i++)
        {
            sum += z._ybr[y - 1][x + i];
        }
        byte avg = (byte)(sum / 8);
        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                z._ybr[y + j][x + i] = avg;
            }
        }
    }

    internal static void PredFunc8DcTopLeft(Decoder z, int y, int x)
    {
        for (int j = 0; j < 8; j++)
        {
            for (int i = 0; i < 8; i++)
            {
                z._ybr[y + j][x + i] = 0x80;
            }
        }
    }

    internal static void PredFunc16Dc(Decoder z, int y, int x)
    {
        uint sum = 16;
        for (int i = 0; i < 16; i++)
        {
            sum += z._ybr[y - 1][x + i];
        }
        for (int j = 0; j < 16; j++)
        {
            sum += z._ybr[y + j][x - 1];
        }
        byte avg = (byte)(sum / 32);
        for (int j = 0; j < 16; j++)
        {
            for (int i = 0; i < 16; i++)
            {
                z._ybr[y + j][x + i] = avg;
            }
        }
    }

    internal static void PredFunc16Tm(Decoder z, int y, int x)
    {
        int delta0 = -z._ybr[y - 1][x - 1];
        for (int j = 0; j < 16; j++)
        {
            int delta1 = delta0 + z._ybr[y + j][x - 1];
            for (int i = 0; i < 16; i++)
            {
                int delta2 = delta1 + z._ybr[y - 1][x + i];
                z._ybr[y + j][x + i] = (byte)System.Math.Clamp(delta2, 0, 255);
            }
        }
    }

    internal static void PredFunc16Ve(Decoder z, int y, int x)
    {
        for (int j = 0; j < 16; j++)
        {
            for (int i = 0; i < 16; i++)
            {
                z._ybr[y + j][x + i] = z._ybr[y - 1][x + i];
            }
        }
    }

    internal static void PredFunc16He(Decoder z, int y, int x)
    {
        for (int j = 0; j < 16; j++)
        {
            for (int i = 0; i < 16; i++)
            {
                z._ybr[y + j][x + i] = z._ybr[y + j][x - 1];
            }
        }
    }

    internal static void PredFunc16DcTop(Decoder z, int y, int x)
    {
        uint sum = 8;
        for (int j = 0; j < 16; j++)
        {
            sum += z._ybr[y + j][x - 1];
        }
        byte avg = (byte)(sum / 16);
        for (int j = 0; j < 16; j++)
        {
            for (int i = 0; i < 16; i++)
            {
                z._ybr[y + j][x + i] = avg;
            }
        }
    }

    internal static void PredFunc16DcLeft(Decoder z, int y, int x)
    {
        uint sum = 8;
        for (int i = 0; i < 16; i++)
        {
            sum += z._ybr[y - 1][x + i];
        }
        byte avg = (byte)(sum / 16);
        for (int j = 0; j < 16; j++)
        {
            for (int i = 0; i < 16; i++)
            {
                z._ybr[y + j][x + i] = avg;
            }
        }
    }

    internal static void PredFunc16DcTopLeft(Decoder z, int y, int x)
    {
        for (int j = 0; j < 16; j++)
        {
            for (int i = 0; i < 16; i++)
            {
                z._ybr[y + j][x + i] = 0x80;
            }
        }
    }
}