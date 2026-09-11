using DotImage;
using DotImage.Color;
using TiffImage = global::DotImage.Extended.Tiff.Tiff;
using TiffWriter = DotImage.Extended.Tiff.TiffWriter;
using TiffCompressionType = DotImage.Extended.Tiff.CompressionType;
using Xunit;

namespace DotImage.Extended.Tests;

public class TiffRoundTripTests
{
    private static IImage FillNrgba(int w, int h)
    {
        NrgbaImage img = Images.NewNrgba(Geometry.Rect(0, 0, w, h));
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                img.Set(x, y, new Rgba((byte)(x * 255 / w), (byte)(y * 255 / h), (byte)((x + y) % 256), 255));
            }
        }
        return img;
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(16, 9)]
    [InlineData(100, 60)]
    public void EncodeDecodeUncompressed(int w, int h)
    {
        IImage src = FillNrgba(w, h);
        using var ms = new MemoryStream();
        TiffWriter.Encode(ms, src, null);
        ms.Position = 0;

        IImage dst = TiffImage.Decode(ms);
        Assert.Equal(w, dst.Bounds().Dx());
        Assert.Equal(h, dst.Bounds().Dy());
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                (uint r, uint g, uint b, uint a) = dst.At(x, y).Rgba();
                Assert.Equal(255u, a >> 8);
                byte er = (byte)(x * 255 / w);
                byte eg = (byte)(y * 255 / h);
                byte eb = (byte)((x + y) % 256);
                Assert.Equal(er, r >> 8);
                Assert.Equal(eg, g >> 8);
                Assert.Equal(eb, b >> 8);
            }
        }
    }

[Fact]
    public void EncodeLzwIsUnsupported()
    {
        IImage src = FillNrgba(40, 30);
        var opt = new TiffWriter.Options
        {
            Compression = TiffCompressionType.Lzw,
            Predictor = true,
        };
        using var ms = new MemoryStream();
        // The Go writer only supports Uncompressed and Deflate; LZW throws.
        Assert.Throws<InvalidOperationException>(() => TiffWriter.Encode(ms, src, opt));
    }

    [Fact]
    public void EncodeDecodeDeflate()
    {
        IImage src = FillNrgba(25, 20);
        var opt = new TiffWriter.Options { Compression = TiffCompressionType.Deflate };
        using var ms = new MemoryStream();
        TiffWriter.Encode(ms, src, opt);
        ms.Position = 0;

        IImage dst = TiffImage.Decode(ms);
        AssertPixelEqual(src, dst);
    }

    [Fact]
    public void EncodeDecodeCmykIsNotSupported()
    {
        IImage src = Images.NewCmyk(Geometry.Rect(0, 0, 4, 4));
        using var ms = new MemoryStream();
        // CMYK falls through to the generic Encode path (RGBA strip).
        TiffWriter.Encode(ms, src, null);
        ms.Position = 0;
        IImage dst = TiffImage.Decode(ms);
        Assert.Equal(4, dst.Bounds().Dx());
        Assert.Equal(4, dst.Bounds().Dy());
    }

    private static void AssertPixelEqual(IImage a, IImage b)
    {
        var ra = a.Bounds();
        var rb = b.Bounds();
        Assert.Equal(ra, rb);
        for (int y = ra.Min.Y; y < ra.Max.Y; y++)
        {
            for (int x = ra.Min.X; x < ra.Max.X; x++)
            {
                (uint r1, uint g1, uint b1, uint a1) = a.At(x, y).Rgba();
                (uint r2, uint g2, uint b2, uint a2) = b.At(x, y).Rgba();
                Assert.True(r1 == r2 && g1 == g2 && b1 == b2 && a1 == a2,
                    $"pixel ({x},{y}): got ({r1},{g1},{b1},{a1}) want ({r2},{g2},{b2},{a2})");
            }
        }
    }
}