namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Why a credit note cannot be issued as it is.
/// </summary>
/// <param name="LineId">The invoice line the problem is about, or <see langword="null"/> for the whole credit note.</param>
/// <param name="Code">What is wrong: <c>NothingCredited</c>, <c>UnknownLine</c>, <c>LineExceeded</c> or <c>TotalExceeded</c>.</param>
/// <param name="Allowed">The most that may still be credited, when the problem is an amount.</param>
public sealed record CreditNoteError(string LineId, string Code, decimal? Allowed);
