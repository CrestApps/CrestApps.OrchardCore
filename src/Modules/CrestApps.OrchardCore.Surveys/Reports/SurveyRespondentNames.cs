using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// Names respondents by user id, through the display name provider, with one lookup per user per run. The user services
/// are resolved on first use, so a read that shows no respondent never needs them.
/// </summary>
internal sealed class SurveyRespondentNames
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);

    public SurveyRespondentNames(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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

        var user = _serviceProvider.GetService<UserManager<IUser>>() is { } userManager ? await userManager.FindByIdAsync(userId) : null;
        var name = user is null
            ? userId
            : (_serviceProvider.GetService<IDisplayNameProvider>() is { } displayNames ? await displayNames.GetAsync(user, cancellationToken) : null) ?? user.UserName;
        _names[userId] = name;

        return name;
    }
}
