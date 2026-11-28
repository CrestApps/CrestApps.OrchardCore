using System.Text.Json.Nodes;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Agent;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Tests.Agent.AIQuality;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Agent.Records;

/// <summary>
/// The AI alert rules, QA cohorts and test cases are record kinds of the generic record tools, with the AI quality
/// permission, written through their admin editors' own drivers and stored in their stores, with the profile and email
/// checks the editors leave to their option lists.
/// </summary>
public sealed class AIQualityRecordKindsTests
{
    [Fact]
    public void TheQualityStartup_RegistersTheThreeKinds_WithTheQualityPermission()
    {
        // Arrange
        var services = new ServiceCollection();

        new AIQualityManagementToolsStartup(new RecordTestLocalizer<AIQualityManagementToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        // Act
        var kinds = provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;

        // Assert
        Assert.Equal(new[] { "aiAlertRule", "aiQACohort", "aiTestCase" }, kinds.Keys.Order(StringComparer.Ordinal));

        foreach (var kind in kinds.Values)
        {
            Assert.Equal(AIPermissions.ManageAIQuality.Name, kind.Permission.Name);
            Assert.Equal(AIPermissions.ManageAIQuality.Name, kind.EffectiveReadPermission.Name);
            Assert.True(kind.CanWrite);
            Assert.False(string.IsNullOrWhiteSpace(kind.Description));
            Assert.NotEqual(kind.Name, kind.Title);
        }

        Assert.Contains("paid judge model call", kinds["aiQACohort"].Description, StringComparison.Ordinal);
        Assert.Contains("listAIAlerts", kinds["aiAlertRule"].Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveRecord_CreatesAnAlertRule_ThroughTheAlertRuleEditor()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "aiAlertRule",
            ["values"] = JsonNode.Parse("""
                {
                  "Name": " Support QA failures ",
                  "Enabled": true,
                  "Metric": "QAFailureRate",
                  "ProfileId": "support",
                  "Condition": "Above",
                  "Threshold": 20,
                  "WindowMinutes": 60,
                  "MinimumSampleSize": 10,
                  "CheckEveryMinutes": 5,
                  "NotifyEmails": "admin@example.com"
                }
                """),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var rule = Assert.Single(harness.Rules);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal(rule.ItemId, json.GetProperty("id").GetString());
        Assert.False(string.IsNullOrEmpty(rule.ItemId));
        Assert.Equal("Support QA failures", rule.Name);
        Assert.Equal(AIConversationMetric.QAFailureRate, rule.Metric);
        Assert.Equal(20, rule.Threshold);
        Assert.Equal(AIQualityToolHarness.Now, rule.CreatedUtc);
        Assert.Equal("admin@example.com", Assert.Single(json.GetProperty("record").GetProperty("notifyEmails").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task SaveRecord_RefusesAnAlertRuleForAnUnknownProfileOrABadAddress()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Rules.Add(new AIAlertRule { ItemId = "rule-1", Name = "Failures", Threshold = 20 });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "aiAlertRule",
            ["id"] = "rule-1",
            ["values"] = JsonNode.Parse("""{ "ProfileId": "ghost", "NotifyEmails": "not an address" }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var errors = json.GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("message").GetString()).ToList();

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains(errors, error => error.Contains("No AI profile has the ID 'ghost'", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("'not an address' is not an email address", StringComparison.Ordinal));
        Assert.Equal(0, harness.Writes);
        Assert.Null(harness.Rules[0].ProfileId);
    }

    [Fact]
    public async Task SaveRecord_CreatesAQACohortThatScoresFromNowOn()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "aiQACohort",
            ["values"] = JsonNode.Parse("""
                {
                  "Name": "Sales accuracy",
                  "Enabled": true,
                  "ProfileIds": [ "support" ],
                  "SamplePercent": 10,
                  "Criteria": "The answer is accurate.",
                  "PassingScore": 70,
                  "CheckKnowledge": true
                }
                """),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var cohort = Assert.Single(harness.Cohorts);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal(10, cohort.SamplePercent);
        Assert.Equal(new[] { "support" }, cohort.ProfileIds);
        Assert.Equal(AIQualityToolHarness.Now, cohort.CursorUtc);
        Assert.Equal(AIQualityToolHarness.Now, cohort.CreatedUtc);
    }

    [Fact]
    public async Task SaveRecord_RefusesAQACohortOfAnUnknownProfile()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "aiQACohort",
            ["values"] = JsonNode.Parse("""{ "Name": "X", "Enabled": true, "ProfileIds": [ "ghost" ], "SamplePercent": 10, "Criteria": "Accurate.", "PassingScore": 70 }"""),
        });

        // Assert
        Assert.Contains("No AI profile has the ID 'ghost'", result, StringComparison.Ordinal);
        Assert.Empty(harness.Cohorts);
        Assert.Equal(0, harness.Writes);
    }

    [Fact]
    public async Task SaveRecord_ChangesATestCase_AndStampsTheChange()
    {
        // Arrange
        var harness = CreateHarness();
        harness.TestCases.Add(new AITestCase
        {
            ItemId = "test-1",
            Name = "Refund",
            ProfileId = "support",
            Scenario = "Wants a refund.",
            Criteria = "A refund is offered.",
        });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "aiTestCase",
            ["id"] = "test-1",
            ["values"] = JsonNode.Parse("""{ "MaxTurns": 5, "MockedTools": "issueRefund: {\"ok\":true}" }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var testCase = Assert.Single(harness.TestCases);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal(5, testCase.MaxTurns);
        Assert.Equal("{\"ok\":true}", testCase.MockedTools["issueRefund"]);
        Assert.Equal(AIQualityToolHarness.Now, testCase.ModifiedUtc);
        Assert.Equal("issueRefund", Assert.Single(json.GetProperty("record").GetProperty("mockedTools").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task DeleteRecord_DeletesATestCase()
    {
        // Arrange
        var harness = CreateHarness();
        harness.TestCases.Add(new AITestCase { ItemId = "test-1", Name = "Refund", ProfileId = "support" });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "aiTestCase",
            ["ids"] = new[] { "test-1" },
        });

        // Assert
        Assert.Contains("test-1", result, StringComparison.Ordinal);
        Assert.Empty(harness.TestCases);
    }

    [Fact]
    public async Task ListRecords_ListsTheQACohortsByName()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Cohorts.Add(new AIQACohort { ItemId = "cohort-1", Name = "Accuracy", ProfileIds = ["support"] });
        harness.Cohorts.Add(new AIQACohort { ItemId = "cohort-2", Name = "Tone", ProfileIds = ["support"] });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListRecordsTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "aiQACohort",
            ["query"] = "accu",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var cohort = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("cohort-1", cohort.GetProperty("id").GetString());
        Assert.Equal("QA: Accuracy", cohort.GetProperty("label").GetString());
    }

    private static AIQualityToolHarness CreateHarness()
    {
        var profile = new AIProfile { ItemId = "support", Name = "support", DisplayText = "Support", Type = AIProfileType.Chat };
        var harness = new AIQualityToolHarness();

        harness.Profiles.Add(profile);

        var profileStore = new Mock<IAIProfileStore>();
        profileStore.Setup(store => store.GetByTypeAsync(It.IsAny<AIProfileType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<AIProfile>)[profile]);

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager.Setup(manager => manager.GetAllBySlotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AIDeployment>());

        harness.Configure = services =>
        {
            services.AddLogging();
            services.AddLocalization();
            services.AddMvcCore().AddDataAnnotations();
            services.AddEditorDisplayManagement();
            services.AddSingleton(profileStore.Object);
            services.AddSingleton(deploymentManager.Object);
            services.AddScoped<IDisplayDriver<AIAlertRule>, AIAlertRuleDisplayDriver>();
            services.AddScoped<IDisplayDriver<AIQACohort>, AIQACohortDisplayDriver>();
            services.AddScoped<IDisplayDriver<AITestCase>, AITestCaseDisplayDriver>();
            services.AddScoped<AIRecordScopeChanges>();

            new AIQualityManagementToolsStartup(new RecordTestLocalizer<AIQualityManagementToolsStartup>()).ConfigureServices(services);
        };

        return harness;
    }
}
