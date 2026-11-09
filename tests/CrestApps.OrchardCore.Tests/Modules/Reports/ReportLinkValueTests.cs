using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Moq;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Reports;

/// <summary>
/// A link value: any report can put one in a table cell, the report page renders it as an anchor, and every export
/// writes its text without the address.
/// </summary>
public sealed class ReportLinkValueTests
{
    [Fact]
    public void Link_RoundTripsItsTextAndAddress()
    {
        var value = ReportValue.Link("Session 7", "/Admin/ai/chat/session/p 1/s:7");

        Assert.True(ReportValue.TryGetLink(value, out var text, out var url));
        Assert.Equal("Session 7", text);
        Assert.Equal("/Admin/ai/chat/session/p 1/s:7", url);
        Assert.Equal("Session 7", ReportValue.GetText(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.example/path")]
    [InlineData("/\\evil.example")]
    [InlineData("data:text/html,hi")]
    [InlineData("relative/path")]
    public void Link_WithoutASafeAddress_IsItsTextAlone(string url)
    {
        var value = ReportValue.Link("Session 7", url);

        Assert.Equal("Session 7", value);
        Assert.False(ReportValue.TryGetLink(value, out _, out _));
    }

    [Fact]
    public void Link_AcceptsAnAbsoluteWebAddress()
    {
        Assert.True(ReportValue.TryGetLink(ReportValue.Link("Docs", "https://example.com/a?b=c"), out _, out var url));
        Assert.Equal("https://example.com/a?b=c", url);
    }

    [Fact]
    public void TryGetLink_RefusesAForgedUnsafeAddress()
    {
        // A value written by hand, not by Link, still can't carry a script address.
        var forged = "\u001Elink:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("javascript:alert(1)")) + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes("x")) + "\u001F";

        Assert.False(ReportValue.TryGetLink(forged, out _, out _));
        Assert.False(ReportValue.TryGetLink("\u001Elink:not-base64\u001F", out _, out _));
        Assert.False(ReportValue.TryGetLink("plain text", out _, out _));
    }

    [Fact]
    public void TheReportPage_RendersALinkAsAnEncodedAnchor()
    {
        var value = ReportValue.Link("<b>s1</b>", "/Admin/ai/chat/session/p1/s1?x=\"y\"");

        Assert.True(ReportValueHtml.TryCreateLink(value, out var html));

        using var writer = new StringWriter();
        html.WriteTo(writer, HtmlEncoder.Default);

        Assert.Equal("<a href=\"/Admin/ai/chat/session/p1/s1?x=&quot;y&quot;\">&lt;b&gt;s1&lt;/b&gt;</a>", writer.ToString());
        Assert.False(ReportValueHtml.TryCreateLink("s1", out _));
    }

    [Fact]
    public async Task AnExport_WritesTheTextOfALink_InTablesAndEverywhereElse()
    {
        var document = Document();

        await Resolver().ResolveAsync(document);

        Assert.Equal("s1", document.Sections[0].Rows[0].Cells[0]);
        Assert.Equal("Open", document.Sections[1].Metrics[0].Value);
        Assert.Equal("Docs", document.Sections[2].Bars[0].Label);
    }

    [Fact]
    public async Task ThePage_KeepsALinkInATableCell_AndWritesItsTextElsewhere()
    {
        var document = Document();

        await Resolver().ResolveNonTableValuesAsync(document);

        Assert.True(ReportValue.TryGetLink(document.Sections[0].Rows[0].Cells[0], out _, out _));
        Assert.Equal("Open", document.Sections[1].Metrics[0].Value);
        Assert.Equal("Docs", document.Sections[2].Bars[0].Label);
    }

    private static ReportDocument Document()
        => new ReportDocument()
            .Add(ReportSection.ForTable("Sessions", [new ReportColumn("Session")], [new ReportRow([ReportValue.Link("s1", "/Admin/s1")])]))
            .Add(ReportSection.ForMetrics("Figures", [new ReportMetric("Latest", ReportValue.Link("Open", "/Admin/latest"))]))
            .Add(ReportSection.ForBars("Bars", [new ReportBar(ReportValue.Link("Docs", "https://example.com"), "1", 1)]));

    private static ReportDisplayValueResolver Resolver()
        => new(Mock.Of<IShapeFactory>(), Mock.Of<IDisplayHelper>());
}
