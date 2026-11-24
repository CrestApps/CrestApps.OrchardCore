using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.Tests.Agent.AIConfiguration;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Agent.AIOperations;

/// <summary>
/// Builds the services the API key, conversation, usage, chat interaction, memory, MCP and A2A tools use, on
/// in-memory lists. The catalog doubles run the checks of the handlers the tools rely on: an MCP prompt message role
/// must be user or assistant, a resource needs a URI and a name, a connection needs a title.
/// </summary>
internal sealed class AIOperationsToolHarness
{
    public const string OtherUserId = "user-2";

    public static readonly DateTime UtcNow = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    public bool SignedIn { get; set; } = true;

    public Func<Permission, object, bool> Authorize { get; set; } = (_, _) => true;

    public List<string> Checks { get; } = [];

    public List<(string Permission, object Resource)> ResourceChecks { get; } = [];

    public InMemoryProfileManager Profiles { get; } = new();

    public List<AIApiKey> ApiKeys { get; } = [];

    public List<AIApiKey> UpdatedApiKeys { get; } = [];

    public List<string> Roles { get; } = ["Administrator", "Editor", "Anonymous"];

    public List<AIChatSession> Sessions { get; } = [];

    public List<string> DeletedSessions { get; } = [];

    public List<string> DeletedAllProfiles { get; } = [];

    public List<AICompletionUsageRecord> Usage { get; } = [];

    public List<AIDeployment> Deployments { get; } = [];

    public List<ChatInteraction> Interactions { get; } = [];

    public List<AIMemoryEntry> Memories { get; } = [];

    public List<(string Id, string Name)> Users { get; } = [(AgentToolTestContext.UserId, AgentToolTestContext.UserName), (OtherUserId, "jsmith")];

    public List<McpConnection> McpConnections { get; } = [];

    public List<McpPrompt> Prompts { get; } = [];

    public List<McpResource> Resources { get; } = [];

    public List<A2AConnection> A2AConnections { get; } = [];

    public List<object> Saved { get; } = [];

    public List<JsonNode> McpConnectionUpdateData { get; } = [];

