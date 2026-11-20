namespace CrestApps.OrchardCore.Banking.Disbursements.Models;

/// <summary>
/// A notice of change from the payee's bank: the payment went through, and the account details must be corrected for
/// the next one.
/// </summary>
/// <param name="TraceNumber">The trace number of the original entry.</param>
/// <param name="Code">The change code, such as <c>C01</c> (account number) or <c>C02</c> (routing number).</param>
/// <param name="CorrectedData">The corrected data the bank sent.</param>
public sealed record DisbursementChangeNotice(string TraceNumber, string Code, string CorrectedData);
