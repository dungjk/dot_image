// Ported from golang.org/x/image/draw/scale.go and golang.org/x/image/draw/impl.go

using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Math.F64;
using DotMath = System.Math;

namespace DotImage.Extended.Draw;

internal static partial class ScaleImpl
{
    // newDistrib returns a distrib that distributes sw source columns (or rows)
    // over dw destination columns (or rows).
    internal static Distrib NewDistrib(Kernel q, int dw, int sw)
    {
        double scale = (double)sw / dw;
        double halfWidth = q.Support, kernelArgScale = 1.0;
        // When shrinking, broaden the effective kernel support so that we still
        // visit every source pixel.
        if (scale > 1)
        {
            halfWidth *= scale;
            kernelArgScale = 1 / scale;
        }

        // Make the sources slice, one source for each column or row, and
        // temporarily appropriate its elements' fields so that invTotalWeight is
        // the scaled coordinate of the source column or row, and i and j are the
        // lower and upper bounds of the range of destination columns or rows
        // affected by the source column or row.
        int n = 0;
        var sources = new Source[dw];
        for (int x = 0; x < dw; x++)
        {
            double center = (x + 0.5) * scale - 0.5;
            int i = (int)DotMath.Floor(center - halfWidth);
            if (i < 0)
            {
                i = 0;
            }
            int j = (int)DotMath.Ceiling(center + halfWidth);
            if (j > sw)
            {
                j = sw;
                if (j < i)
                {
                    j = i;
                }
            }
            sources[x] = new Source { I = i, J = j, InvTotalWeight = center };
            n += j - i;
        }

        var contribs = new Contrib[n];
        int l = 0;
        for (int k = 0; k < sources.Length; k++)
        {
            var b = sources[k];
            double totalWeight = 0.0;
            int start = l;
            for (int coord = b.I; coord < b.J; coord++)
            {
                double t = Abs((b.InvTotalWeight - coord) * kernelArgScale);
                if (t >= q.Support)
                {
                    continue;
                }
                double weight = q.At(t);
                if (weight == 0)
                {
                    continue;
                }
                totalWeight += weight;
                contribs[l++] = new Contrib { Coord = coord, Weight = weight };
            }
            totalWeight = 1 / totalWeight;
            sources[k] = new Source
            {
                I = start,
                J = l,
                InvTotalWeight = totalWeight,
                InvTotalWeightFFFF = totalWeight / 0xffff,
            };
        }

        return new Distrib(sources, contribs);
    }
}
