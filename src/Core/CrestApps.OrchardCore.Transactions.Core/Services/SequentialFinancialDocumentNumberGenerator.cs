using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Transactions.Core.Models;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Issues the numbers of financial documents, for example <c>R-1001</c>, <c>R-1002</c> for receipts, from the shared
/// number series: each <see cref="FinancialDocumentKind"/> is a registered series (see <see cref="FinancialDocumentSeries"/>),
/// so a site chooses the format of each in the number series settings.
/// </summary>
/// <remarks>
/// The numbers are issued by the shared <see cref="INumberSeries"/>, under its lock and in the caller's unit of work, so
/// a number taken by work that then fails is given to the next issuer. Before the shared series existed, the last number
/// of each kind was kept in a <see cref="FinancialDocumentSequences"/> document. A series used for the first time
/// carries on after the number stored there, so no number is issued twice.
/// </remarks>
public sealed class SequentialFinancialDocumentNumberGenerator : IFinancialDocumentNumberGenerator
{
    /// <summary>
    /// The first number issued in a series, unless the site chose another one.
    /// </summary>
    public const long FirstNumber = FinancialDocumentSeries.FirstNumber;

    private readonly INumberSeries _numberSeries;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialFinancialDocumentNumberGenerator"/> class.
    /// </summary>
    /// <param name="numberSeries">The shared number series the numbers come from.</param>
    /// <param name="session">The session the numbers issued before the shared series existed are read from.</param>
    public SequentialFinancialDocumentNumberGenerator(INumberSeries numberSeries, ISession session)
    {
        _numberSeries = numberSeries;
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<FinancialDocumentNumber> GenerateAsync(FinancialDocumentNumberRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var legacyKey = string.IsNullOrEmpty(request.Series) ? request.Kind.ToString() : $"{request.Kind}:{request.Series}";

        var number = await _numberSeries.NextAsync(
            FinancialDocumentSeries.GetName(request.Kind),
            new NumberSeriesRequest
            {
                Scope = request.Series,
                SeedAsync = token => GetLastLegacyNumberAsync(legacyKey, token),
            },
            cancellationToken);

        return new FinancialDocumentNumber(number.Sequence, number.Text);
    }

    private async Task<long> GetLastLegacyNumberAsync(string legacyKey, CancellationToken cancellationToken)
    {
        var sequences = await _session.Query<FinancialDocumentSequences>(collection: TransactionsConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

        return sequences is not null && sequences.Values.TryGetValue(legacyKey, out var last) ? last : 0;
    }
}
