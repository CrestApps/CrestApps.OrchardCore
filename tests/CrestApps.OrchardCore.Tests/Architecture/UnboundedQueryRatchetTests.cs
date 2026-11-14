using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A display driver, a controller or an endpoint runs on every request. When one of them lists a whole table, or every
/// record of a catalog, to count, sum or filter it in memory, it works on a new tenant and times out on one with years
/// of data (see CQ-4 in <c>docs/engineering/review-2026-11/04-code-quality.md</c>).
/// <para>
/// This counts the <c>ListAsync(</c> and <c>GetAllAsync(</c> calls in those files whose statement has no <c>Take(</c>.
/// The files that had such calls when the ratchet was set are listed with their count, which may only shrink. Count with
/// <c>CountAsync</c>, page with <c>Take</c>, pick one record with a picker, or look up a set of ids once.
/// </para>
/// </summary>
public sealed partial class UnboundedQueryRatchetTests
{
    private static readonly Lazy<Dictionary<string, int>> _actual = new(ScanSources);

    [Fact]
    public void NoRequestPath_AddsAnUnboundedQuery()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value,
            _known,
            "These drivers, controllers or endpoints load a whole query or catalog on a request path.",
            "Count with CountAsync, page with Take, pick a record with its picker, or load a set of ids once with IsIn.");
    }

    [Fact]
    public void EveryListedFile_StillHasItsUnboundedQueries()
    {
        Ratchet.AssertNothingStale(_actual.Value, _known, $"{nameof(UnboundedQueryRatchetTests)}.{nameof(_known)}");
    }

    [Theory]
    [InlineData("var all = await _session.QueryIndex<GeocodeRequestIndex>(collection: c).ListAsync();", 1)]
    [InlineData("var teams = await _teamManager.GetAllAsync(cancellationToken);", 1)]
    [InlineData("var page = await query.Skip(10).Take(20).ListAsync(token);", 0)]
    [InlineData("var page = await query\n    .Take(20)\n    .ListAsync(token);", 0)]
    [InlineData("var count = await query.CountAsync();", 0)]
    [InlineData("// var all = await query.ListAsync();", 0)]
    [InlineData("public static Task<IList<SelectListItem>> ListAsync(ITeamManager teamManager)", 0)]
    public void Count_FindsListCallsWithoutTake(string code, int expected)
    {
        // Act
        var actual = Count(code);

        // Assert
        Assert.Equal(expected, actual);
    }

    internal static bool IsRequestPathFile(string relativePath)
    {
        var name = Path.GetFileName(relativePath);
        var type = name[..name.IndexOf('.')];

        return type.EndsWith("Driver", StringComparison.Ordinal)
            || type.EndsWith("Controller", StringComparison.Ordinal)
            || type.EndsWith("Endpoints", StringComparison.Ordinal)
            || type.EndsWith("Endpoint", StringComparison.Ordinal);
    }

    private static Dictionary<string, int> ScanSources()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var scanned = 0;

        foreach (var file in Ratchet.EnumerateFiles("src"))
        {
            if (!IsRequestPathFile(file.RelativePath))
            {
                continue;
            }

            scanned++;

            var count = Count(Ratchet.ReadAllText(file));

            if (count > 0)
            {
                result[file.RelativePath] = count;
            }
        }

        Assert.True(scanned > 500, $"Only {scanned} drivers, controllers and endpoints were scanned; the scan no longer matches the source.");

        return result;
    }

    private static int Count(string source)
    {
        var code = CSharpSourceMask.CommentsAndLiterals(source);
        var count = 0;

        foreach (Match call in ListCall().Matches(code))
        {
            var statementStart = code.LastIndexOfAny([';', '{', '}'], call.Index) + 1;

            if (!Take().IsMatch(code.AsSpan(statementStart, call.Index - statementStart)))
            {
                count++;
            }
        }

        return count;
    }

    [GeneratedRegex(@"\.\s*(?:ListAsync|GetAllAsync)\s*\(")]
    private static partial Regex ListCall();

    [GeneratedRegex(@"\.\s*Take\s*\(")]
    private static partial Regex Take();
}
