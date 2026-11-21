using System.Reflection;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Services;
using Moq;
using OpenIddict.Abstractions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// HT2 (fix-verification review): with the OpenID server on, tokens issued to an account before the proof of its
/// address kept working, because the refresh exchange rebuilds the principal from the user id with no stamp check.
/// Orchard Core's OpenID server issues them with the user id as the subject, and its YesSql stores revoke by subject.
/// </summary>
public sealed class OpenIdUserSignInRevocationTests
{
    [Fact]
    public async Task TheAccountsAuthorizationsAndTokens_AreRevokedBySubject()
    {
        var authorizations = new Mock<IOpenIddictAuthorizationManager>();
        var tokens = new Mock<IOpenIddictTokenManager>();

        await new OpenIdUserSignInRevocation(authorizations.Object, tokens.Object).RevokeAsync("user-1", TestContext.Current.CancellationToken);

        authorizations.Verify(manager => manager.RevokeBySubjectAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
        tokens.Verify(manager => manager.RevokeBySubjectAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TheRevocation_IsRegisteredOnlyWhenTheOpenIdServerIsOn()
    {
        // The Users module does not depend on the OpenID module: the startup that registers the revocation is skipped
        // unless the server's feature is on, which is also what registers the managers it takes.
        var startup = typeof(EmailCodeSignInOpenIdStartup);

        Assert.Equal(UsersConstants.Feature.EmailOwnership, startup.GetCustomAttribute<FeatureAttribute>()?.FeatureName);
        Assert.Equal(new[] { OrchardCoreFeatureIds.OpenIdServer }, startup.GetCustomAttribute<RequireFeaturesAttribute>()?.RequiredFeatureNames);
    }
}
