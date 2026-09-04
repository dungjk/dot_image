// Ported from Go src/image/png error types

namespace DotImage.Png;

public sealed class PngFormatException(string message) : Exception($"png: invalid format: {message}");

public sealed class PngUnsupportedException(string message) : Exception($"png: unsupported feature: {message}");
