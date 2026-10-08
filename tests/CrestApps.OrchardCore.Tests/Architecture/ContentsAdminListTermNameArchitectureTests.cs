using System.Reflection;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Contents.Services;
using YesSql;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Asserts that no CrestApps <see cref="IContentsAdminListFilterProvider"/> replaces a term Orchard Core registers on the
/// content list.
/// </summary>
/// <remarks>
/// YesSql keeps one term per name and the last provider to register a name wins. Orchard Core's <c>status</c> term
/// always runs and limits the content list to the latest version of each item; when the CRM registered its own
/// <c>status</c> term, every content list showed every archived version. Each reserved name is registered first here,
/// as Orchard Core's is in a tenant, then each provider builds on top, and the reserved term must still be the one that
/// runs.
/// </remarks>
public sealed class ContentsAdminListTermNameArchitectureTests
{
    // The terms Orchard Core's DefaultContentsAdminListFilterProvider and LocalizationPartContentsAdminListFilterProvider
    // register on the content list.
    private static readonly string[] _reservedNamedTerms = ["status", "sort", "type", "stereotype", "culture"];

    // Providers that replace a reserved term on purpose, with the term they replace. Add an entry only when the
    // replacement keeps Orchard Core's behavior on every list it does not own.
    private static readonly Dictionary<Type, string> _intendedReplacements = new()
    {
        // Adds phone matching to the free-text box on contact lists and keeps the DisplayText match everywhere else.
        [typeof(OmnichannelContactPhoneContentsAdminListFilterProvider)] = ContentsAdminListFilterOptions.DefaultTermName,
    };

    public static TheoryData<string> Providers()
    {
        var data = new TheoryData<string>();

        foreach (var type in DiscoverProviders())
        {
            data.Add(type.AssemblyQualifiedName);
        }

        return data;
    }

    [Fact]
    public void TheKnownContentListProvidersAreDiscovered()
    {
        // Arrange
        var discovered = DiscoverProviders().ToHashSet();

        // Assert
        Assert.Contains(typeof(CrmContentsAdminListFilterProvider), discovered);
        Assert.Contains(typeof(OmnichannelContactPhoneContentsAdminListFilterProvider), discovered);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Build_DoesNotReplaceAContentListTermOwnedByOrchardCore(string providerTypeName)
    {
        // Arrange
        var providerType = Type.GetType(providerTypeName, throwOnError: true);
        var replaced = new List<string>();

        // Act
        foreach (var name in _reservedNamedTerms)
        {
            if (!await ReservedTermStillRunsAsync(providerType, name, isDefaultTerm: false))
            {
                replaced.Add(name);
            }
        }

        if (!await ReservedTermStillRunsAsync(providerType, ContentsAdminListFilterOptions.DefaultTermName, isDefaultTerm: true))
        {
            replaced.Add(ContentsAdminListFilterOptions.DefaultTermName);
        }

        if (_intendedReplacements.TryGetValue(providerType, out var intended))
        {
            replaced.Remove(intended);
        }

        // Assert
        Assert.True(
            replaced.Count == 0,
            $"{providerType.FullName} replaces the Orchard Core content list term(s) '{string.Join("', '", replaced)}'. " +
            "Give the term a name of its own, such as 'lead-status' rather than 'status'.");
    }

    private static async Task<bool> ReservedTermStillRunsAsync(Type providerType, string name, bool isDefaultTerm)
    {
        var reservedTermRan = false;
        var builder = new QueryEngineBuilder<ContentItem>();

        IQuery<ContentItem> MarkRan(string value, IQuery<ContentItem> query)
        {
            reservedTermRan = true;

            return query;
        }

        if (isDefaultTerm)
        {
            builder.WithDefaultTerm(name, term => term.ManyCondition(MarkRan, MarkRan));
        }
        else
        {
            builder.WithNamedTerm(name, term => term.OneCondition(MarkRan));
        }

        var provider = (IContentsAdminListFilterProvider)Activator.CreateInstance(providerType, nonPublic: true);
        provider.Build(builder);

        // A bare value goes to the free-text term; anything else is sent by name.
        var filter = isDefaultTerm ? "value" : $"{name}:value";
        var query = new Mock<IQuery<ContentItem>> { DefaultValue = DefaultValue.Mock };
        var context = new ContentQueryContext(new Mock<IServiceProvider>().Object, query.Object);

        try
        {
            await builder.Build().Parse(filter).ExecuteAsync(context);
        }
        catch
        {
            // A replacing term may need services the empty provider does not have. It still replaced the term.
        }

        return reservedTermRan;
    }

    private static IEnumerable<Type> DiscoverProviders()
    {
        var assemblies = Directory
            .GetFiles(AppContext.BaseDirectory, "CrestApps.OrchardCore.*.dll")
            .Select(TryLoad)
            .Where(assembly => assembly is not null)
            .Distinct();

        return assemblies
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && typeof(IContentsAdminListFilterProvider).IsAssignableFrom(type)
                && type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes) is not null)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
    }

    private static Assembly TryLoad(string path)
    {
        try
        {
            return Assembly.Load(AssemblyName.GetAssemblyName(path));
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null);
        }
    }
}
