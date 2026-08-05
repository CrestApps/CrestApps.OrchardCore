namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

public sealed class ContactCenterFeatureActivationTests
{
    private static readonly string[] _runtimeProfileIds =
    [
        "ga-core-asterisk",
        "ga-core-dialpad",
    ];

    [Fact]
    public async Task SupportMatrix_TenantProfiles_AllHaveRuntimeScenarios()
    {
        // Arrange
        var matrix = await ContactCenterSupportMatrix.LoadAsync();

        // Act
        var profileIds = matrix.TenantProfiles
            .Select(profile => profile.Id)
            .Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(_runtimeProfileIds.Order(StringComparer.Ordinal), profileIds);
    }

    [Fact]
    public async Task SupportMatrix_GaCoreProfiles_DoNotEnableAutomatedDialing()
    {
        // Arrange
        var matrix = await ContactCenterSupportMatrix.LoadAsync();

        // Act
        var automatedProfiles = matrix.TenantProfiles
            .Where(profile => profile.Features.Contains(
                "CrestApps.OrchardCore.ContactCenter.Dialer.Paced",
                StringComparer.Ordinal))
            .Select(profile => profile.Id)
            .ToArray();

        // Assert
        Assert.Empty(automatedProfiles);
    }

    [Fact]
    public async Task SupportMatrix_GaCoreProfiles_EnableEntryPointsForInboundVoice()
    {
        // Arrange
        var matrix = await ContactCenterSupportMatrix.LoadAsync();

        // Act
        var profilesWithoutEntryPoints = matrix.TenantProfiles
            .Where(profile => !profile.Features.Contains(
                ContactCenterConstants.Feature.InboundVoice,
                StringComparer.Ordinal))
            .Select(profile => profile.Id)
            .ToArray();

        // Assert
        Assert.Empty(profilesWithoutEntryPoints);
    }

    [Theory]
    [InlineData("ga-core-asterisk")]
    [InlineData("ga-core-dialpad")]
    public async Task FreshTenant_ProfileActivation_CompletesWithExpectedServices(string profileId)
    {
        // Arrange
        var matrix = await ContactCenterSupportMatrix.LoadAsync();
        var profile = matrix.TenantProfiles.Single(profile => profile.Id == profileId);
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        // Act
        var tenant = await host.CreateTenantAsync(profile);

        // Assert
        await host.AssertTenantAsync(tenant);
    }

    [Fact]
    public async Task FreshTenant_VoiceDependentFeature_ActivatesProviderCommandServices()
    {
        // Arrange
        // Voice is dependency-only and is reached through a Voice-dependent feature such as Entry Points, which is
        // the canonical inbound-voice consumer every voice deployment enables.
        var profile = new ContactCenterTenantProfile
        {
            Id = "voice-via-entry-points",
            ProviderProfile = "none",
            Features =
            [
                "CrestApps.OrchardCore.ContactCenter.InboundVoice",
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        // Act
        var tenant = await host.CreateTenantAsync(profile);

        // Assert
        await host.AssertVoiceFeatureAsync(tenant);
    }

    [Fact]
    public async Task FreshTenant_RecordingFeatureAlone_ActivatesRecordingServices()
    {
        // Arrange
        var profile = new ContactCenterTenantProfile
        {
            Id = "recording-only",
            ProviderProfile = "none",
            Features =
            [
                "CrestApps.OrchardCore.ContactCenter.Recording",
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        // Act
        var tenant = await host.CreateTenantAsync(profile);

        // Assert
        await host.AssertRecordingFeatureAsync(tenant);
    }

    [Fact]
    public async Task FreshTenant_WorkflowsFeatureAlone_ActivatesReplaySafeWorkflowBridge()
    {
        // Arrange
        var profile = new ContactCenterTenantProfile
        {
            Id = "workflows-only",
            ProviderProfile = "none",
            Features =
            [
                "CrestApps.OrchardCore.ContactCenter.Workflows",
            ],
        };
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        // Act
        var tenant = await host.CreateTenantAsync(profile);

        // Assert
        await host.AssertWorkflowsFeatureAsync(tenant);
    }

    [Theory]
    [InlineData("ga-core-asterisk")]
    [InlineData("ga-core-dialpad")]
    public async Task IdleTenant_ProviderDisableAndReenable_RestoresExpectedServices(string profileId)
    {
        // Arrange
        var matrix = await ContactCenterSupportMatrix.LoadAsync();
        var profile = matrix.TenantProfiles.Single(profile => profile.Id == profileId);
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(profile);

        // Act
        await host.DisableAndReenableProviderAsync(tenant);

        // Assert
        await host.AssertTenantAsync(tenant);
    }

    [Fact]
    public async Task TwoTenants_DifferentProviderProfiles_KeepProviderRegistrationsIsolated()
    {
        // Arrange
        var matrix = await ContactCenterSupportMatrix.LoadAsync();
        var asteriskProfile = matrix.TenantProfiles.Single(profile => profile.Id == "ga-core-asterisk");
        var dialPadProfile = matrix.TenantProfiles.Single(profile => profile.Id == "ga-core-dialpad");
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        // Act
        var asteriskTenant = await host.CreateTenantAsync(asteriskProfile);
        var dialPadTenant = await host.CreateTenantAsync(dialPadProfile);

        // Assert
        await host.AssertTenantAsync(asteriskTenant);
        await host.AssertTenantAsync(dialPadTenant);
    }
}
