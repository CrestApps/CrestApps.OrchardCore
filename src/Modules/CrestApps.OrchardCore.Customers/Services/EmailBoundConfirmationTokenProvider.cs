using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Orchard Core's e-mail confirmation token provider, with the token bound to the address it was sent to.
/// </summary>
/// <remarks>
/// Orchard Core's token is bound to the user id and the security stamp only, and changing the e-mail in the profile
/// editor rotates neither. An old confirmation link therefore confirmed whatever address the user had changed to, and
/// <c>CustomerMergeUserEventHandler</c> recorded that address as proved and merged the guest orders placed with it. With
/// the address in the token's purpose, a link only confirms the address it was e-mailed to.
/// </remarks>
public sealed class EmailBoundConfirmationTokenProvider : DataProtectorTokenProvider<IUser>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailBoundConfirmationTokenProvider"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="options">Orchard Core's options for its e-mail confirmation token: the same name and lifespan.</param>
    /// <param name="logger">The logger.</param>
    public EmailBoundConfirmationTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<EmailConfirmationTokenProviderOptions> options,
        ILogger<DataProtectorTokenProvider<IUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }

    /// <inheritdoc/>
    public override async Task<string> GenerateAsync(string purpose, UserManager<IUser> manager, IUser user)
        => await base.GenerateAsync(await BindAsync(purpose, manager, user), manager, user);

    /// <inheritdoc/>
    public override async Task<bool> ValidateAsync(string purpose, string token, UserManager<IUser> manager, IUser user)
        => await base.ValidateAsync(await BindAsync(purpose, manager, user), token, manager, user);

    // The address is compared the way UserEmailOwnership records it: trimmed, without case.
    private static async Task<string> BindAsync(string purpose, UserManager<IUser> manager, IUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var email = await manager.GetEmailAsync(user);

        return $"{purpose}:{email?.Trim().ToUpperInvariant()}";
    }
}
