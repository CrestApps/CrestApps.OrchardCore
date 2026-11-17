using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Banking.Disbursements.Recipes;

/// <summary>
/// Imports disbursements, such as the payments another system made before the move. Imported disbursements are
/// history: they are never batched or changed. One whose idempotency key already exists is skipped.
/// </summary>
internal sealed class DisbursementsRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The property that holds the disbursements.
    /// </summary>
    public const string DisbursementsProperty = "Disbursements";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IDisbursementManager _manager;

    public DisbursementsRecipeStep(IDisbursementManager manager)
        : base(DisbursementsConstants.RecipeStepName)
    {
        _manager = manager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var node in (context.Step[DisbursementsProperty] as JsonArray ?? []).OfType<JsonObject>())
        {
            var values = node.Deserialize<Disbursement>(JsonOptions);

            if (values is null || string.IsNullOrEmpty(values.IdempotencyKey) || string.IsNullOrEmpty(values.Method) || string.IsNullOrEmpty(values.Currency))
            {
                context.Errors.Add($"{DisbursementsConstants.RecipeStepName}: a disbursement needs an IdempotencyKey, a Method and a Currency.");

                continue;
            }

            if (await _manager.FindByIdempotencyKeyAsync(values.IdempotencyKey) is not null)
            {
                continue;
            }

            var disbursement = await _manager.NewAsync();

            if (!string.IsNullOrEmpty(values.ItemId))
            {
                disbursement.ItemId = values.ItemId;
            }

            disbursement.Payee = values.Payee;
            disbursement.PayeeName = values.PayeeName;
            disbursement.PayeeAddress = values.PayeeAddress;
            disbursement.IsBusinessPayee = values.IsBusinessPayee;
            disbursement.Amount = values.Amount;
            disbursement.Currency = values.Currency;
            disbursement.Method = values.Method;
            disbursement.Provider = values.Provider;
            disbursement.SourceBankAccountId = values.SourceBankAccountId;
            disbursement.DestinationBankAccountId = values.DestinationBankAccountId;
            disbursement.SourceKind = values.SourceKind;
            disbursement.SourceId = values.SourceId;
            disbursement.AccessScope = values.AccessScope;
            disbursement.Memo = values.Memo;
            disbursement.IdempotencyKey = values.IdempotencyKey;
            disbursement.Status = values.Status;
            disbursement.EffectiveDate = values.EffectiveDate;
            disbursement.TraceNumber = values.TraceNumber;
            disbursement.FailureCode = values.FailureCode;
            disbursement.FailureReason = values.FailureReason;
            disbursement.CreatedUtc = values.CreatedUtc;
            disbursement.SubmittedUtc = values.SubmittedUtc;
            disbursement.ClosedUtc = values.ClosedUtc;
            disbursement.IsHistorical = true;

            await _manager.CreateAsync(disbursement);
        }
    }
}
