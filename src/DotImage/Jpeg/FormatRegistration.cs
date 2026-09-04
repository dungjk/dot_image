// JPEG format registration


namespace DotImage.Jpeg;

internal static class FormatRegistration
{
    internal static void Register()
    {
        FormatRegistry.RegisterFormat(
            "jpeg",
            "\xff\xd8",
            JpegReader.Decode,
            JpegReader.DecodeConfig);
    }
}
