// Ported from Go src/image/decode_test.go


namespace DotImage.Tests;

public class DecodeTests
{
    private record ImageTest(string GoldenFilename, string Filename, int Tolerance);

    private static readonly ImageTest[] ImageTests =
    [
        new("video-001.png", "video-001.png", 0),
        // GIF images are restricted to a 256-color palette and the conversion
        // to GIF loses significant image quality.
        new("video-001.png", "video-001.gif", 64 << 8),
        new("video-001.png", "video-001.interlaced.gif", 64 << 8),
        new("video-001.png", "video-001.5bpp.gif", 128 << 8),
        // JPEG is a lossy format and hence needs a non-zero tolerance.
        new("video-001.png", "video-001.jpeg", 8 << 8),
        new("video-001.png", "video-001.progressive.jpeg", 8 << 8),
        new("video-001.221212.png", "video-001.221212.jpeg", 8 << 8),
        new("video-001.cmyk.png", "video-001.cmyk.jpeg", 8 << 8),
        new("video-001.rgb.png", "video-001.rgb.jpeg", 8 << 8),
        new("video-001.progressive.truncated.png", "video-001.progressive.truncated.jpeg", 8 << 8),
        // Grayscale images.
        new("video-005.gray.png", "video-005.gray.jpeg", 8 << 8),
        new("video-005.gray.png", "video-005.gray.png", 0),
    ];

    private static string TestDataPath(string path) =>
        Path.Combine(AppContext.BaseDirectory, "testdata", path);

    private static (IImage Image, string FormatName) Decode(string filename)
    {
        using var f = File.OpenRead(TestDataPath(filename));
        return FormatRegistry.Decode(f);
    }

    private static (Config Config, string FormatName) DecodeConfig(string filename)
    {
        using var f = File.OpenRead(TestDataPath(filename));
        return FormatRegistry.DecodeConfig(f);
    }

    private static int Delta(uint u0, uint u1)
    {
        int d = (int)u0 - (int)u1;
        return d < 0 ? -d : d;
    }

    private static bool WithinTolerance(IColor c0, IColor c1, int tolerance)
    {
        var (r0, g0, b0, a0) = c0.Rgba();
        var (r1, g1, b1, a1) = c1.Rgba();
        return Delta(r0, r1) <= tolerance &&
               Delta(g0, g1) <= tolerance &&
               Delta(b0, b1) <= tolerance &&
               Delta(a0, a1) <= tolerance;
    }

    private static string RgbaString(IColor c)
    {
        var (r, g, b, a) = c.Rgba();
        return $"rgba = 0x{r:x4}, 0x{g:x4}, 0x{b:x4}, 0x{a:x4} for {c.GetType()}{c}";
    }

    [Fact]
    public void Decode_CrossFormat()
    {
        var golden = new Dictionary<string, IImage>();

        foreach (var it in ImageTests)
        {
            if (!golden.TryGetValue(it.GoldenFilename, out var g))
            {
                try
                {
                    (g, _) = Decode(it.GoldenFilename);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"{it.GoldenFilename}: {ex.Message}");
                    continue;
                }
                golden[it.GoldenFilename] = g;
            }

            IImage m;
            string imageFormat;
            try
            {
                (m, imageFormat) = Decode(it.Filename);
            }
            catch (Exception ex)
            {
                Assert.Fail($"{it.Filename}: {ex.Message}");
                continue;
            }

            var b = g.Bounds();
            Assert.True(b.Eq(m.Bounds()),
                $"{it.Filename}: got bounds {m.Bounds()} want {b}");

            for (int y = b.Min.Y; y < b.Max.Y; y++)
            {
                for (int x = b.Min.X; x < b.Max.X; x++)
                {
                    if (!WithinTolerance(g.At(x, y), m.At(x, y), it.Tolerance))
                    {
                        Assert.Fail($"{it.Filename}: at ({x}, {y}):\n" +
                                    $"got  {RgbaString(m.At(x, y))}\n" +
                                    $"want {RgbaString(g.At(x, y))}");
                        goto nextCase;
                    }
                }
            }

            if (imageFormat == "gif")
            {
                // Each frame of a GIF can have a frame-local palette override the
                // GIF-global palette. Thus, Decode can yield a different ColorModel
                // than DecodeConfig.
                continue;
            }

            Config c;
            try
            {
                (c, _) = DecodeConfig(it.Filename);
            }
            catch (Exception ex)
            {
                Assert.Fail($"{it.Filename}: {ex.Message}");
                continue;
            }

            Assert.True(ReferenceEquals(m.ColorModel(), c.ColorModel),
                $"{it.Filename}: color models differ");

            nextCase: ;
        }
    }
}
