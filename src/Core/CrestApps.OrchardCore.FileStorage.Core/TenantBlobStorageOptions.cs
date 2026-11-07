using OrchardCore.FileStorage.AzureBlob;

namespace CrestApps.OrchardCore.FileStorage;

/// <summary>
/// The Azure Blob Storage options of a tenant file store, read from the configuration section the store was registered
/// with. The container name and base path may hold Liquid, such as <c>{{ ShellSettings.Name }}</c>.
/// </summary>
public class TenantBlobStorageOptions : BlobStorageOptions
{
    /// <summary>
    /// Gets or sets whether the container is created when the tenant starts, when it does not exist yet.
    /// </summary>
    public bool CreateContainer { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the whole container is deleted when the tenant is removed.
    /// </summary>
    public bool RemoveContainer { get; set; }

    /// <summary>
    /// Gets or sets whether the files under the base path are deleted when the tenant is removed.
    /// </summary>
    public bool RemoveFilesFromBasePath { get; set; }
}
