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
    private const int MaxAudiencePages = 10;

    private readonly IEnumerable<IUserAudienceProvider> _audienceProviders;
    private readonly IEnumerable<IUserAvatarProvider> _avatarProviders;

    public DefaultUserSearchService(
        ISession session,
        ILookupNormalizer normalizer,
        IDisplayNameProvider displayNameProvider,
        IEnumerable<IUserAudienceProvider> audienceProviders,
        IEnumerable<IUserAvatarProvider> avatarProviders)
    {
        _avatarProviders = avatarProviders;
        _session = session;
        _normalizer = normalizer;
        _displayNameProvider = displayNameProvider;
        _audienceProviders = audienceProviders;
    }

    public async Task<IReadOnlyList<UserSearchResult>> SearchAsync(UserSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var search = _session.Query<User>();

        IEnumerable<string> requestedRoles = query.Roles ?? [];
        IUserAudienceProvider audience = null;

        if (!string.IsNullOrWhiteSpace(query.Audience)
            && _audienceProviders.FirstOrDefault(provider => string.Equals(provider.Name, query.Audience.Trim(), StringComparison.OrdinalIgnoreCase)) is { } found)
        {
            audience = found;

            // No roles at all means the audience isn't limited now; an empty list means it has no members.
            if (await audience.GetRoleNamesAsync(cancellationToken) is { } audienceRoles)
            {
                var names = audienceRoles.Where(static role => !string.IsNullOrWhiteSpace(role)).ToArray();

                if (names.Length == 0)
                {
                    return [];
                }

                requestedRoles = requestedRoles.Concat(names);
            }
        }

        var roles = requestedRoles
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

        var take = Math.Max(1, query.MaxResults);
        var ordered = users.OrderBy(index => index.NormalizedUserName);

        if (audience is null)
        {
            return await ToResultsAsync(await ordered.Take(take).ListAsync(cancellationToken), cancellationToken);
        }

        // The audience may leave out some of the users found, so later pages are read until the list is full.
        var kept = new List<User>();

        for (var page = 0; page < MaxAudiencePages && kept.Count < take; page++)
        {
            var batch = (await ordered.Skip(page * take).Take(take).ListAsync(cancellationToken)).ToList();

            if (batch.Count == 0)
            {
                break;
            }

            var excluded = await audience.GetExcludedUserIdsAsync([.. batch.Select(user => user.UserId)], cancellationToken);
            var excludedIds = excluded is { Count: > 0 } ? excluded.ToHashSet(StringComparer.Ordinal) : null;

            kept.AddRange(batch.Where(user => excludedIds is null || !excludedIds.Contains(user.UserId)).Take(take - kept.Count));

            if (batch.Count < take)
            {
                break;
            }
        }

        return await ToResultsAsync(kept, cancellationToken);
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

    private async Task<string> GetAvatarUrlAsync(User user, CancellationToken cancellationToken)
    {
        foreach (var provider in _avatarProviders)
        {
            if (await provider.GetAvatarUrlAsync(user, cancellationToken) is { Length: > 0 } url)
            {
                return url;
            }
        }

        return null;
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
                AvatarUrl = await GetAvatarUrlAsync(user, cancellationToken),
            });
        }

        return results;
    }
}
