using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterPrTestControlMatrixTests
{
    private static readonly IReadOnlyDictionary<string, int> ExpectedGateCountByPrefix = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["C"] = 8,
        ["D"] = 9,
        ["F"] = 6,
        ["O"] = 6,
        ["S"] = 5,
        ["T"] = 3,
        ["V"] = 4,
    };

    [Fact]
    public void ControlMatrix_CoversEveryCurrentP0AndP1FindingWithNoDuplicates()
    {
        // Arrange
        var matrix = LoadMatrix();
        var gates = matrix["gates"]?.AsArray() ??
            throw new InvalidOperationException("The Contact Center PR-to-test control matrix must define gates.");

        // Act
        var ids = gates
            .Select(gate => gate?["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
        var distinctIds = ids.Distinct(StringComparer.Ordinal).ToList();

        // Assert
        Assert.Equal("blocked-until-r0-r8-pass", matrix["releaseStatus"]?.GetValue<string>());
        Assert.Equal(41, gates.Count);
        Assert.Equal(ids.Count, distinctIds.Count);

        foreach (var (prefix, expectedCount) in ExpectedGateCountByPrefix)
        {
            var actualCount = ids.Count(id => id!.StartsWith(prefix, StringComparison.Ordinal));

            Assert.True(
                actualCount == expectedCount,
                $"Expected {expectedCount} gates with the '{prefix}' prefix, but found {actualCount}.");
        }
    }

    [Fact]
    public void ControlMatrix_EveryGateDeclaresSeverityAndUsesARegisteredCategory()
    {
        // Arrange
        var matrix = LoadMatrix();
        var gates = matrix["gates"]?.AsArray();
        var registeredCategories = matrix["categories"]?.AsArray()
            .Select(category => category?["id"]?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        // Act & Assert
        Assert.NotEmpty(registeredCategories);

        foreach (var gate in gates!)
        {
            var gateId = gate?["id"]?.GetValue<string>() ??
                throw new InvalidOperationException("Every Contact Center control-matrix gate must have an id.");
            var severity = gate?["severity"]?.GetValue<string>();
            var category = gate?["category"]?.GetValue<string>();
            var registeredCategory = matrix["categories"]?.AsArray()
                .Single(registeredCategory => registeredCategory?["id"]?.GetValue<string>() == category);
            var categoryPrefix = registeredCategory?["prefix"]?.GetValue<string>() ??
                throw new InvalidOperationException($"Gate '{gateId}' category must have a prefix.");

            Assert.True(
                severity is "P0" or "P1",
                $"Gate '{gateId}' must be designated P0 or P1, but found '{severity}'.");
            Assert.Contains(category, registeredCategories);
            Assert.StartsWith(
                categoryPrefix,
                gateId,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ControlMatrix_EveryGateResolvesOwnershipAndExecutionContext()
    {
        // Arrange
        var matrix = LoadMatrix();
        var gates = matrix["gates"]?.AsArray();
        var categoriesById = matrix["categories"]?.AsArray()
            .ToDictionary(category => category?["id"]?.GetValue<string>() ?? string.Empty, StringComparer.Ordinal);

        // Act & Assert
        foreach (var gate in gates!)
        {
            var gateId = gate?["id"]?.GetValue<string>();
            var category = categoriesById![gate?["category"]?.GetValue<string>() ?? string.Empty];

            Assert.False(string.IsNullOrWhiteSpace(category?["driRole"]?.GetValue<string>()), $"Gate '{gateId}' category has no DRI role.");
            Assert.NotEmpty(category?["approverRoles"]?.AsArray());

            var providers = gate?["context"]?["providers"]?.AsArray();
            var databases = gate?["context"]?["databases"]?.AsArray();
            var topologies = gate?["context"]?["topologies"]?.AsArray();

            Assert.NotEmpty(providers);
            Assert.NotEmpty(databases);
            Assert.NotEmpty(topologies);
        }
    }

    [Fact]
    public void ControlMatrix_EveryGateHasAFalsifiableInvariantAStableTestIdAndRetainedEvidence()
    {
        // Arrange
        var matrix = LoadMatrix();
        var gates = matrix["gates"]?.AsArray();

        // Act
        var testIds = gates!
            .Select(gate => gate?["testId"]?.GetValue<string>())
            .ToList();

        // Assert
        Assert.Equal(testIds.Count, testIds.Distinct(StringComparer.Ordinal).Count());

        foreach (var gate in gates!)
        {
            var gateId = gate?["id"]?.GetValue<string>();

            Assert.False(string.IsNullOrWhiteSpace(gate?["planFinding"]?.GetValue<string>()), $"Gate '{gateId}' must cite its source plan finding.");
            Assert.False(string.IsNullOrWhiteSpace(gate?["gate"]?.GetValue<string>()), $"Gate '{gateId}' must have a title.");
            Assert.False(string.IsNullOrWhiteSpace(gate?["invariant"]?.GetValue<string>()), $"Gate '{gateId}' must have a falsifiable invariant.");
            Assert.False(string.IsNullOrWhiteSpace(gate?["testId"]?.GetValue<string>()), $"Gate '{gateId}' must resolve to a test id.");
            Assert.False(string.IsNullOrWhiteSpace(gate?["evidenceLocation"]?.GetValue<string>()), $"Gate '{gateId}' must resolve to a retained evidence location.");

            var ciJob = gate?["ciJob"];

            Assert.False(string.IsNullOrWhiteSpace(ciJob?["id"]?.GetValue<string>()), $"Gate '{gateId}' must resolve to a CI job id.");
            Assert.False(string.IsNullOrWhiteSpace(ciJob?["workflow"]?.GetValue<string>()), $"Gate '{gateId}' must resolve to a CI workflow.");

            var status = ciJob?["status"]?.GetValue<string>();

            Assert.True(
                status is "implemented" or "partial" or "planned",
                $"Gate '{gateId}' CI job status must be 'implemented', 'partial', or 'planned', but found '{status}'.");
        }
    }

    [Theory]
    [InlineData("S001", "P0", "Global supervisor SignalR group")]
    [InlineData("S002", "P0", "Agents self-enroll arbitrary queues/campaigns")]
    [InlineData("S003", "P0", "Asterisk logs credentials in URI")]
    [InlineData("S004", "P1", "Stored subject reaches innerHTML")]
    [InlineData("S005", "P0", "Asterisk dashboard anonymous destructive controls")]
    [InlineData("C001", "P0", "Recording state changes without media execution")]
    [InlineData("C002", "P0", "Monitor/whisper/barge/take-over can report success without execution")]
    [InlineData("C003", "P0", "Session loss during wrap-up permanently consumes capacity")]
    [InlineData("C004", "P0", "Completed-handler identity unstable across deployment")]
    [InlineData("C005", "P0", "Ambiguous inbound matches attributed to first contact")]
    [InlineData("C006", "P0", "Calling-window gate insufficient for regulated automated dialing")]
    [InlineData("C007", "P1", "Raw inbound-event endpoint duplicates ingress and broad privilege")]
    [InlineData("C008", "P1", "Capability flags exceed executable contracts")]
    [InlineData("D001", "P0", "Disconnected agents remain routable")]
    [InlineData("D002", "P0", "Provider call outlives failed reservation acceptance")]
    [InlineData("D003", "P0", "Provider event dedupe check-then-insert")]
    [InlineData("D004", "P1", "Distributed locks lack DB CAS")]
    [InlineData("D005", "P1", "Hot-query indexes mismatch predicates")]
    [InlineData("D006", "P1", "Routing/live metrics scale linearly")]
    [InlineData("D007", "P1", "Metrics projections not idempotent/rebuildable")]
    [InlineData("D008", "P1", "Callback promotion duplicate-prone/unbounded")]
    [InlineData("D009", "P1", "Provider reconciliation unbounded/overlaps")]
    [InlineData("F001", "P0", "Base coupled to Omnichannel Managements UI")]
    [InlineData("F002", "P0", "Queues/Voice/RealTime/SoftPhone dependencies inconsistent")]
    [InlineData("F003", "P1", "Workflow bridge not independently feature-gated")]
    [InlineData("F004", "P1", "Provider modules depend on implementation/Asterisk integrations ungated")]
    [InlineData("F005", "P1", "Omnichannel Managements dynamically resolves optional CC services")]
    [InlineData("F006", "P1", "Post-commit uses IServiceProvider/ad hoc child scopes")]
    [InlineData("O001", "P1", "No production health/OpenTelemetry")]
    [InlineData("O002", "P0", "Multi-node SignalR not configured/proven")]
    [InlineData("O003", "P0", "Logs contain raw customer/agent IDs")]
    [InlineData("O004", "P0", "Asterisk sample host unsafe")]
    [InlineData("O005", "P1", "CI omits browser/provider/feature/performance")]
    [InlineData("O006", "P1", "Retention/erasure/rolling-upgrade incomplete")]
    [InlineData("T001", "P0", "No Orchard tenant feature matrix")]
    [InlineData("T002", "P1", "No Orchard Contact Center browser E2E")]
    [InlineData("T003", "P0", "No repeatable multi-node/load/chaos gate")]
    [InlineData("V001", "P0", "Asterisk listeners split-brain/stale ordering")]
    [InlineData("V002", "P1", "Client disconnect cancels PBX commands")]
    [InlineData("V003", "P1", "Webhook buffering/request abort coupling")]
    [InlineData("V004", "P1", "RTP secure boundary/jitter/reorder missing")]
    public void ControlMatrix_EveryCurrentP0OrP1FindingIsPresentWithItsExpectedSeverityAndTitle(string gateId, string expectedSeverity, string expectedTitle)
    {
        // Arrange
        var matrix = LoadMatrix();
        var gates = matrix["gates"]?.AsArray();

        // Act
        var gate = gates!.SingleOrDefault(gate => gate?["id"]?.GetValue<string>() == gateId) ??
            throw new InvalidOperationException($"Gate '{gateId}' is missing from the control matrix.");

        // Assert
        Assert.Equal(expectedSeverity, gate["severity"]?.GetValue<string>());
        Assert.Equal(expectedTitle, gate["gate"]?.GetValue<string>());
    }

    [Fact]
    public void ControlMatrix_AtLeastOneP0GateIsAlreadyEnforcedOrPartiallyEnforced()
    {
        // Arrange
        var matrix = LoadMatrix();
        var gates = matrix["gates"]?.AsArray();

        // Act
        var enforcedP0Gates = gates!
            .Where(gate => gate?["severity"]?.GetValue<string>() == "P0")
            .Where(gate => gate?["ciJob"]?["status"]?.GetValue<string>() is "implemented" or "partial")
            .ToList();

        // Assert
        Assert.NotEmpty(enforcedP0Gates);
    }

    private static JsonObject LoadMatrix()
    {
        var repositoryRoot = FindRepositoryRoot();
        var matrixPath = Path.Combine(
            repositoryRoot,
            ".github",
            "contact-center",
            "pr-test-control-matrix.v1.json");

        return JsonNode.Parse(File.ReadAllText(matrixPath))?.AsObject() ??
            throw new InvalidOperationException("The Contact Center PR-to-test control matrix is invalid.");
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
