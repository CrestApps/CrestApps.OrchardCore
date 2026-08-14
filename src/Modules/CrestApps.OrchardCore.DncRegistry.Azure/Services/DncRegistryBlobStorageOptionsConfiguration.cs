using Fluid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage.AzureBlob;

namespace CrestApps.OrchardCore.DncRegistry.Azure.Services;

/// <summary>
/// Configures the DNC Registry Azure Blob Storage options from shell configuration.
/// </summary>
internal sealed class DncRegistryBlobStorageOptionsConfiguration : BlobStorageOptionsConfiguration<DncRegistryBlobStorageOptions>
{
    internal const string ConfigurationSectionName = "CrestApps:DncRegistry:AzureBlobStorage";

    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DncRegistryBlobStorageOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="fluidParser">The Fluid parser for template expressions.</param>
    /// <param name="shellConfiguration">The shell configuration.</param>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="logger">The logger.</param>
    public DncRegistryBlobStorageOptionsConfiguration(
        FluidParser fluidParser,
        IShellConfiguration shellConfiguration,
        ShellSettings shellSettings,
        ILogger<DncRegistryBlobStorageOptionsConfiguration> logger)
        : base(fluidParser, shellSettings, logger)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <summary>
    /// Reads the raw options from the configuration section.
    /// </summary>
    protected override DncRegistryBlobStorageOptions GetRawOptions()
        => _shellConfiguration.GetSection(ConfigurationSectionName)
        .Get<DncRegistryBlobStorageOptions>();

    /// <summary>
    /// Applies additional configuration beyond base blob storage options.
    /// </summary>
    /// <param name="rawOptions">The raw options from configuration.</param>
    /// <param name="options">The options instance to configure.</param>
    protected override void FurtherConfigure(DncRegistryBlobStorageOptions rawOptions, DncRegistryBlobStorageOptions options)
    {
        options.CreateContainer = rawOptions.CreateContainer;
        options.RemoveContainer = rawOptions.RemoveContainer;
        options.RemoveFilesFromBasePath = rawOptions.RemoveFilesFromBasePath;
    }
}
