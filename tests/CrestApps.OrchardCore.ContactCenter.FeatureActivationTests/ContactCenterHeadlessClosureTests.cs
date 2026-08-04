using System.Reflection;
using System.Text;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.DialPad;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Proves that a Contact Center deployment can run headlessly with no capability-specific administration screens.
/// </summary>
/// <remarks>
/// The capability features used to register their own screens, so enabling <c>Queues</c> transitively activated the
/// whole Omnichannel administration experience. An API-only or embedded deployment therefore had to carry, secure and
/// upgrade a user interface it never served. That is invisible from the manifests alone, because the drag came from a
/// service registration rather than a declared dependency.
/// <para>
/// The oracle is structural rather than a list of feature identifiers. Administration surface is discovered by
/// reflection - every navigation provider, every display driver, and every routed administration controller in the
/// Contact Center, Telephony, provider, and Omnichannel assemblies. The base Telephony provider-configuration screen
/// is always available by design; capability-specific administration surface must remain absent.
/// </para>
/// <para>
/// Controllers are part of that sweep because moving a driver without moving the controller that consumes it is worse
/// than leaving both behind: the route stays registered and authorized while the display pipeline that binds and
/// validates its form no longer resolves, so the write path silently persists whatever the empty editor produced.
/// </para>
/// </remarks>
public sealed class ContactCenterHeadlessClosureTests
{
    private static readonly string[] _alwaysAvailableConfigurationSurface =
    [
        "CrestApps.OrchardCore.Telephony.Drivers.TelephonySettingsDisplayDriver",
        "CrestApps.OrchardCore.Telephony.Services.TelephonyAdminMenu",
    ];

    private static readonly string[] _surfaceAssemblyPrefixes =
    [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.Asterisk",
        "CrestApps.OrchardCore.DialPad",
        "CrestApps.OrchardCore.Omnichannel",
    ];

    /// <summary>
    /// The features that exist to serve a user interface. Splitting these would leave nothing behind, so they are
    /// deliberately excluded from the headless closure rather than split. <see cref="EveryUserExperienceFeature_ActuallyServesAUserInterface"/>
    /// keeps this list honest: a feature may only appear here if it really does register administration surface.
    /// </summary>
    private static readonly string[] _userExperienceFeatures =
    [
        ContactCenterConstants.Feature.Admin,
        ContactCenterConstants.Feature.AgentDesktop,
        ContactCenterConstants.Feature.VoiceSoftPhone,
        ContactCenterConstants.Feature.Supervision,
        ContactCenterConstants.Feature.Analytics,
    ];

    [Fact]
    public async Task TheFullHeadlessClosure_ActivatesWithoutCapabilityAdministrationSurface()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var headlessFeatures = GetHeadlessFeatureIds();

