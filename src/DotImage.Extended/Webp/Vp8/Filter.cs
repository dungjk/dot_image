// Ported from Go golang.org/x/image/vp8/filter.go

using System;

namespace DotImage.Extended.Webp.Vp8;

// FilterParam holds the loop filter parameters for a macroblock.
internal struct FilterParam
{
    // The first three fields are thresholds used by the loop filter to smooth
    // over the edges and interior of a macroblock. Level is used by both the
    // simple and normal filters. The inner level and high edge variance level
    // are only used by the normal filter.
    internal byte Level;
    internal byte ILevel;
    internal byte HLevel;
    // Inner is whether the inner loop filter cannot be optimized out as a
    // no-op for this particular macroblock.
    internal bool Inner;
}

// This file implements the loop filter, as specified in chapter 15.
public sealed partial class Decoder
{
    // Filter2 modifies a 2-pixel wide or 2-pixel high band along an edge.
    internal static void Filter2(Span<byte> pix, int level, int index, int iStep, int jStep)
    {
        for (int n = 16; n > 0; n--, index += iStep)
        {
            int p1 = pix[index - 2 * jStep];
            int p0 = pix[index - 1 * jStep];
            int q0 = pix[index + 0 * jStep];
            int q1 = pix[index + 1 * jStep];
            if ((Abs(p0 - q0) << 1) + (Abs(p1 - q1) >> 1) > level)
            {
                continue;
            }
            int a = 3 * (q0 - p0) + Clamp127(p1 - q1);
            int a1 = Clamp15((a + 4) >> 3);
            int a2 = Clamp15((a + 3) >> 3);
            pix[index - 1 * jStep] = Clamp255(p0 + a2);
            pix[index + 0 * jStep] = Clamp255(q0 - a1);
        }
    }

    // Filter246 modifies a 2-, 4- or 6-pixel wide or high band along an edge.
    internal static void Filter246(Span<byte> pix, int nInitial, int level, int ilevel, int hlevel, int index, int iStep, int jStep, bool fourNotSix)
    {
        int n = nInitial;
        for (; n > 0; n--, index += iStep)
        {
            int p3 = pix[index - 4 * jStep];
            int p2 = pix[index - 3 * jStep];
            int p1 = pix[index - 2 * jStep];
            int p0 = pix[index - 1 * jStep];
            int q0 = pix[index + 0 * jStep];
            int q1 = pix[index + 1 * jStep];
            int q2 = pix[index + 2 * jStep];
            int q3 = pix[index + 3 * jStep];
            if ((Abs(p0 - q0) << 1) + (Abs(p1 - q1) >> 1) > level)
            {
                continue;
            }
            if (Abs(p3 - p2) > ilevel ||
                Abs(p2 - p1) > ilevel ||
                Abs(p1 - p0) > ilevel ||
                Abs(q1 - q0) > ilevel ||
                Abs(q2 - q1) > ilevel ||
                Abs(q3 - q2) > ilevel)
            {
                continue;
            }
            if (Abs(p1 - p0) > hlevel || Abs(q1 - q0) > hlevel)
            {
                // Filter 2 pixels.
                int a = 3 * (q0 - p0) + Clamp127(p1 - q1);
                int a1 = Clamp15((a + 4) >> 3);
                int a2 = Clamp15((a + 3) >> 3);
                pix[index - 1 * jStep] = Clamp255(p0 + a2);
                pix[index + 0 * jStep] = Clamp255(q0 - a1);
            }
            else if (fourNotSix)
            {
                // Filter 4 pixels.
                int a = 3 * (q0 - p0);
                int a1 = Clamp15((a + 4) >> 3);
                int a2 = Clamp15((a + 3) >> 3);
                int a3 = (a1 + 1) >> 1;
                pix[index - 2 * jStep] = Clamp255(p1 + a3);
                pix[index - 1 * jStep] = Clamp255(p0 + a2);
                pix[index + 0 * jStep] = Clamp255(q0 - a1);
                pix[index + 1 * jStep] = Clamp255(q1 - a3);
            }
            else
            {
                // Filter 6 pixels.
                int a = Clamp127(3 * (q0 - p0) + Clamp127(p1 - q1));
                int a1 = (27 * a + 63) >> 7;
                int a2 = (18 * a + 63) >> 7;
                int a3 = (9 * a + 63) >> 7;
                pix[index - 3 * jStep] = Clamp255(p2 + a3);
                pix[index - 2 * jStep] = Clamp255(p1 + a2);
                pix[index - 1 * jStep] = Clamp255(p0 + a1);
                pix[index + 0 * jStep] = Clamp255(q0 - a1);
                pix[index + 1 * jStep] = Clamp255(q1 - a2);
                pix[index + 2 * jStep] = Clamp255(q2 - a3);
            }
        }
    }

