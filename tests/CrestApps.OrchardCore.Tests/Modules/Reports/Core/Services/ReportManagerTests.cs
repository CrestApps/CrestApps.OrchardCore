using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Core.Services;

public sealed class ReportManagerTests
{
    [Fact]
    public void Constructor_WhenReportNamesDifferOnlyByCase_ShouldThrow()
    {
        // Arrange
        IReport[] reports =
        [
            new TestReport("sample"),
            new TestReport("SAMPLE"),
        ];

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => new ReportManager(reports, []));

        // Assert
        Assert.Contains("sample", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ListReports_WhenReportsComeFromProviders_ShouldIncludeThem()
    {
        // Arrange
        IReport[] reports = [new TestReport("individual")];

        IReportProvider[] providers =
        [
            new TestReportProvider(new TestReport("from-provider-a"), new TestReport("from-provider-b")),
        ];

        // Act
        var manager = new ReportManager(reports, providers);

        // Assert
        Assert.NotNull(manager.FindByName("individual"));
        Assert.NotNull(manager.FindByName("from-provider-a"));
        Assert.NotNull(manager.FindByName("from-provider-b"));
        Assert.Equal(3, manager.ListReports().Count);
    }

    [Fact]
    public void Constructor_WhenProviderReportCollidesWithIndividualReport_ShouldThrow()
    {
        // Arrange
        IReport[] reports = [new TestReport("shared")];

        IReportProvider[] providers = [new TestReportProvider(new TestReport("shared"))];

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new ReportManager(reports, providers));
    }

    private sealed class TestReportProvider : IReportProvider
    {
        private readonly IReport[] _reports;

        public TestReportProvider(params IReport[] reports)
        {
            _reports = reports;
        }

        public IEnumerable<IReport> GetReports()
        {
            return _reports;
        }
    }

    private sealed class TestReport : IReport
    {
        public TestReport(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public LocalizedString DisplayName => new(Name, Name);

        public LocalizedString Description => new(Name, Name);

        public string Category => "Tests";

        public Permission Permission => new("TestReport");

        public Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ReportDocument());
        }
    }
}
