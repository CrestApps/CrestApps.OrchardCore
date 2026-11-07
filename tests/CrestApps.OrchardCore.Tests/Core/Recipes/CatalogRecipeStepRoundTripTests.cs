using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Deployments.Sources;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using CrestApps.OrchardCore.Omnichannel.Managements.Recipes;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Tests.Core.Recipes;

/// <summary>
/// The Contact Center and CRM catalog steps run on the Core catalog recipe step and deployment source. These tests
/// replay the sample recipes the repository ships, export what was imported and replay the export again, so the step
/// names and JSON shapes stay the ones existing recipes and plans use.
/// </summary>
public sealed class CatalogRecipeStepRoundTripTests
{
    [Fact]
    public async Task TheShippedReasonCodeRecipe_ImportsExportsAndReplaysWithoutDuplicates()
    {
        // Arrange
        var recipeStep = ReadShippedStep("src/Modules/CrestApps.OrchardCore.ContactCenter/Migrations/agent-state-reason-codes.recipe.json", ContactCenterDeploymentSteps.AgentStateReasonCode);
        var expected = recipeStep["ReasonCodes"].AsArray().Select(node => node["ItemId"].GetValue<string>()).ToArray();
        var store = new List<AgentStateReasonCode>();
        var manager = Backed<IAgentStateReasonCodeManager, AgentStateReasonCode>(store);
        var step = new AgentStateReasonCodeStep(manager.Object);

        // Act
        var firstRun = await RunAsync(step, ContactCenterDeploymentSteps.AgentStateReasonCode, recipeStep);
        var exported = await ExportAsync(new AgentStateReasonCodeDeploymentSource(manager.Object), new AgentStateReasonCodeDeploymentStep());
        var secondRun = await RunAsync(step, ContactCenterDeploymentSteps.AgentStateReasonCode, exported);

        // Assert
        Assert.Empty(firstRun.Errors);
        Assert.Empty(secondRun.Errors);
        Assert.Equal(expected, store.Select(entry => entry.ItemId));
        Assert.Equal("Short break", store[0].Name);
        Assert.Equal(ContactCenterDeploymentSteps.AgentStateReasonCode, exported["name"].GetValue<string>());
        Assert.Equal(expected, exported["ReasonCodes"].AsArray().Select(node => node["ItemId"].GetValue<string>()));
        manager.Verify(value => value.CreateAsync(It.IsAny<AgentStateReasonCode>(), It.IsAny<CancellationToken>()), Times.Exactly(expected.Length));
        manager.Verify(value => value.UpdateAsync(It.IsAny<AgentStateReasonCode>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Exactly(expected.Length));
    }

    [Fact]
    public async Task TheShippedWorkforceSample_ImportsItsContactCenterQueue()
    {
        // Arrange
        var recipeStep = ReadShippedStep("src/Modules/CrestApps.OrchardCore.Workforce.ContactCenter/Recipes/workforce-contact-center-sample.recipe.json", ContactCenterDeploymentSteps.Queue);
        var store = new List<ActivityQueue>();
        var manager = Backed<IActivityQueueManager, ActivityQueue>(store);

        // Act
        var context = await RunAsync(new ContactCenterQueueStep(manager.Object), ContactCenterDeploymentSteps.Queue, recipeStep);
        var exported = await ExportAsync(new ContactCenterQueueDeploymentSource(manager.Object), new ContactCenterQueueDeploymentStep());

        // Assert
        Assert.Empty(context.Errors);
        var queue = Assert.Single(store);
        Assert.Equal("4wfm0cc0q00000000000000001", queue.ItemId);
        Assert.Equal("Support", queue.Name);
        Assert.Equal("Support", Assert.Single(exported["Queues"].AsArray())["Name"].GetValue<string>());
    }

    [Fact]
    public async Task TheLeadStatusStep_MatchesAnEntryWithoutAnIdentifierByName()
    {
        // Arrange
        var store = new List<LeadStatus> { new() { ItemId = "4leadstatus00000000000001", Name = "New" } };
        var manager = Backed<INamedCatalogManager<LeadStatus>, LeadStatus>(store);
        manager.Setup(value => value.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => store.FirstOrDefault(entry => entry.Name == name));

        // Act
        var context = await RunAsync(new LeadStatusStep(manager.Object), OmnichannelDeploymentSteps.LeadStatus, Step("LeadStatuses", new JsonObject { ["Name"] = "New", ["Description"] = "Just arrived." }));

        // Assert
        Assert.Empty(context.Errors);
        var status = Assert.Single(store);
        Assert.Equal("Just arrived.", status.Description);
    }

    [Fact]
    public async Task TheCatalogStep_ChecksAnUpdateBeforeSavingIt()
    {
        // Arrange
        var store = new List<ContactCenterSkill> { new() { ItemId = "4skill000000000000000000001", Name = "Spanish" } };
        var manager = Backed<IContactCenterSkillManager, ContactCenterSkill>(store);
        manager.Setup(value => value.ValidateAsync(It.IsAny<ContactCenterSkill>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactCenterSkill skill, CancellationToken _) => Validate(!string.IsNullOrWhiteSpace(skill.Name)));

        // Act
        var context = await RunAsync(new ContactCenterSkillStep(manager.Object), ContactCenterDeploymentSteps.Skill, Step("Skills", new JsonObject { ["ItemId"] = "4skill000000000000000000001", ["Name"] = string.Empty }));

        // Assert
        Assert.Single(context.Errors);
        Assert.Equal("Spanish", store[0].Name);
        manager.Verify(value => value.UpdateAsync(It.IsAny<ContactCenterSkill>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheCrmDeploymentSource_WritesTheStepNameAndArrayExistingPlansUse()
    {
        // Arrange
        var store = new List<OmnichannelDisposition> { new() { ItemId = "4disp000000000000000000001", Name = "Sold" } };
        var manager = Backed<INamedCatalogManager<OmnichannelDisposition>, OmnichannelDisposition>(store);

        // Act
        var exported = await ExportAsync(new OmnichannelDispositionDeploymentSource(manager.Object), new OmnichannelDispositionDeploymentStep());

        // Assert
        Assert.Equal(OmnichannelDeploymentSteps.Disposition, exported["name"].GetValue<string>());
        var entry = Assert.Single(exported["Dispositions"].AsArray());
        Assert.Equal("Sold", entry["Name"].GetValue<string>());
        Assert.False(entry.AsObject().ContainsKey("CreatedUtc"));
    }

    private static Mock<TManager> Backed<TManager, T>(List<T> store)
        where TManager : class, ICatalogManager<T>
        where T : CatalogItem, new()
    {
        var manager = new Mock<TManager>();
        manager.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => store.FirstOrDefault(entry => entry.ItemId == id));
        manager.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => store.ToArray());
        manager.Setup(value => value.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JsonNode data, CancellationToken _) =>
            {
                var entry = new T { ItemId = Guid.NewGuid().ToString("n") };
                CatalogDeploymentSerializer.Populate(entry, data);

                return entry;
            });
        manager.Setup(value => value.CreateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Callback((T entry, CancellationToken _) => store.Add(entry))
            .Returns(ValueTask.CompletedTask);
        manager.Setup(value => value.UpdateAsync(It.IsAny<T>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback((T entry, JsonNode data, CancellationToken _) => CatalogDeploymentSerializer.Populate(entry, data))
            .Returns(ValueTask.CompletedTask);
        manager.Setup(value => value.ValidateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResultDetails());

        return manager;
    }

    private static async Task<RecipeExecutionContext> RunAsync(IRecipeStepHandler step, string name, JsonObject data)
    {
        var context = new RecipeExecutionContext
        {
            Name = name,
            Step = data,
        };

        await step.ExecuteAsync(context);

        return context;
    }

    private static async Task<JsonObject> ExportAsync(IDeploymentSource source, DeploymentStep step)
    {
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        await source.ProcessDeploymentStepAsync(step, result);

        return Assert.Single(result.Steps);
    }

    private static JsonObject Step(string member, JsonObject entry)
        => new() { [member] = new JsonArray(entry) };

    private static JsonObject ReadShippedStep(string relativePath, string stepName)
    {
        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath)));

        return recipe["steps"].AsArray()
            .OfType<JsonObject>()
            .Single(step => step["name"]?.GetValue<string>() == stepName)
            .DeepClone()
            .AsObject();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }

    private static ValidationResultDetails Validate(bool isValid)
    {
        var result = new ValidationResultDetails();

        if (!isValid)
        {
            result.Fail(new ValidationResult("Not valid."));
        }

        return result;
    }
}
