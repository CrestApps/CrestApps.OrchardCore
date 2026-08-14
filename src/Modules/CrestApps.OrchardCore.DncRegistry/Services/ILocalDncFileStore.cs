using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// A dedicated file store for tenant-local Local DNC Registry uploads.
/// </summary>
public interface ILocalDncFileStore : IFileStore
{
}
