using Microsoft.AspNetCore.Identity;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// Searches the Orchard Core users with their indexes and names them with the <see cref="IDisplayNameProvider"/>.
/// </summary>
internal sealed class DefaultUserSearchService : IUserSearchService
{
    private readonly ISession _session;
    private readonly ILookupNormalizer _normalizer;
    private readonly IDisplayNameProvider _displayNameProvider;

    public DefaultUserSearchService(
        ISession session,
        ILookupNormalizer normalizer,
        IDisplayNameProvider displayNameProvider)
    {
        _session = session;
        _normalizer = normalizer;
        _displayNameProvider = displayNameProvider;
    }

    public async Task<IReadOnlyList<UserSearchResult>> SearchAsync(UserSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var search = _session.Query<User>();

        var roles = (query.Roles ?? [])
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Select(role => _normalizer.NormalizeName(role.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roles.Length > 0)
        {
            search = search.With<UserByRoleNameIndex>(index => index.RoleName.IsIn(roles));
        }

        var text = query.Text?.Trim();
        IQuery<User, UserIndex> users;

        if (string.IsNullOrEmpty(text))
        {
            users = search.With<UserIndex>(index => index.IsEnabled);
        }
        else
        {
            var normalizedName = _normalizer.NormalizeName(text);

            if (query.MatchEmail)
            {
                var normalizedEmail = _normalizer.NormalizeEmail(text);

                users = search.With<UserIndex>(index => index.IsEnabled && (index.NormalizedUserName.Contains(normalizedName) || index.NormalizedEmail.Contains(normalizedEmail)));
            }
            else
            {
                users = search.With<UserIndex>(index => index.IsEnabled && index.NormalizedUserName.Contains(normalizedName));
            }
        }

        var found = await users
            .OrderBy(index => index.NormalizedUserName)
            .Take(Math.Max(1, query.MaxResults))
            .ListAsync(cancellationToken);

        return await ToResultsAsync(found, cancellationToken);
    }

    public async Task<IReadOnlyList<UserSearchResult>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds?
            .Where(static id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        if (ids.Length == 0)
        {
            return [];
        }

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(ids)).ListAsync(cancellationToken);

        return await ToResultsAsync(users, cancellationToken);
    }

    private async Task<IReadOnlyList<UserSearchResult>> ToResultsAsync(IEnumerable<User> users, CancellationToken cancellationToken)
    {
        var results = new List<UserSearchResult>();

        foreach (var user in users)
        {
            results.Add(new UserSearchResult
            {
                UserId = user.UserId,
                UserName = user.UserName,
                NormalizedUserName = user.NormalizedUserName,
                Email = user.Email,
                DisplayName = await _displayNameProvider.GetAsync(user, cancellationToken),
                IsEnabled = user.IsEnabled,
            });
        }

        return results;
    }
}
