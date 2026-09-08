using DotImage.Extended.Font.GoFont.Data;

namespace DotImage.Extended.Font.GoFont;

/// <summary>Ported from golang.org/x/image/font/gofont. Provides embedded
/// TTF data for the Go font family.</summary>
public static class GoFont
{
    /// <summary>The "Go Regular" TrueType font.</summary>
    public static ReadOnlySpan<byte> Goregular => GoregularData.TTF;

    /// <summary>The "Go Italic" TrueType font.</summary>
    public static ReadOnlySpan<byte> Goitalic => GoitalicData.TTF;

    /// <summary>The "Go Bold" TrueType font.</summary>
    public static ReadOnlySpan<byte> Gobold => GoboldData.TTF;

    /// <summary>The "Go Bold Italic" TrueType font.</summary>
    public static ReadOnlySpan<byte> Gobolditalic => GobolditalicData.TTF;

    /// <summary>The "Go Medium" TrueType font.</summary>
    public static ReadOnlySpan<byte> Gomedium => GomediumData.TTF;

    /// <summary>The "Go Medium Italic" TrueType font.</summary>
    public static ReadOnlySpan<byte> Gomediumitalic => GomediumitalicData.TTF;

    /// <summary>The "Go Mono" TrueType font.</summary>
    public static ReadOnlySpan<byte> Gomono => GomonoData.TTF;

    /// <summary>The "Go Mono Italic" TrueType font.</summary>
    public static ReadOnlySpan<byte> Gomonoitalic => GomonoitalicData.TTF;

    /// <summary>The "Go Mono Bold" TrueType font.</summary>
    public static ReadOnlySpan<byte> Gomonobold => GomonoboldData.TTF;

    /// <summary>The "Go Mono Bold Italic" TrueType font.</summary>
    public static ReadOnlySpan<byte> Gomonobolditalic => GomonobolditalicData.TTF;

    /// <summary>The "Go Smallcaps" TrueType font.</summary>
    public static ReadOnlySpan<byte> Gosmallcaps => GosmallcapsData.TTF;

    /// <summary>The "Go Smallcaps Italic" TrueType font.</summary>
    public static ReadOnlySpan<byte> Gosmallcapsitalic => GosmallcapsitalicData.TTF;

    /// <summary>Returns the TTF data for the font with the given Go package name.</summary>
    public static ReadOnlySpan<byte> TTF(string name) => name switch
    {
        "goregular" => Goregular,
        "goitalic" => Goitalic,
        "gobold" => Gobold,
        "gobolditalic" => Gobolditalic,
        "gomedium" => Gomedium,
        "gomediumitalic" => Gomediumitalic,
        "gomono" => Gomono,
        "gomonoitalic" => Gomonoitalic,
        "gomonobold" => Gomonobold,
        "gomonobolditalic" => Gomonobolditalic,
        "gosmallcaps" => Gosmallcaps,
        "gosmallcapsitalic" => Gosmallcapsitalic,
        _ => throw new ArgumentException($"Unknown Go font: {name}", nameof(name)),
    };
}
