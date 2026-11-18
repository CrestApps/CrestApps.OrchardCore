using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Subscriptions;
using CrestApps.OrchardCore.Reports.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Deletes a report subscription, as Reports › Subscriptions does.
/// </summary>
public sealed class DeleteReportSubscriptionTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteReportSubscription";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the subscription to delete, from listReportSubscriptions."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a report subscription so its report is no longer emailed.";

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

        var store = services.GetRequiredService<IReportSubscriptionStore>();
        var subscription = await store.FindByIdAsync(id, cancellationToken);

        if (subscription is null)
        {
            return $"There is no report subscription with the id '{id}'. Call listReportSubscriptions to find it.";
        }

        await store.DeleteAsync(subscription, cancellationToken);

        return Json(new
        {
            deleted = true,
            id = subscription.ItemId,
            name = subscription.Name,
            report = subscription.ReportName,
        });
    }
}
