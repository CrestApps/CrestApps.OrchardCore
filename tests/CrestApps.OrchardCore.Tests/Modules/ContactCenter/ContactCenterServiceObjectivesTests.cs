using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterServiceObjectivesTests
{
    [Fact]
    public void ServiceObjectives_DefineAvailabilityLatencyAndRecoveryContracts()
    {
        // Arrange
        var objectives = LoadObjectives();

        // Act
        var availability = objectives["availabilityObjectives"]?.AsArray();
        var latency = objectives["latencyObjectives"]?.AsArray();
        var recovery = objectives["recoveryObjectives"]?.AsArray();

        // Assert
        Assert.Equal("blocked-until-r0-r8-pass", objectives["releaseStatus"]?.GetValue<string>());
        Assert.Equal(30, objectives["measurementWindowDays"]?.GetValue<int>());
        Assert.NotEmpty(availability);
        Assert.NotEmpty(latency);
        Assert.NotEmpty(recovery);
    }

    [Fact]
    public void ServiceObjectives_PercentileAndErrorBudgetTargetsAreInternallyConsistent()
    {
        // Arrange
        var objectives = LoadObjectives();

        // Act & Assert
        foreach (var objective in objectives["availabilityObjectives"]?.AsArray())
        {
            var target = objective?["targetPercent"]?.GetValue<double>() ?? 0;
            var errorBudget = objective?["maximumMonthlyErrorBudgetMinutes"]?.GetValue<double>() ?? 0;

            Assert.InRange(target, 99, 100);
            Assert.True(errorBudget > 0);
        }

        foreach (var objective in objectives["latencyObjectives"]?.AsArray())
        {
            var p95 = objective?["p95Milliseconds"]?.GetValue<int>() ?? 0;
            var p99 = objective?["p99Milliseconds"]?.GetValue<int>() ?? 0;

            Assert.True(p95 > 0);
            Assert.True(p99 >= p95);
        }
    }

    [Fact]
    public void ServiceObjectives_DefineBoundedDependencyTimeouts()
    {
        // Arrange
        var objectives = LoadObjectives();
        var dependencyLimits = objectives["dependencyLimits"]?.AsArray();

        // Act & Assert
        Assert.Contains(
            dependencyLimits,
            limit => limit?["id"]?.GetValue<string>() == "provider-webhook-body" &&
                limit["maximumBytes"]?.GetValue<int>() == 1_048_576);

        foreach (var limit in dependencyLimits.Where(limit => limit?["timeoutMilliseconds"] is not null))
        {
            var timeout = limit?["timeoutMilliseconds"]?.GetValue<int>() ?? 0;

            Assert.True(timeout > 0);
        }
    }

    [Fact]
    public void ServiceObjectives_AssignEveryAreaToDriAndApproverRoles()
    {
        // Arrange
        var objectives = LoadObjectives();
        var ownership = objectives["ownership"]?.AsArray();

        // Act & Assert
        Assert.NotEmpty(ownership);

        foreach (var area in ownership)
        {
            Assert.False(string.IsNullOrWhiteSpace(area?["area"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(area?["driRole"]?.GetValue<string>()));
            Assert.NotEmpty(area?["approverRoles"]?.AsArray());
        }
    }

    private static JsonObject LoadObjectives()
    {
        var repositoryRoot = FindRepositoryRoot();
        var objectivesPath = Path.Combine(
            repositoryRoot,
            ".github",
            "contact-center",
            "service-objectives.v1.json");

        return JsonNode.Parse(File.ReadAllText(objectivesPath))?.AsObject() ??
            throw new InvalidOperationException("The Contact Center service objectives are invalid.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }
}
