using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Core.Services;

/// <summary>
/// Records money paid outside the system, such as by wire or cash: releasing the batch marks its disbursements paid.
/// </summary>
public sealed class ManualDisbursementProvider : IDisbursementProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualDisbursementProvider"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ManualDisbursementProvider(IStringLocalizer<ManualDisbursementProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name => DisbursementsConstants.Providers.Manual;

    /// <inheritdoc />
    public LocalizedString DisplayName => S["Paid outside the system"];

    /// <inheritdoc />
    public string Method => DisbursementsConstants.Methods.Manual;

    /// <inheritdoc />
    public Task<DisbursementSubmitResult> SubmitAsync(DisbursementBatch batch, IReadOnlyList<Disbursement> disbursements, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disbursements);

        var result = new DisbursementSubmitResult { Succeeded = true };

        foreach (var disbursement in disbursements)
        {
            result.Statuses[disbursement.ItemId] = DisbursementStatus.Settled;
        }

        return Task.FromResult(result);
    }
}
