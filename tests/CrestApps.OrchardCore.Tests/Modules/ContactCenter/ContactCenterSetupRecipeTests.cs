using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Deployments;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterSetupRecipeTests
{
    private const string FeatureStepName = "feature";

    private static readonly Dictionary<string, string> _recipeToTenantProfile = new(StringComparer.Ordinal)
    {
        ["contact-center-asterisk-ga-core.recipe.json"] = "ga-core-asterisk",
        ["contact-center-dialpad-ga-core.recipe.json"] = "ga-core-dialpad",
    };

    private static readonly Dictionary<string, string[]> _tenantProfileFeatures = new(StringComparer.Ordinal)
    {
        ["ga-core-asterisk"] =
        [
            "CrestApps.OrchardCore.ContactCenter",
            "CrestApps.OrchardCore.ContactCenter.Agents",
            "CrestApps.OrchardCore.ContactCenter.Queues",
            "CrestApps.OrchardCore.ContactCenter.InboundVoice",
            "CrestApps.OrchardCore.Telephony.SoftPhone",
            "CrestApps.OrchardCore.ContactCenter.Dialer",
            "CrestApps.OrchardCore.Asterisk",
            "CrestApps.OrchardCore.Asterisk.ContactCenterVoice",
        ],
        ["ga-core-dialpad"] =
        [
            "CrestApps.OrchardCore.ContactCenter",
            "CrestApps.OrchardCore.ContactCenter.Agents",
            "CrestApps.OrchardCore.ContactCenter.Queues",
            "CrestApps.OrchardCore.ContactCenter.InboundVoice",
            "CrestApps.OrchardCore.Telephony.SoftPhone",
            "CrestApps.OrchardCore.ContactCenter.Dialer",
            "CrestApps.OrchardCore.Dialpad",
            "CrestApps.OrchardCore.Dialpad.ContactCenterVoice",
        ],
    };

    private static readonly HashSet<string> _knownStepNames = new(StringComparer.Ordinal)
    {
        FeatureStepName,
        ContactCenterDeploymentSteps.Skill,
        ContactCenterDeploymentSteps.QueueGroup,
        ContactCenterDeploymentSteps.BusinessHoursCalendar,
        ContactCenterDeploymentSteps.Queue,
        ContactCenterDeploymentSteps.EntryPoint,
        ContactCenterDeploymentSteps.DialerProfile,
        ContactCenterDeploymentSteps.AgentStateReasonCode,
    };

    [Theory]
    [InlineData("contact-center-asterisk-ga-core.recipe.json")]
    [InlineData("contact-center-dialpad-ga-core.recipe.json")]
    public void SetupRecipe_IsWellFormedAndHarvestable(string recipeFileName)
    {
        // Arrange
        var recipe = LoadRecipe(recipeFileName);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(recipe["name"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(recipe["displayName"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(recipe["description"]?.GetValue<string>()));

        var steps = recipe["steps"]?.AsArray();

        Assert.NotNull(steps);
        Assert.NotEmpty(steps);
    }

    [Theory]
    [InlineData("contact-center-asterisk-ga-core.recipe.json")]
    [InlineData("contact-center-dialpad-ga-core.recipe.json")]
    public void SetupRecipe_ReferencesOnlyRegisteredSteps(string recipeFileName)
    {
        // Arrange
        var recipe = LoadRecipe(recipeFileName);

        // Act
        var stepNames = recipe["steps"]?.AsArray()
            .Select(step => step?["name"]?.GetValue<string>())
            .ToList();

        // Assert
        Assert.NotNull(stepNames);

        foreach (var stepName in stepNames)
        {
            Assert.Contains(stepName, _knownStepNames);
        }
    }

    [Theory]
    [InlineData("contact-center-asterisk-ga-core.recipe.json")]
    [InlineData("contact-center-dialpad-ga-core.recipe.json")]
    public void SetupRecipe_EnablesExactlyItsSupportedTenantProfileFeatureSet(string recipeFileName)
    {
        // Arrange
        var recipe = LoadRecipe(recipeFileName);
        var tenantProfileId = _recipeToTenantProfile[recipeFileName];
        var expectedFeatures = LoadTenantProfileFeatures(tenantProfileId);

        // Act
        var featureStep = recipe["steps"]?.AsArray()
            .SingleOrDefault(step => string.Equals(step?["name"]?.GetValue<string>(), FeatureStepName, StringComparison.Ordinal));

        var enabledFeatures = featureStep?["enable"]?.AsArray()
            .Select(feature => feature?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.NotNull(featureStep);
        Assert.NotNull(enabledFeatures);
        Assert.True(
            expectedFeatures.SetEquals(enabledFeatures),
            $"The '{recipeFileName}' feature step must enable exactly the '{tenantProfileId}' tenant profile feature set from the support matrix. " +
            $"Missing: [{string.Join(", ", expectedFeatures.Except(enabledFeatures))}]. " +
            $"Unexpected: [{string.Join(", ", enabledFeatures.Except(expectedFeatures))}].");
    }

    [Fact]
    public void EverySupportedTenantProfile_HasAMatchingSetupRecipe()
    {
        // Assert
        foreach (var tenantProfileId in _tenantProfileFeatures.Keys)
        {
            Assert.Contains(tenantProfileId, _recipeToTenantProfile.Values);
        }
    }

    private static HashSet<string> LoadTenantProfileFeatures(string tenantProfileId)
    {
        return _tenantProfileFeatures.TryGetValue(tenantProfileId, out var features)
            ? new HashSet<string>(features, StringComparer.Ordinal)
            : throw new InvalidOperationException($"The tenant profile '{tenantProfileId}' is not a known supported tenant profile.");
    }

    private static JsonObject LoadRecipe(string recipeFileName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var recipePath = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.ContactCenter",
            "Recipes",
            recipeFileName);

        return JsonNode.Parse(File.ReadAllText(recipePath))?.AsObject() ??
            throw new InvalidOperationException($"The recipe '{recipeFileName}' is invalid.");
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
