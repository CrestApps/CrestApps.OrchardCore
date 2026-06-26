using CrestApps.OrchardCore.PhoneNumberVerifications;
using CrestApps.OrchardCore.PhoneNumberVerifications.Models;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumberVerifications;

public sealed class ContentItemPhoneNumberVerificationStoreTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsVerified_WhenStatusVerifiedAndLastVerifiedSet_ReturnsTrue()
    {
        // Arrange
        var store = CreateStore(new PhoneNumberVerificationsSettings());
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            VerificationStatus = PhoneNumberVerificationStatus.Verified,
            LastVerifiedUtc = _now.AddDays(-1),
        });

        // Act
        var isVerified = store.IsVerified(contentItem);

        // Assert
        Assert.True(isVerified);
    }

    [Fact]
    public void IsVerified_WhenStatusInvalid_ReturnsFalse()
    {
        // Arrange
        var store = CreateStore(new PhoneNumberVerificationsSettings());
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            VerificationStatus = PhoneNumberVerificationStatus.Invalid,
            LastVerifiedUtc = _now.AddDays(-1),
        });

        // Act
        var isVerified = store.IsVerified(contentItem);

        // Assert
        Assert.False(isVerified);
    }

    [Fact]
    public async Task RequiresRevalidationAsync_WhenNeverVerified_ReturnsTrue()
    {
        // Arrange
        var store = CreateStore(new PhoneNumberVerificationsSettings());
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());

        // Act
        var requires = await store.RequiresRevalidationAsync(contentItem, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(requires);
    }

    [Fact]
    public async Task RequiresRevalidationAsync_WhenNextDueInFuture_ReturnsFalse()
    {
        // Arrange
        var store = CreateStore(new PhoneNumberVerificationsSettings());
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            LastVerifiedUtc = _now.AddDays(-10),
            NextVerificationDueUtc = _now.AddDays(10),
        });

        // Act
        var requires = await store.RequiresRevalidationAsync(contentItem, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(requires);
    }

    [Fact]
    public async Task RequiresRevalidationAsync_WhenNextDueInPast_ReturnsTrue()
    {
        // Arrange
        var store = CreateStore(new PhoneNumberVerificationsSettings());
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            LastVerifiedUtc = _now.AddDays(-400),
            NextVerificationDueUtc = _now.AddDays(-35),
        });

        // Act
        var requires = await store.RequiresRevalidationAsync(contentItem, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(requires);
    }

    [Fact]
    public async Task UpdateAsync_WhenVerified_SetsNextVerificationDueFromInterval()
    {
        // Arrange
        var settings = new PhoneNumberVerificationsSettings
        {
            RevalidationIntervalDays = 30,
        };
        var store = CreateStore(settings);
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());
        var result = new PhoneNumberVerificationResult
        {
            PhoneNumber = "+17024993350",
            NormalizedPhoneNumber = "+17024993350",
            Status = PhoneNumberVerificationStatus.Verified,
            VerificationProvider = "AbstractApi",
            VerificationDateUtc = _now,
        };

        // Act
        await store.UpdateAsync(contentItem, result, "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));

        Assert.Equal(PhoneNumberVerificationStatus.Verified, part.VerificationStatus);
        Assert.Equal("AbstractApi", part.VerificationProvider);
        Assert.Equal("user-1", part.LastVerifiedByUserId);
        Assert.Equal(1, part.VerificationAttemptCount);
        Assert.Equal(_now, part.LastVerifiedUtc);
        Assert.Equal(_now.AddDays(30), part.NextVerificationDueUtc);
        Assert.False(string.IsNullOrEmpty(part.VerificationResultJson));
    }

    [Fact]
    public async Task UpdateAsync_WhenFailed_DoesNotSetLastVerified()
    {
        // Arrange
        var store = CreateStore(new PhoneNumberVerificationsSettings());
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart());
        var result = new PhoneNumberVerificationResult
        {
            PhoneNumber = "+17024993350",
            Status = PhoneNumberVerificationStatus.Failed,
            VerificationProvider = "AbstractApi",
            VerificationDateUtc = _now,
        };

        // Act
        await store.UpdateAsync(contentItem, result, verifiedByUserId: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(contentItem.TryGet<PhoneNumberVerificationPart>(out var part));

        Assert.Equal(PhoneNumberVerificationStatus.Failed, part.VerificationStatus);
        Assert.Null(part.LastVerifiedUtc);
        Assert.Null(part.NextVerificationDueUtc);
        Assert.Equal(1, part.VerificationAttemptCount);
    }

    [Fact]
    public void Read_WhenResultStored_DeserializesResult()
    {
        // Arrange
        var store = CreateStore(new PhoneNumberVerificationsSettings());
        var result = new PhoneNumberVerificationResult
        {
            PhoneNumber = "+17024993350",
            NormalizedPhoneNumber = "+17024993350",
            Carrier = "Example Carrier",
            Status = PhoneNumberVerificationStatus.Verified,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(result, PhoneNumberVerificationSerialization.Options);
        var contentItem = CreateContentItem(new PhoneNumberVerificationPart
        {
            VerificationResultJson = json,
        });

        // Act
        var stored = store.Read(contentItem);

        // Assert
        Assert.NotNull(stored);
        Assert.Equal("+17024993350", stored.NormalizedPhoneNumber);
        Assert.Equal("Example Carrier", stored.Carrier);
    }

    private static ContentItemPhoneNumberVerificationStore CreateStore(PhoneNumberVerificationsSettings settings)
    {
        var clock = Mock.Of<IClock>(c => c.UtcNow == _now);

        return new ContentItemPhoneNumberVerificationStore(CreateSiteService(settings), clock);
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

    private static ISiteService CreateSiteService(PhoneNumberVerificationsSettings settings)
    {
        var site = new Mock<ISite>();
        site.Setup(x => x.GetOrCreate<PhoneNumberVerificationsSettings>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
