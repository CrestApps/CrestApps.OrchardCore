namespace CrestApps.OrchardCore.AI.FileSources.Services;

/// <summary>
/// The one folder a local file source on this tenant may read, and the rule for what is inside it.
/// </summary>
public interface ITenantFileSourceRoot
{
    /// <summary>
    /// Gets the canonical, absolute path of this tenant's file-source folder.
    /// </summary>
    /// <returns>The folder path. It may not exist yet.</returns>
    string GetRoot();

    /// <summary>
    /// Gets the canonical path of this tenant's file-source folder, creating it when it is absent.
    /// </summary>
    /// <returns>The folder path.</returns>
    string EnsureRoot();

    /// <summary>
    /// Resolves a configured path to the real folder it names, and refuses anything outside this tenant's
    /// folder.
    /// </summary>
    /// <param name="path">The configured path, relative to the tenant folder or absolute.</param>
    /// <param name="resolved">The canonical path with every link followed.</param>
    /// <param name="reason">Why the path was refused, when it was.</param>
    /// <returns><see langword="true"/> when the path is inside this tenant's folder.</returns>
    bool TryResolve(string path, out string resolved, out string reason);

    /// <summary>
    /// Turns a resolved path into the value to store, which is relative to the tenant folder so it does not
    /// go stale if the tenant is renamed or the application is moved.
    /// </summary>
    /// <param name="resolvedPath">The canonical path.</param>
    /// <returns>The path relative to the tenant folder.</returns>
    string ToStoredValue(string resolvedPath);
}
