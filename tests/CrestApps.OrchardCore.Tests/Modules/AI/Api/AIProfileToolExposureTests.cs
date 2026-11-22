using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Endpoints.Api;
using CrestApps.OrchardCore.AI.Tools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore;
using OrchardCore.Security;
using OrchardCore.Security.AuthorizationHandlers;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Api;

/// <summary>
/// C12 (review 11, SEC-A3): a profile that visitors who aren't signed in, or a role without a tool's permission, can use
/// doesn't save with that tool. The roles are evaluated with Orchard Core's real permission handler and permission
/// granting service, so implied permissions count as they do on a live site.
/// </summary>
public sealed class AIProfileToolExposureTests
{
    private static readonly Permission _sendEmail = AIPermissions.CreateAIToolPermission("sendEmail");

    [Fact]
    public async Task Create_RefusesAProfileVisitorsCanUse_WithAToolThatNeedsAPermission()
    {
        // Arrange: the Anonymous role may query the public profile.
        using var provider = Services(new Role { RoleName = OrchardCoreConstants.Roles.Anonymous, RoleClaims = [Grant("QueryAIProfile_public")] });
        var manager = Manager();

        // Act
        var result = await AIProfilesApiEndpoints.CreateAsync(Body("public", "sendEmail"), Context(provider), Allow(), manager.Object, Evaluator(), Instances());

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("'Anonymous'", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        Assert.Contains("sendEmail", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        manager.Verify(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_RefusesARoleThatCanUseTheProfile_ButLacksTheToolsPermission()
    {
        // Arrange
        using var provider = Services(
            new Role { RoleName = OrchardCoreConstants.Roles.Anonymous },
            new Role { RoleName = "Editor", RoleClaims = [Grant(AIPermissions.QueryAnyAIProfile.Name)] });
        var manager = Manager();

        // Act
        var result = await AIProfilesApiEndpoints.CreateAsync(Body("internal", "sendEmail"), Context(provider), Allow(), manager.Object, Evaluator(), Instances());

        // Assert
        var problem = Assert.IsType<ValidationProblem>(result.Result);
        Assert.Contains("'Editor'", problem.ProblemDetails.Errors["tools"][0], StringComparison.Ordinal);
        manager.Verify(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_SavesAProfile_WhenEveryRoleThatCanUseItHoldsTheToolsPermission()
    {
        // Arrange: Support may use the profile and the tool (through Access any AI tool, which implies it); Administrator
        // may use everything.
        using var provider = Services(
            new Role { RoleName = OrchardCoreConstants.Roles.Anonymous },
            new Role { RoleName = "Support", RoleClaims = [Grant("QueryAIProfile_internal"), Grant(AIPermissions.AccessAnyAITool.Name)] },
            new Role { RoleName = OrchardCoreConstants.Roles.Administrator });
        var manager = Manager();

        // Act
        var result = await AIProfilesApiEndpoints.CreateAsync(Body("internal", "sendEmail"), Context(provider), Allow(), manager.Object, Evaluator(), Instances());

        // Assert
        Assert.IsType<Created<AIProfile>>(result.Result);
        manager.Verify(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static RoleClaim Grant(string permission)
        => new() { ClaimType = Permission.ClaimType, ClaimValue = permission };

    private static ServiceProvider Services(params Role[] roles)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddSingleton<IPermissionGrantingService, DefaultPermissionGrantingService>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<IAuthorizationHandler, AIProfileAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler>(new RoleDefaultsHandler(roles));
        services.AddSingleton<IRoleService>(new FakeRoleService(roles));
        services.AddScoped<AIProfileToolExposureValidator>();
        services.RequireAIToolPermission("sendEmail", _sendEmail);

        return services.BuildServiceProvider();
    }

    private static JsonObject Body(string name, params string[] tools)
        => new()
        {
            ["Name"] = name,
            ["Properties"] = new JsonObject
            {
                [nameof(FunctionInvocationMetadata)] = new JsonObject
                {
                    [nameof(FunctionInvocationMetadata.Names)] = new JsonArray([.. tools.Select(tool => (JsonNode)tool)]),
                },
            },
        };

    private static Mock<IAIProfileManager> Manager()
    {
        var manager = new Mock<IAIProfileManager>();
        manager.Setup(m => m.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AIProfile)null);
        manager.Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JsonNode data, CancellationToken _) => data.Deserialize<AIProfile>() ?? new AIProfile());
        manager.Setup(m => m.ValidateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ValidationResultDetails());
        manager.Setup(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        return manager;
    }

    // The editor of the profile may pick any tool; what is checked here is who can use the profile.
    private static IAIToolAccessEvaluator Evaluator()
    {
        var evaluator = new Mock<IAIToolAccessEvaluator>();
        evaluator.Setup(e => e.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>())).ReturnsAsync(true);

        return evaluator.Object;
    }

    private static DefaultAIToolInstanceAccessor Instances()
        => new([], Evaluator(), new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

    private static IAuthorizationService Allow()
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return authorization.Object;
    }

    private static DefaultHttpContext Context(IServiceProvider provider)
        => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "manager")], "Test")),
            RequestServices = provider.CreateScope().ServiceProvider,
        };

    // What Orchard Core's RolesPermissionsHandler (OrchardCore.Roles) does: everyone gets the Anonymous role's
    // permissions, and signed-in people the Authenticated role's as well.
    private sealed class RoleDefaultsHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly Role[] _roles;

        public RoleDefaultsHandler(Role[] roles)
            => _roles = roles;

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var names = context.User.Identity?.IsAuthenticated == true
                ? new[] { OrchardCoreConstants.Roles.Anonymous, OrchardCoreConstants.Roles.Authenticated }
                : [OrchardCoreConstants.Roles.Anonymous];
            var claims = _roles.Where(role => names.Contains(role.RoleName)).SelectMany(role => role.RoleClaims).Select(claim => (Claim)claim);

            if (new DefaultPermissionGrantingService().IsGranted(requirement, claims))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoleService : IRoleService
    {
        private readonly Role[] _roles;

        public FakeRoleService(Role[] roles)
            => _roles = roles;

        public Task<IEnumerable<IRole>> GetRolesAsync()
            => Task.FromResult<IEnumerable<IRole>>(_roles);

        public Task<IEnumerable<Claim>> GetRoleClaimsAsync(string role, CancellationToken cancellationToken = default)
            => Task.FromResult(_roles.Where(item => item.RoleName == role).SelectMany(item => item.RoleClaims).Select(claim => (Claim)claim));

        public Task<IEnumerable<string>> GetNormalizedRoleNamesAsync()
            => Task.FromResult(_roles.Select(role => role.RoleName.ToUpperInvariant()));

        public ValueTask<bool> IsAdminRoleAsync(string role)
            => ValueTask.FromResult(role == OrchardCoreConstants.Roles.Administrator);
    }
}
