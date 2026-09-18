using System.Collections.Frozen;
using CrestApps.Core.Omnichannel.Sms.Portal.Security;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Handlers;

/// <summary>
/// Answers an SMS portal operation with the Orchard Core permission that has always governed it.
/// </summary>
/// <remarks>
/// No permission id, name or description changes. An operation the map does not know is left
/// unhandled, which denies it.
/// </remarks>
public sealed class SmsPortalOperationAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement>
{
    private static readonly FrozenDictionary<string, Permission> _permissions = new Dictionary<string, Permission>(StringComparer.Ordinal)
    {
        [SmsPortalOperations.ViewAllConversations.Name] = SmsPortalPermissions.ViewAllConversations,
        [SmsPortalOperations.UseSmsPortal.Name] = SmsPortalPermissions.UseSmsPortal,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private readonly Lazy<IAuthorizationService> _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsPortalOperationAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="authorizationService">
    /// The authorization service used to evaluate the mapped permission, resolved lazily. It is what
    /// builds this handler, so taking it eagerly would close the loop before either end exists.
    /// </param>
    public SmsPortalOperationAuthorizationHandler(Lazy<IAuthorizationService> authorizationService)
    {
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets the permission an operation maps to.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <returns>The permission, or <see langword="null"/> when the operation is not one of this suite's.</returns>
    public static Permission GetPermission(string operationName)
        => operationName is not null && _permissions.TryGetValue(operationName, out var permission) ? permission : null;

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement)
    {
        if (context.User is null || !_permissions.TryGetValue(requirement.Name, out var permission))
        {
            return;
        }

        // Evaluated without the resource: the permission is the coarse supervisor grant, and the
        // caller narrows it to the conversations this agent may actually see.
        if (await _authorizationService.Value.AuthorizeAsync(context.User, permission))
        {
            context.Succeed(requirement);
        }
    }
}
