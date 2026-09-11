using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotImage;
using DotImage.Draw;
using DotImage.Extended.Draw;
using DotImage.Extended.Math.F64;
using DotImage.Jpeg;

[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class DrawScaleBench
{
    private NrgbaImage _src = null!;
    private Rect _srcBounds;
    private Rect _dstBounds;
    private Scaler _catmullScaler = null!;

    [GlobalSetup]
    public void Setup()
    {
        using var input = new MemoryStream(Bench.LoadJpeg());
        _src = Bench.ToNrgba(JpegReader.Decode(input));
        _srcBounds = _src.Bounds();
        _dstBounds = Geometry.Rect(0, 0, Bench.TargetWidth, Bench.TargetHeight);
        _catmullScaler = ((Kernel)Interpolators.CatmullRom).NewScaler(
            Bench.TargetWidth, Bench.TargetHeight, _srcBounds.Dx(), _srcBounds.Dy());
    }

    private NrgbaImage NewDst() => Images.NewNrgba(_dstBounds);

    [Benchmark]
    public void NearestNeighbor()
    {
        var dst = NewDst();
        Interpolators.NearestNeighbor.Scale(dst, _dstBounds, _src, _srcBounds, Op.Src, null);
    }

    [Benchmark]
    public void ApproxBiLinear()
    {
        var dst = NewDst();
        Interpolators.ApproxBiLinear.Scale(dst, _dstBounds, _src, _srcBounds, Op.Src, null);
    }

    [Benchmark]
    public void BiLinear()
    {
        var dst = NewDst();
        Interpolators.BiLinear.Scale(dst, _dstBounds, _src, _srcBounds, Op.Src, null);
    }

    [Benchmark]
    public void CatmullRom()
    {
        var dst = NewDst();
        Interpolators.CatmullRom.Scale(dst, _dstBounds, _src, _srcBounds, Op.Src, null);
    }

    [Benchmark]
    public void CatmullRomReusedScaler()
    {
        var dst = NewDst();
        _catmullScaler.Scale(dst, _dstBounds, _src, _srcBounds, Op.Src, null);
    }
}

[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class DrawScaleUpBench
{
    private NrgbaImage _src = null!;
    private Rect _srcBounds;
    private Rect _dstBounds;

    [GlobalSetup]
    public void Setup()
    {
        _src = Images.NewNrgba(Geometry.Rect(0, 0, Bench.TargetWidth, Bench.TargetHeight));
        Bench.FillPattern(_src);
        _srcBounds = _src.Bounds();
        _dstBounds = Geometry.Rect(0, 0, _srcBounds.Dx() * 2, _srcBounds.Dy() * 2);
    }

    [Benchmark]
    public void NearestNeighbor()
    {
        var dst = Images.NewNrgba(_dstBounds);
        Interpolators.NearestNeighbor.Scale(dst, _dstBounds, _src, _srcBounds, Op.Src, null);
    }

    [Benchmark]
    public void ApproxBiLinear()
    {
        var dst = Images.NewNrgba(_dstBounds);
        Interpolators.ApproxBiLinear.Scale(dst, _dstBounds, _src, _srcBounds, Op.Src, null);
    }

    [Benchmark]
    public void CatmullRom()
    {
        var dst = Images.NewNrgba(_dstBounds);
        Interpolators.CatmullRom.Scale(dst, _dstBounds, _src, _srcBounds, Op.Src, null);
    }
}

[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class DrawTransformBench
{
    private NrgbaImage _src = null!;
    private NrgbaImage _dst = null!;
    private Rect _srcBounds;
    private Aff3 _matrix;

    [GlobalSetup]
    public void Setup()
    {
        using var input = new MemoryStream(Bench.LoadJpeg());
        _src = Bench.ToNrgba(JpegReader.Decode(input));
        _srcBounds = _src.Bounds();
        _dst = Images.NewNrgba(_srcBounds);
        const double cos30 = 0.866025404;
        const double sin30 = 0.5;
        _matrix = new Aff3(cos30, -sin30, 10, sin30, cos30, 20);
    }

    [Benchmark]
    public void NearestNeighbor()
    {
        Interpolators.NearestNeighbor.Transform(_dst, _matrix, _src, _srcBounds, Op.Src, null);
    }

    [Benchmark]
    public void ApproxBiLinear()
    {
        Interpolators.ApproxBiLinear.Transform(_dst, _matrix, _src, _srcBounds, Op.Src, null);
    }

    [Benchmark]
    public void CatmullRom()
    {
        Interpolators.CatmullRom.Transform(_dst, _matrix, _src, _srcBounds, Op.Src, null);
    }
}

[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class DrawCopyBench
{
    private NrgbaImage _src = null!;
    private Rect _bounds;

    [GlobalSetup]
    public void Setup()
    {
        using var input = new MemoryStream(Bench.LoadJpeg());
        _src = Bench.ToNrgba(JpegReader.Decode(input));
        _bounds = _src.Bounds();
    }

    [Benchmark]
    public void Copy1to1()
    {
        var dst = Images.NewNrgba(_bounds);
        ScaleOps.Copy(dst, _bounds.Min, _src, _bounds, Op.Src, null);
    }

    [Benchmark]
    public void NearestNeighbor1to1()
    {
        var dst = Images.NewNrgba(_bounds);
        Interpolators.NearestNeighbor.Scale(dst, _bounds, _src, _bounds, Op.Src, null);
    }
}
