using System.Linq.Expressions;
using CrestApps.OrchardCore.PointOfSale.Indexes;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.PointOfSale.Reports.DataSources;

/// <summary>
/// One row per shift worked at a register: who opened and closed it, the cash in the drawer from the float to the
/// count, and what it sold, as kept when it closed. Money is in the shift's currency, which the <c>Currency</c>
/// field names. The note left at the close and the reasons given for cash put in or taken out are never exposed.
/// </summary>
public sealed class RegisterShiftsReportDataSet : IndexedReportDataSet<RegisterShift, RegisterShiftIndex>, IPointOfSaleReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the shift was opened, the data set's main date.
    /// </summary>
    public const string OpenedUtcField = "OpenedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterShiftsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session shifts are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RegisterShiftsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<RegisterShiftsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.PointOfSaleSource.Shifts,
                stringLocalizer["Register shifts"],
                stringLocalizer["One row per shift worked at a register: its staff, the cash expected and counted, and its sales, in the shift's currency."]),
            session,
            authorizationService,
            PosConstants.CollectionName,
            PosPermissions.ManageRegisters)
    {
        var S = stringLocalizer;
        var shift = S["Shift"].Value;
        var staff = S["Staff"].Value;
        var cash = S["Cash"].Value;
        var sales = S["Sales"].Value;
        var inCurrency = S["In the shift's currency."].Value;
        var keptAtClose = S["In the shift's currency, kept when the shift closed; empty while it is open."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Shift ID"], DataFieldType.Text, record => record.ItemId, shift).Identifier();
        AddField(nameof(RegisterShift.RegisterId), S["Register ID"], DataFieldType.Text, record => record.RegisterId, shift)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.PointOfSaleSource.Name, CommerceReportDataSets.PointOfSaleSource.Registers));
        AddField(nameof(RegisterShift.RegisterName), S["Register"], DataFieldType.Text, record => record.RegisterName, shift);
        AddField(nameof(RegisterShift.LocationId), S["Location ID"], DataFieldType.Text, record => record.LocationId, shift)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.InventorySource.Name, CommerceReportDataSets.InventorySource.Locations));
        AddField(nameof(RegisterShift.Status), S["Status"], DataFieldType.Text, record => record.Status, shift);
        AddField(OpenedUtcField, S["Opened"], DataFieldType.DateTime, record => record.OpenedUtc, shift);
        AddField(nameof(RegisterShift.ClosedUtc), S["Closed"], DataFieldType.DateTime, record => record.ClosedUtc, shift);

        AddField(nameof(RegisterShift.OpenedByUserId), S["Opened by user ID"], DataFieldType.Text, record => record.OpenedByUserId, staff).Identifier(ReportDataReferences.User());
        AddField(nameof(RegisterShift.OpenedByName), S["Opened by"], DataFieldType.Text, record => record.OpenedByName, staff);
        AddField(nameof(RegisterShift.ClosedByUserId), S["Closed by user ID"], DataFieldType.Text, record => record.ClosedByUserId, staff).Identifier(ReportDataReferences.User());
        AddField(nameof(RegisterShift.ClosedByName), S["Closed by"], DataFieldType.Text, record => record.ClosedByName, staff);

        AddField(nameof(RegisterShift.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, cash);
        AddField(nameof(RegisterShift.OpeningFloat), S["Opening float"], DataFieldType.Decimal, record => record.OpeningFloat, cash).Description = S["The cash put in the drawer at the start, in the shift's currency."].Value;
        AddField("CashMovementCount", S["Cash movements"], DataFieldType.Integer, record => record.CashMovements?.Count ?? 0, cash).Description = S["How many times cash was put in or taken out outside a sale, refunds at the till included."].Value;
        AddField("CashIn", S["Cash put in"], DataFieldType.Decimal, record => record.CashMovements?.Where(movement => movement.Amount > 0m).Sum(movement => movement.Amount) ?? 0m, cash).Description = inCurrency;
        AddField("CashOut", S["Cash taken out"], DataFieldType.Decimal, record => -(record.CashMovements?.Where(movement => movement.Amount < 0m).Sum(movement => movement.Amount) ?? 0m), cash).Description = S["Refunds at the till included, as a positive amount in the shift's currency."].Value;
        AddField(nameof(RegisterShift.ExpectedCash), S["Expected cash"], DataFieldType.Decimal, record => record.ExpectedCash, cash).Description = S["The cash the drawer should have held at the close, in the shift's currency; empty while the shift is open."].Value;
        AddField(nameof(RegisterShift.CountedCash), S["Counted cash"], DataFieldType.Decimal, record => record.CountedCash, cash).Description = S["The cash counted in the drawer at the close, in the shift's currency; empty while the shift is open."].Value;
        AddField("CashDifference", S["Difference"], DataFieldType.Decimal, record => record.CountedCash.HasValue && record.ExpectedCash.HasValue ? record.CountedCash.Value - record.ExpectedCash.Value : null, cash).Description = S["The cash counted less the cash expected, in the shift's currency: negative when the drawer was short."].Value;

        AddField("SaleCount", S["Sales"], DataFieldType.Integer, record => record.Summary?.Sales, sales).Description = S["How many sales the shift rang up, kept when it closed; empty while it is open."].Value;
        AddField("SalesTotal", S["Sales total"], DataFieldType.Decimal, record => record.Summary?.Total, sales).Description = keptAtClose;
        AddField("CashSales", S["Cash kept"], DataFieldType.Decimal, record => record.Summary?.Cash, sales).Description = S["The cash handed over for sales less the change given, in the shift's currency, kept when the shift closed; empty while it is open."].Value;
        AddField("CardSales", S["Card"], DataFieldType.Decimal, record => record.Summary?.Card, sales).Description = keptAtClose;
        AddField("OtherSales", S["Gift cards and balances"], DataFieldType.Decimal, record => record.Summary?.Other, sales).Description = keptAtClose;
        AddField("RefundedAmount", S["Refunded"], DataFieldType.Decimal, record => record.Summary?.Refunded, sales).Description = keptAtClose;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<RegisterShiftIndex, DateTime>> Column)? DateColumn
        => (OpenedUtcField, index => index.OpenedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<RegisterShiftIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<RegisterShiftIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(RegisterShift.RegisterId)] = index => index.RegisterId,
            [nameof(RegisterShift.LocationId)] = index => index.LocationId,
        };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps the status as a number, not the name the field reads.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove(nameof(RegisterShift.Status));

        return columns;
    }
}
