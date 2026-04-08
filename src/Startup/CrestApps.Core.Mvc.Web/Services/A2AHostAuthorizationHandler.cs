using CrestApps.Core.AI.A2A.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Mvc.Web.Services;

internal sealed class A2AHostAuthorizationHandler : AuthorizationHandler<A2AHostAuthorizationRequirement>
{
    private readonly IOptionsMonitor<A2AHostOptions> _optionsMonitor;

    public A2AHostAuthorizationHandler(IOptionsMonitor<A2AHostOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        A2AHostAuthorizationRequirement requirement)
    {
        var options = _optionsMonitor.CurrentValue;

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
                if (context.User.Identity?.IsAuthenticated == true &&
                    (!options.RequireAccessPermission || context.User.IsInRole("Administrator")))
                {
                    context.Succeed(requirement);
                }

                break;
        }

        return Task.CompletedTask;
    }
}

internal sealed class A2AHostAuthorizationRequirement : IAuthorizationRequirement
{
}