        Assert.NotEmpty(headlessFeatures);

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "headless-closure",
            ProviderProfile = "none",
            Features = headlessFeatures,
        });

        var result = await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var featureManager = serviceProvider.GetRequiredService<IShellFeaturesManager>();
            var enabledFeatureIds = (await featureManager.GetEnabledFeaturesAsync())
                .Select(feature => feature.Id)
                .ToHashSet(StringComparer.Ordinal);

            return new
            {
                EnabledUserExperienceFeatures = _userExperienceFeatures
                    .Where(enabledFeatureIds.Contains)
                    .ToArray(),
                Surface = FindAdministrationSurface(serviceProvider)
                    .Except(_alwaysAvailableConfigurationSurface, StringComparer.Ordinal)
                    .ToArray(),
            };
        });

        Assert.True(
            result.EnabledUserExperienceFeatures.Length == 0,
            Describe(
                "A headless tenant activated features that exist only to serve a user interface.",
                "Remove the user-interface dependency from the capability feature that dragged it in.",
                result.EnabledUserExperienceFeatures));

        Assert.True(
            result.Surface.Length == 0,
            Describe(
                "A headless tenant registered capability-specific administration surface.",
                "Move the registration out of the capability feature and into its '.Admin' feature.",
                result.Surface));
    }

    [Fact]
    public async Task TelephonyCore_RegistersProviderConfigurationSurface()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var surface = await GetSurfaceAsync(
            host,
            "telephony-provider-configuration",
            [TelephonyConstants.Feature.Area]);

        Assert.True(
            _alwaysAvailableConfigurationSurface.All(surface.Contains),
            Describe(
                "The Telephony core feature did not register its provider-configuration surface.",
                "Register the Telephony settings display driver and administration menu from the core Telephony startup.",
                _alwaysAvailableConfigurationSurface.Except(surface, StringComparer.Ordinal)));
    }

    [Theory]
    [InlineData(
        AsteriskConstants.Feature.Area,
        "CrestApps.OrchardCore.Asterisk.Drivers.AsteriskSettingsDisplayDriver")]
    [InlineData(
        DialPadConstants.Feature.Area,
        "CrestApps.OrchardCore.DialPad.Drivers.DialPadSettingsDisplayDriver")]
    public async Task TelephonyProviderFeature_RegistersItsSettingsSurface(
        string featureId,
        string settingsDriverType)
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var surface = await GetSurfaceAsync(host, featureId, [featureId]);

        Assert.Contains(settingsDriverType, surface);
    }

    [Fact]
    public async Task EveryUserExperienceFeature_ActuallyServesAUserInterface()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var withoutSurface = new List<string>();

        foreach (var featureId in _userExperienceFeatures)
        {
            if (!await ServesItsOwnUserInterfaceAsync(host, featureId))
            {
                withoutSurface.Add(featureId);
            }
        }

        Assert.True(
            withoutSurface.Count == 0,
            Describe(
                "Features are excluded from the headless closure but serve no user interface of their own, which " +
                "makes the headless proof weaker than it looks.",
                "Either delete the feature from the exclusion list so the headless closure covers it, or give it " +
                "the administration surface its name promises.",
                withoutSurface));
    }

    [Fact]
    public async Task EnablingAdministrationWithACapability_RestoresThatCapabilitysSurface()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        // The single administration feature folds every capability's screens back in, each gated on its capability
        // being enabled. Enabling administration with a capability must therefore register more Contact Center
        // administration surface than administration alone, and the extra surface is what the capability gave up
        // when it went headless.
        var capabilityFeatures = new[]
        {
            ContactCenterConstants.Feature.Agents,
            ContactCenterConstants.Feature.Queues,
            ContactCenterConstants.Feature.Dialer,
            ContactCenterConstants.Feature.Recording,
            ContactCenterConstants.Feature.EntryPoints,
        };

        var administrationOnlySurface = await GetSurfaceAsync(host, "admin-only", [ContactCenterConstants.Feature.Admin]);

        var withoutRestoredSurface = new List<string>();

        foreach (var capabilityFeature in capabilityFeatures)
        {
            var combinedSurface = await GetSurfaceAsync(
                host,
                "admin-with-" + capabilityFeature,
                [ContactCenterConstants.Feature.Admin, capabilityFeature]);

            var restored = combinedSurface
                .Except(administrationOnlySurface, StringComparer.Ordinal)
                .Any(IsContactCenterSurface);

            if (!restored)
            {
                withoutRestoredSurface.Add(capabilityFeature);
            }
        }

        Assert.True(
            withoutRestoredSurface.Count == 0,
            Describe(
                "Enabling Contact Center Administration alongside a capability registered no additional " +
                "administration surface for that capability, so the capability's screens can never be reached.",
                "Gate the capability's administration registrations on '[Feature(Admin)] [RequireFeatures(capability)]' " +
                "so enabling administration restores them.",
                withoutRestoredSurface));
    }

    [Fact]
    public async Task EnablingACapabilityWithoutAdministration_RegistersNoSurface()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        // The mirror of the fold: a capability enabled without the administration feature must stay headless, even
        // though the administration feature carries the registrations gated on that capability.
        var capabilityFeatures = new[]
        {
            ContactCenterConstants.Feature.Agents,
            ContactCenterConstants.Feature.Queues,
            ContactCenterConstants.Feature.Dialer,
            ContactCenterConstants.Feature.Recording,
            ContactCenterConstants.Feature.EntryPoints,
        };

        var withSurface = new List<string>();

        foreach (var capabilityFeature in capabilityFeatures)
        {
            var surface = await GetSurfaceAsync(host, "headless-" + capabilityFeature, [capabilityFeature]);

            if (surface
                .Except(_alwaysAvailableConfigurationSurface, StringComparer.Ordinal)
                .Any(IsContactCenterSurface))
            {
                withSurface.Add(capabilityFeature);
            }
        }

        Assert.True(
            withSurface.Count == 0,
            Describe(
                "A capability enabled without Contact Center Administration registered administration surface.",
                "Gate the registration on '[Feature(Admin)] [RequireFeatures(capability)]' so it stays headless " +
                "until administration is enabled.",
                withSurface));
    }

    private static async Task<List<string>> GetSurfaceAsync(
        ContactCenterFeatureActivationHost host,
        string tenantId,
        string[] features)
    {
        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = tenantId,
            ProviderProfile = "none",
            Features = features,
        });

        return await host.ExecuteInTenantScopeAsync(
            tenant,
            serviceProvider => Task.FromResult(FindAdministrationSurface(serviceProvider)));
    }

    /// <summary>
    /// Determines whether a feature contributes administration surface of its own rather than inheriting all of it.
    /// </summary>
    /// <remarks>
    /// The subtraction is what makes the question meaningful. Every administration feature depends on the
    /// administration root, and every user experience depends on capabilities, so a feature that registers nothing
    /// still resolves everything its dependencies registered. The baseline is therefore the surface of the feature's
    /// declared dependency closure with the feature itself withheld, and only surface this product's own Contact
    /// Center and Telephony assemblies contribute counts, because Omnichannel screens arrive through a declared
    /// dependency and would otherwise answer the question for a feature that registers nothing at all.
    /// </remarks>
    /// <param name="host">The activation host to create tenants on.</param>
    /// <param name="featureId">The feature to evaluate.</param>
    /// <returns><c>true</c> when the feature adds administration surface of its own; otherwise <c>false</c>.</returns>
    private static async Task<bool> ServesItsOwnUserInterfaceAsync(ContactCenterFeatureActivationHost host, string featureId)
    {
        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = featureId,
            ProviderProfile = "none",
            Features = [featureId],
        });

        var result = await host.ExecuteInTenantScopeAsync(tenant, serviceProvider =>
        {
            var extensions = serviceProvider.GetRequiredService<IExtensionManager>();

            var dependencies = extensions.GetFeatureDependencies(featureId)
                .Select(feature => feature.Id)
                .Where(id => !string.Equals(id, featureId, StringComparison.Ordinal))
                .ToArray();

            return Task.FromResult(new
            {
                Dependencies = dependencies,
                Surface = FindAdministrationSurface(serviceProvider),
            });
        });

        var inherited = new HashSet<string>(StringComparer.Ordinal);

        if (result.Dependencies.Length > 0)
        {
            var baselineTenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
            {
                Id = featureId + "-baseline",
                ProviderProfile = "none",
                Features = result.Dependencies,
            });

            var baseline = await host.ExecuteInTenantScopeAsync(
                baselineTenant,
                serviceProvider => Task.FromResult(FindAdministrationSurface(serviceProvider)));

            inherited.UnionWith(baseline);
        }

        return result.Surface.Except(inherited).Any(IsContactCenterSurface);
    }

    /// <summary>
    /// Resolves every navigation provider, display driver, and routed administration controller the tenant can
    /// produce and reports the ones that belong to this product.
    /// </summary>
    /// <param name="serviceProvider">The tenant service provider to resolve against.</param>
    /// <returns>The name of each administration surface type the tenant registered.</returns>
    private static List<string> FindAdministrationSurface(IServiceProvider serviceProvider)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var controller in FindAdministrationControllers(serviceProvider))
        {
            found.Add(controller);
        }

        foreach (var navigationProvider in serviceProvider.GetServices<INavigationProvider>())
        {
            if (IsProductSurface(navigationProvider.GetType()))
            {
                found.Add(navigationProvider.GetType().FullName);
            }
        }

        foreach (var contract in GetDisplayDriverContracts())
        {
            IEnumerable<object> drivers;

            try
            {
                drivers = (IEnumerable<object>)serviceProvider.GetServices(contract);
            }
            catch (Exception)
            {
                // A contract that cannot be constructed is a different defect, owned by the dependency audit.
                continue;
            }

            foreach (var driver in drivers.Where(driver => IsProductSurface(driver.GetType())))
            {
                found.Add(driver.GetType().FullName);
            }
        }

        return [.. found];
    }

    /// <summary>
    /// Reports every administration controller this product routes in the tenant.
    /// </summary>
    /// <remarks>
    /// Orchard builds the action-descriptor collection per tenant from its enabled features, so this is the routed
    /// surface rather than the compiled one: a controller whose feature is disabled is absent from the collection.
    /// </remarks>
    /// <param name="serviceProvider">The tenant service provider to resolve against.</param>
    /// <returns>The name of each routed administration controller type.</returns>
    private static IEnumerable<string> FindAdministrationControllers(IServiceProvider serviceProvider)
    {
        var actions = serviceProvider.GetService<IActionDescriptorCollectionProvider>();

        if (actions is null)
        {
            return [];
        }

        return actions.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(descriptor => descriptor.ControllerTypeInfo.AsType())
            .Where(type => IsProductSurface(type) && type.GetCustomAttribute<AdminAttribute>(inherit: true) is not null)
            .Select(type => type.FullName)
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>
    /// Discovers every closed <see cref="IDisplayDriver{TModel}"/> contract this product implements a driver for.
    /// </summary>
    /// <returns>The display driver contracts to probe.</returns>
    private static Type[] GetDisplayDriverContracts()
        => [.. GetProductTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true })
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDisplayDriver<>))
            .Distinct()];

    /// <summary>
    /// Gets every Contact Center feature that is not excluded as a user experience.
    /// </summary>
    /// <returns>The feature identifiers that make up the headless closure.</returns>
    private static string[] GetHeadlessFeatureIds()
    {
        var excluded = _userExperienceFeatures.ToHashSet(StringComparer.Ordinal);

        return [.. typeof(ContactCenterConstants.Feature)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue())
            .Where(featureId => !string.IsNullOrEmpty(featureId) && !excluded.Contains(featureId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    private static bool IsContactCenterSurface(string typeName)
        => typeName.StartsWith("CrestApps.OrchardCore.ContactCenter", StringComparison.Ordinal) ||
            typeName.StartsWith("CrestApps.OrchardCore.Telephony", StringComparison.Ordinal);

    private static bool IsProductSurface(Type type)
    {
        var assemblyName = type.Assembly.GetName().Name;

        return assemblyName is not null &&
            _surfaceAssemblyPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static IEnumerable<Type> GetProductTypes()
        => AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => IsProductAssembly(assembly.GetName().Name))
            .SelectMany(GetLoadableTypes);

    private static bool IsProductAssembly(string assemblyName)
        => assemblyName is not null &&
            _surfaceAssemblyPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.Ordinal));

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null);
        }
    }

    private static string Describe(string summary, string remedy, IEnumerable<string> violations)
    {
        var message = new StringBuilder(summary).AppendLine().AppendLine();

        foreach (var violation in violations)
        {
            message.Append("  - ").AppendLine(violation);
        }

        return message.AppendLine().AppendLine(remedy).ToString();
    }
}
