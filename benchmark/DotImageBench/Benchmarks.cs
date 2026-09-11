using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotImage;
using DotImage.Jpeg;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using ImageSharpImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class JpegDecodeBench
{
    private byte[] _jpeg = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup() => _jpeg = Bench.LoadJpeg();

    [Benchmark]
    public IImage DotImage()
    {
        using var ms = new MemoryStream(_jpeg);
        return JpegReader.Decode(ms);
    }

    [Benchmark]
    public ImageSharpImage SixLabors() => Image.Load<Rgba32>(_jpeg);

    [Benchmark]
    public SKBitmap SkiaSharp()
    {
        using var data = SKData.CreateCopy(_jpeg);
        return SKBitmap.Decode(data);
    }
}

[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class ResizeBench
{
    private NrgbaImage _dot = null!;
    private ImageSharpImage _sharp = null!;
    private SKBitmap _skia = null!;

    [GlobalSetup]
    public void Setup()
    {
        using var input = new MemoryStream(Bench.LoadJpeg());
        _dot = Bench.ToNrgba(JpegReader.Decode(input));
        _sharp = Image.Load<Rgba32>(Bench.LoadJpeg());
        using var data = SKData.CreateCopy(Bench.LoadJpeg());
        _skia = SKBitmap.Decode(data);
    }

    [Benchmark]
    public NrgbaImage DotImage() => Bench.BicubicScale(_dot, Bench.TargetWidth, Bench.TargetHeight);

    [Benchmark]
    public ImageSharpImage SixLabors() => _sharp.Clone(x => x.Resize(new ResizeOptions
    {
        Size = new SixLabors.ImageSharp.Size(Bench.TargetWidth, Bench.TargetHeight),
        Mode = ResizeMode.Stretch,
        Sampler = KnownResamplers.Bicubic,
    }));

    [Benchmark]
    public SKBitmap SkiaSharp() => Bench.ResizeSkiaSharp(_skia, Bench.TargetWidth, Bench.TargetHeight);
}

[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class BrightnessBench
{
    private NrgbaImage _dot = null!;
    private ImageSharpImage _sharp = null!;
    private SKBitmap _skia = null!;

    [GlobalSetup]
    public void Setup()
    {
        using var input = new MemoryStream(Bench.LoadJpeg());
        _dot = Bench.ToNrgba(JpegReader.Decode(input));
        _sharp = Image.Load<Rgba32>(Bench.LoadJpeg());
        using var data = SKData.CreateCopy(Bench.LoadJpeg());
        _skia = SKBitmap.Decode(data);
    }

    [Benchmark]
    public void DotImage() => Bench.BrightnessDotImage(_dot);

    [Benchmark]
    public void SixLabors() => Bench.BrightnessImageSharp(_sharp);

    [Benchmark]
    public void SkiaSharp() => Bench.BrightnessSkiaSharp(_skia);
}