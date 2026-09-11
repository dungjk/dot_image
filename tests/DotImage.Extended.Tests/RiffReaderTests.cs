using DotImage.Extended.Webp.Riff;
using Xunit;

namespace DotImage.Extended.Tests;

public class RiffReaderTests
{
    [Fact]
    public void TestShortChunks()
    {
        foreach (var m in new[] { 0, 8, 15, 200, 300 })
        {
            foreach (var n in new[] { 0, 1, 2, 7 })
            {
                byte[] s = new byte[]
                {
                    (byte)'R', (byte)'I', (byte)'F', (byte)'F',
                    0x00, 0x01, 0x00, 0x00,
                    (byte)'A', (byte)'B', (byte)'C', (byte)'D',
                    (byte)'a', (byte)'b', (byte)'c', (byte)'d',
                    (byte)(m >> 0), (byte)(m >> 8), (byte)(m >> 16), (byte)(m >> 24)
                };

                using var stream = new MemoryStream(s);
                var (_, r) = RiffReader.NewReader(stream);

                if (m + 12 > 256)
                {
                    var err0 = Assert.Throws<RiffException>(() => r.Next());
                    Assert.Equal("riff: list subchunk too long", err0.Message);
                    continue;
                }

                var (chunkID, chunkLen, _) = r.Next();
                Assert.Equal(new FourCC('a', 'b', 'c', 'd'), chunkID);
                Assert.Equal((uint)m, chunkLen);

                var ex = Assert.Throws<RiffException>(() => r.Next());
                string want = m == 0 ? "riff: short chunk header" : "riff: short chunk data";
                Assert.Equal(want, ex.Message);
            }
        }
    }
}
