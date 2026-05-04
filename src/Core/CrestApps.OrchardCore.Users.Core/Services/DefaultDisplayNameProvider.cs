using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// A basic display-name provider that returns the username as the display name.
/// </summary>
public sealed class DefaultDisplayNameProvider : IDisplayNameProvider
{
    /// <inheritdoc />
    public Task<string> GetAsync(IUser user)
        => Task.FromResult(user?.UserName ?? string.Empty);
}
