using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Agent.Users;

/// <summary>
/// Keeps the users of a tool test in memory behind the store interfaces the user manager needs, and records what was
/// stored and deleted, so a test can tell that a refused call changed nothing.
/// </summary>
internal sealed class FakeUserStore :
    IUserRoleStore<IUser>,
    IUserEmailStore<IUser>,
    IUserLockoutStore<IUser>,
    IUserTwoFactorStore<IUser>,
    IUserAuthenticatorKeyStore<IUser>,
    IUserSecurityStampStore<IUser>,
    IUserPasswordStore<IUser>,
    IUserPhoneNumberStore<IUser>
{
    /// <summary>
    /// The login provider of the authenticator key token.
    /// </summary>
    public const string AuthenticatorKeyProvider = "[AspNetUserStore]";

    /// <summary>
    /// The name of the authenticator key token.
    /// </summary>
    public const string AuthenticatorKeyName = "AuthenticatorKey";

    /// <summary>
    /// Gets the stored users.
    /// </summary>
    public List<User> Users { get; } = [];

    /// <summary>
    /// Gets the IDs of the users that were stored after a change, in order.
    /// </summary>
    public List<string> Updated { get; } = [];

    /// <summary>
    /// Gets the IDs of the users that were deleted, in order.
    /// </summary>
    public List<string> Deleted { get; } = [];

    /// <summary>
    /// Gets or sets the error the store answers an update or a delete with. The default is none.
    /// </summary>
    public IdentityError Failure { get; set; }

    public Task<IdentityResult> CreateAsync(IUser user, CancellationToken cancellationToken)
    {
        Users.Add((User)user);

        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> UpdateAsync(IUser user, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromResult(IdentityResult.Failed(Failure));
        }

        Updated.Add(((User)user).UserId);

        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(IUser user, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromResult(IdentityResult.Failed(Failure));
        }

        Users.Remove((User)user);
        Deleted.Add(((User)user).UserId);

        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
        => Task.FromResult<IUser>(Users.FirstOrDefault(user => user.UserId == userId));

    public Task<IUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        => Task.FromResult<IUser>(Users.FirstOrDefault(user => string.Equals(user.UserName, normalizedUserName, StringComparison.OrdinalIgnoreCase)));

    public Task<IUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        => Task.FromResult<IUser>(Users.FirstOrDefault(user => string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)));

    public Task<string> GetUserIdAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).UserId);

    public Task<string> GetUserNameAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.UserName);

    public Task SetUserNameAsync(IUser user, string userName, CancellationToken cancellationToken)
    {
        ((User)user).UserName = userName;

        return Task.CompletedTask;
    }

    public Task<string> GetNormalizedUserNameAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).NormalizedUserName);

    public Task SetNormalizedUserNameAsync(IUser user, string normalizedName, CancellationToken cancellationToken)
    {
        ((User)user).NormalizedUserName = normalizedName;

        return Task.CompletedTask;
    }

    public Task AddToRoleAsync(IUser user, string roleName, CancellationToken cancellationToken)
    {
        ((User)user).RoleNames.Add(ToRoleName(roleName));

        return Task.CompletedTask;
    }

    public Task RemoveFromRoleAsync(IUser user, string roleName, CancellationToken cancellationToken)
    {
        var roleNames = ((User)user).RoleNames;
        var stored = roleNames.FirstOrDefault(name => string.Equals(name, roleName, StringComparison.OrdinalIgnoreCase));

        if (stored is not null)
        {
            roleNames.Remove(stored);
        }

        return Task.CompletedTask;
    }

    public Task<IList<string>> GetRolesAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).RoleNames);

    public Task<bool> IsInRoleAsync(IUser user, string roleName, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).RoleNames.Contains(roleName, StringComparer.OrdinalIgnoreCase));

    public Task<IList<IUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        => Task.FromResult<IList<IUser>>(Users.Where(user => user.RoleNames.Contains(roleName, StringComparer.OrdinalIgnoreCase)).ToList<IUser>());

    public Task SetEmailAsync(IUser user, string email, CancellationToken cancellationToken)
    {
        ((User)user).Email = email;

        return Task.CompletedTask;
    }

    public Task<string> GetEmailAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).Email);

    public Task<bool> GetEmailConfirmedAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).EmailConfirmed);

    public Task SetEmailConfirmedAsync(IUser user, bool confirmed, CancellationToken cancellationToken)
    {
        ((User)user).EmailConfirmed = confirmed;

        return Task.CompletedTask;
    }

    public Task<string> GetNormalizedEmailAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).NormalizedEmail);

    public Task SetNormalizedEmailAsync(IUser user, string normalizedEmail, CancellationToken cancellationToken)
    {
        ((User)user).NormalizedEmail = normalizedEmail;

        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(IUser user, CancellationToken cancellationToken)
    {
        var lockoutEnd = ((User)user).LockoutEndUtc;

        return Task.FromResult(lockoutEnd.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(lockoutEnd.Value, DateTimeKind.Utc))
            : (DateTimeOffset?)null);
    }

    public Task SetLockoutEndDateAsync(IUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        ((User)user).LockoutEndUtc = lockoutEnd?.UtcDateTime;

        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(++((User)user).AccessFailedCount);

    public Task ResetAccessFailedCountAsync(IUser user, CancellationToken cancellationToken)
    {
        ((User)user).AccessFailedCount = 0;

        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).IsLockoutEnabled);

    public Task SetLockoutEnabledAsync(IUser user, bool enabled, CancellationToken cancellationToken)
    {
        ((User)user).IsLockoutEnabled = enabled;

        return Task.CompletedTask;
    }

    public Task SetTwoFactorEnabledAsync(IUser user, bool enabled, CancellationToken cancellationToken)
    {
        ((User)user).TwoFactorEnabled = enabled;

        return Task.CompletedTask;
    }

    public Task<bool> GetTwoFactorEnabledAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).TwoFactorEnabled);

    // The user store keeps the authenticator key as a token of the user, as Orchard Core's user store does.
    public Task SetAuthenticatorKeyAsync(IUser user, string key, CancellationToken cancellationToken)
    {
        var tokens = ((User)user).UserTokens;

        foreach (var existing in tokens.Where(token => token.LoginProvider == AuthenticatorKeyProvider && token.Name == AuthenticatorKeyName).ToList())
        {
            tokens.Remove(existing);
        }

        tokens.Add(new UserToken { LoginProvider = AuthenticatorKeyProvider, Name = AuthenticatorKeyName, Value = key });

        return Task.CompletedTask;
    }

    public Task<string> GetAuthenticatorKeyAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).UserTokens.FirstOrDefault(token => token.LoginProvider == AuthenticatorKeyProvider && token.Name == AuthenticatorKeyName)?.Value);

    public Task SetSecurityStampAsync(IUser user, string stamp, CancellationToken cancellationToken)
    {
        ((User)user).SecurityStamp = stamp;

        return Task.CompletedTask;
    }

    public Task<string> GetSecurityStampAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).SecurityStamp);

    public Task SetPasswordHashAsync(IUser user, string passwordHash, CancellationToken cancellationToken)
    {
        ((User)user).PasswordHash = passwordHash;

        return Task.CompletedTask;
    }

    public Task<string> GetPasswordHashAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).PasswordHash);

    public Task<bool> HasPasswordAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(!string.IsNullOrEmpty(((User)user).PasswordHash));

    public Task SetPhoneNumberAsync(IUser user, string phoneNumber, CancellationToken cancellationToken)
    {
        ((User)user).PhoneNumber = phoneNumber;

        return Task.CompletedTask;
    }

    public Task<string> GetPhoneNumberAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).PhoneNumber);

    public Task<bool> GetPhoneNumberConfirmedAsync(IUser user, CancellationToken cancellationToken)
        => Task.FromResult(((User)user).PhoneNumberConfirmed);

    public Task SetPhoneNumberConfirmedAsync(IUser user, bool confirmed, CancellationToken cancellationToken)
    {
        ((User)user).PhoneNumberConfirmed = confirmed;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }

    // The user manager gives the store a normalized (upper-case) role name; the stored name keeps its casing.
    private static string ToRoleName(string normalizedRoleName)
        => UserRoleToolHarness.RoleNames.FirstOrDefault(name => string.Equals(name, normalizedRoleName, StringComparison.OrdinalIgnoreCase)) ?? normalizedRoleName;
}
