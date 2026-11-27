using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Core.FileStorage;

/// <summary>
/// One folder a feature may read on behalf of a tenant, <c>App_Data/Sites/{TenantName}/{folderName}</c>, and the rule
/// for what is inside it. File sources, MCP file resources and any other feature that reads files a tenant chose use
/// it, so no feature reads from the application's content root or another tenant's folder.
/// </summary>
/// <remarks>
/// <para>
/// The folder is computed from the tenant's own shell settings. It is never read from configuration and never taken
/// from a request: a tenant administrator who could widen their own reach by editing either would be able to read
/// another tenant's files, or any file the host process can open.
/// </para>
/// <para>
/// Containment is decided on canonical, fully-resolved paths, following links at every segment, files included.
/// Comparing the text a reader typed catches nothing, and a plain prefix comparison would let
/// <c>.../file-sources-other</c> pass for <c>.../file-sources</c>.
/// </para>
/// </remarks>
public sealed class TenantFolder
{
    // The links one path may go through, as the operating systems limit them (Linux's is 40), so a loop ends.
    private const int MaxLinks = 40;

    private readonly string _root;
    private string _realRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantFolder"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings, which name the tenant.</param>
    /// <param name="shellOptions">The shell options, which locate the tenants' App_Data folder.</param>
    /// <param name="folderName">The name of the feature's folder inside the tenant's folder.</param>
    public TenantFolder(ShellSettings shellSettings, ShellOptions shellOptions, string folderName)
    {
        ArgumentNullException.ThrowIfNull(shellSettings);
        ArgumentNullException.ThrowIfNull(shellOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);

        _root = Path.GetFullPath(Path.Combine(
            shellOptions.ShellsApplicationDataPath,
            shellOptions.ShellsContainerName,
            shellSettings.Name,
            folderName));
    }

    /// <summary>
    /// Gets the canonical, absolute path of the folder. It may not exist yet.
    /// </summary>
    public string Root => _root;

    /// <summary>
    /// Creates the folder when it is absent, and returns its path.
    /// </summary>
    /// <returns>The folder path.</returns>
    public string EnsureRoot()
    {
        Directory.CreateDirectory(_root);

        // The folder may not have existed when the real path was last resolved.
        _realRoot = null;

        return _root;
    }

    /// <summary>
    /// Resolves a path to the real file or folder it names, and refuses anything outside the tenant's folder.
    /// </summary>
    /// <param name="path">The path, relative to the folder or absolute.</param>
    /// <param name="resolved">The canonical path with every link followed.</param>
    /// <param name="reason">Why the path was refused, when it was.</param>
    /// <param name="allowAbsolute">Whether an absolute path inside the folder is accepted. A path that comes from a
    /// request should pass <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the path is inside the folder.</returns>
    public bool TryResolve(string path, out string resolved, out string reason, bool allowAbsolute = true)
    {
        resolved = null;
        reason = null;

        var requested = path?.Trim() ?? string.Empty;

        if (requested.Contains('\0', StringComparison.Ordinal))
        {
            reason = "That is not a usable path.";

            return false;
        }

        var realRoot = GetRealRoot();
        string candidate;

        try
        {
            if (requested.Length == 0)
            {
                candidate = realRoot;
            }
            else if (Path.IsPathRooted(requested))
            {
                if (!allowAbsolute)
                {
                    reason = "Only a path relative to the folder is accepted.";

                    return false;
                }

                candidate = Path.GetFullPath(requested);
            }
            else
            {
                candidate = Path.GetFullPath(Path.Combine(realRoot, requested));
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            reason = "That is not a usable path.";

            return false;
        }

        if (!TryGetRelativeSegments(realRoot, candidate, out var segments))
        {
            reason = "That path is outside the folder this tenant may read.";

            return false;
        }

        return TryWalk(realRoot, segments, links: 0, out resolved, out reason);
    }

    // A link inside the folder can point anywhere, and GetFullPath does not follow one. Walking the segments one at a
    // time, following a link wherever one is found, catches a link that leaves the folder at any depth. A link's target
    // is walked the same way from the folder, one link at a time, so a target that passes through another link (a.txt
    // -> sub/x where sub -> /etc) is followed through that link too, rather than taken as the path it reads as (review 3).
    private static bool TryWalk(string realRoot, string[] segments, int links, out string resolved, out string reason)
    {
        resolved = null;
        reason = null;

        var current = realRoot;

        foreach (var segment in segments)
        {
            string target = null;

            try
            {
                current = Path.GetFullPath(Path.Combine(current, segment));

                // The target of a relative link comes back resolved against the link's own folder.
                var link = Directory.Exists(current)
                    ? Directory.ResolveLinkTarget(current, returnFinalTarget: false)
                    : File.Exists(current) ? File.ResolveLinkTarget(current, returnFinalTarget: false) : null;

                target = link is null ? null : Path.GetFullPath(link.FullName);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
            {
                reason = "That path could not be read.";

                return false;
            }

            if (target is not null)
            {
                if (links >= MaxLinks)
                {
                    reason = "That path follows too many links.";

                    return false;
                }

                if (!TryGetRelativeSegments(realRoot, target, out var targetSegments))
                {
                    reason = "That path, or a folder above it, links outside the folder this tenant may read.";

                    return false;
                }

                if (!TryWalk(realRoot, targetSegments, links + 1, out current, out reason))
                {
                    return false;
                }
            }
        }

        resolved = current;

        return true;
    }

    /// <summary>
    /// Turns a resolved path into the value to store, relative to the folder, so it does not go stale if the tenant is
    /// renamed or the application is moved.
    /// </summary>
    /// <param name="resolvedPath">The canonical path.</param>
    /// <returns>The path relative to the folder.</returns>
    public string ToStoredValue(string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return null;
        }

        var relative = Path.GetRelativePath(GetRealRoot(), resolvedPath).Replace('\\', '/');

        return relative == "." ? string.Empty : relative;
    }

    // The folder with any link on it followed. A host may put its App_Data behind a link; that is the host's own
    // arrangement, and resolving it once keeps such a host working while still refusing a link inside the folder.
    private string GetRealRoot()
    {
        if (_realRoot is not null)
        {
            return _realRoot;
        }

        try
        {
            if (Directory.Exists(_root))
            {
                var target = Directory.ResolveLinkTarget(_root, returnFinalTarget: true);

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

        return _realRoot = _root;
    }

    private static bool TryGetRelativeSegments(string root, string candidate, out string[] segments)
    {
        segments = [];

        string relative;

        try
        {
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

        if (relative == ".." ||
            relative.StartsWith("../", StringComparison.Ordinal) ||
            relative.StartsWith("..\\", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            return false;
        }

        segments = relative.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        return true;
    }
}
