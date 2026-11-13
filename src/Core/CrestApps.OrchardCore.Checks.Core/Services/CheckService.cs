using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Numbering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checks.Core.Services;

/// <summary>
/// The default <see cref="ICheckService"/>.
/// </summary>
public sealed class CheckService : ICheckService
{
    /// <summary>
    /// The number a bank account's first check gets when nobody set one.
    /// </summary>
    public const long DefaultFirstNumber = 1001;

    private readonly ICheckManager _manager;
    private readonly IBankAccountManager _bankAccounts;
    private readonly INumberSequence _numbers;
    private readonly IEnumerable<ICheckObserver> _observers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckService"/> class.
    /// </summary>
    /// <param name="manager">The check register.</param>
    /// <param name="bankAccounts">The bank accounts.</param>
    /// <param name="numbers">The number sequences.</param>
    /// <param name="observers">The observers of check changes.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for who made a change.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckService(
        ICheckManager manager,
        IBankAccountManager bankAccounts,
        INumberSequence numbers,
        IEnumerable<ICheckObserver> observers,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<CheckService> stringLocalizer)
    {
        _manager = manager;
        _bankAccounts = bankAccounts;
        _numbers = numbers;
        _observers = observers;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<CheckResult> IssueAsync(CheckIssueRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var account = string.IsNullOrEmpty(request.BankAccountId) ? null : await _bankAccounts.FindByIdAsync(request.BankAccountId, cancellationToken);

        if (account is null)
        {
            errors.Add(S["Choose the bank account the check is drawn on."]);
        }
        else
        {
            if (!account.Owner.IsEmpty)
            {
                errors.Add(S["Checks are drawn only on the business's own bank accounts."]);
            }

            if (account.Status is BankAccountStatus.Closed or BankAccountStatus.Failed)
            {
                errors.Add(S["The bank account is closed or failed: checks can't be drawn on it."]);
            }

            if (!BankAccountNumbers.HasNumbers(account))
            {
                errors.Add(S["The bank account has no routing or account number."]);
            }

            if (!string.IsNullOrEmpty(request.Currency) && !string.Equals(request.Currency, account.Currency, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(S["The check must be in the bank account's currency, {0}.", account.Currency]);
            }
        }

        if (request.Amount <= 0)
        {
            errors.Add(S["The amount must be more than zero."]);
        }

        if (string.IsNullOrWhiteSpace(request.PayeeName))
        {
            errors.Add(S["Enter who the check is paid to."]);
        }

        if (request.CheckDate == default)
        {
            errors.Add(S["Enter the check date."]);
        }

        if (errors.Count > 0)
        {
            return new CheckResult { Errors = errors };
        }

        var currency = account.Currency;
        var number = await _numbers.NextAsync(ChecksConstants.NumberSeries(account.ItemId), new NumberSequenceOptions
        {
            FirstNumber = DefaultFirstNumber,
            SeedAsync = async token => (await GetHighestNumberAsync(account.ItemId, token)) ?? 0,
        }, cancellationToken);

        var check = await _manager.NewAsync(cancellationToken: cancellationToken);

        check.BankAccountId = account.ItemId;
        check.BankAccountName = account.Name;
        check.Number = number;
        check.Payee = request.Payee;
        check.PayeeName = request.PayeeName.Trim();
        check.PayeeAddress = request.PayeeAddress;
        check.Amount = CurrencyScale.Round(request.Amount, currency);
        check.Currency = currency;
        check.CheckDate = request.CheckDate;
        check.Memo = string.IsNullOrWhiteSpace(request.Memo) ? null : request.Memo.Trim();
        check.SourceKind = request.SourceKind;
        check.SourceId = request.SourceId;
        check.Status = CheckStatus.Issued;
        check.IssuedUtc = _clock.UtcNow;

        await _manager.CreateAsync(check, cancellationToken);
        await NotifyAsync(check, CheckChangeKind.Issued, null, null, null, cancellationToken);

        return new CheckResult { Check = check };
    }

    /// <inheritdoc />
    public async Task<long> GetNextNumberAsync(string bankAccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bankAccountId);

        var last = await _numbers.GetLastAsync(ChecksConstants.NumberSeries(bankAccountId), cancellationToken);

        if (last is not null)
        {
            return last.Value + 1;
        }

        var highest = await GetHighestNumberAsync(bankAccountId, cancellationToken);

        return highest is null ? DefaultFirstNumber : highest.Value + 1;
    }

    /// <inheritdoc />
    public async Task<bool> SetNextNumberAsync(string bankAccountId, long next, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bankAccountId);
        ArgumentOutOfRangeException.ThrowIfLessThan(next, 1);

        // A number a check already has would print twice.
        if (await GetHighestNumberAsync(bankAccountId, cancellationToken) is long highest && highest >= next)
        {
            return false;
        }

        await _numbers.SetNextAsync(ChecksConstants.NumberSeries(bankAccountId), next, cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task MarkPrintedAsync(IEnumerable<CheckRecord> checks, string printRunId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checks);

        foreach (var check in checks)
        {
            if (check.IsHistorical || check.Status is not (CheckStatus.Issued or CheckStatus.Printed))
            {
                continue;
            }

            var previous = check.Status;

            check.Status = CheckStatus.Printed;
            check.PrintedUtc = _clock.UtcNow;
            check.PrintRunId = printRunId;

            await _manager.UpdateAsync(check, cancellationToken: cancellationToken);
            await NotifyAsync(check, CheckChangeKind.Printed, previous, null, null, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<bool> VoidAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default)
        => MoveAsync(check, CheckStatus.Voided, CheckChangeKind.Voided, reason, cancellationToken);

    /// <inheritdoc />
    public async Task<CheckResult> ReissueAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (check.IsHistorical || !CheckStatuses.Transitions.CanMove(check.Status, CheckStatus.Reissued))
        {
            return new CheckResult { Errors = [S["Check {0} can't be reissued.", check.Number]] };
        }

        var result = await IssueAsync(new CheckIssueRequest
        {
            BankAccountId = check.BankAccountId,
            Payee = check.Payee,
            PayeeName = check.PayeeName,
            PayeeAddress = check.PayeeAddress,
            Amount = check.Amount,
            Currency = check.Currency,
            CheckDate = DateOnly.FromDateTime(_clock.UtcNow),
            Memo = check.Memo,
            SourceKind = check.SourceKind,
            SourceId = check.SourceId,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return result;
        }

        var previous = check.Status;

        result.Check.ReissueOfId = check.ItemId;
        await _manager.UpdateAsync(result.Check, cancellationToken: cancellationToken);

        check.Status = CheckStatus.Reissued;
        check.ReissuedById = result.Check.ItemId;
        check.ClosedUtc = _clock.UtcNow;
        check.StatusReason = reason;

        await _manager.UpdateAsync(check, cancellationToken: cancellationToken);
        await NotifyAsync(check, CheckChangeKind.Reissued, previous, reason, result.Check, cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public Task<bool> StopPaymentAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default)
        => MoveAsync(check, CheckStatus.StopPayment, CheckChangeKind.StopPayment, reason, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> MarkClearedAsync(CheckRecord check, DateOnly clearedOn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (!CheckStatuses.Transitions.CanMove(check.Status, CheckStatus.Cleared))
        {
            return false;
        }

        check.ClearedOn = clearedOn;

        return await MoveAsync(check, CheckStatus.Cleared, CheckChangeKind.Cleared, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> MarkStaleAsync(CheckRecord check, CancellationToken cancellationToken = default)
        => MoveAsync(check, CheckStatus.Stale, CheckChangeKind.Stale, null, cancellationToken);

    private async Task<bool> MoveAsync(CheckRecord check, CheckStatus status, CheckChangeKind kind, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (check.IsHistorical || !CheckStatuses.Transitions.CanMove(check.Status, status))
        {
            return false;
        }

        var previous = check.Status;

        check.Status = status;
        check.StatusReason = reason ?? check.StatusReason;

        if (status is CheckStatus.Voided or CheckStatus.StopPayment or CheckStatus.Stale)
        {
            check.ClosedUtc = _clock.UtcNow;
        }

        await _manager.UpdateAsync(check, cancellationToken: cancellationToken);
        await NotifyAsync(check, kind, previous, reason, null, cancellationToken);

        return true;
    }

    private Task<long?> GetHighestNumberAsync(string bankAccountId, CancellationToken cancellationToken)
        => _manager.GetHighestNumberAsync(bankAccountId, cancellationToken);

    private async Task NotifyAsync(CheckRecord check, CheckChangeKind kind, CheckStatus? previous, string reason, CheckRecord replacedBy, CancellationToken cancellationToken)
    {
        var change = new CheckChange
        {
            Check = check,
            Kind = kind,
            PreviousStatus = previous,
            ReplacedBy = replacedBy,
            Actor = _httpContextAccessor.HttpContext?.User?.Identity?.Name,
            Reason = reason,
        };

        foreach (var observer in _observers)
        {
            await observer.OnChangedAsync(change, cancellationToken);
        }
    }
}
