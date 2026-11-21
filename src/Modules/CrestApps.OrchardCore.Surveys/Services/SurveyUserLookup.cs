using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Turns user ids into user names and back with the site's users.
/// </summary>
internal sealed class SurveyUserLookup : ISurveyUserLookup
{
    private readonly IUserSearchService _userSearchService;
    private readonly UserManager<IUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyUserLookup"/> class.
    /// </summary>
    /// <param name="userSearchService">The user search service.</param>
    /// <param name="userManager">The user manager.</param>
    public SurveyUserLookup(
        IUserSearchService userSearchService,
        UserManager<IUser> userManager)
    {
        _userSearchService = userSearchService;
        _userManager = userManager;
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds)
    {
        var ids = (userIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return (await _userSearchService.GetAsync(ids)).ToDictionary(user => user.UserId, user => user.UserName, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<string> FindUserIdAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var user = await _userManager.FindByNameAsync(userName.Trim());

        return user is null ? null : await _userManager.GetUserIdAsync(user);
    }
}
