using System.Linq.Expressions;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Indexes;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Customers.Reports.DataSources;

/// <summary>
/// One row per friend's first order a customer was rewarded for through their referral link: the order, and the store
/// credit or points the customer got. Store credit is in the currency the <c>Currency</c> field names. The friend's
/// name and email are never exposed. Links are read newest first, and each link gives its rewards oldest first.
/// </summary>
public sealed class ReferralRewardsReportDataSet : IndexedReportDataSet<ReferralProfile, ReferralProfileIndex, ReportChildRow<ReferralProfile, ReferralRecord>>, ICustomerReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the referral link a reward came from.
    /// </summary>
    public const string ReferralIdField = "ReferralId";

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferralRewardsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the referral links are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReferralRewardsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<ReferralRewardsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CustomerSource.ReferralRewards,
                stringLocalizer["Referral rewards"],
                stringLocalizer["One row per friend's first order a customer was rewarded for: the order, the kind of reward, and the store credit in its currency or the points given."]),
            session,
            authorizationService,
            CustomersConstants.CollectionName,
            CustomerPermissions.ManageCustomers)
    {
        var S = stringLocalizer;
        var referral = S["Referral link"].Value;
        var reward = S["Reward"].Value;

        AddField(ReferralIdField, S["Referral ID"], DataFieldType.Text, row => row.Parent.ItemId, referral)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Referrals));
        AddField(nameof(ReferralProfile.CustomerId), S["Customer ID"], DataFieldType.Text, row => row.Parent.CustomerId, referral)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers));

        AddField("Position", S["Reward number"], DataFieldType.Integer, row => row.Position, reward);
        AddField(nameof(ReferralRecord.CreatedUtc), S["Rewarded"], DataFieldType.DateTime, row => row.Child.CreatedUtc, reward);
        AddField(nameof(ReferralRecord.OrderId), S["Friend's order ID"], DataFieldType.Text, row => row.Child.OrderId, reward)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));
        AddField(nameof(ReferralRecord.OrderNumber), S["Friend's order number"], DataFieldType.Text, row => row.Child.OrderNumber, reward);
        AddField(nameof(ReferralRecord.RewardKind), S["Reward kind"], DataFieldType.Text, row => row.Child.RewardKind, reward);
        AddField(nameof(ReferralRecord.Reward), S["Reward"], DataFieldType.Text, row => row.Child.Reward, reward);
        AddField("Currency", S["Currency"], DataFieldType.Text, row => row.Child.RewardCurrency, reward);
        AddField(nameof(ReferralRecord.RewardAmount), S["Store credit given"], DataFieldType.Decimal, row => row.Child.RewardAmount, reward).Description = S["The store credit the customer got, in the reward's currency. Zero for a reward in points."].Value;
        AddField(nameof(ReferralRecord.RewardPoints), S["Points given"], DataFieldType.Integer, row => row.Child.RewardPoints, reward);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<ReferralProfileIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ReferralProfileIndex, string>>>(StringComparer.Ordinal)
        {
            [ReferralIdField] = index => index.ItemId,
            [nameof(ReferralProfile.CustomerId)] = index => index.CustomerId,
        };

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<ReferralProfile, ReferralRecord>> GetRows(ReferralProfile document)
    {
        return ReportChildRow<ReferralProfile, ReferralRecord>.For(document, document.Referrals);
    }
}
