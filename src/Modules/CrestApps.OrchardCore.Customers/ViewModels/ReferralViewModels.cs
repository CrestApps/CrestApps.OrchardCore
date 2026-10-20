using CrestApps.OrchardCore.Customers.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Customers.ViewModels;

public class ReferralSettingsViewModel
{
    public string FriendCouponId { get; set; }

    public ReferralRewardKind RewardKind { get; set; }

    public decimal RewardAmount { get; set; }

    public int RewardPoints { get; set; }

    public decimal MinimumOrderAmount { get; set; }

    [BindNever]
    public IList<SelectListItem> Coupons { get; set; } = [];
}

public class AccountReferralsViewModel
{
    public string Link { get; set; }

    /// <summary>
    /// Gets or sets what a friend gets, such as "10% off your first order", or <see langword="null"/>.
    /// </summary>
    public string FriendGets { get; set; }

    /// <summary>
    /// Gets or sets what the customer gets for a friend's first order, or <see langword="null"/>.
    /// </summary>
    public string YouGet { get; set; }

    public IReadOnlyList<ReferralRecord> Referrals { get; set; } = [];
}
