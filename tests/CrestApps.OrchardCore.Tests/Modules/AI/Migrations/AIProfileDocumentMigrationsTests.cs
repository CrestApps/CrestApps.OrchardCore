using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class AIProfileDocumentMigrationsTests
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task NewAsync_WhenLegacyProfileUsesNestedPropertiesObject_ShouldPopulateMetadataAndSettings()
    {
        // Arrange
        var token = CreateLegacyProfileToken();
        var manager = CreateManager([]);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var profile = await manager.NewAsync(token, cancellationToken);

        // Assert
        var metadata = profile.GetOrCreate<AIProfileMetadata>();
        Assert.Equal("system message into", metadata.SystemMessage);
        Assert.Equal(400, metadata.MaxTokens);
        Assert.Equal(10, metadata.PastMessagesCount);
        Assert.True(metadata.UseCaching);

        var functionMetadata = profile.GetOrCreate<FunctionInvocationMetadata>();
        Assert.Equal(["getUserInfo", "searchForUsers"], functionMetadata.Names);

        var extractionSettings = profile.GetSettings<AIProfileDataExtractionSettings>();
        Assert.True(extractionSettings.EnableDataExtraction);
        Assert.Single(extractionSettings.DataExtractionEntries);
        Assert.Equal("FirstName", extractionSettings.DataExtractionEntries[0].Name);

        var postSessionSettings = profile.GetSettings<AIProfilePostSessionSettings>();
        Assert.True(postSessionSettings.EnablePostSessionProcessing);
        Assert.Equal(["listAIProfiles", "viewAIProfile"], postSessionSettings.ToolNames);
        Assert.Single(postSessionSettings.PostSessionTasks);
        Assert.Equal("test", postSessionSettings.PostSessionTasks[0].Name);

        var documentsMetadata = profile.GetOrCreate<DocumentsMetadata>();
        Assert.Equal(3, documentsMetadata.DocumentTopN);
        Assert.Single(documentsMetadata.Documents);
        Assert.DoesNotContain(nameof(AIProfile.Properties), profile.Properties.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void NormalizePersistedProfileDocument_WhenLegacyNestedPropertiesExist_ShouldFlattenAndRenameLegacyKeys()
    {
        // Arrange
        var profileDocument = new JsonObject
        {
            [nameof(AIProfile.Name)] = "test",
            [nameof(AIProfile.Properties)] = new JsonObject
            {
                [nameof(AIProfileMetadata)] = new JsonObject
                {
                    [nameof(AIProfileMetadata.SystemMessage)] = "system message into",
                },
                ["AIProfileFunctionInvocationMetadata"] = new JsonObject
                {
                    [nameof(FunctionInvocationMetadata.Names)] = new JsonArray("getUserInfo"),
                },
                ["AIProfileDataSourceMetadata"] = new JsonObject
                {
                    ["DataSourceId"] = "data-source-1",
                },
            },
        };

        // Act
        var updated = InvokeNormalizePersistedProfileDocument(profileDocument);

        // Assert
        Assert.True(updated);
        Assert.DoesNotContain(nameof(AIProfile.Properties), profileDocument.Select(property => property.Key), StringComparer.Ordinal);
        Assert.DoesNotContain("AIProfileFunctionInvocationMetadata", profileDocument.Select(property => property.Key), StringComparer.Ordinal);
        Assert.DoesNotContain("AIProfileDataSourceMetadata", profileDocument.Select(property => property.Key), StringComparer.Ordinal);

        var profile = JsonSerializer.Deserialize<AIProfile>(profileDocument.ToJsonString(), _jsonSerializerOptions);
        Assert.NotNull(profile);

        var metadata = profile.GetOrCreate<AIProfileMetadata>();
        Assert.Equal("system message into", metadata.SystemMessage);

        var functionMetadata = profile.GetOrCreate<FunctionInvocationMetadata>();
        Assert.Equal(["getUserInfo"], functionMetadata.Names);

        var dataSourceMetadata = profile.Get<DataSourceMetadata>("DataSourceMetadata");
        Assert.Equal("data-source-1", dataSourceMetadata.DataSourceId);
    }

    [Fact]
    public void NormalizePersistedProfileDocument_WhenPersistedProfileContainsMetadataProperties_ShouldPopulateTypedMetadata()
    {
        // Arrange
        var profileDocument = JsonNode.Parse(
            """
            {"Name":"Test","DisplayText":"test","Type":"Chat","ChatDeploymentName":"gpt-4.1","UtilityDeploymentName":"gpt-4.1-mini","TitleType":"InitialPrompt","OrchestratorName":"default","CreatedUtc":"2026-04-20T20:19:53Z","OwnerId":"4674z7xss07cj6qqy6dsyz2zd6","Author":"malhayek","Settings":{"AIChatProfileSettings":{"IsOnAdminMenu":true},"AIProfileDataExtractionSettings":{"EnableDataExtraction":true,"ExtractionCheckInterval":1,"SessionInactivityTimeoutInMinutes":30,"DataExtractionEntries":[{"Name":"customer_name","Description":"dddd","AllowMultipleValues":false,"IsUpdatable":true}]},"AIProfilePostSessionSettings":{"EnablePostSessionProcessing":true,"PostSessionTasks":[{"Name":"asdfsdf","Type":1,"Instructions":"asdfasdfsd","AllowMultipleValues":false,"Options":[]}],"ToolNames":["queryChatSessionMetrics"]},"ChatModeProfileSettings":{"ChatMode":0}},"ItemId":"4vtqzsypttpg1vh82sxy820pcr","Properties":{"AIProfileMetadata":{"SystemMessage":"System instructions","Temperature":0,"TopP":1,"FrequencyPenalty":0,"PresencePenalty":0,"MaxTokens":400,"PastMessagesCount":10,"UseCaching":true},"FunctionInvocationMetadata":{"Names":["queryChatSessionMetrics"]},"AgentInvocationMetadata":{"Names":["data-analyst-agent","reviewer-agent"]},"PromptTemplateMetadata":{"Templates":[]},"AnalyticsMetadata":{"EnableSessionMetrics":true,"EnableAIResolutionDetection":true,"EnableConversionMetrics":true,"ConversionGoals":[{"Name":"ssss","Description":"sssss","MinScore":0,"MaxScore":10}]},"DataSourceMetadata":{"DataSourceId":"4vm3c6hjfyh0c77yb9jcjh6edp"},"AIDataSourceRagMetadata":{"Strictness":3,"TopNDocuments":5,"IsInScope":true},"AIProfileSessionDocumentsMetadata":{"AllowSessionDocuments":true},"DocumentsMetadata":{"Documents":[{"DocumentId":"4htwsv6389zaa5t3nj41kkg6dm","FileName":"12-CarRace-by-Starfall.pdf","ContentType":"application/pdf","FileSize":3176542}],"DocumentTopN":3}},"MemoryMetadata":{"EnableUserMemory":false}}
            """) as JsonObject;

        Assert.NotNull(profileDocument);

        // Act
        var updated = InvokeNormalizePersistedProfileDocument(profileDocument);
        var profile = JsonSerializer.Deserialize<AIProfile>(profileDocument.ToJsonString(), _jsonSerializerOptions);

        // Assert
        Assert.True(updated);
        Assert.NotNull(profile);
        Assert.DoesNotContain(nameof(AIProfile.Properties), profileDocument.Select(property => property.Key), StringComparer.Ordinal);

        var metadata = profile.GetOrCreate<AIProfileMetadata>();
        Assert.Equal("System instructions", metadata.SystemMessage);
        Assert.Equal(400, metadata.MaxTokens);
        Assert.Equal(10, metadata.PastMessagesCount);

        var extractionSettings = profile.GetSettings<AIProfileDataExtractionSettings>();
        Assert.True(extractionSettings.EnableDataExtraction);
        Assert.Single(extractionSettings.DataExtractionEntries);

        var functionMetadata = profile.GetOrCreate<FunctionInvocationMetadata>();
        Assert.Equal(["queryChatSessionMetrics"], functionMetadata.Names);
    }

    private static NamedCatalogManager<AIProfile> CreateManager(IEnumerable<AIProfile> profiles)
    {
        var catalog = new TestNamedCatalog(profiles);
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(new DefaultHttpContext());

        var deploymentCatalog = new Mock<INamedCatalog<AIDeployment>>();
        var liquidTemplateManager = new Mock<ILiquidTemplateManager>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 3, 25, 19, 47, 47, DateTimeKind.Utc));

        var handler = new AIProfileHandler(
            deploymentCatalog.Object,
            liquidTemplateManager.Object,
            Mock.Of<IStringLocalizer<AIProfileHandler>>());

        var logger = Mock.Of<ILogger<NamedCatalogManager<AIProfile>>>();

        return new NamedCatalogManager<AIProfile>(catalog, [handler], logger);
    }

    private static JsonObject CreateLegacyProfileToken()
    {
        return new JsonObject
        {
            [nameof(AIProfile.Name)] = "Test",
            [nameof(AIProfile.DisplayText)] = "Test",
            [nameof(AIProfile.Type)] = nameof(AIProfileType.Chat),
            [nameof(AIProfile.TitleType)] = nameof(AISessionTitleType.InitialPrompt),
            [nameof(AIProfile.CreatedUtc)] = "2026-03-25T19:47:47Z",
            [nameof(AIProfile.OwnerId)] = "4gmhenp43fk4d123e4rw22705k",
            [nameof(AIProfile.Author)] = "malhayek",
            [nameof(AIProfile.Source)] = "Azure",
            [nameof(AIProfile.ItemId)] = "4shwgj945z1fc4mphc1y40ddmm",
            [nameof(AIProfile.Settings)] = new JsonObject
            {
                [nameof(AIChatProfileSettings)] = new JsonObject
                {
                    [nameof(AIChatProfileSettings.IsOnAdminMenu)] = false,
                },
                [nameof(AIProfileDataExtractionSettings)] = new JsonObject
                {
                    [nameof(AIProfileDataExtractionSettings.EnableDataExtraction)] = true,
                    [nameof(AIProfileDataExtractionSettings.ExtractionCheckInterval)] = 1,
                    ["SessionInactivityTimeoutInMinutes"] = 30,
                    [nameof(AIProfileDataExtractionSettings.DataExtractionEntries)] = new JsonArray
                    {
                        new JsonObject
                        {
                            [nameof(DataExtractionEntry.Name)] = "FirstName",
                            [nameof(DataExtractionEntry.Description)] = "First name",
                            [nameof(DataExtractionEntry.AllowMultipleValues)] = false,
                            [nameof(DataExtractionEntry.IsUpdatable)] = false,
                        },
                    },
                },
                [nameof(AIProfilePostSessionSettings)] = new JsonObject
                {
                    [nameof(AIProfilePostSessionSettings.EnablePostSessionProcessing)] = true,
                    [nameof(AIProfilePostSessionSettings.PostSessionTasks)] = new JsonArray
                    {
                        new JsonObject
                        {
                            [nameof(PostSessionTask.Name)] = "test",
                            [nameof(PostSessionTask.Type)] = (int)PostSessionTaskType.Semantic,
                            [nameof(PostSessionTask.Instructions)] = "this is a test",
                            [nameof(PostSessionTask.AllowMultipleValues)] = false,
                            [nameof(PostSessionTask.Options)] = new JsonArray(),
                        },
                    },
                    [nameof(AIProfilePostSessionSettings.ToolNames)] = new JsonArray("listAIProfiles", "viewAIProfile"),
                },
                [nameof(ChatModeProfileSettings)] = new JsonObject
                {
                    [nameof(ChatModeProfileSettings.ChatMode)] = (int)ChatMode.Conversation,
                },
            },
            [nameof(AIProfile.Properties)] = new JsonObject
            {
                [nameof(AIProfileMetadata)] = new JsonObject
                {
                    [nameof(AIProfileMetadata.SystemMessage)] = "system message into",
                    [nameof(AIProfileMetadata.Temperature)] = 0,
                    [nameof(AIProfileMetadata.TopP)] = 1,
                    [nameof(AIProfileMetadata.FrequencyPenalty)] = 0,
                    [nameof(AIProfileMetadata.PresencePenalty)] = 0,
                    [nameof(AIProfileMetadata.MaxTokens)] = 400,
                    [nameof(AIProfileMetadata.PastMessagesCount)] = 10,
                    [nameof(AIProfileMetadata.UseCaching)] = true,
                },
                [nameof(FunctionInvocationMetadata)] = new JsonObject
                {
                    [nameof(FunctionInvocationMetadata.Names)] = new JsonArray("getUserInfo", "searchForUsers"),
                },
                [nameof(AgentInvocationMetadata)] = new JsonObject
                {
                    [nameof(AgentInvocationMetadata.Names)] = new JsonArray(),
                },
                [nameof(PromptTemplateMetadata)] = new JsonObject(),
                [nameof(AnalyticsMetadata)] = new JsonObject
                {
                    [nameof(AnalyticsMetadata.EnableSessionMetrics)] = false,
                    [nameof(AnalyticsMetadata.EnableAIResolutionDetection)] = true,
                    [nameof(AnalyticsMetadata.EnableConversionMetrics)] = false,
                    [nameof(AnalyticsMetadata.ConversionGoals)] = new JsonArray(),
                },
                ["DataSourceMetadata"] = new JsonObject(),
                [nameof(AIDataSourceRagMetadata)] = new JsonObject
                {
                    [nameof(AIDataSourceRagMetadata.Strictness)] = 3,
                    [nameof(AIDataSourceRagMetadata.TopNDocuments)] = 5,
                    [nameof(AIDataSourceRagMetadata.IsInScope)] = true,
                },
                [nameof(AIProfileSessionDocumentsMetadata)] = new JsonObject
                {
                    [nameof(AIProfileSessionDocumentsMetadata.AllowSessionDocuments)] = true,
                },
                [nameof(DocumentsMetadata)] = new JsonObject
                {
                    [nameof(DocumentsMetadata.Documents)] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["DocumentId"] = "4nmj7gp8f5qeysg0dxvfg3pdx6",
                            ["FileName"] = "12-CarRace-by-Starfall.pdf",
                            ["ContentType"] = "application/pdf",
                            ["FileSize"] = 3176542,
                        },
                    },
                    [nameof(DocumentsMetadata.DocumentTopN)] = 3,
                },
            },
        };
    }

    private static bool InvokeNormalizePersistedProfileDocument(JsonObject profileDocument)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Migrations.AIProfileDocumentMigrations", throwOnError: true)!
            .GetMethod("NormalizePersistedProfileDocument", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (bool)method.Invoke(null, [profileDocument]);
    }

    private sealed class TestNamedCatalog : INamedCatalog<AIProfile>
    {
        private readonly List<AIProfile> _profiles;

        public TestNamedCatalog(IEnumerable<AIProfile> profiles)
        {
            _profiles = profiles.ToList();
        }

        public ValueTask<AIProfile> FindByIdAsync(
            string id,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_profiles.FirstOrDefault(profile => string.Equals(profile.ItemId, id, StringComparison.Ordinal)));

        public ValueTask<IReadOnlyCollection<AIProfile>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<AIProfile>>(_profiles);

        public ValueTask<IReadOnlyCollection<AIProfile>> GetAsync(
            IEnumerable<string> ids,
            CancellationToken cancellationToken = default)
        {
            var idSet = ids.ToHashSet(StringComparer.Ordinal);

            return ValueTask.FromResult<IReadOnlyCollection<AIProfile>>(
                _profiles.Where(profile => idSet.Contains(profile.ItemId)).ToList());
        }

        public ValueTask<PageResult<AIProfile>> PageAsync<TQuery>(
            int page,
            int pageSize,
            TQuery context,
            CancellationToken cancellationToken = default)
            where TQuery : QueryContext
        {
            return ValueTask.FromResult(new PageResult<AIProfile>
            {
                Count = _profiles.Count,
                Entries = _profiles,
            });
        }

        public ValueTask<bool> DeleteAsync(
            AIProfile entry,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_profiles.Remove(entry));

        public ValueTask CreateAsync(
            AIProfile entry,
            CancellationToken cancellationToken = default)
        {
            _profiles.RemoveAll(profile => string.Equals(profile.ItemId, entry.ItemId, StringComparison.Ordinal));
            _profiles.Add(entry);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(
            AIProfile entry,
            CancellationToken cancellationToken = default)
            => CreateAsync(entry, cancellationToken);

        public ValueTask<AIProfile> FindByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                _profiles.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.Ordinal)));
        }
    }
}
