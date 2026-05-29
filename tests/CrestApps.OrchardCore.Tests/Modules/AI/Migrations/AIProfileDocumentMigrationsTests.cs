using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AIStartup = CrestApps.OrchardCore.AI.Startup;
using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Chat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class AIProfileDocumentMigrationsTests
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter(),
        },
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

    private static NamedCatalogManager<AIProfile> CreateManager(IEnumerable<AIProfile> profiles)
    {
        var catalog = new TestNamedCatalog(profiles);
        var coreHandler = CreateCoreProfileHandler(catalog);
        var logger = Mock.Of<ILogger<NamedCatalogManager<AIProfile>>>();

        return new NamedCatalogManager<AIProfile>(catalog, [coreHandler], logger);
    }

    private static ICatalogEntryHandler<AIProfile> CreateCoreProfileHandler(INamedCatalog<AIProfile> catalog)
    {
        var handlerAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "CrestApps.Core.AI", StringComparison.Ordinal))
            ?? Assembly.Load("CrestApps.Core.AI");
        var handlerType = handlerAssembly.GetType("CrestApps.Core.AI.Handlers.AIProfileHandler");
        Assert.NotNull(handlerType);
        var constructor = Assert.Single(handlerType.GetConstructors());
        var handler = constructor.Invoke(constructor.GetParameters()
            .Select(parameter => ResolveCoreProfileHandlerArgument(parameter.ParameterType, handlerType, catalog))
            .ToArray());

        return Assert.IsType<ICatalogEntryHandler<AIProfile>>(handler, exactMatch: false);
    }

    private static object ResolveCoreProfileHandlerArgument(Type parameterType, Type handlerType, INamedCatalog<AIProfile> catalog)
    {
        if (parameterType == typeof(INamedCatalog<AIProfile>))
        {
            return catalog;
        }

        if (parameterType == typeof(IAIProfileStore))
        {
            return catalog;
        }

        if (parameterType == typeof(INamedSourceCatalog<AIDeployment>))
        {
            return new TestDeploymentCatalog();
        }

        if (parameterType == typeof(IAIDeploymentStore))
        {
            return CreateDeploymentStore();
        }

        if (parameterType == typeof(ILiquidTemplateManager))
        {
            return Mock.Of<ILiquidTemplateManager>();
        }

        if (parameterType == typeof(IHttpContextAccessor))
        {
            return new HttpContextAccessor();
        }

        if (parameterType == typeof(TimeProvider))
        {
            return TimeProvider.System;
        }

        if (parameterType == typeof(IStringLocalizer<>).MakeGenericType(handlerType))
        {
            var localizerType = typeof(PassThroughStringLocalizer<>).MakeGenericType(handlerType);
            var localizer = Activator.CreateInstance(localizerType);
            Assert.NotNull(localizer);

            return localizer;
        }

        if (parameterType.IsGenericType &&
            parameterType.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            var loggerType = typeof(NullLogger<>).MakeGenericType(parameterType.GenericTypeArguments[0]);
            var instanceProperty = loggerType.GetProperty(nameof(NullLogger<object>.Instance), BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(instanceProperty);

            var logger = instanceProperty.GetValue(null);
            Assert.NotNull(logger);

            return logger;
        }

        throw new InvalidOperationException($"Unsupported {handlerType.FullName} constructor dependency: {parameterType.FullName}");
    }

    private static IAIDeploymentStore CreateDeploymentStore()
    {
        var deploymentStore = new Mock<IAIDeploymentStore>();
        deploymentStore.Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AIDeployment>());
        deploymentStore.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);
        deploymentStore.Setup(store => store.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);

        return deploymentStore.Object;
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

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

        public PassThroughStringLocalizer<T> WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private static bool InvokeNormalizePersistedProfileDocument(JsonObject profileDocument)
    {
        var method = typeof(AIStartup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Migrations.AIProfileDocumentMigrations", throwOnError: true)!
            .GetMethod("NormalizePersistedProfileDocument", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (bool)method.Invoke(null, [profileDocument]);
    }

    private sealed class TestNamedCatalog : IAIProfileStore
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

        public ValueTask<IReadOnlyCollection<AIProfile>> GetByTypeAsync(
            AIProfileType type,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyCollection<AIProfile>>(
                _profiles.Where(profile => profile.Type == type).ToList());
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

    private sealed class TestDeploymentCatalog : INamedSourceCatalog<AIDeployment>
    {
        public ValueTask<AIDeployment> FindByIdAsync(
            string id,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AIDeployment>(null);

        public ValueTask<IReadOnlyCollection<AIDeployment>> GetAllAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<AIDeployment>>(Array.Empty<AIDeployment>());

        public ValueTask<IReadOnlyCollection<AIDeployment>> GetAsync(
            IEnumerable<string> ids,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<AIDeployment>>(Array.Empty<AIDeployment>());

        public ValueTask<PageResult<AIDeployment>> PageAsync<TQuery>(
            int page,
            int pageSize,
            TQuery context,
            CancellationToken cancellationToken = default)
            where TQuery : QueryContext
        {
            return ValueTask.FromResult(new PageResult<AIDeployment>
            {
                Count = 0,
                Entries = [],
            });
        }

        public ValueTask<bool> DeleteAsync(
            AIDeployment entry,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);

        public ValueTask CreateAsync(
            AIDeployment entry,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask UpdateAsync(
            AIDeployment entry,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<AIDeployment> FindByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AIDeployment>(null);

        public ValueTask<AIDeployment> GetAsync(string name, string source, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<AIDeployment>(null);

        public ValueTask<IReadOnlyCollection<AIDeployment>> GetAsync(string source, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyCollection<AIDeployment>>([]);
    }
}
