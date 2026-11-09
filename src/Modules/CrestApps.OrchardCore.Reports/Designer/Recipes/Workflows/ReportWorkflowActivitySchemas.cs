using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Reports.Designer.Recipes.Workflows;

/// <summary>
/// Describes the members every Report Builder workflow task shares.
/// </summary>
internal static class ReportTaskSchemaProperties
{
    public const string Category = "Reports";

    public static IEnumerable<(string Name, JsonSchemaBuilder Schema)> Common(string defaultOutputName)
    {
        yield return ("ReportId", WorkflowActivitySchemaBuilders.String("The itemId of the designed report, used when ReportIdExpression is empty."));
        yield return ("ReportIdExpression", WorkflowActivitySchemaBuilders.String("Optional Liquid template that resolves to the itemId of the report. When it is not empty it is used instead of ReportId."));
        yield return ("Filters", WorkflowActivitySchemaBuilders.String("Optional filter values, one 'filter id = value' line per value, rendered with Liquid first. A filter named on several lines gets all their values; a filter not named keeps its default."));
        yield return ("OutputName", WorkflowActivitySchemaBuilders.String(defaultOutputName is null
            ? "Not used by this task."
            : $"The name of the workflow output that receives the result. Defaults to '{defaultOutputName}'."));
    }
}

/// <summary>
/// Describes the Run Report workflow task.
/// </summary>
internal sealed class RunReportTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "RunReportTask";

    protected override string Category => ReportTaskSchemaProperties.Category;

    protected override string DisplayText => "Run Report";

    protected override string Description => "Runs a designed report and writes its title, columns and rows to a workflow output";

    protected override IEnumerable<string> Outcomes => ["Done", "No rows", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => ReportTaskSchemaProperties.Common("Report");
}

/// <summary>
/// Describes the Export Report workflow task.
/// </summary>
internal sealed class ExportReportTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "ExportReportTask";

    protected override string Category => ReportTaskSchemaProperties.Category;

    protected override string DisplayText => "Export Report";

    protected override string Description => "Exports a designed report to a file, saved to the media library or written to a workflow output as Base64";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        foreach (var property in ReportTaskSchemaProperties.Common("ReportFile"))
        {
            yield return property;
        }

        yield return ("Format", WorkflowActivitySchemaBuilders.String("The export format. Defaults to 'csv'; 'xlsx' and 'pdf' need their export features.", ["csv", "xlsx", "pdf"]));
        yield return ("MediaFolder", WorkflowActivitySchemaBuilders.String("Optional Liquid template of a media library folder the file is saved to (needs the Media feature). When empty the file is written to the output as Base64."));
    }
}

/// <summary>
/// Describes the Email Report workflow task.
/// </summary>
internal sealed class EmailReportTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "EmailReportTask";

    protected override string Category => ReportTaskSchemaProperties.Category;

    protected override string DisplayText => "Email Report";

    protected override string Description => "Runs a designed report and emails it, in the body and optionally as an attachment";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["To"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        foreach (var property in ReportTaskSchemaProperties.Common(null))
        {
            yield return property;
        }

        yield return ("To", WorkflowActivitySchemaBuilders.String("The recipients, rendered with Liquid."));
        yield return ("Cc", WorkflowActivitySchemaBuilders.String("The carbon-copy recipients, rendered with Liquid."));
        yield return ("Bcc", WorkflowActivitySchemaBuilders.String("The blind-copy recipients, rendered with Liquid."));
        yield return ("Subject", WorkflowActivitySchemaBuilders.String("The subject, rendered with Liquid. Defaults to the report's title."));
        yield return ("Body", WorkflowActivitySchemaBuilders.String("The HTML body placed before the report, rendered with Liquid."));
        yield return ("AttachmentFormat", WorkflowActivitySchemaBuilders.String("The format of the attached export ('csv' by default, 'xlsx' or 'pdf'), or empty to attach nothing.", ["csv", "xlsx", "pdf"]));
        yield return ("IncludeReport", WorkflowActivitySchemaBuilders.Boolean("Whether the report's tables are added to the body. Defaults to true."));
    }
}

/// <summary>
/// Describes the Create Report Share Link workflow task.
/// </summary>
internal sealed class CreateReportShareLinkTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "CreateReportShareLinkTask";

    protected override string Category => ReportTaskSchemaProperties.Category;

    protected override string DisplayText => "Create Report Share Link";

    protected override string Description => "Creates a share link to a designed report for users of the site and writes its address to a workflow output";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        foreach (var property in ReportTaskSchemaProperties.Common("ReportShareLink"))
        {
            yield return property;
        }

        yield return ("Recipients", WorkflowActivitySchemaBuilders.String("The site users the link is for, separated by commas, semicolons or lines, rendered with Liquid."));
        yield return ("Note", WorkflowActivitySchemaBuilders.String("Optional note shown with the link and in its email, rendered with Liquid."));
        yield return ("ExpiresAfterDays", WorkflowActivitySchemaBuilders.Integer("How many days the link works, at least 1. Defaults to 7."));
        yield return ("MaxUses", WorkflowActivitySchemaBuilders.Integer("How many times the link may be opened, or 0 for no limit."));
        yield return ("AllowExport", WorkflowActivitySchemaBuilders.Boolean("Whether the link lets its recipients export the report."));
        yield return ("NotifyByEmail", WorkflowActivitySchemaBuilders.Boolean("Whether the recipients are emailed the link (needs the Email feature). Defaults to true."));
    }
}

/// <summary>
/// Describes the members the Report Builder workflow events share.
/// </summary>
internal abstract class ReportEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => ReportTaskSchemaProperties.Category;

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ReportIds", WorkflowActivitySchemaBuilders.StringArray("The itemIds of the designed reports that start the workflow. Empty matches every report."));
    }
}

/// <summary>
/// Describes the Report Run workflow event.
/// </summary>
internal sealed class ReportRunEventSchema : ReportEventSchemaBase
{
    public override string Name => "ReportRunEvent";

    protected override string DisplayText => "Report Run";

    protected override string Description => "Starts when a designed report is run";
}

/// <summary>
/// Describes the Report Exported workflow event.
/// </summary>
internal sealed class ReportExportedEventSchema : ReportEventSchemaBase
{
    public override string Name => "ReportExportedEvent";

    protected override string DisplayText => "Report Exported";

    protected override string Description => "Starts when a designed report is exported";
}

/// <summary>
/// Describes the Report Published workflow event.
/// </summary>
internal sealed class ReportPublishedEventSchema : ReportEventSchemaBase
{
    public override string Name => "ReportPublishedEvent";

    protected override string DisplayText => "Report Published";

    protected override string Description => "Starts when a designed report is published";
}
