using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Guards two pieces of module wiring that nothing else notices is missing.
/// </summary>
/// <remarks>
/// Both failures here shipped and were invisible until a page was opened. Neither breaks the build, neither
/// fails a unit test, and neither logs anything: one throws only when a customer first touches the feature,
/// the other silently renders markup that does nothing.
/// </remarks>
public sealed partial class ModuleWiringTests
{
    /// <summary>
    /// A YesSql collection needs its document table created, and that only happens when the collection is
    /// declared in <c>StoreCollectionOptions</c>. Declaring only the index migration leaves the collection
    /// without a document table, and the first read throws "no such table" at runtime.
    /// </summary>
    [Fact]
    public void EveryStoreCollection_IsDeclaredInStoreCollectionOptions()
    {
        var modulesRoot = GetRepositoryPath("src");

        var declared = new HashSet<string>(StringComparer.Ordinal);
        var used = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in EnumerateSources(modulesRoot))
        {
            var content = File.ReadAllText(file);

            foreach (Match match in CollectionsAdd().Matches(content))
            {
                declared.Add(Normalize(match.Groups["name"].Value));
            }

            // A store names the collection it reads and writes; that is the collection which must exist.
            foreach (Match match in CollectionNameAssignment().Matches(content))
            {
                var name = Normalize(match.Groups["name"].Value);

                // A lowercase identifier is a constructor parameter on a generic base, not a named
                // collection, so it carries no claim about what must exist.
                if (name.Length == 0 || !char.IsUpper(name[0]))
                {
                    continue;
                }

                used.TryAdd(name, Path.GetFileName(file));
            }
        }

        var undeclared = used
            .Where(pair => !declared.Contains(pair.Key))
            .Select(pair => $"'{pair.Key}' used by {pair.Value}")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            undeclared.Length == 0,
            "These YesSql collections are read or written but never declared in StoreCollectionOptions, so their document tables are never created:" +
            Environment.NewLine + string.Join(Environment.NewLine, undeclared));
    }

    /// <summary>
    /// The <c>&lt;script asp-name&gt;</c> and <c>at="Foot"</c> tag helpers live in
    /// <c>OrchardCore.ResourceManagement</c>, which is not a transitive dependency of
    /// <c>OrchardCore.DisplayManagement</c>. Without a direct package reference the <c>@addTagHelper</c>
    /// directive resolves to nothing, and the tags render as literal markup: the page loads, looks right,
    /// and none of its behaviour is wired up.
    /// </summary>
    [Fact]
    public void EveryModuleUsingResourceTagHelpers_ReferencesResourceManagement()
    {
        var modulesRoot = GetRepositoryPath(Path.Combine("src", "Modules"));

        var offenders = new List<string>();

        // Scoped to the commerce modules. Other modules in this repository pick the assembly up transitively
        // through packages such as OrchardCore.Admin, which a project-file check cannot see; asserting about
        // them here would fail on references that are actually present.
        foreach (var moduleDirectory in Directory.EnumerateDirectories(modulesRoot).Where(IsCommerceModule))
        {
            var viewsDirectory = Path.Combine(moduleDirectory, "Views");

            if (!Directory.Exists(viewsDirectory))
            {
                continue;
            }

            var usesTagHelpers = Directory
                .EnumerateFiles(viewsDirectory, "*.cshtml", SearchOption.AllDirectories)
                .Any(view => ResourceTagHelperUsage().IsMatch(File.ReadAllText(view)));

            if (!usesTagHelpers)
            {
                continue;
            }

            var project = Directory.EnumerateFiles(moduleDirectory, "*.csproj").FirstOrDefault();

            if (project is null || !File.ReadAllText(project).Contains("\"OrchardCore.ResourceManagement\"", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetFileName(moduleDirectory));
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These modules use the script or style resource tag helpers in a view but do not reference OrchardCore.ResourceManagement, so the tags render as literal markup:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static bool IsCommerceModule(string moduleDirectory)
    {
        var name = Path.GetFileName(moduleDirectory);

        return name is "CrestApps.OrchardCore.Checkout"
            or "CrestApps.OrchardCore.Subscriptions"
            or "CrestApps.OrchardCore.PayLater"
            or "CrestApps.OrchardCore.Transactions"
            or "CrestApps.OrchardCore.Stripe"
            or "CrestApps.OrchardCore.Commerce"
            or "CrestApps.OrchardCore.Products"
            or "CrestApps.OrchardCore.Taxation"
            or "CrestApps.OrchardCore.Receipts"
            or "CrestApps.OrchardCore.Wizard";
    }

    private static IEnumerable<string> EnumerateSources(string root)
        => Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
                !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    // Collections are named by a constant, so the constant's own name identifies it well enough to match a
    // declaration against a use without resolving the value.
    private static string Normalize(string expression)
    {
        var trimmed = expression.Trim();
        var lastDot = trimmed.LastIndexOf('.');

        return lastDot >= 0 ? trimmed[(lastDot + 1)..] : trimmed;
    }

    private static string GetRepositoryPath(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate '{relativePath}' from the test output directory.");
    }

    [GeneratedRegex(@"Collections\.Add\((?<name>[A-Za-z0-9_.]+)\)")]
    private static partial Regex CollectionsAdd();

    [GeneratedRegex(@"CollectionName\s*=\s*(?<name>[A-Za-z0-9_.]+)\s*;")]
    private static partial Regex CollectionNameAssignment();

    [GeneratedRegex("""<(script|style|link)[^>]*(asp-name=|at="Foot"|at="Head")""", RegexOptions.IgnoreCase)]
    private static partial Regex ResourceTagHelperUsage();
}
