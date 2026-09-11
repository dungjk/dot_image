using DotImage;
using DotImage.Color;
using BmpImage = global::DotImage.Extended.Bmp.Bmp;
using Xunit;

namespace DotImage.Extended.Tests;

public class BmpRoundTripTests
{
    [Fact]
    public void NrgbaOpaqueRoundTrip()
    {
        var src = Images.NewNrgba(Geometry.Rect(0, 0, 7, 5));
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                src.SetNrgba(x, y, new Nrgba((byte)(x * 3), (byte)(y * 5), (byte)(x + y), 0xff));
            }
        }

        using var ms = new MemoryStream();
        BmpImage.Encode(ms, src);
        ms.Position = 0;

        IImage dst = BmpImage.Decode(ms);
        Assert.Equal(7, dst.Bounds().Dx());
        Assert.Equal(5, dst.Bounds().Dy());

        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                var (r, g, b, a) = dst.At(x, y).Rgba();
                Assert.Equal((uint)(x * 3 * 257), r);
                Assert.Equal((uint)(y * 5 * 257), g);
                Assert.Equal((uint)((x + y) * 257), b);
                Assert.Equal(0xffffu, a);
            }
        }
    }

    [Fact]
    public void GrayRoundTrip()
    {
        var src = Images.NewGray(Geometry.Rect(0, 0, 3, 7));
        for (int y = 0; y < 7; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                src.Set(x, y, new Gray((byte)(y * 16 + x * 4)));
            }
        }

        using var ms = new MemoryStream();
        BmpImage.Encode(ms, src);
        ms.Position = 0;

        IImage dst = BmpImage.Decode(ms);
        Assert.Equal(3, dst.Bounds().Dx());
        Assert.Equal(7, dst.Bounds().Dy());

        for (int y = 0; y < 7; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                var (r, g, b, a) = dst.At(x, y).Rgba();
                byte want = (byte)(y * 16 + x * 4);
                Assert.Equal((uint)(want * 257), r);
                Assert.Equal((uint)(want * 257), g);
                Assert.Equal((uint)(want * 257), b);
                Assert.Equal(0xffffu, a);
            }
        }
    }

    [Fact]
    public void DecodeConfigMatchesDecodedImage()
    {
        var src = Images.NewRgba(Geometry.Rect(0, 0, 11, 4));
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 11; x++)
            {
                src.SetRgba(x, y, new Rgba(0x10, 0x20, 0x30, 0xff));
            }
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            BmpImage.Encode(ms, src);
            bytes = ms.ToArray();
        }

        using var configMs = new MemoryStream(bytes);
        Config config = BmpImage.DecodeConfig(configMs);
        Assert.Equal(11, config.Width);
        Assert.Equal(4, config.Height);

        using var decodeMs = new MemoryStream(bytes);
        IImage decoded = BmpImage.Decode(decodeMs);
        Assert.Equal(11, decoded.Bounds().Dx());
        Assert.Equal(4, decoded.Bounds().Dy());
    }
}