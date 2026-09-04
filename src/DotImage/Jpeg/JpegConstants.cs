// Ported from Go src/image/jpeg (shared constants and types)


namespace DotImage.Jpeg;

internal static class JpegConstants
{
    internal const int DcTable = 0;
    internal const int AcTable = 1;
    internal const int MaxTc = 1;
    internal const int MaxTh = 3;
    internal const int MaxTq = 3;
    internal const int MaxComponents = 4;
    internal const int BlockSize = 8 * 8;

    internal const byte Sof0Marker = 0xc0;
    internal const byte Sof1Marker = 0xc1;
    internal const byte Sof2Marker = 0xc2;
    internal const byte DhtMarker = 0xc4;
    internal const byte Rst0Marker = 0xd0;
    internal const byte Rst7Marker = 0xd7;
    internal const byte SoiMarker = 0xd8;
    internal const byte EoiMarker = 0xd9;
    internal const byte SosMarker = 0xda;
    internal const byte DqtMarker = 0xdb;
    internal const byte DriMarker = 0xdd;
    internal const byte ComMarker = 0xfe;
    internal const byte App0Marker = 0xe0;
    internal const byte App14Marker = 0xee;
    internal const byte App15Marker = 0xef;

    internal const byte AdobeTransformUnknown = 0;
    internal const byte AdobeTransformYCbCr = 1;
    internal const byte AdobeTransformYCbCrK = 2;

    internal static readonly int[] Unzig =
    [
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,
    ];
}

public sealed class JpegFormatException : Exception
{
    public JpegFormatException(string message) : base("invalid JPEG format: " + message) { }
}

public sealed class JpegUnsupportedException : Exception
{
    public JpegUnsupportedException(string message) : base("unsupported JPEG feature: " + message) { }
}

internal struct Component
{
    public int H;
    public int V;
    public byte C;
    public byte Tq;
    public int ExpandH;
    public int ExpandV;
}

internal struct Bits
{
    public uint A;
    public uint M;
    public int N;
}

internal sealed class HuffmanTable
{
    public int NCodes;
    public readonly ushort[] Lut = new ushort[1 << 8];
    public readonly byte[] Vals = new byte[256];
    public readonly int[] MinCodes = new int[16];
    public readonly int[] MaxCodes = new int[16];
    public readonly int[] ValsIndices = new int[16];
}

internal struct ScanComponent
{
    public byte CompIndex;
    public byte Td;
    public byte Ta;
}
