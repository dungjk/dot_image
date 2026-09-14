using System.IO;
using DotImage.Extended.Internal;

namespace DotImage.Extended;

/// <summary>
/// Decode-time safety guards (dimension validation against decompression bombs).
/// </summary>
public static class Guards
{
    /// <summary>
    /// Upper bound on decoded image pixels (2^28-1). Prevents decompression
    /// bombs from forcing multi-gigabyte allocations.
    /// </summary>
    public const long MaxImagePixels = (1L << 28) - 1;

    /// <summary>
    /// Validates that a decoded image buffer of w*h*bytesPerPixel bytes can be
    /// safely allocated, throwing InvalidDataException for negative dimensions,
    /// integer overflow, or decompression bombs exceeding MaxImagePixels.
    /// </summary>
    public static void EnsureDecodeSize(long w, long h, int bytesPerPixel)
    {
        if (w < 0 || h < 0)
        {
            throw new InvalidDataException("image: negative dimensions");
        }
        if (w * h > MaxImagePixels)
        {
            throw new InvalidDataException("image: image too large (decompression bomb)");
        }
        if (!SafeMath.Mul3((int)w, (int)h, bytesPerPixel).Ok)
        {
            throw new InvalidDataException("image: image buffer size overflows");
        }
    }
}