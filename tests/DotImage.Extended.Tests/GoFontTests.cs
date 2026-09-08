using System.IO;
using DotImage.Extended.Font;
using DotImage.Extended.Font.GoFont;
using DotImage.Extended.Font.Sfnt;
using DotImage.Extended.Math.Fixed;
using SfntFont = DotImage.Extended.Font.Sfnt.Font;
using SfntBuffer = DotImage.Extended.Font.Sfnt.Buffer;
using Xunit;

namespace DotImage.Extended.Tests;

// Ported from golang.org/x/image/font/gofont. Verifies that the embedded
// byte arrays match the raw TTF files on disk and parse correctly.
public class GoFontTests
{
    private static readonly string TestDataDir = FindTestDataDir();
    private const int UnitsPerEm = 2048;

    private static string FindTestDataDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "TestData")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine(dir ?? throw new InvalidOperationException("TestData directory not found"), "TestData");
    }

    private static byte[] LoadFont(string rel)
    {
        string path = Path.Combine(TestDataDir, rel);
        return File.ReadAllBytes(path);
    }

    private static readonly (string Name, string TtfPath)[] FontData =
    [
        ("goregular",           "gofont/Go-Regular.ttf"),
        ("goitalic",            "gofont/Go-Italic.ttf"),
        ("gobold",              "gofont/Go-Bold.ttf"),
        ("gobolditalic",        "gofont/Go-Bold-Italic.ttf"),
        ("gomedium",            "gofont/Go-Medium.ttf"),
        ("gomediumitalic",      "gofont/Go-Medium-Italic.ttf"),
        ("gomono",              "gofont/Go-Mono.ttf"),
        ("gomonoitalic",        "gofont/Go-Mono-Italic.ttf"),
        ("gomonobold",          "gofont/Go-Mono-Bold.ttf"),
        ("gomonobolditalic",    "gofont/Go-Mono-Bold-Italic.ttf"),
        ("gosmallcaps",         "gofont/Go-Smallcaps.ttf"),
        ("gosmallcapsitalic",   "gofont/Go-Smallcaps-Italic.ttf"),
    ];

    [Fact]
    public void AllFontsByteFidelity()
    {
        foreach (var (name, ttfPath) in FontData)
        {
            byte[] onDisk = LoadFont(ttfPath);
            ReadOnlySpan<byte> embedded = GoFont.TTF(name);
            Assert.True(
                embedded.SequenceEqual(onDisk),
                $"{name}: embedded {embedded.Length} bytes != on-disk {onDisk.Length} bytes");
        }
    }

    [Theory]
    [InlineData("goregular")]
    [InlineData("goitalic")]
    [InlineData("gobold")]
    [InlineData("gobolditalic")]
    [InlineData("gomedium")]
    [InlineData("gomediumitalic")]
    [InlineData("gomono")]
    [InlineData("gomonoitalic")]
    [InlineData("gomonobold")]
    [InlineData("gomonobolditalic")]
    [InlineData("gosmallcaps")]
    [InlineData("gosmallcapsitalic")]
    public void ParseSucceeds(string name)
    {
        ReadOnlySpan<byte> ttf = GoFont.TTF(name);
        var f = SfntFont.Parse(ttf.ToArray());
        var buf = new SfntBuffer();
        var metrics = f.Metrics(buf, new Int26_6(UnitsPerEm), Hinting.None);
        Assert.True(metrics.Height.Floor() > 0, $"{name}: height must be positive");
        Assert.True(metrics.Ascent.Floor() > 0, $"{name}: ascent must be positive");
        Assert.True(metrics.Descent.Floor() > 0, $"{name}: descent must be positive");
    }

    [Theory]
    [InlineData("goregular")]
    [InlineData("gobold")]
    [InlineData("gomono")]
    public void GlyphAdvanceNonZero(string name)
    {
        ReadOnlySpan<byte> ttf = GoFont.TTF(name);
        var f = SfntFont.Parse(ttf.ToArray());
        var buf = new SfntBuffer();
        var gi = f.GlyphIndex(buf, 'A');
        Assert.NotEqual(default(GlyphIndex), gi);
        var adv = f.GlyphAdvance(buf, gi, new Int26_6(UnitsPerEm), Hinting.None);
        Assert.True(adv.Round() > 0, $"{name}: 'A' advance must be positive");
    }
}
