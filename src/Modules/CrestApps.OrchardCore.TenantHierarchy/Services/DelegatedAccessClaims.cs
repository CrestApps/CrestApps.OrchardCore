using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Builds and reads the claims of a principal that entered a child tenant through delegated access.
/// </summary>
public static class DelegatedAccessClaims
{
    /// <summary>
    /// The interval used when a principal carries no validation interval.
    /// </summary>
    public static readonly TimeSpan DefaultValidationInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Builds the delegated access claims for a redeemed code.
    /// </summary>
    /// <param name="redemption">The redeemed delegated access.</param>
    public static List<Claim> Create(DelegatedAccessRedemption redemption)
    {
        ArgumentNullException.ThrowIfNull(redemption);

        var claims = new List<Claim>
        {
            new(TenantHierarchyConstants.ClaimTypes.SessionId, redemption.SessionId),
            new(TenantHierarchyConstants.ClaimTypes.ParentTenantId, redemption.ParentTenantId ?? string.Empty),
            new(TenantHierarchyConstants.ClaimTypes.ParentUserId, redemption.ParentUserId ?? string.Empty),
            new(TenantHierarchyConstants.ClaimTypes.ParentDisplayName, redemption.ParentDisplayName ?? string.Empty),
            new(TenantHierarchyConstants.ClaimTypes.ParentAddress, redemption.ParentAddress ?? string.Empty),
            new(TenantHierarchyConstants.ClaimTypes.RolesVersion, redemption.RolesVersion ?? string.Empty),
            new(TenantHierarchyConstants.ClaimTypes.SwitcherMode, redemption.SwitcherMode.ToString()),
            new(
                TenantHierarchyConstants.ClaimTypes.ValidationInterval,
                ((int)redemption.ValidationInterval.TotalSeconds).ToString(CultureInfo.InvariantCulture)),
        };

        if (!string.IsNullOrWhiteSpace(redemption.ChildLabel))
        {
            claims.Add(new Claim(TenantHierarchyConstants.ClaimTypes.ChildLabel, redemption.ChildLabel));
        }

        foreach (var method in redemption.AuthenticationMethods ?? [])
        {
            claims.Add(new Claim(TenantHierarchyConstants.ClaimTypes.AuthenticationMethods, method));
        }

        return claims;
    }

    /// <summary>
    /// Returns the delegated access session identifier of a principal, or <see langword="null"/>.
    /// </summary>
    /// <param name="principal">The principal.</param>
    public static string GetSessionId(ClaimsPrincipal principal)
        => principal?.FindFirst(TenantHierarchyConstants.ClaimTypes.SessionId)?.Value;

    /// <summary>
    /// Returns whether a principal entered through delegated access.
    /// </summary>
    /// <param name="principal">The principal.</param>
    public static bool IsDelegated(ClaimsPrincipal principal)
        => !string.IsNullOrEmpty(GetSessionId(principal));

    /// <summary>
    /// Returns the interval at which the session of a principal is validated with the parent.
    /// </summary>
    /// <param name="principal">The principal.</param>
    public static TimeSpan GetValidationInterval(ClaimsPrincipal principal)
    {
        var value = principal?.FindFirst(TenantHierarchyConstants.ClaimTypes.ValidationInterval)?.Value;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(Math.Min(seconds, 3600));
        }

        return DefaultValidationInterval;
    }

    /// <summary>
    /// Copies the delegated access claims of one principal onto a new identity, replacing the roles version.
    /// </summary>
    /// <param name="source">The principal to copy from.</param>
    /// <param name="target">The identity to copy to.</param>
    /// <param name="rolesVersion">The new roles version.</param>
    public static void CopyTo(ClaimsPrincipal source, ClaimsIdentity target, string rolesVersion)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        foreach (var claim in source.Claims.Where(claim => claim.Type.StartsWith("th:", StringComparison.Ordinal) || claim.Type == TenantHierarchyConstants.ClaimTypes.AuthenticationMethods))
        {
            if (claim.Type == TenantHierarchyConstants.ClaimTypes.RolesVersion || target.HasClaim(claim.Type, claim.Value))
            {
                continue;
            }

            target.AddClaim(new Claim(claim.Type, claim.Value));
        }

        foreach (var existing in target.FindAll(TenantHierarchyConstants.ClaimTypes.RolesVersion).ToArray())
        {
            target.RemoveClaim(existing);
        }

        target.AddClaim(new Claim(TenantHierarchyConstants.ClaimTypes.RolesVersion, rolesVersion ?? string.Empty));
    }
}
