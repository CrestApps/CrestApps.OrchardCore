using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Records;

/// <summary>
/// Grants a permission to a user who holds a claim for it, or for a permission that implies it, so a record source's
/// permission gate can be checked without the roles pipeline.
/// </summary>
internal sealed class PermissionClaimAuthorizationService : IAuthorizationService
{
    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
    {
        foreach (var requirement in requirements)
        {
            if (requirement is PermissionRequirement permissionRequirement && !Has(user, permissionRequirement.Permission))
            {
                return Task.FromResult(AuthorizationResult.Failed());
            }
        }

        return Task.FromResult(AuthorizationResult.Success());
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
        => Task.FromResult(AuthorizationResult.Failed());

    /// <summary>
    /// Makes a signed-in user who holds the given permissions.
    /// </summary>
    /// <param name="permissions">The permissions.</param>
    /// <returns>The user.</returns>
    public static ClaimsPrincipal User(params Permission[] permissions)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1") };

        claims.AddRange(permissions.Select(permission => new Claim(Permission.ClaimType, permission.Name)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static bool Has(ClaimsPrincipal user, Permission permission)
    {
        if (user.HasClaim(Permission.ClaimType, permission.Name))
        {
            return true;
        }

        return permission.ImpliedBy?.Any(implied => implied is not null && Has(user, implied)) == true;
    }
}
