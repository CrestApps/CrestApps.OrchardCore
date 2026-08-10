using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Reports;

public sealed class ReportRowTests
{
    [Fact]
    public void Constructor_WhenEmphasized_ShouldCreateGrandTotalRow()
    {
        var row = new ReportRow(["Total", "10"], emphasize: true);

        Assert.Equal(ReportRowKind.GrandTotal, row.Kind);
        Assert.True(row.Emphasize);
    }

    [Fact]
    public void Constructor_WhenKindIsSubtotal_ShouldRemainEmphasized()
    {
        var row = new ReportRow(["Group total", "5"], ReportRowKind.Subtotal);

        Assert.Equal(ReportRowKind.Subtotal, row.Kind);
        Assert.True(row.Emphasize);
    }

    [Fact]
    public void GetCellStyle_WhenCellHasNoOverride_ShouldFallBackToRowStyle()
    {
        // Arrange
        var rowStyle = ReportStyle.Create("#111111");
        var row = new ReportRow(["A", "B"]).WithStyle(rowStyle);

        // Act
        var style = row.GetCellStyle(1);

        // Assert
        Assert.Same(rowStyle, style);
    }

    [Fact]
    public void GetCellStyle_WhenCellHasOverride_ShouldPreferCellStyle()
    {
        // Arrange
        var rowStyle = ReportStyle.Create("#111111");
        var cellStyle = ReportStyle.Create("#222222");
        var row = new ReportRow(["A", "B"])
            .WithStyle(rowStyle)
            .WithCellStyle(1, cellStyle);

        // Act & Assert
        Assert.Same(rowStyle, row.GetCellStyle(0));
        Assert.Same(cellStyle, row.GetCellStyle(1));
    }

    [Fact]
    public void WithCellStyle_WhenIndexIsNegative_ShouldThrow()
    {
        var row = new ReportRow(["A"]);

        Assert.Throws<ArgumentOutOfRangeException>(() => row.WithCellStyle(-1, ReportStyle.Create("#000000")));
    }
}
