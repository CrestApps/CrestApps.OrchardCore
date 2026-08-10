using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Reports;

public sealed class ReportColorTests
{
    [Theory]
    [InlineData("#2563EB", "FF2563EB")]
    [InlineData("2563eb", "FF2563EB")]
    [InlineData("#abc", "FFAABBCC")]
    [InlineData("80FF0000", "80FF0000")]
    public void TryGetArgb_WhenColorIsHexadecimal_ShouldNormalizeToArgb(string color, string expected)
    {
        // Act
        var result = ReportColor.TryGetArgb(color, out var argb);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, argb);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("not-a-color")]
    [InlineData("")]
    [InlineData(null)]
    public void TryGetArgb_WhenColorIsNotHexadecimal_ShouldReturnFalse(string color)
    {
        // Act
        var result = ReportColor.TryGetArgb(color, out var argb);

        // Assert
        Assert.False(result);
        Assert.Null(argb);
    }

    [Theory]
    [InlineData("#2563eb", "#2563EB")]
    [InlineData("2563EB", "#2563EB")]
    [InlineData("steelblue", "steelblue")]
    [InlineData("80FF0000", "#FF0000")]
    public void ToCssColor_WhenColorIsValid_ShouldReturnSafeCssColor(string color, string expected)
    {
        // Act
        var css = ReportColor.ToCssColor(color);

        // Assert
        Assert.Equal(expected, css);
    }

    [Theory]
    [InlineData("red; content: 'x'")]
    [InlineData("rgb(1,2,3)")]
    [InlineData("")]
    [InlineData(null)]
    public void ToCssColor_WhenColorIsUnsafeOrEmpty_ShouldReturnNull(string color)
    {
        // Act
        var css = ReportColor.ToCssColor(color);

        // Assert
        Assert.Null(css);
    }
}
