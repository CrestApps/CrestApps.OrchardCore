using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Handlers;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// An authorization service that decides the way Orchard Core does for the messaging permissions: a permission is
/// granted when the user's roles hold it or a permission that implies it, and otherwise the messaging conversation
/// authorization handler may still grant it for a conversation that is the user's to work.
/// </summary>
internal sealed class MessagingTestAuthorization : IAuthorizationService
{
    private readonly HashSet<string> _granted;
    private MessagingConversationAuthorizationHandler _handler;

    private MessagingTestAuthorization(IEnumerable<Permission> granted)
    {
        _granted = granted.Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates the service for a user holding the given permissions, whose agent profile is the given one.
    /// </summary>
    public static MessagingTestAuthorization Create(AgentProfile agent, IAgentEntitlementPolicy entitlementPolicy, params Permission[] granted)
    {
        var authorization = new MessagingTestAuthorization(granted);

        var agentProfiles = new Mock<IAgentProfileManager>();
        agentProfiles
            .Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        authorization._handler = new MessagingConversationAuthorizationHandler(
            new Lazy<IAuthorizationService>(() => authorization),
            agentProfiles.Object,
            entitlementPolicy ?? new PermissiveAgentEntitlementPolicy());

        return authorization;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
    {
        var context = new AuthorizationHandlerContext(requirements.ToArray(), user, resource);

        foreach (var requirement in context.PendingRequirements.OfType<PermissionRequirement>().ToArray())
        {
            if (IsGranted(requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }

        await _handler.HandleAsync(context);

        return context.HasSucceeded ? AuthorizationResult.Success() : AuthorizationResult.Failed();
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
        => Task.FromResult(AuthorizationResult.Failed());

    private bool IsGranted(Permission permission)
        => permission is not null &&
            (_granted.Contains(permission.Name) || (permission.ImpliedBy ?? []).Any(IsGranted));
}
