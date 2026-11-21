using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// Reads the person a credential is awarded to and their name, which is resolved when it is shown and never copied.
/// </summary>
public class CredentialRecipientResolver
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialRecipientResolver"/> class.
    /// </summary>
    /// <param name="services">The services, for the user manager and the display name provider.</param>
    public CredentialRecipientResolver(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// Finds a recipient.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The recipient, or <see langword="null"/> when there is no such user.</returns>
    public virtual async Task<CredentialRecipient> FindAsync(string userId)
    {
        var userManager = _services.GetRequiredService<UserManager<IUser>>();
        var user = await userManager.FindByIdAsync(userId);

        return user is null ? null : new CredentialRecipient(userId, await userManager.GetEmailAsync(user));
    }

    /// <summary>
    /// Gets the name of a person, as the site shows it.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The name, or <see langword="null"/> when there is no such user.</returns>
    public virtual async Task<string> GetDisplayNameAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var user = await _services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId);

        if (user is null)
        {
            return null;
        }

        var provider = _services.GetService<IDisplayNameProvider>();
        var name = provider is null ? null : await provider.GetAsync(user);

        return string.IsNullOrWhiteSpace(name) ? user.UserName : name;
    }
}
