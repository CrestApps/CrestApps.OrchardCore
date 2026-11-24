using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Numbering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

/// <summary>
/// The default <see cref="ICheckService"/>.
/// </summary>
public sealed class CheckService : ICheckService
{
    /// <summary>
    /// The number a bank account's first check gets when nobody set one.
    /// </summary>
    public const long DefaultFirstNumber = 1001;

    private const int StaleBatchSize = 200;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly ICheckManager _manager;
    private readonly IBankAccountManager _bankAccounts;
    private readonly INumberSequence _numbers;
    private readonly IEnumerable<ICheckObserver> _observers;
    private readonly IEnumerable<ICheckChangeHandler> _handlers;
    private readonly IEnumerable<ICheckWriteHandler> _writeHandlers;
    private readonly IDistributedLock _locks;
    private readonly ICheckCommitter _committer;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckService"/> class.
    /// </summary>
    /// <param name="manager">The check register.</param>
    /// <param name="bankAccounts">The bank accounts.</param>
    /// <param name="numbers">The number sequences.</param>
    /// <param name="observers">The observers of check changes, told after each change is saved.</param>
    /// <param name="handlers">The handlers asked before a void, reissue, clear, un-clear or edit is saved.</param>
    /// <param name="writeHandlers">The handlers that take part in writing a check, before and after it is saved.</param>
    /// <param name="locks">The distributed lock, so one print run of a bank account is numbered at a time.</param>
    /// <param name="committer">Commits a print run before its lock is released.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for who made a change.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The site's local clock, for the dates printed on checks.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckService(
        ICheckManager manager,
        IBankAccountManager bankAccounts,
        INumberSequence numbers,
        IEnumerable<ICheckObserver> observers,
        IEnumerable<ICheckChangeHandler> handlers,
        IEnumerable<ICheckWriteHandler> writeHandlers,
        IDistributedLock locks,
        ICheckCommitter committer,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        ILocalClock localClock,
        IStringLocalizer<CheckService> stringLocalizer)
    {
        _manager = manager;
        _bankAccounts = bankAccounts;
        _numbers = numbers;
        _observers = observers;
        _handlers = handlers;
        _writeHandlers = writeHandlers;
        _locks = locks;
        _committer = committer;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public Task<CheckResult> IssueAsync(CheckIssueRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return IssueCoreAsync(request, reissueOf: null, cancellationToken);
    }

    // Writes a check: the write handlers complete or refuse the request, Banking checks it, the check is saved, and the
    // write handlers then the observers are told.
    private async Task<CheckResult> IssueCoreAsync(CheckIssueRequest request, CheckRecord reissueOf, CancellationToken cancellationToken)
    {
        // The same request again (a retry, a double click) gets the check already issued, and nobody is asked again.
        if (!string.IsNullOrEmpty(request.IdempotencyKey) && !string.IsNullOrEmpty(request.SourceKind) && !string.IsNullOrEmpty(request.SourceId)
            && (await _manager.ListBySourceAsync(request.SourceKind, request.SourceId, cancellationToken)).FirstOrDefault(existing => string.Equals(existing.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal)) is { } issuedBefore)
        {
            return new CheckResult { Check = issuedBefore };
        }

        var writing = new CheckWritingContext(request, reissueOf);

        foreach (var handler in _writeHandlers)
        {
            await handler.WritingAsync(writing, cancellationToken);
        }

        var errors = new List<string>(writing.Errors);
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

            // The numbers are needed only where they print: the check line on blank stock, which the printer checks.
            if (!BankAccountKinds.CanDrawChecks(account.Kind))
            {
                errors.Add(S["Checks are drawn only on a checking or savings account."]);
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

        if (request.Numbering == CheckNumbering.Handwritten && request.Number is not > 0)
        {
            errors.Add(S["Enter the number written on the check."]);
        }

        if (errors.Count > 0)
        {
            return new CheckResult { Errors = errors };
        }

        long number;
        CheckStatus status;

        switch (request.Numbering)
        {
            case CheckNumbering.AtPrint:
                number = 0;
                status = CheckStatus.Queued;
                break;
            case CheckNumbering.Handwritten:
                number = request.Number.Value;
                status = CheckStatus.Printed;

                if (await _manager.FindByNumberAsync(account.ItemId, number, cancellationToken) is not null)
                {
                    return new CheckResult { Errors = [S["Check number {0} is already used on {1}.", number, account.Name]] };
                }

                break;
            default:
                number = await NextFreeNumberAsync(account.ItemId, cancellationToken);
                status = CheckStatus.Issued;
                break;
        }

        var currency = account.Currency;
        var check = await _manager.NewAsync(cancellationToken: cancellationToken);

        check.BankAccountId = account.ItemId;
        check.BankAccountName = account.Name;
        check.Number = number;
        check.Numbering = request.Numbering;
        check.Payee = request.Payee;
        check.PayeeName = request.PayeeName.Trim();
        check.PayeeAddress = request.PayeeAddress;
        check.Amount = CurrencyScale.Round(request.Amount, currency);
        check.Currency = currency;
        check.CheckDate = request.CheckDate;
        check.Memo = string.IsNullOrWhiteSpace(request.Memo) ? null : request.Memo.Trim();
        check.SourceKind = request.SourceKind;
        check.SourceId = request.SourceId;
        check.AccessScope = string.IsNullOrEmpty(request.SourceKind) ? null : request.AccessScope;
        check.IdempotencyKey = string.IsNullOrEmpty(request.IdempotencyKey) ? null : request.IdempotencyKey;
        check.Status = status;
        check.IssuedUtc = _clock.UtcNow;

        if (request.Properties is not null)
        {
            foreach (var (key, value) in request.Properties)
            {
                check.Properties[key] = value;
            }
        }

        // A check written by hand left with the payee already.
        if (status == CheckStatus.Printed)
        {
            check.PrintedUtc = check.IssuedUtc;
        }

        await _manager.CreateAsync(check, cancellationToken);

        var written = new CheckWrittenContext(check, request, reissueOf);

        foreach (var handler in _writeHandlers)
        {
            await handler.WrittenAsync(written, cancellationToken);
        }

        await NotifyAsync(new CheckChange { Check = check, Kind = CheckChangeKind.Issued }, cancellationToken);

        return new CheckResult { Check = check };
    }

    /// <inheritdoc />
    public async Task<CheckResult> UpdateAsync(CheckRecord check, CheckDetails details, string reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(details);

        // Once a check is printed, the paper is with the payee: it is voided and written again, never edited.
        if (!CheckStatuses.CanEdit(check))
        {
            return new CheckResult { Errors = [S["{0} is no longer waiting to print: void it and write a new check instead.", Describe(check)]] };
        }

        var errors = new List<string>();

        if (details.Amount <= 0)
        {
            errors.Add(S["The amount must be more than zero."]);
        }

        if (string.IsNullOrWhiteSpace(details.PayeeName))
        {
            errors.Add(S["Enter who the check is paid to."]);
        }

        if (details.CheckDate == default)
        {
            errors.Add(S["Enter the check date."]);
        }

        if (errors.Count > 0)
        {
            return new CheckResult { Errors = errors };
        }

        var updated = new CheckDetails
        {
            Payee = details.Payee,
            PayeeName = details.PayeeName.Trim(),
            PayeeAddress = details.PayeeAddress,
            Amount = CurrencyScale.Round(details.Amount, check.Currency),
            CheckDate = details.CheckDate,
            Memo = string.IsNullOrWhiteSpace(details.Memo) ? null : details.Memo.Trim(),
            Properties = details.Properties,
        };

        var refusal = await AskHandlersAsync(new CheckChangingContext(check, CheckChangeKind.Updated, check.Status) { Details = updated, Reason = reason }, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var previous = CheckDetails.From(check);

        check.Payee = updated.Payee;
        check.PayeeName = updated.PayeeName;
        check.PayeeAddress = updated.PayeeAddress;
        check.Amount = updated.Amount;
        check.CheckDate = updated.CheckDate;
        check.Memo = updated.Memo;

        if (updated.Properties is not null)
        {
            check.Properties = new Dictionary<string, object>(updated.Properties, StringComparer.OrdinalIgnoreCase);
        }

        await _manager.UpdateAsync(check, cancellationToken: cancellationToken);
        await NotifyAsync(new CheckChange { Check = check, Kind = CheckChangeKind.Updated, PreviousStatus = check.Status, Reason = reason, PreviousDetails = previous }, cancellationToken);

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

        return highest is null or 0 ? DefaultFirstNumber : highest.Value + 1;
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
    public async Task<IReadOnlyList<string>> ValidatePrintRunAsync(IReadOnlyList<CheckRecord> checks, long firstNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checks);

        if (checks.Count == 0)
        {
            return [S["Choose the checks to print."]];
        }

        var errors = new List<string>();

        foreach (var check in checks.Where(check => check.IsHistorical || check.Status != CheckStatus.Queued))
        {
            errors.Add(S["{0} is not waiting to print: only queued checks are numbered by a print run.", Describe(check)]);
        }

        if (checks.Select(check => check.ItemId).Distinct(StringComparer.Ordinal).Count() != checks.Count)
        {
            errors.Add(S["A check can't print twice in one run."]);
        }

        var accountIds = checks.Select(check => check.BankAccountId).Distinct(StringComparer.Ordinal).ToList();

        if (accountIds.Count > 1)
        {
            errors.Add(S["A print run on pre-printed stock prints the checks of one bank account: the stock carries its numbers."]);
        }

        if (firstNumber < 1)
        {
            errors.Add(S["Enter the number printed on the first sheet."]);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        for (var index = 0; index < checks.Count; index++)
        {
            var number = firstNumber + index;

            if (await _manager.FindByNumberAsync(accountIds[0], number, cancellationToken) is not null)
            {
                errors.Add(S["Number {0} is already used by a check of {1}. Check the number on the first sheet.", number, checks[0].BankAccountName]);
            }
        }

        return errors;
    }

    /// <inheritdoc />
    public async Task<CheckPrintRunResult> ConfirmPrintRunAsync(CheckPrintRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bankAccountId = request.Checks is { Count: > 0 } checks ? checks[0]?.BankAccountId : null;

        if (string.IsNullOrEmpty(bankAccountId))
        {
            return await ConfirmPrintRunLockedAsync(request, cancellationToken);
        }

        // One print run of a bank account at a time, from the check of its numbers to the commit, so two people confirming
        // runs of the same account at once never give two checks the same number.
        var (locker, locked) = await _locks.TryAcquireLockAsync(NumbersLockKey(bankAccountId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new CheckPrintRunResult { Errors = [S["Another print run of this bank account is being confirmed. Try again in a moment."]] };
        }

        await using (locker)
        {
            var result = await ConfirmPrintRunLockedAsync(request, cancellationToken);

            if (result.Succeeded)
            {
                await _committer.CommitAsync(cancellationToken);
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the key of the lock held while a print run of a bank account takes its numbers.
    /// </summary>
    /// <param name="bankAccountId">The bank account's id.</param>
    /// <returns>The lock key.</returns>
    public static string NumbersLockKey(string bankAccountId)
        => "Checks:Numbers:" + bankAccountId;

    private async Task<CheckPrintRunResult> ConfirmPrintRunLockedAsync(CheckPrintRunRequest request, CancellationToken cancellationToken)
    {
        var checks = request.Checks ?? [];
        var misprinted = new HashSet<string>(request.MisprintedCheckIds ?? [], StringComparer.Ordinal);
        var errors = new List<string>(await ValidatePrintRunAsync(checks, request.FirstNumber, cancellationToken));

        if (misprinted.Any(id => !checks.Any(check => string.Equals(check.ItemId, id, StringComparison.Ordinal))))
        {
            errors.Add(S["A misprinted sheet must be one of the run's checks."]);
        }

        if (errors.Count > 0)
        {
            return new CheckPrintRunResult { Errors = errors };
        }

        var bankAccountId = checks[0].BankAccountId;
        var printRunId = string.IsNullOrEmpty(request.PrintRunId) ? IdGenerator.GenerateId() : request.PrintRunId;
        var next = await GetNextNumberAsync(bankAccountId, cancellationToken);
        var today = await GetLocalTodayAsync();
        var printed = new List<CheckRecord>();
        var spoiled = new List<CheckRecord>();

        for (var index = 0; index < checks.Count; index++)
        {
            var check = checks[index];
            var number = request.FirstNumber + index;

            if (misprinted.Contains(check.ItemId))
            {
                // The sheet with this number is spoiled: its number is kept as a voided check, and the check waits for
                // another sheet. It pays nothing, so it carries no source id; the source kind keeps who may see it.
                var sheet = await _manager.NewAsync(cancellationToken: cancellationToken);

                sheet.BankAccountId = check.BankAccountId;
                sheet.BankAccountName = check.BankAccountName;
                sheet.Number = number;
                sheet.Numbering = CheckNumbering.AtPrint;
                sheet.Payee = check.Payee;
                sheet.PayeeName = check.PayeeName;
                sheet.Amount = check.Amount;
                sheet.Currency = check.Currency;
                sheet.CheckDate = check.CheckDate;
                sheet.SourceKind = check.SourceKind;
                sheet.AccessScope = check.AccessScope;
                sheet.Status = CheckStatus.Voided;
                sheet.IsSpoiled = true;
                sheet.SpoiledForId = check.ItemId;
                sheet.IssuedUtc = _clock.UtcNow;
                sheet.PrintedUtc = sheet.IssuedUtc;
                sheet.PrintRunId = printRunId;
                sheet.ClosedUtc = sheet.IssuedUtc;
                sheet.VoidedOn = today;
                sheet.StatusReason = S["Misprinted"];

                await _manager.CreateAsync(sheet, cancellationToken);
                await NotifyAsync(new CheckChange { Check = sheet, Kind = CheckChangeKind.Spoiled, Reason = sheet.StatusReason, VoidedOn = today }, cancellationToken);
                spoiled.Add(sheet);

                continue;
            }

            check.Number = number;
            check.Status = CheckStatus.Printed;
            check.PrintedUtc = _clock.UtcNow;
            check.PrintRunId = printRunId;

            await _manager.UpdateAsync(check, cancellationToken: cancellationToken);
            await NotifyAsync(new CheckChange { Check = check, Kind = CheckChangeKind.Printed, PreviousStatus = CheckStatus.Queued }, cancellationToken);
            printed.Add(check);
        }

        // The stock was used up to the last sheet of the run: the account's next check starts after it.
        if (request.FirstNumber + checks.Count > next)
        {
            await _numbers.SetNextAsync(ChecksConstants.NumberSeries(bankAccountId), request.FirstNumber + checks.Count, cancellationToken);
        }

        return new CheckPrintRunResult { Printed = printed, Spoiled = spoiled };
    }

    /// <inheritdoc />
    public async Task MarkPrintedAsync(IEnumerable<CheckRecord> checks, string printRunId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checks);

        foreach (var check in checks)
        {
            // A printed check prints again only as a reprint, with a reason; a queued one only in a print run.
            if (check.IsHistorical || check.Status is not CheckStatus.Issued)
            {
                continue;
            }

            var previous = check.Status;

            check.Status = CheckStatus.Printed;
            check.PrintedUtc = _clock.UtcNow;
            check.PrintRunId = printRunId;

            await _manager.UpdateAsync(check, cancellationToken: cancellationToken);
            await NotifyAsync(new CheckChange { Check = check, Kind = CheckChangeKind.Printed, PreviousStatus = previous }, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<bool> MarkReprintedAsync(CheckRecord check, string printRunId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (check.IsHistorical || check.Numbering == CheckNumbering.Handwritten || check.Status is not CheckStatus.Printed || string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        check.PrintedUtc = _clock.UtcNow;
        check.PrintRunId = printRunId;
        check.StatusReason = reason.Trim();

        await _manager.UpdateAsync(check, cancellationToken: cancellationToken);
        await NotifyAsync(new CheckChange { Check = check, Kind = CheckChangeKind.Reprinted, PreviousStatus = CheckStatus.Printed, Reason = check.StatusReason }, cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> VoidAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        // A check dated later can't be voided before its date.
        var today = await GetLocalTodayAsync();
        var voidedOn = check.CheckDate > today ? check.CheckDate : today;

        return (await VoidAsync(check, reason, voidedOn, cancellationToken)).Succeeded;
    }

    /// <inheritdoc />
    public async Task<CheckResult> VoidAsync(CheckRecord check, string reason, DateOnly voidedOn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (voidedOn == default)
        {
            return new CheckResult { Errors = [S["Enter the void date."]] };
        }

        if (check.CheckDate != default && voidedOn < check.CheckDate)
        {
            return new CheckResult { Errors = [S["A check can't be voided before its date, {0}.", check.CheckDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)]] };
        }

        return await ChangeAsync(
            check,
            CheckStatus.Voided,
            CheckChangeKind.Voided,
            reason,
            askHandlers: true,
            voidedOn: voidedOn,
            clearedOn: null,
            apply: voided => voided.VoidedOn = voidedOn,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CheckResult> ReissueAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (check.IsHistorical || !CheckStatuses.Transitions.CanMove(check.Status, CheckStatus.Reissued))
        {
            return new CheckResult { Errors = [S["{0} can't be reissued.", Describe(check)]] };
        }

        var refusal = await AskHandlersAsync(new CheckChangingContext(check, CheckChangeKind.Reissued, CheckStatus.Reissued) { Reason = reason }, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await IssueCoreAsync(new CheckIssueRequest
        {
            BankAccountId = check.BankAccountId,
            Payee = check.Payee,
            PayeeName = check.PayeeName,
            PayeeAddress = check.PayeeAddress,
            Amount = check.Amount,
            Currency = check.Currency,
            CheckDate = await GetLocalTodayAsync(),
            Memo = check.Memo,
            SourceKind = check.SourceKind,
            SourceId = check.SourceId,
            AccessScope = check.AccessScope,
            Properties = new Dictionary<string, object>(check.Properties, StringComparer.OrdinalIgnoreCase),

            // A check that was numbered by a print run is replaced on the same stock; any other takes the next number.
            Numbering = check.Numbering == CheckNumbering.AtPrint ? CheckNumbering.AtPrint : CheckNumbering.Next,
        }, check, cancellationToken);

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
        await NotifyAsync(new CheckChange { Check = check, Kind = CheckChangeKind.Reissued, PreviousStatus = previous, Reason = reason, ReplacedBy = result.Check }, cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public Task<bool> StopPaymentAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default)
        => MoveAsync(check, CheckStatus.StopPayment, CheckChangeKind.StopPayment, reason, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> MarkClearedAsync(CheckRecord check, DateOnly clearedOn, CancellationToken cancellationToken = default)
        => (await ClearAsync(check, clearedOn, cancellationToken)).Succeeded;

    /// <inheritdoc />
    public async Task<CheckResult> ClearAsync(CheckRecord check, DateOnly clearedOn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (clearedOn == default)
        {
            return new CheckResult { Errors = [S["Enter the date the bank paid the check."]] };
        }

        return await ChangeAsync(
            check,
            CheckStatus.Cleared,
            CheckChangeKind.Cleared,
            reason: null,
            askHandlers: true,
            voidedOn: null,
            clearedOn: clearedOn,
            apply: cleared =>
            {
                cleared.ClearedFrom = cleared.Status;
                cleared.ClearedOn = clearedOn;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CheckResult> UnclearAsync(CheckRecord check, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        if (check.Status != CheckStatus.Cleared)
        {
            return new CheckResult { Errors = [S["{0} is not cleared.", Describe(check)]] };
        }

        var target = check.ClearedFrom is { } from && from != CheckStatus.Cleared ? from : CheckStatus.Printed;
        var clearedOn = check.ClearedOn;
        var refusal = await AskHandlersAsync(new CheckChangingContext(check, CheckChangeKind.Uncleared, target) { ClearedOn = clearedOn, Reason = reason }, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        check.Status = target;
        check.ClearedOn = null;
        check.ClearedFrom = null;
        check.StatusReason = string.IsNullOrWhiteSpace(reason) ? check.StatusReason : reason.Trim();

        await _manager.UpdateAsync(check, cancellationToken: cancellationToken);
        await NotifyAsync(new CheckChange { Check = check, Kind = CheckChangeKind.Uncleared, PreviousStatus = CheckStatus.Cleared, Reason = reason, ClearedOn = clearedOn }, cancellationToken);

        return new CheckResult { Check = check };
    }

    /// <inheritdoc />
    public Task<bool> MarkStaleAsync(CheckRecord check, CancellationToken cancellationToken = default)
        => MoveAsync(check, CheckStatus.Stale, CheckChangeKind.Stale, null, cancellationToken);

    /// <inheritdoc />
    public async Task<int> MarkStaleChecksAsync(int ageInDays, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ageInDays, 1);

        var before = (await GetLocalTodayAsync()).AddDays(-ageInDays);
        var reason = S["Not cashed within {0} days.", ageInDays].Value;
        var count = 0;

        while (true)
        {
            var batch = await _manager.ListPrintedBeforeAsync(before, StaleBatchSize, cancellationToken);
            var moved = 0;

            foreach (var check in batch)
            {
                if (await MoveAsync(check, CheckStatus.Stale, CheckChangeKind.Stale, reason, cancellationToken))
                {
                    moved++;
                }
            }

            count += moved;

            // A batch that moved nothing would be read again: stop.
            if (batch.Count < StaleBatchSize || moved == 0)
            {
                return count;
            }
        }
    }

    private async Task<bool> MoveAsync(CheckRecord check, CheckStatus status, CheckChangeKind kind, string reason, CancellationToken cancellationToken)
        => (await ChangeAsync(check, status, kind, reason, askHandlers: false, voidedOn: null, clearedOn: null, apply: null, cancellationToken)).Succeeded;

    private async Task<CheckResult> ChangeAsync(
        CheckRecord check,
        CheckStatus status,
        CheckChangeKind kind,
        string reason,
        bool askHandlers,
        DateOnly? voidedOn,
        DateOnly? clearedOn,
        Action<CheckRecord> apply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(check);

        // History keeps its record of what happened at the bank; only printing and reissuing it are refused.
        if (!CheckStatuses.Transitions.CanMove(check.Status, status))
        {
            return new CheckResult { Errors = [S["{0} is {1}: that can't be done.", Describe(check), check.Status.ToString()]] };
        }

        if (askHandlers)
        {
            var refusal = await AskHandlersAsync(new CheckChangingContext(check, kind, status) { Reason = reason, VoidedOn = voidedOn, ClearedOn = clearedOn }, cancellationToken);

            if (refusal is not null)
            {
                return refusal;
            }
        }

        var previous = check.Status;

        apply?.Invoke(check);
        check.Status = status;
        check.StatusReason = reason ?? check.StatusReason;

        if (status is CheckStatus.Voided or CheckStatus.StopPayment or CheckStatus.Stale)
        {
            check.ClosedUtc = _clock.UtcNow;
        }

        await _manager.UpdateAsync(check, cancellationToken: cancellationToken);
        await NotifyAsync(new CheckChange { Check = check, Kind = kind, PreviousStatus = previous, Reason = reason, VoidedOn = voidedOn, ClearedOn = clearedOn }, cancellationToken);

        return new CheckResult { Check = check };
    }

    // Asks every handler; a refusal stops the change before anything is saved.
    private async Task<CheckResult> AskHandlersAsync(CheckChangingContext context, CancellationToken cancellationToken)
    {
        foreach (var handler in _handlers)
        {
            await handler.ChangingAsync(context, cancellationToken);
        }

        return context.IsRefused ? new CheckResult { Errors = context.Errors.ToList() } : null;
    }

    private async Task<long> NextFreeNumberAsync(string bankAccountId, CancellationToken cancellationToken)
    {
        var options = new NumberSequenceOptions
        {
            FirstNumber = DefaultFirstNumber,
            SeedAsync = async token => (await GetHighestNumberAsync(bankAccountId, token)) ?? 0,
        };

        // A check written by hand may have taken a number of the series: the series moves past it. It ends, since each
        // number skipped belongs to a check.
        while (true)
        {
            var number = await _numbers.NextAsync(ChecksConstants.NumberSeries(bankAccountId), options, cancellationToken);

            if (await _manager.FindByNumberAsync(bankAccountId, number, cancellationToken) is null)
            {
                return number;
            }
        }
    }

    private string Describe(CheckRecord check)
        => check.Number > 0 ? S["Check {0}", check.Number] : S["The check to {0}", check.PayeeName];

    private Task<long?> GetHighestNumberAsync(string bankAccountId, CancellationToken cancellationToken)
        => _manager.GetHighestNumberAsync(bankAccountId, cancellationToken);

    private async Task<DateOnly> GetLocalTodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

    private async Task NotifyAsync(CheckChange change, CancellationToken cancellationToken)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        change.Actor = user?.Identity?.Name;
        change.ActorId = user is { Identity.IsAuthenticated: true } ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

        foreach (var observer in _observers)
        {
            await observer.OnChangedAsync(change, cancellationToken);
        }
    }
}
