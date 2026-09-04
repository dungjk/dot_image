// Ported from Go src/image/png/writer_test.go


using System.Buffers.Binary;
using System.IO.Compression;
using DotImage.Png;

namespace DotImage.Tests.Png;

public class PngWriterTests
{
    private static string TestDataPath(string path) =>
        Path.Combine(AppContext.BaseDirectory, "testdata", path);

    private static string Diff(IImage m0, IImage m1)
    {
        var b0 = m0.Bounds();
        var b1 = m1.Bounds();
        if (b0.Dx() != b1.Dx() || b0.Dy() != b1.Dy())
            return $"dimensions differ: {b0} vs {b1}";
        int dx = b1.Min.X - b0.Min.X;
        int dy = b1.Min.Y - b0.Min.Y;
        for (int y = b0.Min.Y; y < b0.Max.Y; y++)
        {
            for (int x = b0.Min.X; x < b0.Max.X; x++)
            {
                var (r0, g0, b0c, a0) = m0.At(x, y).Rgba();
                var (r1, g1, b1c, a1) = m1.At(x + dx, y + dy).Rgba();
                if (r0 != r1 || g0 != g1 || b0c != b1c || a0 != a1)
                    return $"colors differ at ({x}, {y})";
            }
        }
        return "";
    }

    private static IImage EncodeDecode(IImage m)
    {
        using var b = new MemoryStream();
        PngWriter.Encode(b, m);
        b.Position = 0;
        return PngReader.Decode(b);
    }

    private static NrgbaImage ConvertToNrgba(IImage m)
    {
        var b = m.Bounds();
        var ret = Images.NewNrgba(b);
        for (int y = b.Min.Y; y < b.Max.Y; y++)
        {
            for (int x = b.Min.X; x < b.Max.X; x++)
            {
                var c = (Nrgba)Models.Nrgba.Convert(m.At(x, y));
                ret.SetNrgba(x, y, c);
            }
        }
        return ret;
    }

    [Theory]
    [MemberData(nameof(PngReaderTests.Filenames), MemberType = typeof(PngReaderTests))]
    public void Writer_RoundTrip(string fn)
    {
        var m0 = PngReaderTests.ReadPng($"pngsuite/{fn}.png");
        var m1 = PngReaderTests.ReadPng($"pngsuite/{fn}.png");
        var m2 = EncodeDecode(m1);
        var err = Diff(m0, m2);
        Assert.True(string.IsNullOrEmpty(err), $"{fn}: {err}");
    }

    [Theory]
    [InlineData(256, 8, (1 + 32) * 16)]
    [InlineData(128, 8, (1 + 32) * 16)]
    [InlineData(16, 4, (1 + 16) * 16)]
    [InlineData(4, 2, (1 + 8) * 16)]
    [InlineData(2, 1, (1 + 4) * 16)]
    public void Writer_PalettedBitDepth(int plen, byte wantBitdepth, int wantDataLen)
    {
        const int width = 32, height = 16;
        var palette = new IColor[plen];
        for (int i = 0; i < plen; i++)
            palette[i] = new Nrgba((byte)i, (byte)i, (byte)i, 255);
        var m0 = Images.NewPaletted(Geometry.Rect(0, 0, width, height), new Palette(palette));
        int idx = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                m0.SetColorIndex(x, y, (byte)(idx % plen));
                idx++;
            }
        }

        using var b = new MemoryStream();
        PngWriter.Encode(b, m0);
        var data = b.ToArray();
        const int chunkFieldsLength = 12;
        int i0 = 8; // skip PNG header
        while (i0 < data.Length - chunkFieldsLength)
        {
            uint length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i0, 4));
            var name = System.Text.Encoding.ASCII.GetString(data, i0 + 4, 4);
            switch (name)
            {
                case "IHDR":
                    Assert.Equal(wantBitdepth, data[i0 + 8 + 8]);
                    break;
                case "IDAT":
                    using (var ms = new MemoryStream())
                    using (var r = new ZLibStream(new MemoryStream(data, i0 + 8, (int)length), CompressionMode.Decompress))
                    {
                        r.CopyTo(ms);
                        Assert.Equal(wantDataLen, ms.Length);
                    }
                    break;
            }
            i0 += chunkFieldsLength + (int)length;
        }
    }

    [Fact]
    public void Writer_CompressionLevels()
    {
        var m = Images.NewNrgba(Geometry.Rect(0, 0, 100, 100));
        using var b1 = new MemoryStream();
        using var b2 = new MemoryStream();
        new PngEncoder().Encode(b1, m);
        new PngEncoder { CompressionLevel = PngCompressionLevel.NoCompression }.Encode(b2, m);
        Assert.True(b2.Length > b1.Length, "DefaultCompression encoding was larger than NoCompression encoding");
        b1.Position = 0;
        b2.Position = 0;
        PngReader.Decode(b1);
        PngReader.Decode(b2);
    }

    [Fact]
    public void Writer_SubImage()
    {
        var m0 = Images.NewRgba(Geometry.Rect(0, 0, 256, 256));
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
                m0.SetRgba(x, y, new Rgba((byte)x, (byte)y, 0, 255));
        }
        m0 = (RgbaImage)m0.SubImage(Geometry.Rect(50, 30, 250, 130));
        var m1 = EncodeDecode(m0);
        var err = Diff(m0, m1);
        Assert.True(string.IsNullOrEmpty(err), err);
    }

    [Theory]
    [InlineData("Transparent RGBA")]
    [InlineData("Opaque RGBA")]
    [InlineData("50/50 Transparent/Opaque RGBA")]
    [InlineData("RGBA with variable alpha")]
    public void Writer_RgbaImages(string name)
    {
        const int width = 640, height = 480;
        var transparentImg = Images.NewRgba(Geometry.Rect(0, 0, width, height));
        var opaqueImg = Images.NewRgba(Geometry.Rect(0, 0, width, height));
        var mixedImg = Images.NewRgba(Geometry.Rect(0, 0, width, height));
        var translucentImg = Images.NewRgba(Geometry.Rect(0, 0, width, height));
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var opaqueColor = new Rgba((byte)x, (byte)y, (byte)(y + x), 255);
                var translucentColor = new Rgba((byte)(x % 128), (byte)(y % 128), (byte)((y + x) % 128), 128);
                opaqueImg.SetRgba(x, y, opaqueColor);
                translucentImg.SetRgba(x, y, translucentColor);
                if (y % 2 == 0)
                    mixedImg.SetRgba(x, y, opaqueColor);
            }
        }

        IImage m0 = name switch
        {
            "Transparent RGBA" => transparentImg,
            "Opaque RGBA" => opaqueImg,
            "50/50 Transparent/Opaque RGBA" => mixedImg,
            "RGBA with variable alpha" => translucentImg,
            _ => throw new ArgumentException(),
        };
        var m1 = EncodeDecode(m0);
        var err = Diff(ConvertToNrgba(m0), m1);
        Assert.True(string.IsNullOrEmpty(err), err);
    }
}
