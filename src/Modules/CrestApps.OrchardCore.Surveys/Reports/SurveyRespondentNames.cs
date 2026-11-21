using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// Names respondents by user id, through the display name provider, with one lookup per user per run.
/// </summary>
internal sealed class SurveyRespondentNames
{
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);

    public SurveyRespondentNames(
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider)
    {
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
    }

    /// <summary>
    /// Gets the name of a user.
    /// </summary>
    /// <param name="userId">The user id, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The name, the id of a user who no longer exists, or an empty string.</returns>
    public async Task<string> GetAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return string.Empty;
        }

        if (_names.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var user = await _userManager.FindByIdAsync(userId);
        var name = user is null ? userId : await _displayNameProvider.GetAsync(user, cancellationToken) ?? user.UserName;
        _names[userId] = name;

        return name;
    }
}
