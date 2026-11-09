using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Reports.Designer.Recipes;

/// <summary>
/// Describes the <c>ReportDesigns</c> recipe step to CrestApps Recipes.
/// </summary>
/// <remarks>
/// The step reads report groups, views and reports with the designer's web serializer, so member names are matched
/// without regard to case and a deployment plan writes them in camel case, as described here. Enumerations travel as
/// their names.
/// </remarks>
internal sealed class ReportDesignsRecipeStepSchema : RecipeStepSchemaBase
{
    private const string ItemIdPattern = "^[A-Za-z0-9_-]{1,64}$";

    public override string Name => ReportDesignsRecipeStep.Name;

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Groups", RecipeStepSchemaBuilders.Array(Group()).Description("The report groups, imported before the views and reports. A group is matched by its itemId and replaced, or created under that identifier. A group whose parent is missing once every group is in moves to the top level.")),
                ("Views", RecipeStepSchemaBuilders.Array(View()).Description("The reusable views (saved queries that other reports and views read as the 'ReportViews' data source), imported before the reports. A view is matched by its itemId and replaced, or created under that identifier.")),
                ("Reports", RecipeStepSchemaBuilders.Array(Report()).Description("The designed reports. A report is matched by its itemId and replaced, or created under that identifier. Share links are never imported.")),
            ]);

    private static JsonSchemaBuilder Group()
        => RecipeStepSchemaBuilders.Object(
            [
                ("itemId", ItemId("group")),
                ("displayText", Text("The name of the group. A group without a name is refused.")),
                ("parentId", NullableText("The itemId of the group this group is nested in, or null for a top-level group.")),
                ("order", RecipeStepSchemaBuilders.Integer().Description("The position of the group among its siblings.")),
                ("createdUtc", Timestamp("The time the group was created.")),
                ("modifiedUtc", NullableTimestamp("The time the group was last changed.")),
            ],
            ["itemId", "displayText"])
            .Description("A report group, which files reports in a tree.");

    private static JsonSchemaBuilder View()
        => RecipeStepSchemaBuilders.Object(
            [
                ("itemId", ItemId("view")),
                ("displayText", Text("The title of the view. A view without a title is refused.")),
                ("description", NullableText("The description of the view.")),
                ("query", Query().Description("The query the view runs.")),
                ("refreshIntervalMinutes", RecipeStepSchemaBuilders.Integer().Minimum(0).Description("How often the view's result is computed ahead and stored: 0 runs it live on every read, otherwise at least every 15 minutes (60, 360 and 1440 are offered). Other values are normalized on import.")),
                .. Ownership("view"),
            ],
            ["itemId", "displayText"])
            .Description("A reusable view.");

    private static JsonSchemaBuilder Report()
        => RecipeStepSchemaBuilders.Object(
            [
                ("itemId", ItemId("report")),
                ("displayText", Text("The title of the report. A report without a title is refused.")),
                ("description", NullableText("The description of the report.")),
                ("groupId", NullableText("The itemId of the report group the report is in. When no group has this identifier, the group is taken from 'group'.")),
                ("group", NullableText("A path of group names, such as 'Sales / Regional', used when groupId names no group. Missing groups along the path are created.")),
                ("category", NullableText("Obsolete. A top-level group name written before report groups existed; read only when neither groupId nor group places the report.")),
                ("query", Query().Description("The main query of the report.")),
                ("queries", RecipeStepSchemaBuilders.Array(NamedQuery()).Description("Extra queries the report runs besides the main query, so visuals can show the data grouped another way.")),
                ("visuals", RecipeStepSchemaBuilders.Array(Visual()).Description("The tables, charts, metric rows and pivot tables of the report, in display order.")),
                ("containers", RecipeStepSchemaBuilders.Array(Container()).Description("Framed blocks that hold visuals under one title.")),
                ("showInAdminMenu", RecipeStepSchemaBuilders.Boolean().Description("Whether the report is listed in the admin menu under Reports.")),
                ("sharedUserNames", RecipeStepSchemaBuilders.StringArray().Description("The user names the report is shared with.")),
                ("sharedRoles", RecipeStepSchemaBuilders.StringArray().Description("The role names the report is shared with.")),
                ("allowExport", RecipeStepSchemaBuilders.Boolean().Default(true).Description("Whether the people who run the report may export it.")),
                .. Ownership("report"),
            ],
            ["itemId", "displayText"])
            .Description("A designed report.");

    private static IEnumerable<(string Name, JsonSchemaBuilder Schema)> Ownership(string noun)
    {
        yield return ("ownerId", NullableText($"The user id of the {noun}'s owner, whose access the {noun} runs with. Used when OwnerUserName names no user of the site."));
        yield return ("OwnerUserName", NullableText($"The user name of the {noun}'s owner, written by a deployment plan so the owner is found on a site where they have another user id. When neither this nor ownerId names an owner, the person who runs the recipe owns the {noun}."));
        yield return ("author", NullableText($"The name of the person who created the {noun}."));
        yield return ("createdUtc", Timestamp($"The time the {noun} was created."));
        yield return ("modifiedUtc", NullableTimestamp($"The time the {noun} was last changed. Set to the import time."));
    }

    private static JsonSchemaBuilder NamedQuery()
        => RecipeStepSchemaBuilders.Object(
            [
                ("id", Text("The identifier of the query, unique within the report, which visuals name in queryId.")),
                ("name", NullableText("The name the designer shows for the query.")),
                ("query", Query().Description("The query.")),
            ],
            ["id"])
            .Description("An extra query of a report.");

    private static JsonSchemaBuilder Query()
        => RecipeStepSchemaBuilders.Object(
            [
                ("dataSets", RecipeStepSchemaBuilders.Array(RecipeStepSchemaBuilders.Object(
                    [
                        ("alias", Text("The alias fields of this data set are named by, as in 'alias.Field'.")),
                        ("source", RecipeStepSchemaBuilders.SuggestedString(["Contents", "Users", "Queries", "ReportViews"]).Description("The name of the data source, as registered by the features that provide data (for example Contents, Users, Queries or ReportViews).")),
                        ("dataSet", NullableText("The data set within the source, such as a content type, a query name or the itemId of a view.")),
                        ("displayName", NullableText("The label of the data set in the designer.")),
                    ])).Description("The data sets the query reads. The first is the base data set; the others are attached by joins.")),
                ("joins", RecipeStepSchemaBuilders.Array(RecipeStepSchemaBuilders.Object(
                    [
                        ("alias", Text("The alias of the data set being joined.")),
                        ("type", RecipeStepSchemaBuilders.SuggestedString(["Inner", "Left", "Right", "Full"]).Description("The kind of join.")),
                        ("conditions", RecipeStepSchemaBuilders.Array(RecipeStepSchemaBuilders.Object(
                            [
                                ("leftField", Text("The field key on the left side, as 'alias.Field'.")),
                                ("rightField", Text("The field key on the right side, as 'alias.Field'.")),
                            ])).Description("The field pairs that must be equal.")),
                    ])).Description("How the data sets are joined, applied in order.")),
                ("calculatedFields", RecipeStepSchemaBuilders.Array(RecipeStepSchemaBuilders.Object(
                    [
                        ("name", Text("The name the field is referred to by.")),
                        ("label", NullableText("The label of the field.")),
                        ("expression", Text("The formula. A row-level field may refer only to data set fields and to calculated fields listed before it.")),
                    ])).Description("The calculated fields.")),
                ("filters", RecipeStepSchemaBuilders.Array(Filter()).Description("The filters.")),
                ("columns", RecipeStepSchemaBuilders.Array(Column()).Description("The result columns in display order. When any column is a measure, rows are grouped by the dimension columns.")),
                ("sorts", RecipeStepSchemaBuilders.Array(RecipeStepSchemaBuilders.Object(
                    [
                        ("columnId", Text("The id of the column to sort by.")),
                        ("descending", RecipeStepSchemaBuilders.Boolean().Description("Whether the sort is descending.")),
                    ])).Description("The sort order of the result.")),
                ("limit", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(1).Description("The most rows the result holds, or null for the default limit.")),
            ])
            .Description("The data half of a report or view.");

    private static JsonSchemaBuilder Filter()
        => RecipeStepSchemaBuilders.Object(
            [
                ("id", Text("The identifier of the filter, unique within the query. Exposed filter values are submitted under it.")),
                ("field", Text("What the filter applies to: a field key ('alias.Field' or a calculated field name) for the Rows stage, or a column id for the Result stage.")),
                ("stage", RecipeStepSchemaBuilders.String().Enum("Rows", "Result").Description("Whether the filter applies to rows before grouping or to the result after grouping.")),
                ("operator", RecipeStepSchemaBuilders.SuggestedString(["Equals", "NotEquals", "GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual", "Between", "In", "NotIn", "IsEmpty", "IsNotEmpty", "InLastDays", "InNextDays"]).Description("The comparison operator.")),
                ("values", RecipeStepSchemaBuilders.StringArray().Description("The comparison values in the invariant culture (dates as yyyy-MM-dd or yyyy-MM-ddTHH:mm in the tenant time zone). For an exposed filter, the default values.")),
                ("exposed", RecipeStepSchemaBuilders.Boolean().Description("Whether the filter is rendered for the people who run the report.")),
                ("label", NullableText("The label of an exposed filter.")),
                ("control", RecipeStepSchemaBuilders.String().Enum("Auto", "Text", "Select", "MultiSelect", "DateRange", "NumberRange", "Boolean", "RelativeDate").Description("The control that renders an exposed filter.")),
            ])
            .Description("A filter of a query.");

    private static JsonSchemaBuilder Column()
        => RecipeStepSchemaBuilders.Object(
            [
                ("id", Text("The identifier of the column, unique within the query, which sorts and visuals refer to.")),
                ("field", Text("The field key the column reads: 'alias.Field' or a calculated field name.")),
                ("label", NullableText("The header of the column.")),
                ("aggregate", RecipeStepSchemaBuilders.SuggestedString(["None", "Count", "CountDistinct", "Sum", "Average", "Min", "Max", "Median"]).Description("The aggregate that makes the column a measure, or None for a dimension.")),
                ("transform", RecipeStepSchemaBuilders.SuggestedString(["None", "Year", "Quarter", "Month", "MonthOfYear", "Week", "Day", "DayOfWeek", "Hour", "Lower", "Upper"]).Description("A transform applied to the field before grouping, such as a date part.")),
                ("format", NullableText("A .NET format string applied to the values.")),
                ("hidden", RecipeStepSchemaBuilders.Boolean().Description("Whether the column is computed but not shown.")),
                ("shelf", RecipeStepSchemaBuilders.String().Enum("Columns", "Rows").Description("The shelf a pivot places the column on.")),
                ("colors", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("The colors of the column's header and cells: headerColor, headerBackgroundColor, color, backgroundColor, a color scale and color rules.")),
                ("showAs", RecipeStepSchemaBuilders.String().Enum("Value", "PercentOfTotal", "RunningTotal", "DifferenceFromPrevious", "PercentDifference", "Rank").Description("How a measure's values are shown.")),
                ("showAsDirection", RecipeStepSchemaBuilders.String().Enum("Down", "Across", "Table").Description("The direction a showAs calculation runs in a pivot.")),
            ])
            .Description("A result column of a query.");

    private static JsonSchemaBuilder Visual()
        => RecipeStepSchemaBuilders.Object(
            [
                ("id", Text("The identifier of the visual, unique within the report.")),
                ("type", RecipeStepSchemaBuilders.String().Enum("Table", "Chart", "Metrics", "Pivot").Description("The kind of visual.")),
                ("title", NullableText("The title shown above the visual.")),
                ("width", RecipeStepSchemaBuilders.Integer().Minimum(1).Maximum(12).Default(12).Description("The width of the visual on a twelve-column grid (of its container's width when it is in one).")),
                ("containerId", NullableText("The id of the container that holds the visual, or null when it stands on its own.")),
                ("queryId", NullableText("The id of the extra query whose columns the visual shows, or null for the main query.")),
                ("action", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("What clicking a value does: columnId names the clicked column and targets lists the visuals ({ visualId, field }) filtered by the value.")),
                ("chartType", RecipeStepSchemaBuilders.String().Enum("Line", "Bar", "Doughnut", "Pie", "Area", "HorizontalBar").Description("The kind of chart.")),
                ("categoryColumnId", NullableText("The id of the column that labels a chart's categories.")),
                ("seriesColumnId", NullableText("The id of a dimension column whose values become a chart's series or a pivot table's columns.")),
                ("valueColumnIds", RecipeStepSchemaBuilders.StringArray().Description("The ids of the measure columns a chart plots, a metrics row shows or a pivot table sums.")),
                ("columnIds", RecipeStepSchemaBuilders.StringArray().Description("The ids of the columns a table shows, in order (every visible column when empty), or the row columns of a pivot table.")),
                ("showTotals", RecipeStepSchemaBuilders.Boolean().Description("Whether a table or pivot table ends with a totals row.")),
                ("showSubtotals", RecipeStepSchemaBuilders.Boolean().Description("Whether a table of a grouped report shows subtotal rows after each group of its leading dimensions.")),
                ("stacked", RecipeStepSchemaBuilders.Boolean().Description("Whether a bar or area chart stacks its series.")),
                ("showLegend", RecipeStepSchemaBuilders.Boolean().Default(true).Description("Whether a chart shows its legend.")),
                ("referenceLines", RecipeStepSchemaBuilders.Array(RecipeStepSchemaBuilders.Object(
                    [
                        ("columnId", NullableText("The id of the value column the line belongs to.")),
                        ("kind", RecipeStepSchemaBuilders.String().Enum("Average", "Median", "Constant").Description("Whether the line marks the column's average, its median or a constant value.")),
                        ("value", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The value of a Constant line.")),
                        ("label", NullableText("The label of the line.")),
                    ])).Description("Lines a chart draws across its values.")),
            ],
            ["id", "type"])
            .Description("A visual of a report.");

    private static JsonSchemaBuilder Container()
        => RecipeStepSchemaBuilders.Object(
            [
                ("id", Text("The identifier of the container, unique within the report, which visuals name in containerId.")),
                ("title", NullableText("The title shown in the container's header band.")),
                ("width", RecipeStepSchemaBuilders.Integer().Minimum(1).Maximum(12).Default(12).Description("The width of the container on a twelve-column grid.")),
                ("height", RecipeStepSchemaBuilders.Integer().Minimum(0).Description("The height of the container's body in pixels, which scrolls when its visuals are taller, or 0 to grow with its visuals.")),
            ],
            ["id"])
            .Description("A container of a report.");

    private static JsonSchemaBuilder ItemId(string noun)
        => RecipeStepSchemaBuilders.String()
            .Pattern(ItemIdPattern)
            .Description($"The identifier of the {noun}: up to 64 letters, digits, dashes and underscores. A {noun} without a valid itemId is refused.");

    private static JsonSchemaBuilder Text(string description)
        => RecipeStepSchemaBuilders.String().Description(description);

    private static JsonSchemaBuilder NullableText(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder Timestamp(string description)
        => RecipeStepSchemaBuilders.String().Description(description);

    private static JsonSchemaBuilder NullableTimestamp(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}
