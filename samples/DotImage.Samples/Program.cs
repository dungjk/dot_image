using DotImage;
using DotImage.Color;
using DotImage.Draw;
using DotImage.Gif;
using DotImage.Jpeg;
using DotImage.Png;
using DotImage.Extended.Bmp;
using DotImage.Extended.Tiff;
using DotImage.Extended.Webp;
using DotImage.Extended.Draw;
using DotImage.Extended.Math.F64;
using DotImage.Extended.Font.OpenType;
using DotImage.Extended.Math.Fixed;
using DotImage.Extended.Colornames;
using DotImage.Extended.Font;
using DotImage.Extended.Font.Basic;
using DotImage.Extended.Vector;

class Program
{
    static void Main()
    {
        Sample1_RgbaGradient();
        Sample2_DrawComposite();
        Sample3_GifAnimation();
        Sample4_JpegEncode();
        Sample5_YCbCrImage();
        Sample6_BmpEncode();
        Sample7_TiffEncode();
        Sample8_WebpDecode();
        Sample9_ScaleUp();
        Sample10_Transform();
        Sample11_ColorNames();
        Sample12_FixedPoint();
        Sample13_VectorRaster();
        Sample14_BasicFont();
        Sample15_OpenTypeFont();
        Console.WriteLine("All 15 samples completed.");
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

    static void Sample6_BmpEncode()
    {
        var src = Images.NewNrgba(Geometry.Rect(0, 0, 3, 3));
        src.SetNrgba(0, 0, new Nrgba(200, 100, 50, 255));
        using var ms = new MemoryStream();
        Bmp.Encode(ms, src);
        ms.Position = 0;
        IImage dst = Bmp.Decode(ms);
        Console.WriteLine("Sample6: BMP ok, dims=" + dst.Bounds().Dx());
    }

    static void Sample7_TiffEncode()
    {
        var src = Images.NewRgba(Geometry.Rect(0, 0, 4, 4));
        src.SetRgba(0, 0, new Rgba(10, 20, 30, 255));
        using var ms = new MemoryStream();
        TiffWriter.Encode(ms, src, null);
        ms.Position = 0;
        IImage dst = Tiff.Decode(ms);
        Console.WriteLine("Sample7: TIFF ok");
    }

    static void Sample8_WebpDecode()
    {
        var data = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 26, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P',
            (byte)'V', (byte)'P', (byte)'8', (byte)'X', 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            (byte)'V', (byte)'P', (byte)'8', (byte)' ', 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            (byte)'V', (byte)'P', (byte)'8', (byte)' ', 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        };
        try
        {
            using var ms = new MemoryStream(data);
            Decoder.DecodeConfig(ms);
            Console.WriteLine("Sample8: WebP decode config ok");
        }
        catch
        {
            Console.WriteLine("Sample8: WebP decode config attempted");
        }
    }

    static void Sample9_ScaleUp()
    {
        var src = Images.NewRgba(Geometry.Rect(0, 0, 2, 2));
        src.SetRgba(0, 0, new Rgba(255, 0, 0, 255));
        var dst = Images.NewRgba(Geometry.Rect(0, 0, 4, 4));
        Interpolators.NearestNeighbor.Scale(dst, dst.Bounds(), src, src.Bounds(), Op.Src, null);
        Console.WriteLine("Sample9: Scale ok");
    }

    static void Sample10_Transform()
    {
        var src = Images.NewRgba(Geometry.Rect(0, 0, 2, 2));
        src.SetRgba(0, 0, new Rgba(0, 255, 0, 255));
        var dst = Images.NewRgba(Geometry.Rect(0, 0, 4, 4));
        var m = new Aff3(1, 0, 0, 0, 1, 0);
        Interpolators.NearestNeighbor.Transform(dst, m, src, src.Bounds(), Op.Src, null);
        Console.WriteLine("Sample10: Transform ok");
    }

    static void Sample11_ColorNames()
    {
        var r = ColorNames.Map["red"];
        Console.WriteLine("Sample11: ColorNames red=" + r.R);
    }

    static void Sample12_FixedPoint()
    {
        var a = FixedPoint.I(5);
        var b = FixedPoint.I(2);
        var c = a.Mul(b);
        Console.WriteLine("Sample12: FixedPoint 5*2 raw=" + c.Raw);
    }

    static void Sample13_VectorRaster()
    {
        var z = Rasterizer.NewRasterizer(16, 16);
        z.MoveTo(2, 2);
        z.LineTo(8, 2);
        z.LineTo(8, 8);
        z.ClosePath();
        var dst = Images.NewAlpha(Geometry.Rect(0, 0, 16, 16));
        z.Draw(dst, dst.Bounds(), UniformImages.Opaque, new Point(0, 0));
        Console.WriteLine("Sample13: Vector ok");
    }

    static void Sample14_BasicFont()
    {
        var face = DotImage.Extended.Font.Basic.Face7x13.Instance;
        var gm = face.GetGlyph(new Point26_6(Int26_6.FromInt(0), Int26_6.FromInt(11 << 6)), new System.Text.Rune('A'));
        Console.WriteLine("Sample14: BasicFont found=" + gm.Found);
    }

    static void Sample15_OpenTypeFont()
    {
        var f = DotImage.Extended.Font.Sfnt.Font.Parse(DotImage.Extended.Font.GoFont.GoFont.Goregular.ToArray());
        var face = OpenTypeFont.NewFace(f, null);
        var adv = face.GetGlyphAdvance(new System.Text.Rune('A'));
        Console.WriteLine("Sample15: OpenType font advance=" + adv.Floor());
    }
}
