using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Resolves the user a workflow task names. The setting may hold a user id, a user name or an e-mail address (Liquid often
/// gives a name); the task stores the user's id, so a value that names no user fails the task rather than storing text
/// the user-id columns can't hold.
/// </summary>
internal static class SurveyWorkflowUsers
{
    /// <summary>
    /// The longest user id the Surveys indexes store.
    /// </summary>
    public const int MaxUserIdLength = 26;

    /// <summary>
    /// Gets the id of the user a value names: by id, then by user name, then by e-mail address.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="value">The evaluated setting.</param>
    /// <returns>The user id, or <see langword="null"/> when no user matches.</returns>
    public static async Task<string> ResolveAsync(IServiceProvider services, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        if (services.GetService<UserManager<IUser>>() is not { } userManager)
        {
            // Without the Users feature nothing can be looked up; a value that fits the id columns is taken as an id.
            return value.Length <= MaxUserIdLength ? value : null;
        }

        var user = (value.Length <= MaxUserIdLength ? await userManager.FindByIdAsync(value) : null)
            ?? await userManager.FindByNameAsync(value)
            ?? (value.Contains('@') ? await userManager.FindByEmailAsync(value) : null);

        if (user is null)
        {
            return null;
        }

        var userId = await userManager.GetUserIdAsync(user);

        return userId?.Length <= MaxUserIdLength ? userId : null;
    }
}
