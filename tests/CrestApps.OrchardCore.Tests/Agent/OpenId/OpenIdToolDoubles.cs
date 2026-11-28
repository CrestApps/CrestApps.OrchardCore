using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenIddict.Abstractions;
using OrchardCore.Environment.Shell;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Security;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Tests.Agent.OpenId;

/// <summary>
/// Stands in for the OpenID services in a tool test: applications and scopes kept in memory behind the application
/// and scope managers, the application recipe step of the OpenID module, and the role service. The confidential
/// application of the fixture has a client secret that starts with <see cref="Canary"/>, so a test can tell that no
/// tool result carries it and that no tool writes one.
/// </summary>
internal sealed class OpenIdToolDoubles
{
    /// <summary>
    /// The text every secret value of the fixture starts with.
    /// </summary>
    public const string Canary = "CANARY";

    /// <summary>
    /// The names of the roles of the fixture.
    /// </summary>
    public static readonly string[] RoleNames = ["Administrator", "Anonymous", "Authenticated", "Editor", "Author"];

    private static readonly string[] _systemRoleNames = ["Administrator", "Anonymous", "Authenticated"];

    private int _nextId = 100;

    public OpenIdToolDoubles()
    {
        Portal = new FakeOpenIdApplication
        {
            Id = "app-portal",
            ClientId = "portal-web",
            DisplayName = "Portal",
            Type = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            RedirectUris = ["https://portal.contoso.com/callback", "http://localhost:5000/callback"],
            Permissions =
            [
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.Prefixes.Scope + "email",
            ],
            Requirements = [OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange],
        };

        Backend = new FakeOpenIdApplication
        {
            Id = "app-backend",
            ClientId = "backend-service",
            DisplayName = "Backend service",
            Type = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            ClientSecret = $"{Canary}-CLIENT-SECRET-HASH",
            Permissions =
            [
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Prefixes.Scope + "orders.read",
            ],
            Roles = ["Editor"],
        };

        Applications.Add(Portal);
        Applications.Add(Backend);

        Scopes.Add(new FakeOpenIdScope
        {
            Id = "scope-email",
            Name = "email",
            DisplayName = "Email",
            Description = "The email address of the user.",
        });

        Scopes.Add(new FakeOpenIdScope
        {
            Id = "scope-orders",
            Name = "orders.read",
            DisplayName = "Read orders",
            Description = "Read access to the orders API.",
            Resources = ["orders-api"],
        });
    }

    /// <summary>
    /// Gets the public client of the fixture.
    /// </summary>
    public FakeOpenIdApplication Portal { get; }

    /// <summary>
    /// Gets the confidential client of the fixture. Its client secret holds a canary.
    /// </summary>
    public FakeOpenIdApplication Backend { get; }

    /// <summary>
    /// Gets the applications the application manager knows.
    /// </summary>
    public List<FakeOpenIdApplication> Applications { get; } = [];

    /// <summary>
    /// Gets the scopes the scope manager knows.
    /// </summary>
    public List<FakeOpenIdScope> Scopes { get; } = [];

