using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.FileStorage.ProtectedFiles;

/// <summary>
/// The tenant file store every protected file area is kept in.
/// </summary>
/// <remarks>
/// By default no store is shared: each area is kept in a folder of the tenant's App_Data. The Protected Files - Azure
/// Blob Storage feature registers this type with <c>AddTenantFileStore</c>, so the areas share one Azure Blob Storage
/// container, each area in a folder of its own.
/// </remarks>
public sealed class ProtectedFileAreaStore
{
    /// <summary>
    /// The default: no shared store, so each area is kept in a folder of the tenant's App_Data.
    /// </summary>
    public static readonly ProtectedFileAreaStore Local = new(null);

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedFileAreaStore"/> class.
    /// </summary>
    /// <param name="sharedStore">The store every area is kept in, each in a folder of its own, or <see langword="null"/>
    /// to keep each area in a folder of the tenant's App_Data.</param>
    public ProtectedFileAreaStore(IFileStore sharedStore)
    {
        SharedStore = sharedStore;
    }

    /// <summary>
    /// Gets the store every area is kept in, or <see langword="null"/> when each area is kept in a folder of the
    /// tenant's App_Data.
    /// </summary>
    public IFileStore SharedStore { get; }
}
