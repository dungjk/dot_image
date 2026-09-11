using DotImage;
using DotImage.Color;
using DotImage.Draw;
using DotImage.Gif;
using DotImage.Jpeg;
using DotImage.Png;

static class DotImageSamples
{
    public static void RunAll()
    {
        Sample1_RgbaGradient();
        Sample2_DrawComposite();
        Sample3_GifAnimation();
        Sample4_JpegEncode();
        Sample5_YCbCrImage();
    }

    static void Sample1_RgbaGradient()
    {
        var m = Images.NewRgba(Geometry.Rect(0, 0, 4, 4));
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                m.SetRgba(x, y, new Rgba((byte)(x * 60), (byte)(y * 60), 128, 255));
        using var ms = new MemoryStream();
        PngWriter.Encode(ms, m);
        Console.WriteLine("Sample1: RGBA + PNG ok");
    }

    static void Sample2_DrawComposite()
    {
        var dst = Images.NewRgba(Geometry.Rect(0, 0, 8, 8));
        var src = UniformImages.NewUniform(new Rgba(255, 0, 0, 255));
        DrawOps.Draw(dst, dst.Bounds(), src, new Point(0, 0), Op.Over);
        Console.WriteLine("Sample2: DrawComposite ok");
    }

    static void Sample3_GifAnimation()
    {
        var pal = new Palette([new Rgba(255, 0, 0, 255), new Rgba(0, 255, 0, 255)]);
        var pm = Images.NewPaletted(Geometry.Rect(0, 0, 2, 2), pal);
        pm.Set(0, 0, pal[0]);
        pm.Set(1, 1, pal[1]);
        using var ms = new MemoryStream();
        GifWriter.Encode(ms, pm);
        Console.WriteLine("Sample3: GIF ok");
    }

    static void Sample4_JpegEncode()
    {
        var m = Images.NewRgba(Geometry.Rect(0, 0, 2, 2));
        m.SetRgba(0, 0, new Rgba(0, 0, 255, 255));
        using var ms = new MemoryStream();
        JpegWriter.Encode(ms, m);
        Console.WriteLine("Sample4: JPEG ok");
    }

    static void Sample5_YCbCrImage()
    {
        var ycbcr = YCbCrImages.NewYCbCr(Geometry.Rect(0, 0, 4, 4), YCbCrSubsampleRatio.Ratio444);
        ycbcr.Y.Span[0] = 128;
        Console.WriteLine("Sample5: YCbCr ok");
    }
}