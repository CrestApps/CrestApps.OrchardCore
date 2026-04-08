using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Mvc.Web.Areas.AI.ViewModels;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class AIProfileViewModelTests
{
    [Fact]
    public void FromProfile_ReadsSettingsAndMetadataFromMatchingStores()
    {
        var profile = new AIProfile();

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "System",
            PastMessagesCount = 7,
        });
        profile.AlterSettings<AIProfileSettings>(settings =>
        {
            settings.LockSystemMessage = true;
            settings.IsListable = true;
            settings.IsRemovable = false;
        });
        profile.Put(new DocumentsMetadata
        {
            DocumentTopN = 4,
        });
        profile.Put(new AIProfileSessionDocumentsMetadata
        {
            AllowSessionDocuments = true,
        });
        profile.AlterSettings<AIProfileDataExtractionSettings>(settings =>
        {
            settings.EnableDataExtraction = true;
            settings.ExtractionCheckInterval = 3;
            settings.SessionInactivityTimeoutInMinutes = 15;
            settings.DataExtractionEntries =
            [
                new DataExtractionEntry
                {
                    Name = "email",
                    Description = "Capture email",
                    AllowMultipleValues = false,
                    IsUpdatable = true,
                },
            ];
        });
        profile.AlterSettings<AIProfilePostSessionSettings>(settings =>
        {
            settings.EnablePostSessionProcessing = true;
        });
        profile.AlterMemoryMetadata(settings =>
        {
            settings.EnableUserMemory = true;
        });

        var model = AIProfileViewModel.FromProfile(profile);

        Assert.Equal("System", model.SystemMessage);
        Assert.Equal(7, model.PastMessagesCount);
        Assert.True(model.LockSystemMessage);
        Assert.True(model.IsListable);
        Assert.False(model.IsRemovable);
        Assert.Equal(4, model.DocumentTopN);
        Assert.True(model.AllowSessionDocuments);
        Assert.True(model.EnableDataExtraction);
        Assert.Equal(3, model.ExtractionCheckInterval);
        Assert.Equal(15, model.SessionInactivityTimeoutInMinutes);
        Assert.Single(model.DataExtractionEntries);
        Assert.True(model.EnablePostSessionProcessing);
        Assert.True(model.EnableUserMemory);
    }

    [Fact]
    public void ApplyTo_WritesSettingsAndMetadataToMatchingStores()
    {
        var model = new AIProfileViewModel
        {
            Name = "profile",
            DisplayText = "Profile",
            Type = AIProfileType.Chat,
            Source = "source",
            ChatDeploymentName = "chat-deployment",
            UtilityDeploymentName = "utility-deployment",
            OrchestratorName = "orchestrator",
            PromptTemplate = "template-source",
            PromptSubject = "subject",
            Description = "description",
            TitleType = AISessionTitleType.Generated,
            WelcomeMessage = "welcome",
            AddInitialPrompt = true,
            InitialPrompt = " hello ",
            SystemMessage = "system",
            Temperature = 0.2f,
            TopP = 0.7f,
            FrequencyPenalty = 0.3f,
            PresencePenalty = 0.4f,
            MaxTokens = 256,
            PastMessagesCount = 9,
            UseCaching = false,
            LockSystemMessage = true,
            IsListable = true,
            IsRemovable = false,
            SelectedToolNames = ["tool-1"],
            SelectedAgentNames = ["agent-1"],
            DataSourceId = "data-source-1",
            DataSourceStrictness = 4,
            DataSourceTopNDocuments = 8,
            DataSourceIsInScope = true,
            DataSourceFilter = "category eq 'docs'",
            SelectedA2AConnectionIds = ["a2a-1"],
            SelectedMcpConnectionIds = ["mcp-1"],
            PromptTemplates =
            [
                new PromptTemplateSelectionItem
                {
                    TemplateId = "template-1",
                    PromptParameters = "{\"tone\":\"friendly\"}",
                },
            ],
            DocumentTopN = 6,
            AllowSessionDocuments = true,
            EnableDataExtraction = true,
            ExtractionCheckInterval = 2,
            SessionInactivityTimeoutInMinutes = 12,
            DataExtractionEntries =
            [
                new DataExtractionEntryItem
                {
                    Name = "accountId",
                    Description = "Capture account id",
                    AllowMultipleValues = false,
                    IsUpdatable = true,
                },
            ],
            EnablePostSessionProcessing = true,
            PostSessionTasks =
            [
                new PostSessionTaskItem
                {
                    Name = "summary",
                    Type = PostSessionTaskType.Semantic,
                    Instructions = "Summarize the chat",
                    AllowMultipleValues = false,
                    Options = "one" + Environment.NewLine + "two",
                },
            ],
            EnableSessionMetrics = true,
            EnableAIResolutionDetection = false,
            EnableConversionMetrics = true,
            ConversionGoals =
            [
                new ConversionGoalItem
                {
                    Name = "sale",
                    Description = "Close the sale",
                    MinScore = 2,
                    MaxScore = 7,
                },
            ],
            EnableUserMemory = true,
            CopilotModel = "gpt-5",
            CopilotIsAllowAll = true,
        };

        var profile = new AIProfile();

        model.ApplyTo(profile);

        Assert.Equal("profile", profile.Name);
        Assert.Equal("Profile", profile.DisplayText);
        Assert.Equal(AIProfileType.Chat, profile.Type);
        Assert.Equal("source", profile.Source);
        Assert.Equal("chat-deployment", profile.ChatDeploymentName);
        Assert.Equal("utility-deployment", profile.UtilityDeploymentName);
        Assert.Equal("orchestrator", profile.OrchestratorName);
        Assert.Equal("template-source", profile.PromptTemplate);
        Assert.Equal("subject", profile.PromptSubject);
        Assert.Equal("description", profile.Description);
        Assert.Equal(AISessionTitleType.Generated, profile.TitleType);
        Assert.Null(profile.WelcomeMessage);

        var profileMetadata = profile.As<AIProfileMetadata>();
        Assert.Equal("system", profileMetadata.SystemMessage);
        Assert.Equal("hello", profileMetadata.InitialPrompt);
        Assert.Equal(0.2f, profileMetadata.Temperature);
        Assert.Equal(0.7f, profileMetadata.TopP);
        Assert.Equal(0.3f, profileMetadata.FrequencyPenalty);
        Assert.Equal(0.4f, profileMetadata.PresencePenalty);
        Assert.Equal(256, profileMetadata.MaxTokens);
        Assert.Equal(9, profileMetadata.PastMessagesCount);
        Assert.False(profileMetadata.UseCaching);

        var profileSettings = profile.GetSettings<AIProfileSettings>();
        Assert.True(profileSettings.LockSystemMessage);
        Assert.True(profileSettings.IsListable);
        Assert.False(profileSettings.IsRemovable);

        Assert.Equal(["tool-1"], profile.As<FunctionInvocationMetadata>().Names);
        Assert.Equal(["agent-1"], profile.As<AgentInvocationMetadata>().Names);
        Assert.Equal("data-source-1", profile.As<DataSourceMetadata>().DataSourceId);
        Assert.Equal(["a2a-1"], profile.As<AIProfileA2AMetadata>().ConnectionIds);
        Assert.Equal(["mcp-1"], profile.As<AIProfileMcpMetadata>().ConnectionIds);

        var ragMetadata = profile.As<AIDataSourceRagMetadata>();
        Assert.Equal(4, ragMetadata.Strictness);
        Assert.Equal(8, ragMetadata.TopNDocuments);
        Assert.True(ragMetadata.IsInScope);
        Assert.Equal("category eq 'docs'", ragMetadata.Filter);

        var promptMetadata = profile.As<PromptTemplateMetadata>();
        var promptTemplate = Assert.Single(promptMetadata.Templates);
        Assert.Equal("template-1", promptTemplate.TemplateId);
        Assert.Equal("friendly", Assert.IsType<string>(promptTemplate.Parameters["tone"]));

        Assert.Equal(6, profile.As<DocumentsMetadata>().DocumentTopN);
        Assert.True(profile.As<AIProfileSessionDocumentsMetadata>().AllowSessionDocuments);

        var extractionSettings = profile.GetSettings<AIProfileDataExtractionSettings>();
        Assert.True(extractionSettings.EnableDataExtraction);
        Assert.Equal(2, extractionSettings.ExtractionCheckInterval);
        Assert.Equal(12, extractionSettings.SessionInactivityTimeoutInMinutes);
        Assert.Single(extractionSettings.DataExtractionEntries);

        var analyticsMetadata = profile.As<AnalyticsMetadata>();
        Assert.True(analyticsMetadata.EnableSessionMetrics);
        Assert.False(analyticsMetadata.EnableAIResolutionDetection);
        Assert.True(analyticsMetadata.EnableConversionMetrics);
        var conversionGoal = Assert.Single(analyticsMetadata.ConversionGoals);
        Assert.Equal("sale", conversionGoal.Name);
        Assert.Equal("Close the sale", conversionGoal.Description);
        Assert.Equal(2, conversionGoal.MinScore);
        Assert.Equal(7, conversionGoal.MaxScore);

        Assert.True(profile.GetSettings<AIProfilePostSessionSettings>().EnablePostSessionProcessing);
        var postSessionTask = Assert.Single(profile.GetSettings<AIProfilePostSessionSettings>().PostSessionTasks);
        Assert.Equal("summary", postSessionTask.Name);
        Assert.Equal(PostSessionTaskType.Semantic, postSessionTask.Type);
        Assert.Equal("Summarize the chat", postSessionTask.Instructions);
        Assert.False(postSessionTask.AllowMultipleValues);
        Assert.Collection(
            postSessionTask.Options,
            option => Assert.Equal("one", option.Value),
            option => Assert.Equal("two", option.Value));

        Assert.True(profile.As<MemoryMetadata>().EnableUserMemory ?? false);

        Assert.False(profile.TryGet<CopilotSessionMetadata>(out _));

        model.OrchestratorName = CopilotOrchestrator.OrchestratorName;
        model.ApplyTo(profile);

        Assert.True(profile.TryGet<CopilotSessionMetadata>(out var copilotMetadata));
        Assert.Equal("gpt-5", copilotMetadata.CopilotModel);
        Assert.True(copilotMetadata.IsAllowAll);
    }

    [Fact]
    public void FromProfile_WhenLegacyMvcMemorySettingsExist_ShouldFallbackToLegacyValue()
    {
        var profile = new AIProfile();
        profile.AlterSettings<CrestApps.Core.Mvc.Web.Models.MemorySettings>(settings =>
        {
            settings.EnableUserMemory = true;
        });

        var model = AIProfileViewModel.FromProfile(profile);

        Assert.True(model.EnableUserMemory);
    }

    [Fact]
    public void FromProfile_ReadsFlattenedExtensionDataAfterReload()
    {
        var json = """
            {
              "Name":"Test",
              "DisplayText":"Test",
              "Type":0,
              "CreatedUtc":"2026-04-02T19:59:41.5303712Z",
              "Settings":{
                "AIProfileSettings":{
                  "LockSystemMessage":false,
                  "IsListable":true,
                  "IsRemovable":true
                }
              },
              "ItemId":"3e169d42eee44ccb956d61cd046daf43",
              "DataSourceMetadata":{
                "DataSourceId":"4kwqvhkg2p1jx30rrjn7vqv6da"
              },
              "AIDataSourceRagMetadata":{
                "IsInScope":true
              },
              "DocumentsMetadata":{
                "Documents":[],
                "DocumentTopN":5
              }
            }
            """;

        var profile = JsonSerializer.Deserialize<AIProfile>(json);

        Assert.NotNull(profile);
        Assert.True(profile.Properties.ContainsKey(nameof(DataSourceMetadata)));

        var model = AIProfileViewModel.FromProfile(profile);

        Assert.Equal("4kwqvhkg2p1jx30rrjn7vqv6da", model.DataSourceId);
        Assert.True(model.DataSourceIsInScope);
        Assert.Equal(5, model.DocumentTopN);
    }

    [Fact]
    public void As_ShouldReadDictionaryBackedExtensionData()
    {
        var profile = new AIProfile
        {
            Properties = new Dictionary<string, object>
            {
                [nameof(DataSourceMetadata)] = new Dictionary<string, object>
                {
                    [nameof(DataSourceMetadata.DataSourceId)] = "dictionary-source",
                },
                [nameof(AIDataSourceRagMetadata)] = JsonNode.Parse("""{"IsInScope":true}"""),
            },
        };

        Assert.Equal("dictionary-source", profile.As<DataSourceMetadata>().DataSourceId);
        Assert.True(profile.As<AIDataSourceRagMetadata>().IsInScope);
    }

    [Fact]
    public void JsonSerialization_ShouldKeepSettingsNestedAndMetadataFlattened()
    {
        var profile = new AIProfile
        {
            Name = "Test",
        };

        profile.AlterSettings<AIProfileSettings>(settings =>
        {
            settings.IsListable = true;
            settings.IsRemovable = false;
        });
        profile.Alter<DataSourceMetadata>(metadata => metadata.DataSourceId = "serialized-source");

        var json = JsonSerializer.Serialize(profile);

        Assert.Contains("\"Settings\":", json, StringComparison.Ordinal);
        Assert.Contains("\"AIProfileSettings\":", json, StringComparison.Ordinal);
        Assert.Contains("\"DataSourceMetadata\":", json, StringComparison.Ordinal);

        var reloaded = JsonSerializer.Deserialize<AIProfile>(json);

        Assert.NotNull(reloaded);
        Assert.True(reloaded.Properties.ContainsKey(nameof(DataSourceMetadata)));
        Assert.True(reloaded.GetSettings<AIProfileSettings>().IsListable);
        Assert.False(reloaded.GetSettings<AIProfileSettings>().IsRemovable);
        Assert.Equal("serialized-source", reloaded.As<DataSourceMetadata>().DataSourceId);
    }
}
