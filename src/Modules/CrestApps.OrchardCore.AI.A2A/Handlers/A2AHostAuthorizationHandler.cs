using CrestApps.Core.AI.A2A.Models;
using CrestApps.OrchardCore.AI.A2A.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.A2A.Handlers;

internal sealed class A2AHostAuthorizationHandler : AuthorizationHandler<A2AHostAuthorizationRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    private IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AHostAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public A2AHostAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        A2AHostAuthorizationRequirement requirement)
    {
        var options = _serviceProvider.GetRequiredService<IOptions<A2AHostOptions>>().Value;

        switch (options.AuthenticationType)
        {
            case A2AHostAuthenticationType.None:
                context.Succeed(requirement);
                break;

            case A2AHostAuthenticationType.ApiKey:

                if (context.User.Identity?.IsAuthenticated == true &&
                    context.User.Identity.AuthenticationType == A2AApiKeyAuthenticationDefaults.AuthenticationScheme)
                {
                    context.Succeed(requirement);
                }

                break;

            case A2AHostAuthenticationType.OpenId:
            default:

                if (context.User.Identity?.IsAuthenticated == true)
                {
                    if (!options.RequireAccessPermission)
                    {
                        context.Succeed(requirement);
                    }
                    else
                    {
                        _authorizationService ??= _serviceProvider.GetRequiredService<IAuthorizationService>();

                        if (await _authorizationService.AuthorizeAsync(context.User, A2AHostPermissionsProvider.AccessA2AHost))
                        {
                            context.Succeed(requirement);
                        }
                    }
                }

                break;
        }
    }
}

internal sealed class A2AHostAuthorizationRequirement : IAuthorizationRequirement
{
}
