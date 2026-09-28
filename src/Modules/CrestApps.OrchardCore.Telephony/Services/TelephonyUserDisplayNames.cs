using CrestApps.OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// The display names of the users extensions ring, as the site shows its users.
/// </summary>
internal interface ITelephonyUserDisplayNames
{
    /// <summary>
    /// Gets the display name of each user, loading them all in one query.
    /// </summary>
    /// <param name="userIds">The users.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Each found user's display name by user id; the username when the site gives none.</returns>
    Task<IReadOnlyDictionary<string, string>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves display names through the site's <see cref="IDisplayNameProvider"/>, which is registered only while the
/// CrestApps Users feature is on; without it a user's name is their username.
/// </summary>
internal sealed class TelephonyUserDisplayNames : ITelephonyUserDisplayNames
{
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyUserDisplayNames"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="displayNameProviders">The site's display name provider, when there is one.</param>
    public TelephonyUserDisplayNames(ISession session, IEnumerable<IDisplayNameProvider> displayNameProviders)
    {
        _session = session;
        _displayNameProvider = displayNameProviders?.LastOrDefault();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = (userIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (ids.Length == 0)
        {
            return names;
        }

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(ids)).ListAsync(cancellationToken);

        foreach (var user in users)
        {
            if (string.IsNullOrEmpty(user?.UserId))
            {
                continue;
            }

            var name = _displayNameProvider is null
                ? null
                : await _displayNameProvider.GetAsync(user, cancellationToken);

            names[user.UserId] = string.IsNullOrWhiteSpace(name) ? user.UserName : name.Trim();
        }

        return names;
    }
}
