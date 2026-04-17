using CrestApps.Core.AI.Documents;
using CrestApps.OrchardCore.AI.Documents.Azure.Services;
using CrestApps.OrchardCore.AI.Documents.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage.AzureBlob;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Azure;

public sealed class Startup : StartupBase
{
    private readonly ILogger _logger;
    private readonly IShellConfiguration _configuration;

    public Startup(
        ILogger<Startup> logger,
        IShellConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AIDocumentBlobStorageOptions>, AIDocumentBlobStorageOptionsConfiguration>();

        var section = _configuration.GetSection(AIDocumentBlobStorageOptionsConfiguration.ConfigurationSectionName);
        var connectionString = section.GetValue<string>(nameof(AIDocumentBlobStorageOptions.ConnectionString));
        var containerName = section.GetValue<string>(nameof(AIDocumentBlobStorageOptions.ContainerName));

        if (!CheckOptions(connectionString, containerName, _logger))
        {
            return;
        }

        services.Replace(ServiceDescriptor.Singleton<IDocumentFileStore>(serviceProvider =>
        {
            var blobStorageOptions = serviceProvider.GetRequiredService<IOptions<AIDocumentBlobStorageOptions>>().Value;
            var clock = serviceProvider.GetRequiredService<IClock>();
            var contentTypeProvider = serviceProvider.GetRequiredService<IContentTypeProvider>();
            var fileStore = new BlobFileStore(blobStorageOptions, clock, contentTypeProvider);

            return new DefaultDocumentFileStore(fileStore);
        }));

        services.AddScoped<IModularTenantEvents, AIDocumentBlobContainerTenantEvents>();
    }

    private static bool CheckOptions(string connectionString, string containerName, ILogger logger)
    {
        var optionsAreValid = true;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("Azure AI document storage is enabled but not active because the 'ConnectionString' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            logger.LogError("Azure AI document storage is enabled but not active because the 'ContainerName' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        return optionsAreValid;
    }
}
