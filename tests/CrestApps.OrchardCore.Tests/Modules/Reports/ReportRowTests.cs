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
}
