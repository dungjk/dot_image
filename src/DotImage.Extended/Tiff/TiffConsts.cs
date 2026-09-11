// Ported from golang.org/x/image/tiff/consts.go.
// Copyright 2011 The Go Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license.

namespace DotImage.Extended.Tiff;

/// <summary>
/// Constants for the TIFF file format, as described in the TIFF 6.0 spec.
/// </summary>
internal static class TiffConsts
{
    // Header magic for little-endian files.
    public const byte LeHeader0 = (byte)'I';
    public const byte LeHeader1 = (byte)'I';
    public const byte LeHeader2 = 0x2A;
    public const byte LeHeader3 = 0x00;

    // Header magic for big-endian files.
    public const byte BeHeader0 = (byte)'M';
    public const byte BeHeader1 = (byte)'M';
    public const byte BeHeader2 = 0x00;
    public const byte BeHeader3 = 0x2A;

    // Length of an IFD entry in bytes.
    public const int IfdLen = 12;
}

/// <summary>TIFF data types (p. 14-16 of the spec).</summary>
internal static class TiffDataTypes
{
    public const int Byte = 1;
    public const int Ascii = 2;
    public const int Short = 3;
    public const int Long = 4;
    public const int Rational = 5;

    // Length of one instance of each data type in bytes.
    public static readonly uint[] Lengths = { 0, 1, 1, 2, 4, 8 };
}

/// <summary>TIFF tags (see p. 28-41 of the spec).</summary>
internal static class TiffTags
{
    public const int ImageWidth = 256;
    public const int ImageLength = 257;
    public const int BitsPerSample = 258;
    public const int Compression = 259;
    public const int PhotometricInterpretation = 262;

    public const int FillOrder = 266;

    public const int StripOffsets = 273;
    public const int SamplesPerPixel = 277;
    public const int RowsPerStrip = 278;
    public const int StripByteCounts = 279;

    public const int T4Options = 292; // CCITT Group 3 options, a set of 32 flag bits.
    public const int T6Options = 293; // CCITT Group 4 options, a set of 32 flag bits.

    public const int TileWidth = 322;
    public const int TileLength = 323;
    public const int TileOffsets = 324;
    public const int TileByteCounts = 325;

    public const int XResolution = 282;
    public const int YResolution = 283;
    public const int ResolutionUnit = 296;

    public const int Predictor = 317;
    public const int ColorMap = 320;
    public const int ExtraSamples = 338;
    public const int SampleFormat = 339;
}

/// <summary>Compression types (defined in various places in the spec and supplements).</summary>
internal static class TiffCompressionTypes
{
    public const int None = 1;
    public const int Ccitt = 2;
    public const int G3 = 3; // Group 3 Fax.
    public const int G4 = 4; // Group 4 Fax.
    public const int Lzw = 5;
    public const int JpegOld = 6; // Superseded by Jpeg.
    public const int Jpeg = 7;
    public const int Deflate = 8; // zlib compression.
    public const int PackBits = 32773;
    public const int DeflateOld = 32946; // Superseded by Deflate.
}

/// <summary>Photometric interpretation values (see p. 37 of the spec).</summary>
internal static class TiffPhotometricInterpretations
{
    public const int WhiteIsZero = 0;
    public const int BlackIsZero = 1;
    public const int Rgb = 2;
    public const int Paletted = 3;
    public const int TransMask = 4; // transparency mask
    public const int Cmyk = 5;
    public const int YCbCr = 6;
    public const int Cielab = 8;
}

/// <summary>Values for the tPredictor tag (page 64-65 of the spec).</summary>
internal static class TiffPredictors
{
    public const int None = 1;
    public const int Horizontal = 2;
}

/// <summary>Values for the tResolutionUnit tag (page 18).</summary>
internal static class TiffResolutionUnits
{
    public const int None = 1;
    public const int PerInch = 2; // Dots per inch.
    public const int PerCm = 3; // Dots per centimeter.
}

/// <summary>Represents the mode of the image.</summary>
internal enum TiffImageMode
{
    Bilevel,
    Paletted,
    Gray,
    GrayInvert,
    Rgb,
    Rgba,
    Nrgba,
    Cmyk,
}

/// <summary>Describes the type of compression used in <see cref="TiffWriterOptions"/>.</summary>
public enum CompressionType
{
    Uncompressed,
    Deflate,
    Lzw,
    CcittGroup3,
    CcittGroup4,
}

internal static class CompressionTypeExtensions
{
    /// <summary>
    /// Returns the compression type constant from the TIFF spec that is
    /// equivalent to c.
    /// </summary>
    public static uint SpecValue(this CompressionType c)
    {
        switch (c)
        {
            case CompressionType.Lzw:
                return (uint)TiffCompressionTypes.Lzw;
            case CompressionType.Deflate:
                return (uint)TiffCompressionTypes.Deflate;
            case CompressionType.CcittGroup3:
                return (uint)TiffCompressionTypes.G3;
            case CompressionType.CcittGroup4:
                return (uint)TiffCompressionTypes.G4;
        }
        return (uint)TiffCompressionTypes.None;
    }
}