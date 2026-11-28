using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Subscriptions;
using CrestApps.OrchardCore.Reports.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Emails a subscription's report now, for the period before now, without moving its schedule, as Save and send does.
/// That button runs the editor's checks for the person sending first, so this tool does too: the caller must be able to
/// view the report, and to export it when a file is attached.
/// </summary>
public sealed class SendReportSubscriptionTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendReportSubscription";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the subscription to send now, from listReportSubscriptions."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Emails a subscription's report to its recipients now, for the period before now, without changing its schedule. Returns the error when the email could not be sent.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ReportSubscriptionPermissions.ManageReportSubscriptions))
        {
            return NotAllowedMessage;
        }

        var id = ReadString(arguments, "id");

        if (id is null)
        {
            return "Give the subscription's id in id. Call listReportSubscriptions to find it.";
        }

        var subscription = await services.GetRequiredService<IReportSubscriptionStore>().FindByIdAsync(id, cancellationToken);

        if (subscription is null)
        {
            return $"There is no report subscription with the id '{id}'. Call listReportSubscriptions to find it.";
        }

        // Save and send checks what is stored as the person sending before it sends; nothing is changed here.
        var errors = await services.GetRequiredService<ReportSubscriptionEditor>().ApplyAsync(user, new ReportSubscriptionInput
        {
            Name = subscription.Name,
            ReportName = subscription.ReportName,
            Frequency = subscription.Frequency,
            DayOfWeek = subscription.DayOfWeek,
            DayOfMonth = subscription.DayOfMonth,
            Hour = subscription.Hour,
            Recipients = subscription.Recipients,
            AttachmentFormat = subscription.AttachmentFormat,
            IsEnabled = subscription.IsEnabled,
        }, subscription: null);

        if (errors.Count > 0)
        {
            return Json(new
            {
                sent = false,
                errors = errors.Select(error => error.Message).ToList(),
                note = "Nothing was emailed. You must be able to view the report, and to export it when a file is attached; fix the subscription with saveReportSubscription.",
            });
        }

        var service = services.GetRequiredService<ReportSubscriptionService>();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var sent = await service.SendAsync(subscription, now, reschedule: false, cancellationToken);
        var timeZone = await service.GetTimeZoneAsync();
        var (fromUtc, toUtc) = ReportSubscriptionSchedule.GetPeriod(subscription, now, timeZone);

        return Json(new
        {
            sent,
            id = subscription.ItemId,
            name = subscription.Name,
            recipients = ReportSubscriptionEditor.SplitRecipients(subscription.Recipients),
            period = new
            {
                from = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, timeZone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                to = TimeZoneInfo.ConvertTimeFromUtc(toUtc, timeZone).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            },
            error = sent ? null : subscription.LastError,
            nextRun = await ToLocalAsync(services, subscription.NextRunUtc),
        });
    }
}
