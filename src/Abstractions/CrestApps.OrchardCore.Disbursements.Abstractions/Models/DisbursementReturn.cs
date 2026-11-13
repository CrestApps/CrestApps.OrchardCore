namespace CrestApps.OrchardCore.Disbursements.Models;

/// <summary>
/// A payment the bank sent back, read from its return file.
/// </summary>
/// <param name="TraceNumber">The trace number of the original entry.</param>
/// <param name="Code">The return reason code, such as <c>R01</c>.</param>
/// <param name="Description">What the code means, or the bank's text.</param>
public sealed record DisbursementReturn(string TraceNumber, string Code, string Description);
