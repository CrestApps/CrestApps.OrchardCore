namespace CrestApps.OrchardCore.FileStorage;

/// <summary>
/// The Azure Blob Storage options of the tenant file store registered for <typeparamref name="TStore"/>, so every store
/// keeps its own options.
/// </summary>
/// <typeparam name="TStore">The store the options belong to.</typeparam>
public sealed class TenantStoreBlobStorageOptions<TStore> : TenantBlobStorageOptions
    where TStore : class
{
}
