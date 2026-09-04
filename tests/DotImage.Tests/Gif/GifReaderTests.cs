// Ported from Go src/image/gif/reader_test.go


using DotImage.Compress;
using DotImage.Gif;

namespace DotImage.Tests.Gif;

public class GifReaderTests
{
    private const string HeaderStr =
        "GIF89a" +
        "\x02\x00\x01\x00" +
        "\x80\x00\x00";
    private const string PaletteStr = "\x10\x20\x30\x40\x50\x60";
    private const string TrailerStr = "\x3b";
    private const string Extra = "\x02\x02\x02\x02";

    private static byte[] LzwEncode(byte[] input)
    {
        using var b = new MemoryStream();
        using (var w = new LzwWriter(b, LzwOrder.Lsb, 2))
            w.Write(input);
        return b.ToArray();
    }

    public static TheoryData<int, int, int, Type?> DecodeCases => new()
    {
        { 0, 0, 0, typeof(GifNotEnoughException) },
        { 1, 0, 0, typeof(GifNotEnoughException) },
        { 2, 0, 0, null },
        { 2, 0, 1, null },
        { 2, 0, 2, typeof(GifTooMuchException) },
        { 3, 0, 0, typeof(GifTooMuchException) },
        { 2, 1, 0, null },
        { 2, 2, 0, null },
        { 2, 1, 1, typeof(GifTooMuchException) },
    };

    [Theory]
    [MemberData(nameof(DecodeCases))]
    public void Reader_Decode(int nPix, int extraExisting, int extraSeparate, Type? wantErr)
    {
        using var b = new MemoryStream();
        b.Write(System.Text.Encoding.Latin1.GetBytes(HeaderStr));
        b.Write(System.Text.Encoding.Latin1.GetBytes(PaletteStr));
        b.WriteByte(0x2c);
        b.Write([0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x02]);

        if (nPix > 0)
        {
            var enc = LzwEncode(new byte[nPix]);
            if (enc.Length + extraExisting > 0xff)
                return;
            b.WriteByte((byte)(enc.Length + extraExisting));
            b.Write(enc);
            b.Write(System.Text.Encoding.Latin1.GetBytes(Extra[..extraExisting]));
        }

        if (extraSeparate > 0)
        {
            b.WriteByte((byte)extraSeparate);
            b.Write(System.Text.Encoding.Latin1.GetBytes(Extra[..extraSeparate]));
        }

        b.WriteByte(0x00);
        b.Write(System.Text.Encoding.Latin1.GetBytes(TrailerStr));
        b.Position = 0;

        if (wantErr != null)
        {
            Assert.Throws(wantErr, () => GifReader.Decode(b));
            return;
        }

        var got = (PalettedImage)GifReader.Decode(b);
        var want = Images.NewPaletted(Geometry.Rect(0, 0, 2, 1), new Palette([
            new Rgba(0x10, 0x20, 0x30, 0xff),
            new Rgba(0x40, 0x50, 0x60, 0xff),
        ]));
        want.Pix = new byte[] { 0, 0 };
        AssertPalettedEqual(got, want);
    }

    [Fact]
    public void Reader_TransparentIndex()
    {
        using var b = new MemoryStream();
        b.Write(System.Text.Encoding.Latin1.GetBytes(HeaderStr));
        b.Write(System.Text.Encoding.Latin1.GetBytes(PaletteStr));
        for (int transparentIndex = 0; transparentIndex < 3; transparentIndex++)
        {
            if (transparentIndex < 2)
            {
                b.Write([0x21, 0xf9, 0x04, 0x01, 0x00, 0x00, (byte)transparentIndex, 0x00]);
            }
            b.Write([0x2c, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x02]);
            var enc = LzwEncode([0x00, 0x00]);
            b.WriteByte((byte)enc.Length);
            b.Write(enc);
            b.WriteByte(0x00);
        }
        b.Write(System.Text.Encoding.Latin1.GetBytes(TrailerStr));
        b.Position = 0;

        var g = GifReader.DecodeAll(b);
        var c0 = new Rgba(0x10, 0x20, 0x30, 0xff);
        var c1 = new Rgba(0x40, 0x50, 0x60, 0xff);
        var cz = new Rgba(0, 0, 0, 0);
        Palette[] wants =
        [
            new Palette([cz, c1]),
            new Palette([c0, cz]),
            new Palette([c0, c1]),
        ];
        Assert.Equal(wants.Length, g.Image.Count);
        for (int i = 0; i < wants.Length; i++)
            AssertPalettesEqual(wants[i], g.Image[i].Palette);
    }

