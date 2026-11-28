using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Lists the reports the caller may open, as the Reports page and the Report Builder's list show them: a built-in
/// report when the caller holds its permission, and a designed report when the caller may view it (its owner, the
/// people and roles it is shared with, and whoever may view every designed report).
/// </summary>
public sealed class ListReportsTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listReports";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "string",
          "description": "Only return the reports whose title, description, category or group contains this text."
        },
        "category": {
          "type": "string",
          "description": "Only return the built-in reports of this category, or the designed reports of this group or a group nested in it, such as 'Commerce' or 'Sales / Regional'."
        },
        "kind": {
          "type": "string",
          "enum": ["all", "builtIn", "designed"],
          "description": "Which reports to list: all (the default), the built-in reports only, or the reports designed in the Report Builder only."
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
          "description": "The number of reports per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the reports the user may open: built-in reports with their category and description, and, when the Report Builder is on, designed reports with their group and owner. Supports a text search and a category or group filter. Use it first to find the report that answers a question.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var query = ReadString(arguments, "query");
        var category = ReadString(arguments, "category");
        var kind = ReadString(arguments, "kind") ?? "all";
        var (page, pageSize) = ReadPaging(arguments);

        if (kind is not ("all" or ReportToolCatalog.BuiltIn or ReportToolCatalog.Designed))
        {
            return "Give kind as all, builtIn or designed.";
        }

        var localizerFactory = services.GetRequiredService<IStringLocalizerFactory>();
        var entries = new List<Entry>();

        if (kind != ReportToolCatalog.Designed)
        {
            foreach (var report in services.GetRequiredService<IReportManager>().GetReports())
            {
                if (!await IsAuthorizedAsync(services, user, report.Permission))
                {
                    continue;
                }

                entries.Add(new Entry(
                    report.Name,
                    ReportToolCatalog.BuiltIn,
                    localizerFactory.Localize(report.DisplayName).Value,
                    report.Description is null ? null : localizerFactory.Localize(report.Description).Value,
                    report.Category,
                    null,
                    ReportsConstants.Categories.GetOrder(report.Category)));
            }
        }

        var designService = services.GetService<ReportDesignService>();

        if (kind != ReportToolCatalog.BuiltIn && designService is not null)
        {
            var groups = await designService.GetGroupTreeAsync();

            foreach (var design in await designService.GetAllAsync())
            {
                if (!await ReportToolCatalog.CanViewAsync(services, user, design))
                {
                    continue;
                }

                entries.Add(new Entry(
                    design.ItemId,
                    ReportToolCatalog.Designed,
                    design.DisplayText,
                    design.Description,
                    groups.GetPathText(design.GroupId),
                    design.Author,
                    int.MaxValue));
            }
        }

        var matches = entries
            .Where(entry => Matches(entry, query) && InCategory(entry, category))
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Category, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var items = matches
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entry => new
            {
                id = entry.Id,
                kind = entry.Kind,
                title = entry.Title,
                description = entry.Description,
                category = entry.Kind == ReportToolCatalog.BuiltIn ? entry.Category : null,
                group = entry.Kind == ReportToolCatalog.Designed ? entry.Category : null,
                owner = entry.Owner,
            })
            .ToArray();

        return Json(new
        {
            items,
            total = matches.Count,
            page,
            pageSize,
            truncated = page * pageSize < matches.Count ? true : (bool?)null,
            reportBuilderEnabled = designService is not null,
            pendingChanges = ReportToolScopeChanges.Get(services).HasChanges
                ? "Reports changed earlier in this message show as they were until this reply ends."
                : null,
        });
    }

    private static bool Matches(Entry entry, string query)
        => string.IsNullOrEmpty(query) ||
            Contains(entry.Title, query) ||
            Contains(entry.Description, query) ||
            Contains(entry.Category, query);

    // A designed report's group matches its own path and the path of any group it is nested in.
    private static bool InCategory(Entry entry, string category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return true;
        }

        if (string.Equals(entry.Category, category.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return entry.Kind == ReportToolCatalog.Designed &&
            !string.IsNullOrEmpty(entry.Category) &&
            entry.Category.StartsWith(category.Trim() + ReportGroupTree.PathSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string text, string query)
        => text?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;

    private sealed record Entry(string Id, string Kind, string Title, string Description, string Category, string Owner, int Order);
}
