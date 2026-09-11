// Ported from golang.org/x/image/draw/scale.go

using DotImage.Color;
using DotImage.Draw;
using DotImage.Extended.Math.F64;

namespace DotImage.Extended.Draw;

public static class ScaleOps
{
    /// <summary>
    /// Copy copies the part of the source image defined by src and sr and writes
    /// the result of a Porter-Duff composition to the part of the destination image
    /// defined by dst and the translation of sr so that sr.Min translates to dp.
    /// </summary>
    public static void Copy(IWritableImage dst, Point dp, IImage src, Rect sr, Op op, Options? opts)
    {
        var o = opts ?? default;
        Rect dr = sr.Add(dp.Sub(sr.Min));
        if (o.DstMask == null)
            DrawOps.DrawMask(dst, dr, src, sr.Min, o.SrcMask, o.SrcMaskP.Add(sr.Min), op);
        else
            Interpolators.NearestNeighbor.Scale(dst, dr, src, sr, op, opts);
    }

    internal static (Rect, IImage?) ClipAffectedDestRect(Rect adr, IImage? dstMask, Point dstMaskP)
    {
        if (dstMask == null)
        {
            return (adr, null);
        }
        if (dstMask is Rect r)
        {
            return (adr.Intersect(r.Sub(dstMaskP)), null);
        }
        return (adr, dstMask);
    }

    internal static bool Opaque(IImage m) =>
        m is IWritableImage wi && wi.Opaque() ||
        m is UniformImage ui && ui.Opaque();
}

/// <summary>
/// Scaler scales the part of the source image defined by src and sr and writes
/// the result of a Porter-Duff composition to the part of the destination image
/// defined by dst and dr.
/// </summary>
public interface Scaler
{
    void Scale(IWritableImage dst, Rect dr, IImage src, Rect sr, Op op, Options? opts);
}

/// <summary>
/// Transformer transforms the part of the source image defined by src and sr
/// and writes the result of a Porter-Duff composition to the part of the
/// destination image defined by dst and the affine transform m applied to sr.
/// </summary>
public interface Transformer
{
    void Transform(IWritableImage dst, Aff3 m, IImage src, Rect sr, Op op, Options? opts);
}

/// <summary>
/// Interpolator is an interpolation algorithm, when dst and src pixels don't
/// have a 1:1 correspondence.
/// </summary>
public interface Interpolator : Scaler, Transformer;

/// <summary>
/// Options are optional parameters to Copy, Scale and Transform.
/// </summary>
public struct Options
{
    public IImage? DstMask;
    public Point DstMaskP;
    public IImage? SrcMask;
    public Point SrcMaskP;
}

/// <summary>
/// Kernel is an interpolator that blends source pixels weighted by a symmetric
/// kernel function.
/// </summary>
public sealed class Kernel : Interpolator
{
    /// <summary>Kernel support; must be &gt;= 0. At(t) is assumed to be zero when t &gt;= Support.</summary>
    public double Support;
    /// <summary>Kernel function; only called with t in the range [0, Support).</summary>
    public Func<double, double> At;

    public Kernel(double support, Func<double, double> at)
    {
        Support = support;
        At = at;
    }

    /// <summary>Scale implements the Scaler interface.</summary>
    public void Scale(IWritableImage dst, Rect dr, IImage src, Rect sr, Op op, Options? opts) =>
        new KernelScaler(this, dr.Dx(), dr.Dy(), sr.Dx(), sr.Dy()).Scale(dst, dr, src, sr, op, opts);

    /// <summary>
    /// NewScaler returns a Scaler that is optimized for scaling multiple times with
    /// the same fixed destination and source width and height.
    /// </summary>
    public Scaler NewScaler(int dw, int dh, int sw, int sh) =>
        new KernelScaler(this, dw, dh, sw, sh);

    /// <summary>Transform implements the Transformer interface.</summary>
    public void Transform(IWritableImage dst, Aff3 m, IImage src, Rect sr, Op op, Options? opts) =>
        ScaleImpl.KernelTransform(this, dst, m, src, sr, op, opts);
}

public static class Interpolators
{
    /// <summary>
    /// NearestNeighbor is the nearest neighbor interpolator. It is very fast,
    /// but usually gives very low quality results. When scaling up, the result
    /// will look 'blocky'.
    /// </summary>
    public static readonly Interpolator NearestNeighbor = new NnInterpolator();

    /// <summary>
    /// ApproxBiLinear is a mixture of the nearest neighbor and bi-linear
    /// interpolators. It is fast, but usually gives medium quality results.
    /// </summary>
    public static readonly Interpolator ApproxBiLinear = new AblInterpolator();

    /// <summary>BiLinear is the tent kernel. It is slow, but usually gives high quality results.</summary>
    public static readonly Interpolator BiLinear = new Kernel(1, t => 1 - t);

    /// <summary>
    /// CatmullRom is the Catmull-Rom kernel. It is very slow, but usually gives
    /// very high quality results.
    /// </summary>
    public static readonly Interpolator CatmullRom = new Kernel(2, t => t < 1
        ? (1.5 * t - 2.5) * t * t + 1
        : ((-0.5 * t + 2.5) * t - 4) * t + 2);
}