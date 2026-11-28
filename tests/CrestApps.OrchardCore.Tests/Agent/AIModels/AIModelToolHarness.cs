using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Connections;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Speech;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tests.Agent.AIConfiguration;
using CrestApps.OrchardCore.Tests.Agent.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Media;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Agent.AIModels;

/// <summary>
/// Builds the services the provider connection, deployment and voice tools use, on in-memory lists: connections that
/// behave like the provider handlers (a key is protected, an OpenAI connection needs one), deployments with the
/// manager's checks, the site's default deployments, a voice library and a media library.
/// </summary>
internal sealed class AIModelToolHarness
{
    public const string OpenAI = "OpenAI";

    public const string Azure = "Azure";

    public const string ElevenLabs = "ElevenLabs";

    public bool SignedIn { get; set; } = true;

    public Func<Permission, object, bool> Authorize { get; set; } = (_, _) => true;

    public List<string> Checks { get; } = [];

    public List<AIProviderConnection> Connections { get; } = [];

    public List<AIDeployment> Deployments { get; } = [];

    public InMemoryProfileManager Profiles { get; } = new();

    public DefaultAIDeploymentSettings Defaults { get; } = new();

    public List<JsonNode> ConnectionPayloads { get; } = [];

    public List<AIDeployment> UpdatedDeployments { get; } = [];

    public int ReleaseRequests { get; private set; }

    public FakeVoiceLibrary Voices { get; } = new();

