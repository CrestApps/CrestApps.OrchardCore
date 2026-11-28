using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Adds to or takes from a worker's time-off balance, as the Adjust form of the worker's time-off page does.
/// </summary>
internal sealed class AdjustTimeOffBalanceTool : WorkforceAdminToolBase
{
    public const string ToolName = "adjustTimeOffBalance";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "workerId": { "type": "string", "description": "The worker's ID. Give this or workerName." },
        "workerName": { "type": "string", "description": "The worker's name. Give this or workerId." },
        "policy": { "type": "string", "description": "The time-off policy, by name or ID, from getWorkerTimeOff." },
        "kind": { "type": "string", "enum": ["Adjustment", "Opening", "Carryover", "Grant", "Payout"], "description": "Adjustment corrects the balance (the default); Opening sets a starting balance; Carryover brings days over from last year; Grant gives an amount that expires on expiresOn; Payout takes paid-out time off away." },
        "amount": { "type": "number", "description": "The amount in the type's unit (days or hours). Negative takes time away; a payout is always taken away." },
        "effectiveDate": { "type": "string", "description": "Optional. The day it counts from (yyyy-MM-dd); today by default." },
        "expiresOn": { "type": "string", "description": "For a Grant only: the last day it can be used (yyyy-MM-dd)." },
        "note": { "type": "string", "description": "Why, kept on the ledger. Giving the same adjustment twice needs a different note, otherwise the second call is taken as a retry and changes nothing." }
      },
      "required": ["policy", "amount", "note"],
      "additionalProperties": false
    }
    """);

    private static readonly TimeOffLedgerKind[] _kinds =
    [
        TimeOffLedgerKind.Adjustment,
        TimeOffLedgerKind.Opening,
        TimeOffLedgerKind.Carryover,
        TimeOffLedgerKind.Grant,
        TimeOffLedgerKind.Payout,
    ];

    public override string Name => ToolName;

    public override string Description => "Adds to or takes from a worker's time-off balance for a policy on a date, with a note and an optional expiry. It is written to the ledger and cannot be edited afterwards, only offset.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.AdjustTimeOffBalances;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (worker, error) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var policyText = WorkforceAdminTools.Read(arguments, "policy");
        var policies = (await services.GetRequiredService<ITimeOffPolicyManager>().GetAllAsync(cancellationToken)).ToList();
        var policy = policyText is null
            ? null
            : policies.FirstOrDefault(item => item.ItemId == policyText) ?? policies.FirstOrDefault(item => WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(policyText));

        if (policy is null)
        {
            return $"No time-off policy is called '{policyText}'. The policies are: {string.Join(", ", policies.Select(item => item.Name))}.";
        }

        var amount = WorkforceAdminTools.ReadDecimal(arguments, "amount");

        if (amount is null || amount.Value == 0)
        {
            return "Give the amount to add (positive) or take away (negative).";
        }

        var kind = TimeOffLedgerKind.Adjustment;

        if (WorkforceAdminTools.Has(arguments, "kind") && (!WorkforceAdminTools.TryReadEnum(arguments, "kind", out kind) || !_kinds.Contains(kind)))
        {
            return "The kind must be Adjustment, Opening, Carryover, Grant or Payout.";
        }

        var note = WorkforceAdminTools.Read(arguments, "note");

        if (note is null)
        {
            return "Say why the balance changes; the note is kept on the ledger.";
        }

        var effectiveDate = WorkforceAdminTools.ReadDate(arguments, "effectiveDate") ?? await WorkforceAdminTools.TodayAsync(services);
        var expiresOn = WorkforceAdminTools.ReadDate(arguments, "expiresOn");
        var adjustment = new TimeOffAdjustment
        {
            WorkerId = worker.ItemId,
            PolicyId = policy.ItemId,
            Kind = kind,
            Amount = amount.Value,
            EffectiveDate = effectiveDate,
            ExpiresOn = expiresOn,
            Note = note,

            // A retried tool call carries the same arguments, so it finds the entry the first call wrote and adds nothing.
            IdempotencyKey = "ai:" + KeyOf(worker.ItemId, policy.ItemId, kind, amount.Value, effectiveDate, expiresOn, note),
        };
        var result = await services.GetRequiredService<ITimeOffBalanceService>().AdjustAsync(adjustment, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return "The balance wasn't changed: " + string.Join(" ", result.Errors);
        }

        var balance = await services.GetRequiredService<ITimeOffBalanceService>().GetBalanceAsync(worker.ItemId, policy.ItemId, effectiveDate, cancellationToken);

        return WorkforceAdminTools.Json(new
        {
            ledgerEntryId = result.Value.ItemId,
            worker = worker.DisplayName,
            policy = policy.Name,
            kind = result.Value.Kind.ToString(),
            amount = result.Value.Amount,
            effectiveDate = WorkforceAdminTools.Day(result.Value.EffectiveDate),
            expiresOn = WorkforceAdminTools.Day(result.Value.ExpiresOn),
            availableNow = balance?.Available,
        });
    }

    private static string KeyOf(string workerId, string policyId, TimeOffLedgerKind kind, decimal amount, DateOnly effectiveDate, DateOnly? expiresOn, string note)
    {
        var text = string.Join('|', workerId, policyId, kind.ToString(), amount.ToString(CultureInfo.InvariantCulture), WorkforceAdminTools.Day(effectiveDate), WorkforceAdminTools.Day(expiresOn), note);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }
}
