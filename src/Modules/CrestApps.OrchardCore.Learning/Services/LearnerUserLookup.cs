using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Maps user ids to user names and back, so learner records move between sites by user name.
/// </summary>
internal class LearnerUserLookup
{
    private readonly IUserSearchService _userSearchService;
    private readonly UserManager<IUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearnerUserLookup"/> class.
    /// </summary>
    /// <param name="userSearchService">The user search service.</param>
    /// <param name="userManager">The user manager.</param>
    public LearnerUserLookup(
        IUserSearchService userSearchService,
        UserManager<IUser> userManager)
    {
        _userSearchService = userSearchService;
        _userManager = userManager;
    }

    /// <summary>
    /// Gets the user names of users.
    /// </summary>
    /// <param name="userIds">The user ids.</param>
    /// <returns>The user names by user id.</returns>
    public virtual async Task<IDictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return (await _userSearchService.GetAsync(ids)).ToDictionary(user => user.UserId, user => user.UserName, StringComparer.Ordinal);
    }

    /// <summary>
    /// Finds the user id of a user name.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <returns>The user id, or <see langword="null"/>.</returns>
    public virtual async Task<string> FindUserIdAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var user = await _userManager.FindByNameAsync(userName.Trim());

        return user is null ? null : await _userManager.GetUserIdAsync(user);
    }
}
