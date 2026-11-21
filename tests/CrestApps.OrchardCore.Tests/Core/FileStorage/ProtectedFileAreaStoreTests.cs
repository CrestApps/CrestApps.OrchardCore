using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.FileStorage.ProtectedFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
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
        Assert.IsType<LocalProtectedFileAreaStoreFactory>(provider.GetRequiredService<IProtectedFileAreaStoreFactory>());
        Assert.IsType<FileSystemStore>(provider.GetRequiredService<IProtectedFileAreaStoreFactory>().GetStore("LearningPackages"));
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
            services.AddProtectedFilesAzureBlobStorage(Section);
        }

        services.AddProtectedFileArea<AreaA>("LearningPackages");
        services.AddProtectedFileArea<AreaB>("LearningCoursework");

        if (!azureFirst)
        {
            services.AddProtectedFilesAzureBlobStorage(Section);
        }

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IProtectedFileAreaStoreFactory>();

        // Assert
        Assert.IsType<BlobProtectedFileAreaStoreFactory>(factory);
        Assert.IsType<BlobFileStore>(factory.GetStore("LearningPackages"));
        Assert.NotSame(factory.GetStore("LearningPackages"), factory.GetStore("LearningCoursework"));
        Assert.Same(factory.GetStore("LearningPackages"), factory.GetStore("LearningPackages"));
        Assert.Contains(provider.GetServices<IModularTenantEvents>(), events => events is TenantBlobContainerEvents<ProtectedFileAreas>);
    }

    [Fact]
    public void WithAnIncompleteSection_TheAreasStayInTheTenantsApplicationData()
    {
        // Arrange
        var services = Services(Configuration(new Dictionary<string, string>
        {
            [$"{Section}:ContainerName"] = "protected",
        }));
        services.AddProtectedFilesAzureBlobStorage(Section);
        services.AddProtectedFileArea<AreaA>("LearningPackages");

        // Act
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.IsType<FileSystemStore>(provider.GetRequiredService<IProtectedFileAreaStoreFactory>().GetStore("LearningPackages"));
    }

    [Theory]
    [InlineData(null, "LearningPackages")]
    [InlineData("", "LearningPackages")]
    [InlineData("TenantA", "TenantA/LearningPackages")]
    [InlineData("/TenantA/Files/", "TenantA/Files/LearningPackages")]
    [InlineData("TenantA\\Files", "TenantA/Files/LearningPackages")]
    public void EachArea_IsKeptInAFolderOfItsOwnBelowTheBasePath(string basePath, string expected)
        => Assert.Equal(expected, BlobProtectedFileAreaStoreFactory.CombineBasePath(basePath, "LearningPackages"));

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
