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
/// One row per register: where it sells from, the currency it takes, where the store is, and whether it is in use.
/// </summary>
public sealed class RegistersReportDataSet : IndexedReportDataSet<Register, RegisterIndex>, IPointOfSaleReportDataSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegistersReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session registers are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RegistersReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<RegistersReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.PointOfSaleSource.Registers,
                stringLocalizer["Registers"],
                stringLocalizer["One row per register: its location, currency, store address and whether it is in use."]),
            session,
            authorizationService,
            PosConstants.CollectionName,
            PosPermissions.ManageRegisters)
    {
        var S = stringLocalizer;
        var register = S["Register"].Value;
        var address = S["Address"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Register ID"], DataFieldType.Text, record => record.ItemId, register).Identifier();
        AddField(nameof(Register.Name), S["Register"], DataFieldType.Text, record => record.Name, register);
        AddField(nameof(Register.IsEnabled), S["Enabled"], DataFieldType.Boolean, record => record.IsEnabled, register);
        AddField(nameof(Register.LocationId), S["Location ID"], DataFieldType.Text, record => record.LocationId, register)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.InventorySource.Name, CommerceReportDataSets.InventorySource.Locations));
        AddField(nameof(Register.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, register).Description = S["The currency the register sells in; empty when it sells in the products' own currency."].Value;
        AddField(nameof(Register.Roles), S["Roles that may sell"], DataFieldType.Text, record => Join(record.Roles), register).Description = S["Empty when anyone who may sell at a register may sell here."].Value;
        AddField(nameof(Register.CreatedUtc), S["Added"], DataFieldType.DateTime, record => record.CreatedUtc, register);
        AddField(nameof(Register.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, register);

        AddField("Country", S["Country"], DataFieldType.Text, record => record.Address?.Country, address);
        AddField("Region", S["Region"], DataFieldType.Text, record => record.Address?.Region, address);
        AddField("City", S["City"], DataFieldType.Text, record => record.Address?.City, address);
        AddField("PostalCode", S["Postal code"], DataFieldType.Text, record => record.Address?.PostalCode, address);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<RegisterIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<RegisterIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(Register.LocationId)] = index => index.LocationId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<RegisterIndex> columns)
    {
        columns.Text(nameof(Register.Name), nameof(RegisterIndex.Name));
    }

    private static string Join(IEnumerable<string> values)
    {
        var text = string.Join(", ", (values ?? []).Where(value => !string.IsNullOrEmpty(value)));

        return text.Length == 0 ? null : text;
    }
}
