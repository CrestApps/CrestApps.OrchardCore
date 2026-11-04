using Fluid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage.AzureBlob;

namespace CrestApps.OrchardCore.FileStorage;

/// <summary>
/// Reads the Azure Blob Storage options of the tenant file store registered for <typeparamref name="TStore"/> from its
/// shell configuration section, expanding the Liquid in the container name and base path.
/// </summary>
/// <typeparam name="TStore">The store.</typeparam>
public sealed class TenantBlobStorageOptionsConfiguration<TStore> : BlobStorageOptionsConfiguration<TenantStoreBlobStorageOptions<TStore>>
    where TStore : class
{
    private readonly IShellConfiguration _shellConfiguration;
    private readonly TenantFileStoreRegistration<TStore> _registration;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantBlobStorageOptionsConfiguration{TStore}"/> class.
    /// </summary>
    /// <param name="registration">The store's registration.</param>
    /// <param name="fluidParser">The Fluid parser used to expand the container name and base path.</param>
    /// <param name="shellConfiguration">The shell configuration.</param>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="logger">The logger.</param>
    public TenantBlobStorageOptionsConfiguration(
        TenantFileStoreRegistration<TStore> registration,
        FluidParser fluidParser,
        IShellConfiguration shellConfiguration,
        ShellSettings shellSettings,
        ILogger<TenantBlobStorageOptionsConfiguration<TStore>> logger)
        : base(fluidParser, shellSettings, logger)
    {
        _registration = registration;
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    protected override TenantStoreBlobStorageOptions<TStore> GetRawOptions()
        => string.IsNullOrEmpty(_registration.ConfigurationSection)
            ? new TenantStoreBlobStorageOptions<TStore>()
            : _shellConfiguration.GetSection(_registration.ConfigurationSection).Get<TenantStoreBlobStorageOptions<TStore>>()
                ?? new TenantStoreBlobStorageOptions<TStore>();

    /// <inheritdoc/>
    protected override void FurtherConfigure(TenantStoreBlobStorageOptions<TStore> rawOptions, TenantStoreBlobStorageOptions<TStore> options)
    {
        options.CreateContainer = rawOptions.CreateContainer;
        options.RemoveContainer = rawOptions.RemoveContainer;
        options.RemoveFilesFromBasePath = rawOptions.RemoveFilesFromBasePath;
    }
}
