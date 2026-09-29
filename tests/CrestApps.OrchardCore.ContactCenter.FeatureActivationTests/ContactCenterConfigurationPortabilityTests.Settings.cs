using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Asterisk.Deployments;
using CrestApps.OrchardCore.Asterisk.Deployments.Steps;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Deployments;
using CrestApps.OrchardCore.Telnyx.Deployments.Steps;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Settings;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Proves that the Contact Center and phone system settings travel between environments, and that the credentials the
/// provider settings hold never do.
/// </summary>
/// <remarks>
/// Settings without secrets travel through the standard site settings step and come back through the built-in
/// <c>Settings</c> recipe step. The provider settings hold data-protected secrets, which are encrypted with the keys of
/// the tenant that stored them, so they travel through a step of their own that leaves every secret out of the plan and
/// keeps the secret the destination already holds.
/// </remarks>
public sealed partial class ContactCenterConfigurationPortabilityTests
{
    private static readonly string[] _settingsFeatures =
    [
        TelephonyConstants.Feature.Area,
        TelephonyConstants.Feature.SoftPhone,
        ContactCenterConstants.Feature.Area,
        ContactCenterConstants.Feature.Recording,
        ContactCenterConstants.Feature.SecureCapture,
        TelnyxConstants.Feature.Area,
        TelnyxConstants.Feature.Sms,
        "CrestApps.OrchardCore.Asterisk",
        DeploymentFeatureId,
        RecipesFeatureId,
        RecipeSchemasFeatureId,
    ];

    /// <summary>
    /// Describes one settings object that travels between environments.
    /// </summary>
    /// <param name="SettingsType">The settings type, whose name is the site settings property it is stored under.</param>
    /// <param name="CreateStep">Creates the deployment step that exports the settings.</param>
    /// <param name="ProviderStepName">The provider's own recipe step, or <see langword="null"/> for the standard <c>Settings</c> step.</param>
    /// <param name="ProtectedMembers">The data-protected members and their protection purposes, for a provider step.</param>
    private sealed record PortableSettings(
        Type SettingsType,
        Func<DeploymentStep> CreateStep,
        string ProviderStepName = null,
        IReadOnlyDictionary<string, string> ProtectedMembers = null);

    // The telephony settings come first: enabling a provider makes it the default provider when none is set, so the
    // default is seeded before any provider is enabled to keep the two environments comparable.
    private static readonly PortableSettings[] _portableSettings =
    [
        new(typeof(TelephonySettings), static () => new SiteSettingsPropertyDeploymentStep<TelephonySettings>()),
        new(typeof(SoftPhoneWidgetSettings), static () => new SiteSettingsPropertyDeploymentStep<SoftPhoneWidgetSettings>()),
        new(typeof(ContactCenterExternalTransferSettings), static () => new SiteSettingsPropertyDeploymentStep<ContactCenterExternalTransferSettings>()),
        new(typeof(ContactCenterRecordingSettings), static () => new SiteSettingsPropertyDeploymentStep<ContactCenterRecordingSettings>()),
        new(typeof(SecureCaptureSettings), static () => new SiteSettingsPropertyDeploymentStep<SecureCaptureSettings>()),
        new(typeof(TelnyxSettings), static () => new TelnyxSettingsDeploymentStep(), TelnyxDeploymentSteps.Settings, TelnyxDeploymentSteps.VoiceProtectedMembers),
        new(typeof(TelnyxSmsSettings), static () => new TelnyxSmsSettingsDeploymentStep(), TelnyxDeploymentSteps.SmsSettings, TelnyxDeploymentSteps.SmsProtectedMembers),
        new(typeof(AsteriskSettings), static () => new AsteriskSettingsDeploymentStep(), AsteriskDeploymentSteps.Settings, AsteriskDeploymentSteps.ProtectedMembers),
    ];

    private static readonly (Type SettingsType, string PropertyName, JsonNode Value)[] _settingsOverrides =
    [
        // The region is normalized to one Telnyx knows, and the ARI address to its canonical form, on every write path.
        (typeof(TelnyxSettings), nameof(TelnyxSettings.WebRtcRegion), JsonValue.Create(TelnyxSignalingRegions.All[0])),
        (typeof(AsteriskSettings), nameof(AsteriskSettings.BaseUrl), JsonValue.Create("https://pbx.example.test/ari/")),
    ];

