// Ported from Go hash/crc32 (PNG uses CRC-32 IEEE)

using System.IO.Hashing;

namespace DotImage.Png;

internal sealed class Crc32Hasher
{
    private Crc32 _crc = new();

    public void Reset() => _crc = new Crc32();

    public void Write(ReadOnlySpan<byte> data) => _crc.Append(data);

    public uint Sum32() => _crc.GetCurrentHashAsUInt32();
}
