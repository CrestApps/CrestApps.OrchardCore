using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Tests.Core.Security;

/// <summary>
/// DRY-2 step 2: four classes built their own time-limited protector instead of <c>SignedTokenService</c>. They use it
/// now, with the purposes they always had, so a tracking link, a picture link, a delegated sign-in state cookie or a
/// kiosk ticket made before the change still opens.
/// </summary>
public sealed class SignedLinkMigrationTests
{
    [Theory]
    [InlineData("CrestApps.Dispatch.Tracking")]
    [InlineData("CrestApps.Omnichannel.Messaging.Attachments.Public")]
    [InlineData("TenantHierarchy.DelegatedAccess.State")]
    [InlineData("CrestApps.Workforce.Kiosk")]
    public void ATokenMadeTheOldWay_IsReadBySignedTokenService(string purpose)
    {
        // Arrange: exactly what each class did before.
        var protection = new EphemeralDataProtectionProvider();
        var old = protection.CreateProtector(purpose).ToTimeLimitedDataProtector().Protect("visit-1", TimeSpan.FromMinutes(5));

        // Act
        var value = SignedLinkDoubles.Tokens(protection).Unprotect(purpose, old);

        // Assert
        Assert.Equal("visit-1", value);
    }
}
