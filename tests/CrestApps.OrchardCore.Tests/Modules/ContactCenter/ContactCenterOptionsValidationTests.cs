using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Configuration;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Tests.Framework.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves that every operator-supplied Contact Center, Telephony and provider option is rejected when it is
/// invalid, and that the rejection happens rather than being merely declared.
/// </summary>
public sealed class ContactCenterOptionsValidationTests
{
    /// <summary>
    /// The assemblies whose <c>*Options</c> types this gate governs.
    /// </summary>
    private static readonly Type[] _governedAssemblyMarkers =
    [
        typeof(ContactCenterConstants),
        typeof(ContactCenterRetentionOptions),
        typeof(ContactCenterHub),
        typeof(TelephonyCommandOptions),
        typeof(TelephonyHub),
        typeof(DefaultAsteriskOptions),
    ];

    /// <summary>
    /// Options types that carry no operator-supplied value, with the reason each is exempt. An exemption is
    /// recorded here rather than inferred so that adding a genuinely configurable option and forgetting to
    /// validate it is a failure rather than a silent pass.
    /// </summary>
    private static readonly Dictionary<string, string> _optionsWithoutOperatorInput = new(StringComparer.Ordinal)
    {
        ["TelephonyProviderOptions"] =
            "A registry of the providers each feature registered in code. It is never bound from configuration, so there is no operator input to reject.",
        ["TelephonyProviderTypeOptions"] =
            "An entry in the provider registry, populated in code alongside TelephonyProviderOptions.",
        ["ContactCenterProcessLivenessOptions"] =
            "Supplied by the host at pipeline construction, before any tenant exists, and validated by ContactCenterProcessLivenessPathValidator at that point.",
    };

    [Fact]
    public void EveryOperatorConfigurableOptionsType_IsRegisteredWithAValidator()
    {
        // Arrange
        var services = BuildTenantServices();
        var validatedTypes = services
            .Where(descriptor => descriptor.ServiceType.IsGenericType
                && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>))
            .Select(descriptor => descriptor.ServiceType.GetGenericArguments()[0])
            .ToHashSet();

        var discovered = DiscoverOptionsTypes().ToArray();

        // A discovery that silently returned nothing, because an assembly marker was dropped or a naming
        // convention changed, would pass every assertion below while governing nothing.
        Assert.True(
            discovered.Length >= 16,
            $"This gate discovered only {discovered.Length} options types across the governed assemblies, which is " +
            "fewer than are known to exist. Discovery is broken, so the completeness check below proves nothing.");

        Assert.Contains(typeof(ContactCenterRetentionOptions), discovered);
        Assert.Contains(typeof(DefaultAsteriskOptions), discovered);

