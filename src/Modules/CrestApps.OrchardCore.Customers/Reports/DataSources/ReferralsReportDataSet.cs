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
/// One row per customer's referral link: how many friends bought through it and the points it earned the customer.
/// The code in the link is never exposed, since it gives a friend a discount. Links are read newest first.
/// </summary>
public sealed class ReferralsReportDataSet : IndexedReportDataSet<ReferralProfile, ReferralProfileIndex>, ICustomerReportDataSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReferralsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the referral links are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReferralsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<ReferralsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.CustomerSource.Referrals,
                stringLocalizer["Referrals"],
                stringLocalizer["One row per customer's referral link: the customer, how many friends bought through it, the points it earned, and when."]),
            session,
            authorizationService,
            CustomersConstants.CollectionName,
            CustomerPermissions.ManageCustomers)
    {
        var S = stringLocalizer;
        var referral = S["Referral link"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Referral ID"], DataFieldType.Text, record => record.ItemId, referral).Identifier();
        AddField(nameof(ReferralProfile.CustomerId), S["Customer ID"], DataFieldType.Text, record => record.CustomerId, referral)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.CustomerSource.Name, CommerceReportDataSets.CustomerSource.Customers));
        AddField("ReferralCount", S["Friends rewarded for"], DataFieldType.Integer, record => Entries(record).Count(), referral).Description = S["The friends whose first order the customer was rewarded for."].Value;
        AddField("RewardPoints", S["Points rewarded"], DataFieldType.Integer, record => Entries(record).Sum(entry => (long)entry.RewardPoints), referral);
        AddField("LastReferralUtc", S["Last friend's order"], DataFieldType.DateTime, record => Entries(record).Select(entry => (DateTime?)entry.CreatedUtc).Max(), referral);
        AddField(nameof(ReferralProfile.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, referral);
        AddField(nameof(ReferralProfile.UpdatedUtc), S["Last changed"], DataFieldType.DateTime, record => record.UpdatedUtc, referral);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<ReferralProfileIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ReferralProfileIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(ReferralProfile.CustomerId)] = index => index.CustomerId,
        };

    private static IEnumerable<ReferralRecord> Entries(ReferralProfile profile)
    {
        return (profile.Referrals ?? []).Where(entry => entry is not null);
    }
}
