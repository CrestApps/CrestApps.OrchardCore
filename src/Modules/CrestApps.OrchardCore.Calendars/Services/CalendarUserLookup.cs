using CrestApps.OrchardCore.Users;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Finds users for the calendar pickers and shows their names, through the shared <see cref="IUserSearchService"/>.
/// </summary>
public sealed class CalendarUserLookup
{
    private const int MaxResults = 30;

    private readonly IUserSearchService _userSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarUserLookup"/> class.
    /// </summary>
    /// <param name="userSearchService">The shared user search.</param>
    public CalendarUserLookup(IUserSearchService userSearchService)
    {
        _userSearchService = userSearchService;
    }

    /// <summary>
    /// Finds enabled users whose user name or email contains the text.
    /// </summary>
    /// <param name="query">The text to look for.</param>
    /// <returns>The users.</returns>
    public async Task<IReadOnlyList<CalendarUserItem>> SearchAsync(string query)
    {
        var users = await _userSearchService.SearchAsync(new UserSearchQuery
        {
            Text = query,
            MatchEmail = true,
            MaxResults = MaxResults,
        });

        return users.Select(ToItem).ToList();
    }

    /// <summary>
    /// Gets the users with the given ids.
    /// </summary>
    /// <param name="userIds">The user ids.</param>
    /// <returns>The users found.</returns>
    public async Task<IReadOnlyList<CalendarUserItem>> GetAsync(IEnumerable<string> userIds)
    {
        var users = await _userSearchService.GetAsync(userIds ?? []);

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

        var users = await _userSearchService.GetAsync([userId]);
        var user = users.Count > 0 ? users[0] : null;

        return user is null ? null : (user.Email, user.UserName);
    }

    private static CalendarUserItem ToItem(UserSearchResult user)
    {
        return new CalendarUserItem(user.UserId, string.IsNullOrEmpty(user.Email) ? user.UserName : $"{user.UserName} ({user.Email})");
    }
}
