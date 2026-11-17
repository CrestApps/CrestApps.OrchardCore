using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Turns a statement's lines into balanced journal entries:
/// <list type="bullet">
/// <item>an earning debits its expense and credits net pay owed;</item>
/// <item>a deduction debits net pay owed and credits its liability;</item>
/// <item>an employee's tax debits net pay owed and credits the tax's liability;</item>
/// <item>an employer's tax debits its expense and credits its liability;</item>
/// <item>an employer contribution debits its expense and credits its liability;</item>
/// <item>a reimbursement debits its expense and credits net pay owed.</item>
/// </list>
/// Imputed income and memo lines move no money and have no entry. A reversing statement's negative amounts swap the
/// sides, so a void reverses its entries. The accounts come from the settings: a pay code's or tax's mapping, else the
/// default of its kind.
/// </summary>
public static class PayrollJournal
{
    /// <summary>
    /// Gets the entries of a statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="codes">The pay codes, by id.</param>
    /// <param name="settings">The journal settings.</param>
    /// <param name="ledgerCodeOf">The ledger code of a team, or <see langword="null"/>.</param>
    /// <returns>The entries, two a line.</returns>
    public static IEnumerable<PayrollJournalEntry> EntriesFor(PayStatement statement, IReadOnlyDictionary<string, PayCode> codes, PayrollJournalSettings settings, Func<string, string> ledgerCodeOf = null)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(codes);

        settings ??= new PayrollJournalSettings();

        foreach (var line in statement.Lines)
        {
            if (line.Amount == 0)
            {
                continue;
            }

            var code = line.PayCodeId is not null && codes.TryGetValue(line.PayCodeId, out var found) ? found : null;
            var source = line.Type == PayLineType.Tax ? line.TaxCode : code?.Code;
            var mapping = settings.Mappings.FirstOrDefault(candidate => string.Equals(candidate.Key, source, StringComparison.OrdinalIgnoreCase));

            (string Debit, string Credit)? accounts = line.Type switch
            {
                PayLineType.Earning => (settings.WageExpenseAccount, settings.NetPayAccount),
                PayLineType.Deduction => (settings.NetPayAccount, settings.DeductionLiabilityAccount),
                PayLineType.Tax when line.Payer == PayLinePayer.Employer => (settings.EmployerTaxExpenseAccount, settings.TaxLiabilityAccount),
                PayLineType.Tax => (settings.NetPayAccount, settings.TaxLiabilityAccount),
                PayLineType.EmployerContribution => (settings.ContributionExpenseAccount, settings.ContributionLiabilityAccount),
                PayLineType.Reimbursement => (settings.ReimbursementExpenseAccount, settings.NetPayAccount),
                _ => null,
            };

            if (accounts is not { } pair)
            {
                continue;
            }

            var debit = string.IsNullOrWhiteSpace(mapping?.DebitAccount) ? pair.Debit : mapping.DebitAccount.Trim();
            var credit = string.IsNullOrWhiteSpace(mapping?.CreditAccount) ? pair.Credit : mapping.CreditAccount.Trim();
            var department = line.Allocation?.DepartmentTeamId is { Length: > 0 } teamId ? ledgerCodeOf?.Invoke(teamId) : null;

            // A negative amount (a reversal) swaps the sides.
            if (line.Amount < 0)
            {
                (debit, credit) = (credit, debit);
            }

            var amount = Math.Abs(line.Amount);

            yield return new PayrollJournalEntry(statement.ItemId, statement.Number, statement.RunId, statement.EmployerId, statement.PayDate, debit, amount, 0, department, source, line.Description, statement.Currency);
            yield return new PayrollJournalEntry(statement.ItemId, statement.Number, statement.RunId, statement.EmployerId, statement.PayDate, credit, 0, amount, department, source, line.Description, statement.Currency);
        }
    }
}
