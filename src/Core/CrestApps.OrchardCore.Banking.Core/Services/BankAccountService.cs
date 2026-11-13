using System.Security.Claims;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.SensitiveData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Banking.Core.Services;

/// <summary>
/// The default <see cref="IBankAccountService"/>.
/// </summary>
public sealed class BankAccountService : IBankAccountService
{
    private readonly ISensitiveValueProtector _protector;
    private readonly IBankRoutingValidator[] _validators;
    private readonly IBankAccountManager _manager;
    private readonly BankAccountChangeTracker _tracker;
    private readonly IEnumerable<IBankAccountObserver> _observers;
    private readonly IEnumerable<IBankAccountUsage> _usages;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankAccountService"/> class.
    /// </summary>
    /// <param name="protector">The protector of account numbers.</param>
    /// <param name="validators">The routing checks of each country.</param>
    /// <param name="verifiers">The verifiers.</param>
    /// <param name="manager">The account manager.</param>
    /// <param name="tracker">The changes of this request.</param>
    /// <param name="observers">The observers of account changes.</param>
    /// <param name="usages">The modules whose records point to accounts.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for who made a change.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger, which records every account number read.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BankAccountService(
        ISensitiveValueProtector protector,
        IEnumerable<IBankRoutingValidator> validators,
        IEnumerable<IBankAccountVerifier> verifiers,
        IBankAccountManager manager,
        BankAccountChangeTracker tracker,
        IEnumerable<IBankAccountObserver> observers,
        IEnumerable<IBankAccountUsage> usages,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        ILogger<BankAccountService> logger,
        IStringLocalizer<BankAccountService> stringLocalizer)
    {
        _protector = protector;
        _validators = validators.ToArray();
        Verifiers = verifiers.ToArray();
        _manager = manager;
        _tracker = tracker;
        _observers = observers;
        _usages = usages;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public IReadOnlyList<IBankAccountVerifier> Verifiers { get; }

    /// <inheritdoc />
    public IReadOnlyList<BankAccountNumberError> ApplyNumbers(BankAccount account, string routingNumber, string accountNumber)
    {
        ArgumentNullException.ThrowIfNull(account);

        var hasRouting = !string.IsNullOrWhiteSpace(routingNumber);
        var hasAccount = !string.IsNullOrWhiteSpace(accountNumber);

        if (!hasRouting && !hasAccount)
        {
            return [];
        }

        if (account.Status == BankAccountStatus.Closed)
        {
            return [new BankAccountNumberError(nameof(BankAccount.AccountNumber), S["A closed account can't be changed. Add a new account instead."])];
        }

        var country = (account.Country ?? string.Empty).Trim().ToUpperInvariant();
        var validator = _validators.FirstOrDefault(entry => string.Equals(entry.Country, country, StringComparison.OrdinalIgnoreCase));
        var errors = new List<BankAccountNumberError>();
        string routing = null;
        string number = null;

        if (hasRouting && !TryNormalize(validator, routingNumber, isRouting: true, out routing))
        {
            errors.Add(new BankAccountNumberError(nameof(BankAccount.RoutingNumber), validator is null
                ? S["Enter the routing number with letters and digits only."]
                : S["The routing number is not valid for a bank in {0}. Check each digit.", country]));
        }

        if (hasAccount && !TryNormalize(validator, accountNumber, isRouting: false, out number))
        {
            errors.Add(new BankAccountNumberError(nameof(BankAccount.AccountNumber), validator is null
                ? S["Enter the account number with letters and digits only, up to 34 of them."]
                : S["The account number is not valid for a bank in {0}.", country]));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var hadNumbers = !string.IsNullOrEmpty(account.RoutingNumber) || account.AccountNumber is { HasValue: true };
        var changed = false;

        if (routing is not null && !string.Equals(routing, account.RoutingNumber, StringComparison.Ordinal))
        {
            account.RoutingNumber = routing;
            changed = true;
        }

        if (number is not null && !IsSameNumber(account.AccountNumber, number))
        {
            account.AccountNumber = _protector.Protect(BankingConstants.AccountNumberPurpose, number);
            changed = true;
        }

        if (changed && hadNumbers)
        {
            account.NumberChangedUtc = _clock.UtcNow;
            _tracker.Record(account, BankAccountChangeKind.NumberChanged);

            // Payments must not go to a number nobody checked.
            if (account.Status != BankAccountStatus.Unverified)
            {
                Move(account, BankAccountStatus.Unverified, S["The account number changed."], verificationMethod: null);
            }
        }

        return [];
    }

    /// <inheritdoc />
    public bool TrySetStatus(BankAccount account, BankAccountStatus status, string reason = null, string verificationMethod = null)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (account.Status == status)
        {
            return true;
        }

        if (!BankAccountStatuses.Transitions.CanMove(account.Status, status))
        {
            return false;
        }

        Move(account, status, reason, verificationMethod);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ChangeStatusAsync(BankAccount account, BankAccountStatus status, string reason = null, string verificationMethod = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (account.Status == status)
        {
            return true;
        }

        if (!TrySetStatus(account, status, reason, verificationMethod))
        {
            return false;
        }

        await _manager.UpdateAsync(account, cancellationToken: cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<BankAccountVerificationResult> StartVerificationAsync(BankAccount account, string verifierName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var verifier = Verifiers.FirstOrDefault(entry => string.Equals(entry.Name, verifierName, StringComparison.OrdinalIgnoreCase));

        if (verifier is null)
        {
            return BankAccountVerificationResult.Refused(S["No such way to check an account is available."]);
        }

        if (account.Status is BankAccountStatus.Verified or BankAccountStatus.Closed)
        {
            return BankAccountVerificationResult.Refused(S["Only an account that isn't verified or closed can be checked."]);
        }

        if (!BankAccountNumbers.HasNumbers(account))
        {
            return BankAccountVerificationResult.Refused(S["Enter the routing and account numbers before the account is checked."]);
        }

        var result = await verifier.StartAsync(account, cancellationToken);

        if (result.Succeeded)
        {
            if (!TrySetStatus(account, result.Status, result.Message, verifier.Name))
            {
                return BankAccountVerificationResult.Refused(S["The account can't move to {0}.", result.Status.ToString()]);
            }

            account.VerificationMethod = verifier.Name;
            await _manager.UpdateAsync(account, cancellationToken: cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<string> RevealAccountNumberAsync(BankAccount account, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (!_protector.TryUnprotect(BankingConstants.AccountNumberPurpose, account.AccountNumber, out var clearText) || clearText is null)
        {
            return null;
        }

        var user = _httpContextAccessor.HttpContext?.User;
        var change = new BankAccountChange
        {
            Account = account,
            Kinds = [BankAccountChangeKind.NumberRevealed],
            Actor = user?.Identity?.Name,
            ActorId = user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null,
            Reason = reason,
        };

        // Written whether or not the Audit Trail is on, so every read leaves a trace. Never the number.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The account number of bank account {BankAccountId} was read by {Actor} ({ActorId}): {Reason}",
                account.ItemId,
                change.Actor ?? "the system",
                change.ActorId,
                reason);
        }

        foreach (var observer in _observers)
        {
            await observer.OnChangedAsync(change, cancellationToken);
        }

        return clearText;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUsagesAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        return await BankAccountUsages.GetReasonsAsync(_usages, account, cancellationToken);
    }

    private void Move(BankAccount account, BankAccountStatus status, string reason, string verificationMethod)
    {
        var previous = account.Status;

        account.Status = status;
        account.StatusChangedUtc = _clock.UtcNow;
        account.StatusReason = reason;

        if (status == BankAccountStatus.Verified)
        {
            account.VerifiedUtc = _clock.UtcNow;
            account.VerificationMethod = verificationMethod;
        }
        else if (status is BankAccountStatus.Unverified)
        {
            account.VerifiedUtc = null;
            account.VerificationMethod = null;
        }
        else if (status is BankAccountStatus.VerificationSent)
        {
            account.VerificationMethod = verificationMethod;
        }

        _tracker.Record(account, BankAccountChangeKind.StatusChanged, previous, reason);
    }

    private bool IsSameNumber(ProtectedValue stored, string number)
        => stored is { HasValue: true }
            && _protector.TryUnprotect(BankingConstants.AccountNumberPurpose, stored, out var current)
            && string.Equals(current, number, StringComparison.Ordinal);

    private static bool TryNormalize(IBankRoutingValidator validator, string value, bool isRouting, out string normalized)
    {
        if (validator is not null)
        {
            return isRouting
                ? validator.TryNormalizeRoutingNumber(value, out normalized)
                : validator.TryNormalizeAccountNumber(value, out normalized);
        }

        normalized = BankNumberFormat.Clean(value);

        if (BankNumberFormat.HasForm(normalized, 1, BankNumberFormat.MaxLength, digitsOnly: false))
        {
            return true;
        }

        normalized = null;

        return false;
    }
}
