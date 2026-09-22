using CrestApps.Core.AI.FileSources.Connectors;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.FileSources.Services;

/// <summary>
/// Pins the folders the file-system connector may read to the one folder this tenant owns.
/// </summary>
/// <remarks>
/// <para>
/// The framework's allowed-roots list is a host-wide allow-list, which is the wrong shape for a
/// multi-tenant host: a tenant administrator who can edit their own tenant's configuration would be able to
/// widen their own reach. Here the list is not configuration at all. It is the tenant's own folder,
/// computed from its shell settings, and nothing else.
/// </para>
/// <para>
/// It post-configures rather than configures because the framework's own step runs first and adds whatever
/// the host set. Both what it added and the base path it pointed at the content root have to be taken back,
/// and a single surviving entry is the whole boundary gone.
/// </para>
/// <para>
/// Setting the base path as well as the root is what makes a file source's stored folder tenant-relative:
/// <c>contracts</c> resolves under this tenant's folder rather than against the process's current
/// directory, so a stored value survives the tenant being renamed or the application being moved, and a
/// host-absolute path is never written onto the record.
/// </para>
/// </remarks>
internal sealed class FileSystemConnectorOptionsConfiguration : IPostConfigureOptions<FileSystemConnectorOptions>
{
    private readonly ITenantFileSourceRoot _tenantRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemConnectorOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="tenantRoot">This tenant's file-source folder.</param>
    public FileSystemConnectorOptionsConfiguration(ITenantFileSourceRoot tenantRoot)
    {
        _tenantRoot = tenantRoot;
    }

    /// <summary>
    /// Replaces the allowed roots with this tenant's folder.
    /// </summary>
    /// <param name="name">The options name.</param>
    /// <param name="options">The options instance to configure.</param>
    public void PostConfigure(string name, FileSystemConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var root = _tenantRoot.GetRoot();

        // Whatever the host configured is discarded, base path included. The boundary is this tenant's own
        // folder and nothing else.
        options.AllowedRoots.Clear();
        options.AllowedRoots.Add(root);
        options.BasePath = root;
    }
}
