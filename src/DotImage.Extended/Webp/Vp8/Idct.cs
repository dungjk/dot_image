// Ported from Go golang.org/x/image/vp8/idct.go

namespace DotImage.Extended.Webp.Vp8;

// This file implements the inverse Discrete Cosine Transform and the inverse
// Walsh Hadamard Transform (WHT), as specified in sections 14.3 and 14.4.
public sealed partial class Decoder
{
    internal static byte Clip8(int i)
    {
        if (i < 0)
        {
            return 0;
        }
        if (i > 255)
        {
            return 255;
        }
        return (byte)i;
    }

    internal void InverseDct4(int y, int x, int coeffBase)
    {
        const int c1 = 85627; // 65536 * cos(pi/8) * sqrt(2).
        const int c2 = 35468; // 65536 * sin(pi/8) * sqrt(2).
        var m = new int[4, 4];
        int cb = coeffBase;
        for (int i = 0; i < 4; i++)
        {
            int a = _coeff[cb + 0] + _coeff[cb + 8];
            int b = _coeff[cb + 0] - _coeff[cb + 8];
            int c = ((_coeff[cb + 4] * c2) >> 16) - ((_coeff[cb + 12] * c1) >> 16);
            int d = ((_coeff[cb + 4] * c1) >> 16) + ((_coeff[cb + 12] * c2) >> 16);
            m[i, 0] = a + d;
            m[i, 1] = b + c;
            m[i, 2] = b - c;
            m[i, 3] = a - d;
            cb++;
        }
        for (int j = 0; j < 4; j++)
        {
            int dc = m[0, j] + 4;
            int a = dc + m[2, j];
            int b = dc - m[2, j];
            int c = ((m[1, j] * c2) >> 16) - ((m[3, j] * c1) >> 16);
            int d = ((m[1, j] * c1) >> 16) + ((m[3, j] * c2) >> 16);
            _ybr[y + j][x + 0] = Clip8(_ybr[y + j][x + 0] + ((a + d) >> 3));
            _ybr[y + j][x + 1] = Clip8(_ybr[y + j][x + 1] + ((b + c) >> 3));
            _ybr[y + j][x + 2] = Clip8(_ybr[y + j][x + 2] + ((b - c) >> 3));
            _ybr[y + j][x + 3] = Clip8(_ybr[y + j][x + 3] + ((a - d) >> 3));
        }
    }

    internal void InverseDct4DcOnly(int y, int x, int coeffBase)
    {
        int dc = (_coeff[coeffBase + 0] + 4) >> 3;
        for (int j = 0; j < 4; j++)
        {
            for (int i = 0; i < 4; i++)
            {
                _ybr[y + j][x + i] = Clip8(_ybr[y + j][x + i] + dc);
            }
        }
    }

    internal void InverseDct8(int y, int x, int coeffBase)
    {
        InverseDct4(y + 0, x + 0, coeffBase + 0 * 16);
        InverseDct4(y + 0, x + 4, coeffBase + 1 * 16);
        InverseDct4(y + 4, x + 0, coeffBase + 2 * 16);
        InverseDct4(y + 4, x + 4, coeffBase + 3 * 16);
    }

    internal void InverseDct8DcOnly(int y, int x, int coeffBase)
    {
        InverseDct4DcOnly(y + 0, x + 0, coeffBase + 0 * 16);
        InverseDct4DcOnly(y + 0, x + 4, coeffBase + 1 * 16);
        InverseDct4DcOnly(y + 4, x + 0, coeffBase + 2 * 16);
        InverseDct4DcOnly(y + 4, x + 4, coeffBase + 3 * 16);
    }

    internal void InverseWht16()
    {
        var m = new int[16];
        for (int i = 0; i < 4; i++)
        {
            int a0 = _coeff[384 + 0 + i] + _coeff[384 + 12 + i];
            int a1 = _coeff[384 + 4 + i] + _coeff[384 + 8 + i];
            int a2 = _coeff[384 + 4 + i] - _coeff[384 + 8 + i];
            int a3 = _coeff[384 + 0 + i] - _coeff[384 + 12 + i];
            m[0 + i] = a0 + a1;
            m[8 + i] = a0 - a1;
            m[4 + i] = a3 + a2;
            m[12 + i] = a3 - a2;
        }
        int out0 = 0;
        for (int i = 0; i < 4; i++)
        {
            int dc = m[0 + i * 4] + 3;
            int a0 = dc + m[3 + i * 4];
            int a1 = m[1 + i * 4] + m[2 + i * 4];
            int a2 = m[1 + i * 4] - m[2 + i * 4];
            int a3 = dc - m[3 + i * 4];
            _coeff[out0 + 0] = (short)((a0 + a1) >> 3);
            _coeff[out0 + 16] = (short)((a3 + a2) >> 3);
            _coeff[out0 + 32] = (short)((a0 - a1) >> 3);
            _coeff[out0 + 48] = (short)((a3 - a2) >> 3);
            out0 += 64;
        }
    }
}