using CrestApps.OrchardCore.Transactions.FinancialDocuments;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Takes the next number of a document series that no document holds yet. A document imported from another site keeps
/// its number, which the series here may not have reached; that number is passed over rather than given twice.
/// </summary>
internal static class UnusedDocumentNumbers
{
    // A series is never expected to run into more imported numbers than this in a row.
    private const int MaxAttempts = 1000;

    /// <summary>
    /// Returns the next unused number, or <see langword="null"/> when the series keeps giving numbers already held.
    /// </summary>
    public static async Task<FinancialDocumentNumber> NextAsync(
        IFinancialDocumentNumberGenerator generator,
        FinancialDocumentKind kind,
        Func<string, Task<bool>> isHeld,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var number = await generator.GenerateAsync(new FinancialDocumentNumberRequest(kind), cancellationToken);

            if (!await isHeld(number.PublicToken))
            {
                return number;
            }
        }

        return null;
    }
}
