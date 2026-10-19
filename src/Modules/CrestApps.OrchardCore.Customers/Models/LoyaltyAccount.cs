using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// The loyalty points a customer has earned and not spent.
/// </summary>
public sealed class LoyaltyAccount : CatalogItem
{
    public long Id { get; set; }

    public string CustomerId { get; set; }

    public int Points { get; set; }

    /// <summary>
    /// Gets or sets every change to the points, oldest first.
    /// </summary>
    public List<LoyaltyActivity> Activity { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Changes the points by <paramref name="points"/>, never below zero, and records why. Returns how many points
    /// actually changed.
    /// </summary>
    public int ChangePoints(int points, LoyaltyActivityKind kind, string reason, DateTime utcNow, string orderId = null, string sessionId = null, string actor = null)
    {
        var before = Points;

        Points = Math.Max(0, Points + points);
        Activity ??= [];
        Activity.Add(new LoyaltyActivity
        {
            CreatedUtc = utcNow,
            Points = Points - before,
            Balance = Points,
            Kind = kind,
            Reason = reason,
            OrderId = orderId,
            SessionId = sessionId,
            Actor = actor,
        });

        return Points - before;
    }

    /// <summary>
    /// Returns the points an order earned that haven't been taken back.
    /// </summary>
    public int GetEarnedFor(string orderId)
        => Math.Max(0, (Activity ?? [])
            .Where(activity => activity.OrderId == orderId && activity.Kind is LoyaltyActivityKind.Earned or LoyaltyActivityKind.Reversed)
            .Sum(activity => activity.Points));
}

/// <summary>
/// One change to a customer's points.
/// </summary>
public sealed class LoyaltyActivity
{
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets how many points changed: positive when earned or given, negative when spent or taken back.
    /// </summary>
    public int Points { get; set; }

    public int Balance { get; set; }

    public LoyaltyActivityKind Kind { get; set; }

    public string Reason { get; set; }

    public string OrderId { get; set; }

    public string SessionId { get; set; }

    public string Actor { get; set; }
}

public enum LoyaltyActivityKind
{
    Earned,
    Reversed,
    Spent,
    Returned,
    Adjusted,
}

/// <summary>
/// How customers earn and spend loyalty points, under Settings > Commerce > Rewards.
/// </summary>
public sealed class LoyaltySettings
{
    /// <summary>
    /// Gets or sets what the program is called where buyers see it, such as "Rewards".
    /// </summary>
    public string ProgramName { get; set; }

    /// <summary>
    /// Gets or sets the points earned for each unit of currency spent on goods, after discounts and before shipping
    /// and tax.
    /// </summary>
    public decimal PointsPerUnit { get; set; } = 1m;

    /// <summary>
    /// Gets or sets what one point is worth when it's spent, in the checkout currency.
    /// </summary>
    public decimal PointValue { get; set; } = 0.01m;

    /// <summary>
    /// Gets or sets the fewest points a buyer must have to spend them.
    /// </summary>
    public int MinimumPointsToRedeem { get; set; } = 100;
}
