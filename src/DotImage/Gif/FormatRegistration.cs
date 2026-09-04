// GIF format registration


namespace DotImage.Gif;

internal static class FormatRegistration
{
    private const string GifMagic = "GIF8?a";

    internal static void Register()
    {
        FormatRegistry.RegisterFormat(
            "gif",
            GifMagic,
            GifReader.Decode,
            GifReader.DecodeConfig);
    }
}
