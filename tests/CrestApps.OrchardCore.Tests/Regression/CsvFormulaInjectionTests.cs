using System.IO;
using System.Text;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;

namespace CrestApps.OrchardCore.Tests.Regression;

/// <summary>
/// Audit defect 12: the CSV exports were hand-written, so a cell such as <c>=HYPERLINK(...)</c> ran as a formula when the
/// file was opened in a spreadsheet, and some exports did not escape at all. Every export now writes through the shared
/// <c>CsvWriter</c>.
/// </summary>
public sealed class CsvFormulaInjectionTests
{
    private const string Formula = "=HYPERLINK(\"http://attacker.example\",\"Click\")";

    [Fact]
    public void ReportCsvExport_GuardsACellThatStartsLikeAFormula()
    {
        // Arrange
        var document = new ReportDocument()
            .Add(ReportSection.ForTable("Users", [new ReportColumn("Name")], [new ReportRow([Formula])]));

        // Act
        var content = Encoding.UTF8.GetString(new CsvReportExportFormat().Serialize(document)).TrimStart('﻿');

        // Assert
        Assert.Contains("\"'=HYPERLINK(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\n=HYPERLINK", content, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderCsv_GuardsACustomerNameThatStartsLikeAFormula()
    {
        // Arrange
        var order = new Order
        {
            ItemId = "order-1",
            OrderNumber = "1001",
            CustomerName = "@SUM(1+1)*cmd|' /C calc'!A0",
            Currency = "USD",
            CreatedUtc = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc),
        };

        // Act
        var csv = OrderCsv.Write([order], utc => utc);

        // Assert
        Assert.Contains(",'@SUM(1+1)*cmd|' /C calc'!A0,", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void StockCsv_GuardsATitleThatStartsLikeAFormula_AndStillReadsTheCounts()
    {
        // Arrange
        using var writer = new StringWriter();

        StockCsv.WriteHeader(writer);
        StockCsv.Write(writer, new StockItem { Sku = "MUG", Title = "+1+cmd", ContentItemId = "mug", OnHand = -2 });

        // Act
        var csv = writer.ToString();
        var rows = StockCsv.Read(new StringReader(csv), 10);

        // Assert
        Assert.Contains("MUG,'+1+cmd,mug,,-2,", csv, StringComparison.Ordinal);
        Assert.Equal(-2, Assert.Single(rows).OnHand);
    }
}
