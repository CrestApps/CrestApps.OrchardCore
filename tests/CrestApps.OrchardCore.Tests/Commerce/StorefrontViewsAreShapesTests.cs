using System.Reflection;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Every storefront page of the commerce modules renders through a named shape, so a theme can override the page with
/// a Razor or Liquid template of that shape's name, or from the admin with the Templates feature.
/// </summary>
/// <remarks>
/// <para>
/// A controller view (<c>Views/{Controller}/{Action}.cshtml</c>) can only be replaced by a theme that ships a Razor file
/// at the same path. The pages therefore keep only their frame there (the title, the breadcrumb, the account menu) with
/// <c>@model IShape</c>, and render the shape the controller built with <c>DisplayAsync(Model)</c>, or as the
/// <c>Content</c> of the <c>CommerceAccountPage</c> shape that frames an account page; the page's content lives in the
/// shape's template.
/// </para>
/// <para>
/// The shape's model is a proxy Orchard Core derives from the view model, so a sealed view model, or one without a
/// parameterless constructor, renders an empty page with no error anywhere. The second guard catches that.
/// </para>
/// </remarks>
public sealed partial class StorefrontViewsAreShapesTests
{
    private static readonly string[] _modules =
    [
        "CrestApps.OrchardCore.Carts",
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Commerce",
        "CrestApps.OrchardCore.Customers",
        "CrestApps.OrchardCore.Downloads",
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Products",
        "CrestApps.OrchardCore.Stripe",
        "CrestApps.OrchardCore.Subscriptions",
        "CrestApps.OrchardCore.Transactions",
    ];

    // Storefront controller views that keep their own markup, each with the reason.
    private static readonly Dictionary<string, string> _allowedViews = new(StringComparer.OrdinalIgnoreCase)
    {
        ["src/Modules/CrestApps.OrchardCore.Checkout/Views/Checkout/Display.cshtml"] =
            "The step form that posts the CheckoutFlow editor shape it renders; the layout lives in that shape's template.",
        ["src/Modules/CrestApps.OrchardCore.Checkout/Views/Checkout/OnePage.cshtml"] =
            "The one-page form that posts each step's editor shape it renders; the sections are those steps' shapes.",
        ["src/Modules/CrestApps.OrchardCore.Checkout/Views/Checkout/Confirmation.cshtml"] =
            "Renders the CheckoutFlow Confirmation display shape a theme overrides; around it is only the thank-you frame.",
    };

    [Fact]
    public void StorefrontControllerViews_RenderAShape()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();
        var scannedControllers = new HashSet<string>(StringComparer.Ordinal);
        var scannedViewCount = 0;

        // Act
        foreach (var (module, controller) in GetStorefrontControllers(repositoryRoot))
        {
            scannedControllers.Add(controller);

            var viewsDirectory = Path.Combine(repositoryRoot, "src", "Modules", module, "Views", controller);

            if (!Directory.Exists(viewsDirectory))
            {
                continue;
            }

            foreach (var view in Directory.EnumerateFiles(viewsDirectory, "*.cshtml"))
            {
                scannedViewCount++;

                var relativePath = Path.GetRelativePath(repositoryRoot, view).Replace(Path.DirectorySeparatorChar, '/');

                if (_allowedViews.ContainsKey(relativePath))
                {
                    continue;
                }

                var content = File.ReadAllText(view);

                if (!ShapeModel().IsMatch(content) || !DisplaysModel().IsMatch(content))
                {
                    violations.Add(relativePath);
                }
            }
        }