    private static readonly byte[] TestGif =
    [
        (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a',
        1, 0, 1, 0,
        128, 0, 0,
        0, 0, 0, 1, 1, 1,
        0x21, 0xf9, 0x04, 0x00, 0x00, 0x00, 0xff, 0x00,
        0x2c,
        0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00,
        0x00,
        0x02, 0x02, 0x4c, 0x01, 0x00,
        0x3b,
    ];

    private static void Try(byte[] data, string want)
    {
        try
        {
            GifReader.DecodeAll(new MemoryStream(data));
            Assert.Equal("", want);
        }
        catch (Exception ex)
        {
            Assert.Equal(want, ex.Message);
        }
    }

    [Fact]
    public void Reader_Bounds()
    {
        var gif = (byte[])TestGif.Clone();
        gif[32] = 2;
        Try(gif, "gif: frame bounds larger than image bounds");

        gif[32] = 0;
        Try(gif, "gif: too much image data");
        gif[32] = 1;

        for (int i = 0; i < 4; i++)
            gif[32 + i] = 0xff;
        Try(gif, "gif: frame bounds larger than image bounds");
    }

    [Fact]
    public void Reader_NoPalette()
    {
        using var b = new MemoryStream();
        b.Write(System.Text.Encoding.Latin1.GetBytes(HeaderStr[..^3]));
        b.Write([0x00, 0x00, 0x00]);
        b.Write([0x2c, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x02]);
        var enc = LzwEncode([0x00, 0x03]);
        b.WriteByte((byte)enc.Length);
        b.Write(enc);
        b.WriteByte(0x00);
        b.Write(System.Text.Encoding.Latin1.GetBytes(TrailerStr));
        Try(b.ToArray(), "gif: no color table");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Reader_PixelOutsidePaletteRange(byte pval)
    {
        using var b = new MemoryStream();
        b.Write(System.Text.Encoding.Latin1.GetBytes(HeaderStr));
        b.Write(System.Text.Encoding.Latin1.GetBytes(PaletteStr));
        b.Write([0x2c, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x02]);
        var enc = LzwEncode([pval, pval]);
        b.WriteByte((byte)enc.Length);
        b.Write(enc);
        b.WriteByte(0x00);
        b.Write(System.Text.Encoding.Latin1.GetBytes(TrailerStr));
        string want = pval >= 2 ? "gif: invalid pixel value" : "";
        Try(b.ToArray(), want);
    }

    [Fact]
    public void Reader_TransparentPixelOutsidePaletteRange()
    {
        using var b = new MemoryStream();
        b.Write(System.Text.Encoding.Latin1.GetBytes(HeaderStr));
        b.Write(System.Text.Encoding.Latin1.GetBytes(PaletteStr));
        b.Write([0x21, 0xf9, 0x04, 0x01, 0x00, 0x00, 0x03, 0x00]);
        b.Write([0x2c, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x02]);
        var enc = LzwEncode([0x03, 0x03]);
        b.WriteByte((byte)enc.Length);
        b.Write(enc);
        b.WriteByte(0x00);
        b.Write(System.Text.Encoding.Latin1.GetBytes(TrailerStr));
        Try(b.ToArray(), "");
    }

    public static TheoryData<string, byte[], int> LoopCountCases => new()
    {
        {
            "loopcount-missing",
            Convert.FromHexString(
                "47494638396130303000303030" +
                "2c300000000a000a0080303030303030" +
                "0208f03175b9fd616c05003b"),
            -1
        },
        {
            "loopcount-0",
            Convert.FromHexString(
                "47494638396130303000303030" +
                "21ff0b4e45545343415045322e300301000000" +
                "2c300000000a000a0080303030303030" +
                "0208f03175b9fd616c0500" +
                "2c300000000a000a0080303030303030" +
                "0208f03175b9fd616c05003b"),
            0
        },
        {
            "loopcount-1",
            Convert.FromHexString(
                "47494638396130303000303030" +
                "21ff0b4e45545343415045322e300301010000" +
                "2c300000000a000a0080303030303030" +
                "0208f03175b9fd616c0500" +
                "2c300000000a000a0080303030303030" +
                "0208f03175b9fd616c05003b"),
            1
        },
    };

    [Theory]
    [MemberData(nameof(LoopCountCases))]
    public void Reader_LoopCount(string _, byte[] data, int loopCount)
    {
        var img = GifReader.DecodeAll(new MemoryStream(data));
        using var w = new MemoryStream();
        GifWriter.EncodeAll(w, img);
        w.Position = 0;
        var img1 = GifReader.DecodeAll(w);
        Assert.Equal(loopCount, img.LoopCount);
        Assert.Equal(img.LoopCount, img1.LoopCount);
    }

    [Fact]
    public void Reader_UnexpectedEof()
    {
        for (int i = TestGif.Length - 1; i >= 0; i--)
        {
            try
            {
                GifReader.DecodeAll(new MemoryStream(TestGif[..i]));
            }
            catch (GifNotEnoughException)
            {
                continue;
            }
            catch (Exception ex)
            {
                Assert.StartsWith("gif:", ex.Message);
                Assert.EndsWith(": unexpected EOF", ex.Message);
            }
        }
    }

    [Fact]
    public void Reader_ReencodeExtendedPalette()
    {
        byte[] data = Convert.FromHexString(
            "4749463839616c02020157220221ff0b280154ffffffff00000021474946306127dc213000ff84ff840000000000800021ffffffff8f4e4554530041508f8f0202020000000000000000000000000202020202020207020202022f31050000000000000021f904ab2c3826002c00000000c00001009800462b07fc1f02061202020602020202220202930202020202020202020202020286090222202222222222222222222222222222222222222222222222222220222222222222222222222222222222222222222222222222221a22222222332223222222222222222222222222222222222222224b222222222222002200002b474946312829021f0000000000cbff002f0202073121f904ab2c2c000021f92c3803002c00e0c0000000f932");
        var img = GifReader.Decode(new MemoryStream(data));
        GifWriter.Encode(Stream.Null, img, new GifOptions { NumColors = 1 });
    }

    internal static void AssertPalettedEqual(PalettedImage got, PalettedImage want)
    {
        Assert.Equal(want.Rect, got.Rect);
        Assert.Equal(want.Stride, got.Stride);
        Assert.Equal(want.Pix.ToArray(), got.Pix.ToArray());
        AssertPalettesEqual(want.Palette, got.Palette);
    }

    internal static void AssertPalettesEqual(Palette want, Palette got)
    {
        Assert.Equal(want.Length, got.Length);
        for (int i = 0; i < want.Length; i++)
        {
            var (wr, wg, wb, wa) = want[i].Rgba();
            var (gr, gg, gb, ga) = got[i].Rgba();
            Assert.Equal((wr, wg, wb, wa), (gr, gg, gb, ga));
        }
    }
}
