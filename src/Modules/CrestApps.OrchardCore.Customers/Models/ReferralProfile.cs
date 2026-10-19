using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// A customer's referral link, and the friends who bought through it.
/// </summary>
public sealed class ReferralProfile : CatalogItem
{
    public long Id { get; set; }

    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the code in the customer's referral link.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the friends whose first order the customer was rewarded for, oldest first.
    /// </summary>
    public List<ReferralRecord> Referrals { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// One friend who bought through a referral link.
/// </summary>
public sealed class ReferralRecord
{
    /// <summary>
    /// Gets or sets the friend's email, in lower case: one reward per friend.
    /// </summary>
    public string FriendEmail { get; set; }

    public string FriendName { get; set; }

    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets what the customer was given, such as "$10 store credit", or <see langword="null"/> when nothing
    /// was given because the store gives no reward.
    /// </summary>
    public string Reward { get; set; }

    /// <summary>
    /// Gets or sets what kind of reward was given, so a canceled order takes back exactly that.
    /// </summary>
    public ReferralRewardKind RewardKind { get; set; }

    public decimal RewardAmount { get; set; }

    public string RewardCurrency { get; set; }

    public int RewardPoints { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// The referral a checkout came from, stored on the checkout until its order is paid.
/// </summary>
public sealed class CheckoutReferral
{
    public string Code { get; set; }
}

/// <summary>
/// What a referred friend gets and what the customer who referred them gets, under Settings > Commerce > Referrals.
/// </summary>
public sealed class ReferralSettings
{
    /// <summary>
    /// Gets or sets the coupon each referred friend gets a single-use code of, or <see langword="null"/> for none.
    /// </summary>
    public string FriendCouponId { get; set; }

    public ReferralRewardKind RewardKind { get; set; }

    /// <summary>
    /// Gets or sets the store credit the customer gets for a friend's first order, in the checkout currency.
    /// </summary>
    public decimal RewardAmount { get; set; } = 10m;

    /// <summary>
    /// Gets or sets the loyalty points the customer gets for a friend's first order.
    /// </summary>
    public int RewardPoints { get; set; } = 500;

    /// <summary>
    /// Gets or sets the least a friend's first order must spend on goods for the customer to be rewarded.
    /// </summary>
    public decimal MinimumOrderAmount { get; set; }
}

public enum ReferralRewardKind
{
    None,
    StoreCredit,
    Points,
}
