using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Describes one report the caller may open. A built-in report needs its own permission, as its page does; a designed
/// report needs to be viewable by the caller. A designed report's definition and versions need the permission that
/// changes it, as the designer does, and its share links also need the permission that shares reports publicly, as the
/// designer's link list does.
/// </summary>
public sealed class GetReportTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getReport";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The report's id from listReports: a built-in report's technical name or a designed report's id."
        },
        "include": {
          "type": "array",
          "items": {
            "type": "string",
            "enum": ["definition", "versions", "shareLinks"]
          },
          "description": "For a designed report: also return its full definition (query, visuals and who it is shared with), its saved versions, or its share links. Needs the right to change the report; share links also need the right to share reports publicly."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Describes one report: what it shows, the filters it accepts with their allowed values, its default period, the export formats and whether the user may export. A built-in report can only be given a period here; its other filters are set on its page. For a designed report it can also return the full definition, its saved versions and its share links.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");

        if (id is null)
        {
            return "Give the report's id in id. Call listReports to find it.";
        }

        var include = (ReadStringList(arguments, "include") ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var report = ReportToolCatalog.FindBuiltIn(services, id);

        if (report is not null)
        {
            if (!await IsAuthorizedAsync(services, user, report.Permission))
            {
                return NotAllowedMessage;
            }

            return Json(await DescribeBuiltInAsync(services, user, report));
        }

        if (ReportToolScopeChanges.Get(services).WasChanged(id))
        {
            return ReportToolScopeChanges.ChangedMessage(id);
        }

        var design = await ReportToolCatalog.FindDesignedAsync(services, id);

        if (design is null)
        {
            return ReportToolCatalog.NotFoundMessage(id);
        }

        if (!await ReportToolCatalog.CanViewAsync(services, user, design))
        {
            return NotAllowedMessage;
        }

        return Json(await DescribeDesignedAsync(services, user, design, include, cancellationToken));
    }

    private static async Task<object> DescribeBuiltInAsync(IServiceProvider services, ClaimsPrincipal user, ReportDescriptor report)
    {
        var localizerFactory = services.GetRequiredService<IStringLocalizerFactory>();
        var canExport = await ReportToolCatalog.CanExportAsync(services, user, report);
        var runner = services.GetRequiredService<IReportManager>().GetReport(report.Name);
        var otherFilters = runner is IReportFilterMetadata metadata
            ? metadata.FilterNames?.Where(name => !string.IsNullOrEmpty(name)).ToArray()
            : null;

        return new
        {
            id = report.Name,
            kind = ReportToolCatalog.BuiltIn,
            title = localizerFactory.Localize(report.DisplayName).Value,
            description = report.Description is null ? null : localizerFactory.Localize(report.Description).Value,
            category = report.Category,
            defaultPeriod = report.DefaultDateRange?.Key ?? "today",
            acceptsAllTime = report.DefaultDateRange is not null,
            filters = new[]
            {
                new
                {
                    id = "period",
                    label = "Date range",
                    how = report.DefaultDateRange is not null
                        ? "Give from and to as days (yyyy-MM-dd) in the user's time zone, only one of them for an open-ended range, or allTime for every date. Without them the report runs on its default period."
                        : "Give from and to as days (yyyy-MM-dd) in the user's time zone. Without them the report runs on today.",
                },
            },
            otherFilters = otherFilters is { Length: > 0 } ? otherFilters : null,
            otherFiltersNote = otherFilters is { Length: > 0 }
                ? "These filters are set on the report's page; runReport and exportReport set only the period."
                : null,
            canExport,
            exportFormats = canExport ? DescribeFormats(services) : [],
        };
    }

    private static async Task<object> DescribeDesignedAsync(IServiceProvider services, ClaimsPrincipal user, ReportDesign design, HashSet<string> include, CancellationToken cancellationToken)
    {
        var designService = services.GetRequiredService<ReportDesignService>();
        var canManage = await ReportToolCatalog.CanManageAsync(services, user, design);
        var canExport = await ReportToolCatalog.CanExportAsync(services, user, design);
        var groups = await designService.GetGroupTreeAsync();
        var refused = new List<string>();

        object definition = null;
        object versions = null;
        object shareLinks = null;
        bool? hasUnpublishedChanges = null;

        if (include.Contains("definition") || include.Contains("versions"))
        {
            if (canManage)
            {
                var history = services.GetRequiredService<ReportDesignHistoryService>();
                var workingCopy = await history.GetWorkingCopyAsync(design);

                hasUnpublishedChanges = workingCopy.HasDraft;

                if (include.Contains("definition"))
                {
                    definition = new
                    {
                        query = JsonSerializer.SerializeToElement(design.Query, ReportDesignerJson.Options),
                        queries = JsonSerializer.SerializeToElement(design.Queries, ReportDesignerJson.Options),
                        visuals = JsonSerializer.SerializeToElement(design.Visuals, ReportDesignerJson.Options),
                        containers = JsonSerializer.SerializeToElement(design.Containers, ReportDesignerJson.Options),
                        showInAdminMenu = design.ShowInAdminMenu,
                        allowExport = design.AllowExport,
                        sharedWith = new
                        {
                            users = design.SharedUserNames ?? [],
                            roles = design.SharedRoles ?? [],
                        },
                    };
                }

                if (include.Contains("versions"))
                {
                    var list = new List<object>();
                    var index = 0;

                    foreach (var version in await history.ListVersionsAsync(design.ItemId))
                    {
                        list.Add(new
                        {
                            number = version.Number,
                            title = version.DisplayText,
                            created = await ToLocalAsync(services, version.CreatedUtc),
                            createdBy = version.CreatedByName,
                            restoredFrom = version.RestoredFrom,
                            isCurrent = index++ == 0,
                        });
                    }

                    versions = list;
                }
            }
            else
            {
                refused.Add("The definition and versions need the right to change this report.");
            }
        }

        if (include.Contains("shareLinks"))
        {
            if (canManage && await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ShareReportsPublicly))
            {
                var list = new List<object>();

                foreach (var link in await services.GetRequiredService<ReportShareLinkService>().ListAsync(design, cancellationToken))
                {
                    list.Add(new
                    {
                        id = link.LinkId,
                        note = link.Properties.GetValueOrDefault(ReportSharedLinkHandler.NoteProperty),
                        recipients = link.RecipientNames,
                        created = await ToLocalAsync(services, link.CreatedUtc),
                        expires = await ToLocalAsync(services, link.ExpiresUtc),
                        revoked = await ToLocalAsync(services, link.RevokedUtc),
                        uses = link.Uses,
                        maxUses = link.MaxUses == 0 ? (int?)null : link.MaxUses,
                        isUsedUp = link.IsUsedUp,
                        allowExport = ReportSharedLinkHandler.AllowsExport(link),
                    });
                }

                shareLinks = list;
            }
            else
            {
                refused.Add("The share links need the right to change this report and to share reports publicly.");
            }
        }

        return new
        {
            id = design.ItemId,
            kind = ReportToolCatalog.Designed,
            title = design.DisplayText,
            description = design.Description,
            group = groups.GetPathText(design.GroupId),
            owner = design.Author,
            created = await ToLocalAsync(services, design.CreatedUtc),
            modified = await ToLocalAsync(services, design.ModifiedUtc),
            filters = ReportToolCatalog.GetExposedFilters(design).Select(filter => new
            {
                id = filter.Id,
                label = filter.Label ?? filter.Field,
                field = filter.Field,
                control = filter.Control.ToString(),
                @operator = filter.Operator.ToString(),
                takesRange = ReportToolCatalog.IsRange(filter),
                defaultValues = filter.Values ?? [],
            }).ToArray(),
            filtersNote = "Give filter values to runReport or exportReport by filter id, as a list of texts; a range takes [from, to], dates as yyyy-MM-dd.",
            canEdit = canManage,
            canExport,
            exportFormats = canExport ? DescribeFormats(services) : [],
            hasUnpublishedChanges,
            definition,
            versions,
            shareLinks,
            notIncluded = refused.Count > 0 ? refused : null,
        };
    }

    private static object[] DescribeFormats(IServiceProvider services)
    {
        var localizerFactory = services.GetRequiredService<IStringLocalizerFactory>();

        return services.GetRequiredService<IReportExportManager>().GetFormats()
            .Select(format => (object)new
            {
                name = format.Name,
                displayName = format.DisplayName is null ? format.Name : localizerFactory.Localize(format.DisplayName).Value,
            })
            .ToArray();
    }
}
