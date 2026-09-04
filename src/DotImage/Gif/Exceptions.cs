// Ported from Go src/image/gif error types


namespace DotImage.Gif;

public sealed class GifNotEnoughException() : Exception("gif: not enough image data");

public sealed class GifTooMuchException() : Exception("gif: too much image data");

public sealed class GifBadPixelException() : Exception("gif: invalid pixel value");
