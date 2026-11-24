using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Turns the user id an agreement stores as its owner into the name a person reads, as the agreement's admin page
/// shows it: the user's display name when a provider is registered, otherwise the user name.
/// </summary>
internal static class SubscriptionOwnerNames
{
    /// <summary>
    /// Gets the name of an agreement's owner.
    /// </summary>
    /// <param name="services">The services of the request or tool call.</param>
    /// <param name="ownerId">The owner's user id.</param>
    /// <returns>The name, or <see langword="null"/> when there is no owner or the user no longer exists.</returns>
    public static async Task<string> ResolveAsync(IServiceProvider services, string ownerId)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrEmpty(ownerId) || services.GetService<IUserService>() is not { } userService)
        {
            return null;
        }

        var user = await userService.GetUserByUniqueIdAsync(ownerId);

        if (user is null)
        {
            return null;
        }

        return services.GetService<IDisplayNameProvider>() is { } displayNameProvider
            ? await displayNameProvider.GetAsync(user)
            : user.UserName;
    }
}
