using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tickets.Reports.DataSources;

/// <summary>
/// The ticket categories of every ticket type, one row per category: its name, the category above it, its level and path,
/// and the desk, team, priority and service-level policy it routes to (its own, or inherited from above), so the Report
/// Builder joins tickets to categories and groups by any level. Types that share a taxonomy list each category once. A
/// reader with no ticket scope gets no rows, like the other ticket data sets.
/// </summary>
internal sealed class TicketCategoriesReportDataSet : RecordDataSet<TicketCategory>
{
    private readonly TicketReportAccess _access;
    private TicketReportNames _names;

    public TicketCategoriesReportDataSet(TicketReportAccess access, IStringLocalizer<TicketCategoriesReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(
            TicketReportDataSets.TicketCategories,
            stringLocalizer["Ticket categories"],
            stringLocalizer["The ticket categories: each one's place in the tree and the desk, team, priority and service level it routes tickets to."]))
    {
        _access = access;

        var S = stringLocalizer;
        var category = S["Category"].Value;
        var routing = S["Routing"].Value;

        AddField(TicketReportDataSets.CategoryIdField, S["Category ID"], DataFieldType.Text, row => row.Id, category).Identifier();
        AddField("Name", S["Name"], DataFieldType.Text, row => row.Name, category);
        AddField("ParentId", S["Parent ID"], DataFieldType.Text, row => row.ParentId, category);
        AddField("Parent", S["Parent"], DataFieldType.Text, row => _names?.Category(row.ParentId)?.Name, category);
        AddField("Level", S["Level"], DataFieldType.Integer, row => row.Level, category).Description = S["0 for a top category, 1 below it, and so on."].Value;
        AddField("PathText", S["Path"], DataFieldType.Text, row => row.PathText, category).Description = S["The category with those above it, such as Hardware > Laptop."].Value;
        AddField("HasChildren", S["Has sub-categories"], DataFieldType.Boolean, row => row.HasChildren, category);
        AddField("CustomersCanChoose", S["Customers can choose it"], DataFieldType.Boolean, row => row.CustomersCanChoose, category);
        AddField("DeskId", S["Desk ID"], DataFieldType.Text, row => row.DeskId, routing);
        AddField("Desk", S["Desk"], DataFieldType.Text, row => _names?.Desk(row.DeskId), routing);
        AddField("TeamId", S["Team ID"], DataFieldType.Text, row => row.TeamId, routing);
        AddField("PriorityId", S["Priority ID"], DataFieldType.Text, row => row.PriorityId, routing);
        AddField("Priority", S["Priority"], DataFieldType.Text, row => _names?.Priority(row.PriorityId), routing);
        AddField("ServiceLevelPolicyId", S["Service-level policy ID"], DataFieldType.Text, row => row.ServiceLevelPolicyId, routing);
    }

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => await _access.GetScopeAsync(context?.User, cancellationToken) is not null;

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<TicketCategory> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (await _access.GetScopeAsync(query?.Context?.User, cancellationToken) is null)
        {
            yield break;
        }

        _names = await _access.GetNamesAsync(cancellationToken);

        foreach (var row in _names.Categories)
        {
            yield return row;
        }
    }
}
