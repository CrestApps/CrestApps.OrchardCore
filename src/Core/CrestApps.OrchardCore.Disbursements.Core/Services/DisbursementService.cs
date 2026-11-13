using System.Security.Claims;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Disbursements.Core.Nacha;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Disbursements.Core.Services;

/// <summary>
/// The default <see cref="IDisbursementService"/>.
/// </summary>
public sealed class DisbursementService : IDisbursementService
{
    private const int SettleBatchSize = 50;

    private readonly IDisbursementManager _manager;
    private readonly IDisbursementBatchManager _batches;
    private readonly IDisbursementBatchStore _batchStore;
    private readonly IDisbursementFileStore _files;
    private readonly IBankAccountManager _bankAccounts;
    private readonly IBankAccountService _bankAccountService;
    private readonly IEnumerable<IDisbursementObserver> _observers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementService"/> class.
    /// </summary>
    /// <param name="manager">The disbursements.</param>
    /// <param name="batches">The batches.</param>
    /// <param name="batchStore">The batch store.</param>
    /// <param name="files">The batch files.</param>
    /// <param name="bankAccounts">The bank accounts.</param>
    /// <param name="bankAccountService">The bank account rules.</param>
    /// <param name="providers">The providers.</param>
    /// <param name="observers">The observers of changes.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for who made a change.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementService(
        IDisbursementManager manager,
        IDisbursementBatchManager batches,
        IDisbursementBatchStore batchStore,
        IDisbursementFileStore files,
        IBankAccountManager bankAccounts,
        IBankAccountService bankAccountService,
        IEnumerable<IDisbursementProvider> providers,
        IEnumerable<IDisbursementObserver> observers,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<DisbursementService> stringLocalizer)
    {
        _manager = manager;
        _batches = batches;
        _batchStore = batchStore;
        _files = files;
        _bankAccounts = bankAccounts;
        _bankAccountService = bankAccountService;
        Providers = providers.ToArray();
        _observers = observers;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public IReadOnlyList<IDisbursementProvider> Providers { get; }

    /// <inheritdoc />
    public async Task<DisbursementResult> CreateAsync(DisbursementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? (string.IsNullOrEmpty(request.SourceKind) || string.IsNullOrEmpty(request.SourceId)
                ? null
                : string.Join(':', request.SourceKind, request.SourceId, request.Method, request.DestinationBankAccountId))
            : request.IdempotencyKey.Trim();

        if (key is null)
        {
            return new DisbursementResult { Errors = [S["Give the record the disbursement pays, or a key that identifies it."]] };
        }

        // Asking twice gives the same disbursement, so a retried pay run never pays twice.
        if (await _manager.FindByIdempotencyKeyAsync(key, cancellationToken) is { } existing)
        {
            return new DisbursementResult { Disbursement = existing, AlreadyExisted = true };
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Method) || !Providers.Any(provider => provider.Method == request.Method))
        {
            errors.Add(S["Choose how the money is sent."]);
        }

        if (string.IsNullOrWhiteSpace(request.PayeeName))
        {
            errors.Add(S["Enter who is paid."]);
        }

        if (request.IsPrenote)
        {
            if (request.Amount != 0)
            {
                errors.Add(S["A test entry (prenote) carries no amount."]);
            }
        }
        else if (request.Amount <= 0)
        {
            errors.Add(S["The amount must be more than zero."]);
        }

        var currency = request.Currency;
        var needsSource = request.Method is DisbursementsConstants.Methods.Ach or DisbursementsConstants.Methods.Check;
        BankAccount source = null;

        if (!string.IsNullOrEmpty(request.SourceBankAccountId))
        {
            source = await _bankAccounts.FindByIdAsync(request.SourceBankAccountId, cancellationToken);
        }

        if (source is null)
        {
            if (needsSource)
            {
                errors.Add(S["Choose the business's bank account the money leaves from."]);
            }
        }
        else
        {
            if (!source.Owner.IsEmpty)
            {
                errors.Add(S["Money leaves only from the business's own bank accounts."]);
            }

            if (source.Status is BankAccountStatus.Closed or BankAccountStatus.Failed)
            {
                errors.Add(S["The bank account the money leaves from is closed or failed."]);
            }

            if (needsSource && !BankAccountNumbers.HasNumbers(source))
            {
                errors.Add(S["The bank account the money leaves from has no routing or account number."]);
            }

            currency ??= source.Currency;

            if (!string.Equals(currency, source.Currency, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(S["The disbursement must be in the currency of the account it leaves from, {0}.", source.Currency]);
            }
        }

        if (request.Method == DisbursementsConstants.Methods.Ach)
        {
            var destination = string.IsNullOrEmpty(request.DestinationBankAccountId) ? null : await _bankAccounts.FindByIdAsync(request.DestinationBankAccountId, cancellationToken);

            if (destination is null)
            {
                errors.Add(S["Choose the payee's bank account."]);
            }
            else if (!BankAccountNumbers.HasNumbers(destination))
            {
                errors.Add(S["The payee's bank account has no routing or account number."]);
            }
            else if (request.IsPrenote ? destination.Status == BankAccountStatus.Closed : !destination.CanReceivePayments())
            {
                // A test entry is what checks an account, so it goes to one that is not verified yet.
                errors.Add(request.IsPrenote
                    ? S["The payee's bank account is closed."]
                    : S["The payee's bank account is not verified: no money goes to it until it is."]);
            }
            else if (currency is not null && !string.Equals(currency, destination.Currency, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(S["The payee's bank account is in another currency, {0}.", destination.Currency]);
            }
        }

        if (string.IsNullOrEmpty(currency))
        {
            errors.Add(S["Enter the currency."]);
        }

        if (errors.Count > 0)
        {
            return new DisbursementResult { Errors = errors };
        }

        var disbursement = await _manager.NewAsync(cancellationToken: cancellationToken);

        disbursement.Payee = request.Payee;
        disbursement.PayeeName = request.PayeeName.Trim();
        disbursement.PayeeAddress = request.PayeeAddress;
        disbursement.IsBusinessPayee = request.IsBusinessPayee;
        disbursement.Amount = CurrencyScale.Round(request.Amount, currency);
        disbursement.Currency = currency.ToUpperInvariant();
        disbursement.Method = request.Method;
        disbursement.SourceBankAccountId = source?.ItemId;
        disbursement.DestinationBankAccountId = request.Method == DisbursementsConstants.Methods.Ach ? request.DestinationBankAccountId : request.DestinationBankAccountId.NullIfEmpty();
        disbursement.SourceKind = request.SourceKind.NullIfEmpty();
        disbursement.SourceId = request.SourceId.NullIfEmpty();
        disbursement.AccessScope = disbursement.SourceKind is null ? null : request.AccessScope.NullIfEmpty();
        disbursement.Memo = string.IsNullOrWhiteSpace(request.Memo) ? null : request.Memo.Trim();
        disbursement.EffectiveDate = request.EffectiveDate;
        disbursement.IdempotencyKey = key;
        disbursement.IsPrenote = request.IsPrenote;
        disbursement.Status = DisbursementStatus.Pending;
        disbursement.CreatedUtc = _clock.UtcNow;

        await _manager.CreateAsync(disbursement, cancellationToken);
        await NotifyAsync(disbursement, DisbursementChangeKind.Created, null, cancellationToken);

        return new DisbursementResult { Disbursement = disbursement };
    }

    /// <inheritdoc />
    public Task<bool> CancelAsync(Disbursement disbursement, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disbursement);

        // A batched one is cancelled by cancelling its open batch first.
        if (disbursement.Status != DisbursementStatus.Pending)
        {
            return Task.FromResult(false);
        }

        return MarkClosedAsync(disbursement, DisbursementStatus.Canceled, null, reason, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(DisbursementBatch Batch, IReadOnlyList<string> Problems)> CreateBatchAsync(string providerName, IEnumerable<string> disbursementIds, DateOnly effectiveDate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disbursementIds);

        var problems = new List<string>();
        var provider = Providers.FirstOrDefault(entry => string.Equals(entry.Name, providerName, StringComparison.Ordinal));

        if (provider is null)
        {
            return (null, [S["Choose how the batch is sent."]]);
        }

        // In the order given, so the bank file lists the entries as the caller sent them.
        var order = disbursementIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        var candidates = (await _manager.ListAsync(order, cancellationToken)).OrderBy(entry => order.IndexOf(entry.ItemId)).ToList();
        var chosen = new List<Disbursement>();

        foreach (var disbursement in candidates)
        {
            if (disbursement.IsHistorical || disbursement.Status != DisbursementStatus.Pending)
            {
                problems.Add(S["{0} to {1} is {2} and can't be batched.", CurrencyScale.Format(disbursement.Amount, disbursement.Currency), disbursement.PayeeName, disbursement.Status.ToString()]);
            }
            else if (disbursement.Method != provider.Method)
            {
                problems.Add(S["{0} to {1} is not sent by {2}.", CurrencyScale.Format(disbursement.Amount, disbursement.Currency), disbursement.PayeeName, provider.DisplayName]);
            }
            else if (chosen.Count > 0 && (!string.Equals(chosen[0].SourceBankAccountId, disbursement.SourceBankAccountId, StringComparison.Ordinal) || chosen[0].Currency != disbursement.Currency))
            {
                problems.Add(S["{0} to {1} leaves from another bank account or in another currency: batch it separately.", CurrencyScale.Format(disbursement.Amount, disbursement.Currency), disbursement.PayeeName]);
            }
            else
            {
                chosen.Add(disbursement);
            }
        }

        if (chosen.Count == 0)
        {
            problems.Insert(0, S["No disbursement could be batched."]);

            return (null, problems);
        }

        var source = string.IsNullOrEmpty(chosen[0].SourceBankAccountId) ? null : await _bankAccounts.FindByIdAsync(chosen[0].SourceBankAccountId, cancellationToken);
        var batch = await _batches.NewAsync(cancellationToken: cancellationToken);

        batch.Provider = provider.Name;
        batch.SourceBankAccountId = chosen[0].SourceBankAccountId;
        batch.SourceBankAccountName = source?.Name;
        batch.EffectiveDate = effectiveDate;
        batch.DisbursementIds = chosen.Select(entry => entry.ItemId).ToList();
        batch.Sources = chosen
            .Where(entry => !string.IsNullOrEmpty(entry.SourceKind))
            .Select(entry => entry.SourceKind + "/" + entry.AccessScope)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        batch.Count = chosen.Count;
        batch.Currency = chosen[0].Currency;
        batch.TotalAmount = CurrencyScale.Round(chosen.Sum(entry => entry.Amount), batch.Currency);
        batch.Status = DisbursementBatchStatus.Open;
        batch.CreatedUtc = _clock.UtcNow;
        batch.CreatedBy = Actor().Name;

        await _batches.CreateAsync(batch, cancellationToken);

        foreach (var disbursement in chosen)
        {
            await MoveAsync(disbursement, DisbursementStatus.Batched, DisbursementChangeKind.Batched, cancellationToken, moved =>
            {
                moved.BatchId = batch.ItemId;
                moved.Provider = provider.Name;
                moved.EffectiveDate = effectiveDate;
            });
        }

        return (batch, problems);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ReleaseBatchAsync(DisbursementBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Status != DisbursementBatchStatus.Open)
        {
            return [S["The batch was already released or cancelled."]];
        }

        var provider = Providers.FirstOrDefault(entry => entry.Name == batch.Provider);

        if (provider is null)
        {
            return [S["The feature that sends this batch is off."]];
        }

        var disbursements = (await _manager.ListAsync(batch.DisbursementIds, cancellationToken))
            .Where(entry => entry.BatchId == batch.ItemId && entry.Status == DisbursementStatus.Batched)
            .OrderBy(entry => batch.DisbursementIds.IndexOf(entry.ItemId))
            .ToList();

        if (disbursements.Count == 0)
        {
            return [S["The batch has no disbursement left to send."]];
        }

        var result = await provider.SubmitAsync(batch, disbursements, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Errors.Count > 0 ? result.Errors : [S["The batch could not be sent."]];
        }

        if (result.File is { Length: > 0 })
        {
            await _files.WriteAsync(batch.ItemId, result.File);
            batch.HasFile = true;
            batch.FileName = result.FileName;
        }

        foreach (var disbursement in disbursements)
        {
            var status = result.Statuses.GetValueOrDefault(disbursement.ItemId, DisbursementStatus.Submitted);
            var trace = result.TraceNumbers.GetValueOrDefault(disbursement.ItemId);
            var failure = result.FailureReasons.GetValueOrDefault(disbursement.ItemId);

            disbursement.TraceNumber = trace ?? disbursement.TraceNumber;
            disbursement.CheckId = result.CheckIds.GetValueOrDefault(disbursement.ItemId) ?? disbursement.CheckId;
            disbursement.SubmittedUtc = _clock.UtcNow;

            if (status is DisbursementStatus.Failed)
            {
                await MarkClosedAsync(disbursement, DisbursementStatus.Failed, null, failure, cancellationToken);

                continue;
            }

            if (status is DisbursementStatus.Settled)
            {
                await MoveAsync(disbursement, DisbursementStatus.Submitted, DisbursementChangeKind.Submitted, cancellationToken);
                await MoveAsync(disbursement, DisbursementStatus.Settled, DisbursementChangeKind.Settled, cancellationToken, moved => moved.ClosedUtc = _clock.UtcNow);

                continue;
            }

            await MoveAsync(disbursement, DisbursementStatus.Submitted, DisbursementChangeKind.Submitted, cancellationToken);
        }

        batch.ProviderReference = result.ProviderReference;
        batch.Status = disbursements.All(entry => entry.Status is DisbursementStatus.Settled or DisbursementStatus.Failed)
            ? DisbursementBatchStatus.Settled
            : DisbursementBatchStatus.Released;
        batch.ReleasedUtc = _clock.UtcNow;
        batch.ReleasedBy = Actor().Name;

        await _batches.UpdateAsync(batch, cancellationToken: cancellationToken);

        return [];
    }

    /// <inheritdoc />
    public async Task<bool> CancelBatchAsync(DisbursementBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Status != DisbursementBatchStatus.Open)
        {
            return false;
        }

        foreach (var disbursement in await _manager.ListAsync(batch.DisbursementIds, cancellationToken))
        {
            if (disbursement.BatchId == batch.ItemId && disbursement.Status == DisbursementStatus.Batched)
            {
                await MoveAsync(disbursement, DisbursementStatus.Pending, DisbursementChangeKind.Batched, cancellationToken, moved =>
                {
                    moved.BatchId = null;
                    moved.Provider = null;
                });
            }
        }

        batch.Status = DisbursementBatchStatus.Canceled;
        await _batches.UpdateAsync(batch, cancellationToken: cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<int> SettleBatchAsync(DisbursementBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Status != DisbursementBatchStatus.Released)
        {
            return 0;
        }

        var count = 0;

        foreach (var disbursement in await _manager.ListAsync(batch.DisbursementIds, cancellationToken))
        {
            if (disbursement.BatchId == batch.ItemId && disbursement.Status == DisbursementStatus.Submitted && await MarkSettledAsync(disbursement, cancellationToken))
            {
                count++;
            }
        }

        batch.Status = DisbursementBatchStatus.Settled;
        await _batches.UpdateAsync(batch, cancellationToken: cancellationToken);

        return count;
    }

    /// <inheritdoc />
    public async Task<int> SettleReleasedBatchesAsync(int ageInDays, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ageInDays, 1);

        var before = _clock.UtcNow.AddDays(-ageInDays);
        var count = 0;

        while (true)
        {
            var batches = await _batchStore.ListToSettleAsync(before, SettleBatchSize, cancellationToken);

            foreach (var batch in batches)
            {
                count += await SettleBatchAsync(batch, cancellationToken);
            }

            if (batches.Count < SettleBatchSize)
            {
                return count;
            }
        }
    }

    /// <inheritdoc />
    public Task<bool> MarkSettledAsync(Disbursement disbursement, CancellationToken cancellationToken = default)
        => MoveAsync(disbursement, DisbursementStatus.Settled, DisbursementChangeKind.Settled, cancellationToken, moved => moved.ClosedUtc = _clock.UtcNow);

    /// <inheritdoc />
    public Task<bool> MarkClosedAsync(Disbursement disbursement, DisbursementStatus status, string code, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disbursement);

        var kind = status switch
        {
            DisbursementStatus.Returned => DisbursementChangeKind.Returned,
            DisbursementStatus.Failed => DisbursementChangeKind.Failed,
            DisbursementStatus.Canceled => DisbursementChangeKind.Canceled,
            DisbursementStatus.Reversed => DisbursementChangeKind.Reversed,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        return MoveAsync(disbursement, status, kind, cancellationToken, moved =>
        {
            moved.FailureCode = code.NullIfEmpty();
            moved.FailureReason = reason.NullIfEmpty();
            moved.ClosedUtc = _clock.UtcNow;
        });
    }

    /// <inheritdoc />
    public async Task<DisbursementImportResult> ApplyReturnsAsync(IEnumerable<DisbursementReturn> returns, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(returns);

        var result = new DisbursementImportResult();

        foreach (var entry in returns)
        {
            var disbursement = string.IsNullOrEmpty(entry.TraceNumber) ? null : await _manager.FindByTraceNumberAsync(entry.TraceNumber, cancellationToken);

            if (disbursement is null)
            {
                result.Problems.Add(S["No disbursement has the trace number {0}.", entry.TraceNumber]);
            }
            else if (await MarkClosedAsync(disbursement, DisbursementStatus.Returned, entry.Code, entry.Description, cancellationToken))
            {
                result.Returned++;
            }
            else if (disbursement.Status != DisbursementStatus.Returned)
            {
                result.Problems.Add(S["The disbursement with trace number {0} is {1} and can't be returned.", entry.TraceNumber, disbursement.Status.ToString()]);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<DisbursementImportResult> ApplyChangeNoticesAsync(IEnumerable<DisbursementChangeNotice> notices, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notices);

        var result = new DisbursementImportResult();

        foreach (var notice in notices)
        {
            var disbursement = string.IsNullOrEmpty(notice.TraceNumber) ? null : await _manager.FindByTraceNumberAsync(notice.TraceNumber, cancellationToken);
            var account = disbursement is null || string.IsNullOrEmpty(disbursement.DestinationBankAccountId)
                ? null
                : await _bankAccounts.FindByIdAsync(disbursement.DestinationBankAccountId, cancellationToken);

            if (account is null)
            {
                result.Problems.Add(S["No bank account matches the notice of change for trace number {0}.", notice.TraceNumber]);

                continue;
            }

            var correction = NachaChangeCorrection.Parse(notice.Code, notice.CorrectedData);

            if (correction is null)
            {
                result.Problems.Add(S["The notice of change {0} for trace number {1} is not one this site applies: change the bank account by hand.", notice.Code, notice.TraceNumber]);

                continue;
            }

            var errors = _bankAccountService.ApplyNumbers(account, correction.RoutingNumber, correction.AccountNumber);

            if (errors.Count > 0)
            {
                result.Problems.Add(S["The corrected numbers for trace number {0} were refused: {1}", notice.TraceNumber, string.Join(" ", errors.Select(error => error.Message))]);

                continue;
            }

            if (correction.Kind is { } kind)
            {
                account.Kind = kind;
            }

            await _bankAccounts.UpdateAsync(account, cancellationToken: cancellationToken);
            result.Corrected++;
        }

        return result;
    }

    private async Task<bool> MoveAsync(Disbursement disbursement, DisbursementStatus status, DisbursementChangeKind kind, CancellationToken cancellationToken, Action<Disbursement> apply = null)
    {
        ArgumentNullException.ThrowIfNull(disbursement);

        if (disbursement.IsHistorical || !DisbursementStatuses.Transitions.CanMove(disbursement.Status, status))
        {
            return false;
        }

        var previous = disbursement.Status;

        apply?.Invoke(disbursement);
        disbursement.Status = status;

        await _manager.UpdateAsync(disbursement, cancellationToken: cancellationToken);
        await NotifyAsync(disbursement, kind, previous, cancellationToken);

        return true;
    }

    private async Task NotifyAsync(Disbursement disbursement, DisbursementChangeKind kind, DisbursementStatus? previous, CancellationToken cancellationToken)
    {
        var (name, id) = Actor();
        var change = new DisbursementChange
        {
            Disbursement = disbursement,
            Kind = kind,
            PreviousStatus = previous,
            Actor = name,
            ActorId = id,
        };

        foreach (var observer in _observers)
        {
            await observer.OnChangedAsync(change, cancellationToken);
        }
    }

    private (string Name, string Id) Actor()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? (user.Identity.Name, user.FindFirstValue(ClaimTypes.NameIdentifier))
            : (null, null);
    }
}

internal static class DisbursementStringExtensions
{
    public static string NullIfEmpty(this string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
