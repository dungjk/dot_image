using DotImage.Color;
using DotImage.Extended.Colornames;
using Xunit;

namespace DotImage.Extended.Tests;

public class ColorNamesTests
{
    [Fact]
    public void NamesAreSortedAlphabetically()
    {
        string[] sorted = (string[])ColorNames.Names.Clone();
        System.Array.Sort(sorted, System.StringComparer.Ordinal);
        Assert.Equal(sorted, ColorNames.Names);
    }

    [Fact]
    public void MapContainsExactlyAllNames()
    {
        Assert.Equal(ColorNames.Names.Length, ColorNames.Map.Count);
        foreach (string name in ColorNames.Names)
        {
            Assert.True(ColorNames.Map.ContainsKey(name), $"missing map entry for {name}");
        }
    }

    [Fact]
    public void StaticFieldsMatchMap()
    {
        Assert.Equal(ColorNames.Map["red"], ColorNames.Red);
        Assert.Equal(ColorNames.Map["green"], ColorNames.Green);
        Assert.Equal(ColorNames.Map["blue"], ColorNames.Blue);
        Assert.Equal(ColorNames.Map["black"], ColorNames.Black);
        Assert.Equal(ColorNames.Map["white"], ColorNames.White);
    }

    [Theory]
    [InlineData("red", 0xff, 0x00, 0x00, 0xff)]
    [InlineData("green", 0x00, 0x80, 0x00, 0xff)]
    [InlineData("blue", 0x00, 0x00, 0xff, 0xff)]
    [InlineData("black", 0x00, 0x00, 0x00, 0xff)]
    [InlineData("white", 0xff, 0xff, 0xff, 0xff)]
    [InlineData("aliceblue", 0xf0, 0xf8, 0xff, 0xff)]
    [InlineData("seagreen", 0x2e, 0x8b, 0x57, 0xff)]
    [InlineData("darkred", 0x8b, 0x00, 0x00, 0xff)]
    public void KnownColors(string name, byte r, byte g, byte b, byte a)
    {
        Assert.True(ColorNames.Map.TryGetValue(name, out Rgba value), $"unknown color {name}");
        Assert.Equal(new Rgba(r, g, b, a), value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("notacolor")]
    [InlineData("Red")]
    [InlineData(" RED")]
    public void UnknownColorsAreNotResolved(string name)
    {
        Assert.False(ColorNames.Map.ContainsKey(name));
    }
}