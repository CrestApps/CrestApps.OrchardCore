using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Finds the tools of a profile that someone who can use the profile may not use (SEC-A3). A tool with a registered
/// permission (<see cref="AIToolPermissionOptions"/>) refuses a caller without it, so a profile that visitors who aren't
/// signed in, or a role without the permission, can use would offer them a tool that only says no; worse, it shows that
/// an administrator meant those people to have it. Such a profile doesn't save until the tool, the role's access or the
/// role's permissions change.
/// </summary>
public sealed class AIProfileToolExposureValidator
{
    private const string AuthenticationType = "AIProfileToolExposure";

    private readonly IAuthorizationService _authorizationService;
    private readonly AIToolPermissionOptions _toolPermissions;
    private readonly IdentityOptions _identityOptions;
    private readonly IRoleService _roleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileToolExposureValidator"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="toolPermissions">The permission each tool needs.</param>
    /// <param name="identityOptions">The identity options, for the role claim type.</param>
    /// <param name="roleService">The role service, when the Roles feature is on.</param>
    public AIProfileToolExposureValidator(
        IAuthorizationService authorizationService,
        IOptions<AIToolPermissionOptions> toolPermissions,
        IOptions<IdentityOptions> identityOptions,
        IRoleService roleService = null)
    {
        _authorizationService = authorizationService;
        _toolPermissions = toolPermissions.Value;
        _identityOptions = identityOptions.Value;
        _roleService = roleService;
    }

    /// <summary>
    /// Gets the tools of the profile that someone who can use it may not use.
    /// </summary>
    /// <param name="profile">The profile, as it will be saved.</param>
    /// <param name="toolNames">The tools the profile runs with its callers.</param>
    /// <returns>Each role that can use the profile and a tool it may not use; empty when there are none.</returns>
    public async Task<IReadOnlyList<AIToolExposureIssue>> ValidateAsync(AIProfile profile, IEnumerable<string> toolNames)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var guarded = (toolNames ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => (Name: name, Permission: _toolPermissions.GetPermission(name)))
            .Where(tool => tool.Permission is not null)
            .ToArray();

        if (guarded.Length == 0)
        {
            return [];
        }

        var issues = new List<AIToolExposureIssue>();

        foreach (var role in await GetRoleNamesAsync())
        {
            var principal = await CreatePrincipalAsync(role);

            if (principal is null || !await _authorizationService.AuthorizeAsync(principal, AIPermissions.QueryAnyAIProfile, profile))
            {
                continue;
            }

            foreach (var (name, permission) in guarded)
            {
                if (!await _authorizationService.AuthorizeAsync(principal, permission))
                {
                    issues.Add(new AIToolExposureIssue(role, name, permission.Name));
                }
            }
        }

        return issues;
    }

    // Visitors who aren't signed in are checked whether or not the Roles feature lists the Anonymous role.
    private async Task<IEnumerable<string>> GetRoleNamesAsync()
    {
        var names = new List<string> { OrchardCoreConstants.Roles.Anonymous };

        if (_roleService is not null)
        {
            names.AddRange((await _roleService.GetRolesAsync())
                .Select(role => role.RoleName)
                .Where(name => !string.IsNullOrEmpty(name) && !string.Equals(name, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase)));
        }

        return names;
    }

    // A person who holds only the role: what Orchard Core signs them in with. Administrators may use every tool.
    private async Task<ClaimsPrincipal> CreatePrincipalAsync(string role)
    {
        if (string.Equals(role, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase))
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        if (string.Equals(role, OrchardCoreConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase))
        {
            return new ClaimsPrincipal(new ClaimsIdentity(AuthenticationType));
        }

        if (await _roleService.IsAdminRoleAsync(role))
        {
            return null;
        }

        var claims = new List<Claim> { new(_identityOptions.ClaimsIdentity.RoleClaimType, role) };
        claims.AddRange(await _roleService.GetRoleClaimsAsync(role));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType));
    }
}
