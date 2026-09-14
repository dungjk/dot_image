using DotImage;
using DotImage.Color;
using NetVips;
using SixLabors.ImageSharp.PixelFormats;
using ImageSharpImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

static class Bench
{
    public const int TargetWidth = 320;
    public const int TargetHeight = 213;
    public const int BrightnessDelta = 40;

    public static string JpegPath { get; private set; } = string.Empty;
    public static byte[] JpegBytes { get; private set; } = Array.Empty<byte>();
    public static string PngPath { get; private set; } = string.Empty;
    public static byte[] PngBytes { get; private set; } = Array.Empty<byte>();
    public static bool VipsAvailable { get; private set; }

    static Bench()
    {
        JpegPath = FindJpeg();
        JpegBytes = File.ReadAllBytes(JpegPath);
        PngPath = FindPng();
        PngBytes = File.ReadAllBytes(PngPath);
        try
        {
            NetVips.Image.NewFromFile(JpegPath, access: Enums.Access.Sequential).CopyMemory();
            VipsAvailable = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"warning: NetVips unavailable ({ex.GetType().Name}: {ex.Message})");
            VipsAvailable = false;
        }
    }

    public static byte[] LoadJpeg() => JpegBytes;

    public static byte[] LoadPng() => PngBytes;

    private static string FindJpeg()
    {
        const string name = "sample_640×426.jpeg";
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "images", name),
            Path.Combine(Environment.CurrentDirectory, "samples", "DotImage.Samples", "images", name),
            Path.Combine(Environment.CurrentDirectory, "images", name),
        ];
        foreach (string c in candidates)
        {
            if (File.Exists(c))
                return c;
        }
        throw new FileNotFoundException(name);
    }

    private static string FindPng()
    {
        const string name = "sample_640×426.png";
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "images", name),
            Path.Combine(Environment.CurrentDirectory, "samples", "DotImage.Samples", "images", name),
            Path.Combine(Environment.CurrentDirectory, "images", name),
        ];
        foreach (string c in candidates)
        {
            if (File.Exists(c))
                return c;
        }
        throw new FileNotFoundException(name);
    }

    public static NrgbaImage ToNrgba(IImage image)
    {
        var b = image.Bounds();
        var dst = Images.NewNrgba(b);
        for (int y = b.Min.Y; y < b.Max.Y; y++)
            for (int x = b.Min.X; x < b.Max.X; x++)
                dst.SetNrgba(x, y, (Nrgba)ColorModels.Nrgba.Convert(image.At(x, y)));
        return dst;
    }

    public static void FillPattern(NrgbaImage img)
    {
        var b = img.Bounds();
        for (int y = b.Min.Y; y < b.Max.Y; y++)
        {
            for (int x = b.Min.X; x < b.Max.X; x++)
            {
                img.SetNrgba(x, y, new Nrgba(
                    (byte)((x * 31 + y * 7) & 0xff),
                    (byte)((x * 13 + y * 29) & 0xff),
                    (byte)((x * 5 + y * 11) & 0xff),
                    0xff));
            }
        }
    }

    public static NrgbaImage BicubicScale(NrgbaImage src, int dw, int dh)
    {
        int sw = src.Bounds().Dx(), sh = src.Bounds().Dy();
        var dst = Images.NewNrgba(Geometry.Rect(0, 0, dw, dh));
        float xScale = (float)sw / dw;
        float yScale = (float)sh / dh;
        for (int dy = 0; dy < dh; dy++)
        {
            float sy = (dy + 0.5f) * yScale - 0.5f;
            int y0 = Math.Max(0, (int)MathF.Floor(sy) - 1);
            float[] wy = CubicWeights(sy - y0, 4);
            for (int dx = 0; dx < dw; dx++)
            {
                float sx = (dx + 0.5f) * xScale - 0.5f;
                int x0 = Math.Max(0, (int)MathF.Floor(sx) - 1);
                float[] wx = CubicWeights(sx - x0, 4);

                float r = 0, g = 0, b = 0, a = 0;
                for (int j = 0; j < 4; j++)
                {
                    int y = Math.Min(sh - 1, y0 + j);
                    for (int i = 0; i < 4; i++)
                    {
                        int x = Math.Min(sw - 1, x0 + i);
                        var c = (Nrgba)src.At(x, y);
                        float w = wx[i] * wy[j];
                        r += c.R * w;
                        g += c.G * w;
                        b += c.B * w;
                        a += c.A * w;
                    }
                }
                dst.SetNrgba(dx, dy, new Nrgba(ClampByte(r), ClampByte(g), ClampByte(b), ClampByte(a)));
            }
        }
        return dst;
    }

    private static float[] CubicWeights(float t, int samples)
    {
        var w = new float[samples];
        for (int i = 0; i < samples; i++)
            w[i] = Cubic(t - i);
        return w;
    }

    private static float Cubic(float x)
    {
        float ax = MathF.Abs(x);
        if (ax <= 1f)
            return (1.5f * ax - 2.5f) * ax * ax + 1f;
        if (ax < 2f)
        {
            float t = ax - 2f;
            return 0.5f * t * t * t;
        }
        return 0f;
    }

    public static byte ClampByte(float v) => (byte)Math.Clamp(MathF.Round(v), 0f, 255f);

    public static void BrightnessDotImage(NrgbaImage img)
    {
        img.ProcessRows((_, row) =>
        {
            for (int i = 0; i < row.Length; i += 4)
            {
                row[i] = ClampByte(row[i] + BrightnessDelta);
                row[i + 1] = ClampByte(row[i + 1] + BrightnessDelta);
                row[i + 2] = ClampByte(row[i + 2] + BrightnessDelta);
            }
        });
    }

    public static void BrightnessImageSharp(ImageSharpImage img)
    {
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    ref var p = ref row[x];
                    p.R = ClampByte(p.R + BrightnessDelta);
                    p.G = ClampByte(p.G + BrightnessDelta);
                    p.B = ClampByte(p.B + BrightnessDelta);
                }
            }
        });
    }

    public static void BrightnessSkiaSharp(SkiaSharp.SKBitmap bmp)
    {
        SkiaSharp.SKColor[] pixels = bmp.Pixels;
        for (int i = 0; i < pixels.Length; i++)
        {
            SkiaSharp.SKColor c = pixels[i];
            pixels[i] = new SkiaSharp.SKColor(
                ClampByte(c.Red + BrightnessDelta),
                ClampByte(c.Green + BrightnessDelta),
                ClampByte(c.Blue + BrightnessDelta),
                c.Alpha);
        }
        bmp.Pixels = pixels;
    }

    public static SkiaSharp.SKBitmap ResizeSkiaSharp(SkiaSharp.SKBitmap src, int dw, int dh)
    {
        using var srcImage = SkiaSharp.SKImage.FromBitmap(src);
        using var dst = new SkiaSharp.SKBitmap(dw, dh, src.ColorType, src.AlphaType);
        using var canvas = new SkiaSharp.SKCanvas(dst);
        canvas.DrawImage(srcImage, new SkiaSharp.SKRect(0, 0, dw, dh), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKCubicResampler.Mitchell));
        canvas.Flush();
        return dst;
    }
}