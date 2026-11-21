using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// HT2 leftover (third fix-verification review): publishing the reset to the distributed cache was outside the try
/// that guards the revocations, so a cache outage threw after the proof was saved, and the OpenID tokens were never
/// revoked. These run the proof over Orchard Core's user store on SQLite with a cache whose server can't be reached.
/// </summary>
public sealed class CredentialsResetPublishTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await EmailSignInHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task WhenTheResetCantBePublished_TheProofIsKept_AndWhatWasIssuedIsStillRevoked()
    {
        // Arrange
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, EmailConfirmed = true, IsEnabled = true });
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The cache server can't be reached."));
        var resets = new UserCredentialsResets(cache.Object, new ShellSettings { Name = "Default" }, Options.Create(new SecurityStampValidatorOptions()), NullLogger<UserCredentialsResets>.Instance);

        // Act
        var result = await _harness.InRequestAsync(async services =>
            await _harness.CreateProof(services, resets).RecordAsync((User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync("user-1"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(EmailProofResult.CredentialsRemoved, result);
        Assert.Equal(["user-1"], _harness.Revoked);

        var account = await _harness.InRequestAsync(async services => (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync("user-1"));
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }
}
