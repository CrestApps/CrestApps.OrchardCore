using CrestApps.OrchardCore.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage;
using OrchardCore.FileStorage.AzureBlob;
using OrchardCore.FileStorage.FileSystem;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Core.FileStorage;

/// <summary>
/// The five <c>.Azure</c> features register their store with <c>AddTenantFileStore</c>: the store moves to Azure Blob
/// Storage only when its configuration section is complete, and otherwise keeps the parent feature's local store.
/// </summary>
public sealed class TenantFileStoreTests
{
    private const string Section = "CrestApps:AI:AzureDocuments";

    [Fact]
    public void TheOptions_AreReadFromTheSectionTheStoreWasRegisteredWith()
    {
        // Arrange
        var configuration = Configuration(new Dictionary<string, string>
        {
            [$"{Section}:ConnectionString"] = "UseDevelopmentStorage=true",
            [$"{Section}:ContainerName"] = "AI-DOCUMENTS",
            [$"{Section}:BasePath"] = "TenantA/AIDocuments",
            [$"{Section}:CreateContainer"] = "false",
            [$"{Section}:RemoveContainer"] = "true",
            [$"{Section}:RemoveFilesFromBasePath"] = "true",
        });

        var options = new TenantStoreBlobStorageOptions<IStoreUnderTest>();
        var optionsConfiguration = new TenantBlobStorageOptionsConfiguration<IStoreUnderTest>(
            new TenantFileStoreRegistration<IStoreUnderTest>("AIDocuments", Section, removeOnTenantRemoval: true),
            new Fluid.FluidParser(),
            configuration,
            new ShellSettings { Name = "TenantA" },
            NullLogger<TenantBlobStorageOptionsConfiguration<IStoreUnderTest>>.Instance);

        // Act
        optionsConfiguration.Configure(options);

        // Assert
        Assert.Equal("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.Equal("ai-documents", options.ContainerName);
        Assert.Equal("TenantA/AIDocuments", options.BasePath);
        Assert.False(options.CreateContainer);
        Assert.True(options.RemoveContainer);
        Assert.True(options.RemoveFilesFromBasePath);
    }

    [Fact]
    public void WithoutAConfiguredContainer_TheParentFeaturesStoreStays()
    {
        // Arrange
        var local = new StoreUnderTest(Mock.Of<IFileStore>());
        var services = Services(Configuration([]));
        services.AddSingleton<IStoreUnderTest>(local);

        // Act
        services.AddTenantFileStore<IStoreUnderTest>("AIDocuments", Section, (_, fileStore) => new StoreUnderTest(fileStore), removeOnTenantRemoval: true);
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.Same(local, provider.GetRequiredService<IStoreUnderTest>());
    }

    [Fact]
    public void WithAConfiguredContainer_TheStoreKeepsItsFilesInAzureBlobStorage()
    {
        // Arrange
        var services = Services(Configuration(new Dictionary<string, string>
        {
            [$"{Section}:ConnectionString"] = "UseDevelopmentStorage=true",
            [$"{Section}:ContainerName"] = "documents",
        }));
        services.AddSingleton<IStoreUnderTest>(new StoreUnderTest(Mock.Of<IFileStore>()));

        // Act
        services.AddTenantFileStore<IStoreUnderTest>("AIDocuments", Section, (_, fileStore) => new StoreUnderTest(fileStore), removeOnTenantRemoval: true);
        using var provider = services.BuildServiceProvider();

        // Assert
        var store = Assert.IsType<StoreUnderTest>(provider.GetRequiredService<IStoreUnderTest>());
        Assert.IsType<BlobFileStore>(store.FileStore);
        Assert.Contains(provider.GetServices<IModularTenantEvents>(), events => events is TenantBlobContainerEvents<IStoreUnderTest>);
    }

    [Fact]
    public void WithoutAParentStore_TheStoreKeepsItsFilesInTheTenantsApplicationData()
    {
        // Arrange
        var services = Services(Configuration([]));

        // Act
        services.AddTenantFileStore<IStoreUnderTest>("Temp", configurationSection: null, (_, fileStore) => new StoreUnderTest(fileStore));
        using var provider = services.BuildServiceProvider();

        // Assert
        var store = Assert.IsType<StoreUnderTest>(provider.GetRequiredService<IStoreUnderTest>());
        Assert.IsType<FileSystemStore>(store.FileStore);
        Assert.Empty(provider.GetServices<IModularTenantEvents>());
    }

    private static ServiceCollection Services(IShellConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton(new ShellSettings { Name = "TenantA" });
        services.AddSingleton(Options.Create(new ShellOptions { ShellsApplicationDataPath = Path.GetTempPath(), ShellsContainerName = "Sites" }));
        services.AddSingleton(new Fluid.FluidParser());
        services.AddSingleton(Mock.Of<IClock>());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddLocalization();
        services.AddOptions();

        return services;
    }

    private static IShellConfiguration Configuration(Dictionary<string, string> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var shellConfiguration = new Mock<IShellConfiguration>();
        shellConfiguration.Setup(value => value.GetSection(It.IsAny<string>())).Returns((string key) => configuration.GetSection(key));

        return shellConfiguration.Object;
    }

    public interface IStoreUnderTest
    {
    }

    private sealed class StoreUnderTest : IStoreUnderTest
    {
        public StoreUnderTest(IFileStore fileStore)
            => FileStore = fileStore;

        public IFileStore FileStore { get; }
    }
}
