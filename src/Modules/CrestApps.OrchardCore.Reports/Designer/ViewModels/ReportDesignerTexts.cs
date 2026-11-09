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
            ["The server stopped waiting for the report to run. Run it again, or narrow its filters."] = S["The server stopped waiting for the report to run. Run it again, or narrow its filters."].Value,
            ["Running the report…"] = S["Running the report…"].Value,
            ["Try again"] = S["Try again"].Value,
            ["The design was not saved."] = S["The design was not saved."].Value,
            ["Saved. Fix these problems before people run it:"] = S["Saved. Fix these problems before people run it:"].Value,
            ["Saved."] = S["Saved."].Value,
            ["Close"] = S["Close"].Value,
            ["Unsaved changes"] = S["Unsaved changes"].Value,
            ["This design has problems:"] = S["This design has problems:"].Value,
            ["Save"] = S["Save"].Value,
            ["Publish (Ctrl+S)"] = S["Publish (Ctrl+S)"].Value,
            ["Publish"] = S["Publish"].Value,
            ["Versions"] = S["Versions"].Value,
            ["Run report"] = S["Run report"].Value,
            ["Collapse the data pane"] = S["Collapse the data pane"].Value,
            ["Collapse the properties pane"] = S["Collapse the properties pane"].Value,
            ["Collapse the columns and filters"] = S["Collapse the columns and filters"].Value,
            ["Add data set"] = S["Add data set"].Value,
            ["Refresh"] = S["Refresh"].Value,
            ["Properties"] = S["Properties"].Value,
            ["Properties and visuals"] = S["Properties and visuals"].Value,
            ["Data"] = S["Data"].Value,
            ["Design"] = S["Design"].Value,
            ["Columns and filters"] = S["Columns and filters"].Value,
            ["Preview"] = S["Preview"].Value,
            ["Settings"] = S["Settings"].Value,
            ["Data model"] = S["Data model"].Value,
            ["Sharing"] = S["Sharing"].Value,
            ["Remove"] = S["Remove"].Value,
            ["Joins"] = S["Joins"].Value,
            ["Add the columns that must match."] = S["Add the columns that must match."].Value,
            ["Not joined yet"] = S["Not joined yet"].Value,
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
            ["Whole number, such as 1,235"] = S["Whole number, such as 1,235"].Value,
            ["Two decimals, such as 1,234.57"] = S["Two decimals, such as 1,234.57"].Value,
            ["Currency, such as $1,234.57"] = S["Currency, such as $1,234.57"].Value,
            ["Percentage of a fraction, such as 0.123 as 12.3%"] = S["Percentage of a fraction, such as 0.123 as 12.3%"].Value,
            ["One decimal without separators, such as 1234.6"] = S["One decimal without separators, such as 1234.6"].Value,
            ["Short date, such as 10/9/2026"] = S["Short date, such as 10/9/2026"].Value,
            ["Year, month and day, such as 2026-10-09"] = S["Year, month and day, such as 2026-10-09"].Value,
            ["Month and year, such as Oct 2026"] = S["Month and year, such as Oct 2026"].Value,
            ["Day of the week, such as Friday"] = S["Day of the week, such as Friday"].Value,
            ["Date and time, such as 10/9/2026 2:30 PM"] = S["Date and time, such as 10/9/2026 2:30 PM"].Value,
            ["Time of day, such as 14:30"] = S["Time of day, such as 14:30"].Value,
            ["Numbers"] = S["Numbers"].Value,
            ["Dates and times"] = S["Dates and times"].Value,
            ["Format"] = S["Format"].Value,
            ["Common formats"] = S["Common formats"].Value,
            ["Field"] = S["Field"].Value,
            ["Header"] = S["Header"].Value,
            ["Aggregate"] = S["Aggregate"].Value,
            ["Pick None to group the result by this column."] = S["Pick None to group the result by this column."].Value,
            ["Transform"] = S["Transform"].Value,
            ["How values are shown. Click the question mark for common formats."] = S["How values are shown. Click the question mark for common formats."].Value,
            ["Hide from tables"] = S["Hide from tables"].Value,
            ["A hidden column still groups the data and can feed charts."] = S["A hidden column still groups the data and can feed charts."].Value,
            ["All time"] = S["All time"].Value,
            ["Today"] = S["Today"].Value,
            ["Last 12 months"] = S["Last 12 months"].Value,
            ["Last"] = S["Last"].Value,
            ["days"] = S["days"].Value,
            ["Default period"] = S["Default period"].Value,
            ["The people who run the report can pick another period."] = S["The people who run the report can pick another period."].Value,
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
            ["Join"] = S["Join"].Value,
            ["Filter"] = S["Filter"].Value,
            ["Select a join, column, or filter to change it."] = S["Select a join, column, or filter to change it."].Value,
            ["Drop a field here"] = S["Drop a field here"].Value,
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
            ["Show subtotals"] = S["Show subtotals"].Value,
            ["Adds a subtotal after each group of the leading dimensions, such as per user within each role, then per role."] = S["Adds a subtotal after each group of the leading dimensions, such as per user within each role, then per role."].Value,
            ["Move up"] = S["Move up"].Value,
            ["Visuals"] = S["Visuals"].Value,
            ["Without visuals the report shows one table of the result."] = S["Without visuals the report shows one table of the result."].Value,
            ["Expand"] = S["Expand"].Value,
            ["Collapse"] = S["Collapse"].Value,
            ["Add to columns"] = S["Add to columns"].Value,
            ["Add to filters"] = S["Add to filters"].Value,
            ["Loading…"] = S["Loading…"].Value,
            ["Base"] = S["Base"].Value,
            ["Edit join"] = S["Edit join"].Value,
            ["Remove data set"] = S["Remove data set"].Value,
            ["Pick a column"] = S["Pick a column"].Value,
            ["Matching columns"] = S["Matching columns"].Value,
            ["Column of the data sets before"] = S["Column of the data sets before"].Value,
            ["Column of the joined data set"] = S["Column of the joined data set"].Value,
            ["to"] = S["to"].Value,
            ["Inner join"] = S["Inner join"].Value,
            ["Left join"] = S["Left join"].Value,
            ["Right join"] = S["Right join"].Value,
            ["Full join"] = S["Full join"].Value,
            ["Only rows that match on both sides"] = S["Only rows that match on both sides"].Value,
            ["All rows before, matching rows of this data set"] = S["All rows before, matching rows of this data set"].Value,
            ["All rows of this data set, matching rows before"] = S["All rows of this data set, matching rows before"].Value,
            ["All rows of both sides"] = S["All rows of both sides"].Value,
            ["Join type"] = S["Join type"].Value,
            ["Add matching columns"] = S["Add matching columns"].Value,
            ["Rows match when every pair of columns is equal. Add more pairs to match on several columns. Key columns are marked with a key."] = S["Rows match when every pair of columns is equal. Add more pairs to match on several columns. Key columns are marked with a key."].Value,
            ["Search fields"] = S["Search fields"].Value,
            ["Start by adding a data set, such as a content type."] = S["Start by adding a data set, such as a content type."].Value,
            ["Edit formula"] = S["Edit formula"].Value,
            ["Calculated fields"] = S["Calculated fields"].Value,
            ["New"] = S["New"].Value,
            ["Filter data sets"] = S["Filter data sets"].Value,
            ["Data sources"] = S["Data sources"].Value,
            ["Related"] = S["Related"].Value,
            ["Joined automatically when added."] = S["Joined automatically when added."].Value,
            ["Related to"] = S["Related to"].Value,
            ["Added"] = S["Added"].Value,
            ["Add again"] = S["Add again"].Value,
            ["Add"] = S["Add"].Value,
            ["No data sets match the filter."] = S["No data sets match the filter."].Value,
            ["Add Data Set"] = S["Add Data Set"].Value,
            ["No data sets are available to you."] = S["No data sets are available to you."].Value,
            ["No data sources are enabled."] = S["No data sources are enabled."].Value,
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
            ["The report was not published."] = S["The report was not published."].Value,
            ["Published as version"] = S["Published as version"].Value,
            ["Published. Nothing changed since the last version."] = S["Published. Nothing changed since the last version."].Value,
            ["Fix these problems before people run it:"] = S["Fix these problems before people run it:"].Value,
            ["Delete this report? It was never published, so nothing else is kept."] = S["Delete this report? It was never published, so nothing else is kept."].Value,
            ["Discard the unpublished changes? The report goes back to its published version."] = S["Discard the unpublished changes? The report goes back to its published version."].Value,
            ["The changes could not be discarded."] = S["The changes could not be discarded."].Value,
            ["Publishing keeps a version when the report changed. Restoring a version copies it into the draft, where you can check it and publish it."] = S["Publishing keeps a version when the report changed. Restoring a version copies it into the draft, where you can check it and publish it."].Value,
            ["This report has no versions yet. Publish it to keep one."] = S["This report has no versions yet. Publish it to keep one."].Value,
            ["Version"] = S["Version"].Value,
            ["Untitled report"] = S["Untitled report"].Value,
            ["Restored from version"] = S["Restored from version"].Value,
            ["Published"] = S["Published"].Value,
            ["Restore"] = S["Restore"].Value,
            ["The versions could not be loaded."] = S["The versions could not be loaded."].Value,
            ["Restore version"] = S["Restore version"].Value,
            ["It replaces the unpublished changes; publish it to make it the report people run."] = S["It replaces the unpublished changes; publish it to make it the report people run."].Value,
            ["The version could not be restored."] = S["The version could not be restored."].Value,
            ["Also editing:"] = S["Also editing:"].Value,
            ["Someone"] = S["Someone"].Value,
            ["deleted this report. Your changes are not saved."] = S["deleted this report. Your changes are not saved."].Value,
            ["Someone else is saving this report. Try again in a moment."] = S["Someone else is saving this report. Try again in a moment."].Value,
            ["changed this report"] = S["changed this report"].Value,
            ["Your latest changes are not saved."] = S["Your latest changes are not saved."].Value,
            ["Reload their changes"] = S["Reload their changes"].Value,
            ["Keep mine"] = S["Keep mine"].Value,
            ["published this report."] = S["published this report."].Value,
            ["discarded the unpublished changes."] = S["discarded the unpublished changes."].Value,
            ["restored a version of this report."] = S["restored a version of this report."].Value,
            ["changed this report."] = S["changed this report."].Value,
            ["Reload to get their changes."] = S["Reload to get their changes."].Value,
            ["Reload"] = S["Reload"].Value,
            ["also has this report open. Changes you both make can conflict."] = S["also has this report open. Changes you both make can conflict."].Value,
            ["This report is saved as a draft and was never published. Only you and the people who manage every report can see it."] = S["This report is saved as a draft and was never published. Only you and the people who manage every report can see it."].Value,
            ["Delete draft"] = S["Delete draft"].Value,
            ["by"] = S["by"].Value,
            ["Unpublished changes"] = S["Unpublished changes"].Value,
            ["People who run the report still see the published version."] = S["People who run the report still see the published version."].Value,
            ["Discard changes"] = S["Discard changes"].Value,
            ["Saving…"] = S["Saving…"].Value,
            ["The draft could not be saved. It is retried with your next change."] = S["The draft could not be saved. It is retried with your next change."].Value,
            ["Not saved"] = S["Not saved"].Value,
            ["Draft saved"] = S["Draft saved"].Value,
            ["Drag onto a column of another data set to join on it."] = S["Drag onto a column of another data set to join on it."].Value,
            ["Key column"] = S["Key column"].Value,
            ["Drag to move"] = S["Drag to move"].Value,
            ["Not joined"] = S["Not joined"].Value,
            ["Add data sets, then drag a column of one onto the matching column of another to join them."] = S["Add data sets, then drag a column of one onto the matching column of another to join them."].Value,
            ["Drag a column from one data set onto the matching column of another to join them, such as an order's customer onto the customer's id."] = S["Drag a column from one data set onto the matching column of another to join them, such as an order's customer onto the customer's id."].Value,
            ["Click a line or its badge to choose which rows to keep, or to match on more columns."] = S["Click a line or its badge to choose which rows to keep, or to match on more columns."].Value,
            ["Key columns are marked with a key and listed first."] = S["Key columns are marked with a key and listed first."].Value,
            ["Live"] = S["Live"].Value,
            ["Every 15 minutes"] = S["Every 15 minutes"].Value,
            ["Every hour"] = S["Every hour"].Value,
            ["Every 6 hours"] = S["Every 6 hours"].Value,
            ["Every day"] = S["Every day"].Value,
            ["Every {minutes} minutes"] = S["Every {minutes} minutes"].Value,
            ["The last refresh failed"] = S["The last refresh failed"].Value,
            ["Reports read the rows of the last successful refresh."] = S["Reports read the rows of the last successful refresh."].Value,
            ["Not refreshed yet. Reports run the view until its first refresh."] = S["Not refreshed yet. Reports run the view until its first refresh."].Value,
            ["Last refreshed {time} ({rows} rows)"] = S["Last refreshed {time} ({rows} rows)"].Value,
            ["The view could not be refreshed."] = S["The view could not be refreshed."].Value,
            ["Refresh now"] = S["Refresh now"].Value,
            ["Live runs the view every time a report reads it. A schedule stores the result, so reports read it quickly; the data is as old as the last refresh."] = S["Live runs the view every time a report reads it. A schedule stores the result, so reports read it quickly; the data is as old as the last refresh."].Value,
            ["View name"] = S["View name"].Value,
            ["Report title"] = S["Report title"].Value,
            ["Such as Revenue by region"] = S["Such as Revenue by region"].Value,
            ["Such as Sales by region"] = S["Such as Sales by region"].Value,
            ["Other reports list the view by this name."] = S["Other reports list the view by this name."].Value,
            ["Shown at the top of the report and in the report list."] = S["Shown at the top of the report and in the report list."].Value,
            ["Description"] = S["Description"].Value,
            ["Tells other report builders what the view prepares."] = S["Tells other report builders what the view prepares."].Value,
            ["Shown above the report."] = S["Shown above the report."].Value,
            ["Group"] = S["Group"].Value,
            ["No group"] = S["No group"].Value,
            ["Lists the report under this group in the admin menu and the report list."] = S["Lists the report under this group in the admin menu and the report list."].Value,
            ["Manage groups"] = S["Manage groups"].Value,
            ["Show in the admin menu"] = S["Show in the admin menu"].Value,
            ["Adds the report under Reports in the admin menu, inside its group, for everyone who can open it."] = S["Adds the report under Reports in the admin menu, inside its group, for everyone who can open it."].Value,
            ["Let people the report is shared with export it"] = S["Let people the report is shared with export it"].Value,
            ["Search people by user name or email"] = S["Search people by user name or email"].Value,
            ["Search people"] = S["Search people"].Value,
            ["Everyone, including visitors who are not signed in, can open the report."] = S["Everyone, including visitors who are not signed in, can open the report."].Value,
            ["Everyone who is signed in can open the report."] = S["Everyone who is signed in can open the report."].Value,
            ["Never"] = S["Never"].Value,
            ["Save the report to create share links."] = S["Save the report to create share links."].Value,
            ["You are not allowed to create share links."] = S["You are not allowed to create share links."].Value,
            ["Copy"] = S["Copy"].Value,
            ["The report has no share links."] = S["The report has no share links."].Value,
            ["Expires"] = S["Expires"].Value,
            ["Allows"] = S["Allows"].Value,
            ["Status"] = S["Status"].Value,
            ["Revoked"] = S["Revoked"].Value,
            ["Expired"] = S["Expired"].Value,
            ["Active"] = S["Active"].Value,
            ["Export"] = S["Export"].Value,
            ["Revoke"] = S["Revoke"].Value,
            ["Note"] = S["Note"].Value,
            ["Allow export"] = S["Allow export"].Value,
            ["The link could not be created."] = S["The link could not be created."].Value,
            ["Create link"] = S["Create link"].Value,
            ["A shared report shows its data with your access, so people see what the report shows even if they cannot open that data themselves."] = S["A shared report shows its data with your access, so people see what the report shows even if they cannot open that data themselves."].Value,
            ["People"] = S["People"].Value,
            ["The people who can open the report. Search by user name or email."] = S["The people who can open the report. Search by user name or email."].Value,
            ["Roles"] = S["Roles"].Value,
            ["Everyone in these roles can open the report."] = S["Everyone in these roles can open the report."].Value,
            ["Share links"] = S["Share links"].Value,
            ["The link was emailed to {0}. For security it is not shown again after you leave this page."] = S["The link was emailed to {0}. For security it is not shown again after you leave this page."].Value,
            ["Copy the link and send it to its recipients. For security it is not shown again."] = S["Copy the link and send it to its recipients. For security it is not shown again."].Value,
            ["Shared with"] = S["Shared with"].Value,
            ["Uses"] = S["Uses"].Value,
            ["Used up"] = S["Used up"].Value,
            ["{0} of {1}"] = S["{0} of {1}"].Value,
            ["Recipients"] = S["Recipients"].Value,
            ["The users of the site the link is for. Only they can open it, once signed in."] = S["The users of the site the link is for. Only they can open it, once signed in."].Value,
            ["Optional. What the link is for, sent with the email and shown in the list of links."] = S["Optional. What the link is for, sent with the email and shown in the list of links."].Value,
            ["Expires after (days)"] = S["Expires after (days)"].Value,
            ["Most uses"] = S["Most uses"].Value,
            ["0 for no limit. Opening the report, changing its filters and exporting it are each a use."] = S["0 for no limit. Opening the report, changing its filters and exporting it are each a use."].Value,
            ["Email the link to the recipients"] = S["Email the link to the recipients"].Value,
            ["Needs the Email feature. Otherwise, copy the link and send it yourself."] = S["Needs the Email feature. Otherwise, copy the link and send it yourself."].Value,
            ["Pick the people the link is for."] = S["Pick the people the link is for."].Value,
            ["A share link sends the report to users of the site, who open it once signed in, until it expires, is used as many times as allowed, or you revoke it."] = S["A share link sends the report to users of the site, who open it once signed in, until it expires, is used as many times as allowed, or you revoke it."].Value,
            ["Container"] = S["Container"].Value,
            ["Shown in the band across the top of the container."] = S["Shown in the band across the top of the container."].Value,
            ["The widths of its visuals divide the width of the container."] = S["The widths of its visuals divide the width of the container."].Value,
            ["Height"] = S["Height"].Value,
            ["Grow with its visuals"] = S["Grow with its visuals"].Value,
            ["Visuals taller than the container scroll inside it."] = S["Visuals taller than the container scroll inside it."].Value,
            ["Frames the visual with the others in the container."] = S["Frames the visual with the others in the container."].Value,
            ["Visuals in the container"] = S["Visuals in the container"].Value,
            ["Containers"] = S["Containers"].Value,
            ["Containers frame visuals under a title and scroll them within a height, so one page can show everything."] = S["Containers frame visuals under a title and scroll them within a height, so one page can show everything."].Value,
            ["Add container"] = S["Add container"].Value,
            ["Main query"] = S["Main query"].Value,
            ["Query"] = S["Query"].Value,
            ["Query name"] = S["Query name"].Value,
            ["A new query starts as a copy of this one. The viewer filters it copies stay linked: one control filters both."] = S["A new query starts as a copy of this one. The viewer filters it copies stay linked: one control filters both."].Value,
            ["Add query"] = S["Add query"].Value,
            ["Remove this query and the visuals that show it?"] = S["Remove this query and the visuals that show it?"].Value,
            ["Remove query"] = S["Remove query"].Value,
            ["Each query groups its own data; pick the query a visual shows in its properties."] = S["Each query groups its own data; pick the query a visual shows in its properties."].Value,
            ["Add a query to show data grouped another way, such as by day next to a table by agent."] = S["Add a query to show data grouped another way, such as by day next to a table by agent."].Value,
            ["The visual shows the columns of this query."] = S["The visual shows the columns of this query."].Value,
            ["When clicked"] = S["When clicked"].Value,
            ["Pick the value of"] = S["Pick the value of"].Value,
            ["Nothing, clicks do nothing"] = S["Nothing, clicks do nothing"].Value,
            ["Clicking a bar, point or slice picks its category or series."] = S["Clicking a bar, point or slice picks its category or series."].Value,
            ["Clicking a row picks its value in this column."] = S["Clicking a row picks its value in this column."].Value,
            ["Add another visual for this one to filter."] = S["Add another visual for this one to filter."].Value,
            ["The same field"] = S["The same field"].Value,
            ["Field filtered in"] = S["Field filtered in"].Value,
            ["Its query doesn't read this field. Pick the field to filter."] = S["Its query doesn't read this field. Pick the field to filter."].Value,
            ["Filter these visuals"] = S["Filter these visuals"].Value,
            ["They show only the rows with the picked values. Hold Ctrl to pick several; click again to clear."] = S["They show only the rows with the picked values. Hold Ctrl to pick several; click again to clear."].Value,
            ["Absolute value"] = S["Absolute value"].Value,
            ["Add red, orange, green"] = S["Add red, orange, green"].Value,
            ["Add rule"] = S["Add rule"].Value,
            ["Add to Columns"] = S["Add to Columns"].Value,
            ["Add to Rows"] = S["Add to Rows"].Value,
            ["Capitalize words"] = S["Capitalize words"].Value,
            ["Cell fill"] = S["Cell fill"].Value,
            ["Cell text"] = S["Cell text"].Value,
            ["Color"] = S["Color"].Value,
            ["Color rules"] = S["Color rules"].Value,
            ["Color scale"] = S["Color scale"].Value,
            ["Color the cells whose value meets a condition. The first rule a value meets wins."] = S["Color the cells whose value meets a condition. The first rule a value meets wins."].Value,
            ["Color the text"] = S["Color the text"].Value,
            ["Colors"] = S["Colors"].Value,
            ["Count (distinct)"] = S["Count (distinct)"].Value,
            ["Count of yes"] = S["Count of yes"].Value,
            ["Custom"] = S["Custom"].Value,
            ["Custom color"] = S["Custom color"].Value,
            ["Custom formula…"] = S["Custom formula…"].Value,
            ["Days until today"] = S["Days until today"].Value,
            ["Dimension"] = S["Dimension"].Value,
            ["Dimensions on Rows head the rows; dimensions on Columns then spread across the top."] = S["Dimensions on Rows head the rows; dimensions on Columns then spread across the top."].Value,
            ["Drag dimensions here to head the rows. The dimensions on Columns then spread across the top."] = S["Drag dimensions here to head the rows. The dimensions on Columns then spread across the top."].Value,
            ["Earliest"] = S["Earliest"].Value,
            ["Fill the cell"] = S["Fill the cell"].Value,
            ["Filter on this field"] = S["Filter on this field"].Value,
            ["Format and colors…"] = S["Format and colors…"].Value,
            ["Green, yellow, red (low to high)"] = S["Green, yellow, red (low to high)"].Value,
            ["Header fill"] = S["Header fill"].Value,
            ["Header text"] = S["Header text"].Value,
            ["High"] = S["High"].Value,
            ["Insert a common formula"] = S["Insert a common formula"].Value,
            ["Is empty"] = S["Is empty"].Value,
            ["Latest"] = S["Latest"].Value,
            ["Low"] = S["Low"].Value,
            ["Lower case"] = S["Lower case"].Value,
            ["Maximum"] = S["Maximum"].Value,
            ["Measure"] = S["Measure"].Value,
            ["Middle"] = S["Middle"].Value,
            ["Minimum"] = S["Minimum"].Value,
            ["Month number"] = S["Month number"].Value,
            ["More options"] = S["More options"].Value,
            ["Move to Columns"] = S["Move to Columns"].Value,
            ["Move to Rows"] = S["Move to Rows"].Value,
            ["Number of characters"] = S["Number of characters"].Value,
            ["Other"] = S["Other"].Value,
            ["Paint"] = S["Paint"].Value,
            ["Red below a value, orange between two values, green from the second value."] = S["Red below a value, orange between two values, green from the second value."].Value,
            ["Red, yellow, green (low to high)"] = S["Red, yellow, green (low to high)"].Value,
            ["Round to 2 decimals"] = S["Round to 2 decimals"].Value,
            ["Search functions"] = S["Search functions"].Value,
            ["Shades each number from the low color at the smallest value to the high color at the largest."] = S["Shades each number from the low color at the smallest value to the high color at the largest."].Value,
            ["Shelf"] = S["Shelf"].Value,
            ["Sort ascending"] = S["Sort ascending"].Value,
            ["Sort descending"] = S["Sort descending"].Value,
            ["Start of the month"] = S["Start of the month"].Value,
            ["Trim spaces"] = S["Trim spaces"].Value,
            ["Upper case"] = S["Upper case"].Value,
            ["White to blue"] = S["White to blue"].Value,
            ["White to red"] = S["White to red"].Value,
            ["Column options"] = S["Column options"].Value,
            ["A quick table calculation over the values each table and chart shows. Totals rows keep the plain totals."] = S["A quick table calculation over the values each table and chart shows. Totals rows keep the plain totals."].Value,
            ["Across the columns"] = S["Across the columns"].Value,
            ["Add reference line"] = S["Add reference line"].Value,
            ["Average"] = S["Average"].Value,
            ["Compute along"] = S["Compute along"].Value,
            ["Constant"] = S["Constant"].Value,
            ["Difference from previous"] = S["Difference from previous"].Value,
            ["Down the rows"] = S["Down the rows"].Value,
            ["Draw a line across the chart at the average or median of a value, or at a target."] = S["Draw a line across the chart at the average or median of a value, or at a target."].Value,
            ["Format like"] = S["Format like"].Value,
            ["The values it runs over: down each column, across each row, or every cell. In a chart, the series are its columns."] = S["The values it runs over: down each column, across each row, or every cell. In a chart, the series are its columns."].Value,
            ["Such as Target"] = S["Such as Target"].Value,
            ["Such as a target"] = S["Such as a target"].Value,
            ["Line at"] = S["Line at"].Value,
            ["Median"] = S["Median"].Value,
            ["Of"] = S["Of"].Value,
            ["Over the whole table"] = S["Over the whole table"].Value,
            ["Percent difference from previous"] = S["Percent difference from previous"].Value,
            ["Percent of total"] = S["Percent of total"].Value,
            ["Rank"] = S["Rank"].Value,
            ["Reference lines"] = S["Reference lines"].Value,
            ["Running total"] = S["Running total"].Value,
            ["Show as"] = S["Show as"].Value,
            ["Pick a column…"] = S["Pick a column…"].Value,
            ["The value is shown in the format of this column."] = S["The value is shown in the format of this column."].Value,
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
            ["RelativeDate"] = S["Recent period (last 30 days...)"].Value,
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
    /// Builds the short labels of the join types.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <returns>The labels by join type name.</returns>
    public static Dictionary<string, string> Joins(IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Inner"] = S["Inner join"].Value,
            ["Left"] = S["Left join"].Value,
            ["Right"] = S["Right join"].Value,
            ["Full"] = S["Full join"].Value,
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
