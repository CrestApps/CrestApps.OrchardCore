using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Approvals.AI;

/// <summary>
/// Lists approval requests as Approvals › Requests does, with its three views: the requests waiting for the user's
/// decision (<see cref="ApprovalInbox.ListWaitingAsync"/>, no Approvals permission needed, each request's own rule
/// decides), the requests the user asked for, and every request the user may see (View approval requests, filtered by
/// the subject types' access).
/// </summary>
internal sealed class ListApprovalRequestsTool : ApprovalsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listApprovalRequests";

    /// <summary>
    /// The view of the requests waiting for the user's decision.
    /// </summary>
    public const string WaitingForMeView = "waitingForMe";

    /// <summary>
    /// The view of the requests the user asked for.
    /// </summary>
    public const string MineView = "mine";

    /// <summary>
    /// The view of every request the user may see.
    /// </summary>
    public const string AllView = "all";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "view": {
          "type": "string",
          "enum": ["waitingForMe", "mine", "all"],
          "description": "waitingForMe (the default): the pending requests the user may decide now. mine: the requests the user asked for. all: every request the user may see, which needs the View approval requests permission."
        },
        "status": {
          "type": "string",
          "enum": ["pending", "approved", "rejected", "canceled", "any"],
          "description": "For mine and all: only requests with this status (default pending). waitingForMe is always pending."
        },
        "type": {
          "type": "string",
          "description": "Only requests of this approval type, by its name, such as PayRun. An unknown name returns the registered types."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many requests per page, up to 50 (default 20)."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists approval requests: by default the ones waiting for the signed-in user's decision, or the ones the user asked for (mine), or every request the user may see (all). Returns what is asked, the type, amount, requester, stage and approvals, due time and a link to the request. Invoices awaiting approval are not here: find them with findInvoices (status pendingApproval).";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var view = ReadString(arguments, "view") ?? WaitingForMeView;

        if (!string.Equals(view, WaitingForMeView, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(view, MineView, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(view, AllView, StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was listed: view must be waitingForMe, mine or all.";
        }

        ApprovalStatus? status;

        switch (ReadString(arguments, "status")?.ToLowerInvariant() ?? "pending")
        {
            case "pending":
                status = ApprovalStatus.Pending;
                break;
            case "approved":
                status = ApprovalStatus.Approved;
                break;
            case "rejected":
                status = ApprovalStatus.Rejected;
                break;
            case "canceled":
                status = ApprovalStatus.Canceled;
                break;
            case "any":
                status = null;
                break;
            default:
                return "Nothing was listed: status must be pending, approved, rejected, canceled or any.";
        }

        var types = services.GetRequiredService<IApprovalTypeCatalog>();
        string type = null;

        if (ReadString(arguments, "type") is { } typeName)
        {
            if (types.Find(typeName) is not { } found)
            {
                var known = types.List().Select(entry => entry.Name).ToList();

                return known.Count == 0
                    ? $"Nothing was listed: '{typeName}' is not an approval type, and no approval type is registered."
                    : $"Nothing was listed: '{typeName}' is not an approval type. The types are: {string.Join(", ", known)}.";
            }

            type = found.Name;
        }

        var (page, pageSize) = ReadPaging(arguments);
        var skip = (page - 1) * pageSize;
        int count;
        IReadOnlyList<ApprovalRequest> requests;

        if (string.Equals(view, WaitingForMeView, StringComparison.OrdinalIgnoreCase))
        {
            view = WaitingForMeView;
            (count, requests) = await services.GetRequiredService<ApprovalInbox>().ListWaitingAsync(caller, type, skip, pageSize, cancellationToken);
        }
        else
        {
            var mine = string.Equals(view, MineView, StringComparison.OrdinalIgnoreCase);

            view = mine ? MineView : AllView;

            // As the page: who can't see every request has the inbox and their own requests.
            if (!mine && !await IsAuthorizedAsync(services, caller, ApprovalsPermissions.ViewApprovalRequests))
            {
                return "Nothing was listed: the all view needs the View approval requests permission. Use waitingForMe or mine.";
            }

            (count, requests) = await services.GetRequiredService<IApprovalRequestStore>().ListPageAsync(
                new ApprovalRequestQuery
                {
                    Status = status,
                    Type = type,
                    OldestFirst = status == ApprovalStatus.Pending,
                    RequesterUserId = mine ? caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? "-" : null,
                    Access = mine
                        ? SourceAccessFilter.Unrestricted
                        : await services.GetRequiredService<ISourceAccessService>().GetFilterAsync(caller, SourceAccessLevel.View, cancellationToken),
                },
                skip,
                pageSize,
                cancellationToken);
        }

        var items = new List<ApprovalRequestItem>();

        foreach (var request in requests)
        {
            items.Add(await ApprovalRequestResults.DescribeAsync<ApprovalRequestItem>(services, request, cancellationToken));
        }

        return Json(new
        {
            view,
            items,
            total = count,
            page,
            pageSize,
            truncated = skip + items.Count < count,
            note = ApprovalRequestResults.UntrustedNote,
        });
    }
}
