using CrestApps.OrchardCore.Reports.Designer;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// The public colorizer coded reports share with the Report Builder: rules by data type, the color scale, readable text
/// on a background, header styles and colors that color nothing.
/// </summary>
public sealed class ReportColumnColorizerTests
{
    private static readonly DateTime _today = new(2026, 11, 21, 9, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Create_WithoutColors_ReturnsNull()
    {
        // Act
        var none = ReportColumnColorizer.Create(DataFieldType.Decimal, null, null, _today);
        var empty = ReportColumnColorizer.Create(DataFieldType.Decimal, new ReportColumnColors(), null, _today);

        // Assert
        Assert.Null(none);
        Assert.Null(empty);
    }

    [Theory]
    [InlineData(59.9, "#B02A37")]
    [InlineData(60.0, null)]
    [InlineData(null, null)]
    public void StyleOf_ARuleOnANumberColumn_ColorsTheTextOfMatchingValues(double? percent, string expected)
    {
        // Arrange
        var colors = new ReportColumnColors
        {
            Rules = [new ReportColorRule { Operator = DataFilterOperator.LessThan, Values = ["60"], Color = "#B02A37", Target = ReportColorTarget.Text }],
        };
        var colorizer = ReportColumnColorizer.Create(DataFieldType.Decimal, colors, null, _today);

        // Act
        var style = colorizer.StyleOf(percent is { } value ? (decimal)value : null);

        // Assert
        Assert.Equal(expected, style?.Color);
    }

    [Theory]
    [InlineData(3, "#F8D7DA")]
    [InlineData(7, "#FFF3CD")]
    [InlineData(10, "#D1E7DD")]
    public void StyleOf_TheFirstRuleTheValueMeets_PaintsTheBackground_WithReadableText(int score, string background)
    {
        // Arrange: the Net Promoter Score bands.
        var colors = new ReportColumnColors
        {
            Rules =
            [
                new ReportColorRule { Operator = DataFilterOperator.LessThanOrEqual, Values = ["6"], Color = "#F8D7DA" },
                new ReportColorRule { Operator = DataFilterOperator.Between, Values = ["7", "8"], Color = "#FFF3CD" },
                new ReportColorRule { Operator = DataFilterOperator.GreaterThanOrEqual, Values = ["9"], Color = "#D1E7DD" },
            ],
        };
        var colorizer = ReportColumnColorizer.Create(DataFieldType.Integer, colors, null, _today);

        // Act
        var style = colorizer.StyleOf((long)score);

        // Assert
        Assert.Equal(background, style.BackgroundColor);
        Assert.Equal("#212529", style.Color);
    }

    [Fact]
    public void StyleOf_ABooleanRule_MatchesFalse()
    {
        // Arrange
        var colors = new ReportColumnColors
        {
            Rules = [new ReportColorRule { Operator = DataFilterOperator.Equals, Values = ["false"], Color = "#F8D7DA" }],
        };
        var colorizer = ReportColumnColorizer.Create(DataFieldType.Boolean, colors, null, _today);

        // Act
        var failed = colorizer.StyleOf(false);
        var passed = colorizer.StyleOf(true);

        // Assert
        Assert.Equal("#F8D7DA", failed?.BackgroundColor);
        Assert.Null(passed);
    }

    [Fact]
    public void StyleOf_AColorScale_SpansTheValuesShown()
    {
        // Arrange
        var colors = new ReportColumnColors
        {
            Scale = new ReportColorScale { LowColor = "#000000", HighColor = "#FFFFFF" },
        };
        var colorizer = ReportColumnColorizer.Create(DataFieldType.Decimal, colors, [0m, 50m, 100m], _today);

        // Act
        var low = colorizer.StyleOf(0m);
        var high = colorizer.StyleOf(100m);

        // Assert
        Assert.Equal("#000000", low.BackgroundColor);
        Assert.Equal("#FFFFFF", low.Color);
        Assert.Equal("#FFFFFF", high.BackgroundColor);
        Assert.Equal("#212529", high.Color);
    }

    [Fact]
    public void HeaderStyleOf_Colors_UsesTheHeaderColors()
    {
        // Act
        var style = ReportColumnColorizer.HeaderStyleOf(new ReportColumnColors { HeaderBackgroundColor = "#000000" });
        var none = ReportColumnColorizer.HeaderStyleOf(new ReportColumnColors());

        // Assert
        Assert.Equal("#000000", style.BackgroundColor);
        Assert.Equal("#FFFFFF", style.Color);
        Assert.Null(none);
    }
}
