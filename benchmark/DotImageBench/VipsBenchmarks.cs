using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class VipsBenchmarks
{
    private NetVips.Image _vips = null!;

    [GlobalSetup]
    public void Setup() =>
        _vips = NetVips.Image.NewFromFile(Bench.JpegPath, access: NetVips.Enums.Access.Sequential);

    [Benchmark]
    public NetVips.Image Decode() => _vips.CopyMemory();

    [Benchmark]
    public NetVips.Image Resize() =>
        _vips.Resize(Bench.TargetWidth / (double)_vips.Width, kernel: NetVips.Enums.Kernel.Cubic).CopyMemory();

    [Benchmark]
    public NetVips.Image Brightness() =>
        _vips.Linear(
            new double[] { 1, 1, 1 },
            new double[] { Bench.BrightnessDelta, Bench.BrightnessDelta, Bench.BrightnessDelta }).CopyMemory();
}