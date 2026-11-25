using CrestApps.Core.AI;
using CrestApps.OrchardCore.Reports.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Registers the Report Builder's AI tools whenever the AI feature is on: finding and querying data sets, saving,
/// sharing, restoring and deleting designed reports, their share links, report views and report groups. Sharing,
/// share links and deletions ask for approval.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class BuilderAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuilderAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BuilderAIToolsStartup(IStringLocalizer<BuilderAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<ReportToolScopeChanges>();

        services.AddCoreAITool<ListReportDataSetsTool>(ListReportDataSetsTool.TheName)
            .WithTitle(S["List Report Data Sets"])
            .WithDescription(S["Lists the data sources and the data sets the user may read (orders, customers, content types, users, saved views, connected data sets and so on), optionally for one data source or matching a text. Use it to find where the data for a question lives."])
            .WithCategory(S["Report Builder"])
            .Selectable();

        services.AddCoreAITool<GetReportDataSetSchemaTool>(GetReportDataSetSchemaTool.TheName)
            .WithTitle(S["Get Report Data Set Schema"])
            .WithDescription(S["Returns the fields of one data set with their names, types, groups, descriptions and the data sets they reference, and the formula functions available for calculated fields. Use it before writing a query so the field names are exact."])
            .WithCategory(S["Report Builder"])
            .Selectable();

        services.AddCoreAITool<QueryReportDataTool>(QueryReportDataTool.TheName)
            .WithTitle(S["Query Report Data"])
            .WithDescription(S["Runs an ad hoc query over one or more data sets with the user's own access, with joins, filters, grouping by a field or by year, quarter, month, week or day, aggregates (count, sum, average, min, max, median), sorting and a limit, and returns rows. Use it when no existing report answers the question; nothing is saved."])
            .WithCategory(S["Report Builder"])
            .Selectable();

        services.AddCoreAITool<SaveReportDesignTool>(SaveReportDesignTool.TheName)
            .WithTitle(S["Save Report Design"])
            .WithDescription(S["Creates and publishes a designed report, or publishes a change to an existing one by id, from a definition with its query, visuals, group, export and admin menu options. Returns the planner's errors and warnings; it does not change who the report is shared with. Test the query with queryReportData first."])
            .WithCategory(S["Report Builder"])
            .Selectable();

        services.AddCoreAITool<ShareReportDesignTool>(ShareReportDesignTool.TheName)
            .WithTitle(S["Share Report Design"])
            .WithDescription(S["Sets the users and roles a designed report is shared with. The people it is shared with see the report's data with its owner's access."])
            .WithCategory(S["Report Builder"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<RestoreReportDesignVersionTool>(RestoreReportDesignVersionTool.TheName)
            .WithTitle(S["Restore Report Design Version"])
            .WithDescription(S["Restores an earlier saved version of a designed report. Use getReport with versions to find the version number."])
            .WithCategory(S["Report Builder"])
            .Selectable();

        services.AddCoreAITool<DeleteReportDesignTool>(DeleteReportDesignTool.TheName)
            .WithTitle(S["Delete Report Design"])
            .WithDescription(S["Deletes a designed report with its drafts and versions, or an unpublished draft that was never published. People it was shared with lose it."])
            .WithCategory(S["Report Builder"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<SetReportShareLinkTool>(SetReportShareLinkTool.TheName)
            .WithTitle(S["Set Report Share Link"])
            .WithDescription(S["Creates or revokes a link that opens a designed report for named recipients. create takes the recipients, an expiry in days, an optional maximum number of uses, whether export is allowed and a note, and emails the link to the recipients; the link itself is never returned, since it holds a secret token. revoke takes the link id from getReport with share links and stops the link from working."])
            .WithCategory(S["Report Builder"])
            .AsSecurity()
            .Selectable();

        services.AddCoreAITool<ListReportViewsTool>(ListReportViewsTool.TheName)
            .WithTitle(S["List Report Views"])
            .WithDescription(S["Lists the saved report views (reusable queries that reports read as data sets) with owner, refresh interval and snapshot state, or one view by id with its query and the reports that use it."])
            .WithCategory(S["Report Builder"])
            .Selectable();

        services.AddCoreAITool<SaveReportViewTool>(SaveReportViewTool.TheName)
            .WithTitle(S["Save Report View"])
            .WithDescription(S["Creates a report view from a query, or updates one by id, with an optional refresh interval for its stored snapshot. Returns the planner's errors and warnings."])
            .WithCategory(S["Report Builder"])
            .Selectable();

        services.AddCoreAITool<DeleteReportViewTool>(DeleteReportViewTool.TheName)
            .WithTitle(S["Delete Report View"])
            .WithDescription(S["Deletes a report view. Reports and other views that read it stop working, so check its usages first."])
            .WithCategory(S["Report Builder"])
            .AsDestructive()
            .Selectable();

        services.AddCoreAITool<ListReportGroupsTool>(ListReportGroupsTool.TheName)
            .WithTitle(S["List Report Groups"])
            .WithDescription(S["Lists the report groups as a tree with the number of groups and reports in each."])
            .WithCategory(S["Report Builder"])
            .Selectable();

        services.AddCoreAITool<SaveReportGroupTool>(SaveReportGroupTool.TheName)
            .WithTitle(S["Save Report Group"])
            .WithDescription(S["Creates a report group under a parent, or renames, moves or reorders one by id."])
            .WithCategory(S["Report Builder"])
            .Selectable();

        services.AddCoreAITool<DeleteReportGroupTool>(DeleteReportGroupTool.TheName)
            .WithTitle(S["Delete Report Group"])
            .WithDescription(S["Deletes a report group. Check what it contains first; the service refuses or reports what blocks the deletion."])
            .WithCategory(S["Report Builder"])
            .AsDestructive()
            .Selectable();
    }
}
