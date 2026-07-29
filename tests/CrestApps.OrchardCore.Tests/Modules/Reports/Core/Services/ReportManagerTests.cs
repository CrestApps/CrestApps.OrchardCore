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
        var exception = Assert.Throws<InvalidOperationException>(() => new ReportManager(reports));

        // Assert
        Assert.Contains("sample", exception.Message, StringComparison.OrdinalIgnoreCase);
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
