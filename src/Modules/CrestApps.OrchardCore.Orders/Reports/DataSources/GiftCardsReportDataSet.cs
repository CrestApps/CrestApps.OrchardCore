using System.Linq.Expressions;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.Reports.DataSources;

/// <summary>
/// One row per gift card: what it was worth, what is left on it, and the order that issued it. The code that pays with
/// a card is never exposed, only its last characters, as the gift cards page shows them.
/// </summary>
public sealed class GiftCardsReportDataSet : IndexedReportDataSet<GiftCard, GiftCardIndex>, IOrderReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the gift card was issued, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="GiftCardsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session gift cards are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GiftCardsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<GiftCardsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.OrderSource.GiftCards,
                stringLocalizer["Gift cards"],
                stringLocalizer["One row per gift card: its value, balance and what was spent in its currency, whether it can pay, and the order that issued it."]),
            session,
            authorizationService,
            OrderConstants.CollectionName,
            OrderPermissions.ManageGiftCards)
    {
        var S = stringLocalizer;
        var card = S["Gift card"].Value;
        var inCurrency = S["In the gift card's currency."].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Gift card ID"], DataFieldType.Text, record => record.ItemId, card).Identifier();
        AddField(nameof(GiftCard.LastCharacters), S["Code ending"], DataFieldType.Text, record => record.LastCharacters, card);
        AddField(nameof(GiftCard.Currency), S["Currency"], DataFieldType.Text, record => record.Currency, card);
        AddField(nameof(GiftCard.InitialValue), S["Initial value"], DataFieldType.Decimal, record => record.InitialValue, card).Description = inCurrency;
        AddField(nameof(GiftCard.Balance), S["Balance"], DataFieldType.Decimal, record => record.Balance, card).Description = inCurrency;
        AddField("SpentAmount", S["Spent"], DataFieldType.Decimal, record => Math.Max(0m, record.InitialValue - record.Balance), card).Description = inCurrency;
        AddField(nameof(GiftCard.IsEnabled), S["Enabled"], DataFieldType.Boolean, record => record.IsEnabled, card);
        AddField(nameof(GiftCard.ExpiresUtc), S["Expires"], DataFieldType.DateTime, record => record.ExpiresUtc, card);
        AddField(nameof(GiftCard.RecipientName), S["Recipient name"], DataFieldType.Text, record => record.RecipientName, card);
        AddField(nameof(GiftCard.RecipientEmail), S["Recipient email"], DataFieldType.Text, record => record.RecipientEmail, card);
        AddField(nameof(GiftCard.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, card)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(GiftCard.OrderNumber), S["Order number"], DataFieldType.Text, record => record.OrderNumber, card);
        AddField("UseCount", S["Times used"], DataFieldType.Integer, record => record.Activity?.Count(activity => activity.Amount < 0m) ?? 0, card);
        AddField("LastUsedUtc", S["Last used"], DataFieldType.DateTime, record => record.Activity?.Where(activity => activity.Amount < 0m).Select(activity => (DateTime?)activity.CreatedUtc).Max(), card);
        AddField(CreatedUtcField, S["Issued"], DataFieldType.DateTime, record => record.CreatedUtc, card);
        AddField(nameof(GiftCard.UpdatedUtc), S["Last changed"], DataFieldType.DateTime, record => record.UpdatedUtc, card);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<GiftCardIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<GiftCardIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<GiftCardIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(GiftCard.OrderId)] = index => index.OrderId,
        };

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> GetAggregateColumns()
    {
        // The index keeps the recipient's email address in lower case, so it is not the field's value.
        var columns = new Dictionary<string, string>(base.GetAggregateColumns(), StringComparer.Ordinal);

        columns.Remove(nameof(GiftCard.RecipientEmail));

        return columns;
    }
}
