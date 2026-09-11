using DotImage.Compress;

namespace DotImage.Tests.Compress;

public class LzwTests
{
    private static byte[] RoundTrip(byte[] input, int readChunk)
    {
        using var ms = new MemoryStream();
        using (var w = new LzwWriter(ms, LzwOrder.Lsb, 8))
            w.Write(input, 0, input.Length);
        ms.Position = 0;
        using var r = new LzwReader(ms, LzwOrder.Lsb, 8);
        var output = new byte[input.Length];
        int off = 0;
        while (off < output.Length)
        {
            int n = r.Read(output, off, Math.Min(readChunk, output.Length - off));
            if (n == 0)
                break;
            off += n;
        }
        Assert.Equal(output.Length, off);
        return output;
    }

    private static byte[] RoundTripSpan(byte[] input, int readChunk)
    {
        using var ms = new MemoryStream();
        using (var w = new LzwWriter(ms, LzwOrder.Lsb, 8))
            w.Write(input, 0, input.Length);
        ms.Position = 0;
        using var r = new LzwReader(ms, LzwOrder.Lsb, 8);
        var output = new byte[input.Length];
        int off = 0;
        while (off < output.Length)
        {
            int chunk = Math.Min(readChunk, output.Length - off);
            int n = r.Read(output.AsSpan(off, chunk));
            if (n == 0)
                break;
            off += n;
        }
        Assert.Equal(output.Length, off);
        return output;
    }

    private static byte[] RoundTripWriteSpan(byte[] input, int writeChunk)
    {
        using var ms = new MemoryStream();
        using (var w = new LzwWriter(ms, LzwOrder.Lsb, 8))
        {
            int off = 0;
            while (off < input.Length)
            {
                int chunk = Math.Min(writeChunk, input.Length - off);
                w.Write(input.AsSpan(off, chunk));
                off += chunk;
            }
        }
        ms.Position = 0;
        using var r = new LzwReader(ms, LzwOrder.Lsb, 8);
        var output = new byte[input.Length];
        int roff = 0;
        while (roff < output.Length)
        {
            int n = r.Read(output.AsSpan(roff));
            if (n == 0)
                break;
            roff += n;
        }
        Assert.Equal(output.Length, roff);
        return output;
    }

    public static TheoryData<int, int> ReadChunkCases => new()
    {
        { 10000, 1 },
        { 10000, 7 },
        { 10000, 511 },
        { 10000, 4096 },
        { 10000, 8193 },
        { 1, 1 },
        { 100, 50 },
        { 100, 100 },
        { 4096, 4096 },
        { 4097, 4097 },
    };

    public static TheoryData<int, int> WriteChunkCases => new()
    {
        { 10000, 1 },
        { 10000, 3 },
        { 10000, 997 },
        { 10000, 4096 },
        { 1, 1 },
        { 100, 50 },
        { 100, 100 },
        { 4096, 4096 },
        { 4097, 4097 },
        { 50000, 4096 },
    };

    [Theory]
    [MemberData(nameof(ReadChunkCases))]
    public void Lzw_RoundTrip_ByteRead(int length, int readChunk)
    {
        var input = new byte[length];
        for (int i = 0; i < length; i++)
            input[i] = (byte)(i & 0xff);
        var output = RoundTrip(input, readChunk);
        Assert.Equal(input, output);
    }

    [Theory]
    [MemberData(nameof(ReadChunkCases))]
    public void Lzw_RoundTrip_SpanRead(int length, int readChunk)
    {
        var input = new byte[length];
        for (int i = 0; i < length; i++)
            input[i] = (byte)(i & 0xff);
        var output = RoundTripSpan(input, readChunk);
        Assert.Equal(input, output);
    }

    [Theory]
    [MemberData(nameof(WriteChunkCases))]
    public void Lzw_RoundTrip_SpanWrite(int length, int writeChunk)
    {
        var input = new byte[length];
        for (int i = 0; i < length; i++)
            input[i] = (byte)(i & 0xff);
        var output = RoundTripWriteSpan(input, writeChunk);
        Assert.Equal(input, output);
    }
}
