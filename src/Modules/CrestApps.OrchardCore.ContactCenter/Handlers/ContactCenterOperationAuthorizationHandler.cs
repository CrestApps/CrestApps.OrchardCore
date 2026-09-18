using System.Collections.Frozen;
using CrestApps.Core.ContactCenter.Security;
using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Answers a Contact Center operation with the Orchard Core permission that has always governed it.
/// </summary>
/// <remarks>
/// <para>
/// The suite asks what the caller is trying to do; this decides who may do it, without changing any
/// permission id, name or description. An operation the map does not know is left unhandled, which
/// denies it - the safe direction for a question nobody has answered.
/// </para>
/// <para>
/// Registered wherever a service that asks one of these operations is registered. A missing
/// registration is not a silent widening: it takes the feature away instead.
/// </para>
/// </remarks>
public sealed class ContactCenterOperationAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement>
{
    private static readonly FrozenDictionary<string, Permission> _permissions = new Dictionary<string, Permission>(StringComparer.Ordinal)
    {
        [ContactCenterOperations.SuperviseQueue.Name] = ContactCenterPermissions.MonitorContactCenter,
        [ContactCenterOperations.TransferExternally.Name] = ContactCenterPermissions.TransferExternally,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private readonly Lazy<IAuthorizationService> _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOperationAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="authorizationService">
    /// The authorization service used to evaluate the mapped permission, resolved lazily. It is what
    /// builds this handler, so taking it eagerly would close the loop before either end exists.
    /// </param>
    public ContactCenterOperationAuthorizationHandler(Lazy<IAuthorizationService> authorizationService)
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

        // Evaluated without the resource: the permission is the coarse grant, and the caller narrows
        // it to the specific queue or destination afterwards.
        if (await _authorizationService.Value.AuthorizeAsync(context.User, permission))
        {
            context.Succeed(requirement);
        }
    }
}
