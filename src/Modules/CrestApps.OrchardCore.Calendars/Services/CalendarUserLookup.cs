using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Finds users for the calendar pickers and shows their names.
/// </summary>
public sealed class CalendarUserLookup
{
    private const int MaxResults = 30;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarUserLookup"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public CalendarUserLookup(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Finds enabled users whose user name or email starts with or contains the text.
    /// </summary>
    /// <param name="query">The text to look for.</param>
    /// <returns>The users.</returns>
    public async Task<IReadOnlyList<CalendarUserItem>> SearchAsync(string query)
    {
        IEnumerable<User> users;

        if (string.IsNullOrWhiteSpace(query))
        {
            users = await _session.Query<User, UserIndex>(index => index.IsEnabled).OrderBy(index => index.NormalizedUserName).Take(MaxResults).ListAsync();
        }
        else
        {
            var normalized = query.Trim().ToUpperInvariant();

            users = await _session.Query<User, UserIndex>(index => index.IsEnabled && (index.NormalizedUserName.Contains(normalized) || index.NormalizedEmail.Contains(normalized)))
                .OrderBy(index => index.NormalizedUserName)
                .Take(MaxResults)
                .ListAsync();
        }

        return users.Select(ToItem).ToList();
    }

    /// <summary>
    /// Gets the users with the given ids.
    /// </summary>
    /// <param name="userIds">The user ids.</param>
    /// <returns>The users found.</returns>
    public async Task<IReadOnlyList<CalendarUserItem>> GetAsync(IEnumerable<string> userIds)
    {
        var ids = userIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray() ?? [];

        if (ids.Length == 0)
        {
            return [];
        }

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(ids)).ListAsync();

        return users.Select(ToItem).ToList();
    }

    /// <summary>
    /// Gets a user's email and name, for adding the user to an event.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The user's email and name, or <see langword="null"/> when there is no such user.</returns>
    public async Task<(string Email, string Name)?> GetContactAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var user = await _session.Query<User, UserIndex>(index => index.UserId == userId).FirstOrDefaultAsync();

        return user is null ? null : (user.Email, user.UserName);
    }

    private static CalendarUserItem ToItem(User user)
    {
        return new CalendarUserItem(user.UserId, string.IsNullOrEmpty(user.Email) ? user.UserName : $"{user.UserName} ({user.Email})");
    }
}
