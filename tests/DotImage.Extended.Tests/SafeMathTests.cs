using DotImage.Extended.Internal;
using Xunit;

namespace DotImage.Extended.Tests;

public class SafeMathTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, true)]
    [InlineData(1, 1, 1, 1, true)]
    [InlineData(10, 20, 30, 6000, true)]
    [InlineData(46340, 46340, 1, 2147395600, true)]
    [InlineData(int.MaxValue, 1, 1, int.MaxValue, true)]
    [InlineData(int.MaxValue, 1, 0, 0, true)]
    [InlineData(1, 1, -1, -1, false)]
    [InlineData(-1, 1, 1, -1, false)]
    [InlineData(1, -1, 1, -1, false)]
    [InlineData(int.MinValue, 1, 1, -1, false)]
    [InlineData(1000000, 1000000, 1000000, -1, false)]
    [InlineData(int.MaxValue, 2, 1, -1, false)]
    public void Mul3(int x, int y, int z, int expectedValue, bool expectedOk)
    {
        (int value, bool ok) = SafeMath.Mul3(x, y, z);
        Assert.Equal(expectedOk, ok);
        if (expectedOk)
        {
            Assert.Equal(expectedValue, value);
        }
    }
}