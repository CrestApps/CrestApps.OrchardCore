using System.Reflection;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.OrchardCore.Core.Configuration;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Core.Configuration;

/// <summary>
/// Pins the bridge that exposes a tenant's site settings as options.
/// </summary>
/// <remarks>
/// The services that read these settings decide whether a call is recorded, whether a card number is
/// captured, and which outside numbers an agent may transfer to. A property that silently keeps its
/// default rather than the operator's value is a policy change nobody asked for, so the tests below
/// assert over every property of each settings type rather than over a hand-picked few.
/// </remarks>
public sealed class SiteSettingsOptionsConfigurationTests
{
    [Fact]
    public void Configure_CarriesEveryPropertyOfTheRecordingSettings()
        => AssertEveryPropertyIsCarried(new ContactCenterRecordingSettings
        {
            RecordingEnabled = true,
            ConsentModel = RecordingConsentModel.SingleParty,
            RequireExplicitConsent = true,
            RetentionDays = 42,
            LegalHoldByDefault = true,
            AllowAgentSecurePause = true,
            MaxSecurePauseSeconds = 120,
            RequirePauseReason = true,
        });

    [Fact]
    public void Configure_CarriesEveryPropertyOfTheSecureCaptureSettings()
        => AssertEveryPropertyIsCarried(new SecureCaptureSettings
        {
            Enabled = true,
            LinkTimeToLiveSeconds = SecureCaptureSettings.DefaultLinkTimeToLiveSeconds + 30,
            PauseRecordingDuringCapture = false,
        });

    [Fact]
    public void Configure_CarriesEveryPropertyOfTheExternalTransferSettings()
        => AssertEveryPropertyIsCarried(new ContactCenterExternalTransferSettings
        {
            Destinations =
            [
                new ContactCenterExternalDestination
                {
                    Id = "support",
                    Enabled = true,
                    E164Address = "+15550000000",
                },
            ],
        });

    [Fact]
    public void Configure_WhenTheTenantHasNoSettings_LeavesTheDefaults()
    {
        // Arrange
        // A site whose settings accessor is not set up stands in for a tenant that has never saved
        // these settings: the accessor answers with nothing.
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(new Mock<ISite>().Object);

        var configuration = new SiteSettingsOptionsConfiguration<ContactCenterRecordingSettings>(siteService.Object);
        var options = new ContactCenterRecordingSettings { RetentionDays = 7 };

        // Act
        configuration.Configure(options);

        // Assert: a tenant that has never opened the settings screen keeps whatever the code decided,
        // rather than having every value overwritten with null.
        Assert.Equal(7, options.RetentionDays);
    }

    [Fact]
    public void Configure_WithoutAnOptionsInstance_Throws()
    {
        // Arrange
        var configuration = new SiteSettingsOptionsConfiguration<ContactCenterRecordingSettings>(
            SiteServiceFactory.Create(new ContactCenterRecordingSettings()));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => configuration.Configure(null));
    }

    /// <summary>
    /// Asserts that configuring from <paramref name="stored"/> reproduces it property for property.
    /// </summary>
    /// <remarks>
    /// Every value in <paramref name="stored"/> must differ from the type's default, or the
    /// assertion would pass for a property that was never copied.
    /// </remarks>
    /// <typeparam name="TOptions">The settings type.</typeparam>
    /// <param name="stored">The settings as the tenant has them.</param>
    private static void AssertEveryPropertyIsCarried<TOptions>(TOptions stored)
        where TOptions : class, new()
    {
        // Arrange
        var configuration = new SiteSettingsOptionsConfiguration<TOptions>(SiteServiceFactory.Create(stored));
        var options = new TOptions();
        var defaults = new TOptions();

        var properties = typeof(TOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetSetMethod() is not null)
            .ToArray();

        Assert.NotEmpty(properties);

        // Act
        configuration.Configure(options);

        // Assert
        foreach (var property in properties)
        {
            Assert.True(
                !Equals(property.GetValue(stored), property.GetValue(defaults)),
                $"'{typeof(TOptions).Name}.{property.Name}' is at its default in this test's stored " +
                "settings, so copying it would not be observable. Give it a non-default value.");

            Assert.Equal(property.GetValue(stored), property.GetValue(options));
        }
    }
}
