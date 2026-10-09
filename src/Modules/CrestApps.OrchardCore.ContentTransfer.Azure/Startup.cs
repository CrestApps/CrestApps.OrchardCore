using CrestApps.OrchardCore.ContentTransfer.Azure.Services;
using CrestApps.OrchardCore.ContentTransfer.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage.AzureBlob;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContentTransfer.Azure;

/// <summary>
/// Registers services for Azure Blob Storage-backed Content Transfer file storage.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly ILogger _logger;
    private readonly IShellConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The shell configuration.</param>
    public Startup(
        ILogger<Startup> logger,
        IShellConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Configures Content Transfer Azure Blob Storage services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<ContentTransferBlobStorageOptions>, ContentTransferBlobStorageOptionsConfiguration>();

        var section = _configuration.GetSection(ContentTransferBlobStorageOptionsConfiguration.ConfigurationSectionName);
        var connectionString = section.GetValue<string>(nameof(ContentTransferBlobStorageOptions.ConnectionString));
        var containerName = section.GetValue<string>(nameof(ContentTransferBlobStorageOptions.ContainerName));

        if (!CheckOptions(connectionString, containerName, _logger))
        {
            return;
        }

        services.Replace(ServiceDescriptor.Singleton<IContentTransferFileStore>(serviceProvider =>
        {
            var blobStorageOptions = serviceProvider.GetRequiredService<IOptions<ContentTransferBlobStorageOptions>>().Value;
            var clock = serviceProvider.GetRequiredService<IClock>();
            var contentTypeProvider = serviceProvider.GetService<IContentTypeProvider>() ?? new FileExtensionContentTypeProvider();
            var fileStore = new BlobFileStore(blobStorageOptions, clock, contentTypeProvider);

            return new ContentTransferFileStore(fileStore);
        }));

        services.AddScoped<IModularTenantEvents, ContentTransferBlobContainerTenantEvents>();
    }

    private static bool CheckOptions(string connectionString, string containerName, ILogger logger)
    {
        var optionsAreValid = true;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("Azure Content Transfer storage is enabled but not active because the 'ConnectionString' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            logger.LogError("Azure Content Transfer storage is enabled but not active because the 'ContainerName' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        return optionsAreValid;
    }
}
