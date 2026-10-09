using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Designer.ViewModels;

/// <summary>
/// Builds the localized texts and option labels the designer page script shows.
/// </summary>
public static class ReportDesignerTexts
{
    /// <summary>
    /// Builds the texts of the designer script, keyed by their English source text.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <returns>The texts.</returns>
    public static Dictionary<string, string> Build(IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Number of rows"] = S["Number of rows"].Value,
            ["This data set is not available to you."] = S["This data set is not available to you."].Value,
            ["The design could not be checked. Try again."] = S["The design could not be checked. Try again."].Value,
            ["Add a data set, then drag fields to Columns to see a preview."] = S["Add a data set, then drag fields to Columns to see a preview."].Value,
            ["The preview could not be loaded."] = S["The preview could not be loaded."].Value,
            ["The design was not saved."] = S["The design was not saved."].Value,
            ["Saved. Fix these problems before people run it:"] = S["Saved. Fix these problems before people run it:"].Value,
            ["Saved."] = S["Saved."].Value,
            ["Close"] = S["Close"].Value,
            ["Unsaved changes"] = S["Unsaved changes"].Value,
            ["This design has problems:"] = S["This design has problems:"].Value,
            ["View name"] = S["View name"].Value,
            ["Report title"] = S["Report title"].Value,
            ["Save"] = S["Save"].Value,
            ["Run report"] = S["Run report"].Value,
            ["Design"] = S["Design"].Value,
            ["Preview"] = S["Preview"].Value,
            ["Refresh"] = S["Refresh"].Value,
            ["Settings"] = S["Settings"].Value,
            ["Sharing"] = S["Sharing"].Value,
            ["Remove"] = S["Remove"].Value,
            ["Columns"] = S["Columns"].Value,
            ["Drag fields here. Numbers are summed; add a dimension to group them."] = S["Drag fields here. Numbers are summed; add a dimension to group them."].Value,
            ["Filters"] = S["Filters"].Value,
            ["Drag fields here to filter the data."] = S["Drag fields here to filter the data."].Value,
            ["Viewer"] = S["Viewer"].Value,
            ["Change direction"] = S["Change direction"].Value,
            ["Add sort…"] = S["Add sort…"].Value,
            ["Add sort"] = S["Add sort"].Value,
            ["Sort"] = S["Sort"].Value,
            ["Top rows"] = S["Top rows"].Value,
            ["All"] = S["All"].Value,
            ["Field"] = S["Field"].Value,
            ["Header"] = S["Header"].Value,
            ["Aggregate"] = S["Aggregate"].Value,
            ["Pick None to group the result by this column."] = S["Pick None to group the result by this column."].Value,
            ["Transform"] = S["Transform"].Value,
            ["Format"] = S["Format"].Value,
            ["A .NET format, such as N0 for whole numbers, C2 for currency, or MMM yyyy for months."] = S["A .NET format, such as N0 for whole numbers, C2 for currency, or MMM yyyy for months."].Value,
            ["Hide from tables"] = S["Hide from tables"].Value,
            ["A hidden column still groups the data and can feed charts."] = S["A hidden column still groups the data and can feed charts."].Value,
            ["Value"] = S["Value"].Value,
            ["Any"] = S["Any"].Value,
            ["Yes"] = S["Yes"].Value,
            ["No"] = S["No"].Value,
            ["Values"] = S["Values"].Value,
            ["One value per line."] = S["One value per line."].Value,
            ["Between"] = S["Between"].Value,
            ["From"] = S["From"].Value,
            ["To"] = S["To"].Value,
            ["Leave a side empty to leave it open."] = S["Leave a side empty to leave it open."].Value,
            ["Days"] = S["Days"].Value,
            ["Applies to"] = S["Applies to"].Value,
            ["Rows, before grouping"] = S["Rows, before grouping"].Value,
            ["Result, after grouping"] = S["Result, after grouping"].Value,
            ["Column"] = S["Column"].Value,
            ["Condition"] = S["Condition"].Value,
            ["Let viewers change this filter"] = S["Let viewers change this filter"].Value,
            ["Shows the filter above the report. The values above become its defaults."] = S["Shows the filter above the report. The values above become its defaults."].Value,
            ["Filter label"] = S["Filter label"].Value,
            ["Control"] = S["Control"].Value,
            ["Properties"] = S["Properties"].Value,
            ["Filter"] = S["Filter"].Value,
            ["Select a column or filter to change it."] = S["Select a column or filter to change it."].Value,
            ["None"] = S["None"].Value,
            ["Title"] = S["Title"].Value,
            ["Width"] = S["Width"].Value,
            ["Quarter"] = S["Quarter"].Value,
            ["Third"] = S["Third"].Value,
            ["Half"] = S["Half"].Value,
            ["Two thirds"] = S["Two thirds"].Value,
            ["Full"] = S["Full"].Value,
            ["Chart type"] = S["Chart type"].Value,
            ["Categories"] = S["Categories"].Value,
            ["Split into series by"] = S["Split into series by"].Value,
            ["Draws one series per value of this column, using the first value column."] = S["Draws one series per value of this column, using the first value column."].Value,
            ["Stack series"] = S["Stack series"].Value,
            ["Show legend"] = S["Show legend"].Value,
            ["Rows"] = S["Rows"].Value,
            ["Columns across"] = S["Columns across"].Value,
            ["Show totals"] = S["Show totals"].Value,
            ["Columns shown"] = S["Columns shown"].Value,
            ["Leave all unchecked to show every visible column."] = S["Leave all unchecked to show every visible column."].Value,
            ["Move up"] = S["Move up"].Value,
            ["Visuals"] = S["Visuals"].Value,
            ["Without visuals the report shows one table of the result."] = S["Without visuals the report shows one table of the result."].Value,
            ["Add to columns"] = S["Add to columns"].Value,
            ["Add to filters"] = S["Add to filters"].Value,
            ["Loading…"] = S["Loading…"].Value,
            ["Base"] = S["Base"].Value,
            ["Remove data set"] = S["Remove data set"].Value,
            ["Pick a field"] = S["Pick a field"].Value,
            ["Field before"] = S["Field before"].Value,
            ["Field of the joined data set"] = S["Field of the joined data set"].Value,
            ["Join"] = S["Join"].Value,
            ["Only rows that match on both sides"] = S["Only rows that match on both sides"].Value,
            ["All rows before, matching rows of this data set"] = S["All rows before, matching rows of this data set"].Value,
            ["All rows of this data set, matching rows before"] = S["All rows of this data set, matching rows before"].Value,
            ["All rows of both sides"] = S["All rows of both sides"].Value,
            ["Join type"] = S["Join type"].Value,
            ["Match fields"] = S["Match fields"].Value,
            ["Search fields"] = S["Search fields"].Value,
            ["Data"] = S["Data"].Value,
            ["Add data set"] = S["Add data set"].Value,
            ["Start by adding a data set, such as a content type."] = S["Start by adding a data set, such as a content type."].Value,
            ["Relationships"] = S["Relationships"].Value,
            ["Edit formula"] = S["Edit formula"].Value,
            ["Calculated fields"] = S["Calculated fields"].Value,
            ["New"] = S["New"].Value,
            ["Pick a data source"] = S["Pick a data source"].Value,
            ["Data source"] = S["Data source"].Value,
            ["Search data sets"] = S["Search data sets"].Value,
            ["No data sets found."] = S["No data sets found."].Value,
            ["Pick a data source first."] = S["Pick a data source first."].Value,
            ["Label, such as Profit margin"] = S["Label, such as Profit margin"].Value,
            ["Name used in formulas, such as ProfitMargin"] = S["Name used in formulas, such as ProfitMargin"].Value,
            ["Enter a valid name and formula."] = S["Enter a valid name and formula."].Value,
            ["The formula is valid."] = S["The formula is valid."].Value,
            ["Result type"] = S["Result type"].Value,
            ["aggregated per group"] = S["aggregated per group"].Value,
            ["Edit calculated field"] = S["Edit calculated field"].Value,
            ["New calculated field"] = S["New calculated field"].Value,
            ["Label"] = S["Label"].Value,
            ["Name"] = S["Name"].Value,
            ["Formula"] = S["Formula"].Value,
            ["Write fields in square brackets. Use an aggregate function such as SUM or COUNTD to calculate one value per group."] = S["Write fields in square brackets. Use an aggregate function such as SUM or COUNTD to calculate one value per group."].Value,
            ["Fields"] = S["Fields"].Value,
            ["Functions"] = S["Functions"].Value,
            ["Delete"] = S["Delete"].Value,
            ["Check"] = S["Check"].Value,
            ["Apply"] = S["Apply"].Value,
            ["Description"] = S["Description"].Value,
            ["Tells other designers what the view prepares."] = S["Tells other designers what the view prepares."].Value,
            ["Shown above the report."] = S["Shown above the report."].Value,
            ["Category"] = S["Category"].Value,
            ["Groups the report in the admin menu and the report list."] = S["Groups the report in the admin menu and the report list."].Value,
            ["Show in the admin menu"] = S["Show in the admin menu"].Value,
            ["Adds the report under Reports in the admin menu for everyone who can open it."] = S["Adds the report under Reports in the admin menu for everyone who can open it."].Value,
            ["Let people the report is shared with export it"] = S["Let people the report is shared with export it"].Value,
            ["Search people by user name or email"] = S["Search people by user name or email"].Value,
            ["Search people"] = S["Search people"].Value,
            ["Everyone, including visitors who are not signed in, can open the report."] = S["Everyone, including visitors who are not signed in, can open the report."].Value,
            ["Everyone who is signed in can open the report."] = S["Everyone who is signed in can open the report."].Value,
            ["Never"] = S["Never"].Value,
            ["Save the report to create share links."] = S["Save the report to create share links."].Value,
            ["You are not allowed to create share links."] = S["You are not allowed to create share links."].Value,
            ["What the link is for"] = S["What the link is for"].Value,
            ["Copy the link now. For security it is not shown again."] = S["Copy the link now. For security it is not shown again."].Value,
            ["Copy"] = S["Copy"].Value,
            ["The report has no share links."] = S["The report has no share links."].Value,
            ["Link"] = S["Link"].Value,
            ["Expires"] = S["Expires"].Value,
            ["Allows"] = S["Allows"].Value,
            ["Status"] = S["Status"].Value,
            ["Revoked"] = S["Revoked"].Value,
            ["Expired"] = S["Expired"].Value,
            ["Active"] = S["Active"].Value,
            ["Unnamed link"] = S["Unnamed link"].Value,
            ["Export"] = S["Export"].Value,
            ["Signed-in people only"] = S["Signed-in people only"].Value,
            ["Anyone with the link"] = S["Anyone with the link"].Value,
            ["Revoke"] = S["Revoke"].Value,
            ["Note"] = S["Note"].Value,
            ["Allow export"] = S["Allow export"].Value,
            ["Require sign-in"] = S["Require sign-in"].Value,
            ["The link could not be created."] = S["The link could not be created."].Value,
            ["Create link"] = S["Create link"].Value,
            ["A shared report shows its data with your access, so people see what the report shows even if they cannot open that data themselves."] = S["A shared report shows its data with your access, so people see what the report shows even if they cannot open that data themselves."].Value,
            ["People"] = S["People"].Value,
            ["Roles"] = S["Roles"].Value,
            ["Share links"] = S["Share links"].Value,
            ["A share link opens the report for anyone who has it, until it expires or you revoke it."] = S["A share link opens the report for anyone who has it, until it expires or you revoke it."].Value,
        };
    }

    /// <summary>
    /// Builds the labels of the aggregates.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <returns>The labels by aggregate name.</returns>
    public static Dictionary<string, string> Aggregates(IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["None"] = S["None"].Value,
            ["Count"] = S["Count"].Value,
            ["CountDistinct"] = S["Count distinct"].Value,
            ["Sum"] = S["Sum"].Value,
            ["Average"] = S["Average"].Value,
            ["Min"] = S["Minimum"].Value,
            ["Max"] = S["Maximum"].Value,
            ["Median"] = S["Median"].Value,
        };
    }

    /// <summary>
    /// Builds the labels of the transforms.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <returns>The labels by transform name.</returns>
    public static Dictionary<string, string> Transforms(IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["None"] = S["None"].Value,
            ["Upper"] = S["Upper case"].Value,
            ["Lower"] = S["Lower case"].Value,
            ["Trim"] = S["Trim spaces"].Value,
            ["Length"] = S["Number of characters"].Value,
            ["Year"] = S["Year"].Value,
            ["Quarter"] = S["Quarter"].Value,
            ["Month"] = S["Month"].Value,
            ["Week"] = S["Week"].Value,
            ["Day"] = S["Day"].Value,
            ["DayOfWeek"] = S["Day of week"].Value,
            ["Hour"] = S["Hour of day"].Value,
            ["MonthOfYear"] = S["Month of year"].Value,
            ["Round"] = S["Round to whole number"].Value,
        };
    }

    /// <summary>
    /// Builds the labels of the filter operators.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <returns>The labels by operator name.</returns>
    public static Dictionary<string, string> Operators(IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Equals"] = S["is"].Value,
            ["NotEquals"] = S["is not"].Value,
            ["Contains"] = S["contains"].Value,
            ["NotContains"] = S["does not contain"].Value,
            ["StartsWith"] = S["starts with"].Value,
            ["EndsWith"] = S["ends with"].Value,
            ["GreaterThan"] = S["is greater than (after)"].Value,
            ["GreaterThanOrEqual"] = S["is at least (on or after)"].Value,
            ["LessThan"] = S["is less than (before)"].Value,
            ["LessThanOrEqual"] = S["is at most (on or before)"].Value,
            ["Between"] = S["is between"].Value,
            ["In"] = S["is one of"].Value,
            ["NotIn"] = S["is none of"].Value,
            ["IsEmpty"] = S["is empty"].Value,
            ["IsNotEmpty"] = S["is not empty"].Value,
            ["InLastDays"] = S["is in the last number of days"].Value,
            ["InNextDays"] = S["is in the next number of days"].Value,
        };
    }

    /// <summary>
    /// Builds the labels of the exposed filter controls.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <returns>The labels by control name.</returns>
    public static Dictionary<string, string> Controls(IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Auto"] = S["Automatic"].Value,
            ["Text"] = S["Text box"].Value,
            ["Select"] = S["Drop-down list"].Value,
            ["MultiSelect"] = S["List with several choices"].Value,
            ["DateRange"] = S["Date range"].Value,
            ["NumberRange"] = S["Number range"].Value,
            ["Boolean"] = S["Yes or no"].Value,
        };
    }

    /// <summary>
    /// Builds the labels of the chart types.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <returns>The labels by chart type name.</returns>
    public static Dictionary<string, string> Charts(IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Bar"] = S["Bar"].Value,
            ["HorizontalBar"] = S["Horizontal bar"].Value,
            ["Line"] = S["Line"].Value,
            ["Area"] = S["Area"].Value,
            ["Pie"] = S["Pie"].Value,
            ["Doughnut"] = S["Doughnut"].Value,
        };
    }

    /// <summary>
    /// Builds the labels of the visual types.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <returns>The labels by visual type name.</returns>
    public static Dictionary<string, string> Visuals(IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Table"] = S["Table"].Value,
            ["Chart"] = S["Chart"].Value,
            ["Metrics"] = S["Metrics"].Value,
            ["Pivot"] = S["Pivot table"].Value,
        };
    }
}
