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
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Proves that every configurable Contact Center capability activates its required administration surface.
/// </summary>
/// <remarks>
/// The oracle is structural rather than a list of feature identifiers. Administration surface is discovered by
/// reflection - every navigation provider, every display driver, and every routed administration controller in the
/// Contact Center, Telephony, provider, and Omnichannel assemblies.
/// </remarks>
public sealed class ContactCenterAdministrationSurfaceTests
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

    [Theory]
    [InlineData(
        ContactCenterConstants.Feature.Area,
        "CrestApps.OrchardCore.ContactCenter.Services.ContactCenterSettingsAdminMenu")]
    [InlineData(
        ContactCenterConstants.Feature.Agents,
        "CrestApps.OrchardCore.ContactCenter.Drivers.AgentStateReasonCodeDisplayDriver")]
    [InlineData(
        ContactCenterConstants.Feature.Queues,
        "CrestApps.OrchardCore.ContactCenter.Controllers.QueuesController")]
    [InlineData(
        ContactCenterConstants.Feature.Dialer,
        "CrestApps.OrchardCore.ContactCenter.Controllers.DialerProfilesController")]
    [InlineData(
        ContactCenterConstants.Feature.DialerPaced,
        "CrestApps.OrchardCore.ContactCenter.Controllers.DialerProfilesController")]
    [InlineData(
        ContactCenterConstants.Feature.Recording,
        "CrestApps.OrchardCore.ContactCenter.Drivers.ContactCenterRecordingSettingsDisplayDriver")]
    [InlineData(
        ContactCenterConstants.Feature.InboundVoice,
        "CrestApps.OrchardCore.ContactCenter.Controllers.EntryPointsController")]
    public async Task ConfigurableCapability_RegistersItsAdministrationSurface(
        string featureId,
        string expectedSurfaceType)
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var surface = await GetSurfaceAsync(host, featureId, [featureId]);

        Assert.Contains(expectedSurfaceType, surface);
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
