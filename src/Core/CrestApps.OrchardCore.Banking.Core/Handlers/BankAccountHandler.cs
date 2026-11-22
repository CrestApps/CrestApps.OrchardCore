using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Banking.Core.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Banking.Core.Handlers;

/// <summary>
/// Stamps bank accounts, checks them, and tells the <see cref="IBankAccountObserver"/>s what each save changed.
/// </summary>
public sealed class BankAccountHandler : CatalogEntryHandlerBase<BankAccount>
{
    private readonly BankAccountChangeTracker _tracker;
    private readonly IEnumerable<IBankAccountObserver> _observers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankAccountHandler"/> class.
    /// </summary>
    /// <param name="tracker">The changes of this request.</param>
    /// <param name="observers">The observers of account changes.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for who made a change.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BankAccountHandler(
        BankAccountChangeTracker tracker,
        IEnumerable<IBankAccountObserver> observers,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<BankAccountHandler> stringLocalizer)
    {
        _tracker = tracker;
        _observers = observers;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<BankAccount> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;
        context.Model.Status = BankAccountStatus.Unverified;
        context.Model.Country ??= BankingConstants.Countries.UnitedStates;
        context.Model.Currency ??= "USD";

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<BankAccount> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidatingAsync(ValidatingContext<BankAccount> context, CancellationToken cancellationToken = default)
    {
        var account = context.Model;

        if (string.IsNullOrWhiteSpace(account.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(BankAccount.Name)]));
        }

        if (string.IsNullOrWhiteSpace(account.HolderName))
        {
            context.Result.Fail(new ValidationResult(S["Enter the name of the account holder as the bank knows it."], [nameof(BankAccount.HolderName)]));
        }

        if (string.IsNullOrEmpty(account.Country) || account.Country.Length != 2 || !account.Country.All(char.IsAsciiLetter))
        {
            context.Result.Fail(new ValidationResult(S["Enter the bank's country as a two-letter code, such as US."], [nameof(BankAccount.Country)]));
        }

        if (string.IsNullOrEmpty(account.Currency) || account.Currency.Length != 3 || !account.Currency.All(char.IsAsciiLetter))
        {
            context.Result.Fail(new ValidationResult(S["Enter the currency as a three-letter code, such as USD."], [nameof(BankAccount.Currency)]));
        }

        // An account may be stored without its numbers, as an import without sensitive data leaves it; it can then be
        // neither checked nor paid until someone enters them.
        if (account.Status is BankAccountStatus.VerificationSent or BankAccountStatus.Verified && !BankAccountNumbers.HasNumbers(account))
        {
            context.Result.Fail(new ValidationResult(S["Enter the routing and account numbers before the account is checked."], [nameof(BankAccount.AccountNumber)]));
        }

        if (!BankAccountNumbers.IsValidLastFour(account.AccountNumberLastFour))
        {
            context.Result.Fail(new ValidationResult(S["Enter at most the last four letters or digits of the account number."], [nameof(BankAccount.AccountNumberLastFour)]));
        }

        if (string.IsNullOrEmpty(account.Owner.Kind) != string.IsNullOrEmpty(account.Owner.Id))
        {
            context.Result.Fail(new ValidationResult(S["Choose who owns the account, or leave it empty for one of the business's own accounts."], [nameof(BankAccount.Owner)]));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task CreatedAsync(CreatedContext<BankAccount> context, CancellationToken cancellationToken = default)
    {
        var (kinds, previous, reason) = _tracker.Take(context.Model);

        // A new account's number and status are part of creating it.
        return NotifyAsync(context.Model, [BankAccountChangeKind.Created], previous, reason, cancellationToken);
    }

    /// <inheritdoc />
    public override Task UpdatedAsync(UpdatedContext<BankAccount> context, CancellationToken cancellationToken = default)
    {
        var (kinds, previous, reason) = _tracker.Take(context.Model);

        return NotifyAsync(context.Model, kinds.Count == 0 ? [BankAccountChangeKind.Updated] : kinds, previous, reason, cancellationToken);
    }

    /// <inheritdoc />
    public override Task DeletedAsync(DeletedContext<BankAccount> context, CancellationToken cancellationToken = default)
    {
        _tracker.Take(context.Model);

        return NotifyAsync(context.Model, [BankAccountChangeKind.Deleted], null, null, cancellationToken);
    }

    private async Task NotifyAsync(BankAccount account, IReadOnlyList<BankAccountChangeKind> kinds, BankAccountStatus? previous, string reason, CancellationToken cancellationToken)
    {
        var change = new BankAccountChange
        {
            Account = account,
            Kinds = kinds,
            PreviousStatus = previous,
            Actor = _httpContextAccessor.HttpContext?.User?.Identity?.Name,
            ActorId = _httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null,
            Reason = reason,
        };

        foreach (var observer in _observers)
        {
            await observer.OnChangedAsync(change, cancellationToken);
        }
    }
}