        // Act
        var unvalidated = discovered
            .Where(type => !validatedTypes.Contains(type))
            .Where(type => !_optionsWithoutOperatorInput.ContainsKey(type.Name))
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.True(
            unvalidated.Length == 0,
            "These options types accept operator input but no validator rejects an invalid value, so a bad " +
            "deployment configuration is discovered by whatever code reads the option first: " +
            string.Join(", ", unvalidated));
    }

    [Fact]
    public void OptionsExemptedFromValidation_AreAllRealTypes()
    {
        // A stale exemption makes the register look considered while quietly excusing nothing, and hides that
        // the type it named was renamed or deleted.
        var discoveredNames = DiscoverOptionsTypes()
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var stale = _optionsWithoutOperatorInput.Keys
            .Where(name => !discoveredNames.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            $"These options types are exempted from validation but no longer exist: {string.Join(", ", stale)}.");
    }

    [Theory]
    [InlineData("CrestApps_ContactCenter:Retention:InteractionEventRetentionDays", "-1")]
    [InlineData("CrestApps_ContactCenter:Retention:ProjectionReplayHorizonDays", "-1")]
    [InlineData("CrestApps_ContactCenter:Retention:LegalHoldMinimumDays", "-1")]
    public void RetentionOptions_WhenAWindowIsNegative_AreRejected(string key, string value)
    {
        var exception = Assert.Throws<OptionsValidationException>(
            () => ResolveContactCenterOptions<ContactCenterRetentionOptions>(key, value));

        Assert.NotEmpty(exception.Failures);
    }

    [Theory]
    [InlineData("CrestApps_ContactCenter:HealthChecks:DeadLetterDegradedThreshold", "0")]
    [InlineData("CrestApps_ContactCenter:HealthChecks:OverdueBacklogDegradedThreshold", "0")]
    [InlineData("CrestApps_ContactCenter:HealthChecks:ConsecutiveFailuresBeforeUnready", "0")]
    [InlineData("CrestApps_ContactCenter:HealthChecks:ConsecutiveSuccessesBeforeReady", "0")]
    public void HealthCheckOptions_WhenAThresholdIsBelowOne_AreRejected(string key, string value)
    {
        Assert.Throws<OptionsValidationException>(
            () => ResolveContactCenterOptions<ContactCenterHealthCheckOptions>(key, value));
    }

    [Fact]
    public void HealthCheckOptions_WhenTheUnhealthyBoundSitsBelowTheDegradedBound_AreRejected()
    {
        // Silently swapping these, which is what Normalize does for a programmatically built instance, would
        // leave an operator believing a threshold they can read back from their own configuration file.
        var exception = Assert.Throws<OptionsValidationException>(() => ResolveContactCenterOptions<ContactCenterHealthCheckOptions>(
            new Dictionary<string, string>
            {
                ["CrestApps_ContactCenter:HealthChecks:DeadLetterDegradedThreshold"] = "10",
                ["CrestApps_ContactCenter:HealthChecks:DeadLetterUnhealthyThreshold"] = "5",
            }));

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("DeadLetterUnhealthyThreshold", StringComparison.Ordinal));
    }

    [Fact]
    public void TopologyOptions_WhenTheDeclaredProfileIsNotRecognized_AreRejected()
    {
        // A typo that fell through to the default would be a silent downgrade out of the production topology.
        var exception = Assert.Throws<OptionsValidationException>(() => ResolveContactCenterOptions<ContactCenterTopologyOptions>(
            "CrestApps_ContactCenter:Topology:ProfileId",
            "single-node-distributedd"));

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("not recognized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TopologyOptions_WhenNoProfileIsDeclared_AreAccepted()
    {
        // An undeclared topology is the default for development and tests. Rejecting it here would make the
        // module unusable outside a production host, which is not what the topology contract says.
        var options = ResolveContactCenterOptions<ContactCenterTopologyOptions>([]);

        Assert.Null(options.ProfileId);
    }

    [Fact]
    public void ValidateOnStart_DoesNotValidateWhenTheContainerIsBuilt()
    {
        // Pins the reason TenantOptionsStartupValidator exists. ValidateOnStart records its rules against
        // IStartupValidator, which only the generic host invokes, and only against the root container. Orchard
        // builds a container per tenant, so without an explicit invocation the rule never fires at start.
        var services = new ServiceCollection();
        services
            .AddOptions<ContactCenterRetentionOptions>()
            .Validate(_ => false, "always fails")
            .ValidateOnStart();

        using var serviceProvider = services.BuildServiceProvider();

        // Building the container is not enough.
        Assert.NotNull(serviceProvider.GetRequiredService<IStartupValidator>());

        // Only an explicit invocation surfaces the failure.
        Assert.Throws<OptionsValidationException>(
            () => serviceProvider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public async Task TenantOptionsStartupValidator_OnActivation_RejectsAnInvalidConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddOptions<ContactCenterRetentionOptions>()
            .Validate(_ => false, "The retention window is invalid.")
            .ValidateOnStart();

        using var serviceProvider = services.BuildServiceProvider();

        var validator = new TenantOptionsStartupValidator(
            serviceProvider,
            new ShellSettings { Name = "Default" },
            NullLogger<TenantOptionsStartupValidator>.Instance);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(validator.ActivatingAsync);

        Assert.Contains("The retention window is invalid.", exception.Failures);
    }

    [Fact]
    public async Task TenantOptionsStartupValidator_OnActivation_AcceptsAValidConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddOptions<ContactCenterRetentionOptions>()
            .Validate(_ => true, "never fails")
            .ValidateOnStart();

        using var serviceProvider = services.BuildServiceProvider();

        var validator = new TenantOptionsStartupValidator(
            serviceProvider,
            new ShellSettings { Name = "Default" },
            NullLogger<TenantOptionsStartupValidator>.Instance);

        // Act
        await validator.ActivatingAsync();
    }

    [Fact]
    public async Task TenantOptionsStartupValidator_WhenNoFeatureRegisteredARule_DoesNothing()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        using var serviceProvider = services.BuildServiceProvider();

        var validator = new TenantOptionsStartupValidator(
            serviceProvider,
            new ShellSettings { Name = "Default" },
            NullLogger<TenantOptionsStartupValidator>.Instance);

        await validator.ActivatingAsync();
    }

    [Fact]
    public void ContactCenterStartup_RegistersTheTenantActivationValidator()
    {
        var services = BuildTenantServices();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IModularTenantEvents)
                && descriptor.ImplementationType == typeof(TenantOptionsStartupValidator));
    }

    /// <summary>
    /// The configuration a correctly deployed host supplies. Startup classes that fail closed on a dangerous
    /// default, such as the shared health endpoint guard, would otherwise abort this gate before it inspected
    /// anything. This is the documented safe deployment, not a suppression.
    /// </summary>
    private static readonly Dictionary<string, string> _baselineConfiguration = new(StringComparer.Ordinal)
    {
        ["OrchardCore_HealthChecks:Url"] = "/health/aggregate",
    };

    private static T ResolveContactCenterOptions<T>(string key, string value)
        where T : class
        => ResolveContactCenterOptions<T>(new Dictionary<string, string> { [key] = value });

    private static T ResolveContactCenterOptions<T>(Dictionary<string, string> settings)
        where T : class
    {
        var services = BuildTenantServices(settings);
        using var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<IOptions<T>>().Value;
    }

    private static ServiceCollection BuildTenantServices(Dictionary<string, string> settings = null)
    {
        var values = new Dictionary<string, string>(_baselineConfiguration, StringComparer.Ordinal);

        foreach (var setting in settings ?? [])
        {
            values[setting.Key] = setting.Value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var shellConfiguration = new TestShellConfiguration(configuration);
        var services = new ServiceCollection();

        foreach (var startupType in DiscoverStartupTypes())
        {
            var startup = (StartupBase)CreateStartup(startupType, shellConfiguration);

            startup.ConfigureServices(services);
        }

        return services;
    }

    private static object CreateStartup(Type startupType, IShellConfiguration shellConfiguration)
    {
        var constructor = startupType.GetConstructors().Single();
        var arguments = constructor
            .GetParameters()
            .Select(parameter => ResolveStartupArgument(parameter.ParameterType, shellConfiguration))
            .ToArray();

        return constructor.Invoke(arguments);
    }

    private static object ResolveStartupArgument(Type parameterType, IShellConfiguration shellConfiguration)
    {
        if (parameterType == typeof(IShellConfiguration))
        {
            return shellConfiguration;
        }

        if (parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(IStringLocalizer<>))
        {
            return CreateLocalizer(parameterType);
        }

        Assert.Fail(
            $"A module startup takes a '{parameterType.Name}' dependency this gate cannot supply, so its options " +
            "registrations were never inspected. Teach ResolveStartupArgument about the new dependency.");

        return null;
    }

    private static object CreateLocalizer(Type localizerType)
    {
        var mockType = typeof(Mock<>).MakeGenericType(localizerType);
        var mock = (Mock)Activator.CreateInstance(mockType);

        return mock.Object;
    }

    private static IEnumerable<Type> DiscoverStartupTypes()
        => _governedAssemblyMarkers
            .Select(marker => marker.Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass
                && !type.IsAbstract
                && typeof(StartupBase).IsAssignableFrom(type)
                && type.GetConstructors().Length == 1)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

    private static IEnumerable<Type> DiscoverOptionsTypes()
        => _governedAssemblyMarkers
            .Select(marker => marker.Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass
                && !type.IsAbstract
                && type.IsPublic
                && type.Name.EndsWith("Options", StringComparison.Ordinal))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
}
