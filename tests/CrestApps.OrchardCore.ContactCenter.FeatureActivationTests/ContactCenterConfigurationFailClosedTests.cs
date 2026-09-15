using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Verifies that a tenant carrying an invalid deployment configuration refuses to activate, rather than
/// activating and discovering the problem when the first request happens to read the option.
/// </summary>
/// <remarks>
/// A unit test can prove a validator returns a failure. It cannot prove the failure is ever raised, because
/// <c>ValidateOnStart</c> registers its rules against <c>IStartupValidator</c>, which the generic host invokes
/// only against the root container. Orchard Core builds a container per tenant, so the declaration alone is
/// inert. These tests boot the real host and create real tenants to prove the rule actually fires.
/// </remarks>
public sealed class ContactCenterConfigurationFailClosedTests
{
    private static readonly string[] _baseFeatures =
    [
        "CrestApps.OrchardCore.ContactCenter",
    ];

    private static ContactCenterTenantProfile CreateProfile(params string[] additionalFeatures)
        => new()
        {
            Id = "fail-closed",
            ProviderProfile = "asterisk-ga-core",
            Features = additionalFeatures.Length == 0
                ? _baseFeatures
                : [.. _baseFeatures, .. additionalFeatures],
        };

    [Fact]
    public async Task Tenant_WhenARetentionWindowIsNegative_RefusesToActivate()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync(
            shellConfiguration: new Dictionary<string, string>
            {
                ["CrestApps:ContactCenter:Retention:InteractionEventRetentionDays"] = "-5",
            });

