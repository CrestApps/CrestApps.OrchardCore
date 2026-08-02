using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class WorkflowActivitySchemaServiceTests
{
    [Fact]
    public async Task GetActivityDescriptorsAsync_MergesTheLibraryWithTheRegisteredDefinitions()
    {
        var service = CreateService();
        var descriptors = await service.GetActivityDescriptorsAsync(TestContext.Current.CancellationToken);
        var emailTask = descriptors.Single(descriptor => descriptor.Name == "EmailTask");

        Assert.True(emailTask.IsTask);
        Assert.False(emailTask.IsEvent);
        Assert.True(emailTask.HasSchemaDefinition);
        Assert.Equal("Messaging", emailTask.Category);
        Assert.Equal(["Done", "Failed"], emailTask.Outcomes);
        Assert.NotNull(emailTask.Properties);
    }

    [Fact]
    public async Task GetActivityDescriptorsAsync_FlagsActivitiesWithoutADefinition()
    {
        var service = CreateService();
        var descriptors = await service.GetActivityDescriptorsAsync(TestContext.Current.CancellationToken);
        var undescribed = descriptors.Single(descriptor => descriptor.Name == "UndescribedTask");

        Assert.False(undescribed.HasSchemaDefinition);
        Assert.Null(undescribed.Properties);
        Assert.Empty(undescribed.Outcomes);
        Assert.Equal("Custom", undescribed.Category);
        Assert.Equal("Undescribed Task", undescribed.DisplayText);
    }

    [Fact]
    public async Task GetActivityDescriptorsAsync_CachesResult()
    {
        var service = CreateService();
        var first = await service.GetActivityDescriptorsAsync(TestContext.Current.CancellationToken);
        var second = await service.GetActivityDescriptorsAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetActivitySchemaAsync_AcceptsAnEventAsTheStartActivity()
    {
        var schema = await CreateService().GetActivitySchemaAsync(TestContext.Current.CancellationToken);

        Assert.True(Evaluate(
            schema,
            """
            {
                "ActivityId": "1",
                "Name": "ContentPublishedEvent",
                "IsStart": true,
                "Properties": { "ContentTypeFilter": [ "Article" ] }
            }
            """));
    }

    [Fact]
    public async Task GetActivitySchemaAsync_AllowsATaskAsTheStartActivity()
    {
        var schema = await CreateService().GetActivitySchemaAsync(TestContext.Current.CancellationToken);

        Assert.True(Evaluate(
            schema,
            """
            {
                "ActivityId": "1",
                "Name": "EmailTask",
                "IsStart": true,
                "Properties": { "Recipients": { "Expression": "sales@example.com" } }
            }
            """));
    }

    [Fact]
    public async Task GetActivitySchemaAsync_AllowsAnEventAsABlockingNonStartActivity()
    {
        var schema = await CreateService().GetActivitySchemaAsync(TestContext.Current.CancellationToken);

        Assert.True(Evaluate(
            schema,
            """
            {
                "ActivityId": "1",
                "Name": "ContentPublishedEvent",
                "IsStart": false,
                "Properties": {}
            }
            """));
    }

    [Fact]
    public async Task GetActivitySchemaAsync_AcceptsAnExpressionThatWasNeverFilledIn()
    {
        var schema = await CreateService().GetActivitySchemaAsync(TestContext.Current.CancellationToken);

        Assert.True(Evaluate(
            schema,
            """
            {
                "ActivityId": "2",
                "Name": "EmailTask",
                "IsStart": false,
                "Properties": {
                    "ActivityMetadata": { "Title": null },
                    "Recipients": { "Expression": "sales@example.com" },
                    "Sender": {},
                    "Cc": { "Expression": null }
                }
            }
            """));
    }

    [Fact]
    public async Task GetActivitySchemaAsync_AppliesTheMatchingActivityPropertiesSchema()
    {
        var schema = await CreateService().GetActivitySchemaAsync(TestContext.Current.CancellationToken);

        Assert.False(Evaluate(
            schema,
            """
            {
                "ActivityId": "2",
                "Name": "EmailTask",
                "IsStart": false,
                "Properties": { "Subject": { "Expression": "Hello" } }
            }
            """));

        Assert.True(Evaluate(
            schema,
            """
            {
                "ActivityId": "2",
                "Name": "EmailTask",
                "IsStart": false,
                "Properties": {
                    "Recipients": { "Expression": "sales@example.com" },
                    "Subject": { "Expression": "Hello" }
                }
            }
            """));
    }

    [Fact]
    public async Task GetActivitySchemaAsync_LeavesUndescribedActivitiesUnconstrained()
    {
        var schema = await CreateService().GetActivitySchemaAsync(TestContext.Current.CancellationToken);

        Assert.True(Evaluate(
            schema,
            """
            {
                "ActivityId": "3",
                "Name": "UndescribedTask",
                "IsStart": false,
                "Properties": { "AnythingGoes": 42 }
            }
            """));
    }

    [Fact]
    public async Task GetActivitySchemaAsync_AllowsUnknownActivityNames()
    {
        var schema = await CreateService().GetActivitySchemaAsync(TestContext.Current.CancellationToken);

        Assert.True(Evaluate(
            schema,
            """
            {
                "ActivityId": "4",
                "Name": "NotARegisteredTask",
                "IsStart": false,
                "Properties": {}
            }
            """));
    }

    [Fact]
    public async Task WorkflowTypeRecipeStep_AcceptsACompleteWorkflowType()
    {
        var schema = await new WorkflowTypeRecipeStep(CreateService()).GetSchemaAsync(TestContext.Current.CancellationToken);

        Assert.True(Evaluate(schema, CreateWorkflowRecipe()));
    }

    [Fact]
    public async Task WorkflowTypeRecipeStep_RequiresTheWorkflowTypeIdentifier()
    {
        var schema = await new WorkflowTypeRecipeStep(CreateService()).GetSchemaAsync(TestContext.Current.CancellationToken);
        var recipe = JsonNode.Parse(CreateWorkflowRecipe()).AsObject();

        recipe["data"][0].AsObject().Remove("WorkflowTypeId");

        Assert.False(Evaluate(schema, recipe.ToJsonString()));
    }

    [Fact]
    public async Task WorkflowTypeRecipeStep_RequiresEveryTransitionEndpoint()
    {
        var schema = await new WorkflowTypeRecipeStep(CreateService()).GetSchemaAsync(TestContext.Current.CancellationToken);
        var recipe = JsonNode.Parse(CreateWorkflowRecipe()).AsObject();

        recipe["data"][0]["Transitions"][0].AsObject().Remove("SourceOutcomeName");

        Assert.False(Evaluate(schema, recipe.ToJsonString()));
    }

    [Fact]
    public async Task WorkflowTypeRecipeStep_RequiresEveryActivityIdentifier()
    {
        var schema = await new WorkflowTypeRecipeStep(CreateService()).GetSchemaAsync(TestContext.Current.CancellationToken);
        var recipe = JsonNode.Parse(CreateWorkflowRecipe()).AsObject();

        recipe["data"][0]["Activities"][1].AsObject().Remove("ActivityId");

        Assert.False(Evaluate(schema, recipe.ToJsonString()));
    }

    [Fact]
    public async Task WorkflowTypeRecipeStep_AcceptsTheExportedWorkflowTypePropertyBag()
    {
        var schema = await new WorkflowTypeRecipeStep(CreateService()).GetSchemaAsync(TestContext.Current.CancellationToken);
        var recipe = JsonNode.Parse(CreateWorkflowRecipe()).AsObject();

        recipe["data"][0]["Properties"] = new JsonObject();
        recipe["data"][0]["LockTimeout"] = 0;
        recipe["data"][0]["LockExpiration"] = 0;
        recipe["data"][0]["Transitions"][0]["Id"] = 0;

        Assert.True(Evaluate(schema, recipe.ToJsonString()));
    }

    [Fact]
    public async Task WorkflowTypeRecipeStep_RejectsANonObjectWorkflowTypePropertyBag()
    {
        var schema = await new WorkflowTypeRecipeStep(CreateService()).GetSchemaAsync(TestContext.Current.CancellationToken);
        var recipe = JsonNode.Parse(CreateWorkflowRecipe()).AsObject();

        recipe["data"][0]["Properties"] = "not-an-object";

        Assert.False(Evaluate(schema, recipe.ToJsonString()));
    }

    [Fact]
    public async Task WorkflowTypeRecipeStep_RequiresAtLeastOneActivity()
    {
        var schema = await new WorkflowTypeRecipeStep(CreateService()).GetSchemaAsync(TestContext.Current.CancellationToken);
        var recipe = JsonNode.Parse(CreateWorkflowRecipe()).AsObject();

        recipe["data"][0]["Activities"] = new JsonArray();

        Assert.False(Evaluate(schema, recipe.ToJsonString()));
    }

    [Fact]
    public async Task WorkflowTypeRecipeStep_ListsTheKnownOutcomesInTheTransitionDescription()
    {
        var schema = await new WorkflowTypeRecipeStep(CreateService()).GetSchemaAsync(TestContext.Current.CancellationToken);
        var description = JsonNode.Parse(schema.Root.Source.GetRawText())
            ["properties"]["data"]["items"]["properties"]["Transitions"]["items"]["properties"]["SourceOutcomeName"]["description"]
            .GetValue<string>();

        Assert.Contains("Done", description);
        Assert.Contains("Failed", description);
    }

    [Fact]
    public async Task WorkflowTypeRecipeStep_CachesResult()
    {
        var step = new WorkflowTypeRecipeStep(CreateService());
        var first = await step.GetSchemaAsync(TestContext.Current.CancellationToken);
        var second = await step.GetSchemaAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    private static string CreateWorkflowRecipe()
        => """
        {
            "name": "WorkflowType",
            "data": [
                {
                    "WorkflowTypeId": "notify-on-publish",
                    "Name": "Notify on publish",
                    "IsEnabled": true,
                    "IsSingleton": false,
                    "DeleteFinishedWorkflows": false,
                    "Activities": [
                        {
                            "ActivityId": "1",
                            "Name": "ContentPublishedEvent",
                            "IsStart": true,
                            "X": 100,
                            "Y": 100,
                            "Properties": {
                                "ActivityMetadata": { "Title": "When an article is published" },
                                "ContentTypeFilter": [ "Article" ]
                            }
                        },
                        {
                            "ActivityId": "2",
                            "Name": "EmailTask",
                            "IsStart": false,
                            "X": 400,
                            "Y": 100,
                            "Properties": {
                                "Recipients": { "Expression": "editors@example.com" },
                                "Subject": { "Expression": "Published" },
                                "BodyFormat": "Html",
                                "HtmlBody": { "Expression": "<p>Live.</p>" }
                            }
                        }
                    ],
                    "Transitions": [
                        {
                            "SourceActivityId": "1",
                            "SourceOutcomeName": "Done",
                            "DestinationActivityId": "2"
                        }
                    ]
                }
            ]
        }
        """;

    private static bool Evaluate(JsonSchemaBuilder builder, string json)
        => Evaluate(builder.Build(), json);

    private static bool Evaluate(JsonSchema schema, string json)
    {
        using var document = JsonDocument.Parse(json);

        return schema.Evaluate(document.RootElement).IsValid;
    }

    private static WorkflowActivitySchemaService CreateService()
    {
        var activities = new[]
        {
            CreateActivity<IEvent>("ContentPublishedEvent", "Content", "Content Published Event"),
            CreateActivity<ITask>("EmailTask", "Messaging", "Email Task"),
            CreateActivity<ITask>("UndescribedTask", "Custom", "Undescribed Task"),
        };

        var library = new Mock<IActivityLibrary>();
        library.Setup(instance => instance.ListActivities())
            .Returns(activities);

        var definitions = new IWorkflowActivitySchemaDefinition[]
        {
            new ContentPublishedEventSchema(),
            new EmailTaskSchema(),
        };

        return new WorkflowActivitySchemaService(library.Object, definitions);
    }

    private static IActivity CreateActivity<TActivity>(string name, string category, string displayText)
        where TActivity : class, IActivity
    {
        var activity = new Mock<TActivity>();
        activity.SetupGet(instance => instance.Name).Returns(name);
        activity.SetupGet(instance => instance.Category).Returns(new LocalizedString(category, category));
        activity.SetupGet(instance => instance.DisplayText).Returns(new LocalizedString(displayText, displayText));

        return activity.Object;
    }
}
