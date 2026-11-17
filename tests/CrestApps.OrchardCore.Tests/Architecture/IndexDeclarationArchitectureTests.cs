using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Every YesSql index provider declares what is true of its rows (OrchardCore PR #20201), and declares nothing false.
/// </summary>
/// <remarks>
/// <para>
/// <c>ExclusiveToDocumentType()</c> lets a count read the index table without joining <c>Document</c>. It is only true
/// while one document type writes the index, so an index type produced by two providers would be counted wrong.
/// </para>
/// <para>
/// <c>SingleRecordPerDocument()</c> lets a query skip grouping by document. On a map that returns a row per item of a
/// list, saving a document with two items throws, and rows already in the table return their document twice. The
/// list below names the index types whose map returns several rows, so a new provider has to be placed on one side.
/// </para>
/// </remarks>
public sealed partial class IndexDeclarationArchitectureTests
{
    /// <summary>
    /// The index types whose map returns a row per item of a list, and so must not declare a single row per document.
    /// </summary>
    private static readonly HashSet<string> _multipleRowIndexes = new(StringComparer.Ordinal)
    {
        "AccountMemberIndex",
        "AgentAllowedQueueIndex",
        "AgentQueueMembershipIndex",
        "AppointmentResourceIndex",
        "CalendarEventAttendeeIndex",
        "CalendarShareIndex",
        "CallSessionLegIndex",
        "DialerDecisionAgentIndex",
        "LeadImportIndex",
        "OmnichannelContactPhoneNumberIndex",
        "PayStatementLineIndex",
        "ProductAttributeIndex",
        "ProductBundleIndex",
        "ProductSkuIndex",
        "StaffCapabilityIndex",
        "StaffTeamMembershipIndex",
        "TelephonyUserConnectionIndex",
        "TimeOffPolicyAssignmentIndex",
        "VendorUserIndex",
        "VisitWorkerIndex",
        "WorkOrderAssetIndex",
    };

    [Fact]
    public void EveryIndexRegistration_DeclaresWhatItsRowsHold()
    {
        // Arrange
        var registrations = FindRegistrations(ProjectReferenceRulesTests.FindRepositoryRoot());
        var violations = new List<string>();

        // Act
        foreach (var registration in registrations)
        {
            if (!registration.Exclusive)
            {
                violations.Add($"{registration.Location}: '{registration.IndexType}' doesn't declare ExclusiveToDocumentType().");
            }

            var multipleRows = _multipleRowIndexes.Contains(registration.IndexType);

            if (multipleRows && registration.SingleRecord)
            {
                violations.Add($"{registration.Location}: '{registration.IndexType}' maps several rows per document but declares SingleRecordPerDocument().");
            }
            else if (!multipleRows && !registration.SingleRecord)
            {
                violations.Add(
                    $"{registration.Location}: '{registration.IndexType}' doesn't declare SingleRecordPerDocument(). Declare it when the map " +
                    "returns at most one row per document, or add the index type to the list of multi-row indexes of this test.");
            }
        }

        // Assert
        Assert.NotEmpty(registrations);
        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    [Fact]
    public void EveryIndexType_IsProducedByOneProvider()
    {
        // An index declared exclusive to a document type holds that type's rows only. A second provider would write
        // rows of another type into it, and while that provider isn't registered, YesSql would count them too.
        var duplicates = FindRegistrations(ProjectReferenceRulesTests.FindRepositoryRoot())
            .GroupBy(registration => registration.IndexType, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"'{group.Key}' is produced in {string.Join(", ", group.Select(registration => registration.Location))}.")
            .ToList();

        Assert.True(duplicates.Count == 0, BuildMessage(duplicates));
    }

    [Fact]
    public void TheMultipleRowList_NamesExistingIndexes()
    {
        // A stale entry would exempt a future index of the same name without anyone deciding it.
        var indexTypes = FindRegistrations(ProjectReferenceRulesTests.FindRepositoryRoot())
            .Select(registration => registration.IndexType)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(_multipleRowIndexes, index => Assert.Contains(index, indexTypes));
    }

    [Theory]
    [InlineData("context.For<AIndex>()\n    .ExclusiveToDocumentType()\n    .SingleRecordPerDocument()\n    .Map(x => new AIndex());", true, true)]
    [InlineData("context\n    .For<AIndex>()\n    .ExclusiveToDocumentType()\n    .When(x => x.Ok)\n    .Map(x => new AIndex());", true, false)]
    [InlineData("context.For<AIndex>().Map(x => new AIndex());", false, false)]
    public void TheScan_ReadsTheDeclarationsOfARegistration(string describe, bool exclusive, bool singleRecord)
    {
        // A guard whose only evidence is that it passes is a guard nobody has checked.
        var source = $"public sealed class AIndexProvider : IndexProvider<A>\n{{\n    public override void Describe(DescribeContext<A> context)\n    {{\n        {describe}\n    }}\n}}";

        var registration = Assert.Single(ReadRegistrations("A.cs", source));

        Assert.Equal("AIndex", registration.IndexType);
        Assert.Equal(exclusive, registration.Exclusive);
        Assert.Equal(singleRecord, registration.SingleRecord);
    }

    private static List<Registration> FindRegistrations(string repositoryRoot)
    {
        var registrations = new List<Registration>();
        var separator = Path.DirectorySeparatorChar;

        foreach (var file in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{separator}obj{separator}", StringComparison.Ordinal) ||
                file.Contains($"{separator}bin{separator}", StringComparison.Ordinal) ||
                file.Contains($"{separator}node_modules{separator}", StringComparison.Ordinal))
            {
                continue;
            }

            registrations.AddRange(ReadRegistrations(Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/'), File.ReadAllText(file)));
        }

        return registrations;
    }

    private static IEnumerable<Registration> ReadRegistrations(string relativePath, string source)
    {
        if (!ProviderPattern().IsMatch(source))
        {
            yield break;
        }

        foreach (Match match in ForPattern().Matches(source))
        {
            // The declarations sit between For<TIndex>() and the call that maps the rows.
            var rest = source[(match.Index + match.Length)..];
            var mapIndex = rest.IndexOf(".Map(", StringComparison.Ordinal);
            var chain = mapIndex < 0 ? string.Empty : rest[..mapIndex];
            var lineNumber = source.AsSpan(0, match.Index).Count('\n') + 1;

            yield return new Registration(
                match.Groups["index"].Value,
                $"{relativePath}:{lineNumber}",
                chain.Contains(".ExclusiveToDocumentType()", StringComparison.Ordinal),
                chain.Contains(".SingleRecordPerDocument()", StringComparison.Ordinal));
        }
    }

    private static string BuildMessage(IReadOnlyList<string> lines)
    {
        var message = new StringBuilder().AppendLine();

        foreach (var line in lines.Order(StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(line);
        }

        return message.ToString();
    }

    [GeneratedRegex(@":\s*(Scoped)?IndexProvider<")]
    private static partial Regex ProviderPattern();

    [GeneratedRegex(@"\.For<(?<index>\w+)>\(\)")]
    private static partial Regex ForPattern();

    private sealed record Registration(string IndexType, string Location, bool Exclusive, bool SingleRecord);
}
