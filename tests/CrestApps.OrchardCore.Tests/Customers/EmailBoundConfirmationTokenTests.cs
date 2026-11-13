using CrestApps.OrchardCore.Customers.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// SEC-C3: Orchard Core's e-mail confirmation token is bound to the user id and the security stamp, and changing the
/// e-mail in the profile editor rotates neither. An old link then confirmed the new address, and the customer merge
/// recorded it as proved and merged the guest orders placed with it. These run the real ASP.NET Core Identity user
/// manager with Orchard Core's token options and provider registration, then the Customers feature's replacement.
/// </summary>
public sealed class EmailBoundConfirmationTokenTests
{
    [Fact]
    public async Task AConfirmationLink_DoesNotConfirmAnAddressTheUserChangedToAfterItWasSent()
    {
        // Arrange
        await using var services = CreateServices();
        var manager = services.GetRequiredService<UserManager<IUser>>();
        var user = new User { UserId = "user-1", UserName = "pat", Email = "pat@example.test", SecurityStamp = "stamp-1" };

        var link = await manager.GenerateEmailConfirmationTokenAsync(user);

        // Act: the profile editor sets the new address directly, as Orchard Core's UserInformationDisplayDriver does.
        user.Email = "guest-buyer@example.test";
        await manager.UpdateAsync(user);

        var result = await manager.ConfirmEmailAsync(user, link);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task AConfirmationLink_ConfirmsTheAddressItWasSentTo()
    {
        // Arrange
        await using var services = CreateServices();
        var manager = services.GetRequiredService<UserManager<IUser>>();
        var user = new User { UserId = "user-1", UserName = "pat", Email = "pat@example.test", SecurityStamp = "stamp-1" };

        var link = await manager.GenerateEmailConfirmationTokenAsync(user);

        // Act: case and surrounding spaces are not a different address.
        user.Email = " PAT@example.test ";
        var result = await manager.ConfirmEmailAsync(user, link);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(user.EmailConfirmed);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddIdentityCore<IUser>();
        services.AddSingleton<IUserStore<IUser>, InMemoryUserStore>();

        // Orchard Core's Users feature, as its Startup registers it.
        services.AddOptions<EmailConfirmationTokenProviderOptions>();
        services.AddTransient<EmailConfirmationTokenProvider>();
        services.AddTransient<IConfigureOptions<IdentityOptions>, EmailConfirmationIdentityOptionsConfigurations>();

        // The Customers feature, which depends on Users, so its configuration runs after.
        services.AddTransient<EmailBoundConfirmationTokenProvider>();
        services.AddTransient<IConfigureOptions<IdentityOptions>, EmailBoundConfirmationIdentityOptions>();

        return services.BuildServiceProvider();
    }

    // The parts of Orchard Core's UserStore the token flow uses: the user's id, name, e-mail and security stamp.
    private sealed class InMemoryUserStore : IUserEmailStore<IUser>, IUserSecurityStampStore<IUser>
    {
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

        public Task<IdentityResult> CreateAsync(IUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> UpdateAsync(IUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(IUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult<IUser>(null);

        public Task<IUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
            => Task.FromResult<IUser>(null);

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

        public Task<IUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult<IUser>(null);

        public Task<string> GetNormalizedEmailAsync(IUser user, CancellationToken cancellationToken)
            => Task.FromResult(((User)user).NormalizedEmail);

        public Task SetNormalizedEmailAsync(IUser user, string normalizedEmail, CancellationToken cancellationToken)
        {
            ((User)user).NormalizedEmail = normalizedEmail;

            return Task.CompletedTask;
        }

        public Task SetSecurityStampAsync(IUser user, string stamp, CancellationToken cancellationToken)
        {
            ((User)user).SecurityStamp = stamp;

            return Task.CompletedTask;
        }

        public Task<string> GetSecurityStampAsync(IUser user, CancellationToken cancellationToken)
            => Task.FromResult(((User)user).SecurityStamp);

        public void Dispose()
        {
        }
    }
}
