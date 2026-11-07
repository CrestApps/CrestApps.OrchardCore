using CrestApps.OrchardCore.Core.Migrations;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Tests.Core.Migrations;

/// <summary>
/// Bug: data migrations logged a failed record and skipped it, one line among many, so nobody noticed records that were
/// never migrated. Each skip now names the record, and the migration ends with one warning that counts and lists them.
/// </summary>
public sealed class MigrationSkipReportTests
{
    [Fact]
    public void LogSummary_AfterSkips_WritesOneWarningWithTheCountAndTheIds()
    {
        // Arrange
        var logger = new RecordingLogger<MigrationSkipReportTests>();
        var report = new MigrationSkipReport("AIProfileDocumentMigrations", logger);

        // Act
        report.Skip("profile-1", "validation failed: Name is required");
        report.Skip("profile-2", "could not be read", new InvalidOperationException("bad json"));
        report.LogSummary();

        // Assert
        Assert.Equal(2, report.Count);
        Assert.Equal(["profile-1", "profile-2"], report.SkippedIds);

        var warnings = logger.At(LogLevel.Warning).ToList();
        Assert.Equal(3, warnings.Count);
        Assert.Contains("profile-1", warnings[0], StringComparison.Ordinal);
        Assert.Contains("2 skipped record(s)", warnings[2], StringComparison.Ordinal);
        Assert.Contains("profile-1, profile-2", warnings[2], StringComparison.Ordinal);
    }

    [Fact]
    public void LogSummary_WithoutSkips_WritesNothing()
    {
        // Arrange
        var logger = new RecordingLogger<MigrationSkipReportTests>();
        var report = new MigrationSkipReport("AIProfileDocumentMigrations", logger);

        // Act
        report.LogSummary();

        // Assert
        Assert.Empty(logger.At(LogLevel.Warning));
    }

    [Fact]
    public void LogSummary_WithManySkips_ListsTheFirstIdsAndCountsTheRest()
    {
        // Arrange
        var logger = new RecordingLogger<MigrationSkipReportTests>();
        var report = new MigrationSkipReport("AIDeploymentV1DocumentMigrations", logger);

        for (var index = 0; index < MigrationSkipReport.MaxListedIds + 5; index++)
        {
            report.Skip($"deployment-{index}", "no name");
        }

        // Act
        report.LogSummary();

        // Assert
        var summary = logger.At(LogLevel.Warning).Last();
        Assert.Contains($"{MigrationSkipReport.MaxListedIds + 5} skipped record(s)", summary, StringComparison.Ordinal);
        Assert.Contains("and 5 more", summary, StringComparison.Ordinal);
    }
}
