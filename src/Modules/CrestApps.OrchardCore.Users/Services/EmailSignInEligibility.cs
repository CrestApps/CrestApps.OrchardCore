using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Roles;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Which accounts a code e-mailed to their address can sign in, or confirm the address of: enabled, not locked out,
/// not using two-factor sign-in, which a code would get around, not in a role kept to password sign-in (Administrator
/// by default), and, to sign in, not one that signs in only through an external provider whose address is proved, whose
/// own checks a code would get around too. One whose address isn't proved may be somebody else's registration with the
/// owner's address, or the owner's own passwordless account that no provider proved, so a right code only offers to take
/// it back: the owner chooses to remove its logins and use their email, and only then is anything removed and the
/// address proved (see <see cref="Controllers.EmailSignInController"/>). A code never signs in to it with its logins
/// kept. An external sign-in whose provider asserted the same address as verified proves it
/// (<see cref="EmailOwnershipExternalLoginHandler"/>), so such an account gets no code at all. An account without a
/// password that signs in with passkeys is served like one with a password once its address is proved (a passkey is a
/// local credential, and the code is mailed to the proved owner), and like an external-only one until then: a right code
/// only offers to remove its passkeys.
/// </summary>
public sealed class EmailSignInEligibility
{
    private readonly UserManager<IUser> _userManager;
    private readonly ISystemRoleProvider _systemRoleProvider;
    private readonly EmailSignInOptions _options;

    public EmailSignInEligibility(UserManager<IUser> userManager, ISystemRoleProvider systemRoleProvider, IOptions<EmailSignInOptions> options)
    {
        _userManager = userManager;
        _systemRoleProvider = systemRoleProvider;
        _options = options.Value;
    }

    /// <summary>
    /// Whether a code can sign in to <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The account.</param>
    public async Task<bool> AllowsAsync(IUser user)
        => await AllowsConfirmationAsync(user) &&
            (await _userManager.HasPasswordAsync(user) ||
                (await _userManager.GetLoginsAsync(user)).Count == 0 ||
                user is User account && !UserEmailOwnership.IsVerified(account.Properties, account.Email));

    /// <summary>
    /// Whether a code can confirm the address of <paramref name="user"/>, who is signed in already: the accounts a code
    /// can sign in to, and also one that signs in only through an external provider, since confirming signs nobody in.
    /// </summary>
    /// <param name="user">The account.</param>
    public async Task<bool> AllowsConfirmationAsync(IUser user)
        => user is not null &&
            (user is not User account || account.IsEnabled) &&
            !await _userManager.IsLockedOutAsync(user) &&
            !await _userManager.GetTwoFactorEnabledAsync(user) &&
            !await IsInExcludedRoleAsync(user);

    // "Administrator", the default, stands for the tenant's administrator role whatever the tenant named it.
    private async Task<bool> IsInExcludedRoleAsync(IUser user)
    {
        foreach (var role in _options.ExcludedRoles ?? [])
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }

            if (await _userManager.IsInRoleAsync(user, role) ||
                (string.Equals(role.Trim(), OrchardCoreConstants.Roles.Administrator, StringComparison.OrdinalIgnoreCase) &&
                _systemRoleProvider.GetAdminRole()?.RoleName is { Length: > 0 } adminRole &&
                await _userManager.IsInRoleAsync(user, adminRole)))
            {
                return true;
            }
        }

        return false;
    }
}
