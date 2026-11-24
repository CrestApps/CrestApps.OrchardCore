using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Payroll.Core.TaxForms;

/// <summary>
/// The sums over pay statements that tax forms are worked out from, whatever the country.
/// </summary>
public static class PayrollFilingSums
{
    /// <summary>Gets the wages of a tax category of statements, from their pay code lines.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="codes">The pay codes.</param>
    /// <param name="category">The tax category, such as IncomeTax.</param>
    /// <returns>The wages.</returns>
    public static decimal Wages(IEnumerable<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes, string category)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(codes);

        return statements.Sum(statement =>
        {
            // A reversal's lines are the opposite of the statement it voids: worked out as that statement, then taken off.
            var sign = statement.VoidOf is null ? 1 : -1;

            return sign * PayCodeTaxMatrix.WagesFor(category, statement.Lines
                .Where(line => line.Type != PayLineType.Tax && line.PayCodeId is not null && codes.ContainsKey(line.PayCodeId))
                .Select(line => new PayCodeAmount(codes[line.PayCodeId], sign * line.Amount)));
        });
    }

    /// <summary>Gets the amount of the taxes whose codes match any of the patterns, such as US-FIT or CA-*-PIT.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="patterns">The tax codes; * matches any characters.</param>
    /// <returns>The amount.</returns>
    public static decimal Tax(IEnumerable<PayStatement> statements, params string[] patterns)
        => TaxLines(statements, patterns).Sum(line => line.Amount);

    /// <summary>Gets the wages the taxes whose codes match any of the patterns were worked out on.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="patterns">The tax codes; * matches any characters.</param>
    /// <returns>The wages.</returns>
    public static decimal Taxable(IEnumerable<PayStatement> statements, params string[] patterns)
        => TaxLines(statements, patterns).Sum(line => line.TaxableWages ?? 0);

    /// <summary>Gets whether the statements have a tax line whose code matches any of the patterns.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="patterns">The tax codes; * matches any characters.</param>
    /// <returns>Whether they have.</returns>
    public static bool HasTax(IEnumerable<PayStatement> statements, params string[] patterns)
        => TaxLines(statements, patterns).Any();

    /// <summary>Gets the earnings and imputed income of pay codes of some kinds, such as Tips.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="codes">The pay codes.</param>
    /// <param name="kinds">The kinds.</param>
    /// <returns>The amount.</returns>
    public static decimal OfKind(IEnumerable<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes, params string[] kinds)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(codes);

        return statements.SelectMany(statement => statement.Lines)
            .Where(line => line.Type is PayLineType.Earning or PayLineType.ImputedIncome
                && line.PayCodeId is not null
                && codes.TryGetValue(line.PayCodeId, out var code)
                && kinds.Contains(code.Kind, StringComparer.OrdinalIgnoreCase))
            .Sum(line => line.Amount);
    }

    /// <summary>
    /// Gets the amounts of the lines whose pay code reports to a form box (Pay Codes, Form boxes). A reversal's lines are
    /// negative, so a voided amount cancels.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="codes">The pay codes.</param>
    /// <param name="formBox">The form box's stored name, such as W2:12D.</param>
    /// <returns>The amount.</returns>
    public static decimal ReportedTo(IEnumerable<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes, string formBox)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(codes);

        return statements.SelectMany(statement => statement.Lines)
            .Where(line => line.PayCodeId is not null
                && codes.TryGetValue(line.PayCodeId, out var code)
                && code.GetPayrollSettings().FormBoxes.Contains(formBox, StringComparer.OrdinalIgnoreCase))
            .Sum(line => line.Amount);
    }

    /// <summary>Gets the deposits made to a jurisdiction, of one group of taxes or of all.</summary>
    /// <param name="deposits">The deposits.</param>
    /// <param name="jurisdiction">The jurisdiction, such as US.</param>
    /// <param name="group">The group, or <see langword="null"/> for all.</param>
    /// <returns>The amount.</returns>
    public static decimal Deposited(IEnumerable<TaxDeposit> deposits, string jurisdiction, string group = null)
    {
        ArgumentNullException.ThrowIfNull(deposits);

        return deposits
            .Where(deposit => string.Equals(deposit.Jurisdiction, jurisdiction, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrEmpty(group) || string.Equals(deposit.Group, group, StringComparison.OrdinalIgnoreCase)))
            .Sum(deposit => deposit.Amount);
    }

    /// <summary>
    /// Gets the number of people paid for a pay period that includes a day of the last month of the period, such as the
    /// 12th for Form 941 box 1.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="to">The last day of the filing's period.</param>
    /// <param name="day">The day of the month.</param>
    /// <returns>The number of people.</returns>
    public static int Headcount(IEnumerable<PayStatement> statements, DateOnly to, int day)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var on = new DateOnly(to.Year, to.Month, Math.Clamp(day, 1, DateTime.DaysInMonth(to.Year, to.Month)));

        return statements
            .Where(statement => statement.VoidOf is null && statement.PeriodStart <= on && statement.PeriodEnd >= on && statement.Gross > 0)
            .Select(statement => statement.StaffMemberId)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    /// <summary>
    /// Gets the jurisdiction one level under a parent that statements paid tax in, such as CA-ON under CA, from their
    /// tax lines: the last one wins.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="parent">The parent jurisdiction, such as CA.</param>
    /// <returns>The jurisdiction, in capitals, or <see langword="null"/>.</returns>
    public static string JurisdictionUnder(IEnumerable<PayStatement> statements, string parent)
    {
        ArgumentNullException.ThrowIfNull(statements);

        if (string.IsNullOrEmpty(parent))
        {
            return null;
        }

        var prefix = parent + "-";

        return statements.SelectMany(statement => statement.Lines)
            .Where(line => line.Type == PayLineType.Tax
                && line.Jurisdiction is { } jurisdiction
                && jurisdiction.Length > prefix.Length
                && jurisdiction.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && jurisdiction.IndexOf('-', prefix.Length) < 0)
            .Select(line => line.Jurisdiction.ToUpperInvariant())
            .LastOrDefault();
    }

    /// <summary>
    /// Splits a person's statements by the jurisdiction under a parent they paid tax in, such as the province of
    /// employment for a T4 slip each. A statement with no such jurisdiction of its own (one with no provincial tax line,
    /// say) counts with the latest jurisdiction before it, else the first one after it.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="parent">The parent jurisdiction, such as CA.</param>
    /// <returns>The statements of each jurisdiction, in the order the person was paid there.</returns>
    public static IReadOnlyList<(string Jurisdiction, IReadOnlyList<PayStatement> Statements)> ByJurisdictionUnder(IReadOnlyList<PayStatement> statements, string parent)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var ordered = statements.OrderBy(statement => statement.PayDate).ToList();
        var jurisdictions = ordered.Select(statement => JurisdictionUnder([statement], parent)).ToList();
        var current = jurisdictions.FirstOrDefault(jurisdiction => jurisdiction is not null);
        var groups = new List<(string Jurisdiction, List<PayStatement> Statements)>();

        for (var i = 0; i < ordered.Count; i++)
        {
            current = jurisdictions[i] ?? current;

            var group = groups.FindIndex(candidate => candidate.Jurisdiction == current);

            if (group < 0)
            {
                groups.Add((current, [ordered[i]]));
            }
            else
            {
                groups[group].Statements.Add(ordered[i]);
            }
        }

        return [.. groups.Select(group => (group.Jurisdiction, (IReadOnlyList<PayStatement>)group.Statements))];
    }

    /// <summary>
    /// Gets the state and local amounts of one person's statements of a year (a W-2's boxes 15 to 20): for each
    /// jurisdiction below the country of an income tax the person paid, the wages it was worked out on and the tax, the
    /// employer's account id from its registration with the jurisdiction (the one for income tax first) and, for a local
    /// jurisdiction, the name printed, the code's part after the region (DETROIT for US-MI-DETROIT). States come first,
    /// each followed by its local jurisdictions.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="employer">The employer, for its account ids; or <see langword="null"/>.</param>
    /// <returns>The amounts.</returns>
    public static IReadOnlyList<PayrollFilingJurisdictionAmount> JurisdictionAmounts(IReadOnlyList<PayStatement> statements, Employer employer)
    {
        ArgumentNullException.ThrowIfNull(statements);

        return [.. statements
            .SelectMany(statement => statement.Lines)
            .Where(line => line.Type == PayLineType.Tax
                && line.Payer == PayLinePayer.Employee
                && string.Equals(line.TaxCategory, IncomeTaxCategories.IncomeTax, StringComparison.Ordinal)
                && line.Jurisdiction?.Contains('-', StringComparison.Ordinal) == true)
            .GroupBy(line => line.Jurisdiction.ToUpperInvariant(), StringComparer.Ordinal)
            .Select(group =>
            {
                var registrations = (employer?.Registrations ?? []).Where(registration => string.Equals(registration.Jurisdiction, group.Key, StringComparison.OrdinalIgnoreCase)).ToList();
                var registration = registrations.FirstOrDefault(candidate => (candidate.Categories ?? []).Contains(IncomeTaxCategories.IncomeTax, StringComparer.OrdinalIgnoreCase))
                    ?? registrations.FirstOrDefault(candidate => candidate.Categories is not { Count: > 0 })
                    ?? registrations.FirstOrDefault();
                var amount = new PayrollFilingJurisdictionAmount
                {
                    Jurisdiction = group.Key,
                    AccountId = registration?.AccountId,
                    Wages = group.Sum(line => line.TaxableWages ?? 0),
                    Tax = group.Sum(line => line.Amount),
                };

                if (amount.IsLocal)
                {
                    amount.Name = string.Join('-', group.Key.Split('-').Skip(2));
                }

                return amount;
            })
            .Where(amount => amount.Wages != 0 || amount.Tax != 0)
            .OrderBy(amount => amount.Region, StringComparer.Ordinal)
            .ThenBy(amount => amount.IsLocal)
            .ThenBy(amount => amount.Jurisdiction, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Gets the wages or the income tax of the employee's income tax lines of the jurisdictions below the country, such
    /// as the states' (a W-3's boxes 16 and 17) or the local ones' (18 and 19).
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="local">Whether the local jurisdictions (a county, a city) rather than the regions (a state).</param>
    /// <param name="tax">Whether the tax rather than the wages.</param>
    /// <returns>The amount.</returns>
    public static decimal BelowCountry(IEnumerable<PayStatement> statements, bool local, bool tax)
    {
        ArgumentNullException.ThrowIfNull(statements);

        return statements.SelectMany(statement => statement.Lines)
            .Where(line => line.Type == PayLineType.Tax
                && line.Payer == PayLinePayer.Employee
                && string.Equals(line.TaxCategory, IncomeTaxCategories.IncomeTax, StringComparison.Ordinal)
                && line.Jurisdiction?.Contains('-', StringComparison.Ordinal) == true
                && (line.Jurisdiction.Count(character => character == '-') >= 2) == local)
            .Sum(line => tax ? line.Amount : line.TaxableWages ?? 0);
    }

    /// <summary>Gets whether a tax code matches a pattern, ignoring case; * matches any characters.</summary>
    /// <param name="code">The tax code.</param>
    /// <param name="pattern">The pattern.</param>
    /// <returns>Whether it matches.</returns>
    public static bool Matches(string code, string pattern)
    {
        if (code is null || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(code, pattern, StringComparison.OrdinalIgnoreCase);
        }

        var parts = pattern.Split('*');
        var position = 0;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (part.Length == 0)
            {
                continue;
            }

            if (i == 0)
            {
                if (!code.StartsWith(part, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                position = part.Length;

                continue;
            }

            if (i == parts.Length - 1)
            {
                return code.Length - part.Length >= position && code.EndsWith(part, StringComparison.OrdinalIgnoreCase);
            }

            var found = code.IndexOf(part, position, StringComparison.OrdinalIgnoreCase);

            if (found < 0)
            {
                return false;
            }

            position = found + part.Length;
        }

        return true;
    }

    private static IEnumerable<PayStatementLine> TaxLines(IEnumerable<PayStatement> statements, string[] patterns)
    {
        ArgumentNullException.ThrowIfNull(statements);

        return statements.SelectMany(statement => statement.Lines)
            .Where(line => line.Type == PayLineType.Tax && patterns.Any(pattern => Matches(line.TaxCode, pattern)));
    }
}
