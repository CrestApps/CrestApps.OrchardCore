using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// The guest ownership token is what replaced an IP address and user agent as the proof that a guest owns a
/// pending checkout. These tests pin the property that made the change necessary: two visitors who look
/// identical on the wire must not be able to resume each other's sessions.
/// </summary>
public sealed class GuestSessionTokenManagerTests
{
    private static readonly GuestSessionTokenScope _scope = new(
        "checkout_owner",
        "CrestApps.Tests.GuestOwnership.v1",
        TimeSpan.FromDays(30));

    [Fact]
    public void Verify_ForTheBrowserThatWasIssuedTheToken_Succeeds()
    {
        // Arrange
        var protectionProvider = new EphemeralDataProtectionProvider();
        var issuingContext = new DefaultHttpContext();
        var manager = CreateManager(protectionProvider, issuingContext);

        // Act
        var hash = manager.Issue(_scope, "session-1");

        var returning = CreateReturningContext(issuingContext);
        var returningManager = CreateManager(protectionProvider, returning);

        // Assert
        Assert.NotNull(hash);
        Assert.True(returningManager.Verify(_scope, "session-1", hash));
    }

    /// <summary>
    /// This is the case the old check got wrong. Both visitors share a NAT address and a common browser, so
    /// an IP-and-user-agent comparison would have handed the second one the first one's checkout.
    /// </summary>
    [Fact]
    public void Verify_ForADifferentBrowser_Fails()
    {
        // Arrange
        var protectionProvider = new EphemeralDataProtectionProvider();
        var issuingContext = new DefaultHttpContext();
        var hash = CreateManager(protectionProvider, issuingContext).Issue(_scope, "session-1");

        // Act
        var stranger = CreateManager(protectionProvider, new DefaultHttpContext());

        // Assert
        Assert.False(stranger.Verify(_scope, "session-1", hash));
    }

    [Fact]
    public void Verify_WithAForgedCookie_Fails()
    {
        // Arrange
        var protectionProvider = new EphemeralDataProtectionProvider();
        var issuingContext = new DefaultHttpContext();
        var hash = CreateManager(protectionProvider, issuingContext).Issue(_scope, "session-1");

        var forged = new DefaultHttpContext();
        forged.Request.Headers.Cookie = $"{_scope.CookieName}=not-a-protected-payload";

        // Act
        var manager = CreateManager(protectionProvider, forged);

        // Assert
        Assert.False(manager.Verify(_scope, "session-1", hash));
    }

    /// <summary>
    /// A session with no stored hash predates the token, or was never issued one. Nobody may claim it.
    /// </summary>
    [Fact]
    public void Verify_WithNoStoredHash_Fails()
    {
        // Arrange
        var protectionProvider = new EphemeralDataProtectionProvider();
        var context = new DefaultHttpContext();
        var manager = CreateManager(protectionProvider, context);

        manager.Issue(_scope, "session-1");

        // Act & Assert
        Assert.False(manager.Verify(_scope, "session-1", null));
        Assert.False(manager.Verify(_scope, "session-1", string.Empty));
    }

    /// <summary>
    /// The token proves ownership of one specific session, not of every session the browser holds.
    /// </summary>
    [Fact]
    public void Verify_ForAnotherSessionId_Fails()
    {
        // Arrange
        var protectionProvider = new EphemeralDataProtectionProvider();
        var issuingContext = new DefaultHttpContext();
        var manager = CreateManager(protectionProvider, issuingContext);

        var firstHash = manager.Issue(_scope, "session-1");

        var returning = CreateReturningContext(issuingContext);
        var returningManager = CreateManager(protectionProvider, returning);

        // Act & Assert
        Assert.False(returningManager.Verify(_scope, "session-2", firstHash));
    }

    [Fact]
    public void Revoke_PreventsTheSessionFromBeingResumedAgain()
    {
        // Arrange
        var protectionProvider = new EphemeralDataProtectionProvider();
        var issuingContext = new DefaultHttpContext();
        var hash = CreateManager(protectionProvider, issuingContext).Issue(_scope, "session-1");

        var revoking = CreateReturningContext(issuingContext);
        var revokingManager = CreateManager(protectionProvider, revoking);

        // Act
        revokingManager.Revoke(_scope, "session-1");

        var afterRevoke = CreateReturningContext(revoking);

        // Assert
        Assert.False(CreateManager(protectionProvider, afterRevoke).Verify(_scope, "session-1", hash));
    }

    private static GuestSessionTokenManager CreateManager(IDataProtectionProvider protectionProvider, HttpContext httpContext)
    {
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        return new GuestSessionTokenManager(accessor, protectionProvider);
    }

    // Turns the cookie a response wrote into the cookie the next request sends, which is what a browser does
    // between two visits.
    private static DefaultHttpContext CreateReturningContext(DefaultHttpContext previous)
    {
        var context = new DefaultHttpContext();

        var setCookie = previous.Response.Headers.SetCookie.ToString();

        if (!string.IsNullOrEmpty(setCookie))
        {
            context.Request.Headers.Cookie = setCookie.Split(';')[0];
        }

        return context;
    }
}
