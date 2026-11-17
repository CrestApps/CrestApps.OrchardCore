using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Agent.AIConfiguration;

/// <summary>
/// Builds the services the AI profile, template, persona and tool instance tools use, on in-memory stores: a signed-in
/// or anonymous caller, the rule that grants permissions (to the caller and, separately, to a visitor who is not signed
/// in), the tools the caller may use, browser API keys, published chat widgets and the admin widget's profile.
/// </summary>
internal sealed class AIConfigurationToolHarness
{
    public const string SafeToolName = "safeVisitorTool";

    public const string PowerfulToolName = "powerfulTool";

    public bool SignedIn { get; set; } = true;

    /// <summary>
    /// Gets or sets the rule that grants a permission on a resource to the signed-in caller.
    /// </summary>
    public Func<Permission, object, bool> Authorize { get; set; } = (_, _) => true;

    /// <summary>
    /// Gets or sets the rule that grants a permission on a resource to a visitor who is not signed in.
    /// </summary>
    public Func<Permission, object, bool> AuthorizeVisitor { get; set; } = (_, _) => false;

    /// <summary>
    /// Gets the tools the caller may not use.
    /// </summary>
    public HashSet<string> DeniedTools { get; } = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryProfileManager Profiles { get; } = new();

    public List<AIProfileTemplate> Templates { get; } = [];

    public List<AIProfileTemplate> ShippedTemplates { get; } = [];

    public List<AIPersona> Personas { get; } = [];

    public List<AIPersona> PersonaFiles { get; } = [];

    public List<AIToolInstance> Instances { get; } = [];

    public List<AIProfileVersion> Versions { get; } = [];

    public List<AIApiKey> ApiKeys { get; } = [];

    public Dictionary<string, int> PublishedWidgets { get; } = new(StringComparer.Ordinal);

    public string AdminWidgetProfileId { get; set; }

    public List<JsonNode> InstanceUpdates { get; } = [];

    public string ModelReply { get; set; } = "Hello from the model.";

    public List<AICompletionContext> Completions { get; } = [];

    public List<(string Permission, object Resource, bool Visitor)> Checks { get; } = [];

