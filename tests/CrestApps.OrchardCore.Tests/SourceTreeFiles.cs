namespace CrestApps.OrchardCore.Tests;

/// <summary>
/// Lists the files under a source folder the way <see cref="Directory.EnumerateFiles(string, string, SearchOption)"/>
/// does with <see cref="SearchOption.AllDirectories"/>, except that it never walks into the folders a test skips.
/// </summary>
/// <remarks>
/// Source scans skip build output, packages and generated assets. Filtering those paths after a full walk still opens
/// every folder under every bin, obj and node_modules folder, which is more than half the tree and made a handful of
/// architecture tests take most of the suite's time. Skipping a folder here returns the same files a full walk followed
/// by the same path filter would, as long as every skipped folder is one the caller's filter also excludes.
/// </remarks>
internal static class SourceTreeFiles
{
    /// <summary>
    /// Whether a folder is build output (bin or obj). Source scans that drop paths containing a bin or obj segment can
    /// pass this to skip those folders instead of walking them first.
    /// </summary>
    public static bool IsBuildOutputDirectory(string directory)
    {
        return Path.GetFileName(directory) is "bin" or "obj";
    }

    public static IEnumerable<string> Enumerate(string root, string searchPattern, Func<string, bool> skipDirectory)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Pop();

            foreach (var file in Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (!skipDirectory(childDirectory))
                {
                    pendingDirectories.Push(childDirectory);
                }
            }
        }
    }
}
