using System;
using System.IO;
using System.Text;
using DotImage;
using DotImage.Draw;
using DotImage.Extended.Font;
using DotImage.Extended.Font.Plan9;
using DotImage.Extended.Math.Fixed;
using Xunit;

namespace DotImage.Extended.Tests;

public class Plan9FontTests
{
    private static readonly string FixedDir = FindTestDataDir("testdata/fixed");

    private static string FindTestDataDir(string subDir)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "TestData")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return Path.Combine(dir ?? throw new InvalidOperationException("TestData directory not found"), "TestData", subDir);
    }

    private static byte[] ReadFile(string name) => File.ReadAllBytes(Path.Combine(FixedDir, name));

    [Fact]
    public void Metrics()
    {
        byte[] readFile(string name) => File.ReadAllBytes(Path.Combine(FixedDir, name));

        var data = readFile("unicode.7x13.font");
        var face = Plan9Font.ParseFont(data, readFile);

        var want = new FontMetrics(
            Int26_6.FromRaw(832),
            Int26_6.FromRaw(704),
            Int26_6.FromRaw(128),
            Int26_6.FromRaw(704),
            Int26_6.FromRaw(704),
            new Point(0, 1));
        Assert.Equal(want, face.Metrics);

        var subData = readFile("7x13.0000");
        var subFace = Plan9Font.ParseSubfont(subData, 0);
        Assert.Equal(want, subFace.Metrics);
    }

    [Fact]
    public void ExampleParseFont()
    {
        byte[] readFile(string name) => File.ReadAllBytes(Path.Combine(FixedDir, name));
        var fontData = readFile("unicode.7x13.font");
        var face = Plan9Font.ParseFont(fontData, readFile);
        int ascent = face.Metrics.Ascent.Ceil();

        var dst = Images.NewRgba(new Rect(new Point(0, 0), new Point(4 * 7, 13)));
        DrawOps.Draw(dst, dst.Bounds(), UniformImages.Black, new Point(), Op.Src);
        var d = new Drawer
        {
            Dst = dst,
            Src = UniformImages.White,
            Face = face,
            Dot = new Point26_6(new Int26_6(0), new Int26_6(ascent << 6)),
        };
        // Draw:
        //  - U+0053 LATIN CAPITAL LETTER S
        //  - U+03A3 GREEK CAPITAL LETTER SIGMA
        //  - U+222B INTEGRAL
        //  - U+3055 HIRAGANA LETTER SA
        // The testdata does not contain the CJK subfont files, so U+3055 HIRAGANA
        // LETTER SA (さ) should be rendered as U+FFFD REPLACEMENT CHARACTER (�).
        //
        // The missing subfont file will trigger an
        // "plan9font: couldn't read subfont ../shinonome/k12.3000" error message.
        // This is expected and can be ignored.
        d.DrawString("SΣ∫さ");

        var sb = new StringBuilder();
        for (int y = 0; y < 13; y++)
        {
            sb.Append((char)('0' + y % 10));
            sb.Append(' ');
            for (int x = 0; x < 28; x++)
            {
                sb.Append(dst.RgbaAt(x, y).R > 0 ? 'X' : '.');
            }
            // Highlight the last row before the baseline. Glyphs like 'S' without
            // descenders should not affect any pixels whose Y coordinate is >= the
            // baseline.
            if (y == ascent - 1)
            {
                sb.Append('_');
            }
            sb.Append('\n');
        }

        string want =
            "0 ..................X.........\n" +
            "1 .................X.X........\n" +
            "2 .XXXX..XXXXXX....X.....XXX..\n" +
            "3 X....X.X.........X....XX.XX.\n" +
            "4 X.......X........X....X.X.X.\n" +
            "5 X........X.......X....XXX.X.\n" +
            "6 .XXXX.....X......X....XX.XX.\n" +
            "7 .....X...X.......X....XX.XX.\n" +
            "8 .....X..X........X....XXXXX.\n" +
            "9 X....X.X.........X....XX.XX.\n" +
            "0 .XXXX..XXXXXX....X.....XXX.._\n" +
            "1 ...............X.X..........\n" +
            "2 ................X...........\n";
        Assert.Equal(want, sb.ToString());
    }
}