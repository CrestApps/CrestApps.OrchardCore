using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterSupportMatrixTests
{
    [Fact]
    public void SupportMatrix_DefinesFiniteBlockedGaProfiles()
    {
        // Arrange
        var matrix = LoadMatrix();

        // Act
        var tenantProfiles = matrix["tenantProfiles"]?.AsArray();
        var providerProfiles = matrix["providerProfiles"]?.AsArray();
        var productionDatabases = matrix["databases"]?.AsArray()
            .Where(database => database?["production"]?.GetValue<bool>() == true)
            .ToList();
        var productionTopologies = matrix["topologies"]?.AsArray()
            .Where(topology => topology?["production"]?.GetValue<bool>() == true)
            .ToList();

        // Assert
        Assert.Equal("blocked-until-r0-r8-pass", matrix["releaseStatus"]?.GetValue<string>());
        Assert.NotEmpty(tenantProfiles);
        Assert.NotEmpty(providerProfiles);
        Assert.NotEmpty(productionDatabases);
        Assert.NotEmpty(productionTopologies);
        Assert.Equal(
            tenantProfiles.Count,
            tenantProfiles.Select(profile => profile?["id"]?.GetValue<string>()).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SupportMatrix_GaProfilesReferenceDeclaredProductionDependencies()
    {
        // Arrange
        var matrix = LoadMatrix();
        var providerIds = matrix["providerProfiles"]?.AsArray()
            .Select(profile => profile?["id"]?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var databaseIds = matrix["databases"]?.AsArray()
            .Where(database => database?["production"]?.GetValue<bool>() == true)
            .Select(database => database?["id"]?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var topologyIds = matrix["topologies"]?.AsArray()
            .Where(topology => topology?["production"]?.GetValue<bool>() == true)
            .Select(topology => topology?["id"]?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        // Act & Assert
        foreach (var profile in matrix["tenantProfiles"]?.AsArray())
        {
            Assert.Contains(profile?["providerProfile"]?.GetValue<string>(), providerIds);
            Assert.Contains(profile?["database"]?.GetValue<string>(), databaseIds);
            Assert.Contains(profile?["topology"]?.GetValue<string>(), topologyIds);
            Assert.NotEmpty(profile?["features"]?.AsArray());
        }
    }

    [Fact]
    public void SupportMatrix_ProhibitsUncertifiedHighRiskCapabilities()
    {
        // Arrange
        var matrix = LoadMatrix();
        var prohibitedCombinations = matrix["prohibitedCombinations"]?.AsArray()
            .Select(item => item?.GetValue<string>())
            .ToList();

        // Act & Assert
        Assert.Contains("Power, Progressive, or Predictive dialing", prohibitedCombinations);
        Assert.Contains("recording, monitor, whisper, barge, take-over, or bidirectional media", prohibitedCombinations);
        Assert.Contains("multi-node without a Redis SignalR backplane", prohibitedCombinations);
        Assert.Contains("unlisted feature, provider, database, or topology combinations", prohibitedCombinations);

        foreach (var provider in matrix["providerProfiles"]?.AsArray())
        {
            var prohibitedCapabilities = provider?["prohibitedCapabilities"]?.AsArray()
                .Select(capability => capability?.GetValue<string>())
                .ToHashSet(StringComparer.Ordinal);

            Assert.Contains("predictive-dial", prohibitedCapabilities);
            Assert.Contains("recording", prohibitedCapabilities);
            Assert.Contains("bidirectional-media", prohibitedCapabilities);
        }
    }

    [Fact]
    public void SupportMatrix_TierOneCapacityIsExplicitAndBounded()
    {
        // Arrange
        var matrix = LoadMatrix();
        var capacity = matrix["capacityTier"];

        // Act & Assert
        Assert.Equal("tier-1", capacity?["id"]?.GetValue<string>());
        Assert.Equal(100, capacity?["maxConcurrentSignedInAgentsPerTenant"]?.GetValue<int>());
        Assert.Equal(50, capacity?["maxConcurrentVoiceInteractionsPerTenant"]?.GetValue<int>());
        Assert.Equal(10, capacity?["maxNewInteractionsPerSecondPerTenant"]?.GetValue<int>());
        Assert.True(capacity?["maxTenantsPerDeployment"]?.GetValue<int>() > 0);
    }

    private static JsonObject LoadMatrix()
    {
        var repositoryRoot = FindRepositoryRoot();
        var matrixPath = Path.Combine(
            repositoryRoot,
            ".github",
            "contact-center",
            "support-matrix.v1.json");

        return JsonNode.Parse(File.ReadAllText(matrixPath))?.AsObject() ??
            throw new InvalidOperationException("The Contact Center support matrix is invalid.");
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
