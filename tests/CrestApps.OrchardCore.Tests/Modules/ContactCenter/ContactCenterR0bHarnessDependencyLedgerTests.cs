using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterR0bHarnessDependencyLedgerTests
{
    [Fact]
    public void Ledger_TracksEveryRequiredDistributedFailureScenario()
    {
        // Arrange
        var ledger = LoadLedger();
        var scenarioIds = ledger["scenarios"]?.AsArray()
            .Select(scenario => scenario?["id"]?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.Equal("minimal-harness-implemented-certification-pending", ledger["status"]?.GetValue<string>());
        Assert.Contains("redis-backplane-two-shell", scenarioIds);
        Assert.Contains("duplicate-provider-stream-two-process", scenarioIds);
        Assert.Contains("listener-lease-loss", scenarioIds);
        Assert.Contains("application-node-failure", scenarioIds);
        Assert.Contains("redis-network-partition", scenarioIds);
        Assert.Contains("database-network-partition", scenarioIds);
        Assert.Contains("rolling-version-deployment", scenarioIds);
    }

    [Fact]
    public void Ledger_ScenariosResolveControlIdsAndExecutionDependencies()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var ledger = LoadLedger();
        var controlMatrix = JsonNode.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            ".github",
            "contact-center",
            "pr-test-control-matrix.v1.json")))?.AsObject() ??
            throw new InvalidOperationException("The Contact Center PR-to-test control matrix is invalid.");
        var controlIds = controlMatrix["gates"]?.AsArray()
            .Select(control => control?["id"]?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        // Act & Assert
        foreach (var scenario in ledger["scenarios"]?.AsArray())
        {
            Assert.NotEmpty(scenario?["description"]?.GetValue<string>());
            Assert.Matches("^R[1-8]$", scenario?["implementationPhase"]?.GetValue<string>());
            Assert.Matches("^R[1-8]$", scenario?["certificationPhase"]?.GetValue<string>());
            Assert.NotEmpty(scenario?["currentEvidence"]?.AsArray());
            Assert.NotEmpty(scenario?["blockedBy"]?.AsArray());
            Assert.NotEmpty(scenario?["harnessDependencies"]?.AsArray());
            Assert.StartsWith(
                ".github/contact-center/evidence/",
                scenario?["evidenceTarget"]?.GetValue<string>(),
                StringComparison.Ordinal);

            foreach (var controlId in scenario?["controlIds"]?.AsArray()
                .Select(control => control?.GetValue<string>()))
            {
                Assert.Contains(controlId, controlIds);
            }
        }
    }

    [Fact]
    public void Ledger_DoesNotClaimUncertifiedHarnessesAreCertified()
    {
        // Arrange
        var scenarios = LoadLedger()["scenarios"]?.AsArray();

        // Act & Assert
        Assert.All(scenarios, scenario =>
        {
            var status = scenario?["status"]?.GetValue<string>();

            Assert.True(status is "blocked" or "implemented-uncertified");
            Assert.NotEmpty(scenario?["blockedBy"]?.AsArray());
        });
    }

    private static JsonObject LoadLedger()
    {
        var ledgerPath = Path.Combine(
            FindRepositoryRoot(),
            ".github",
            "contact-center",
            "r0b-harness-dependency-ledger.v1.json");

        return JsonNode.Parse(File.ReadAllText(ledgerPath))?.AsObject() ??
            throw new InvalidOperationException("The Contact Center R0b harness dependency ledger is invalid.");
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
