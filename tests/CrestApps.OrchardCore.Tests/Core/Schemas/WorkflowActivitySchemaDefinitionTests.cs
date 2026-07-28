using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class WorkflowActivitySchemaDefinitionTests
{
    private static readonly string[] _expectedActivityNames =
    [
        "AIChatSessionAllFieldsExtractedEvent",
        "AIChatSessionClosedEvent",
        "AIChatSessionFieldExtractedEvent",
        "AIChatSessionPostProcessedEvent",
        "AICompletionFromProfileTask",
        "AICompletionWithConfigTask",
        "AddModelValidationErrorTask",
        "AssignUserRoleTask",
        "BindModelStateTask",
        "CommitTransactionTask",
        "ContentCreatedEvent",
        "ContentDeletedEvent",
        "ContentDraftSavedEvent",
        "ContentPublishedEvent",
        "ContentUnpublishedEvent",
        "ContentUpdatedEvent",
        "ContentVersionedEvent",
        "CorrelateTask",
        "CreateContentTask",
        "CreateTenantTask",
        "DeleteContentTask",
        "DisableTenantTask",
        "EmailTask",
        "EnableTenantTask",
        "ForEachTask",
        "ForLoopTask",
        "ForkTask",
        "GetUsersByRoleTask",
        "HttpRedirectTask",
        "HttpRedirectToFormLocationTask",
        "HttpRequestEvent",
        "HttpRequestFilterEvent",
        "HttpRequestTask",
        "HttpResponseTask",
        "IfElseTask",
        "JoinTask",
        "LiquidTask",
        "LogTask",
        "NotifyContentOwnerTask",
        "NotifyTask",
        "NotifyUserTask",
        "PublishContentTask",
        "RegisterUserTask",
        "RetrieveContentTask",
        "ScriptTask",
        "SetOutputTask",
        "SetPropertyTask",
        "SetupTenantTask",
        "SignalEvent",
        "SmsTask",
        "TimerEvent",
        "UnassignUserRoleTask",
        "UnpublishContentTask",
        "UpdateContentTask",
        "UpdateTwitterStatusTask",
        "UserConfirmedEvent",
        "UserCreatedEvent",
        "UserDeletedEvent",
        "UserDisabledEvent",
        "UserEnabledEvent",
        "UserLoggedInEvent",
        "UserTaskEvent",
        "UserUpdatedEvent",
        "ValidateAntiforgeryTokenTask",
        "ValidateFormFieldTask",
        "ValidateFormTask",
        "ValidateReCaptchaTask",
        "ValidateUserTask",
        "WhileLoopTask",
        "WorkflowFaultEvent",
    ];

    public static TheoryData<string> DefinitionNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var definition in CreateAllDefinitions())
            {
                data.Add(definition.Name);
            }

            return data;
        }
    }

    [Fact]
    public void EveryKnownWorkflowActivity_HasASchemaDefinition()
    {
        var actual = CreateAllDefinitions()
            .Select(definition => definition.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expected = _expectedActivityNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SchemaDefinitions_DoNotDeclareDuplicateNames()
    {
        var duplicates = CreateAllDefinitions()
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void EverySchemaDefinition_IsRegisteredByTheRecipesModule()
    {
        var registered = GetRegisteredDefinitionTypes();
        var declared = GetDefinitionTypes()
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(declared, registered);
    }

    [Fact]
    public void SchemaDefinitions_AreSealed()
    {
        var unsealed = GetDefinitionTypes()
            .Where(type => !type.IsSealed)
            .Select(type => type.Name)
            .ToArray();

        Assert.Empty(unsealed);
    }

    [Theory]
    [MemberData(nameof(DefinitionNames))]
    public async Task GetActivitySchemaAsync_ReturnsCompleteMetadata(string activityName)
    {
        var definition = CreateDefinition(activityName);
        var schema = await definition.GetActivitySchemaAsync(
            CreateContext(activityName),
            TestContext.Current.CancellationToken);

        Assert.NotNull(schema);
        Assert.False(string.IsNullOrWhiteSpace(schema.Category), $"'{activityName}' does not declare a category.");
        Assert.False(string.IsNullOrWhiteSpace(schema.DisplayText), $"'{activityName}' does not declare a display text.");
        Assert.False(string.IsNullOrWhiteSpace(schema.Description), $"'{activityName}' does not declare a description.");
        Assert.NotNull(schema.Properties);

        var hasOutcomes = schema.Outcomes.Count > 0 || schema.HasDynamicOutcomes;

        Assert.True(hasOutcomes, $"'{activityName}' declares neither outcomes nor dynamic outcomes.");
        Assert.DoesNotContain(schema.Outcomes, string.IsNullOrWhiteSpace);
    }

    [Theory]
    [MemberData(nameof(DefinitionNames))]
    public async Task GetActivitySchemaAsync_AlwaysDescribesActivityMetadata(string activityName)
    {
        var definition = CreateDefinition(activityName);
        var schema = await definition.GetActivitySchemaAsync(
            CreateContext(activityName),
            TestContext.Current.CancellationToken);
        var json = ToJson(schema.Properties);

        Assert.Contains("ActivityMetadata", json);
        Assert.Contains("\"type\":\"object\"", json);
    }

    [Theory]
    [MemberData(nameof(DefinitionNames))]
    public async Task GetActivitySchemaAsync_DescribesEveryPropertyAndOutcome(string activityName)
    {
        var definition = CreateDefinition(activityName);
        var schema = await definition.GetActivitySchemaAsync(
            CreateContext(activityName),
            TestContext.Current.CancellationToken);
        var document = ToNode(schema.Properties);
        var description = GetDescription(document);

        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.Contains(schema.Category, description);

        foreach (var outcome in schema.Outcomes)
        {
            Assert.Contains(outcome, description);
        }

        var properties = GetProperties(document);

        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(GetDescription(property.Value)),
                $"'{activityName}.{property.Key}' does not declare a description.");
        }
    }

    [Theory]
    [MemberData(nameof(DefinitionNames))]
    public async Task GetActivitySchemaAsync_RequiredPropertiesAreAlsoDeclared(string activityName)
    {
        var definition = CreateDefinition(activityName);
        var schema = await definition.GetActivitySchemaAsync(
            CreateContext(activityName),
            TestContext.Current.CancellationToken);
        var document = ToNode(schema.Properties);
        var declared = GetProperties(document).Select(property => property.Key).ToArray();

        foreach (var name in GetRequired(document))
        {
            Assert.Contains(name, declared);
        }
    }

    [Theory]
    [MemberData(nameof(DefinitionNames))]
    public async Task GetActivitySchemaAsync_CachesResult(string activityName)
    {
        var definition = CreateDefinition(activityName);
        var context = CreateContext(activityName);
        var first = await definition.GetActivitySchemaAsync(context, TestContext.Current.CancellationToken);
        var second = await definition.GetActivitySchemaAsync(context, TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetActivitySchemaAsync_ThrowsWhenContextIsNull()
    {
        var definition = new EmailTaskSchema();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await definition.GetActivitySchemaAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EmailTaskSchema_DescribesTheDocumentedContract()
    {
        var definition = new EmailTaskSchema();
        var schema = await definition.GetActivitySchemaAsync(
            CreateContext("EmailTask"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Messaging", schema.Category);
        Assert.Equal("Email Task", schema.DisplayText);
        Assert.Equal(["Done", "Failed"], schema.Outcomes);
        Assert.False(schema.HasDynamicOutcomes);

        var document = ToNode(schema.Properties);
        var properties = GetProperties(document);

        Assert.Equal(
            [
                "ActivityMetadata",
                "Author",
                "Bcc",
                "Body",
                "BodyFormat",
                "Cc",
                "HtmlBody",
                "IsHtmlBody",
                "Recipients",
                "ReplyTo",
                "Sender",
                "Subject",
                "TextBody",
            ],
            properties.Select(property => property.Key).OrderBy(key => key, StringComparer.Ordinal));

        Assert.Equal(["Recipients"], GetRequired(document));
        Assert.Contains("Deprecated.", GetDescription(document["properties"]["Body"]));
        Assert.Contains(WorkflowActivitySchemaBuilders.LiquidSupportText, GetDescription(document["properties"]["Subject"]));
    }

    [Fact]
    public async Task EmailTaskSchema_AcceptsAValidPayload()
    {
        var schema = await BuildPropertiesSchemaAsync("EmailTask");

        using var document = JsonDocument.Parse(
            """
            {
                "ActivityMetadata": { "Title": "Notify the sales team" },
                "Recipients": { "Expression": "sales@example.com" },
                "Subject": { "Expression": "New lead" },
                "BodyFormat": "Html",
                "HtmlBody": { "Expression": "<p>A new lead arrived.</p>" }
            }
            """);

        Assert.True(schema.Evaluate(document.RootElement).IsValid);
    }

    [Fact]
    public async Task ExpressionProperties_AcceptAnEmptyOrNullExpression()
    {
        var schema = await BuildPropertiesSchemaAsync("EmailTask");

        using var document = JsonDocument.Parse(
            """
            {
                "ActivityMetadata": { "Title": null },
                "Recipients": { "Expression": "sales@example.com" },
                "Sender": {},
                "Cc": { "Expression": null }
            }
            """);

        Assert.True(schema.Evaluate(document.RootElement).IsValid);
    }

    [Fact]
    public async Task ExpressionProperties_RejectUnknownMembers()
    {
        var schema = await BuildPropertiesSchemaAsync("EmailTask");

        using var document = JsonDocument.Parse(
            """
            {
                "Recipients": { "Expression": "sales@example.com", "Syntax": "Liquid" }
            }
            """);

        Assert.False(schema.Evaluate(document.RootElement).IsValid);
    }

    [Fact]
    public async Task EmailTaskSchema_RejectsAPayloadMissingRecipients()
    {
        var schema = await BuildPropertiesSchemaAsync("EmailTask");

        using var document = JsonDocument.Parse(
            """
            {
                "Subject": { "Expression": "New lead" }
            }
            """);

        Assert.False(schema.Evaluate(document.RootElement).IsValid);
    }

    [Fact]
    public async Task ExpressionProperties_RejectPlainStrings()
    {
        var schema = await BuildPropertiesSchemaAsync("EmailTask");

        using var document = JsonDocument.Parse(
            """
            {
                "Recipients": "sales@example.com"
            }
            """);

        Assert.False(schema.Evaluate(document.RootElement).IsValid);
    }

    [Fact]
    public async Task UnknownProperties_AreRejectedByDefault()
    {
        var schema = await BuildPropertiesSchemaAsync("EmailTask");

        using var document = JsonDocument.Parse(
            """
            {
                "Recipients": { "Expression": "sales@example.com" },
                "NotAnEmailTaskProperty": true
            }
            """);

        Assert.False(schema.Evaluate(document.RootElement).IsValid);
    }

    [Theory]
    [InlineData("\"Html\"")]
    [InlineData("2")]
    public async Task EnumProperties_AcceptBothTheMemberNameAndTheOrdinal(string value)
    {
        var schema = await BuildPropertiesSchemaAsync("EmailTask");

        using var document = JsonDocument.Parse(
            $$"""
            {
                "Recipients": { "Expression": "sales@example.com" },
                "BodyFormat": {{value}}
            }
            """);

        Assert.True(schema.Evaluate(document.RootElement).IsValid);
    }

    [Fact]
    public async Task EnumProperties_RejectUnknownValues()
    {
        var schema = await BuildPropertiesSchemaAsync("EmailTask");

        using var document = JsonDocument.Parse(
            """
            {
                "Recipients": { "Expression": "sales@example.com" },
                "BodyFormat": "Markdown"
            }
            """);

        Assert.False(schema.Evaluate(document.RootElement).IsValid);
    }

    [Fact]
    public async Task DynamicOutcomeActivities_AnnounceTheirDynamicOutcomes()
    {
        var definition = new ForkTaskSchema();
        var schema = await definition.GetActivitySchemaAsync(
            CreateContext("ForkTask"),
            TestContext.Current.CancellationToken);

        Assert.True(schema.HasDynamicOutcomes);
        Assert.Contains("derived from this activity's own configuration", GetDescription(ToNode(schema.Properties)));
    }

    [Fact]
    public async Task EventDefinitions_DescribeThemselvesAsEvents()
    {
        var definition = new ContentPublishedEventSchema();
        var schema = await definition.GetActivitySchemaAsync(
            CreateContext("ContentPublishedEvent", isEvent: true),
            TestContext.Current.CancellationToken);

        Assert.Contains("workflow event", GetDescription(ToNode(schema.Properties)));
    }

    [Fact]
    public async Task ActivitiesWithoutProperties_StillDescribeActivityMetadata()
    {
        var schema = await BuildPropertiesSchemaAsync("CommitTransactionTask");

        using var document = JsonDocument.Parse(
            """
            {
                "ActivityMetadata": { "Title": "Save changes" }
            }
            """);

        Assert.True(schema.Evaluate(document.RootElement).IsValid);
    }

    [Fact]
    public async Task FallsBackToTheActivityLibraryValuesWhenNotOverridden()
    {
        var definition = new FallbackActivitySchema();
        var schema = await definition.GetActivitySchemaAsync(
            new WorkflowActivitySchemaContext
            {
                ActivityName = "FallbackTask",
                IsEvent = false,
                IsTask = true,
                Category = "Library Category",
                DisplayText = "Library Display Text",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("Library Category", schema.Category);
        Assert.Equal("Library Display Text", schema.DisplayText);
        Assert.Contains("produces no outcomes", GetDescription(ToNode(schema.Properties)));
    }

    private static async Task<JsonSchema> BuildPropertiesSchemaAsync(string activityName)
    {
        var definition = CreateDefinition(activityName);
        var schema = await definition.GetActivitySchemaAsync(
            CreateContext(activityName),
            TestContext.Current.CancellationToken);

        return schema.Properties.Build();
    }

    private static WorkflowActivitySchemaContext CreateContext(string activityName, bool isEvent = false)
        => new()
        {
            ActivityName = activityName,
            IsEvent = isEvent,
            IsTask = !isEvent,
        };

    private static string ToJson(JsonSchemaBuilder builder)
        => builder.Build().Root.Source.GetRawText();

    private static JsonNode ToNode(JsonSchemaBuilder builder)
        => JsonNode.Parse(ToJson(builder));

    private static string GetDescription(JsonNode schema)
        => schema?["description"]?.GetValue<string>();

    private static JsonObject GetProperties(JsonNode schema)
        => schema?["properties"]?.AsObject() ?? [];

    private static string[] GetRequired(JsonNode schema)
        => schema?["required"]?.AsArray().Select(node => node.GetValue<string>()).ToArray() ?? [];

    private static WorkflowActivitySchemaDefinitionBase CreateDefinition(string activityName)
        => CreateAllDefinitions().Single(definition => definition.Name == activityName);

    private static IEnumerable<WorkflowActivitySchemaDefinitionBase> CreateAllDefinitions()
        => GetDefinitionTypes().Select(type => (WorkflowActivitySchemaDefinitionBase)Activator.CreateInstance(type));

    private static string[] GetRegisteredDefinitionTypes()
    {
        var services = new ServiceCollection();
        var startupTypes = Assembly.Load("CrestApps.OrchardCore.Recipes")
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(IStartup).IsAssignableFrom(type))
            .Where(type => type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, []) is not null);

        foreach (var startupType in startupTypes)
        {
            ((IStartup)Activator.CreateInstance(startupType)).ConfigureServices(services);
        }

        return services
            .Where(descriptor => descriptor.ServiceType == typeof(IWorkflowActivitySchemaDefinition))
            .Select(descriptor => descriptor.ImplementationType?.FullName)
            .Where(name => name is not null)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<Type> GetDefinitionTypes()
        => typeof(EmailTaskSchema).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(WorkflowActivitySchemaDefinitionBase).IsAssignableFrom(type))
            .Where(type => type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, []) is not null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

    private sealed class FallbackActivitySchema : WorkflowActivitySchemaDefinitionBase
    {
        public override string Name { get; } = "FallbackTask";

        protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
            => [];
    }
}
