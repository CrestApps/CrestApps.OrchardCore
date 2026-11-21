using CrestApps.OrchardCore.Core.FileStorage;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.FileSources.Services;

/// <summary>
/// Decides the one folder a local file source on this tenant may read, <c>App_Data/Sites/{TenantName}/file-sources</c>,
/// and whether a given path is inside it. The rule itself is the shared <see cref="TenantFolder"/>, which MCP file
/// resources use too.
/// </summary>
public sealed class TenantFileSourceRoot : ITenantFileSourceRoot
{
    private readonly TenantFolder _folder;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantFileSourceRoot"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings, which name the tenant.</param>
    /// <param name="shellOptions">The shell options, which locate the tenant's App_Data directory.</param>
    public TenantFileSourceRoot(ShellSettings shellSettings, IOptions<ShellOptions> shellOptions)
    {
        _folder = new TenantFolder(shellSettings, shellOptions.Value, FileSourceConstants.TenantFolderName);
    }

    /// <inheritdoc />
    public string GetRoot()
        => _folder.Root;

    /// <inheritdoc />
    public string EnsureRoot()
        => _folder.EnsureRoot();

    /// <inheritdoc />
    public bool TryResolve(string path, out string resolved, out string reason)
        => _folder.TryResolve(path, out resolved, out reason);

    /// <inheritdoc />
    public string ToStoredValue(string resolvedPath)
        => _folder.ToStoredValue(resolvedPath);
}