    public InMemoryMediaFileStore Media { get; } = new();

    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn);
        services.AddSingleton<IAuthorizationService>(new HarnessAuthorizationService(this));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        services.AddSingleton<IAIProfileManager>(Profiles);
        services.AddSingleton(CreateConnectionManager());
        services.AddSingleton(CreateConnectionStore());

        var deploymentManager = CreateDeploymentManager();
        services.AddSingleton(deploymentManager);
        services.AddSingleton<INamedSourceCatalogManager<AIDeployment>>(deploymentManager);

        services.AddSingleton(CreateSiteService());
        services.AddSingleton(CreateReleaseManager());
        services.AddSingleton(CreateCapabilityService());
        services.AddSingleton(CreateVoiceLibraryResolver());
        services.AddSingleton<IMediaFileStore>(Media);

        services.Configure<AIOptions>(options =>
        {
            options.AddConnectionSource(OpenAI);
            options.AddConnectionSource(Azure);
            options.AddDeploymentProvider(OpenAI);
            options.AddDeploymentProvider(Azure);
            options.AddDeploymentProvider(ElevenLabs, entry => entry.UseContainedConnection = true);
        });

        return services.BuildServiceProvider();
    }

    public AIProviderConnection AddConnection(string id, string source, string name, string endpoint = "https://api.example.com/", string protectedKey = "CipherText", Action<AIProviderConnection> configure = null)
    {
        var metadataKey = source == OpenAI ? "OpenAIConnectionMetadata" : "AzureConnectionMetadata";
        var metadata = new JsonObject { ["Endpoint"] = endpoint };

        if (protectedKey is not null)
        {
            metadata["ApiKey"] = protectedKey;
        }

        if (source == Azure)
        {
            metadata["AuthenticationType"] = protectedKey is null ? "Default" : "ApiKey";
        }

        var connection = new AIProviderConnection
        {
            ItemId = id,
            Source = source,
            Name = name,
            DisplayText = name,
            Properties = new Dictionary<string, object> { [metadataKey] = metadata },
        };

        configure?.Invoke(connection);
        Connections.Add(connection);

        return connection;
    }

    public AIDeployment AddDeployment(string id, string source, string name, string connectionName = null, Action<AIDeployment> configure = null)
    {
        var deployment = new AIDeployment
        {
            ItemId = id,
            Source = source,
            Name = name,
            ModelName = name,
            ConnectionName = connectionName,
        };

        deployment.Put(new AIDeploymentMetadata { Features = ["textGeneration"] });
        configure?.Invoke(deployment);
        Deployments.Add(deployment);

        return deployment;
    }

    private INamedSourceCatalogManager<AIProviderConnection> CreateConnectionManager()
    {
        var manager = new Mock<INamedSourceCatalogManager<AIProviderConnection>>();

        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(Connections.FirstOrDefault(c => c.ItemId == id)));
        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IEnumerable<AIProviderConnection>>(Connections.ToList()));
        manager.Setup(m => m.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode data, CancellationToken _) =>
            {
                ConnectionPayloads.Add(data?.DeepClone());

                var connection = new AIProviderConnection
                {
                    ItemId = "new-connection",
                    Source = source,
                    Name = data?["Name"]?.GetValue<string>(),
                    DisplayText = data?["DisplayText"]?.GetValue<string>(),
                    Properties = new Dictionary<string, object>(),
                };

                Populate(connection, data);

                return ValueTask.FromResult(connection);
            });
        manager.Setup(m => m.ValidateAsync(It.IsAny<AIProviderConnection>(), It.IsAny<CancellationToken>()))
            .Returns((AIProviderConnection connection, CancellationToken _) =>
            {
                var result = new ValidationResultDetails();
                var metadata = GetMetadata(connection);

                if (connection.Source == OpenAI && string.IsNullOrEmpty(metadata?["ApiKey"]?.GetValue<string>()))
                {
                    result.Fail(new ValidationResult("ApiKey is required for OpenAI connection.", ["ApiKey"]));
                }

                if (Connections.Any(c => c.ItemId != connection.ItemId && string.Equals(c.Name, connection.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Fail(new ValidationResult("A connection with this name already exists. The name must be unique.", ["Name"]));
                }

                return ValueTask.FromResult(result);
            });
        manager.Setup(m => m.CreateAsync(It.IsAny<AIProviderConnection>(), It.IsAny<CancellationToken>()))
            .Returns((AIProviderConnection connection, CancellationToken _) =>
            {
                Connections.Add(connection);

                return ValueTask.CompletedTask;
            });
        manager.Setup(m => m.UpdateAsync(It.IsAny<AIProviderConnection>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((AIProviderConnection connection, JsonNode data, CancellationToken _) =>
            {
                ConnectionPayloads.Add(data?.DeepClone());

                if (data?["DisplayText"] is JsonValue title)
                {
                    connection.DisplayText = title.GetValue<string>();
                }

                Populate(connection, data);

                return ValueTask.CompletedTask;
            });
        manager.Setup(m => m.DeleteAsync(It.IsAny<AIProviderConnection>(), It.IsAny<CancellationToken>()))
            .Returns((AIProviderConnection connection, CancellationToken _) => ValueTask.FromResult(Connections.Remove(connection)));

        return manager.Object;
    }

    private IAIProviderConnectionStore CreateConnectionStore()
    {
        var store = new Mock<IAIProviderConnectionStore>();

        store.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIProviderConnection>>(Connections.ToList()));
        store.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string source, CancellationToken _) => ValueTask.FromResult<IReadOnlyCollection<AIProviderConnection>>(Connections.Where(c => c.Source == source).ToList()));

        return store.Object;
    }

    private IAIDeploymentManager CreateDeploymentManager()
    {
        var manager = new Mock<IAIDeploymentManager>();

        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(Deployments.FirstOrDefault(d => d.ItemId == id)));
        manager.Setup(m => m.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string name, CancellationToken _) => ValueTask.FromResult(Deployments.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))));
        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IEnumerable<AIDeployment>>(Deployments.ToList()));
        manager.Setup(m => m.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode _, CancellationToken _) => ValueTask.FromResult(new AIDeployment { ItemId = "new-deployment", Source = source }));
        manager.Setup(m => m.ValidateAsync(It.IsAny<AIDeployment>(), It.IsAny<CancellationToken>()))
            .Returns((AIDeployment deployment, CancellationToken _) =>
            {
                var result = new ValidationResultDetails();

                if (string.IsNullOrWhiteSpace(deployment.Name))
                {
                    result.Fail(new ValidationResult("Deployment Name is required."));
                }

                if (!deployment.TryGet<AIDeploymentMetadata>(out var metadata) || metadata.Features is not { Length: > 0 })
                {
                    result.Fail(new ValidationResult("At least one model capability is required."));
                }

                return ValueTask.FromResult(result);
            });
        manager.Setup(m => m.CreateAsync(It.IsAny<AIDeployment>(), It.IsAny<CancellationToken>()))
            .Returns((AIDeployment deployment, CancellationToken _) =>
            {
                Deployments.Add(deployment);

                return ValueTask.CompletedTask;
            });
        manager.Setup(m => m.UpdateAsync(It.IsAny<AIDeployment>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((AIDeployment deployment, JsonNode _, CancellationToken _) =>
            {
                UpdatedDeployments.Add(deployment);

                return ValueTask.CompletedTask;
            });
        manager.Setup(m => m.DeleteAsync(It.IsAny<AIDeployment>(), It.IsAny<CancellationToken>()))
            .Returns((AIDeployment deployment, CancellationToken _) => ValueTask.FromResult(Deployments.Remove(deployment)));
        manager.Setup(m => m.ResolveSlotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns((string slot, string name, string _, IReadOnlyDictionary<string, string> _, CancellationToken _) =>
            {
                var fallback = slot == AIDeploymentSlotNames.Utility ? Defaults.DefaultUtilityDeploymentName ?? Defaults.DefaultChatDeploymentName : Defaults.DefaultChatDeploymentName;
                var wanted = string.IsNullOrEmpty(name) ? fallback : name;

                return ValueTask.FromResult(Deployments.FirstOrDefault(d => string.Equals(d.Name, wanted, StringComparison.OrdinalIgnoreCase)));
            });

        return manager.Object;
    }

    private ISiteService CreateSiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.Properties).Returns(() => new JsonObject());
        site.Setup(s => s.GetOrCreate<DefaultAIDeploymentSettings>()).Returns(() => Defaults);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }

    private IShellReleaseManager CreateReleaseManager()
    {
        var manager = new Mock<IShellReleaseManager>();
        manager.Setup(m => m.RequestRelease()).Callback(() => ReleaseRequests++);

        return manager.Object;
    }

    private IAIDeploymentCapabilityService CreateCapabilityService()
    {
        var service = new Mock<IAIDeploymentCapabilityService>();

        service.Setup(s => s.GetRegisteredFeatures()).Returns(
        [
            new AIDeploymentFeatureDescriptor { Name = "textGeneration", EnabledByDefault = true, Order = 1 },
            new AIDeploymentFeatureDescriptor { Name = "toolCalling", Order = 2 },
            new AIDeploymentFeatureDescriptor { Name = "textToSpeech", Order = 3 },
        ]);
        service.Setup(s => s.GetDeploymentsWithFeatureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string feature, CancellationToken _) => ValueTask.FromResult<IReadOnlyList<AIDeployment>>(Deployments
                .Where(d => d.TryGet<AIDeploymentMetadata>(out var metadata) && metadata.Features.Contains(feature))
                .ToList()));

        return service.Object;
    }

    private IVoiceLibraryResolver CreateVoiceLibraryResolver()
    {
        var resolver = new Mock<IVoiceLibraryResolver>();

        resolver.Setup(r => r.GetLibraryAsync(It.IsAny<AIDeployment>(), It.IsAny<CancellationToken>()))
            .Returns((AIDeployment deployment, CancellationToken _) => Task.FromResult<IVoiceLibrary>(deployment.Source == ElevenLabs ? Voices : null));

        return resolver.Object;
    }

    private static JsonObject GetMetadata(AIProviderConnection connection)
    {
        var key = connection.Source == OpenAI ? "OpenAIConnectionMetadata" : "AzureConnectionMetadata";

        return connection.Properties.TryGetValue(key, out var value)
            ? JsonSerializer.SerializeToNode(value) as JsonObject
            : null;
    }

    // Writes the connection fields the way the provider handlers do: a key given in the payload is protected, one left
    // out is kept, and the authentication fields are reset to what the payload says.
    private static void Populate(AIProviderConnection connection, JsonNode data)
    {
        if (data is not JsonObject json || !(json.ContainsKey("Endpoint") || json.ContainsKey("AuthenticationType") || json.ContainsKey("IdentityId") || json.ContainsKey("ApiKey")))
        {
            return;
        }

        var key = connection.Source == OpenAI ? "OpenAIConnectionMetadata" : "AzureConnectionMetadata";
        var metadata = GetMetadata(connection) ?? new JsonObject();

        if (json["Endpoint"] is JsonValue endpoint)
        {
            metadata["Endpoint"] = endpoint.GetValue<string>();
        }

        if (connection.Source == Azure)
        {
            metadata["AuthenticationType"] = json["AuthenticationType"]?.GetValue<string>() ?? "Default";
            metadata["IdentityId"] = json["IdentityId"]?.GetValue<string>();
        }

        if (json["ApiKey"] is JsonValue apiKey)
        {
            metadata["ApiKey"] = "Protected:" + apiKey.GetValue<string>();
        }

        connection.Properties[key] = metadata;
    }

    private sealed class HarnessAuthorizationService : IAuthorizationService
    {
        private readonly AIModelToolHarness _harness;

        public HarnessAuthorizationService(AIModelToolHarness harness)
        {
            _harness = harness;
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            foreach (var requirement in requirements.OfType<PermissionRequirement>())
            {
                _harness.Checks.Add(requirement.Permission.Name);

                if (user?.Identity?.IsAuthenticated != true || !_harness.Authorize(requirement.Permission, resource))
                {
                    return Task.FromResult(AuthorizationResult.Failed());
                }
            }

            return Task.FromResult(AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}

/// <summary>
/// A voice library that keeps its voices in a list and records what was cloned and deleted.
/// </summary>
internal sealed class FakeVoiceLibrary : IVoiceLibrary
{
    public bool SupportsCloning { get; set; } = true;

    public List<SpeechVoice> All { get; } =
    [
        new SpeechVoice { Id = "voice-calm", Name = "Calm Clara", Language = "en", Gender = SpeechVoiceGender.Female },
        new SpeechVoice { Id = "voice-bold", Name = "Bold Ben", Language = "en", Gender = SpeechVoiceGender.Male },
    ];

    public List<VoiceSearchRequest> Searches { get; } = [];

    public List<VoiceCloneRequest> Clones { get; } = [];

    public List<string> Deleted { get; } = [];

    public string Failure { get; set; }

    public Task<VoiceSearchResult> SearchAsync(VoiceSearchRequest request, CancellationToken cancellationToken = default)
    {
        Searches.Add(request);
        ThrowWhenFailing();

        var voices = All
            .Where(voice => string.IsNullOrEmpty(request.Query) || voice.Name.Contains(request.Query, StringComparison.OrdinalIgnoreCase))
            .Where(voice => request.Gender is null || voice.Gender == request.Gender)
            .ToList();

        return Task.FromResult(new VoiceSearchResult { Voices = voices, NextCursor = voices.Count > request.PageSize ? "next" : null });
    }

    public Task<SpeechVoice> CloneAsync(VoiceCloneRequest request, CancellationToken cancellationToken = default)
    {
        Clones.Add(request);
        ThrowWhenFailing();

        return Task.FromResult(new SpeechVoice { Id = "cloned-1", Name = request.Name, Language = request.Language });
    }

    public Task DeleteAsync(string voiceId, CancellationToken cancellationToken = default)
    {
        ThrowWhenFailing();
        Deleted.Add(voiceId);

        return Task.CompletedTask;
    }

    private void ThrowWhenFailing()
    {
        if (Failure is not null)
        {
            throw new HttpRequestException(Failure);
        }
    }
}

/// <summary>
/// A localizer that returns the text it is given, as the site does when no translation exists.
/// </summary>
internal sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name]
        => new(name, name);

    public LocalizedString this[string name, params object[] arguments]
        => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => [];
}
