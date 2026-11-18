using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Subscriptions;
using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using CrestApps.OrchardCore.Reports.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Adds a report subscription, or changes one by id, through the checks of the subscription editor: a built-in report the
/// caller may view, up to 50 email addresses, an hour and a day in range, and a file format the caller may export the
/// report in. The caller becomes the person who last saved it, whose export permission a file attachment needs at each
/// run. It emails report data to the recipients, so it asks for approval.
/// </summary>
public sealed class SaveReportSubscriptionTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveReportSubscription";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the subscription to change, from listReportSubscriptions. Leave it out to add a subscription."
        },
        "name": {
          "type": "string",
          "description": "What the subscription is called. When left empty, the report's title."
        },
        "report": {
          "type": "string",
          "description": "The technical name of the built-in report to email, from listReports, such as order-sales. Designed reports cannot be subscribed to. Required for a new subscription."
        },
        "frequency": {
          "type": "string",
          "enum": ["Daily", "Weekly", "Monthly"],
          "description": "How often it goes out: Daily for the day before, Weekly for the seven days before, Monthly for the month before. Weekly when new."
        },
        "dayOfWeek": {
          "type": "string",
          "enum": ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
          "description": "The day a weekly report goes out. Monday when new."
        },
        "dayOfMonth": {
          "type": "integer",
          "minimum": 1,
          "maximum": 28,
          "description": "The day of the month a monthly report goes out, from 1 to 28. 1 when new."
        },
        "hour": {
          "type": "integer",
          "minimum": 0,
          "maximum": 23,
          "description": "The hour it goes out, from 0 to 23, in the site's time zone. 7 when new."
        },
        "recipients": {
          "type": "array",
          "items": { "type": "string" },
          "maxItems": 50,
          "description": "The email addresses it goes to, at most 50. Replaces the current list. Required for a new subscription."
        },
        "attachmentFormat": {
          "type": "string",
          "description": "The format of the file attached to the email, such as csv, xlsx or pdf, or none for no file. Attaching a file needs the permission to export the report."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether it goes out on its schedule. A new subscription is enabled unless this is false."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a schedule that emails a built-in report daily, weekly or monthly to up to 50 addresses, or updates one by id, including enabling or disabling it. The report is emailed for the period before each run; it needs the Email feature to be configured.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ReportSubscriptionPermissions.ManageReportSubscriptions))
        {
            return NotAllowedMessage;
        }

        var store = services.GetRequiredService<IReportSubscriptionStore>();
        var id = ReadString(arguments, "id");
        var existing = id is null ? null : await store.FindByIdAsync(id, cancellationToken);

        if (id is not null && existing is null)
        {
            return $"There is no report subscription with the id '{id}'. Call listReportSubscriptions to find it.";
        }

        if (ReadInput(arguments, existing, out var input) is { } problem)
        {
            return problem;
        }

        var editor = services.GetRequiredService<ReportSubscriptionEditor>();
        var service = services.GetRequiredService<ReportSubscriptionService>();
        var clock = services.GetRequiredService<IClock>();
        var subscription = existing ?? new ReportSubscription
        {
            ItemId = IdGenerator.GenerateId(),
            CreatedUtc = clock.UtcNow,
            CreatedBy = user.Identity?.Name,
        };

        var errors = await editor.ApplyAsync(user, input, subscription);

        if (errors.Count > 0)
        {
            return Json(new
            {
                saved = false,
                errors = errors.Select(error => error.Message).ToList(),
                formats = services.GetRequiredService<IReportExportManager>().GetFormats().Select(format => format.Name).ToList(),
                hint = "Call listReports for the reports you may view.",
            });
        }

        await service.ScheduleAsync(subscription);

        if (existing is null)
        {
            await store.CreateAsync(subscription, cancellationToken);
        }
        else
        {
            subscription.ModifiedUtc = clock.UtcNow;
            subscription.ModifiedBy = user.Identity?.Name;
            await store.UpdateAsync(subscription, cancellationToken);
        }

        return Json(new
        {
            saved = true,
            created = existing is null,
            subscription = await ReportSubscriptionToolResults.DescribeAsync(services, subscription, utc => ToLocalAsync(services, utc)),
            canEmail = service.CanSend,
            note = service.CanSend ? null : ReportSubscriptionToolResults.NoEmailNote,
        });
    }

    // Reads the arguments over the stored values, as the editor shows them, or over the editor's defaults for a new one.
    private static string ReadInput(AIFunctionArguments arguments, ReportSubscription existing, out ReportSubscriptionInput input)
    {
        input = null;

        var frequency = existing?.Frequency ?? ReportSubscriptionFrequency.Weekly;
        var dayOfWeek = existing?.DayOfWeek ?? DayOfWeek.Monday;

        if (ReadString(arguments, "frequency") is { } frequencyText &&
            (!Enum.TryParse(frequencyText, ignoreCase: true, out frequency) || !Enum.IsDefined(frequency) || int.TryParse(frequencyText, out _)))
        {
            return "Give frequency as Daily, Weekly or Monthly.";
        }

        if (ReadString(arguments, "dayOfWeek") is { } dayText &&
            (!Enum.TryParse(dayText, ignoreCase: true, out dayOfWeek) || !Enum.IsDefined(dayOfWeek) || int.TryParse(dayText, out _)))
        {
            return "Give dayOfWeek as a day name, such as Monday.";
        }

        var dayOfMonth = ReadInt(arguments, "dayOfMonth");
        var hour = ReadInt(arguments, "hour");

        if ((Has(arguments, "dayOfMonth") && dayOfMonth is null) || (Has(arguments, "hour") && hour is null))
        {
            return "Give dayOfMonth and hour as whole numbers: a day from 1 to 28 and an hour from 0 to 23.";
        }

        var recipients = ReadStringList(arguments, "recipients");
        var attachmentFormat = existing?.AttachmentFormat;

        if (Has(arguments, "attachmentFormat"))
        {
            attachmentFormat = ReadString(arguments, "attachmentFormat");

            if (string.Equals(attachmentFormat, "none", StringComparison.OrdinalIgnoreCase))
            {
                attachmentFormat = null;
            }
        }

        input = new ReportSubscriptionInput
        {
            Name = Has(arguments, "name") ? ReadString(arguments, "name") : existing?.Name,
            ReportName = ReadString(arguments, "report") ?? existing?.ReportName,
            Frequency = frequency,
            DayOfWeek = dayOfWeek,
            DayOfMonth = dayOfMonth ?? existing?.DayOfMonth ?? 1,
            Hour = hour ?? existing?.Hour ?? 7,
            Recipients = recipients is null ? existing?.Recipients : string.Join(", ", recipients),
            AttachmentFormat = attachmentFormat,
            IsEnabled = ReadBool(arguments, "enabled") ?? existing?.IsEnabled ?? true,
        };

        return null;
    }
}
