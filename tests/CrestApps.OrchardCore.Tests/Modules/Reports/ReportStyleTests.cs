using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Reports;

public sealed class ReportStyleTests
{
    [Fact]
    public void IsEmpty_WhenNoColorAndNotBold_ShouldReturnTrue()
    {
        var style = new ReportStyle();

        Assert.True(style.IsEmpty);
    }

    [Theory]
    [InlineData("#111111", null, false)]
    [InlineData(null, "#222222", false)]
    [InlineData(null, null, true)]
    public void IsEmpty_WhenAnyPropertySet_ShouldReturnFalse(string color, string background, bool bold)
    {
        var style = ReportStyle.Create(color, background, bold);

        Assert.False(style.IsEmpty);
    }

    [Fact]
    public void ToInlineCss_WhenAllPropertiesSet_ShouldBuildDeclaration()
    {
        // Arrange
        var style = ReportStyle.Create("#2563EB", "#EFF6FF", bold: true);

        // Act
        var css = style.ToInlineCss();

        // Assert
        Assert.Equal("color:#2563EB;background-color:#EFF6FF;font-weight:600;", css);
    }

    [Fact]
    public void ToInlineCss_WhenColorIsUnsafe_ShouldOmitIt()
    {
        // Arrange
        var style = ReportStyle.Create("red; content:'x'", "steelblue");

        // Act
        var css = style.ToInlineCss();

        // Assert
        Assert.Equal("background-color:steelblue;", css);
    }
}
