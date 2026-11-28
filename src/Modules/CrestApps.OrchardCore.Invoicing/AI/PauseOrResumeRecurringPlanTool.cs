using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Pauses or resumes a recurring invoice plan as the plan's <b>Pause</b> and <b>Resume</b> buttons do, through
/// <see cref="IRecurringPlanService"/>, which takes the plan's lock: a paused plan skips the runs due while it is paused,
/// and a resumed one continues from its next date on or after today (in the user's or the site's time zone). Either can
/// be undone with the other. It asks for approval: once a plan is resumed, the recurring task makes its next invoice as soon
/// as it is due (today, when an occurrence falls today) and, as the plan's mode says, issues it, e-mails it or charges the
/// customer's saved payment method, with nobody asked again.
/// </summary>
internal sealed class PauseOrResumeRecurringPlanTool : InvoiceAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "pauseOrResumeRecurringPlan";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "planId": {
          "type": "string",
          "description": "The recurring plan's id, from listRecords with kind recurringInvoicePlan."
        },
        "action": {
          "type": "string",
          "enum": ["pause", "resume"],
          "description": "pause an active plan, or resume a paused one."
        }
      },
      "required": ["planId", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Pauses a recurring invoice plan (the invoices due while it is paused are skipped, not made later) or resumes a paused one from its next date on or after today. Returns its status, its next run date and what each run does (draft, issue, issue and e-mail, or issue and charge the saved card). Once resumed, the next invoice is made, and issued, e-mailed or charged as the plan says, as soon as it is due, today included.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, InvoicingPermissions.ManageRecurringInvoices))
        {
            return NotAllowedMessage;
        }

        var planId = Read(arguments, "planId");

        if (planId is null || await services.GetRequiredService<IRecurringPlanManager>().FindByIdAsync(planId, cancellationToken) is null)
        {
            return "Nothing was changed: no recurring plan has that id. Find it with listRecords, kind recurringInvoicePlan.";
        }

        var action = Read(arguments, "action")?.ToLowerInvariant();

        if (action is not ("pause" or "resume"))
        {
            return "Nothing was changed: action is pause or resume.";
        }

        // The plan service discards the session's unsaved work when the plan changed meanwhile, so earlier work is written
        // first.
        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        var plans = services.GetRequiredService<IRecurringPlanService>();
        RecurringPlanResult result;

        try
        {
            result = action == "pause"
                ? await plans.PauseAsync(planId, cancellationToken)
                : await plans.ResumeAsync(planId, await TodayAsync(services), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkSessionClosed(services);

            return "Changing the recurring plan stopped with an error. Check its page before trying again.";
        }

        var plan = result.Plan;

        if (!result.Succeeded || plan is null)
        {
            MarkSessionClosed(services);

            return Json(new
            {
                id = planId,
                status = plan is null ? null : ToCamel(plan.Status.ToString()),
                errors = result.Errors,
                message = "Nothing was changed; read the errors.",
            });
        }

        var total = plan.Totals?.Total ?? 0m;

        return Json(new
        {
            id = plan.ItemId,
            name = plan.Name,
            status = ToCamel(plan.Status.ToString()),
            nextRunDate = Date(plan.NextRunDate),
            mode = ToCamel(plan.Mode.ToString()),
            currency = plan.Currency,
            totalPerRun = total,
            message = plan.Status == RecurringPlanStatus.Paused
                ? $"Plan '{plan.Name}' is paused: it makes no invoice until it is resumed, and the runs due meanwhile are skipped."
                : plan.NextRunDate is { } next
                    ? $"Plan '{plan.Name}' was resumed: on {Date(next)} it {DescribeRun(plan.Mode)} of {Format(total, plan.Currency)}."
                    : $"Plan '{plan.Name}' was resumed, but its schedule has no run left.",
        });
    }

    private static string DescribeRun(RecurringPlanMode mode)
        => mode switch
        {
            RecurringPlanMode.Issue => "issues an invoice",
            RecurringPlanMode.IssueAndSend => "issues and e-mails an invoice",
            RecurringPlanMode.IssueAndCharge => "issues an invoice and charges the customer's saved payment method",
            _ => "makes a draft invoice",
        };
}
