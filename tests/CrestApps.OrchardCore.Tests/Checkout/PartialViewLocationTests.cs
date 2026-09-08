using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Checks that every <c>&lt;partial&gt;</c> a commerce view references can actually be found at runtime.
/// </summary>
/// <remarks>
/// Razor resolves a partial by name from the calling view's own folder and from <c>Views/Shared</c>, and
/// nowhere else. A partial that sits directly in <c>Views/</c> compiles, ships, and then throws
/// <c>InvalidOperationException</c> the first time somebody opens the page that uses it. Nothing before that
/// moment complains, which is why two commerce screens — the agreement list and the transaction list — both
/// failed on their first real page load.
/// </remarks>
public sealed partial class PartialViewLocationTests
{
    /// <summary>
    /// A partial must live beside the view that uses it or in that module's <c>Views/Shared</c> folder.
    /// </summary>
    [Fact]
    public void EveryReferencedPartial_ResolvesFromItsCallingView()
    {
        var moduleRoot = GetModulesRoot();

        var missing = new List<string>();

        foreach (var view in Directory.EnumerateFiles(moduleRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            if (view.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                view.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var content = File.ReadAllText(view);

            foreach (Match match in PartialReference().Matches(content))
            {
                var name = match.Groups["name"].Value;

                // Only relative names are resolved by convention; an explicit path is the author's business.
                if (name.Contains('/', StringComparison.Ordinal) || name.Contains('~', StringComparison.Ordinal))
                {
                    continue;
                }

                var callingFolder = Path.GetDirectoryName(view);
                var viewsRoot = FindViewsRoot(callingFolder);

                var beside = Path.Combine(callingFolder, name + ".cshtml");
                var shared = viewsRoot is null ? null : Path.Combine(viewsRoot, "Shared", name + ".cshtml");

                if (File.Exists(beside) || (shared is not null && File.Exists(shared)))
                {
                    continue;
                }

                missing.Add($"'{name}' referenced by '{Path.GetRelativePath(moduleRoot, view)}'");
            }
        }

        Assert.True(
            missing.Count == 0,
            "These partials cannot be resolved from the view that references them. Move each one next to its caller or into that module's Views/Shared folder:" +
            Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    private static string FindViewsRoot(string folder)
    {
        var current = new DirectoryInfo(folder);

        while (current is not null)
        {
            if (string.Equals(current.Name, "Views", StringComparison.Ordinal))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string GetModulesRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Modules");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the 'src/Modules' folder from the test output directory.");
    }

    [GeneratedRegex(@"<partial\s+name\s*=\s*""(?<name>[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PartialReference();
}
