using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.FileStorage.ProtectedFiles;

/// <summary>
/// Creates the file store a protected file area keeps its files in.
/// </summary>
/// <remarks>
/// The default keeps every area in a folder of the tenant's <c>App_Data</c>. A feature that moves protected files
/// somewhere else, such as Protected Files - Azure Blob Storage, replaces this service, so every area moves at once.
/// </remarks>
public interface IProtectedFileAreaStoreFactory
{
    /// <summary>
    /// Gets the file store of an area.
    /// </summary>
    /// <param name="folder">The folder of the area, as passed to <c>AddProtectedFileArea</c>.</param>
    /// <returns>The file store.</returns>
    IFileStore GetStore(string folder);
}
