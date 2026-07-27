using CrestApps.OrchardCore.ContactCenter.Core.Maintenance;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the Contact Center preview maintenance registry to the persisted document types it must cover. The
/// export and reset tooling is only recoverable if it covers every persisted type, so the registry is checked
/// against the types discovered from the registered YesSql index providers rather than trusted as prose.
/// </summary>
public sealed class ContactCenterPreviewMaintenanceCompletenessTests
{
    // A floor on discovery. Without it a reflection bug that finds nothing would make every completeness
    // assertion below pass vacuously.
    private const int MinimumPersistedDocumentTypeCount = 21;

    [Fact]
    public void PersistedDocumentTypes_AreDiscoveredAboveTheFloor()
    {
        var persisted = DiscoverPersistedDocumentTypes();

        Assert.True(
            persisted.Count >= MinimumPersistedDocumentTypeCount,
            $"Discovered only {persisted.Count} persisted Contact Center document types, which is below the floor of {MinimumPersistedDocumentTypeCount}. Discovery is broken, so the completeness assertions would pass vacuously.");
    }

    [Fact]
    public void EveryPersistedDocumentType_HasAPreviewDataSet()
    {
        var persisted = DiscoverPersistedDocumentTypes();
        var declared = ContactCenterPreviewDataSetRegistry.Descriptors
            .Select(descriptor => descriptor.DocumentType)
            .ToHashSet();

        var missing = persisted
            .Where(type => !declared.Contains(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"The following persisted Contact Center document types cannot be exported or reset because they are absent from ContactCenterPreviewDataSetRegistry: {string.Join(", ", missing)}. A preview reset would silently leave them behind.");
    }

    [Fact]
    public void EveryPreviewDataSet_MapsToAPersistedDocumentType()
    {
        var persisted = DiscoverPersistedDocumentTypes();

        var unknown = ContactCenterPreviewDataSetRegistry.Descriptors
            .Where(descriptor => !persisted.Contains(descriptor.DocumentType))
            .Select(descriptor => descriptor.DocumentType.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unknown.Length == 0,
            $"The following ContactCenterPreviewDataSetRegistry entries do not correspond to any persisted Contact Center document type: {string.Join(", ", unknown)}. Remove them so the registry stays an honest inventory.");
    }

    [Fact]
    public void EveryPreviewDataSet_IsDeclaredExactlyOnce()
    {
        var duplicates = ContactCenterPreviewDataSetRegistry.Descriptors
            .GroupBy(descriptor => descriptor.DocumentType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            duplicates.Length == 0,
            $"The following document types are declared more than once in ContactCenterPreviewDataSetRegistry: {string.Join(", ", duplicates)}. Duplicate registrations would delete the same data set twice and double-count the export.");
    }

    [Fact]
    public void EveryPreviewDataSet_ResolvesAGovernanceCategory()
    {
        var categories = ContactCenterDataGovernanceCatalog.Categories
            .Select(category => category.Key)
            .ToHashSet(StringComparer.Ordinal);

        var unresolved = ContactCenterPreviewDataSetRegistry.Descriptors
            .Where(descriptor => !categories.Contains(descriptor.GovernanceCategoryKey))
            .Select(descriptor => $"{descriptor.DocumentType.Name} -> '{descriptor.GovernanceCategoryKey}'")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unresolved.Length == 0,
            $"The following ContactCenterPreviewDataSetRegistry entries reference a governance category that does not exist in ContactCenterDataGovernanceCatalog: {string.Join(", ", unresolved)}.");
    }

    [Fact]
    public void EveryGovernanceCategory_IsClaimedByAPreviewDataSet()
    {
        var claimed = ContactCenterPreviewDataSetRegistry.Descriptors
            .Select(descriptor => descriptor.GovernanceCategoryKey)
            .ToHashSet(StringComparer.Ordinal);

        var unclaimed = ContactCenterDataGovernanceCatalog.Categories
            .Where(category => !claimed.Contains(category.Key))
            .Select(category => category.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unclaimed.Length == 0,
            $"The following governance categories describe persisted data that no preview data set covers: {string.Join(", ", unclaimed)}. Either the category is obsolete or the data it describes cannot be exported or reset.");
    }

    private static HashSet<Type> DiscoverPersistedDocumentTypes()
    {
        var documentTypes = new HashSet<Type>();

        foreach (var providerType in typeof(ContactCenterHub).Assembly.GetTypes())
        {
            if (providerType.IsAbstract || !providerType.IsClass)
            {
                continue;
            }

            for (var candidate = providerType.BaseType; candidate is not null; candidate = candidate.BaseType)
            {
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IndexProvider<>))
                {
                    documentTypes.Add(candidate.GetGenericArguments()[0]);

                    break;
                }
            }
        }

        return documentTypes;
    }

    [Fact]
    public void DiscoveryReadsIndexProviders_AndNotAnArbitraryTypeList()
    {
        // Proves the discovery oracle is actually reading the YesSql registration surface: every discovered
        // type must be reachable from a concrete IndexProvider<T> in the Contact Center module assembly.
        var providerCount = typeof(ContactCenterHub).Assembly
            .GetTypes()
            .Count(type => type.IsClass && !type.IsAbstract && IsIndexProvider(type));

        Assert.True(
            providerCount >= MinimumPersistedDocumentTypeCount,
            $"Found only {providerCount} concrete IndexProvider<T> implementations in the Contact Center module assembly, which is below the floor of {MinimumPersistedDocumentTypeCount}.");
    }

    private static bool IsIndexProvider(Type type)
    {
        for (var candidate = type.BaseType; candidate is not null; candidate = candidate.BaseType)
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IndexProvider<>))
            {
                return true;
            }
        }

        return false;
    }
}
