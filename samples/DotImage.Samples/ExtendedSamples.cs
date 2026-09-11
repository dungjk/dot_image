using DotImage;
using DotImage.Color;
using DotImage.Draw;
using DotImage.Png;
using DotImage.Extended.Bmp;
using DotImage.Extended.Tiff;
using DotImage.Extended.Webp;
using DotImage.Extended.Draw;
using DotImage.Extended.Math.Fixed;
using DotImage.Extended.Font.Sfnt;
using DotImage.Extended.Font;
using DotImage.Extended.Font.Basic;
using DotImage.Extended.Font.OpenType;
using DotImage.Extended.Vector;

class ExtendedSamples
{
    public static void RunAll()
    {
        BmpEncodeDecode();
        TiffEncodeDecode();
        WebpDecodeConfig();
        ScaleUpNearest();
        VectorAndFontScene();
    }

    static void BmpEncodeDecode()
    {
        var src = Images.NewNrgba(Geometry.Rect(0, 0, 4, 4));
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                src.SetNrgba(x, y, new Nrgba((byte)(x * 60), (byte)(y * 60), 128, 255));
        using var ms = new MemoryStream();
        Bmp.Encode(ms, src);
        ms.Position = 0;
        IImage dst = Bmp.Decode(ms);
        Console.WriteLine("ExtendedSample1: BMP encode+decode ok, dims=" + dst.Bounds().Dx() + "x" + dst.Bounds().Dy());
    }

    static void TiffEncodeDecode()
    {
        var src = Images.NewRgba(Geometry.Rect(0, 0, 4, 4));
        src.SetRgba(0, 0, new Rgba(10, 20, 30, 255));
        using var ms = new MemoryStream();
        TiffWriter.Encode(ms, src, null);
        ms.Position = 0;
        IImage dst = Tiff.Decode(ms);
        Console.WriteLine("ExtendedSample2: TIFF encode+decode ok, model=" + dst.ColorModel());
    }

    static void WebpDecodeConfig()
    {
        var data = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 26, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P',
            (byte)'V', (byte)'P', (byte)'8', (byte)'X', 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            (byte)'V', (byte)'P', (byte)'8', (byte)' ', 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            (byte)'V', (byte)'P', (byte)'8', (byte)' ', 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        };
        using var ms = new MemoryStream(data);
        Config c = Decoder.DecodeConfig(ms);
        Console.WriteLine("ExtendedSample3: WebP config ok, dims=" + c.Width + "x" + c.Height);
    }

    static void ScaleUpNearest()
    {
        var src = Images.NewRgba(Geometry.Rect(0, 0, 2, 2));
        src.SetRgba(0, 0, new Rgba(255, 0, 0, 255));
        var dst = Images.NewRgba(Geometry.Rect(0, 0, 8, 8));
        Interpolators.NearestNeighbor.Scale(dst, dst.Bounds(), src, src.Bounds(), Op.Src, null);
        Console.WriteLine("ExtendedSample4: Scale ok, dst=" + dst.Bounds().Dx() + "x" + dst.Bounds().Dy());
    }

    static void VectorAndFontScene()
    {
        const int size = 600;
        var canvas = Images.NewNrgba(Geometry.Rect(0, 0, size, size));
        DrawOps.Draw(canvas, canvas.Bounds(), UniformImages.NewUniform(new Nrgba(242, 242, 242, 255)), new Point(0, 0), Op.Src);

        var triGrad = MakeDiagonalGradient(size, size, new Nrgba(220, 60, 40, 255), new Nrgba(255, 165, 0, 255));
        var quadGrad = MakeDiagonalGradient(size, size, new Nrgba(40, 120, 220, 255), new Nrgba(150, 60, 200, 255));
        var blobGrad = MakeDiagonalGradient(size, size, new Nrgba(255, 200, 0, 255), new Nrgba(240, 80, 160, 255));
        var moonGrad = MakeDiagonalGradient(size, size, new Nrgba(60, 170, 80, 255), new Nrgba(0, 180, 160, 255));

        var tri = Rasterizer.NewRasterizer(size, size);
        tri.MoveTo(60, 520);
        tri.LineTo(180, 340);
        tri.LineTo(300, 520);
        tri.ClosePath();
        tri.Draw(canvas, canvas.Bounds(), triGrad, new Point(0, 0));

        var quad = Rasterizer.NewRasterizer(size, size);
        quad.MoveTo(330, 520);
        quad.LineTo(560, 500);
        quad.LineTo(520, 340);
        quad.LineTo(360, 360);
        quad.ClosePath();
        quad.Draw(canvas, canvas.Bounds(), quadGrad, new Point(0, 0));

        var blob = Rasterizer.NewRasterizer(size, size);
        blob.MoveTo(350, 230);
        blob.QuadTo(450, 230, 490, 310);
        blob.QuadTo(505, 380, 430, 430);
        blob.CubeTo(380, 465, 300, 420, 290, 350);
        blob.CubeTo(282, 300, 300, 255, 350, 230);
        blob.ClosePath();
        blob.Draw(canvas, canvas.Bounds(), blobGrad, new Point(0, 0));

        var crescent = Rasterizer.NewRasterizer(size, size);
        crescent.MoveTo(150, 260);
        crescent.QuadTo(140, 120, 260, 110);
        crescent.CubeTo(120, 200, 100, 300, 150, 260);
        crescent.ClosePath();
        crescent.Draw(canvas, canvas.Bounds(), moonGrad, new Point(0, 0));

        var basic = new Drawer
        {
            Dst = canvas,
            Src = UniformImages.NewUniform(new Nrgba(40, 40, 40, 255)),
            Face = Face7x13.Instance,
            Dot = new Point26_6(Int26_6.FromInt(20), Int26_6.FromInt(24)),
        };
        basic.DrawString("DotImage.Extended scene: basicfont 7x13");

        var ttf = DotImage.Extended.Font.Sfnt.Font.Parse(DotImage.Extended.Font.GoFont.GoFont.Goregular.ToArray());
        using var otf = OpenTypeFont.NewFace(ttf, new FaceOptions { Size = 28, DPI = 72, Hinting = Hinting.None });
        var open = new Drawer
        {
            Dst = canvas,
            Src = UniformImages.NewUniform(new Nrgba(20, 20, 140, 255)),
            Face = otf,
            Dot = new Point26_6(Int26_6.FromInt(20), Int26_6.FromInt(80)),
        };
        open.DrawString("Curves and gradient fills (OpenType / Go-Regular TTF)");

        using var file = File.Create("scene.png");
        PngWriter.Encode(file, canvas);
        Console.WriteLine("ExtendedSample5: vector+fonts scene with curves and gradients rendered to scene.png");
    }

    static NrgbaImage MakeDiagonalGradient(int w, int h, Nrgba a, Nrgba b)
    {
        var img = Images.NewNrgba(Geometry.Rect(0, 0, w, h));
        float denom = w + h;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float t = (x + y) / denom;
                var c = new Nrgba(
                    (byte)(a.R + (b.R - a.R) * t),
                    (byte)(a.G + (b.G - a.G) * t),
                    (byte)(a.B + (b.B - a.B) * t),
                    255);
                img.SetNrgba(x, y, c);
            }
        }
        return img;
    }
}