    public ServiceProvider Build()
    {
        var httpContext = new DefaultHttpContext();

        if (SignedIn)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, AgentToolTestContext.UserId),
                new Claim(ClaimTypes.Name, AgentToolTestContext.UserName),
            ], "Test"));
        }

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);

        var services = AgentToolTestContext.CreateServices(SignedIn);
        services.AddSingleton(httpContextAccessor.Object);
        services.AddSingleton<IAuthorizationService>(new HarnessAuthorizationService(this));

        services.AddSingleton<IAIProfileManager>(Profiles);
        services.AddSingleton(CreateTemplateManager());
        services.AddSingleton(CreateTemplateCatalog());
        services.AddSingleton(CreatePersonaManager());
        services.AddSingleton(CreatePersonaCatalog());
        services.AddSingleton(CreateInstanceManager());
        services.AddSingleton(CreateInstanceCatalog());
        services.AddSingleton(CreateVersionStore());
        services.AddSingleton(CreateApiKeyStore());
        services.AddSingleton<IPublishedChatWidgetFinder>(new FakeWidgetFinder(PublishedWidgets));
        services.AddSingleton(CreateSiteService());
        services.AddSingleton<IAIToolAccessEvaluator>(new FakeToolAccessEvaluator(DeniedTools));

        services.AddLogging();
        services.AddSingleton<AIProfileTemplateProfileFactory>();
        services.AddSingleton(CreateCompletionContextBuilder());
        services.AddSingleton(CreateDeploymentManager());
        services.AddSingleton(CreateCompletionService());

        services.Configure<AnonymousAIToolOptions>(options => options.MarkSafe(SafeToolName));
        services.Configure<AIOptions>(options =>
        {
            options.AddToolInstanceSource("http-api-request");
            options.AddToolInstanceSource("sitemap-documentation");
            options.AddToolInstanceSource("algolia-documentation");
            options.AddTemplateSource(AITemplateSources.Profile);
            options.AddTemplateSource(AITemplateSources.SystemPrompt);
        });

        services.AddCoreAITool<FakeTool>(SafeToolName).WithTitle("Safe").WithDescription("Safe").WithCategory("Test").Selectable();
        services.AddCoreAITool<FakeTool>(PowerfulToolName).WithTitle("Powerful").WithDescription("Powerful").WithCategory("Test").Selectable();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Starts an AI turn run by a profile, as the chat hub does.
    /// </summary>
    /// <param name="runningProfile">The profile that runs the turn.</param>
    /// <param name="sessionProfileId">The profile of the chat session, when it is another one.</param>
    /// <returns>The scope, to dispose when the turn ends.</returns>
    public static AIInvocationScopeHandle BeginTurn(AIProfile runningProfile, string sessionProfileId = null)
    {
        var context = new CrestApps.Core.AI.Orchestration.AIInvocationContext
        {
            ToolExecutionContext = runningProfile is null ? null : new AIToolExecutionContext(runningProfile),
            ChatSession = sessionProfileId is null ? null : new AIChatSession { ProfileId = sessionProfileId },
        };

        return new AIInvocationScopeHandle(CrestApps.Core.AI.Orchestration.AIInvocationScope.Begin(context));
    }

    public AIProfile AddProfile(string id, string name, AIProfileType type = AIProfileType.Chat, Action<AIProfile> configure = null)
    {
        var profile = new AIProfile
        {
            ItemId = id,
            Name = name,
            DisplayText = name,
            Type = type,
        };

        configure?.Invoke(profile);
        Profiles.Add(profile);

        return profile;
    }

    private IAIProfileTemplateManager CreateTemplateManager()
    {
        var manager = new Mock<IAIProfileTemplateManager>();
        IEnumerable<AIProfileTemplate> All() => Templates.Concat(ShippedTemplates.Where(shipped => !Templates.Any(t => t.Name == shipped.Name))).ToList();

        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(All()));
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(All().FirstOrDefault(t => t.ItemId == id)));
        manager.Setup(m => m.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string name, CancellationToken _) => ValueTask.FromResult(All().FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))));
        manager.Setup(m => m.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode _, CancellationToken _) => ValueTask.FromResult(new AIProfileTemplate { ItemId = "new-template", Source = source }));
        manager.Setup(m => m.ValidateAsync(It.IsAny<AIProfileTemplate>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));
        manager.Setup(m => m.CreateAsync(It.IsAny<AIProfileTemplate>(), It.IsAny<CancellationToken>()))
            .Returns((AIProfileTemplate template, CancellationToken _) =>
            {
                Templates.Add(template);

                return ValueTask.CompletedTask;
            });
        manager.Setup(m => m.UpdateAsync(It.IsAny<AIProfileTemplate>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        manager.Setup(m => m.DeleteAsync(It.IsAny<AIProfileTemplate>(), It.IsAny<CancellationToken>()))
            .Returns((AIProfileTemplate template, CancellationToken _) => ValueTask.FromResult(Templates.Remove(template)));

        return manager.Object;
    }

    private INamedSourceCatalog<AIProfileTemplate> CreateTemplateCatalog()
    {
        var catalog = new Mock<INamedSourceCatalog<AIProfileTemplate>>();

        catalog.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIProfileTemplate>>(Templates.ToList()));
        catalog.Setup(c => c.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(Templates.FirstOrDefault(t => t.ItemId == id)));
        catalog.Setup(c => c.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string name, CancellationToken _) => ValueTask.FromResult(Templates.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))));

        return catalog.Object;
    }

    private IAIPersonaManager CreatePersonaManager()
    {
        var manager = new Mock<IAIPersonaManager>();
        IEnumerable<AIPersona> All() => Personas.Concat(PersonaFiles.Where(file => !Personas.Any(p => p.Name == file.Name))).ToList();

        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(All()));
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(All().FirstOrDefault(p => p.ItemId == id)));
        manager.Setup(m => m.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string name, CancellationToken _) => ValueTask.FromResult(All().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))));
        manager.Setup(m => m.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string name, JsonNode _, CancellationToken _) => ValueTask.FromResult(new AIPersona { ItemId = "new-persona-" + name, Name = name }));
        manager.Setup(m => m.ValidateAsync(It.IsAny<AIPersona>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));
        manager.Setup(m => m.CreateAsync(It.IsAny<AIPersona>(), It.IsAny<CancellationToken>()))
            .Returns((AIPersona persona, CancellationToken _) =>
            {
                Personas.Add(persona);

                return ValueTask.CompletedTask;
            });
        manager.Setup(m => m.UpdateAsync(It.IsAny<AIPersona>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        manager.Setup(m => m.DeleteAsync(It.IsAny<AIPersona>(), It.IsAny<CancellationToken>()))
            .Returns((AIPersona persona, CancellationToken _) => ValueTask.FromResult(Personas.Remove(persona)));

        return manager.Object;
    }

    private INamedCatalog<AIPersona> CreatePersonaCatalog()
    {
        var catalog = new Mock<INamedCatalog<AIPersona>>();

        catalog.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIPersona>>(Personas.ToList()));
        catalog.Setup(c => c.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(Personas.FirstOrDefault(p => p.ItemId == id)));

        return catalog.Object;
    }

    private ISourceCatalogManager<AIToolInstance> CreateInstanceManager()
    {
        var manager = new Mock<ISourceCatalogManager<AIToolInstance>>();

        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(Instances.FirstOrDefault(i => i.ItemId == id)));
        manager.Setup(m => m.PageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<QueryContext>(), It.IsAny<CancellationToken>()))
            .Returns((int page, int pageSize, QueryContext _, CancellationToken _) => ValueTask.FromResult(new PageResult<AIToolInstance>
            {
                Count = Instances.Count,
                Entries = Instances.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            }));
        manager.Setup(m => m.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode data, CancellationToken _) =>
            {
                var instance = new AIToolInstance
                {
                    ItemId = "new-instance",
                    Source = source,
                    Name = data?["Name"]?.GetValue<string>(),
                    Description = data?["Description"]?.GetValue<string>(),
                };

                MergeProperties(instance, data);

                return ValueTask.FromResult(instance);
            });
        manager.Setup(m => m.ValidateAsync(It.IsAny<AIToolInstance>(), It.IsAny<CancellationToken>()))
            .Returns((AIToolInstance instance, CancellationToken _) =>
            {
                var result = new ValidationResultDetails();

                if (string.IsNullOrWhiteSpace(instance.Description))
                {
                    result.Fail(new ValidationResult("A description is required so the AI model can tell instances apart."));
                }

                return ValueTask.FromResult(result);
            });
        manager.Setup(m => m.CreateAsync(It.IsAny<AIToolInstance>(), It.IsAny<CancellationToken>()))
            .Returns((AIToolInstance instance, CancellationToken _) =>
            {
                Instances.Add(instance);

                return ValueTask.CompletedTask;
            });
        manager.Setup(m => m.UpdateAsync(It.IsAny<AIToolInstance>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((AIToolInstance instance, JsonNode data, CancellationToken _) =>
            {
                InstanceUpdates.Add(data?.DeepClone());

                if (data?["Description"] is JsonValue description)
                {
                    instance.Description = description.GetValue<string>();
                }

                MergeProperties(instance, data);

                return ValueTask.CompletedTask;
            });
        manager.Setup(m => m.DeleteAsync(It.IsAny<AIToolInstance>(), It.IsAny<CancellationToken>()))
            .Returns((AIToolInstance instance, CancellationToken _) => ValueTask.FromResult(Instances.Remove(instance)));

        return manager.Object;
    }

    private INamedCatalog<AIToolInstance> CreateInstanceCatalog()
    {
        var catalog = new Mock<INamedCatalog<AIToolInstance>>();

        catalog.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIToolInstance>>(Instances.ToList()));
        catalog.Setup(c => c.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string name, CancellationToken _) => ValueTask.FromResult(Instances.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase))));

        return catalog.Object;
    }

    private IAIProfileVersionStore CreateVersionStore()
    {
        var store = new Mock<IAIProfileVersionStore>();

        store.Setup(s => s.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => Versions.FirstOrDefault(v => v.ItemId == id));
        store.Setup(s => s.PageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string profileId, int page, int pageSize, CancellationToken _) =>
            {
                var versions = Versions.Where(v => v.ProfileId == profileId).OrderByDescending(v => v.Version).ToList();

                return ((IReadOnlyList<AIProfileVersion>)versions.Skip((page - 1) * pageSize).Take(pageSize).ToList(), versions.Count);
            });

        return store.Object;
    }

    private IAIApiKeyStore CreateApiKeyStore()
    {
        var store = new Mock<IAIApiKeyStore>();

        store.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIApiKey>>(ApiKeys.ToList()));

        return store.Object;
    }

    private ISiteService CreateSiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.Properties).Returns(() => AdminWidgetProfileId is null
            ? new JsonObject()
            : new JsonObject { [AIConfigurationGuard.AdminWidgetSettingsKey] = new JsonObject { ["ProfileId"] = AdminWidgetProfileId } });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }

    private static IAICompletionContextBuilder CreateCompletionContextBuilder()
    {
        var builder = new Mock<IAICompletionContextBuilder>();

        builder.Setup(b => b.BuildAsync(It.IsAny<object>(), It.IsAny<Action<AICompletionContext>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new AICompletionContext()));

        return builder.Object;
    }

    private static IAIDeploymentManager CreateDeploymentManager()
    {
        var manager = new Mock<IAIDeploymentManager>();

        manager.Setup(m => m.ResolveSlotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new AIDeployment { ItemId = "deployment-1", Name = "gpt" }));

        return manager.Object;
    }

    private IAICompletionService CreateCompletionService()
    {
        var service = new Mock<IAICompletionService>();

        service.Setup(s => s.CompleteAsync(It.IsAny<AIDeployment>(), It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<AICompletionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment _, IEnumerable<ChatMessage> _, AICompletionContext context, CancellationToken _) =>
            {
                Completions.Add(context);

                return new ChatResponse(new ChatMessage(ChatRole.Assistant, ModelReply));
            });

        return service.Object;
    }

    private static void MergeProperties(AIToolInstance instance, JsonNode data)
    {
        if (data?["Properties"] is not JsonObject properties)
        {
            return;
        }

        foreach (var (key, value) in properties)
        {
            var current = instance.Properties.TryGetValue(key, out var stored)
                ? JsonSerializer.SerializeToNode(stored) as JsonObject ?? new JsonObject()
                : new JsonObject();

            foreach (var (field, fieldValue) in (JsonObject)value)
            {
                current[field] = fieldValue?.DeepClone();
            }

            instance.Properties[key] = current;
        }
    }

    /// <summary>
    /// Ends the AI turn it started when disposed.
    /// </summary>
    internal sealed class AIInvocationScopeHandle : IDisposable
    {
        private readonly CrestApps.Core.AI.Orchestration.AIInvocationScope.Scope _scope;

        public AIInvocationScopeHandle(CrestApps.Core.AI.Orchestration.AIInvocationScope.Scope scope)
        {
            _scope = scope;
        }

        public void Dispose()
            => _scope.Dispose();
    }

    private sealed class HarnessAuthorizationService : IAuthorizationService
    {
        private readonly AIConfigurationToolHarness _harness;

        public HarnessAuthorizationService(AIConfigurationToolHarness harness)
        {
            _harness = harness;
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var visitor = user?.Identity?.IsAuthenticated != true;

            foreach (var requirement in requirements.OfType<PermissionRequirement>())
            {
                _harness.Checks.Add((requirement.Permission.Name, resource, visitor));

                var granted = visitor
                    ? _harness.AuthorizeVisitor(requirement.Permission, resource)
                    : _harness.Authorize(requirement.Permission, resource);

                if (!granted)
                {
                    return Task.FromResult(AuthorizationResult.Failed());
                }
            }

            return Task.FromResult(AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }

    private sealed class FakeToolAccessEvaluator : IAIToolAccessEvaluator
    {
        private readonly HashSet<string> _denied;

        public FakeToolAccessEvaluator(HashSet<string> denied)
        {
            _denied = denied;
        }

        public Task<bool> IsAuthorizedAsync(ClaimsPrincipal user, string toolName)
            => Task.FromResult(user?.Identity?.IsAuthenticated == true && !_denied.Contains(toolName));
    }

    private sealed class FakeWidgetFinder : IPublishedChatWidgetFinder
    {
        private readonly Dictionary<string, int> _widgets;

        public FakeWidgetFinder(Dictionary<string, int> widgets)
        {
            _widgets = widgets;
        }

        public Task<int> CountAsync(string profileId)
            => Task.FromResult(_widgets.GetValueOrDefault(profileId));
    }

    private sealed class FakeTool : AIFunction
    {
        public override string Name => "fake";

        protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult<object>(null);
    }
}

/// <summary>
/// A profile manager on a dictionary. New profiles and JSON updates take the fields the profile tools send, the way
/// the real profile handler reads them, including the instruction lock.
/// </summary>
internal sealed class InMemoryProfileManager : IAIProfileManager
{
    private readonly Dictionary<string, AIProfile> _profiles = new(StringComparer.Ordinal);
    private int _nextId;

    public List<AIProfile> Updated { get; } = [];

    public List<AIProfile> Deleted { get; } = [];

    public void Add(AIProfile profile)
        => _profiles[profile.ItemId] = profile;

    public AIProfile Find(string itemId)
        => _profiles.GetValueOrDefault(itemId);

    public IReadOnlyCollection<AIProfile> All
        => _profiles.Values;

    public ValueTask<AIProfile> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(id is null ? null : _profiles.GetValueOrDefault(id));

    public ValueTask<AIProfile> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_profiles.Values.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)));

    public ValueTask<IEnumerable<AIProfile>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IEnumerable<AIProfile>>(_profiles.Values.ToList());

    public ValueTask<IEnumerable<AIProfile>> GetAsync(AIProfileType type, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IEnumerable<AIProfile>>(_profiles.Values.Where(profile => profile.Type == type).ToList());

    public ValueTask<IReadOnlyCollection<AIProfile>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<AIProfile>>(_profiles.Values.Where(profile => ids.Contains(profile.ItemId)).ToList());

    public ValueTask<PageResult<AIProfile>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
        => throw new NotSupportedException();

    public ValueTask<bool> DeleteAsync(AIProfile model, CancellationToken cancellationToken = default)
    {
        Deleted.Add(model);

        return ValueTask.FromResult(_profiles.Remove(model.ItemId));
    }

    public ValueTask<AIProfile> NewAsync(JsonNode data = null, CancellationToken cancellationToken = default)
    {
        var profile = new AIProfile { ItemId = $"new-profile-{++_nextId}" };
        Populate(profile, data as JsonObject);

        return ValueTask.FromResult(profile);
    }

    public ValueTask<AIProfile> NewAsync(string name, JsonNode data = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public ValueTask CreateAsync(AIProfile model, CancellationToken cancellationToken = default)
    {
        Add(model);

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(AIProfile model, JsonNode data = null, CancellationToken cancellationToken = default)
    {
        Populate(model, data as JsonObject);
        Updated.Add(model);
        Add(model);

        return ValueTask.CompletedTask;
    }

    public ValueTask<ValidationResultDetails> ValidateAsync(AIProfile model, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            result.Fail(new ValidationResult("Name is required."));
        }

        if (model.Type == AIProfileType.Agent && string.IsNullOrWhiteSpace(model.Description))
        {
            result.Fail(new ValidationResult("Description is required for agent profiles."));
        }

        return ValueTask.FromResult(result);
    }

    private static void Populate(AIProfile profile, JsonObject json)
    {
        if (json is null)
        {
            return;
        }

        string Text(string key) => json[key] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

        profile.Name = Text(nameof(AIProfile.Name)) ?? profile.Name;
        profile.DisplayText = Text(nameof(AIProfile.DisplayText)) ?? profile.DisplayText;
        profile.Description = Text(nameof(AIProfile.Description)) ?? profile.Description;
        profile.WelcomeMessage = Text(nameof(AIProfile.WelcomeMessage)) ?? profile.WelcomeMessage;

        if (Text(nameof(AIProfile.Type)) is { } type && Enum.TryParse<AIProfileType>(type, out var profileType))
        {
            profile.Type = profileType;
        }

        if (Text(nameof(AIProfileMetadata.SystemMessage)) is { } systemMessage && !profile.GetSettings<AIProfileSettings>().LockSystemMessage)
        {
            profile.Alter<AIProfileMetadata>(metadata => metadata.SystemMessage = systemMessage);
        }

        if (json[nameof(AIProfileMetadata.Temperature)] is JsonValue temperature)
        {
            profile.Alter<AIProfileMetadata>(metadata => metadata.Temperature = temperature.GetValue<float>());
        }

        if (json.ContainsKey(nameof(AIPersonaMetadata.PersonaId)))
        {
            var personaId = Text(nameof(AIPersonaMetadata.PersonaId));
            profile.Alter<AIPersonaMetadata>(metadata => metadata.PersonaId = string.IsNullOrEmpty(personaId) ? null : personaId);
        }
    }
}
