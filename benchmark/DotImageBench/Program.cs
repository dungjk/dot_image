using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

class Program
{
    static void Main(string[] args)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        var runInfos = new List<BenchmarkRunInfo>
        {
            BenchmarkConverter.TypeToBenchmarks(typeof(JpegDecodeBench), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(PngDecodeBench), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(ResizeBench), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(BrightnessBench), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(DrawScaleBench), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(DrawScaleUpBench), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(DrawTransformBench), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(DrawCopyBench), config),
        };

        if (Bench.VipsAvailable)
        {
            runInfos.Add(BenchmarkConverter.TypeToBenchmarks(typeof(VipsBenchmarks), config));
        }
        else
        {
            Console.WriteLine("libvips not found; NetVips benchmarks excluded.");
        }

        BenchmarkRunner.Run(runInfos.ToArray());
    }
}