using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Core.Services;

/// <summary>
/// Pays a batch by check, when Checks is on: releasing it issues one check per disbursement in the check register,
/// for the same record and access scope, ready to print. <see cref="CheckDisbursementObserver"/> follows the checks.
/// </summary>
public sealed class CheckDisbursementProvider : IDisbursementProvider
{
    /// <summary>
    /// The source kind of checks that pay a disbursement with no source of its own.
    /// </summary>
    public const string DisbursementSourceKind = "Disbursement";

    private readonly ICheckService _checkService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckDisbursementProvider"/> class.
    /// </summary>
    /// <param name="checkService">The check rules.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckDisbursementProvider(ICheckService checkService, IStringLocalizer<CheckDisbursementProvider> stringLocalizer)
    {
        _checkService = checkService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name => DisbursementsConstants.Providers.Check;

    /// <inheritdoc />
    public LocalizedString DisplayName => S["Printed check"];

    /// <inheritdoc />
    public string Method => DisbursementsConstants.Methods.Check;

    /// <inheritdoc />
    public async Task<DisbursementSubmitResult> SubmitAsync(DisbursementBatch batch, IReadOnlyList<Disbursement> disbursements, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(disbursements);

        var result = new DisbursementSubmitResult { Succeeded = true };

        foreach (var disbursement in disbursements)
        {
            var hasSource = !string.IsNullOrEmpty(disbursement.SourceKind);
            var issued = await _checkService.IssueAsync(new CheckIssueRequest
            {
                BankAccountId = disbursement.SourceBankAccountId,
                Payee = disbursement.Payee,
                PayeeName = disbursement.PayeeName,
                PayeeAddress = disbursement.PayeeAddress,
                Amount = disbursement.Amount,
                Currency = disbursement.Currency,
                CheckDate = batch.EffectiveDate,
                Memo = disbursement.Memo,
                SourceKind = hasSource ? disbursement.SourceKind : DisbursementSourceKind,
                SourceId = hasSource ? disbursement.SourceId : disbursement.ItemId,
                AccessScope = hasSource ? disbursement.AccessScope : null,
            }, cancellationToken);

            if (issued.Succeeded)
            {
                result.Statuses[disbursement.ItemId] = DisbursementStatus.Submitted;
                result.CheckIds[disbursement.ItemId] = issued.Check.ItemId;
            }
            else
            {
                result.Statuses[disbursement.ItemId] = DisbursementStatus.Failed;
                result.FailureReasons[disbursement.ItemId] = string.Join(" ", issued.Errors);
            }
        }

        return result;
    }
}

/// <summary>
/// Follows the checks that pay disbursements: a cleared check settles its disbursement, a voided or stopped one
/// cancels it, and a reissue moves it onto the new check.
/// </summary>
public sealed class CheckDisbursementObserver : ICheckObserver
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckDisbursementObserver"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, which give the disbursement services when a check changes (they
    /// depend on the check rules through the check provider, so they can't be taken here).</param>
    public CheckDisbursementObserver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task OnChangedAsync(CheckChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (change.Kind is not (CheckChangeKind.Cleared or CheckChangeKind.Voided or CheckChangeKind.StopPayment or CheckChangeKind.Reissued))
        {
            return;
        }

        var manager = _serviceProvider.GetRequiredService<IDisbursementManager>();
        var disbursement = await manager.FindByCheckIdAsync(change.Check.ItemId, cancellationToken);

        if (disbursement is null)
        {
            return;
        }

        var service = _serviceProvider.GetRequiredService<IDisbursementService>();

        switch (change.Kind)
        {
            case CheckChangeKind.Cleared:
                await service.MarkSettledAsync(disbursement, cancellationToken);
                break;
            case CheckChangeKind.Reissued when change.ReplacedBy is not null:
                disbursement.CheckId = change.ReplacedBy.ItemId;
                await manager.UpdateAsync(disbursement, cancellationToken: cancellationToken);
                break;
            case CheckChangeKind.Voided or CheckChangeKind.StopPayment:
                await service.MarkClosedAsync(disbursement, DisbursementStatus.Canceled, null, change.Reason, cancellationToken);
                break;
        }
    }
}
