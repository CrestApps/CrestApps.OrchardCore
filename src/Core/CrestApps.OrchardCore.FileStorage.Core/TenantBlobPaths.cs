namespace CrestApps.OrchardCore.FileStorage;

/// <summary>
/// Builds the blob prefixes a tenant's removal deletes.
/// </summary>
internal static class TenantBlobPaths
{
    /// <summary>
    /// Returns the prefix of the blobs under a base path, ending with a delimiter so a sibling folder whose name starts
    /// the same way is never matched, or <see langword="null"/> when there is no base path, since a removal must then
    /// delete nothing rather than every blob of the container.
    /// </summary>
    /// <param name="basePath">The store's base path.</param>
    public static string GetRemovalPrefix(string basePath)
    {
        var trimmed = basePath?.Trim().Trim('/');

        return string.IsNullOrEmpty(trimmed) ? null : trimmed + "/";
    }
}
