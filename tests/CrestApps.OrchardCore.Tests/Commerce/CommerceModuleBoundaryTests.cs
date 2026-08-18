using System.Reflection;
using OrchardCore.Data.Migration;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Enforces that the Commerce module stays a thin composition/orchestration shell. It may own the shared
/// admin menu and later cross-domain orchestration, but it must never own persistence, domain data models,
/// migrations, indexes, or a domain store. Those belong to the reusable domain modules (Customers, Orders,
/// Carts, Transactions, Products, Taxation, Checkout, Receipts, Reports) that Commerce composes.
/// </summary>
public sealed class CommerceModuleBoundaryTests
{
    private static readonly Assembly _commerceAssembly = typeof(CrestApps.OrchardCore.Commerce.Startup).Assembly;

    private static readonly Assembly _commerceAbstractionsAssembly = typeof(CrestApps.OrchardCore.Commerce.FinancialDocuments.IFinancialDocumentPolicy).Assembly;

    // Domain web/core and provider assemblies Commerce must never take a dependency on, because doing so
    // would let the orchestration shell reach into or own another domain's data or provider integration.
    // These are matched exactly so the domains' ".Abstractions" contracts stay composable by Commerce.
    private static readonly string[] _forbiddenExactReferences =
    [
        "CrestApps.OrchardCore.Transactions",
        "CrestApps.OrchardCore.Transactions.Core",
        "CrestApps.OrchardCore.Taxation",
        "CrestApps.OrchardCore.Taxation.Core",
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Checkout.Core",
        "CrestApps.OrchardCore.Products",
        "CrestApps.OrchardCore.Products.Core",
        "CrestApps.OrchardCore.Payments",
        "CrestApps.OrchardCore.Payments.Core",
        "CrestApps.OrchardCore.Receipts",
        "CrestApps.OrchardCore.Receipts.Core",
        "CrestApps.OrchardCore.Reports",
        "CrestApps.OrchardCore.Reports.Core",
        "CrestApps.OrchardCore.Addresses",
        "CrestApps.OrchardCore.Addresses.Core",
        "CrestApps.OrchardCore.Customers",
        "CrestApps.OrchardCore.Customers.Core",
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.Orders.Core",
        "CrestApps.OrchardCore.Carts",
        "CrestApps.OrchardCore.Carts.Core",
        "CrestApps.OrchardCore.Subscriptions",
        "CrestApps.OrchardCore.Subscriptions.Core",
        "CrestApps.OrchardCore.Stripe",
        "CrestApps.OrchardCore.PayLater",
    ];

    // Infrastructure prefixes Commerce must never reference. Matched by exact name or a dotted prefix, which
    // is safe here because these families have no ".Abstractions" contract that Commerce is meant to use.
    private static readonly string[] _forbiddenReferencePrefixes =
    [
        "YesSql",
    ];

    [Fact]
    public void CommerceAssembly_DoesNotReferenceDomainPersistenceOrProviders()
    {
        var referenced = _commerceAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        foreach (var forbidden in _forbiddenExactReferences)
        {
            var offending = referenced.FirstOrDefault(name =>
                string.Equals(name, forbidden, StringComparison.Ordinal));

            Assert.True(
                offending is null,
                $"Commerce must stay a thin orchestrator and must not reference '{forbidden}', but references '{offending}'. " +
                "Commerce composes reusable domain '.Abstractions' contracts; it must never own or reach into another domain's persistence or provider.");
        }

        foreach (var forbidden in _forbiddenReferencePrefixes)
        {
            var offending = referenced.FirstOrDefault(name =>
                string.Equals(name, forbidden, StringComparison.Ordinal) ||
                name.StartsWith(forbidden + ".", StringComparison.Ordinal));

            Assert.True(
                offending is null,
                $"Commerce must stay a thin orchestrator and must not reference '{forbidden}', but references '{offending}'. " +
                "Persistence infrastructure belongs to the reusable domain modules Commerce composes, not to the orchestrator.");
        }
    }

    [Fact]
    public void CommerceAssembly_DefinesNoDataMigration()
    {
        var offending = GetLoadableTypes(_commerceAssembly)
            .Where(type => typeof(IDataMigration).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(
            offending.Length == 0,
            $"Commerce must not own schema/migrations, but defines migration types: {string.Join(", ", offending)}.");
    }

    [Fact]
    public void CommerceAssembly_DefinesNoIndexOrIndexProvider()
    {
        var types = GetLoadableTypes(_commerceAssembly);

        var indexProviders = types
            .Where(type => typeof(IIndexProvider).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
            .Select(type => type.FullName)
            .ToArray();

        var indexes = types
            .Where(type => typeof(IIndex).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(
            indexProviders.Length == 0 && indexes.Length == 0,
            "Commerce must not own queryable persistence. Offending index providers: " +
            $"[{string.Join(", ", indexProviders)}]; offending indexes: [{string.Join(", ", indexes)}].");
    }

    [Fact]
    public void CommerceAbstractions_ReferencesNoDomainPersistenceOrProviders()
    {
        var referenced = _commerceAbstractionsAssembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        foreach (var forbidden in _forbiddenExactReferences)
        {
            var offending = referenced.FirstOrDefault(name =>
                string.Equals(name, forbidden, StringComparison.Ordinal));

            Assert.True(
                offending is null,
                $"Commerce.Abstractions must contain only provider-neutral contracts and must not reference '{forbidden}', but references '{offending}'.");
        }

        foreach (var forbidden in _forbiddenReferencePrefixes)
        {
            var offending = referenced.FirstOrDefault(name =>
                string.Equals(name, forbidden, StringComparison.Ordinal) ||
                name.StartsWith(forbidden + ".", StringComparison.Ordinal));

            Assert.True(
                offending is null,
                $"Commerce.Abstractions must not reference persistence infrastructure '{forbidden}', but references '{offending}'.");
        }
    }

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null).ToArray();
        }
    }
}
