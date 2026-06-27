using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
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
    public void AlterPhoneNumberVerificationResult_WhenFailed_IncrementsFailedAttemptCountAndStoresError()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());
        var result = new PhoneNumberVerificationResult
        {
            PhoneNumber = "+17024993350",
            Status = PhoneNumberVerificationStatus.Failed,
            VerificationProvider = "AbstractApi",
            VerificationDateUtc = _now,
            ErrorMessage = "Provider returned 429 Too Many Requests.",
        };

        // Act
        contentItem.AlterPhoneNumberVerificationResult(result);
        contentItem.AlterPhoneNumberVerificationResult(result);

        // Assert
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));

        Assert.Equal(PhoneNumberVerificationStatus.Failed, part.VerificationStatus);
        Assert.Equal(2, part.FailedAttemptCount);
        Assert.Equal(2, part.VerificationAttemptCount);
        Assert.Equal("Provider returned 429 Too Many Requests.", part.LastError);
        Assert.Equal(_now, part.LastAttemptUtc);
        Assert.Null(part.LastVerifiedUtc);
    }

    [Fact]
    public void AlterPhoneNumberVerificationResult_WhenFailedAfterVerified_DoesNotDowngradeStatus()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());
        var verified = new PhoneNumberVerificationResult
        {
            PhoneNumber = "14159929960",
            NormalizedPhoneNumber = "+14159929960",
            Status = PhoneNumberVerificationStatus.Verified,
            VerificationProvider = "AbstractApi",
            VerificationDateUtc = _now,
        };

        contentItem.AlterPhoneNumberVerificationResult(verified, "user-1", revalidationIntervalDays: 30);

        var failed = new PhoneNumberVerificationResult
        {
            PhoneNumber = "14159929960",
            Status = PhoneNumberVerificationStatus.Failed,
            VerificationProvider = "AbstractApi",
            VerificationDateUtc = _now.AddDays(31),
            ErrorMessage = "Provider rate limit exceeded.",
        };

        // Act
        contentItem.AlterPhoneNumberVerificationResult(failed);

        // Assert
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));

        Assert.Equal(PhoneNumberVerificationStatus.Verified, part.VerificationStatus);
        Assert.Equal(_now, part.LastVerifiedUtc);
        Assert.Equal(_now.AddDays(30), part.NextVerificationDueUtc);
        Assert.Equal(1, part.FailedAttemptCount);
        Assert.Equal("Provider rate limit exceeded.", part.LastError);
    }

    [Fact]
    public void AlterPhoneNumberVerificationResult_WhenCompletedAfterFailure_ResetsFailureState()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());
        var failed = new PhoneNumberVerificationResult
        {
            PhoneNumber = "14159929960",
            Status = PhoneNumberVerificationStatus.Failed,
            VerificationProvider = "AbstractApi",
            VerificationDateUtc = _now,
            ErrorMessage = "Transient error.",
        };

        contentItem.AlterPhoneNumberVerificationResult(failed);

        var verified = new PhoneNumberVerificationResult
        {
            PhoneNumber = "14159929960",
            NormalizedPhoneNumber = "+14159929960",
            Status = PhoneNumberVerificationStatus.Verified,
            VerificationProvider = "AbstractApi",
            VerificationDateUtc = _now.AddMinutes(5),
        };

        // Act
        contentItem.AlterPhoneNumberVerificationResult(verified, "user-1", revalidationIntervalDays: 30);

        // Assert
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));

        Assert.Equal(PhoneNumberVerificationStatus.Verified, part.VerificationStatus);
        Assert.Equal(0, part.FailedAttemptCount);
        Assert.Null(part.LastError);
        Assert.Equal(_now.AddMinutes(5), part.LastVerifiedUtc);
    }

    [Fact]
    public void RequeuePhoneNumberVerification_WhenExhausted_ResetsFailureCountersAndDueDate()
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            VerificationStatus = PhoneNumberVerificationStatus.Failed,
            FailedAttemptCount = 3,
            LastError = "Provider rate limit exceeded.",
            NextVerificationDueUtc = _now.AddDays(30),
        });

        // Act
        contentItem.RequeuePhoneNumberVerification();

        // Assert
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));

        Assert.Equal(0, part.FailedAttemptCount);
        Assert.Null(part.LastError);
        Assert.Null(part.NextVerificationDueUtc);
    }

    [Theory]
    [InlineData(0, 3, false)]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    public void HasReachedMaxVerificationAttempts_ReturnsExpectedResult(int failedAttemptCount, int maxAttempts, bool expected)
    {
        // Arrange
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            FailedAttemptCount = failedAttemptCount,
        });

        // Act
        var reached = contentItem.HasReachedMaxVerificationAttempts(maxAttempts);

        // Assert
        Assert.Equal(expected, reached);
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
