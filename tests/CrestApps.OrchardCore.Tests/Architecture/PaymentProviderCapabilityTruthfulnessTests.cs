using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Stripe.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Guarantees the <see cref="PaymentProviderCapabilities.SupportsRefunds"/> flag is an executable promise
/// rather than an unbacked boolean: any concrete payment provider that advertises refunds must also
/// implement <see cref="ICheckoutPaymentRefundProvider"/>, and any provider that does not implement it must
/// not advertise refunds. New providers added to a referenced module are checked automatically.
/// </summary>
public sealed class PaymentProviderCapabilityTruthfulnessTests
{
    public static IEnumerable<object[]> ConcreteProviders()
    {
        foreach (var assembly in DiscoverCrestAppsAssemblies())
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (typeof(ICheckoutPaymentProvider).IsAssignableFrom(type))
                {
                    yield return [type];
                }
            }
        }
    }

    // Loads every CrestApps assembly present in the test output so a payment provider added in any module
    // is discovered automatically, instead of only the two providers a hardcoded list would know about.
    private static List<Assembly> DiscoverCrestAppsAssemblies()
    {
        // Touch the two known provider assemblies so their modules are certain to be present in the output.
        _ = typeof(StripeCheckoutPaymentProvider).Assembly;
        _ = typeof(PayLaterCheckoutPaymentProvider).Assembly;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assemblies = new List<Assembly>();

        // The test assembly matches the discovery pattern but contains test-double providers that are not
        // real product providers, so it is excluded from the capability audit.
        var testAssemblyName = typeof(PaymentProviderCapabilityTruthfulnessTests).Assembly.GetName().Name;

        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "CrestApps.OrchardCore.*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(name, testAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add(name))
            {
                continue;
            }

            try
            {
                assemblies.Add(Assembly.LoadFrom(path));
            }
            catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
            {
            }
        }

        return assemblies;
    }

    [Theory]
    [MemberData(nameof(ConcreteProviders))]
    public void SupportsRefunds_MatchesRefundProviderImplementation(Type providerType)
    {
        // Arrange
        var provider = (ICheckoutPaymentProvider)CreateWithMocks(providerType);
        var claimsRefunds = provider.Capabilities.SupportsRefunds;
        var implementsRefundProvider = provider is ICheckoutPaymentRefundProvider;

        // Assert
        Assert.True(
            claimsRefunds == implementsRefundProvider,
            $"'{providerType.Name}' reports SupportsRefunds={claimsRefunds} but " +
            $"{(implementsRefundProvider ? "implements" : "does not implement")} ICheckoutPaymentRefundProvider.");
    }

    [Fact]
    public void KnownProviders_AreCovered()
    {
        // Guards the discovery query itself, so a future refactor that stops finding providers cannot make
        // the truthfulness theory silently vacuous.
        var discovered = ConcreteProviders().Select(data => (Type)data[0]).ToArray();

        Assert.Contains(typeof(StripeCheckoutPaymentProvider), discovered);
        Assert.Contains(typeof(PayLaterCheckoutPaymentProvider), discovered);
    }

    [Fact]
    public void EverySourceDeclaredProvider_IsDiscoverableAtRuntime()
    {
        // Closes the "silently invisible" gap: a provider added in a module the test project does not
        // reference would never load, so the capability audit would skip it. Assert that every concrete
        // provider declared anywhere in the source tree is actually discovered at runtime, forcing the test
        // project to reference each provider module.
        var declaredInSource = FindSourceDeclaredProviderNames();
        var discovered = ConcreteProviders()
            .Select(data => ((Type)data[0]).Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var providerName in declaredInSource)
        {
            Assert.True(
                discovered.Contains(providerName),
                $"Provider '{providerName}' is declared in source but was not discovered at runtime. Add a " +
                "ProjectReference from the test project to its module so the capability audit can see it.");
        }
    }

    private static IEnumerable<string> FindSourceDeclaredProviderNames()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");

        // Captures every class declaration together with its modifiers, its name, and the raw base list (the
        // text between the ':' and the opening brace or a generic 'where' constraint). Following the base
        // list lets the scan reach a provider that implements ICheckoutPaymentProvider through a base class
        // rather than declaring the interface directly.
        var classPattern = new Regex(
            @"(?<mods>(?:\b(?:public|internal|private|protected|sealed|abstract|static|partial)\b\s+)*)class\s+(?<name>\w+)(?:\s*<[^>]*>)?\s*:\s*(?<bases>[^{]+?)\s*(?:\bwhere\b|{)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        var declarations = new Dictionary<string, (bool IsAbstract, HashSet<string> Bases)>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (System.Text.RegularExpressions.Match match in classPattern.Matches(File.ReadAllText(file)))
            {
                var name = match.Groups["name"].Value;
                var isAbstract = match.Groups["mods"].Value.Contains("abstract", StringComparison.Ordinal);

                declarations[name] = (isAbstract, ParseBaseTypeNames(match.Groups["bases"].Value));
            }
        }

        // Seed with every class that declares ICheckoutPaymentProvider directly (never the resolver
        // interface, whose distinct simple name is not this exact token).
        var providers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (name, declaration) in declarations)
        {
            if (declaration.Bases.Contains("ICheckoutPaymentProvider"))
            {
                providers.Add(name);
            }
        }

        // Follow inheritance so a concrete provider that extends an abstract provider base (which carries the
        // interface) is included even though it never names the interface itself.
        bool added;

        do
        {
            added = false;

            foreach (var (name, declaration) in declarations)
            {
                if (!providers.Contains(name) && declaration.Bases.Any(providers.Contains))
                {
                    providers.Add(name);
                    added = true;
                }
            }
        }
        while (added);

        // Only concrete providers are registered and discoverable at runtime; an abstract provider base is
        // never instantiated, so it is excluded from the runtime-discoverability assertion.
        return providers
            .Where(name => !declarations[name].IsAbstract)
            .OrderBy(name => name, StringComparer.Ordinal);
    }

    // Reduces a raw base list ("Base<T>, Namespace.IFoo, IBar") to the set of simple base type names, so
    // inheritance can be followed by name without resolving namespaces or generic arguments.
    private static HashSet<string> ParseBaseTypeNames(string baseList)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var token in baseList.Split(','))
        {
            var trimmed = token.Trim();

            var angle = trimmed.IndexOf('<');

            if (angle >= 0)
            {
                trimmed = trimmed.Substring(0, angle);
            }

            var dot = trimmed.LastIndexOf('.');

            if (dot >= 0)
            {
                trimmed = trimmed.Substring(dot + 1);
            }

            trimmed = trimmed.Trim();

            if (!string.IsNullOrEmpty(trimmed))
            {
                names.Add(trimmed);
            }
        }

        return names;
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

    private static object CreateWithMocks(Type type)
    {
        var constructor = type
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var arguments = constructor
            .GetParameters()
            .Select(parameter => CreateMock(parameter.ParameterType))
            .ToArray();

        return constructor.Invoke(arguments);
    }

    private static object CreateMock(Type type)
    {
        var mockType = typeof(Mock<>).MakeGenericType(type);
        var mock = (Mock)Activator.CreateInstance(mockType);

        return mock.Object;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null);
        }
    }
}