    /// <summary>
    /// Gets the application recipe steps the tools ran, in order.
    /// </summary>
    public List<JsonObject> Steps { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the application recipe step of the OpenID module is registered. It is
    /// when the Authorization Server feature is on.
    /// </summary>
    public bool StepIsRegistered { get; set; } = true;

    /// <summary>
    /// Adds the doubles to the services of a tool call.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    public void AddTo(IServiceCollection services)
    {
        services.AddSingleton(CreateApplicationManager());
        services.AddSingleton(CreateScopeManager());
        services.AddSingleton(CreateRoleService());
        services.AddSingleton(new ShellSettings { Name = "Default" });

        if (StepIsRegistered)
        {
            services.AddSingleton<IRecipeStepHandler>(new ApplicationStep(this));
        }

        // Every recipe step handler gets every step, so one that owns another name is there too.
        services.AddSingleton<IRecipeStepHandler>(new OtherStep());
    }

    private IOpenIdApplicationManager CreateApplicationManager()
    {
        var manager = new Mock<IOpenIdApplicationManager>(MockBehavior.Strict);

        manager
            .Setup(m => m.FindByPhysicalIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => new ValueTask<object>(Applications.FirstOrDefault(application => application.Id == id)));
        manager
            .Setup(m => m.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string clientId, CancellationToken _) => new ValueTask<object>(Applications.FirstOrDefault(application => application.ClientId == clientId)));
        manager
            .Setup(m => m.ListAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsync(Applications.ToList()));
        manager
            .Setup(m => m.GetPhysicalIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) => new ValueTask<string>(((FakeOpenIdApplication)application).Id));
        manager
            .Setup(m => m.GetClientIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) => new ValueTask<string>(((FakeOpenIdApplication)application).ClientId));
        manager
            .Setup(m => m.GetDisplayNameAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) => new ValueTask<string>(((FakeOpenIdApplication)application).DisplayName));
        manager
            .Setup(m => m.GetClientTypeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) => new ValueTask<string>(((FakeOpenIdApplication)application).Type));
        manager
            .Setup(m => m.GetConsentTypeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) => new ValueTask<string>(((FakeOpenIdApplication)application).ConsentType));
        manager
            .Setup(m => m.GetPermissionsAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) => new ValueTask<ImmutableArray<string>>([.. ((FakeOpenIdApplication)application).Permissions]));
        manager
            .Setup(m => m.GetRequirementsAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) => new ValueTask<ImmutableArray<string>>([.. ((FakeOpenIdApplication)application).Requirements]));
        manager
            .Setup(m => m.GetRedirectUrisAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) => new ValueTask<ImmutableArray<string>>([.. ((FakeOpenIdApplication)application).RedirectUris]));
        manager
            .Setup(m => m.GetPostLogoutRedirectUrisAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) => new ValueTask<ImmutableArray<string>>([.. ((FakeOpenIdApplication)application).PostLogoutRedirectUris]));
        manager
            .Setup(m => m.GetRolesAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) => new ValueTask<ImmutableArray<string>>([.. ((FakeOpenIdApplication)application).Roles]));
        manager
            .Setup(m => m.HasPermissionAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((object application, string permission, CancellationToken _) => new ValueTask<bool>(((FakeOpenIdApplication)application).Permissions.Contains(permission)));
        manager
            .Setup(m => m.DeleteAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object application, CancellationToken _) =>
            {
                Applications.Remove((FakeOpenIdApplication)application);

                return ValueTask.CompletedTask;
            });

        return manager.Object;
    }

    private IOpenIdScopeManager CreateScopeManager()
    {
        var manager = new Mock<IOpenIdScopeManager>(MockBehavior.Strict);

        manager
            .Setup(m => m.FindByPhysicalIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => new ValueTask<object>(Scopes.FirstOrDefault(scope => scope.Id == id)));
        manager
            .Setup(m => m.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string name, CancellationToken _) => new ValueTask<object>(Scopes.FirstOrDefault(scope => scope.Name == name)));
        manager
            .Setup(m => m.ListAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsync(Scopes.ToList()));
        manager
            .Setup(m => m.GetPhysicalIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object scope, CancellationToken _) => new ValueTask<string>(((FakeOpenIdScope)scope).Id));
        manager
            .Setup(m => m.GetNameAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object scope, CancellationToken _) => new ValueTask<string>(((FakeOpenIdScope)scope).Name));
        manager
            .Setup(m => m.GetDisplayNameAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object scope, CancellationToken _) => new ValueTask<string>(((FakeOpenIdScope)scope).DisplayName));
        manager
            .Setup(m => m.GetDescriptionAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object scope, CancellationToken _) => new ValueTask<string>(((FakeOpenIdScope)scope).Description));
        manager
            .Setup(m => m.GetResourcesAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object scope, CancellationToken _) => new ValueTask<ImmutableArray<string>>([.. ((FakeOpenIdScope)scope).Resources]));
        manager
            .Setup(m => m.PopulateAsync(It.IsAny<OpenIddictScopeDescriptor>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((OpenIddictScopeDescriptor descriptor, object scope, CancellationToken _) =>
            {
                var stored = (FakeOpenIdScope)scope;

                descriptor.Name = stored.Name;
                descriptor.DisplayName = stored.DisplayName;
                descriptor.Description = stored.Description;
                descriptor.Resources.UnionWith(stored.Resources);

                return ValueTask.CompletedTask;
            });
        manager
            .Setup(m => m.CreateAsync(It.IsAny<OpenIddictScopeDescriptor>(), It.IsAny<CancellationToken>()))
            .Returns((OpenIddictScopeDescriptor descriptor, CancellationToken _) =>
            {
                if (descriptor.Name.Contains(' '))
                {
                    throw new OpenIddictExceptions.ValidationException("The scope name cannot contain spaces.");
                }

                var scope = new FakeOpenIdScope
                {
                    Id = $"scope-{_nextId++}",
                };

                Apply(scope, descriptor);
                Scopes.Add(scope);

                return new ValueTask<object>(scope);
            });
        manager
            .Setup(m => m.UpdateAsync(It.IsAny<object>(), It.IsAny<OpenIddictScopeDescriptor>(), It.IsAny<CancellationToken>()))
            .Returns((object scope, OpenIddictScopeDescriptor descriptor, CancellationToken _) =>
            {
                Apply((FakeOpenIdScope)scope, descriptor);

                return ValueTask.CompletedTask;
            });
        manager
            .Setup(m => m.DeleteAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object scope, CancellationToken _) =>
            {
                Scopes.Remove((FakeOpenIdScope)scope);

                return ValueTask.CompletedTask;
            });

        return manager.Object;
    }

    private static IRoleService CreateRoleService()
    {
        var roleService = new Mock<IRoleService>();

        // The roles a user can be given come from these three members: every role that is not a system role, and the
        // administrator role.
        roleService
            .Setup(service => service.GetRolesAsync())
            .ReturnsAsync(() => RoleNames.Select(name => (IRole)new Role { RoleName = name }).ToList());
        roleService
            .Setup(service => service.IsAdminRoleAsync(It.IsAny<string>()))
            .Returns((string name) => new ValueTask<bool>(string.Equals(name, "Administrator", StringComparison.OrdinalIgnoreCase)));
        roleService
            .Setup(service => service.IsSystemRoleAsync(It.IsAny<string>()))
            .Returns((string name) => new ValueTask<bool>(_systemRoleNames.Contains(name, StringComparer.OrdinalIgnoreCase)));

        return roleService.Object;
    }

    private static void Apply(FakeOpenIdScope scope, OpenIddictScopeDescriptor descriptor)
    {
        scope.Name = descriptor.Name;
        scope.DisplayName = descriptor.DisplayName;
        scope.Description = descriptor.Description;
        scope.Resources = [.. descriptor.Resources];
    }

    private static async IAsyncEnumerable<object> ToAsync(IEnumerable<object> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Does what the application recipe step of the OpenID module does with the fields the tools read back: it finds
    /// the application by its client ID, creates it when there is none, and turns the flows of the step into the
    /// permissions of the application. A step without a client secret keeps the stored one.
    /// </summary>
    private sealed class ApplicationStep : NamedRecipeStepHandler
    {
        private readonly OpenIdToolDoubles _doubles;

        public ApplicationStep(OpenIdToolDoubles doubles)
            : base("OpenIdApplication")
        {
            _doubles = doubles;
        }

        protected override Task HandleAsync(RecipeExecutionContext context)
        {
            var step = context.Step;

            _doubles.Steps.Add(step);

            var clientId = step["ClientId"]?.GetValue<string>();
            var application = _doubles.Applications.FirstOrDefault(candidate => candidate.ClientId == clientId);

            if (application is null)
            {
                application = new FakeOpenIdApplication
                {
                    Id = $"app-{_doubles._nextId++}",
                    ClientId = clientId,
                };

                _doubles.Applications.Add(application);
            }

            application.DisplayName = step["DisplayName"]?.GetValue<string>();
            application.Type = step["Type"]?.GetValue<string>();
            application.ConsentType = step["ConsentType"]?.GetValue<string>();
            application.RedirectUris = Split(step["RedirectUris"]);
            application.PostLogoutRedirectUris = Split(step["PostLogoutRedirectUris"]);
            application.Roles = Names(step["RoleEntries"]);

            if (step["ClientSecret"] is JsonValue secret && !string.IsNullOrEmpty(secret.GetValue<string>()))
            {
                application.ClientSecret = secret.GetValue<string>();
            }

            var isPublic = string.Equals(application.Type, OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase);
            var permissions = new List<string>();

            bool Flag(string name) => step[name]?.GetValue<bool>() == true;

            if (Flag("AllowAuthorizationCodeFlow") || Flag("AllowHybridFlow"))
            {
                permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            }

            if (Flag("AllowAuthorizationCodeFlow"))
            {
                permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            }

            if (Flag("AllowClientCredentialsFlow"))
            {
                permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
            }

            if (Flag("AllowHybridFlow") || Flag("AllowImplicitFlow"))
            {
                permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Implicit);
            }

            if (Flag("AllowHybridFlow"))
            {
                permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.CodeIdToken);
            }

            if (Flag("AllowImplicitFlow"))
            {
                permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.IdToken);
            }

            if (Flag("AllowPasswordFlow"))
            {
                permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Password);
            }

            if (Flag("AllowRefreshTokenFlow"))
            {
                permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            }

            if (Flag("AllowLogoutEndpoint"))
            {
                permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
            }

            if (Flag("AllowIntrospectionEndpoint"))
            {
                permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
            }

            if (Flag("AllowRevocationEndpoint"))
            {
                permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
            }

            permissions.AddRange(Names(step["ScopeEntries"]).Select(scope => OpenIddictConstants.Permissions.Prefixes.Scope + scope));

            application.Permissions = permissions;
            application.Requirements = [];

            if (Flag("RequireProofKeyForCodeExchange"))
            {
                application.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
            }

            if (Flag("RequirePushedAuthorizationRequests"))
            {
                application.Requirements.Add(OpenIddictConstants.Requirements.Features.PushedAuthorizationRequests);
            }

            // As the OpenID module does: a public client has no secret.
            if (isPublic)
            {
                application.ClientSecret = null;
            }

            return Task.CompletedTask;
        }

        private static List<string> Split(JsonNode node)
            => [.. (node?.GetValue<string>() ?? string.Empty).Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)];

        private static List<string> Names(JsonNode node)
            => [.. (node as JsonArray ?? []).Select(entry => entry["Name"].GetValue<string>())];
    }

    private sealed class OtherStep : NamedRecipeStepHandler
    {
        public OtherStep()
            : base("Settings")
        {
        }

        protected override Task HandleAsync(RecipeExecutionContext context)
            => throw new InvalidOperationException("A step with another name was given to this handler.");
    }
}
