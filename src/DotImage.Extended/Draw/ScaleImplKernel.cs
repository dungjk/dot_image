// Ported from golang.org/x/image/draw/scale.go and golang.org/x/image/draw/impl.go

using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Math.F64;

namespace DotImage.Extended.Draw;

internal sealed class NnInterpolator : Interpolator
{
    public void Scale(IWritableImage dst, Rect dr, IImage src, Rect sr, Op op, Options? opts) =>
        ScaleImpl.NnScale(dst, dr, src, sr, op, opts);

    public void Transform(IWritableImage dst, Aff3 m, IImage src, Rect sr, Op op, Options? opts) =>
        ScaleImpl.NnTransform(dst, m, src, sr, op, opts);
}

internal sealed class AblInterpolator : Interpolator
{
    public void Scale(IWritableImage dst, Rect dr, IImage src, Rect sr, Op op, Options? opts) =>
        ScaleImpl.AblScale(dst, dr, src, sr, op, opts);

    public void Transform(IWritableImage dst, Aff3 m, IImage src, Rect sr, Op op, Options? opts) =>
        ScaleImpl.AblTransform(dst, m, src, sr, op, opts);
}

internal struct Source
{
    // I and J are the indices of the first and last contribs for this source
    // column or row.
    internal int I, J;
    // InvTotalWeight is the reciprocal of the total weight of this source
    // column or row.
    internal double InvTotalWeight;
    // InvTotalWeightFFFF is like InvTotalWeight, but for 0xffff instead of 1.
    internal double InvTotalWeightFFFF;
}

internal struct Contrib
{
    internal int Coord;
    internal double Weight;
}

internal sealed class Distrib
{
    // Sources are what contribs each column or row in the source image owns,
    // and the total weight of those contribs.
    internal Source[] Sources;
    // Contribs are the contributions indexed by sources[s].I and sources[s].J.
    internal Contrib[] Contribs;

    internal Distrib(Source[] sources, Contrib[] contribs)
    {
        Sources = sources;
        Contribs = contribs;
    }
}

internal sealed partial class KernelScaler : Scaler
{
    internal Kernel Kernel;
    internal int Dw, Dh, Sw, Sh;
    internal Distrib Horizontal, Vertical;

    public KernelScaler(Kernel kernel, int dw, int dh, int sw, int sh)
    {
        Kernel = kernel;
        Dw = dw;
        Dh = dh;
        Sw = sw;
        Sh = sh;
        Horizontal = ScaleImpl.NewDistrib(kernel, dw, sw);
        Vertical = ScaleImpl.NewDistrib(kernel, dh, sh);
    }
}