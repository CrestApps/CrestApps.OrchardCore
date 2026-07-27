using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies the topology decision that gates Contact Center work admission.
/// </summary>
/// <remarks>
/// The support matrix states which deployments are supported. This evaluator is what makes that statement
/// enforceable rather than aspirational, so every way a deployment can fall short of its declared topology is
/// exercised here individually. A requirement that is declared but never checked is indistinguishable, at
/// runtime, from a requirement that does not exist.
/// </remarks>
public sealed class ContactCenterTopologyEvaluatorTests
{
    [Fact]
    public void Evaluate_Throws_WhenObservationsAreMissing()
    {
        Assert.Throws<ArgumentNullException>(() => ContactCenterTopologyEvaluator.Evaluate(null));
    }

    [Fact]
    public void Evaluate_IsSatisfied_WhenNoProfileIsDeclaredOutsideProduction()
    {
        // Development, tests, and demos declare nothing. Requiring a declaration everywhere would make the
        // default experience fail closed for deployments that never claimed support in the first place.
        var result = ContactCenterTopologyEvaluator.Evaluate(new ContactCenterTopologyObservations
        {
            DeclaredProfileId = null,
            IsProductionHostEnvironment = false,
        });

        Assert.True(result.IsSatisfied);
        Assert.Null(result.DeclaredProfileId);
        Assert.False(result.IsProductionTopology);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_Fails_WhenNoProfileIsDeclaredInProduction(string declaredProfileId)
    {
        // Without this branch the entire validator is bypassed by omitting one configuration key, which is the
        // single most likely way an operator reaches production on an unchecked deployment.
        var result = ContactCenterTopologyEvaluator.Evaluate(new ContactCenterTopologyObservations
        {
            DeclaredProfileId = declaredProfileId,
            IsProductionHostEnvironment = true,
        });

        Assert.False(result.IsSatisfied);
        Assert.Contains(result.Failures, failure => failure.Contains("CrestApps_ContactCenter:Topology:ProfileId", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains(ContactCenterTopologyProfiles.SingleNodeDistributedId, StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_Fails_WhenTheDeclaredProfileIsNotRecognized()
    {
        // A typo must surface, not silently select the profile with no requirements.
        var result = ContactCenterTopologyEvaluator.Evaluate(new ContactCenterTopologyObservations
        {
            DeclaredProfileId = "single-node-distrubuted",
            IsProductionHostEnvironment = true,
        });

        Assert.False(result.IsSatisfied);
        Assert.False(result.IsProductionTopology);
        Assert.Contains(result.Failures, failure => failure.Contains("is not recognized", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains(ContactCenterTopologyProfiles.SingleNodeDistributedId, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ContactCenterTopologyProfiles.SingleNodeDevelopmentId)]
    [InlineData(ContactCenterTopologyProfiles.SingleRegionMultiNodeId)]
    public void Evaluate_ImposesNoInfrastructureRequirements_OnNonProductionTopologies(string profileId)
    {
        // Declaring a non-production topology is a statement that the deployment is not claiming support. That
        // statement is always internally consistent, so it cannot fail.
        var result = ContactCenterTopologyEvaluator.Evaluate(new ContactCenterTopologyObservations
        {
            DeclaredProfileId = profileId,
            IsProductionHostEnvironment = true,
            DatabaseProvider = "Sqlite",
            RedisFeatureEnabled = false,
            RedisLockFeatureEnabled = false,
            SignalRRedisBackplaneFeatureEnabled = false,
            DistributedLockIsProcessLocal = true,
        });

        Assert.True(result.IsSatisfied);
        Assert.False(result.IsProductionTopology);
        Assert.Equal(profileId, result.DeclaredProfileId);
    }

    [Fact]
    public void Evaluate_IsSatisfied_WhenTheProductionTopologyIsFullyMet()
    {
        var result = ContactCenterTopologyEvaluator.Evaluate(CreateSatisfiedProductionObservations());

        Assert.True(result.IsSatisfied);
        Assert.True(result.IsProductionTopology);
        Assert.Equal(ContactCenterTopologyProfiles.SingleNodeDistributedId, result.DeclaredProfileId);
        Assert.Empty(result.Failures);
    }

    [Theory]
    [InlineData("single-node-distributed")]
    [InlineData("Single-Node-Distributed")]
    [InlineData("  single-node-distributed  ")]
    public void Evaluate_ResolvesTheDeclaredProfile_IgnoringCaseAndSurroundingWhitespace(string declaredProfileId)
    {
        // Configuration values arrive from environment variables and JSON files edited by hand. Rejecting a
        // correct topology over casing would push operators toward removing the declaration entirely.
        var observations = CreateSatisfiedProductionObservations(observations => observations.DeclaredProfileId = declaredProfileId);

        var result = ContactCenterTopologyEvaluator.Evaluate(observations);

        Assert.True(result.IsSatisfied);
        Assert.Equal(ContactCenterTopologyProfiles.SingleNodeDistributedId, result.DeclaredProfileId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Sqlite")]
    [InlineData("SqlConnection")]
    [InlineData("MySql")]
    public void Evaluate_Fails_WhenTheProductionDatabaseProviderIsNotPostgres(string databaseProvider)
    {
        var observations = CreateSatisfiedProductionObservations(o => o.DatabaseProvider = databaseProvider);

        var result = ContactCenterTopologyEvaluator.Evaluate(observations);

        Assert.False(result.IsSatisfied);
        Assert.Contains(result.Failures, failure => failure.Contains("database provider", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_AcceptsThePostgresProvider_RegardlessOfCasing()
    {
        var observations = CreateSatisfiedProductionObservations(o => o.DatabaseProvider = "postgres");

        var result = ContactCenterTopologyEvaluator.Evaluate(observations);

        Assert.True(result.IsSatisfied);
    }

    [Fact]
    public void Evaluate_Fails_WhenTheRedisFeatureIsDisabled()
    {
        var observations = CreateSatisfiedProductionObservations(o => o.RedisFeatureEnabled = false);

        var result = ContactCenterTopologyEvaluator.Evaluate(observations);

        Assert.False(result.IsSatisfied);
        Assert.Contains(result.Failures, failure => failure.Contains(ContactCenterTopologyEvaluator.RedisFeatureId, StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_ReportsTheMissingRedisFeatureOnce_RatherThanAsEveryDerivedFailure()
    {
        // Both Redis-backed requirements resolve their connection through the same base feature. Reporting the
        // base failure twice would bury the actual cause under its own consequences.
        var observations = CreateSatisfiedProductionObservations(o => o.RedisFeatureEnabled = false);

        var result = ContactCenterTopologyEvaluator.Evaluate(observations);

        var redisFeatureFailures = result.Failures.Count(failure =>
            failure.Contains($"'{ContactCenterTopologyEvaluator.RedisFeatureId}'", StringComparison.Ordinal));

        Assert.Equal(1, redisFeatureFailures);
    }

    [Fact]
    public void Evaluate_Fails_WhenTheRedisLockFeatureIsDisabled()
    {
        var observations = CreateSatisfiedProductionObservations(o => o.RedisLockFeatureEnabled = false);

        var result = ContactCenterTopologyEvaluator.Evaluate(observations);

        Assert.False(result.IsSatisfied);
        Assert.Contains(result.Failures, failure => failure.Contains(ContactCenterTopologyEvaluator.RedisLockFeatureId, StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_Fails_WhenTheResolvedLockIsProcessLocal()
    {
        // The enabled feature list can say Redis locking is on while the container still hands out the local
        // implementation. The lock that is actually injected is the one that decides whether two overlapping
        // processes can enter the same critical section, so that is what is checked.
        var observations = CreateSatisfiedProductionObservations(o => o.DistributedLockIsProcessLocal = true);

        var result = ContactCenterTopologyEvaluator.Evaluate(observations);

        Assert.False(result.IsSatisfied);
        Assert.Contains(result.Failures, failure => failure.Contains("process-local", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_Fails_WhenTheSignalRRedisBackplaneFeatureIsDisabled()
    {
        var observations = CreateSatisfiedProductionObservations(o => o.SignalRRedisBackplaneFeatureEnabled = false);

        var result = ContactCenterTopologyEvaluator.Evaluate(observations);

        Assert.False(result.IsSatisfied);
        Assert.Contains(result.Failures, failure => failure.Contains(ContactCenterTopologyEvaluator.SignalRRedisBackplaneFeatureId, StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_ReportsEveryUnmetRequirement_RatherThanStoppingAtTheFirst()
    {
        // An operator fixing one requirement per deployment is the slowest possible path to a supported
        // configuration, and each intermediate deployment is another unsupported production release.
        var observations = new ContactCenterTopologyObservations
        {
            DeclaredProfileId = ContactCenterTopologyProfiles.SingleNodeDistributedId,
            IsProductionHostEnvironment = true,
            DatabaseProvider = "Sqlite",
            RedisFeatureEnabled = false,
            RedisLockFeatureEnabled = false,
            SignalRRedisBackplaneFeatureEnabled = false,
            DistributedLockIsProcessLocal = true,
        };

        var result = ContactCenterTopologyEvaluator.Evaluate(observations);

        Assert.False(result.IsSatisfied);
        Assert.Contains(result.Failures, failure => failure.Contains("database provider", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Failures, failure => failure.Contains($"'{ContactCenterTopologyEvaluator.RedisFeatureId}'", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains($"'{ContactCenterTopologyEvaluator.RedisLockFeatureId}'", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains($"'{ContactCenterTopologyEvaluator.SignalRRedisBackplaneFeatureId}'", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("process-local", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_TreatsTheProductionTopologyAsProduction_EvenWhenItIsUnsatisfied()
    {
        // The verdict has to keep reporting which topology was claimed, otherwise an operator reading the
        // failure cannot tell which contract they failed to meet.
        var observations = CreateSatisfiedProductionObservations(o => o.RedisFeatureEnabled = false);

        var result = ContactCenterTopologyEvaluator.Evaluate(observations);

        Assert.True(result.IsProductionTopology);
        Assert.Equal(ContactCenterTopologyProfiles.SingleNodeDistributedId, result.DeclaredProfileId);
    }

    [Fact]
    public void Profiles_ResolveByIdentifier_AndRejectUnknownIdentifiers()
    {
        Assert.NotNull(ContactCenterTopologyProfiles.Find(ContactCenterTopologyProfiles.SingleNodeDistributedId));
        Assert.NotNull(ContactCenterTopologyProfiles.Find(ContactCenterTopologyProfiles.SingleRegionMultiNodeId));
        Assert.NotNull(ContactCenterTopologyProfiles.Find(ContactCenterTopologyProfiles.SingleNodeDevelopmentId));
        Assert.Null(ContactCenterTopologyProfiles.Find("does-not-exist"));
        Assert.Null(ContactCenterTopologyProfiles.Find(null));
        Assert.Null(ContactCenterTopologyProfiles.Find("   "));
    }

    [Fact]
    public void Profiles_DeclareExactlyOneProductionTopology()
    {
        // The whole point of this release is that one topology, and only one, is certified. Adding a second
        // production profile in code without earning it must fail here.
        var productionProfiles = ContactCenterTopologyProfiles.All.Where(profile => profile.IsProduction);

        Assert.Equal(
            [ContactCenterTopologyProfiles.SingleNodeDistributedId],
            productionProfiles.Select(profile => profile.Id));
    }

    [Fact]
    public void Profiles_RequireEveryDistributedComponent_ForEveryProductionTopology()
    {
        // A production topology that waives one of these requirements would silently reintroduce the
        // single-process assumptions this release exists to remove.
        foreach (var profile in ContactCenterTopologyProfiles.All.Where(candidate => candidate.IsProduction))
        {
            Assert.True(profile.RequiresRedisBackplane, $"'{profile.Id}' must require the Redis backplane.");
            Assert.True(profile.RequiresRedisDistributedLock, $"'{profile.Id}' must require Redis distributed locking.");
            Assert.True(profile.RequiresSharedRelationalDatabase, $"'{profile.Id}' must require a shared relational database.");
        }
    }

    private static ContactCenterTopologyObservations CreateSatisfiedProductionObservations(
        Action<MutableObservations> configure = null)
    {
        var mutable = new MutableObservations
        {
            DeclaredProfileId = ContactCenterTopologyProfiles.SingleNodeDistributedId,
            IsProductionHostEnvironment = true,
            DatabaseProvider = ContactCenterTopologyEvaluator.RequiredProductionDatabaseProvider,
            RedisFeatureEnabled = true,
            RedisLockFeatureEnabled = true,
            SignalRRedisBackplaneFeatureEnabled = true,
            DistributedLockIsProcessLocal = false,
        };

        configure?.Invoke(mutable);

        return mutable.ToObservations();
    }

    private sealed class MutableObservations
    {
        public string DeclaredProfileId { get; set; }

        public bool IsProductionHostEnvironment { get; set; }

        public string DatabaseProvider { get; set; }

        public bool RedisFeatureEnabled { get; set; }

        public bool RedisLockFeatureEnabled { get; set; }

        public bool SignalRRedisBackplaneFeatureEnabled { get; set; }

        public bool DistributedLockIsProcessLocal { get; set; }

        public ContactCenterTopologyObservations ToObservations()
            => new()
            {
                DeclaredProfileId = DeclaredProfileId,
                IsProductionHostEnvironment = IsProductionHostEnvironment,
                DatabaseProvider = DatabaseProvider,
                RedisFeatureEnabled = RedisFeatureEnabled,
                RedisLockFeatureEnabled = RedisLockFeatureEnabled,
                SignalRRedisBackplaneFeatureEnabled = SignalRRedisBackplaneFeatureEnabled,
                DistributedLockIsProcessLocal = DistributedLockIsProcessLocal,
            };
    }
}
