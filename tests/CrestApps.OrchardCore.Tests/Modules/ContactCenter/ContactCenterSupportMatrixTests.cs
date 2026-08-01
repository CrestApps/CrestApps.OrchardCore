using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.DialPad;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterSupportMatrixTests
{
    private const string SingleNodeDistributedTopologyId = "single-node-distributed";

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
        Assert.Contains("recording, monitor, whisper, barge, or bidirectional media", prohibitedCombinations);
        Assert.Contains("multi-node without a Redis SignalR backplane", prohibitedCombinations);
        Assert.Contains("multi-node without OrchardCore.Redis.Lock distributed locking", prohibitedCombinations);
        Assert.Contains("unlisted feature, provider, database, or topology combinations", prohibitedCombinations);

        foreach (var topology in matrix["topologies"]?.AsArray()
            .Where(topology => topology?["production"]?.GetValue<bool>() == true))
        {
            Assert.True(topology?["redisBackplaneRequired"]?.GetValue<bool>());
            Assert.True(topology?["redisDistributedLockRequired"]?.GetValue<bool>());
        }

        foreach (var provider in matrix["providerProfiles"]?.AsArray())
        {
            var prohibitedCapabilities = provider?["prohibitedCapabilities"]?.AsArray()
                .Select(capability => capability?.GetValue<string>())
                .ToHashSet(StringComparer.Ordinal);

            Assert.Contains("predictive-dial", prohibitedCapabilities);
            Assert.Contains("recording", prohibitedCapabilities);
            Assert.Contains("bidirectional-media", prohibitedCapabilities);
        }

        foreach (var profile in matrix["tenantProfiles"]?.AsArray())
        {
            var features = profile?["features"]?.AsArray()
                .Select(feature => feature?.GetValue<string>())
                .ToHashSet(StringComparer.Ordinal);

            Assert.DoesNotContain("CrestApps.OrchardCore.ContactCenter.Voice.Media", features);
            Assert.DoesNotContain("CrestApps.OrchardCore.Asterisk.ContactCenterMedia", features);
        }
    }

    [Fact]
    public void VoiceProviderCapabilities_DoNotExposeUncertifiedLegacyMediaFlag()
    {
        // Act
        var capabilityNames = Enum.GetNames<ContactCenterVoiceProviderCapabilities>();

        // Assert
        Assert.DoesNotContain("BidirectionalMedia", capabilityNames);
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

    [Fact]
    public void SupportMatrix_GaProfilesReferenceCurrentProviderAdapterFeatures()
    {
        // Arrange
        var matrix = LoadMatrix();
        var profiles = matrix["tenantProfiles"]?.AsArray()
            .ToDictionary(
                profile => profile?["id"]?.GetValue<string>() ?? string.Empty,
                profile => profile?["features"]?.AsArray()
                    .Select(feature => feature?.GetValue<string>())
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        // Act & Assert
        Assert.Contains(AsteriskConstants.Feature.ContactCenterVoice, profiles["ga-core-asterisk"]);
        Assert.Contains(DialPadConstants.Feature.ContactCenterVoice, profiles["ga-core-dialpad"]);
    }

    /// <summary>
    /// Verifies the production topology this release earns exists and carries the distributed requirements that define it.
    /// </summary>
    [Fact]
    public void SupportMatrix_DeclaresSingleNodeDistributedAsAProductionTopology()
    {
        // Arrange
        var matrix = LoadMatrix();

        // Act
        var topology = Assert.Single(
            matrix["topologies"]?.AsArray()
                .Where(candidate => string.Equals(candidate?["id"]?.GetValue<string>(), SingleNodeDistributedTopologyId, StringComparison.Ordinal)));

        // Assert
        Assert.True(topology?["production"]?.GetValue<bool>());
        Assert.Equal(1, topology?["minimumApplicationNodes"]?.GetValue<int>());
        Assert.Equal(1, topology?["maximumApplicationNodes"]?.GetValue<int>());
        Assert.True(topology?["redisBackplaneRequired"]?.GetValue<bool>());
        Assert.True(topology?["redisDistributedLockRequired"]?.GetValue<bool>());
        Assert.True(topology?["sharedRelationalDatabaseRequired"]?.GetValue<bool>());
    }

    /// <summary>
    /// Verifies no declared production topology contradicts a prohibited combination. A matrix that supports what it also
    /// prohibits is worse than one that does neither, because both statements read as authoritative to an operator.
    /// </summary>
    [Fact]
    public void SupportMatrix_ProductionTopologiesDoNotContradictProhibitedCombinations()
    {
        // Arrange
        var matrix = LoadMatrix();
        var prohibitedCombinations = matrix["prohibitedCombinations"]?.AsArray()
            .Select(item => item?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var productionTopologies = matrix["topologies"]?.AsArray()
            .Where(topology => topology?["production"]?.GetValue<bool>() == true);

        // Assert
        Assert.Contains("production with more than one application node", prohibitedCombinations);
        Assert.Contains(
            "production with a single application node without Redis distributed locking and a Redis SignalR backplane",
            prohibitedCombinations);

        // The blanket single-node prohibition is what this release replaces; leaving it would prohibit the profile it earns.
        Assert.DoesNotContain("production with a single application node", prohibitedCombinations);

        foreach (var topology in productionTopologies)
        {
            var id = topology?["id"]?.GetValue<string>();

            // Consequence of prohibiting production on more than one application node.
            Assert.Equal(1, topology?["minimumApplicationNodes"]?.GetValue<int>());
            Assert.Equal(1, topology?["maximumApplicationNodes"]?.GetValue<int>());

            // Consequence of prohibiting a production node that does not run the distributed contract.
            Assert.True(topology?["redisBackplaneRequired"]?.GetValue<bool>(), $"'{id}' is production without a Redis backplane.");
            Assert.True(topology?["redisDistributedLockRequired"]?.GetValue<bool>(), $"'{id}' is production without Redis distributed locking.");
            Assert.True(topology?["sharedRelationalDatabaseRequired"]?.GetValue<bool>(), $"'{id}' is production without a shared relational database.");
        }
    }

    /// <summary>
    /// Verifies production status is not silently widened. Every supported tenant profile must run the one certified topology.
    /// </summary>
    [Fact]
    public void SupportMatrix_SupportedTenantProfilesRunTheCertifiedTopology()
    {
        // Arrange
        var matrix = LoadMatrix();

        // Act
        var productionTopologyIds = matrix["topologies"]?.AsArray()
            .Where(topology => topology?["production"]?.GetValue<bool>() == true)
            .Select(topology => topology?["id"]?.GetValue<string>())
            .ToList();

        // Assert
        Assert.Equal([SingleNodeDistributedTopologyId], productionTopologyIds);

        foreach (var profile in matrix["tenantProfiles"]?.AsArray())
        {
            Assert.Equal(SingleNodeDistributedTopologyId, profile?["topology"]?.GetValue<string>());
        }
    }

    [Fact]
    public void ShippedTopologyProfiles_AreIdenticalToTheSupportMatrix()
    {
        // The support matrix is a governance document that is not deployed with the product, so the running
        // application enforces the shipped copy in ContactCenterTopologyProfiles. If the two are allowed to
        // drift, the product enforces a second, private definition of "production" that nobody reviewed.
        var matrix = LoadMatrix();

        var declared = matrix["topologies"]?.AsArray()
            .Select(topology => new
            {
                Id = topology?["id"]?.GetValue<string>(),
                Production = topology?["production"]?.GetValue<bool>(),
                Minimum = topology?["minimumApplicationNodes"]?.GetValue<int>(),
                Maximum = topology?["maximumApplicationNodes"]?.GetValue<int>(),
                Backplane = topology?["redisBackplaneRequired"]?.GetValue<bool>(),
                Lock = topology?["redisDistributedLockRequired"]?.GetValue<bool>(),
                Database = topology?["sharedRelationalDatabaseRequired"]?.GetValue<bool>(),
            })
            .OrderBy(topology => topology.Id, StringComparer.Ordinal);

        var shipped = ContactCenterTopologyProfiles.All
            .Select(profile => new
            {
                Id = profile.Id,
                Production = (bool?)profile.IsProduction,
                Minimum = (int?)profile.MinimumApplicationNodes,
                Maximum = (int?)profile.MaximumApplicationNodes,
                Backplane = (bool?)profile.RequiresRedisBackplane,
                Lock = (bool?)profile.RequiresRedisDistributedLock,
                Database = (bool?)profile.RequiresSharedRelationalDatabase,
            })
            .OrderBy(profile => profile.Id, StringComparer.Ordinal);

        Assert.Equal(declared, shipped);
    }

    [Fact]
    public void ShippedProductionDatabaseProvider_MatchesTheOnlyProductionDatabaseInTheSupportMatrix()
    {
        // The evaluator compares the tenant's configured Orchard provider against a literal, because the pure
        // decision layer carries no data-layer dependency. This test is what keeps that literal honest.
        var matrix = LoadMatrix();

        var productionDatabaseIds = matrix["databases"]?.AsArray()
            .Where(database => database?["production"]?.GetValue<bool>() == true)
            .Select(database => database?["id"]?.GetValue<string>());

        Assert.Equal(["postgresql-16"], productionDatabaseIds);
        Assert.Equal("Postgres", ContactCenterTopologyEvaluator.RequiredProductionDatabaseProvider);
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
