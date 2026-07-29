using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Requires the registered event upcasters to cover every schema version step the running release has to
/// cross. The point of failure this guards is not today but the first schema change after release: bumping
/// the current version without writing the conversion leaves every event already on disk unreadable, and
/// nothing about that bump looks wrong in review.
/// </summary>
public sealed class InteractionEventUpcastCoverageTests
{
    [Fact]
    public void RegisteredUpcasters_CoverEveryVersionStepThisReleaseHasToCross()
    {
        // Arrange
        var registered = RegisteredUpcasterTypes();
        var declared = Declare(registered);

        // Act
        var gaps = MissingSteps(declared, ContactCenterConstants.CurrentEventSchemaVersion);

        // Assert
        Assert.True(
            gaps.Count == 0,
            $"Contact Center events are written at schema version {ContactCenterConstants.CurrentEventSchemaVersion}, but no registered upcaster converts from version(s) {string.Join(", ", gaps)}. Every event already stored below the current version has to be able to reach it.");
    }

    [Fact]
    public void TheCoverageRule_FailsWhenTheSchemaVersionIsBumpedWithoutAnUpcaster()
    {
        // Arrange
        // The rule is applied to the version this release would have if somebody bumped the constant and
        // shipped nothing else, which is the exact mistake the gate exists to stop. Without this the gate
        // above passes for as long as the constant never moves, and proves nothing about the day it does.
        var declared = Declare(RegisteredUpcasterTypes());

        // Act
        var gaps = MissingSteps(declared, ContactCenterConstants.CurrentEventSchemaVersion + 1);

        // Assert
        Assert.Contains(ContactCenterConstants.CurrentEventSchemaVersion, gaps);
    }

    [Fact]
    public void EveryUpcasterInTheAssembly_IsRegisteredWithTheContainer()
    {
        // Arrange
        // An upcaster that was written but never registered is the same as one that was never written, and it
        // is worse in review because the file exists.
        var registered = RegisteredUpcasterTypes();

        var implemented = typeof(IInteractionEventUpcaster).Assembly
            .GetTypes()
            .Where(type => type.IsClass
                && !type.IsAbstract
                && typeof(IInteractionEventUpcaster).IsAssignableFrom(type));

        // Act & Assert
        foreach (var type in implemented)
        {
            Assert.True(
                registered.Contains(type),
                $"'{type.FullName}' implements IInteractionEventUpcaster but is not registered, so it would never convert anything.");
        }
    }

    [Fact]
    public void TheUpcastService_IsRegisteredSoEveryEventReadPassesThroughIt()
    {
        // Arrange
        var services = ConfiguredServices();

        // Act
        var descriptor = services.FirstOrDefault(service => service.ServiceType == typeof(IInteractionEventUpcastService));

        // Assert
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(DefaultInteractionEventUpcastService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    private static List<int> MissingSteps(IReadOnlyCollection<int> declaredFromVersions, int currentVersion)
    {
        var gaps = new List<int>();

        for (var version = 1; version < currentVersion; version++)
        {
            if (!declaredFromVersions.Contains(version))
            {
                gaps.Add(version);
            }
        }

        return gaps;
    }

    private static HashSet<int> Declare(IReadOnlyCollection<Type> upcasterTypes)
    {
        var versions = new HashSet<int>();

        foreach (var type in upcasterTypes)
        {
            var upcaster = (IInteractionEventUpcaster)Activator.CreateInstance(type);

            versions.Add(upcaster.FromVersion);
        }

        return versions;
    }

    private static Type[] RegisteredUpcasterTypes()
    {
        return ConfiguredServices()
            .Where(service => service.ServiceType == typeof(IInteractionEventUpcaster) && service.ImplementationType is not null)
            .Select(service => service.ImplementationType)
            .ToArray();
    }

    private static ServiceCollection ConfiguredServices()
    {
        var services = new ServiceCollection();

        new Startup(new EmptyShellConfiguration()).ConfigureServices(services);

        return services;
    }

    private sealed class EmptyShellConfiguration : IShellConfiguration
    {
        private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

        public string this[string key]
        {
            get => _configuration[key];
            set => _configuration[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return _configuration.GetChildren();
        }

        public IChangeToken GetReloadToken()
        {
            return _configuration.GetReloadToken();
        }

        public IConfigurationSection GetSection(string key)
        {
            return _configuration.GetSection(key);
        }
    }
}