        // Act
        var tenant = await host.CreateTenantAsync(CreateProfile());

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => host.ActivateTenantAsync(tenant));

        // Assert
        var validationFailure = Unwrap(exception);

        Assert.Contains(
            validationFailure.Failures,
            failure => failure.Contains("CrestApps:ContactCenter:Retention:InteractionEventRetentionDays", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tenant_WhenAHealthCheckThresholdIsBelowOne_RefusesToActivate()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync(
            shellConfiguration: new Dictionary<string, string>
            {
                ["CrestApps:ContactCenter:HealthChecks:ConsecutiveFailuresBeforeUnready"] = "0",
            });

        // The health-check options are bound and validated only when OrchardCore.HealthChecks is enabled, so the
        // fail-closed rule for a below-one threshold can only fire on a tenant that opted into health checks.
        var tenant = await host.CreateTenantAsync(CreateProfile("OrchardCore.HealthChecks"));

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => host.ActivateTenantAsync(tenant));

        Assert.Contains(
            Unwrap(exception).Failures,
            failure => failure.Contains("CrestApps:ContactCenter:HealthChecks:ConsecutiveFailuresBeforeUnready", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tenant_WhenTheTopologyProfileIsNotRecognized_RefusesToActivate()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync(
            shellConfiguration: new Dictionary<string, string>
            {
                ["CrestApps:ContactCenter:Topology:ProfileId"] = "single-node-distributedd",
            });

        var tenant = await host.CreateTenantAsync(CreateProfile());

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => host.ActivateTenantAsync(tenant));

        Assert.Contains(
            Unwrap(exception).Failures,
            failure => failure.Contains("not recognized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Tenant_WhenTheConfigurationIsValid_Activates()
    {
        // The control for the three refusals above. Without it they would also pass if tenant activation were
        // broken for an unrelated reason.
        await using var host = await ContactCenterFeatureActivationHost.StartAsync(
            shellConfiguration: new Dictionary<string, string>
            {
                ["CrestApps:ContactCenter:Retention:InteractionEventRetentionDays"] = "30",
                ["CrestApps:ContactCenter:HealthChecks:ConsecutiveFailuresBeforeUnready"] = "3",
                ["CrestApps:ContactCenter:Topology:ProfileId"] = "single-node-distributed",
            });

        var tenant = await host.CreateTenantAsync(CreateProfile());

        await host.ActivateTenantAsync(tenant);

        Assert.NotNull(tenant);
    }

    [Fact]
    public async Task Tenant_InProduction_RefusesToActivateWithACheckedInAsteriskCredential()
    {
        // Arrange
        var profile = await LoadAsteriskProfileAsync();
        await using var host = await ContactCenterFeatureActivationHost.StartAsync(
            Environments.Production,
            CreateAsteriskConfiguration());

        var tenant = await host.CreateTenantAsync(profile);

        // Act
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => host.ActivateTenantAsync(tenant));

        // Assert
        Assert.Contains(
            Unwrap(exception).Failures,
            failure => failure.Contains("Password", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tenant_InDevelopment_ActivatesWithTheSameAsteriskCredential()
    {
        // The same configuration that is refused above must keep working for the Aspire development stack the
        // credential was published for. This is what makes the rule a production guard rather than a ban.
        var profile = await LoadAsteriskProfileAsync();
        await using var host = await ContactCenterFeatureActivationHost.StartAsync(
            Environments.Development,
            CreateAsteriskConfiguration());

        var tenant = await host.CreateTenantAsync(profile);

        await host.ActivateTenantAsync(tenant);

        Assert.NotNull(tenant);
    }

    [Fact]
    public async Task Tenant_InProduction_RefusesToActivateWithTheCheckedInCoturnSecret()
    {
        // The plan names this value explicitly: a deployment that reaches production still carrying the Coturn
        // development secret can have its TURN relay credentials forged by anyone who has read this repository.
        var profile = await LoadAsteriskProfileAsync();
        var configuration = CreateAsteriskConfiguration();
        configuration["CrestApps:Asterisk:Default:Password"] = "an-operator-supplied-secret";
        configuration["CrestApps:Asterisk:Default:TurnSharedSecret"] = ReadDevelopmentTurnSecret();

        await using var host = await ContactCenterFeatureActivationHost.StartAsync(
            Environments.Production,
            configuration);

        var tenant = await host.CreateTenantAsync(profile);

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => host.ActivateTenantAsync(tenant));

        Assert.Contains(
            Unwrap(exception).Failures,
            failure => failure.Contains("TurnSharedSecret", StringComparison.Ordinal));
    }

    /// <summary>
    /// Reads the Coturn static authentication secret from the checked-in development configuration.
    /// </summary>
    /// <returns>The development TURN shared secret.</returns>
    private static string ReadDevelopmentTurnSecret()
        => ReadAssignment(
            Path.Combine("src", "Startup", "CrestApps.Aspire.AppHost", "Coturn", "turnserver.conf"),
            "static-auth-secret");

    private static async Task<ContactCenterTenantProfile> LoadAsteriskProfileAsync()
    {
        var matrix = await ContactCenterSupportMatrix.LoadAsync();

        return matrix.TenantProfiles.Single(profile => profile.Id == "ga-core-asterisk");
    }

    private static Dictionary<string, string> CreateAsteriskConfiguration()
        => new(StringComparer.Ordinal)
        {
            ["CrestApps:Asterisk:Default:BaseUrl"] = "http://127.0.0.1:8088/",
            ["CrestApps:Asterisk:Default:ApplicationName"] = "contact-center",
            ["CrestApps:Asterisk:Default:UserName"] = "contact-center-operator",
            ["CrestApps:Asterisk:Default:Password"] = ReadDevelopmentAriPassword(),
        };

    /// <summary>
    /// Reads the Asterisk ARI password from the checked-in development configuration, so the test proves the
    /// guard rejects the value the repository actually publishes rather than a copy that can drift from it.
    /// </summary>
    /// <returns>The development ARI password.</returns>
    private static string ReadDevelopmentAriPassword()
        => ReadAssignment(
            Path.Combine("src", "Startup", "CrestApps.Aspire.AppHost", "Asterisk", "ari.conf"),
            "password");

    /// <summary>
    /// Reads a <c>name = value</c> assignment out of a checked-in development configuration file, so a test
    /// proves the guard rejects the value this repository actually publishes rather than a copy of it that can
    /// silently drift out of date.
    /// </summary>
    /// <param name="relativePath">The path to the configuration file, relative to the repository root.</param>
    /// <param name="settingName">The name on the left of the assignment.</param>
    /// <returns>The assigned value.</returns>
    private static string ReadAssignment(string relativePath, string settingName)
    {
        var path = Path.Combine(FindRepositoryRoot(), relativePath);

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            var separator = trimmed.IndexOf('=');

            if (separator > 0
                && string.Equals(trimmed.Substring(0, separator).Trim(), settingName, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(separator + 1).Trim();
            }
        }

        Assert.Fail($"No '{settingName}' assignment was found in '{path}', so this test cannot prove anything.");

        return null;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }

    /// <summary>
    /// Finds the options validation failure inside whatever the tenant activation pipeline wrapped it in.
    /// </summary>
    /// <param name="exception">The exception tenant activation surfaced.</param>
    /// <returns>The options validation failure.</returns>
    private static OptionsValidationException Unwrap(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is OptionsValidationException validationException)
            {
                return validationException;
            }
        }

        Assert.Fail(
            "Tenant activation failed, but not with an options validation failure, so this test did not prove " +
            $"the configuration guard fired: {exception}");

        return null;
    }
}
