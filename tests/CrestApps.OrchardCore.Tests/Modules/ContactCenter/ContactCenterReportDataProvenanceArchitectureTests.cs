using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Guards against reports that measure a field nothing ever writes.
/// </summary>
/// <remarks>
/// Two reports shipped reading persisted state that no code path in the product ever assigned. Call-leg performance
/// read <c>Interaction.CallLegs</c>, which was declared and never written - the legs were always projected onto the
/// call session instead - and transcript coverage read <c>Interaction.TranscriptReference</c>, which belongs to a
/// quality-management pillar that does not exist yet. Both rendered on every tenant, and both were indistinguishable
/// from a real measurement: an empty table and a confident 0%.
/// <para>
/// Nothing catches this at runtime. The query succeeds, the aggregation succeeds, and the report renders. It is only
/// visible by asking the opposite question: for every persisted field a report reads, is there anywhere in the product
/// that writes it?
/// </para>
/// </remarks>
public sealed class ContactCenterReportDataProvenanceArchitectureTests
{
    private static readonly Type[] _reportedModels =
    [
        typeof(Interaction),
        typeof(CallSession),
    ];

    /// <summary>
    /// Members a report may read even though no production code assigns them, because the value is produced by
    /// something other than an assignment in this repository.
    /// </summary>
    private static readonly HashSet<string> _writtenElsewhere = new(StringComparer.Ordinal)
    {
        // CatalogItem identity, assigned by the shared catalog infrastructure through the base type.
        "ItemId",
    };

    [Fact]
    public void EveryPersistedFieldAReportReads_IsWrittenSomewhereOutsideTheReports()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var reportSources = EnumerateSources(Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.ContactCenter", "Reports"))
            .Select(File.ReadAllText)
            .ToArray();

        Assert.NotEmpty(reportSources);

        var declaringSources = _reportedModels
            .Select(model => $"{model.Name}.cs")
            .ToHashSet(StringComparer.Ordinal);

        // The model's own file is excluded: a property initializer there proves only that the field has a default,
        // not that anything ever puts a value in it.
        var producerSources = EnumerateSources(Path.Combine(repositoryRoot, "src"))
            .Where(file => !IsUnderReports(file) && !declaringSources.Contains(Path.GetFileName(file)))
            .Select(File.ReadAllText)
            .ToArray();

        // Act
        var unwritten = new List<string>();

        foreach (var model in _reportedModels)
        {
            foreach (var property in GetPersistedProperties(model))
            {
                if (_writtenElsewhere.Contains(property.Name))
                {
                    continue;
                }

                var read = MemberRead(property.Name);

                if (!reportSources.Any(source => read.IsMatch(source)))
                {
                    continue;
                }

                var written = MemberWritten(property.Name);

                if (!producerSources.Any(source => written.IsMatch(source)))
                {
                    unwritten.Add($"{model.Name}.{property.Name}");
                }
            }
        }

        // Assert
        Assert.True(
            unwritten.Count == 0,
            Describe(
                "Reports read persisted fields that no production code outside the reports ever writes. The report " +
                "will render an empty table or a zero on every tenant, and a reader cannot tell that apart from a " +
                "real measurement.",
                "Either write the field from the capability that owns it, or remove the report and the field.",
                unwritten));
    }

    private static IEnumerable<PropertyInfo> GetPersistedProperties(Type model)
    {
        return model
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.SetMethod is not null && property.SetMethod.IsPublic);
    }

    private static bool IsUnderReports(string file)
    {
        return file.Contains(
            $"{Path.DirectorySeparatorChar}CrestApps.OrchardCore.ContactCenter{Path.DirectorySeparatorChar}Reports{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateSources(string root)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var directory = Path.GetDirectoryName(file) ?? string.Empty;

            if (directory.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                directory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            yield return file;
        }
    }

    // A read of the member off an instance, which is how every report reaches persisted state.
    private static Regex MemberRead(string name)
        => new($@"\.{Regex.Escape(name)}\b(?!\s*=[^=])", RegexOptions.None, TimeSpan.FromSeconds(5));

    // An assignment - qualified ("record.Member = ") or in an object initializer ("Member = ") - or a mutation of a
    // collection member, which is how the projectors fill the list-valued fields.
    private static Regex MemberWritten(string name)
    {
        var escaped = Regex.Escape(name);

        return new Regex(
            $@"(?<![\w])(?:\w+\s*\.\s*)?{escaped}\s*(=(?!=)|\.\s*(Add|AddRange|Insert|Clear|Remove)\s*\()",
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(5));
    }

    private static string Describe(string problem, string remedy, IReadOnlyCollection<string> offenders)
    {
        var builder = new StringBuilder(problem)
            .AppendLine()
            .AppendLine()
            .AppendLine(remedy)
            .AppendLine();

        foreach (var offender in offenders)
        {
            builder.Append("  - ").AppendLine(offender);
        }

        return builder.ToString();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }
}
