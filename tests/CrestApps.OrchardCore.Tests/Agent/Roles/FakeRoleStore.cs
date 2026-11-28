using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Security;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Tests.Agent.Roles;

/// <summary>
/// Keeps the roles of a tool test in memory. It is the role store behind the role manager and the role service the
/// tools ask for role names and system roles, and it records what was created, updated and deleted.
/// </summary>
internal sealed class FakeRoleStore : IRoleStore<IRole>, IRoleService
{
    /// <summary>
    /// Gets the stored roles.
    /// </summary>
    public List<Role> Roles { get; } = [];

    /// <summary>
    /// Gets the names of the roles that were created, in order.
    /// </summary>
    public List<string> Created { get; } = [];

    /// <summary>
    /// Gets the names of the roles that were updated, in order.
    /// </summary>
    public List<string> Updated { get; } = [];

    /// <summary>
    /// Gets the names of the roles that were deleted, in order.
    /// </summary>
    public List<string> Deleted { get; } = [];

    /// <summary>
    /// Gets or sets the name of the administrator role, which a site can configure.
    /// </summary>
    public string AdministratorRoleName { get; set; } = "Administrator";

    /// <summary>
    /// Gets or sets the error the store answers a change with. The default is none.
    /// </summary>
    public IdentityError Failure { get; set; }

    public Task<IdentityResult> CreateAsync(IRole role, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromResult(IdentityResult.Failed(Failure));
        }

        Roles.Add((Role)role);
        Created.Add(role.RoleName);

        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> UpdateAsync(IRole role, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromResult(IdentityResult.Failed(Failure));
        }

        Roles.RemoveAll(stored => string.Equals(stored.RoleName, role.RoleName, StringComparison.OrdinalIgnoreCase));
        Roles.Add((Role)role);
        Updated.Add(role.RoleName);

        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(IRole role, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromResult(IdentityResult.Failed(Failure));
        }

        Roles.RemoveAll(stored => string.Equals(stored.RoleName, role.RoleName, StringComparison.OrdinalIgnoreCase));
        Deleted.Add(role.RoleName);

        return Task.FromResult(IdentityResult.Success);
    }

    // As the role store of Orchard Core does, a role is found by its name and handed out as a copy.
    public Task<IRole> FindByIdAsync(string roleId, CancellationToken cancellationToken)
        => Task.FromResult<IRole>(Find(roleId)?.Clone());

    public Task<IRole> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        => Task.FromResult<IRole>(Find(normalizedRoleName)?.Clone());

    public Task<string> GetRoleIdAsync(IRole role, CancellationToken cancellationToken)
        => Task.FromResult(role.RoleName.ToUpperInvariant());

    public Task<string> GetRoleNameAsync(IRole role, CancellationToken cancellationToken)
        => Task.FromResult(role.RoleName);

    public Task SetRoleNameAsync(IRole role, string roleName, CancellationToken cancellationToken)
    {
        ((Role)role).RoleName = roleName;

        return Task.CompletedTask;
    }

    public Task<string> GetNormalizedRoleNameAsync(IRole role, CancellationToken cancellationToken)
        => Task.FromResult(((Role)role).NormalizedRoleName);

    public Task SetNormalizedRoleNameAsync(IRole role, string normalizedName, CancellationToken cancellationToken)
    {
        ((Role)role).NormalizedRoleName = normalizedName;

        return Task.CompletedTask;
    }

    public Task<IEnumerable<IRole>> GetRolesAsync()
        => Task.FromResult<IEnumerable<IRole>>(Roles.ToList());

    public Task<IEnumerable<Claim>> GetRoleClaimsAsync(string role, CancellationToken cancellationToken = default)
        => Task.FromResult(Find(role)?.RoleClaims.Select(claim => claim.ToClaim()) ?? []);

    public Task<IEnumerable<string>> GetNormalizedRoleNamesAsync()
        => Task.FromResult(Roles.Select(role => role.RoleName.ToUpperInvariant()));

    public ValueTask<bool> IsAdminRoleAsync(string role)
        => ValueTask.FromResult(string.Equals(role, AdministratorRoleName, StringComparison.OrdinalIgnoreCase));

    public ValueTask<bool> IsSystemRoleAsync(string role)
    {
        return ValueTask.FromResult(
            string.Equals(role, AdministratorRoleName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Anonymous", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Authenticated", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
    }

    private Role Find(string roleName)
        => Roles.FirstOrDefault(role => string.Equals(role.RoleName, roleName, StringComparison.OrdinalIgnoreCase));
}
