using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Lists the report groups as a tree, as the Report Groups page does: it needs the permission that manages report
/// groups. Every report is counted, as on that page, whoever may open it.
/// </summary>
public sealed class ListReportGroupsTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listReportGroups";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "string",
          "description": "Only list the groups whose path, such as 'Sales / Regional', contains this text."
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
          "description": "The number of groups per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the report groups as a tree with the number of groups and reports in each.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageReportGroups))
        {
            return NotAllowedMessage;
        }

        var search = ReadString(arguments, "query");
        var (page, pageSize) = ReadPaging(arguments);
        var tree = await services.GetRequiredService<ReportGroupService>().GetTreeAsync();
        var reportCounts = (await services.GetRequiredService<ReportDesignService>().GetAllAsync())
            .Where(design => tree.Find(design.GroupId) is not null)
            .GroupBy(design => design.GroupId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var nodes = tree.Flatten()
            .Where(node => search is null || node.PathText.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        return Json(new
        {
            items = nodes
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(node => new
                {
                    id = node.Group.ItemId,
                    name = node.Group.DisplayText,
                    path = node.PathText,
                    parentId = node.Group.ParentId,
                    depth = node.Depth,
                    order = node.Group.Order,
                    groupCount = tree.GetChildren(node.Group.ItemId).Count,
                    reportCount = reportCounts.GetValueOrDefault(node.Group.ItemId),
                })
                .ToArray(),
            total = nodes.Count,
            page,
            pageSize,
            truncated = page * pageSize < nodes.Count ? true : (bool?)null,
        });
    }
}