    // SimpleFilter implements the simple filter, as specified in section 15.2.
    internal void SimpleFilter()
    {
        for (int mby = 0; mby < _mbh; mby++)
        {
            for (int mbx = 0; mbx < _mbw; mbx++)
            {
                FilterParam f = _perMBFilterParams[_mbw * mby + mbx];
                if (f.Level == 0)
                {
                    continue;
                }
                int l = f.Level;
                int yIndex = (mby * _img!.YStride + mbx) * 16;
                if (mbx > 0)
                {
                    Filter2(_img.Y.Span, l + 4, yIndex, _img.YStride, 1);
                }
                if (f.Inner)
                {
                    Filter2(_img.Y.Span, l, yIndex + 0x4, _img.YStride, 1);
                    Filter2(_img.Y.Span, l, yIndex + 0x8, _img.YStride, 1);
                    Filter2(_img.Y.Span, l, yIndex + 0xc, _img.YStride, 1);
                }
                if (mby > 0)
                {
                    Filter2(_img.Y.Span, l + 4, yIndex, 1, _img.YStride);
                }
                if (f.Inner)
                {
                    Filter2(_img.Y.Span, l, yIndex + _img.YStride * 0x4, 1, _img.YStride);
                    Filter2(_img.Y.Span, l, yIndex + _img.YStride * 0x8, 1, _img.YStride);
                    Filter2(_img.Y.Span, l, yIndex + _img.YStride * 0xc, 1, _img.YStride);
                }
            }
        }
    }

    // NormalFilter implements the normal filter, as specified in section 15.3.
    internal void NormalFilter()
    {
        for (int mby = 0; mby < _mbh; mby++)
        {
            for (int mbx = 0; mbx < _mbw; mbx++)
            {
                FilterParam f = _perMBFilterParams[_mbw * mby + mbx];
                if (f.Level == 0)
                {
                    continue;
                }
                int l = f.Level, il = f.ILevel, hl = f.HLevel;
                int yIndex = (mby * _img!.YStride + mbx) * 16;
                int cIndex = (mby * _img.CStride + mbx) * 8;
                if (mbx > 0)
                {
                    Filter246(_img.Y.Span, 16, l + 4, il, hl, yIndex, _img.YStride, 1, false);
                    Filter246(_img.Cb.Span, 8, l + 4, il, hl, cIndex, _img.CStride, 1, false);
                    Filter246(_img.Cr.Span, 8, l + 4, il, hl, cIndex, _img.CStride, 1, false);
                }
                if (f.Inner)
                {
                    Filter246(_img.Y.Span, 16, l, il, hl, yIndex + 0x4, _img.YStride, 1, true);
                    Filter246(_img.Y.Span, 16, l, il, hl, yIndex + 0x8, _img.YStride, 1, true);
                    Filter246(_img.Y.Span, 16, l, il, hl, yIndex + 0xc, _img.YStride, 1, true);
                    Filter246(_img.Cb.Span, 8, l, il, hl, cIndex + 0x4, _img.CStride, 1, true);
                    Filter246(_img.Cr.Span, 8, l, il, hl, cIndex + 0x4, _img.CStride, 1, true);
                }
                if (mby > 0)
                {
                    Filter246(_img.Y.Span, 16, l + 4, il, hl, yIndex, 1, _img.YStride, false);
                    Filter246(_img.Cb.Span, 8, l + 4, il, hl, cIndex, 1, _img.CStride, false);
                    Filter246(_img.Cr.Span, 8, l + 4, il, hl, cIndex, 1, _img.CStride, false);
                }
                if (f.Inner)
                {
                    Filter246(_img.Y.Span, 16, l, il, hl, yIndex + _img.YStride * 0x4, 1, _img.YStride, true);
                    Filter246(_img.Y.Span, 16, l, il, hl, yIndex + _img.YStride * 0x8, 1, _img.YStride, true);
                    Filter246(_img.Y.Span, 16, l, il, hl, yIndex + _img.YStride * 0xc, 1, _img.YStride, true);
                    Filter246(_img.Cb.Span, 8, l, il, hl, cIndex + _img.CStride * 0x4, 1, _img.CStride, true);
                    Filter246(_img.Cr.Span, 8, l, il, hl, cIndex + _img.CStride * 0x4, 1, _img.CStride, true);
                }
            }
        }
    }

