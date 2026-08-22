using CrestApps.OrchardCore.Telephony.Azure.Services;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage.AzureBlob;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Azure;

/// <summary>
/// Swaps the Telephony recording media store to an Azure Blob Storage backend when this feature is enabled and
/// configured. The bytes are still encrypted at rest by the same data-protection wrapper the local store uses, so
/// enabling this feature only changes where the encrypted recordings live, not how they are protected. When the
/// connection string or container name is missing the feature stays inert and the local store remains in effect.
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

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<TelephonyRecordingBlobStorageOptions>, TelephonyRecordingBlobStorageOptionsConfiguration>();

        var section = _configuration.GetSection(TelephonyRecordingBlobStorageOptionsConfiguration.ConfigurationSectionName);
        var connectionString = section.GetValue<string>(nameof(TelephonyRecordingBlobStorageOptions.ConnectionString));
        var containerName = section.GetValue<string>(nameof(TelephonyRecordingBlobStorageOptions.ContainerName));

        if (!CheckOptions(connectionString, containerName, _logger))
        {
            return;
        }

        // Replace the local encrypted store's file backend with Azure Blob while keeping the same encryption wrapper,
        // so recordings are client-side encrypted before they reach Azure and tenant-wide purge still works.
        services.Replace(ServiceDescriptor.Singleton<IRecordingMediaStore>(serviceProvider =>
        {
            var blobStorageOptions = serviceProvider.GetRequiredService<IOptions<TelephonyRecordingBlobStorageOptions>>().Value;
            var clock = serviceProvider.GetRequiredService<IClock>();
            var contentTypeProvider = serviceProvider.GetRequiredService<IContentTypeProvider>();
            var dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
            var fileStore = new BlobFileStore(blobStorageOptions, clock, contentTypeProvider);

            return new LocalEncryptedRecordingMediaStore(fileStore, dataProtectionProvider);
        }));

        services.AddScoped<IModularTenantEvents, RecordingBlobContainerTenantEvents>();
    }

    private static bool CheckOptions(string connectionString, string containerName, ILogger logger)
    {
        var optionsAreValid = true;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("Azure Telephony recording storage is enabled but not active because the 'ConnectionString' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            logger.LogError("Azure Telephony recording storage is enabled but not active because the 'ContainerName' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        return optionsAreValid;
    }
}
