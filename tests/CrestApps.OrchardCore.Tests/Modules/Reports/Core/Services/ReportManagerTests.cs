using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Core.Services;

public sealed class ReportManagerTests
{
    private static readonly Permission _permission = new("TestReport");

    [Fact]
    public void GetReports_RegisteredReports_AreListedWithoutBeingCreated()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddReport<SampleReport>();
        services.AddReport<SampleReport>();
        services.Configure<ReportOptions>(options => options.Add(Describe("from-family")));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var manager = new ReportManager(scope.ServiceProvider.GetRequiredService<IOptions<ReportOptions>>(), scope.ServiceProvider);

        // Act
        var reports = manager.GetReports();

        // Assert
        Assert.Equal([SampleReport.Name, "from-family"], reports.Select(report => report.Name));
        Assert.Equal(SampleReport.DisplayName, manager.GetDescriptor("SAMPLE").DisplayName);
        Assert.Equal(0, SampleReport.Instances);
        Assert.IsType<SampleReport>(manager.GetReport("sample"));
        Assert.IsType<FamilyReport>(manager.GetReport("from-family"));
        Assert.Null(manager.GetReport("missing"));
        Assert.Null(manager.GetDescriptor(null));
    }

    [Fact]
    public void Constructor_WhenReportNamesDifferOnlyByCase_ShouldThrow()
    {
        // Arrange
        var options = new ReportOptions()
            .Add(Describe("sample"))
            .Add(Describe("SAMPLE"));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => new ReportManager(Options.Create(options), new ServiceCollection().BuildServiceProvider()));

        // Assert
        Assert.Contains("sample", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WhenAFamilyReportCollidesWithAReport_ShouldThrow()
    {
        // Arrange
        var options = new ReportOptions()
            .Add(ReportDescriptor.Create<SampleReport>())
            .Add(Describe(SampleReport.Name));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new ReportManager(Options.Create(options), new ServiceCollection().BuildServiceProvider()));
    }

    private static ReportDescriptor Describe(string name)
    {
        return new ReportDescriptor(
            name,
            LocalizationSource.Create(name),
            LocalizationSource.Create(name),
            "Tests",
            _permission,
            typeof(FamilyReport),
            _ => new FamilyReport());
    }

    private sealed class SampleReport : IReport
    {
        public SampleReport()
        {
            Instances++;
        }

        public static int Instances { get; private set; }

        public static string Name => "sample";

        public static LocalizationSource DisplayName { get; } = LocalizationSource.Create("Sample");

        public static LocalizationSource Description { get; } = LocalizationSource.Create("A sample report.");

        public static string Category => "Tests";

        public static Permission Permission => _permission;

        public Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ReportDocument());
        }
    }

    private sealed class FamilyReport : IReportRunner
    {
        public Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ReportDocument());
        }
    }
}
