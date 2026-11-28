using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Subscriptions;
using CrestApps.OrchardCore.Reports.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Lists the report subscriptions, as Reports › Subscriptions does: by name, with a search on the name.
/// </summary>
public sealed class ListReportSubscriptionsTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listReportSubscriptions";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "Return only this subscription."
        },
        "query": {
          "type": "string",
          "description": "Only list the subscriptions whose name contains this text."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of subscriptions per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the scheduled report emails with their report, frequency, recipients, attachment format, enabled state, next run, last run and last error, with optional search and paging.";

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
        var canEmail = services.GetRequiredService<ReportSubscriptionService>().CanSend;
        var id = ReadString(arguments, "id");

        if (id is not null)
        {
            var subscription = await store.FindByIdAsync(id, cancellationToken);

            if (subscription is null)
            {
                return $"There is no report subscription with the id '{id}'. Call listReportSubscriptions to find it.";
            }

            return Json(new
            {
                subscription = await ReportSubscriptionToolResults.DescribeAsync(services, subscription, utc => ToLocalAsync(services, utc)),
                canEmail,
                note = canEmail ? null : ReportSubscriptionToolResults.NoEmailNote,
            });
        }

        var (page, pageSize) = ReadPaging(arguments);
        var (total, subscriptions) = await store.PageAsync(ReadString(arguments, "query"), (page - 1) * pageSize, pageSize, cancellationToken);
        var items = new List<object>();

        foreach (var subscription in subscriptions)
        {
            items.Add(await ReportSubscriptionToolResults.DescribeAsync(services, subscription, utc => ToLocalAsync(services, utc)));
        }

        return Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = page * pageSize < total ? true : (bool?)null,
            canEmail,
            note = canEmail ? null : ReportSubscriptionToolResults.NoEmailNote,
        });
    }
}
