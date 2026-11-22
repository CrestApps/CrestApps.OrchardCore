using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.FileStorage.ProtectedFiles;
using CrestApps.OrchardCore.ProtectedFiles.Azure;
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
/// Protected file areas stay in the tenant's App_Data until the Protected Files - Azure Blob Storage feature moves
/// every area to one container, each area in a folder of its own.
/// </summary>
public sealed class ProtectedFileAreaStoreTests
{
    private const string Section = "CrestApps:ProtectedFiles:AzureBlobStorage";

    [Fact]
    public void WithoutTheAzureFeature_EveryAreaStaysInTheTenantsApplicationData()
    {
        // Arrange
        var services = Services(Configuration([]));
        services.AddProtectedFileArea<AreaA>("LearningPackages");

        // Act
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Assert
        Assert.Same(ProtectedFileAreaStore.Local, provider.GetRequiredService<ProtectedFileAreaStore>());
        Assert.IsType<ProtectedFileStore<AreaA>>(scope.ServiceProvider.GetRequiredService<IProtectedFileStore<AreaA>>());
        Assert.Empty(provider.GetServices<IModularTenantEvents>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithAConfiguredContainer_EveryAreaMovesToAzureBlobStorage_WhicheverFeatureStartsFirst(bool azureFirst)
    {
        // Arrange
        var services = Services(Configuration(new Dictionary<string, string>
        {
            [$"{Section}:ConnectionString"] = "UseDevelopmentStorage=true",
            [$"{Section}:ContainerName"] = "protected",
        }));

        // Act
        if (azureFirst)
        {
            new Startup().ConfigureServices(services);
        }

        services.AddProtectedFileArea<AreaA>("LearningPackages");
        services.AddProtectedFileArea<AreaB>("LearningCoursework");

        if (!azureFirst)
        {
            new Startup().ConfigureServices(services);
        }

        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.IsType<BlobFileStore>(provider.GetRequiredService<ProtectedFileAreaStore>().SharedStore);
        Assert.Equal("ProtectedFiles", provider.GetRequiredService<TenantFileStoreRegistration<ProtectedFileAreaStore>>().Purpose);
        Assert.Contains(provider.GetServices<IModularTenantEvents>(), events => events is TenantBlobContainerEvents<ProtectedFileAreaStore>);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithAnIncompleteSection_TheAreasStayInTheTenantsApplicationData(bool azureFirst)
    {
        // Arrange
        var services = Services(Configuration(new Dictionary<string, string>
        {
            [$"{Section}:ContainerName"] = "protected",
        }));

        if (azureFirst)
        {
            new Startup().ConfigureServices(services);
        }

        services.AddProtectedFileArea<AreaA>("LearningPackages");

        if (!azureFirst)
        {
            new Startup().ConfigureServices(services);
        }

        // Act
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.Null(provider.GetRequiredService<ProtectedFileAreaStore>().SharedStore);
    }

    [Fact]
    public async Task InASharedStore_EachAreaIsKeptInAFolderOfItsOwn()
    {
        // Arrange
        var folder = Path.Combine(Path.GetTempPath(), "pf-shared-" + Guid.NewGuid().ToString("N"));
        var shared = new FileSystemStore(folder, NullLogger<FileSystemStore>.Instance);
        var packages = new ProtectedFileStore<AreaA>(shared, "LearningPackages", new FileCreationService([]));
        var coursework = new ProtectedFileStore<AreaB>(shared, "LearningCoursework", new FileCreationService([]));

        try
        {
            // Act
            var file = await packages.SaveAsync("course.zip", new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(File.Exists(Path.Combine(folder, "LearningPackages", file.FileId, "course.zip")));
            Assert.NotNull(await packages.FindAsync(file.FileId, TestContext.Current.CancellationToken));
            Assert.Null(await coursework.FindAsync(file.FileId, TestContext.Current.CancellationToken));

            await using (var stream = await packages.OpenAsync(file.FileId, TestContext.Current.CancellationToken))
            {
                Assert.Equal(3, stream.Length);
            }

            Assert.True(await packages.DeleteAsync(file.FileId, TestContext.Current.CancellationToken));
            Assert.False(Directory.Exists(Path.Combine(folder, "LearningPackages", file.FileId)));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
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

    private sealed class AreaA
    {
    }

    private sealed class AreaB
    {
    }
}
