using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Turns user ids into user names and back with the site's users.
/// </summary>
internal sealed class SurveyUserLookup : ISurveyUserLookup
{
    private readonly IServiceProvider _serviceProvider;
    private readonly UserManager<IUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyUserLookup"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider. The shared user search service is optional: when no feature
    /// registers it, names are read one user at a time through the user manager.</param>
    /// <param name="userManager">The user manager.</param>
    public SurveyUserLookup(
        IServiceProvider serviceProvider,
        UserManager<IUser> userManager)
    {
        _serviceProvider = serviceProvider;
        _userManager = userManager;
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds)
    {
        var ids = (userIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (ids.Length == 0)
        {
            return names;
        }

        var userSearchService = _serviceProvider.GetService<IUserSearchService>();

        if (userSearchService is not null)
        {
            foreach (var user in await userSearchService.GetAsync(ids))
            {
                names[user.UserId] = user.UserName;
            }

            return names;
        }

        foreach (var id in ids)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user is not null)
            {
                names[id] = await _userManager.GetUserNameAsync(user);
            }
        }

        return names;
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
