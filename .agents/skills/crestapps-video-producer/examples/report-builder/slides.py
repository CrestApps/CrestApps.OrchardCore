"""The slides of the Report Builder video."""
from brand import MARGIN, W, bullets, draw_code, note_cards, slide_base

WHY = [
    ("1", "Data lives in many places", "Content items, users, calls, activities, AI chats and messages"),
    ("2", "Built-in reports are fixed", "They answer the questions someone thought of in advance"),
    ("check", "Build your own reports", "Pick data sets, join, group, total, chart, and share"),
]

CONCEPTS = [
    ("1", "Data sources and data sets", "Contents, Users, Contact Center... each with data sets and fields"),
    ("2", "Reports", "Data sets, columns and filters, shown as tables, charts, metrics, pivots"),
    ("3", "Views", "Saved, reusable data sets that can refresh on a schedule"),
    ("check", "Permissions everywhere", "Sources show what you may read; reports run with the owner's access"),
]

SOURCE = """
public sealed class TicketsReportDataSource : IReportDataSource
{
    public string Name => "Tickets";

    public LocalizedString DisplayName => S["Tickets"]; // ...

    public Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(
        ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        // The data sets this person may read.
    }

    public Task<ReportDataSetSchema> GetSchemaAsync(
        string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        // The fields of a data set: their names, labels and types.
    }

    public Task<ReportDataTable> QueryAsync(
        ReportDataSourceQuery query, CancellationToken cancellationToken = default)
    {
        // The rows, with only the fields the report asks for.
    }
}
"""

STARTUP = """
[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IReportDataSource, TicketsReportDataSource>();
    }
}
"""

TYPES = ("TicketsReportDataSource", "IReportDataSource", "ReportDataSetDescriptor", "ReportDataSourceContext",
         "ReportDataSetSchema", "ReportDataTable", "ReportDataSourceQuery", "CancellationToken", "Task",
         "IReadOnlyList", "LocalizedString", "ReportsStartup", "StartupBase", "IServiceCollection", "ReportsConstants", "RequireFeatures")


def slide(step_id, chapter_title, label):
    if step_id.startswith("why-"):
        image = slide_base(chapter_title, label, "Answers without writing a query")
        bullets(image, WHY, active=set(range(int(step_id[-1]))))
        return image
    if step_id.startswith("concepts-"):
        image = slide_base(chapter_title, label, "Sources, reports and views")
        bullets(image, CONCEPTS, active={int(step_id[-1]) - 1})
        return image
    if step_id == "ext-1":
        image = slide_base(chapter_title, label, "1. Implement IReportDataSource")
        draw_code(image, (MARGIN, 225, W - MARGIN, 1060), "TicketsReportDataSource.cs", "csharp", SOURCE, types=TYPES, size=22)
        return image
    if step_id == "ext-2":
        image = slide_base(chapter_title, label, "2. Register it")
        draw_code(image, (MARGIN, 240, 1150, 640), "Startup.cs", "csharp", STARTUP, types=TYPES, size=24)
        note_cards(image, [
            ("Records", "Derive from ReportRecordDataSource"),
            ("Security", "Return only what they may read"),
        ], (1190, 240, W - MARGIN, 0))
        return image
    if step_id == "ext-3":
        image = slide_base(chapter_title, label, "3. Follow the skill")
        bullets(image, [
            ("1", "crestapps-report-data-source", "In .agents/skills of the repository"),
            ("2", "Every contract, step by step", "Data sets, schemas, queries, joins and grouping in the source"),
            ("check", "Then it is a source like any other", "Its data sets appear in the builder's Data pane"),
        ])
        return image
    raise SystemExit(f"No slide for {step_id}")
