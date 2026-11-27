using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Sends an estimate as the estimate's <b>Send</b> button does: when the site's e-mail is set up,
/// <see cref="EstimateSender"/> numbers a draft and e-mails it to the bill-to's address with its PDF and the link where
/// the customer accepts or declines it; otherwise <see cref="IEstimateCommandService.SendAsync"/> only numbers it, so staff
/// share its link from its page. The signed link is never returned: anyone holding it can accept the offer.
/// </summary>
internal sealed class SendEstimateTool : InvoiceAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendEstimate";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "estimate": {
          "type": "string",
          "description": "The estimate's number, such as EST-1001, or its id from draftEstimate or listRecords."
        }
      },
      "required": ["estimate"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends an estimate to its customer: numbers a draft and locks its offer, then e-mails it to the customer's address with its PDF and the link where they accept or decline it. When the site's e-mail is not set up it only numbers it, so staff share its link from its page. Also e-mails an estimate waiting for an answer again. Cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, InvoicingPermissions.ManageEstimates))
        {
            return NotAllowedMessage;
        }

        var estimate = await EstimateToolSupport.FindAsync(services, Read(arguments, "estimate"), cancellationToken);

        if (estimate is null)
        {
            return "Nothing was sent: no estimate has that number or id.";
        }

        // The estimate commands discard the session's unsaved work when they refuse, so earlier work is written first.
        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        var sender = services.GetRequiredService<EstimateSender>();
        var emailed = sender.CanSend;
        EstimateCommandResult result;

        try
        {
            // As the Send button: e-mailed when e-mail is set up, otherwise only numbered so its link can be shared.
            result = emailed
                ? await sender.SendAsync(estimate.ItemId, to: null, cancellationToken)
                : await services.GetRequiredService<IEstimateCommandService>().SendAsync(estimate.ItemId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkSessionClosed(services);

            return $"Sending estimate {estimate.Number ?? estimate.ItemId} stopped with an error, and whether it was e-mailed is unknown. Check its page before trying again.";
        }

        var sent = result.Estimate ?? estimate;

        if (!result.Succeeded)
        {
            // Only a refused send command discards the session's work, and then the estimate is still a draft. An
            // estimate that was sent but couldn't be e-mailed (no address, e-mail off) kept the session, so later
            // changes in the message can still be saved.
            if (sent.Status == EstimateStatus.Draft)
            {
                MarkSessionClosed(services);
            }

            return Json(new
            {
                id = sent.ItemId,
                number = sent.Number,
                status = ToCamel(sent.Status.ToString()),
                errors = result.Errors,
                message = sent.Status == EstimateStatus.Draft
                    ? "The estimate was not sent; read the errors."
                    : $"Estimate {sent.Number} is {ToCamel(sent.Status.ToString())}, but it was not e-mailed; read the errors.",
            });
        }

        return Json(new
        {
            id = sent.ItemId,
            number = sent.Number,
            status = ToCamel(sent.Status.ToString()),
            emailed,
            emailedTo = emailed ? sent.BillTo?.Contact?.Email : null,
            currency = sent.Currency,
            total = sent.Totals?.Total ?? 0m,
            expiryDate = Date(sent.ExpiryDate),
            adminUrl = EstimateToolSupport.AdminPath(services, "EstimatesAdmin", sent.ItemId),
            message = emailed
                ? $"Estimate {sent.Number} for {Format(sent.Totals?.Total ?? 0m, sent.Currency)} was e-mailed to {sent.BillTo?.Contact?.Email}; it is valid until {Date(sent.ExpiryDate)}."
                : $"Estimate {sent.Number} was numbered and is waiting for an answer, but the site's e-mail isn't set up, so it was not e-mailed. Staff copy its link from its page and share it.",
        });
    }
}
