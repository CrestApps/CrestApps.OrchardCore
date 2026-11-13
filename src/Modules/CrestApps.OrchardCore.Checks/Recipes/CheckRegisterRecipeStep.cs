using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Checks.Recipes;

/// <summary>
/// Imports a check register, such as the checks written by another system before the move. Imported checks are
/// history: they are never printed or changed, and they keep their numbers. A check already in the register (same
/// bank account and number) is skipped.
/// </summary>
internal sealed class CheckRegisterRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The property that holds the checks.
    /// </summary>
    public const string ChecksProperty = "Checks";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ICheckManager _manager;

    public CheckRegisterRecipeStep(ICheckManager manager)
        : base(ChecksConstants.RegisterRecipeStepName)
    {
        _manager = manager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var node in (context.Step[ChecksProperty] as JsonArray ?? []).OfType<JsonObject>())
        {
            var values = node.Deserialize<CheckRecord>(JsonOptions);

            if (values is null || string.IsNullOrEmpty(values.BankAccountId) || values.Number <= 0)
            {
                context.Errors.Add($"{ChecksConstants.RegisterRecipeStepName}: a check needs a BankAccountId and a Number.");

                continue;
            }

            if (await _manager.FindByNumberAsync(values.BankAccountId, values.Number) is not null)
            {
                continue;
            }

            var check = await _manager.NewAsync();

            if (!string.IsNullOrEmpty(values.ItemId))
            {
                check.ItemId = values.ItemId;
            }

            check.BankAccountId = values.BankAccountId;
            check.BankAccountName = values.BankAccountName;
            check.Number = values.Number;
            check.Payee = values.Payee;
            check.PayeeName = values.PayeeName;
            check.PayeeAddress = values.PayeeAddress;
            check.Amount = values.Amount;
            check.Currency = values.Currency;
            check.CheckDate = values.CheckDate;
            check.Memo = values.Memo;
            check.SourceKind = values.SourceKind;
            check.SourceId = values.SourceId;
            check.Status = values.Status;
            check.IssuedUtc = values.IssuedUtc;
            check.PrintedUtc = values.PrintedUtc;
            check.ClosedUtc = values.ClosedUtc;
            check.StatusReason = values.StatusReason;
            check.ClearedOn = values.ClearedOn;
            check.ReissuedById = values.ReissuedById;
            check.ReissueOfId = values.ReissueOfId;
            check.IsHistorical = true;

            await _manager.CreateAsync(check);
        }
    }
}
