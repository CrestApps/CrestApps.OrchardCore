namespace CrestApps.OrchardCore.FileStorage;

/// <summary>
/// Describes the tenant file store registered for <typeparamref name="TStore"/>: the folder it keeps its files in and
/// where its Azure Blob Storage options are read from.
/// </summary>
/// <typeparam name="TStore">The store.</typeparam>
public sealed class TenantFileStoreRegistration<TStore>
    where TStore : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantFileStoreRegistration{TStore}"/> class.
    /// </summary>
    /// <param name="purpose">The folder, inside the tenant's application data, the store keeps its files in locally.</param>
    /// <param name="configurationSection">The shell configuration section the Azure Blob Storage options are read from.</param>
    /// <param name="removeOnTenantRemoval">Whether the store's blobs may be removed with the tenant.</param>
    public TenantFileStoreRegistration(string purpose, string configurationSection, bool removeOnTenantRemoval)
    {
        Purpose = purpose;
        ConfigurationSection = configurationSection;
        RemoveOnTenantRemoval = removeOnTenantRemoval;
    }

    /// <summary>
    /// Gets the folder, inside the tenant's application data, the store keeps its files in locally.
    /// </summary>
    public string Purpose { get; }

    /// <summary>
    /// Gets the shell configuration section the Azure Blob Storage options are read from, or <see langword="null"/>
    /// when the store is always local.
    /// </summary>
    public string ConfigurationSection { get; }

    /// <summary>
    /// Gets whether the store's container or base path may be removed when the tenant is removed, as the options say.
    /// </summary>
    public bool RemoveOnTenantRemoval { get; }
}