    public List<object> Deleted { get; } = [];

    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn);
        services.AddSingleton<IAuthorizationService>(new HarnessAuthorizationService(this));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton<IAIProfileManager>(Profiles);
        services.AddSingleton(CreateClock());
        services.AddSingleton(CreateApiKeyStore());
        services.AddSingleton(CreateRoleService());
        services.AddSingleton(CreateSessionManager());
        services.AddSingleton(CreateUsageService());
        services.AddSingleton(CreateDeploymentStore());
        services.AddSingleton(CreateInteractionManager());
        services.AddSingleton(CreateMemoryStore());
        services.AddSingleton(CreateMemoryManager());
        services.AddSingleton(CreateUserManager());
        services.AddSingleton(CreateMcpConnectionManager());
        services.AddSingleton(CreatePromptManager());
        services.AddSingleton(CreateResourceManager());
        services.AddSingleton(CreateA2AConnectionManager());

        services.Configure<McpClientAIOptions>(options => options.AddTransportType(McpConstants.TransportTypes.Sse));
        services.Configure<McpOptions>(options =>
        {
            options.AddResourceType("file", entry => entry.SupportedVariables = [new McpResourceVariable("fileName")]);
            options.AddResourceType("ftp");
            options.AddResourceType("media", entry => entry.SupportedVariables = [new McpResourceVariable("fileName")]);
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Starts an AI turn, as the chat hub does.
    /// </summary>
    public static AIInvocationScope.Scope BeginTurn(AIProfile runningProfile = null, AIChatSession session = null, ChatInteraction interaction = null)
        => AIInvocationScope.Begin(new AIInvocationContext
        {
            ToolExecutionContext = runningProfile is null ? null : new AIToolExecutionContext(runningProfile),
            ChatSession = session,
            ChatInteraction = interaction,
        });

    public AIProfile AddProfile(string id, string name, Action<AIProfile> configure = null)
    {
        var profile = new AIProfile { ItemId = id, Name = name, DisplayText = name, Type = AIProfileType.Chat };
        configure?.Invoke(profile);
        Profiles.Add(profile);

        return profile;
    }

    public AIApiKey AddApiKey(string id, AIApiKeyKind kind, Action<AIApiKey> configure = null)
    {
        var key = new AIApiKey
        {
            ItemId = id,
            Name = id,
            Kind = kind,
            Prefix = kind == AIApiKeyKind.Browser ? "cpk_12345678" : "cak_12345678",
            Hash = "hash-" + id,
            PublicKey = kind == AIApiKeyKind.Browser ? "cpk_12345678_PUBLICVALUE" : null,
            Enabled = true,
            CreatedUtc = UtcNow.AddDays(-30),
            Scopes = kind == AIApiKeyKind.Browser ? [AIApiKeys.Scopes.Chat] : [AIApiKeys.Scopes.Profiles],
            Roles = kind == AIApiKeyKind.Browser ? [] : ["Editor"],
            AllowedOrigins = kind == AIApiKeyKind.Browser ? ["https://www.example.com"] : [],
            AllowedProfileIds = [],
        };

        configure?.Invoke(key);
        ApiKeys.Add(key);

        return key;
    }

    public McpConnection AddSseConnection(string id, string title, string endpoint = "https://mcp.example.com/sse", Action<SseMcpConnectionMetadata> configure = null)
    {
        var metadata = new SseMcpConnectionMetadata { Endpoint = new Uri(endpoint) };
        configure?.Invoke(metadata);

        var connection = new McpConnection { ItemId = id, Source = McpConstants.TransportTypes.Sse, DisplayText = title };
        connection.Put(metadata);
        McpConnections.Add(connection);

        return connection;
    }

    public McpConnection AddStdioConnection(string id, string title)
    {
        var connection = new McpConnection { ItemId = id, Source = McpConstants.TransportTypes.StdIo, DisplayText = title };
        connection.Put(new StdioMcpConnectionMetadata
        {
            Command = "docker",
            Arguments = ["run", "-i", "--token=SECRET-ARGUMENT"],
            EnvironmentVariables = new Dictionary<string, string> { ["GITHUB_TOKEN"] = "SECRET-ENVIRONMENT" },
        });
        McpConnections.Add(connection);

        return connection;
    }

    public A2AConnection AddA2AConnection(string id, string title, string endpoint = "https://agents.partner.com", Action<A2AConnectionMetadata> configure = null)
    {
        var metadata = new A2AConnectionMetadata();
        configure?.Invoke(metadata);

        var connection = new A2AConnection { ItemId = id, DisplayText = title, Endpoint = endpoint };
        connection.Put(metadata);
        A2AConnections.Add(connection);

        return connection;
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(UtcNow);
        clock.Setup(c => c.ConvertToTimeZone(It.IsAny<DateTimeOffset>(), It.IsAny<ITimeZone>())).Returns((DateTimeOffset value, ITimeZone _) => value);

        return clock.Object;
    }

    private IAIApiKeyStore CreateApiKeyStore()
    {
        var store = new Mock<IAIApiKeyStore>();
        store.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIApiKey>>(ApiKeys.ToList()));
        store.Setup(s => s.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string id, CancellationToken _) => ValueTask.FromResult(ApiKeys.FirstOrDefault(k => k.ItemId == id)));
        store.Setup(s => s.UpdateAsync(It.IsAny<AIApiKey>(), It.IsAny<CancellationToken>())).Returns((AIApiKey key, CancellationToken _) =>
        {
            UpdatedApiKeys.Add(key);

            return ValueTask.CompletedTask;
        });
        store.Setup(s => s.DeleteAsync(It.IsAny<AIApiKey>(), It.IsAny<CancellationToken>())).Returns((AIApiKey key, CancellationToken _) => Remove(ApiKeys, key));

        return store.Object;
    }

    private IRoleService CreateRoleService()
    {
        var service = new Mock<IRoleService>();
        service.Setup(s => s.GetRolesAsync()).ReturnsAsync(() => Roles.Select(name => Mock.Of<IRole>(role => role.RoleName == name)).ToList());

        return service.Object;
    }

    private IAIChatSessionManager CreateSessionManager()
    {
        var manager = new Mock<IAIChatSessionManager>();
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string id, CancellationToken _) => Task.FromResult(Sessions.FirstOrDefault(s => s.SessionId == id)));

        // As the manager does, only the signed-in user's own sessions are deleted.
        manager.Setup(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string id, CancellationToken _) =>
        {
            var session = Sessions.FirstOrDefault(s => s.SessionId == id && s.UserId == AgentToolTestContext.UserId);

            if (session is null)
            {
                return Task.FromResult(false);
            }

            Sessions.Remove(session);
            DeletedSessions.Add(id);

            return Task.FromResult(true);
        });
        manager.Setup(m => m.DeleteAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string profileId, CancellationToken _) =>
        {
            DeletedAllProfiles.Add(profileId);

            return Task.FromResult(Sessions.RemoveAll(s => s.ProfileId == profileId && s.UserId == AgentToolTestContext.UserId));
        });

        return manager.Object;
    }

    private IAICompletionUsageService CreateUsageService()
    {
        var service = new Mock<IAICompletionUsageService>();
        service.Setup(s => s.GetAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns((DateTime? from, DateTime? to, CancellationToken _) => Task.FromResult<IReadOnlyList<AICompletionUsageRecord>>(Usage
                .Where(record => (from is null || record.CreatedUtc >= from) && (to is null || record.CreatedUtc < to))
                .ToList()));

        return service.Object;
    }

    private IAIDeploymentStore CreateDeploymentStore()
    {
        var store = new Mock<IAIDeploymentStore>();
        store.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIDeployment>>(Deployments.ToList()));

        return store.Object;
    }

    private ICatalogManager<ChatInteraction> CreateInteractionManager()
    {
        var manager = new Mock<ICatalogManager<ChatInteraction>>();
        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult<IEnumerable<ChatInteraction>>(Interactions.ToList()));
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string id, CancellationToken _) => ValueTask.FromResult(Interactions.FirstOrDefault(i => i.ItemId == id)));
        manager.Setup(m => m.DeleteAsync(It.IsAny<ChatInteraction>(), It.IsAny<CancellationToken>())).Returns((ChatInteraction interaction, CancellationToken _) => Remove(Interactions, interaction));

        return manager.Object;
    }

    private IAIMemoryStore CreateMemoryStore()
    {
        var store = new Mock<IAIMemoryStore>();
        store.Setup(s => s.CountByUserAsync(It.IsAny<string>())).Returns((string userId) => Task.FromResult(Memories.Count(m => m.UserId == userId)));
        store.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIMemoryEntry>>(Memories.ToList()));

        return store.Object;
    }

    private ICatalogManager<AIMemoryEntry> CreateMemoryManager()
    {
        var manager = new Mock<ICatalogManager<AIMemoryEntry>>();
        manager.Setup(m => m.DeleteAsync(It.IsAny<AIMemoryEntry>(), It.IsAny<CancellationToken>())).Returns((AIMemoryEntry memory, CancellationToken _) => Remove(Memories, memory));

        return manager.Object;
    }

    private UserManager<IUser> CreateUserManager()
    {
        var manager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);

        IUser Find(Func<(string Id, string Name), bool> match)
        {
            var found = Users.FirstOrDefault(match);

            return found.Id is null ? null : new TestUser(found.Id, found.Name);
        }

        manager.Setup(m => m.FindByNameAsync(It.IsAny<string>())).Returns((string name) => Task.FromResult(Find(user => string.Equals(user.Name, name, StringComparison.OrdinalIgnoreCase))));
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>())).Returns((string id) => Task.FromResult(Find(user => user.Id == id)));
        manager.Setup(m => m.GetUserIdAsync(It.IsAny<IUser>())).Returns((IUser user) => Task.FromResult(((TestUser)user).Id));
        manager.Setup(m => m.GetUserNameAsync(It.IsAny<IUser>())).Returns((IUser user) => Task.FromResult(user.UserName));

        return manager.Object;
    }

    private ISourceCatalogManager<McpConnection> CreateMcpConnectionManager()
    {
        var manager = new Mock<ISourceCatalogManager<McpConnection>>();
        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult<IEnumerable<McpConnection>>(McpConnections.ToList()));
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string id, CancellationToken _) => ValueTask.FromResult(McpConnections.FirstOrDefault(c => c.ItemId == id)));
        manager.Setup(m => m.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode _, CancellationToken _) => ValueTask.FromResult(new McpConnection { ItemId = "new-mcp", Source = source }));
        manager.Setup(m => m.ValidateAsync(It.IsAny<McpConnection>(), It.IsAny<CancellationToken>()))
            .Returns((McpConnection connection, CancellationToken _) => ValueTask.FromResult(Require(connection.DisplayText, "DisplayText is required field.")));
        manager.Setup(m => m.CreateAsync(It.IsAny<McpConnection>(), It.IsAny<CancellationToken>())).Returns((McpConnection connection, CancellationToken _) => Add(McpConnections, connection));
        manager.Setup(m => m.UpdateAsync(It.IsAny<McpConnection>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).Returns((McpConnection connection, JsonNode data, CancellationToken _) =>
        {
            McpConnectionUpdateData.Add(data);

            return Track(connection);
        });
        manager.Setup(m => m.DeleteAsync(It.IsAny<McpConnection>(), It.IsAny<CancellationToken>())).Returns((McpConnection connection, CancellationToken _) => Remove(McpConnections, connection));

        return manager.Object;
    }

    private INamedCatalogManager<McpPrompt> CreatePromptManager()
    {
        var manager = new Mock<INamedCatalogManager<McpPrompt>>();
        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult<IEnumerable<McpPrompt>>(Prompts.ToList()));
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string id, CancellationToken _) => ValueTask.FromResult(Prompts.FirstOrDefault(p => p.ItemId == id)));
        manager.Setup(m => m.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string name, CancellationToken _) => ValueTask.FromResult(Prompts.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))));
        manager.Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).Returns((JsonNode _, CancellationToken _) => ValueTask.FromResult(new McpPrompt { ItemId = "new-prompt" }));
        manager.Setup(m => m.ValidateAsync(It.IsAny<McpPrompt>(), It.IsAny<CancellationToken>())).Returns((McpPrompt prompt, CancellationToken _) =>
        {
            var result = Require(prompt.Name, "Name is required.");

            for (var i = 0; i < (prompt.Messages?.Count ?? 0); i++)
            {
                if (prompt.Messages[i].Role is not ("user" or "assistant"))
                {
                    result.Fail(new ValidationResult($"Message {i + 1} must have a role of 'user' or 'assistant'."));
                }
            }

            return ValueTask.FromResult(result);
        });
        manager.Setup(m => m.CreateAsync(It.IsAny<McpPrompt>(), It.IsAny<CancellationToken>())).Returns((McpPrompt prompt, CancellationToken _) => Add(Prompts, prompt));
        manager.Setup(m => m.UpdateAsync(It.IsAny<McpPrompt>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).Returns((McpPrompt prompt, JsonNode _, CancellationToken _) => Track(prompt));
        manager.Setup(m => m.DeleteAsync(It.IsAny<McpPrompt>(), It.IsAny<CancellationToken>())).Returns((McpPrompt prompt, CancellationToken _) => Remove(Prompts, prompt));

        return manager.Object;
    }

    private ISourceCatalogManager<McpResource> CreateResourceManager()
    {
        var manager = new Mock<ISourceCatalogManager<McpResource>>();
        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult<IEnumerable<McpResource>>(Resources.ToList()));
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string id, CancellationToken _) => ValueTask.FromResult(Resources.FirstOrDefault(r => r.ItemId == id)));
        manager.Setup(m => m.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode _, CancellationToken _) => ValueTask.FromResult(new McpResource { ItemId = "new-resource", Source = source }));
        manager.Setup(m => m.ValidateAsync(It.IsAny<McpResource>(), It.IsAny<CancellationToken>())).Returns((McpResource resource, CancellationToken _) =>
        {
            var result = Require(resource.Resource?.Uri, "URI is required.");

            if (string.IsNullOrEmpty(resource.Resource?.Name))
            {
                result.Fail(new ValidationResult("Name is required."));
            }

            return ValueTask.FromResult(result);
        });
        manager.Setup(m => m.CreateAsync(It.IsAny<McpResource>(), It.IsAny<CancellationToken>())).Returns((McpResource resource, CancellationToken _) => Add(Resources, resource));
        manager.Setup(m => m.UpdateAsync(It.IsAny<McpResource>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).Returns((McpResource resource, JsonNode _, CancellationToken _) => Track(resource));
        manager.Setup(m => m.DeleteAsync(It.IsAny<McpResource>(), It.IsAny<CancellationToken>())).Returns((McpResource resource, CancellationToken _) => Remove(Resources, resource));

        return manager.Object;
    }

    private ICatalogManager<A2AConnection> CreateA2AConnectionManager()
    {
        var manager = new Mock<ICatalogManager<A2AConnection>>();
        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult<IEnumerable<A2AConnection>>(A2AConnections.ToList()));
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string id, CancellationToken _) => ValueTask.FromResult(A2AConnections.FirstOrDefault(c => c.ItemId == id)));
        manager.Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).Returns((JsonNode _, CancellationToken _) => ValueTask.FromResult(new A2AConnection { ItemId = "new-a2a" }));
        manager.Setup(m => m.ValidateAsync(It.IsAny<A2AConnection>(), It.IsAny<CancellationToken>())).Returns((A2AConnection connection, CancellationToken _) => ValueTask.FromResult(Require(connection.DisplayText, "The Title is required.")));
        manager.Setup(m => m.CreateAsync(It.IsAny<A2AConnection>(), It.IsAny<CancellationToken>())).Returns((A2AConnection connection, CancellationToken _) => Add(A2AConnections, connection));
        manager.Setup(m => m.UpdateAsync(It.IsAny<A2AConnection>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).Returns((A2AConnection connection, JsonNode _, CancellationToken _) => Track(connection));
        manager.Setup(m => m.DeleteAsync(It.IsAny<A2AConnection>(), It.IsAny<CancellationToken>())).Returns((A2AConnection connection, CancellationToken _) => Remove(A2AConnections, connection));

        return manager.Object;
    }

    private static ValidationResultDetails Require(string value, string message)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(value))
        {
            result.Fail(new ValidationResult(message));
        }

        return result;
    }

    private ValueTask Add<T>(List<T> list, T item)
    {
        list.Add(item);
        Saved.Add(item);

        return ValueTask.CompletedTask;
    }

    private ValueTask Track(object item)
    {
        Saved.Add(item);

        return ValueTask.CompletedTask;
    }

    private ValueTask<bool> Remove<T>(List<T> list, T item)
    {
        Deleted.Add(item);

        return ValueTask.FromResult(list.Remove(item));
    }

    private sealed class TestUser : IUser
    {
        public TestUser(string id, string userName)
        {
            Id = id;
            UserName = userName;
        }

        public string Id { get; }

        public string UserName { get; }
    }

    private sealed class HarnessAuthorizationService : IAuthorizationService
    {
        private readonly AIOperationsToolHarness _harness;

        public HarnessAuthorizationService(AIOperationsToolHarness harness)
        {
            _harness = harness;
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            foreach (var requirement in requirements.OfType<PermissionRequirement>())
            {
                // The configuration guard asks with a visitor's principal whether visitors may chat with a profile.
                if (user?.Identity?.IsAuthenticated != true)
                {
                    return Task.FromResult(AuthorizationResult.Failed());
                }

                _harness.Checks.Add(requirement.Permission.Name);
                _harness.ResourceChecks.Add((requirement.Permission.Name, resource));

                if (!_harness.Authorize(requirement.Permission, resource))
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
