using CrestApps.OrchardCore.Transactions.Core.Models;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Issues short sequential document numbers per site, for example <c>R-1001</c>, <c>R-1002</c>, for receipts.
/// </summary>
/// <remarks>
/// The last number of each series is kept in one document, read and saved in the caller's own unit of work. A
/// distributed lock is taken before reading it and held until that unit of work has been saved, so two nodes or two
/// requests never read the same last number. Using the caller's session rather than a separate one matters on SQLite,
/// where a second connection writing while the first holds its write transaction would wait on it forever. A number
/// taken by work that then fails is not reused, so a failed payment can leave a gap.
/// </remarks>
public sealed class SequentialFinancialDocumentNumberGenerator : IFinancialDocumentNumberGenerator
{
    /// <summary>
    /// The first number issued in a series.
    /// </summary>
    public const long FirstNumber = 1001;

    private const string LockKey = "CrestApps.Transactions.FinancialDocumentNumber";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;

    private ILocker _locker;

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialFinancialDocumentNumberGenerator"/> class.
    /// </summary>
    /// <param name="session">The caller's session, in which the last numbers are read and saved.</param>
    /// <param name="distributedLock">The lock that keeps two issuers from reading the same last number.</param>
    public SequentialFinancialDocumentNumberGenerator(ISession session, IDistributedLock distributedLock)
    {
        _session = session;
        _distributedLock = distributedLock;
    }

    /// <inheritdoc/>
    public async Task<FinancialDocumentNumber> GenerateAsync(FinancialDocumentNumberRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await HoldLockAsync();

        var sequences = await _session.Query<FinancialDocumentSequences>(collection: TransactionsConstants.CollectionName).FirstOrDefaultAsync(cancellationToken)
            ?? new FinancialDocumentSequences();

        var series = string.IsNullOrEmpty(request.Series) ? request.Kind.ToString() : $"{request.Kind}:{request.Series}";
        var next = sequences.Values.TryGetValue(series, out var last) ? last + 1 : FirstNumber;

        sequences.Values[series] = next;

        await _session.SaveAsync(sequences, checkConcurrency: true, collection: TransactionsConstants.CollectionName, cancellationToken);

        if (ShellScope.Current is null)
        {
            // Outside a shell scope there is no unit of work to wait for: save now and let the next issuer in.
            await _session.SaveChangesAsync(cancellationToken);
            await ReleaseLockAsync();
        }

        return new FinancialDocumentNumber(next, Format(request.Kind, next));
    }

    /// <summary>
    /// Formats a document number for display, with a prefix that tells the kinds apart.
    /// </summary>
    /// <param name="kind">The kind of document.</param>
    /// <param name="sequence">The number in its series.</param>
    public static string Format(FinancialDocumentKind kind, long sequence)
        => kind switch
        {
            FinancialDocumentKind.Receipt => $"R-{sequence}",
            FinancialDocumentKind.Invoice => $"INV-{sequence}",
            FinancialDocumentKind.CreditNote => $"CN-{sequence}",
            FinancialDocumentKind.RefundDocument => $"RF-{sequence}",
            _ => sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

    private async Task HoldLockAsync()
    {
        if (_locker is not null)
        {
            return;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            throw new TimeoutException("Another process is issuing a document number. Try again in a moment.");
        }

        _locker = locker;

        // Released once this unit of work has been saved, so the next issuer reads the number saved here. If the
        // work fails, the lock simply expires.
        if (ShellScope.Current is not null)
        {
            ShellScope.AddDeferredTask(_ => ReleaseLockAsync());
        }
    }

    private async Task ReleaseLockAsync()
    {
        var locker = _locker;

        _locker = null;

        if (locker is not null)
        {
            await locker.DisposeAsync();
        }
    }
}