    [Fact]
    public async Task Settings_ReplayIntoAnotherTenantWithoutLosingASettingOrCarryingASecret()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var source = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "settings-export-origin",
            ProviderProfile = "none",
            Features = _settingsFeatures,
        });

        var destination = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "settings-export-target",
            ProviderProfile = "none",
            Features = _settingsFeatures,
        });

        await SeedSettingsAsync(host, source);

        // The destination already holds credentials of its own, which a replayed plan must not erase.
        await ImportAsync(host, destination, [.. _portableSettings.Where(settings => settings.ProviderStepName is not null).Select(BuildDestinationSecretsStep)]);

        var plan = await ExportSettingsAsync(host, source);

        Assert.Equal(_portableSettings.Length, plan.Length);

        var exported = string.Join(Environment.NewLine, plan.Select(step => step.ToJsonString()));

        foreach (var settings in _portableSettings.Where(settings => settings.ProviderStepName is not null))
        {
            var step = plan.Single(candidate => candidate["name"].GetValue<string>() == settings.ProviderStepName);
            var carried = step["Settings"].AsObject();

            foreach (var member in settings.ProtectedMembers.Keys)
            {
                Assert.False(carried.ContainsKey(member), $"{settings.ProviderStepName} exported its secret '{member}'.");
                Assert.DoesNotContain(SecretMarker(settings.SettingsType, member), exported, StringComparison.Ordinal);
            }
        }

        await ImportAsync(host, destination, plan);

        var original = await ReadSettingsAsync(host, source);
        var replayed = await ReadSettingsAsync(host, destination);
        var differences = new List<string>();

        foreach (var settings in _portableSettings)
        {
            var name = settings.SettingsType.Name;
            var ignored = settings.ProtectedMembers?.Keys.ToHashSet(StringComparer.Ordinal) ?? [];

            Assert.True(original[name] is JsonObject, $"{name} was not stored on the source tenant.");
            Assert.True(replayed[name] is JsonObject, $"{name} was not stored on the destination tenant.");

            foreach (var property in original[name].AsObject())
            {
                if (ignored.Contains(property.Key))
                {
                    continue;
                }

                var expected = property.Value?.ToJsonString();
                var actual = replayed[name][property.Key]?.ToJsonString();

                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    differences.Add($"{name}.{property.Key}: exported {expected ?? "null"} but replayed {actual ?? "null"}.");
                }
            }
        }

        Assert.True(
            differences.Count == 0,
            Describe(
                "Contact Center and phone system settings did not survive a deployment plan, so promoting a tenant " +
                "between environments silently loses settings.",
                "Export the whole settings object rather than a hand-listed subset of its members.",
                differences));

        await host.ExecuteInTenantScopeAsync(destination, async serviceProvider =>
        {
            var protection = serviceProvider.GetRequiredService<IDataProtectionProvider>();

            foreach (var settings in _portableSettings.Where(settings => settings.ProviderStepName is not null))
            {
                foreach (var (member, purpose) in settings.ProtectedMembers)
                {
                    var stored = replayed[settings.SettingsType.Name][member]?.GetValue<string>();

                    Assert.False(string.IsNullOrEmpty(stored), $"{settings.SettingsType.Name}.{member} was erased by the replayed plan.");
                    Assert.Equal(DestinationSecret(settings.SettingsType, member), protection.CreateProtector(purpose).Unprotect(stored));
                }
            }
        });
    }

    [Fact]
    public async Task ASecretSuppliedInARecipe_IsStoredProtected()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "settings-secret-protection",
            ProviderProfile = "none",
            Features = _settingsFeatures,
        });

        var providerSettings = _portableSettings.Where(settings => settings.ProviderStepName is not null).ToArray();

        await ImportAsync(host, tenant, [.. providerSettings.Select(BuildDestinationSecretsStep)]);

        var stored = await ReadSettingsAsync(host, tenant);

        await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var protection = serviceProvider.GetRequiredService<IDataProtectionProvider>();

            foreach (var settings in providerSettings)
            {
                foreach (var (member, purpose) in settings.ProtectedMembers)
                {
                    var value = stored[settings.SettingsType.Name][member]?.GetValue<string>();
                    var secret = DestinationSecret(settings.SettingsType, member);

                    Assert.NotEqual(secret, value);
                    Assert.Equal(secret, protection.CreateProtector(purpose).Unprotect(value));
                }
            }
        });
    }

    private static JsonObject BuildDestinationSecretsStep(PortableSettings settings)
    {
        var secrets = new JsonObject();

        foreach (var member in settings.ProtectedMembers.Keys)
        {
            secrets[member] = DestinationSecret(settings.SettingsType, member);
        }

        return new JsonObject
        {
            ["name"] = settings.ProviderStepName,
            ["Settings"] = secrets,
        };
    }

    private static string SecretMarker(Type settingsType, string member)
        => $"{settingsType.Name}-{member}";

    private static string DestinationSecret(Type settingsType, string member)
        => $"destination-{settingsType.Name}-{member}";

    private static async Task SeedSettingsAsync(ContactCenterFeatureActivationHost host, ContactCenterTenant tenant)
    {
        var steps = new List<JsonObject>();

        foreach (var settings in _portableSettings)
        {
            // Every member, the secrets included, is given a value that differs from its default, so a member dropped by
            // the export or the import shows up as a difference rather than as a matching default.
            var (entry, _) = BuildFullyPopulated(settings.SettingsType, settings.SettingsType.Name);

            foreach (var (settingsType, propertyName, value) in _settingsOverrides)
            {
                if (settingsType == settings.SettingsType)
                {
                    entry[propertyName] = value.DeepClone();
                }
            }

            steps.Add(settings.ProviderStepName is null
                ? new JsonObject { ["name"] = "Settings", [settings.SettingsType.Name] = entry }
                : new JsonObject { ["name"] = settings.ProviderStepName, ["Settings"] = entry });
        }

        await ImportAsync(host, tenant, [.. steps]);
    }

    private static async Task<JsonObject[]> ExportSettingsAsync(ContactCenterFeatureActivationHost host, ContactCenterTenant tenant)
    {
        return await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var sources = serviceProvider.GetServices<IDeploymentSource>();
            var steps = new List<JsonObject>();

            foreach (var settings in _portableSettings)
            {
                var step = settings.CreateStep();
                var result = new DeploymentPlanResult(new NullFileBuilder(), new RecipeDescriptor());

                foreach (var source in sources)
                {
                    await source.ProcessDeploymentStepAsync(step, result);
                }

                steps.AddRange(result.Steps.Cast<JsonObject>());
            }

            return steps.ToArray();
        });
    }

    private static async Task<JsonObject> ReadSettingsAsync(ContactCenterFeatureActivationHost host, ContactCenterTenant tenant)
    {
        return await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var site = await serviceProvider.GetRequiredService<ISiteService>().LoadSiteSettingsAsync();
            var settings = new JsonObject();

            foreach (var portable in _portableSettings)
            {
                settings[portable.SettingsType.Name] = site.Properties[portable.SettingsType.Name]?.DeepClone();
            }

            return settings;
        });
    }
}
