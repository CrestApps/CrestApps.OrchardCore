using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Disbursements.Core.Nacha;

/// <summary>
/// Checks a payee's bank account with a zero-amount test entry (a prenote) in the next bank file sent from the
/// business's account set up for bank files. The account waits as Verification sent: it becomes Verified when the
/// batch settles without a return, and Failed when the bank returns the entry (<see cref="NachaPrenoteObserver"/>).
/// </summary>
public sealed class NachaPrenoteVerifier : IBankAccountVerifier
{
    /// <summary>
    /// The verifier's name.
    /// </summary>
    public const string VerifierName = "Prenote";

    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="NachaPrenoteVerifier"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, which give the disbursement services when a check starts (the
    /// bank account service depends on the verifiers, so they can't be taken here).</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public NachaPrenoteVerifier(IServiceProvider serviceProvider, IStringLocalizer<NachaPrenoteVerifier> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name => VerifierName;

    /// <inheritdoc />
    public LocalizedString DisplayName => S["Test entry (prenote)"];

    /// <inheritdoc />
    public async Task<BankAccountVerificationResult> StartAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (account.Owner.IsEmpty)
        {
            return BankAccountVerificationResult.Refused(S["A test entry checks a payee's account, not one of the business's own."]);
        }

        var bankAccounts = _serviceProvider.GetRequiredService<IBankAccountManager>();
        var origin = (await bankAccounts.ListByOwnersAsync([default], cancellationToken))
            .Where(entry => entry.Status != BankAccountStatus.Closed && entry.TryGet<NachaOriginSettings>(out var settings) && settings.Enabled)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (origin is null)
        {
            return BankAccountVerificationResult.Refused(S["Set up bank files on one of the business's accounts first."]);
        }

        // A new number gets a new test entry; asking again for the same number gives the same one.
        var version = (account.NumberChangedUtc ?? account.CreatedUtc).Ticks.ToString(CultureInfo.InvariantCulture);
        var result = await _serviceProvider.GetRequiredService<IDisbursementService>().CreateAsync(new DisbursementRequest
        {
            Payee = account.Owner,
            PayeeName = account.HolderName,
            Amount = 0,
            Currency = account.Currency,
            Method = DisbursementsConstants.Methods.Ach,
            SourceBankAccountId = origin.ItemId,
            DestinationBankAccountId = account.ItemId,
            SourceKind = DisbursementsConstants.PrenoteSourceKind,
            SourceId = account.ItemId + ":" + version,
            Memo = S["Test entry"],
            IsPrenote = true,
        }, cancellationToken);

        return result.Succeeded
            ? BankAccountVerificationResult.Started()
            : BankAccountVerificationResult.Refused(string.Join(" ", result.Errors));
    }
}

/// <summary>
/// Finishes a prenote check: a settled test entry verifies the account, a returned one fails it.
/// </summary>
public sealed class NachaPrenoteObserver : IDisbursementObserver
{
    private readonly IBankAccountManager _bankAccounts;
    private readonly IBankAccountService _bankAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NachaPrenoteObserver"/> class.
    /// </summary>
    /// <param name="bankAccounts">The bank accounts.</param>
    /// <param name="bankAccountService">The bank account rules.</param>
    public NachaPrenoteObserver(IBankAccountManager bankAccounts, IBankAccountService bankAccountService)
    {
        _bankAccounts = bankAccounts;
        _bankAccountService = bankAccountService;
    }

    /// <inheritdoc />
    public async Task OnChangedAsync(DisbursementChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        var disbursement = change.Disbursement;

        if (!disbursement.IsPrenote || change.Kind is not (DisbursementChangeKind.Settled or DisbursementChangeKind.Returned) || string.IsNullOrEmpty(disbursement.DestinationBankAccountId))
        {
            return;
        }

        var account = await _bankAccounts.FindByIdAsync(disbursement.DestinationBankAccountId, cancellationToken);

        // The test entry checked the number it was sent to; a number changed since is checked again.
        if (account is null || account.Status != BankAccountStatus.VerificationSent)
        {
            return;
        }

        if (change.Kind == DisbursementChangeKind.Settled)
        {
            await _bankAccountService.ChangeStatusAsync(account, BankAccountStatus.Verified, "Test entry settled without a return", NachaPrenoteVerifier.VerifierName, cancellationToken);
        }
        else
        {
            await _bankAccountService.ChangeStatusAsync(account, BankAccountStatus.Failed, $"Test entry returned: {disbursement.FailureCode} {disbursement.FailureReason}".Trim(), NachaPrenoteVerifier.VerifierName, cancellationToken);
        }
    }
}
