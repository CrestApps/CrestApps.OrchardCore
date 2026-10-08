using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.FileSources.Services;

/// <summary>
/// Decides the one folder a local file source on this tenant may read, and whether a given path is inside it.
/// </summary>
/// <remarks>
/// <para>
/// The folder is <c>App_Data/Sites/{TenantName}/file-sources</c>, computed from the tenant's own shell
/// settings. It is never read from configuration and never taken from a request: a tenant administrator who
/// could widen their own reach by editing either would be able to read another tenant's files, or any file
/// the host process can open.
/// </para>
/// <para>
/// Containment is decided on canonical, fully-resolved paths. Comparing the text a reader typed catches
/// nothing -- <c>.../TenantA/file-sources/../../TenantB</c> is a path inside TenantB however it reads -- and
/// a plain prefix comparison would let <c>.../file-sources-other</c> pass for <c>.../file-sources</c>.
/// </para>
/// </remarks>
public sealed class TenantFileSourceRoot : ITenantFileSourceRoot
{
    private readonly ShellSettings _shellSettings;
    private readonly ShellOptions _shellOptions;

    private string _root;
    private string _realRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantFileSourceRoot"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings, which name the tenant.</param>
    /// <param name="shellOptions">The shell options, which locate the tenant's App_Data directory.</param>
    public TenantFileSourceRoot(ShellSettings shellSettings, IOptions<ShellOptions> shellOptions)
    {
        _shellSettings = shellSettings;
        _shellOptions = shellOptions.Value;
    }

    /// <inheritdoc />
    public string GetRoot()
        => _root ??= Path.GetFullPath(Path.Combine(
            _shellOptions.ShellsApplicationDataPath,
            _shellOptions.ShellsContainerName,
            _shellSettings.Name,
            FileSourceConstants.TenantFolderName));

    /// <inheritdoc />
    public string EnsureRoot()
    {
        var root = GetRoot();

        Directory.CreateDirectory(root);

        // The folder may not have existed when the real path was last resolved, so forget it and let the
        // next caller resolve it against the folder that now exists.
        _realRoot = null;

        return root;
    }

    /// <inheritdoc />
    public bool TryResolve(string path, out string resolved, out string reason)
    {
        resolved = null;
        reason = null;

        // An empty value is the tenant folder itself, which is the obvious default and a legitimate choice.
        var requested = path?.Trim() ?? string.Empty;
        var realRoot = GetRealRoot();

        string candidate;

        try
        {
            // Relative to the tenant folder, so a stored value survives the tenant being renamed or the
            // application being moved. An absolute path is accepted and then checked like any other.
            candidate = requested.Length == 0
                ? realRoot
                : Path.IsPathRooted(requested)
                    ? Path.GetFullPath(requested)
                    : Path.GetFullPath(Path.Combine(realRoot, requested));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            reason = "That is not a usable folder path.";

            return false;
        }

        // Checked before anything is opened, and again as each segment is walked below. This one catches a
        // path that climbs out with "..", which normalization above has already turned into a plain path
        // somewhere else on disk.
        if (!TryGetRelativeSegments(realRoot, candidate, out var segments))
        {
            reason = "That folder is outside the folder this tenant may read.";

            return false;
        }

        // A link inside the tenant folder can point anywhere, and GetFullPath does not follow one: it
        // normalizes the text and stops. Walking the segments one at a time, following a link wherever one
        // is found, is what catches a link that leaves the folder -- at any depth, not only the last one.
        var current = realRoot;

        foreach (var segment in segments)
        {
            try
            {
                current = Path.GetFullPath(Path.Combine(current, segment));

                if (Directory.Exists(current))
                {
                    var target = Directory.ResolveLinkTarget(current, returnFinalTarget: true);

                    if (target is not null)
                    {
                        current = Path.GetFullPath(target.FullName);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
            {
                reason = "That folder could not be read.";

                return false;
            }

            if (!IsInside(realRoot, current))
            {
                reason = "That folder, or a folder above it, links outside the folder this tenant may read.";

                return false;
            }
        }

        resolved = current;

        return true;
    }

    /// <inheritdoc />
    public string ToStoredValue(string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return null;
        }

        var relative = Path.GetRelativePath(GetRealRoot(), resolvedPath).Replace('\\', '/');

        return relative == "." ? string.Empty : relative;
    }

    /// <summary>
    /// Gets the tenant folder with any link on it followed, which is what everything beneath it is measured
    /// against.
    /// </summary>
    /// <returns>The real path of the tenant folder.</returns>
    /// <remarks>
    /// A host is free to put its App_Data behind a link, and that is the host's own arrangement rather than
    /// a tenant escaping anything. Resolving it once here is what keeps such a host working while still
    /// refusing a link a tenant creates inside its own folder.
    /// </remarks>
    private string GetRealRoot()
    {
        if (_realRoot is not null)
        {
            return _realRoot;
        }

        var root = GetRoot();

        try
        {
            if (Directory.Exists(root))
            {
                var target = Directory.ResolveLinkTarget(root, returnFinalTarget: true);

                if (target is not null)
                {
                    return _realRoot = Path.GetFullPath(target.FullName);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Nothing to follow, or nothing readable to follow it with. The canonical path stands.
        }

        return _realRoot = root;
    }

    /// <summary>
    /// Splits the path from <paramref name="root"/> to <paramref name="candidate"/> into its segments, and
    /// reports whether the candidate is inside the root at all.
    /// </summary>
    /// <param name="root">The canonical tenant root.</param>
    /// <param name="candidate">The canonical candidate path.</param>
    /// <param name="segments">The segments between the two.</param>
    /// <returns><see langword="true"/> when the candidate is the root or sits beneath it.</returns>
    private static bool TryGetRelativeSegments(string root, string candidate, out string[] segments)
    {
        segments = [];

        string relative;

        try
        {
            // Compared as a path rather than as text, so a sibling sharing a name prefix -- "file-sources"
            // against "file-sources-other" -- cannot pass.
            relative = Path.GetRelativePath(root, candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (relative == ".")
        {
            return true;
        }

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return false;
        }

        segments = relative.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        return true;
    }

    /// <summary>
    /// Determines whether <paramref name="candidate"/> is <paramref name="root"/> or sits beneath it.
    /// </summary>
    /// <param name="root">The canonical tenant root.</param>
    /// <param name="candidate">The canonical candidate path.</param>
    /// <returns><see langword="true"/> when the candidate is inside the root.</returns>
    private static bool IsInside(string root, string candidate)
        => TryGetRelativeSegments(root, candidate, out _);
}
