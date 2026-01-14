using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

internal sealed class McpServerAuthorizationHandler : AuthorizationHandler<McpServerAuthorizationRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    private IAuthorizationService _authorizationService;

    public McpServerAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        McpServerAuthorizationRequirement requirement)
    {
        var options = _serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        switch (options.AuthenticationType)
        {
            case McpServerAuthenticationType.None:
                // Allow anonymous access.
                context.Succeed(requirement);
                break;

            case McpServerAuthenticationType.ApiKey:
                // For API key auth, check if the user is authenticated via the McpApiKey scheme.
                if (context.User.Identity?.IsAuthenticated == true &&
                    context.User.Identity.AuthenticationType == McpApiKeyAuthenticationDefaults.AuthenticationScheme)
                {
                    context.Succeed(requirement);
                }
                break;

            case McpServerAuthenticationType.OpenId:
            default:
                // For OpenId auth, check if the user is authenticated.
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    if (!options.RequireAccessPermission)
                    {
                        // Permission not required, just need authentication.
                        context.Succeed(requirement);
                    }
                    else
                    {
                        // Permission required, check if the user has the AccessMcpServer permission.
                        _authorizationService ??= _serviceProvider.GetRequiredService<IAuthorizationService>();

                        if (await _authorizationService.AuthorizeAsync(context.User, McpServerPermissionsProvider.AccessMcpServer))
                        {
                            context.Succeed(requirement);
                        }
                    }
                }
                break;
        }
    }
}

internal sealed class McpServerAuthorizationRequirement : IAuthorizationRequirement
{
}
