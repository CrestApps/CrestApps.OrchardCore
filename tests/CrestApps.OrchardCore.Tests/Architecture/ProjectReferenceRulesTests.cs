using System.IO;
using System.Xml.Linq;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Enforces the reusable module graph at the level of MSBuild project references, not only compiled
/// metadata. A <c>&lt;ProjectReference&gt;</c> is checked even when nothing in the referenced project is
/// used yet, so an inverted dependency is caught the moment it is declared rather than only once code
/// starts consuming it.
/// </summary>
public sealed class ProjectReferenceRulesTests
{
    private static readonly string[] _foundationProjectMarkers =
    [
        "CrestApps.OrchardCore.Payments",
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Taxation",
        "CrestApps.OrchardCore.Addresses",
    ];

    private static readonly string[] _forbiddenFoundationReferences =
    [
        "CrestApps.OrchardCore.Stripe",
        "CrestApps.OrchardCore.PayLater",
        "CrestApps.OrchardCore.Subscriptions",
        "CrestApps.OrchardCore.Storefront",
        "CrestApps.OrchardCore.Admin",
        "CrestApps.OrchardCore.Commerce",
    ];

    // Reusable, purchase-agnostic modules that other commerce modules build on. Like the foundation
    // abstractions, they must never depend on a concrete gateway or a presentation/orchestration module.
    private static readonly string[] _reusableModuleMarkers =
    [
        "CrestApps.OrchardCore.Products",
        "CrestApps.OrchardCore.Users",
    ];

    // Presentation and orchestration modules a reusable module (including Subscriptions) must never
    // reference, so the dependency direction always points from presentation toward reusable contracts.
    private static readonly string[] _presentationReferences =
    [
        "CrestApps.OrchardCore.Storefront",
        "CrestApps.OrchardCore.Admin",
        "CrestApps.OrchardCore.Commerce",
    ];

    [Fact]
    public void AbstractionProjects_OnlyReferenceOtherAbstractions()
    {
        var abstractionsRoot = Path.Combine(FindRepositoryRoot(), "src", "Abstractions");

        foreach (var project in Directory.EnumerateFiles(abstractionsRoot, "*.csproj", SearchOption.AllDirectories))
        {
            foreach (var reference in ReadProjectReferenceNames(project))
            {
                Assert.True(
                    reference.Contains(".Abstractions", StringComparison.Ordinal),
                    $"Abstraction project '{Path.GetFileName(project)}' must only reference other abstraction " +
                    $"projects, but references '{reference}'. Abstractions are a pure contract layer.");
            }
        }
    }

    [Fact]
    public void FoundationProjects_DoNotReferenceProvidersOrPresentation()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");

        foreach (var project in Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(project);

            if (!IsFoundationProject(projectName))
            {
                continue;
            }

            foreach (var reference in ReadProjectReferenceNames(project))
            {
                var offending = _forbiddenFoundationReferences.FirstOrDefault(forbidden =>
                    reference.StartsWith(forbidden, StringComparison.Ordinal));

                Assert.True(
                    offending is null,
                    $"Foundation project '{projectName}' must stay provider- and presentation-neutral, but " +
                    $"references '{reference}'. Concrete providers and storefront/admin adapters depend on the " +
                    "foundation, never the reverse.");
            }
        }
    }

    private static bool IsFoundationProject(string projectName)
        => _foundationProjectMarkers.Any(marker =>
            projectName.Equals(marker, StringComparison.Ordinal) ||
            projectName.StartsWith(marker + ".", StringComparison.Ordinal));

    [Fact]
    public void ReusableModules_DoNotReferenceProvidersOrPresentation()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");

        foreach (var project in Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(project);

            if (!IsReusableModule(projectName))
            {
                continue;
            }

            foreach (var reference in ReadProjectReferenceNames(project))
            {
                var offending = _forbiddenFoundationReferences.FirstOrDefault(forbidden =>
                    reference.StartsWith(forbidden, StringComparison.Ordinal));

                Assert.True(
                    offending is null,
                    $"Reusable module '{projectName}' must stay provider- and presentation-neutral, but " +
                    $"references '{reference}'. Providers, subscriptions, and storefront/admin adapters depend " +
                    "on the reusable catalog, never the reverse.");
            }
        }
    }

    [Fact]
    public void Subscriptions_DoNotReferencePresentation()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");

        foreach (var project in Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(project);

            if (!projectName.StartsWith("CrestApps.OrchardCore.Subscriptions", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var reference in ReadProjectReferenceNames(project))
            {
                var offending = _presentationReferences.FirstOrDefault(forbidden =>
                    reference.StartsWith(forbidden, StringComparison.Ordinal));

                Assert.True(
                    offending is null,
                    $"Subscriptions project '{projectName}' may build on shared reusable contracts (including a " +
                    $"payment gateway) but must never reference commerce presentation, yet references '{reference}'.");
            }
        }
    }

    private static bool IsReusableModule(string projectName)
        => _reusableModuleMarkers.Any(marker =>
            projectName.Equals(marker, StringComparison.Ordinal) ||
            projectName.StartsWith(marker + ".", StringComparison.Ordinal));

    private static IEnumerable<string> ReadProjectReferenceNames(string projectPath)
    {
        var document = XDocument.Load(projectPath);

        foreach (var element in document.Descendants("ProjectReference"))
        {
            var include = element.Attribute("Include")?.Value;

            if (string.IsNullOrEmpty(include))
            {
                continue;
            }

            yield return Path.GetFileNameWithoutExtension(include.Replace('\\', Path.DirectorySeparatorChar));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root (CrestApps.OrchardCore.slnx).");
    }
}
