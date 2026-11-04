using CrestApps.OrchardCore.Users.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// Keeps role grants on the user's account (<see cref="UserRoleGrants"/>) and puts the user in, or takes them out of,
/// the role through the user manager.
/// </summary>
internal sealed class DefaultRoleGrantService : IRoleGrantService
{
    private readonly UserManager<IUser> _userManager;
    private readonly IUserService _userService;
    private readonly IEnumerable<IRoleGrantSource> _sources;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultRoleGrantService(
        UserManager<IUser> userManager,
        IUserService userService,
        IEnumerable<IRoleGrantSource> sources,
        IClock clock,
        ILogger<DefaultRoleGrantService> logger)
    {
        _userManager = userManager;
        _userService = userService;
        _sources = sources;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> GrantAsync(string userId, string roleName, string source, string sourceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(roleName);
        ArgumentException.ThrowIfNullOrEmpty(source);

        if (string.IsNullOrEmpty(userId) || await _userService.GetUserByUniqueIdAsync(userId) is not { } user)
        {
            return false;
        }

        var isInRole = await _userManager.IsInRoleAsync(user, roleName);

        if (user is not IEntity entity)
        {
            // A user store without properties can't keep grants: put the user in the role and nothing more.
            return isInRole || await AddToRoleAsync(user, roleName);
        }

        var grants = await GetGrantsAsync(userId, entity);

        if (!grants.Grants.Any(grant => Matches(grant, roleName, source, sourceId)))
        {
            grants.Grants.Add(new RoleGrant
            {
                RoleName = roleName,
                Source = source,
                SourceId = sourceId,
                GrantedUtc = _clock.UtcNow,
            });
        }

        if (isInRole)
        {
            entity.Put(grants);
            await _userManager.UpdateAsync(user);

            return true;
        }

        if (!grants.AddedRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase))
        {
            grants.AddedRoles.Add(roleName);
        }

        entity.Put(grants);

        // Adding the role saves the user, and with it the grants.
        return await AddToRoleAsync(user, roleName);
    }

    public async Task<bool> RevokeAsync(string userId, string roleName, string source, string sourceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(roleName);
        ArgumentException.ThrowIfNullOrEmpty(source);

        if (string.IsNullOrEmpty(userId) ||
            await _userService.GetUserByUniqueIdAsync(userId) is not { } user ||
            user is not IEntity entity)
        {
            return false;
        }

        var grants = await GetGrantsAsync(userId, entity, new RoleGrant { RoleName = roleName, Source = source, SourceId = sourceId });

        grants.Grants.RemoveAll(grant => Matches(grant, roleName, source, sourceId));

        var remove = !grants.Grants.Any(grant => string.Equals(grant.RoleName, roleName, StringComparison.OrdinalIgnoreCase)) &&
            grants.AddedRoles.RemoveAll(role => string.Equals(role, roleName, StringComparison.OrdinalIgnoreCase)) > 0;

        entity.Put(grants);

        if (!remove || !await _userManager.IsInRoleAsync(user, roleName))
        {
            await _userManager.UpdateAsync(user);

            return false;
        }

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);

        if (!result.Succeeded)
        {
            _logger.LogError("Could not take user '{UserId}' out of the '{RoleName}' role: {Errors}", userId, roleName, string.Join("; ", result.Errors.Select(error => error.Description)));

            return false;
        }

        // Roles are read from the sign-in: a new security stamp makes it be checked again, so what the grant opened
        // closes now rather than when the user next signs in.
        await _userManager.UpdateSecurityStampAsync(user);

        return true;
    }

    public async Task<IReadOnlyList<RoleGrant>> ListAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId) ||
            await _userService.GetUserByUniqueIdAsync(userId) is not { } user ||
            user is not IEntity entity)
        {
            return [];
        }

        return (await GetGrantsAsync(userId, entity)).Grants;
    }

    // The grants kept on the user, or, the first time, those the sources report, so grants made before they were
    // recorded by source are kept. A grant being revoked the first time a user is touched was made before grants were
    // recorded, and its source no longer reports it once it ended: it is kept as one that put the user in the role,
    // as the sources removed such roles before, unless a source reports that the user was in the role already.
    private async Task<UserRoleGrants> GetGrantsAsync(string userId, IEntity entity, RoleGrant revoking = null)
    {
        if (entity.TryGet<UserRoleGrants>(out var grants))
        {
            grants.Grants ??= [];
            grants.AddedRoles ??= [];

            return grants;
        }

        grants = new UserRoleGrants();

        var heldBefore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in _sources)
        {
            foreach (var existing in await source.ListExistingGrantsAsync(userId) ?? [])
            {
                if (string.IsNullOrEmpty(existing?.RoleName))
                {
                    continue;
                }

                if (!grants.Grants.Any(grant => Matches(grant, existing.RoleName, source.Source, existing.SourceId)))
                {
                    grants.Grants.Add(new RoleGrant
                    {
                        RoleName = existing.RoleName,
                        Source = source.Source,
                        SourceId = existing.SourceId,
                        GrantedUtc = _clock.UtcNow,
                    });
                }

                if (!existing.RoleAddedBySource)
                {
                    heldBefore.Add(existing.RoleName);
                }
                else if (!grants.AddedRoles.Contains(existing.RoleName, StringComparer.OrdinalIgnoreCase))
                {
                    grants.AddedRoles.Add(existing.RoleName);
                }
            }
        }

        if (revoking is not null && !grants.Grants.Any(grant => Matches(grant, revoking.RoleName, revoking.Source, revoking.SourceId)))
        {
            revoking.GrantedUtc = _clock.UtcNow;
            grants.Grants.Add(revoking);

            if (!heldBefore.Contains(revoking.RoleName) && !grants.AddedRoles.Contains(revoking.RoleName, StringComparer.OrdinalIgnoreCase))
            {
                grants.AddedRoles.Add(revoking.RoleName);
            }
        }

        return grants;
    }

    private async Task<bool> AddToRoleAsync(IUser user, string roleName)
    {
        var result = await _userManager.AddToRoleAsync(user, roleName);

        if (!result.Succeeded)
        {
            _logger.LogError("Could not put user '{UserName}' in the '{RoleName}' role: {Errors}", user.UserName, roleName, string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        return result.Succeeded;
    }

    private static bool Matches(RoleGrant grant, string roleName, string source, string sourceId)
        => string.Equals(grant.RoleName, roleName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(grant.Source, source, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(grant.SourceId, sourceId, StringComparison.Ordinal);
}
