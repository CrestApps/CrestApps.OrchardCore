using CrestApps.OrchardCore.PhoneNumbers.Verifications;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications;

public sealed class PhoneNumberVerificationPartExtensionsTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsPhoneNumberVerified_WhenStatusVerifiedAndLastVerifiedSet_ReturnsTrue()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            VerificationStatus = PhoneNumberVerificationStatus.Verified,
            LastVerifiedUtc = _now.AddDays(-1),
        });

        // Act
        var isVerified = contentItem.IsPhoneNumberVerified();

        // Assert
        Assert.True(isVerified);
    }

    [Fact]
    public void IsPhoneNumberVerified_WhenStatusInvalid_ReturnsFalse()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            VerificationStatus = PhoneNumberVerificationStatus.Invalid,
            LastVerifiedUtc = _now.AddDays(-1),
        });

        // Act
        var isVerified = contentItem.IsPhoneNumberVerified();

        // Assert
        Assert.False(isVerified);
    }

    [Fact]
    public void RequiresPhoneNumberRevalidation_WhenNeverVerified_ReturnsTrue()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());

        // Act
        var requires = contentItem.RequiresPhoneNumberRevalidation(_now);

        // Assert
        Assert.True(requires);
    }

    [Fact]
    public void RequiresPhoneNumberRevalidation_WhenNextDueInFuture_ReturnsFalse()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            LastVerifiedUtc = _now.AddDays(-10),
            NextVerificationDueUtc = _now.AddDays(10),
        });

        // Act
        var requires = contentItem.RequiresPhoneNumberRevalidation(_now);

        // Assert
        Assert.False(requires);
    }

    [Fact]
    public void RequiresPhoneNumberRevalidation_WhenNextDueInPast_ReturnsTrue()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            LastVerifiedUtc = _now.AddDays(-400),
            NextVerificationDueUtc = _now.AddDays(-35),
        });

        // Act
        var requires = contentItem.RequiresPhoneNumberRevalidation(_now);

        // Assert
        Assert.True(requires);
    }

    [Fact]
    public void AlterPhoneNumberVerificationResult_WhenVerified_StoresResultOnPart()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());
        var result = new PhoneNumberVerificationResult
        {
            PhoneNumber = "14159929960",
            NormalizedPhoneNumber = "+14159929960",
            Status = PhoneNumberVerificationStatus.Verified,
            VerificationProvider = "AbstractApi",
            VerificationDateUtc = _now,
            Carrier = "T-Mobile",
            LineType = PhoneNumberLineType.Mobile,
        };

        // Act
        contentItem.AlterPhoneNumberVerificationResult(result, "user-1", revalidationIntervalDays: 30);

        // Assert
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));

        Assert.Equal("14159929960", part.PhoneNumber);
        Assert.Equal("+14159929960", part.NormalizedPhoneNumber);
        Assert.Equal(PhoneNumberVerificationStatus.Verified, part.VerificationStatus);
        Assert.Equal("AbstractApi", part.VerificationProvider);
        Assert.Equal("user-1", part.LastVerifiedByUserId);
        Assert.Equal(1, part.VerificationAttemptCount);
        Assert.Equal(_now, part.LastVerifiedUtc);
        Assert.Equal(_now.AddDays(30), part.NextVerificationDueUtc);
        Assert.False(string.IsNullOrEmpty(part.VerificationResultJson));
    }

    [Fact]
    public void AlterPhoneNumberVerificationResult_WhenFailed_DoesNotSetLastVerified()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());
        var result = new PhoneNumberVerificationResult
        {
            PhoneNumber = "+17024993350",
            Status = PhoneNumberVerificationStatus.Failed,
            VerificationProvider = "AbstractApi",
            VerificationDateUtc = _now,
        };

        // Act
        contentItem.AlterPhoneNumberVerificationResult(result);

        // Assert
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));

        Assert.Equal(PhoneNumberVerificationStatus.Failed, part.VerificationStatus);
        Assert.Null(part.LastVerifiedUtc);
        Assert.Null(part.NextVerificationDueUtc);
        Assert.Equal(1, part.VerificationAttemptCount);
    }

    [Fact]
    public void TryGetPhoneNumberVerificationResult_WhenResultStored_ReturnsResult()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());
        var result = new PhoneNumberVerificationResult
        {
            PhoneNumber = "+17024993350",
            NormalizedPhoneNumber = "+17024993350",
            Carrier = "Example Carrier",
            Status = PhoneNumberVerificationStatus.Verified,
        };

        contentItem.AlterPhoneNumberVerificationResult(result);

        // Act
        var found = contentItem.TryGetPhoneNumberVerificationResult(out var stored);

        // Assert
        Assert.True(found);
        Assert.NotNull(stored);
        Assert.Equal("+17024993350", stored.NormalizedPhoneNumber);
        Assert.Equal("Example Carrier", stored.Carrier);
    }

    [Fact]
    public void AlterPhoneNumberVerificationPending_WhenPhoneNumberProvided_StoresUnverifiedPart()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());

        // Act
        contentItem.AlterPhoneNumberVerificationPending("14159929960", "+14159929960");

        // Assert
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));

        Assert.Equal("14159929960", part.PhoneNumber);
        Assert.Equal("+14159929960", part.NormalizedPhoneNumber);
        Assert.Equal(PhoneNumberVerificationStatus.Unverified, part.VerificationStatus);
        Assert.Null(part.VerificationProvider);
        Assert.Null(part.VerificationResultJson);
        Assert.Null(part.LastVerifiedUtc);
        Assert.Null(part.NextVerificationDueUtc);
    }

    [Fact]
    public void ClearPhoneNumberVerification_WhenPartExists_ClearsVerificationData()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            PhoneNumber = "14159929960",
            NormalizedPhoneNumber = "+14159929960",
            VerificationStatus = PhoneNumberVerificationStatus.Verified,
            VerificationProvider = "AbstractApi",
            VerificationResultJson = "{}",
            LastVerifiedUtc = _now,
            NextVerificationDueUtc = _now.AddDays(30),
        });

        // Act
        contentItem.ClearPhoneNumberVerification();

        // Assert
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));

        Assert.Null(part.PhoneNumber);
        Assert.Null(part.NormalizedPhoneNumber);
        Assert.Equal(PhoneNumberVerificationStatus.Unverified, part.VerificationStatus);
        Assert.Null(part.VerificationProvider);
        Assert.Null(part.VerificationResultJson);
        Assert.Null(part.LastVerifiedUtc);
        Assert.Null(part.NextVerificationDueUtc);
    }

    private static ContentItem CreateContentItem(PhoneNumberVerificationPart part)
    {
        var contentItem = new ContentItem
        {
            ContentType = "Contact",
        };

        contentItem.Apply(part);

        return contentItem;
    }
}
