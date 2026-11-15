using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Payroll.DataSources;

/// <summary>A garnishment order with the names of its person and employer.</summary>
/// <param name="Order">The order.</param>
/// <param name="EmployeeName">The person's name.</param>
/// <param name="EmployerName">The employer's name.</param>
/// <param name="ShowsCase">Whether the reader manages garnishments for the employer, and so sees the case number and agency.</param>
public sealed record GarnishmentRow(GarnishmentOrder Order, string EmployeeName, string EmployerName, bool ShowsCase = false);

/// <summary>
/// Garnishments: one row per order of the employers a user may export payroll data for.
/// </summary>
public sealed class GarnishmentsDataSet : RecordDataSet<GarnishmentRow>, IPayrollDataSet
{
    private readonly IGarnishmentOrderManager _orders;
    private readonly PayrollReportReader _reader;
    private readonly PayrollNames _names;

    public GarnishmentsDataSet(IGarnishmentOrderManager orders, PayrollReportReader reader, PayrollNames names, IStringLocalizer<GarnishmentsDataSet> stringLocalizer)
        : base(new DataSetDescriptor("Garnishments", stringLocalizer["Garnishments"], stringLocalizer["One row per garnishment order: the person and employer, the kind, case number and payee, what it asks, the total owed, what was withheld and what is left, and its status."]))
    {
        _orders = orders;
        _reader = reader;
        _names = names;

        var S = stringLocalizer;
        var group = S["Garnishment"].Value;

        AddField("GarnishmentId", S["Garnishment ID"], DataFieldType.Text, row => row.Order.ItemId, group).Identifier();
        AddField("EmployerId", S["Employer ID"], DataFieldType.Text, row => row.Order.EmployerId, group);
        AddField("EmployerName", S["Employer"], DataFieldType.Text, row => row.EmployerName, group);
        AddField("StaffMemberId", S["Staff member ID"], DataFieldType.Text, row => row.Order.StaffMemberId, group);
        AddField("EmployeeName", S["Name"], DataFieldType.Text, row => row.EmployeeName, group);
        AddField("Type", S["Kind"], DataFieldType.Text, row => row.Order.Type.ToString(), group);
        AddField("CaseNumber", S["Case number"], DataFieldType.Text, row => row.ShowsCase ? row.Order.CaseNumber : null, group);
        AddField("Agency", S["Court or agency"], DataFieldType.Text, row => row.ShowsCase ? row.Order.Agency : null, group);
        AddField("PayeeName", S["Payee"], DataFieldType.Text, row => row.Order.PayeeName, group);
        AddField("PaymentMethod", S["Paid by"], DataFieldType.Text, row => row.Order.PaymentMethod.ToString(), group);
        AddField("Amount", S["Amount each pay"], DataFieldType.Decimal, row => row.Order.Amount, group);
        AddField("Percent", S["Percent of disposable earnings"], DataFieldType.Decimal, row => row.Order.Percent, group);
        AddField("TotalOwed", S["Total owed"], DataFieldType.Decimal, row => row.Order.TotalOwed, group);
        AddField("Withheld", S["Withheld"], DataFieldType.Decimal, row => row.Order.Withheld, group);
        AddField("Remaining", S["Left"], DataFieldType.Decimal, row => row.Order.Remaining, group);
        AddField("ReceivedDate", S["Received"], DataFieldType.Date, row => Day(row.Order.ReceivedDate), group);
        AddField("StartDate", S["From"], DataFieldType.Date, row => Day(row.Order.StartDate), group);
        AddField("EndDate", S["To"], DataFieldType.Date, row => row.Order.EndDate is { } end ? Day(end) : null, group);
        AddField("Status", S["Status"], DataFieldType.Text, row => row.Order.Status.ToString(), group);

        Descriptor.DefaultDateField = "StartDate";
    }

    /// <inheritdoc />
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => !(await _reader.GetScopeAsync(context?.User, PayrollPermissions.ExportPayrollData, cancellationToken)).IsEmpty;

    protected override async IAsyncEnumerable<GarnishmentRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var scope = await _reader.GetScopeAsync(query?.Context?.User, PayrollPermissions.ExportPayrollData, cancellationToken);

        if (scope.IsEmpty)
        {
            yield break;
        }

        // The case number and the court say what the person owes and to whom: only for who manages the employer's orders.
        var manage = await _reader.GetScopeAsync(query?.Context?.User, PayrollPermissions.ManageGarnishments, cancellationToken);

        foreach (var order in await _orders.ListByEmployersAsync(scope.All ? null : [.. scope.EmployerIds], cancellationToken))
        {
            yield return new GarnishmentRow(order, await _names.StaffAsync(order.StaffMemberId), await _names.EmployerAsync(order.EmployerId), manage.Allows(order.EmployerId));
        }
    }

    private static DateTime? Day(DateOnly day)
        => day == default ? null : day.ToDateTime(TimeOnly.MinValue);
}
