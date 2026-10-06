using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class RecordingGovernancePolicyTests
{
    [Fact]
    public void RecordingSettings_RecordEveryCallByDefault()
    {
        // Enabling the Call Recording feature is the decision to record; there is no second tenant switch that could
        // leave recording silently off. Every call is recorded automatically unless the tenant turns it off.
        var settings = new ContactCenterRecordingSettings();

        Assert.True(settings.RecordAllCalls);
    }

    [Fact]
    public async Task EvaluateStartAsync_WhenAllPartiesConsentRequiredAndConsentMissing_Denies()
    {
        // Arrange
        var settings = new ContactCenterRecordingSettings
        {
            ConsentModel = RecordingConsentModel.AllParties,
            RequireExplicitConsent = true,
        };
        var policy = CreatePolicy(settings, DateTime.UtcNow);

        // Act
        var decision = await policy.EvaluateStartAsync(new Interaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.Allowed);
        Assert.Equal(ContactCenterConstants.RecordingGovernanceDenyReason.ConsentRequired, decision.DenyReasonCode);
    }

    [Fact]
    public async Task EvaluateStartAsync_WhenConsentCaptured_Allows()
    {
        // Arrange
        var settings = new ContactCenterRecordingSettings
        {
            ConsentModel = RecordingConsentModel.AllParties,
            RequireExplicitConsent = true,
        };
        var policy = CreatePolicy(settings, DateTime.UtcNow);
        var interaction = new Interaction { RecordingConsentCapturedUtc = DateTime.UtcNow };

        // Act
        var decision = await policy.EvaluateStartAsync(interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task EvaluateStartAsync_WhenSinglePartyConsent_AllowsWithoutCapturedConsent()
    {
        // Arrange
        var settings = new ContactCenterRecordingSettings
        {
            ConsentModel = RecordingConsentModel.SingleParty,
            RequireExplicitConsent = true,
        };
        var policy = CreatePolicy(settings, DateTime.UtcNow);

        // Act
        var decision = await policy.EvaluateStartAsync(new Interaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task EvaluateStartAsync_WhenConsentModelIsUndefinedAndConsentRequired_FailsClosed()
    {
        // Arrange
        var settings = new ContactCenterRecordingSettings
        {
            ConsentModel = (RecordingConsentModel)999,
            RequireExplicitConsent = true,
        };
        var policy = CreatePolicy(settings, DateTime.UtcNow);

        // Act
        var decision = await policy.EvaluateStartAsync(new Interaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.Allowed);
        Assert.Equal(ContactCenterConstants.RecordingGovernanceDenyReason.ConsentRequired, decision.DenyReasonCode);
    }

    [Fact]
    public async Task EvaluateStartAsync_WhenRetentionExceedsMaximum_ClampsWithoutOverflow()
    {
        // Arrange
        var now = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var settings = new ContactCenterRecordingSettings
        {
            RetentionDays = int.MaxValue,
        };
        var policy = CreatePolicy(settings, now);

        // Act
        var decision = await policy.EvaluateStartAsync(new Interaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Allowed);
        Assert.Equal(now.AddDays(ContactCenterRecordingSettings.MaxRetentionDays), decision.RetainUntilUtc);
    }

    [Fact]
    public async Task EvaluateStartAsync_WhenRetentionConfigured_ResolvesRetainUntilAndLegalHold()
    {
        // Arrange
        var now = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var settings = new ContactCenterRecordingSettings
        {
            RetentionDays = 30,
            LegalHoldByDefault = true,
        };
        var policy = CreatePolicy(settings, now);

        // Act
        var decision = await policy.EvaluateStartAsync(new Interaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Allowed);
        Assert.Equal(now.AddDays(30), decision.RetainUntilUtc);
        Assert.True(decision.LegalHold);
    }

    [Fact]
    public async Task EvaluateStartAsync_WhenNoRetentionConfigured_ReturnsIndefiniteRetention()
    {
        // Arrange
        var settings = new ContactCenterRecordingSettings
        {
            RetentionDays = 0,
        };
        var policy = CreatePolicy(settings, DateTime.UtcNow);

        // Act
        var decision = await policy.EvaluateStartAsync(new Interaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Allowed);
        Assert.Null(decision.RetainUntilUtc);
        Assert.False(decision.LegalHold);
    }

    private static RecordingGovernancePolicy CreatePolicy(ContactCenterRecordingSettings settings, DateTime utcNow)
    {
        var siteService = SiteServiceFactory.Create(settings);
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(utcNow);

        return new RecordingGovernancePolicy(siteService, clock.Object);
    }
}