        // Assert
        Assert.Contains("Cart", scannedControllers);
        Assert.Contains("AccountOrders", scannedControllers);
        Assert.Contains("AccountPaymentMethods", scannedControllers);
        Assert.DoesNotContain("OrdersAdmin", scannedControllers);
        Assert.DoesNotContain("MyAccount", scannedControllers);
        Assert.True(
            scannedViewCount >= 20,
            $"Only {scannedViewCount} storefront controller views were found, so the guard is no longer reading the pages it should.");
        Assert.True(
            violations.Count == 0,
            "These storefront pages render their own markup, so a theme can only replace them with a Razor file at the same " +
            "path. Build a named shape in the controller, move the page's content into Views/{ShapeName}.cshtml, and keep " +
            "the controller view as '@model IShape' with '@await DisplayAsync(Model)': " +
            string.Join(", ", violations));
    }

    [Fact]
    public void StorefrontViewAllowList_OnlyNamesViewsThatExist()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();

        // Act
        var missing = _allowedViews.Keys
            .Where(relativePath => !File.Exists(Path.Combine(repositoryRoot, relativePath)))
            .ToArray();

        // Assert
        Assert.True(missing.Length == 0, $"The storefront view allow-list names views that no longer exist: {string.Join(", ", missing)}.");
    }

    [Fact]
    public void StorefrontShapeViewModels_CanBeProxied()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var problems = new List<string>();
        var checkedTypes = 0;

        // Act
        foreach (var module in _modules)
        {
            var types = GetTypesVisibleTo(Assembly.Load(new AssemblyName(module)));

            foreach (var (_, controller) in GetStorefrontControllers(repositoryRoot).Where(entry => entry.Module == module))
            {
                var source = File.ReadAllText(FindControllerSource(repositoryRoot, module, controller));

                foreach (var name in TypedShapeModels().Matches(source).Select(match => match.Groups["type"].Value).Distinct())
                {
                    if (!types.TryGetValue(name, out var type))
                    {
                        problems.Add($"'{name}' (built by {controller}Controller) could not be found");

                        continue;
                    }

                    checkedTypes++;

                    if (type.IsSealed)
                    {
                        problems.Add($"'{name}' is sealed");
                    }

                    if (type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is null)
                    {
                        problems.Add($"'{name}' has no public parameterless constructor");
                    }
                }
            }
        }

        // Assert
        Assert.True(checkedTypes >= 10, $"Only {checkedTypes} storefront shape view models were found, so the guard read too little.");
        Assert.True(
            problems.Count == 0,
            "Orchard Core builds a typed shape as a proxy derived from its view model; these can't be derived from, so their " +
            "pages would render empty: " + string.Join("; ", problems));
    }

    // The storefront controllers of the commerce modules: every controller without [Admin] on the class or an action.
    private static List<(string Module, string Controller)> GetStorefrontControllers(string repositoryRoot)
    {
        var controllers = new List<(string Module, string Controller)>();

        foreach (var module in _modules)
        {
            var controllersDirectory = Path.Combine(repositoryRoot, "src", "Modules", module, "Controllers");

            if (!Directory.Exists(controllersDirectory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(controllersDirectory, "*.cs"))
            {
                var source = File.ReadAllText(file);
                var declarations = ControllerDeclaration().Matches(source);

                for (var i = 0; i < declarations.Count; i++)
                {
                    // A class runs from its attributes to the next class's attributes.
                    var start = declarations[i].Index;
                    var end = i + 1 < declarations.Count ? declarations[i + 1].Index : source.Length;

                    if (!AdminAttribute().IsMatch(source[start..end]))
                    {
                        controllers.Add((module, declarations[i].Groups["name"].Value));
                    }
                }
            }
        }

        return controllers;
    }

    private static string FindControllerSource(string repositoryRoot, string module, string controller)
        => Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src", "Modules", module, "Controllers"), "*.cs")
            .First(file => Regex.IsMatch(File.ReadAllText(file), $@"\bclass\s+{controller}Controller\b"));

    // A module's own types and those of the CrestApps assemblies it builds on, by simple name.
    private static Dictionary<string, Type> GetTypesVisibleTo(Assembly assembly)
    {
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        var assemblies = assembly
            .GetReferencedAssemblies()
            .Where(name => name.Name?.StartsWith("CrestApps.", StringComparison.Ordinal) == true)
            .Select(Assembly.Load)
            .Prepend(assembly);

        foreach (var type in assemblies.SelectMany(candidate => candidate.GetExportedTypes()))
        {
            types.TryAdd(type.Name, type);
        }

        return types;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }

    [GeneratedRegex(@"(?:\[[^\]\r\n]*\]\s*)*public\s+(?:sealed\s+)?class\s+(?<name>\w+)Controller\b")]
    private static partial Regex ControllerDeclaration();

    [GeneratedRegex(@"\[Admin[\]\(]")]
    private static partial Regex AdminAttribute();

    [GeneratedRegex(@"^\s*@model\s+(?:OrchardCore\.DisplayManagement\.)?IShape\s*$", RegexOptions.Multiline)]
    private static partial Regex ShapeModel();

    // The shape renders directly, or as the content of the CommerceAccountPage frame shape of an account page.
    [GeneratedRegex(@"DisplayAsync\(\s*Model\s*\)|New\.CommerceAccountPage\([^;]*\bContent:\s*Model\b")]
    private static partial Regex DisplaysModel();

    [GeneratedRegex(@"CreateAsync<(?<type>\w+)>\(")]
    private static partial Regex TypedShapeModels();
}
