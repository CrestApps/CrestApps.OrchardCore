using CrestApps.OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Names the agents on a set of recorded calls the way the site names its users.
/// </summary>
public sealed class CallRecordingAgentNameResolver
{
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingAgentNameResolver"/> class.
    /// </summary>
    /// <param name="session">The YesSql session, used to load the users in one query.</param>
    /// <param name="displayNameProviders">The site's display name provider, when there is one.</param>
    public CallRecordingAgentNameResolver(ISession session, IEnumerable<IDisplayNameProvider> displayNameProviders)
    {
        _session = session;
        _displayNameProvider = displayNameProviders.LastOrDefault();
    }

    /// <summary>
    /// Gets the display names of users.
    /// </summary>
    /// <param name="userIds">The user identifiers; empty and repeated ones are ignored.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The names by user identifier. A user that no longer exists is missing.</returns>
    public async Task<Dictionary<string, string>> GetNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (ids.Length == 0)
        {
            return names;
        }

        // One query for the whole set, rather than one per call.
        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(ids)).ListAsync(cancellationToken);

        foreach (var user in users)
        {
            var name = _displayNameProvider is null
                ? null
                : await _displayNameProvider.GetAsync(user, cancellationToken);

            names[user.UserId] = string.IsNullOrWhiteSpace(name) ? user.UserName : name.Trim();
        }

        return names;
    }

    /// <summary>
    /// Looks up a user's name in names returned by <see cref="GetNamesAsync"/>.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="names">The names.</param>
    /// <returns>The name, the identifier itself when the user is not named, or <see langword="null"/> when there is no user.</returns>
    public static string NameOf(string userId, Dictionary<string, string> names)
        => string.IsNullOrEmpty(userId)
            ? null
            : names.TryGetValue(userId, out var name) ? name : userId;
}
