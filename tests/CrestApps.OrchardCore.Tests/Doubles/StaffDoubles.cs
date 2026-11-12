using System.Collections.Concurrent;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// The user and role services Staff needs, for tests without Orchard's user store.
/// </summary>
public static class StaffDoubles
{
    /// <summary>
    /// Registers an in-memory user directory, role grants and role manager, an HTTP context accessor, and turns team
    /// memberships on as the Teams module does.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddStaffDoubles(this IServiceCollection services)
    {
        services.TryAddSingleton<InMemoryUserDirectory>();
        services.TryAddSingleton<IUserSearchService>(provider => provider.GetRequiredService<InMemoryUserDirectory>());
        services.TryAddSingleton<RecordingRoleGrantService>();
        services.TryAddSingleton<IRoleGrantService>(provider => provider.GetRequiredService<RecordingRoleGrantService>());
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Built-in capability labels are localized; the tests show the source text.
        services.TryAddSingleton<Microsoft.Extensions.Localization.IStringLocalizerFactory, SourceTextLocalizerFactory>();

        // Capability records live in a document; the tests keep it in memory.
        services.TryAddSingleton<IDocumentManager<StaffCapabilitiesDocument>>(new CrestApps.OrchardCore.Tests.AgenticCommerce.InMemoryDocumentManager<StaffCapabilitiesDocument>());

        // The tests stand in for a site with the Teams module on, which turns team memberships on for Staff.
        services.Configure<StaffOptions>(options => options.UsesTeams = true);
        services.TryAddScoped(_ =>
        {
            var roleStore = new Mock<IRoleStore<IRole>>();
            var manager = new Mock<RoleManager<IRole>>(roleStore.Object, null, null, null, null);

            manager.Setup(roles => roles.NormalizeKey(It.IsAny<string>())).Returns<string>(name => name?.ToUpperInvariant());
            manager.Setup(roles => roles.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(new Role { RoleName = "Staff" });

            return manager.Object;
        });

        return services;
    }
}

/// <summary>
/// Users known to a test, found by id; a user that wasn't added is unknown, so a staff member keeps its own name.
/// </summary>
public sealed class InMemoryUserDirectory : IUserSearchService
{
    private readonly ConcurrentDictionary<string, UserSearchResult> _users = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds or changes a user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="displayName">The name.</param>
    /// <param name="email">The e-mail.</param>
    public void Set(string userId, string displayName, string email)
        => _users[userId] = new UserSearchResult { UserId = userId, UserName = userId, DisplayName = displayName, Email = email, IsEnabled = true };

    /// <inheritdoc />
    public Task<IReadOnlyList<UserSearchResult>> SearchAsync(UserSearchQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserSearchResult>>([.. _users.Values.Where(user => string.IsNullOrEmpty(query?.Text) || user.DisplayName.Contains(query.Text, StringComparison.OrdinalIgnoreCase))]);

    /// <inheritdoc />
    public Task<IReadOnlyList<UserSearchResult>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserSearchResult>>([.. (userIds ?? []).Where(id => id is not null && _users.ContainsKey(id)).Select(id => _users[id])]);
}

/// <summary>
/// Remembers the roles granted and revoked, by user.
/// </summary>
public sealed class RecordingRoleGrantService : IRoleGrantService
{
    private readonly ConcurrentDictionary<string, List<RoleGrant>> _grants = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<bool> GrantAsync(string userId, string roleName, string source, string sourceId)
    {
        var grants = _grants.GetOrAdd(userId, _ => []);

        lock (grants)
        {
            if (!grants.Any(grant => grant.RoleName == roleName && grant.Source == source && grant.SourceId == sourceId))
            {
                grants.Add(new RoleGrant { RoleName = roleName, Source = source, SourceId = sourceId });
            }
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> RevokeAsync(string userId, string roleName, string source, string sourceId)
    {
        if (_grants.TryGetValue(userId, out var grants))
        {
            lock (grants)
            {
                grants.RemoveAll(grant => grant.RoleName == roleName && grant.Source == source && grant.SourceId == sourceId);
            }
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RoleGrant>> ListAsync(string userId)
    {
        if (!_grants.TryGetValue(userId, out var grants))
        {
            return Task.FromResult<IReadOnlyList<RoleGrant>>([]);
        }

        lock (grants)
        {
            return Task.FromResult<IReadOnlyList<RoleGrant>>([.. grants]);
        }
    }

    /// <summary>
    /// Gets whether a user holds a role.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="roleName">The role.</param>
    /// <returns><see langword="true"/> when granted.</returns>
    public bool Holds(string userId, string roleName)
        => _grants.TryGetValue(userId, out var grants) && grants.Any(grant => grant.RoleName == roleName);
}
