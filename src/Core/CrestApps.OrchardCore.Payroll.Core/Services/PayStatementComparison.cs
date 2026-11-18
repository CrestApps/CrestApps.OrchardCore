using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// A change of one kind of pay between two statements: a pay code or a tax.
/// </summary>
/// <param name="Key">The pay code's code or the tax code.</param>
/// <param name="Description">What it is.</param>
/// <param name="Type">The kind of line.</param>
/// <param name="Previous">The amount on the earlier statement.</param>
/// <param name="Current">The amount on this one.</param>
/// <param name="Change">The difference.</param>
public sealed record PayStatementLineChange(string Key, string Description, PayLineType Type, decimal Previous, decimal Current, decimal Change);

/// <summary>
/// Compares the pay of two statements or two sets of statements, line kind by line kind: what an assistant needs to say
/// what changed since the last pay.
/// </summary>
public static class PayStatementComparison
{
    /// <summary>
    /// Compares the lines of statements.
    /// </summary>
    /// <param name="current">The current statements, such as a person's in a run.</param>
    /// <param name="previous">The earlier statements.</param>
    /// <param name="codes">The pay codes, by id.</param>
    /// <returns>Each kind of line whose amount changed, the largest change first.</returns>
    public static IReadOnlyList<PayStatementLineChange> Compare(IEnumerable<PayStatement> current, IEnumerable<PayStatement> previous, IReadOnlyDictionary<string, PayCode> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);

        var now = Sum(current, codes);
        var before = Sum(previous, codes);

        return [.. now.Keys.Union(before.Keys)
            .Select(key =>
            {
                var (description, type, amount) = now.GetValueOrDefault(key);
                var (earlierDescription, earlierType, earlier) = before.GetValueOrDefault(key);

                return new PayStatementLineChange(key.Key, description ?? earlierDescription, description is null ? earlierType : type, earlier, amount, amount - earlier);
            })
            .Where(change => change.Change != 0)
            .OrderByDescending(change => Math.Abs(change.Change))
            .ThenBy(change => change.Key, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Gets whether a statement counts as pay: not a reversing statement and not voided.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns><see langword="true"/> when it counts.</returns>
    public static bool Counts(PayStatement statement)
        => statement is not null && statement.VoidOf is null && statement.Status != PayStatementStatus.Voided;

    // The amount of each kind of line, keyed by its code and whether the employer pays it.
    private static Dictionary<(string Key, bool Employer), (string Description, PayLineType Type, decimal Amount)> Sum(IEnumerable<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes)
    {
        var sums = new Dictionary<(string, bool), (string, PayLineType, decimal)>();

        foreach (var line in (statements ?? []).Where(Counts).SelectMany(statement => statement.Lines))
        {
            var code = line.PayCodeId is not null && codes.TryGetValue(line.PayCodeId, out var found) ? found.Code : null;
            var key = (code ?? line.TaxCode ?? line.Description ?? line.Type.ToString(), line.Type == PayLineType.Tax && line.Payer == PayLinePayer.Employer);
            var (description, type, amount) = sums.GetValueOrDefault(key);

            sums[key] = (description ?? line.Description, line.Type, amount + line.Amount);
        }

        return sums;
    }
}