    // ComputeFilterParams computes the loop filter parameters, as specified in
    // section 15.4.
    internal void ComputeFilterParams()
    {
        for (int i = 0; i < NSegment; i++)
        {
            int baseLevel = _filterHeader.Level;
            if (_segmentHeader.UseSegment)
            {
                baseLevel = _segmentHeader.FilterStrength[i];
                if (_segmentHeader.RelativeDelta)
                {
                    baseLevel += _filterHeader.Level;
                }
            }

            for (int j = 0; j < 2; j++)
            {
                ref FilterParam p = ref _filterParams[i, j];
                p.Inner = j != 0;
                int level = baseLevel;
                if (_filterHeader.UseLFDelta)
                {
                    // The libwebp C code has a "TODO: only CURRENT is handled for now."
                    level += _filterHeader.RefLFDelta[0];
                    if (j != 0)
                    {
                        level += _filterHeader.ModeLFDelta[0];
                    }
                }
                if (level <= 0)
                {
                    p.Level = 0;
                    continue;
                }
                if (level > 63)
                {
                    level = 63;
                }
                int ilevel = level;
                if (_filterHeader.Sharpness > 0)
                {
                    if (_filterHeader.Sharpness > 4)
                    {
                        ilevel >>= 2;
                    }
                    else
                    {
                        ilevel >>= 1;
                    }
                    sbyte x = (sbyte)(9 - _filterHeader.Sharpness);
                    if (ilevel > x)
                    {
                        ilevel = x;
                    }
                }
                if (ilevel < 1)
                {
                    ilevel = 1;
                }
                p.ILevel = (byte)ilevel;
                p.Level = (byte)(2 * level + ilevel);
                if (_frameHeader.KeyFrame)
                {
                    if (level < 15)
                    {
                        p.HLevel = 0;
                    }
                    else if (level < 40)
                    {
                        p.HLevel = 1;
                    }
                    else
                    {
                        p.HLevel = 2;
                    }
                }
                else
                {
                    if (level < 15)
                    {
                        p.HLevel = 0;
                    }
                    else if (level < 20)
                    {
                        p.HLevel = 1;
                    }
                    else if (level < 40)
                    {
                        p.HLevel = 2;
                    }
                    else
                    {
                        p.HLevel = 3;
                    }
                }
            }
        }
    }

    internal static int Abs(int x)
    {
        // In two's complement representation, the negative number
        // of any number (except the smallest one) can be computed
        // by flipping all the bits and add 1. This is faster than
        // code with a branch.
        // See Hacker's Delight, section 2-4.
        return (x ^ (x >> 31)) - (x >> 31);
    }

    internal static int Clamp15(int x)
    {
        if (x < -16)
        {
            return -16;
        }
        if (x > 15)
        {
            return 15;
        }
        return x;
    }

    internal static int Clamp127(int x)
    {
        if (x < -128)
        {
            return -128;
        }
        if (x > 127)
        {
            return 127;
        }
        return x;
    }

    internal static byte Clamp255(int x)
    {
        if (x < 0)
        {
            return 0;
        }
        if (x > 255)
        {
            return 255;
        }
        return (byte)x;
    }
}