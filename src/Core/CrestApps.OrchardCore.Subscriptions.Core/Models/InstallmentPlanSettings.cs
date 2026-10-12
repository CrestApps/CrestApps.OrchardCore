using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// The site settings for installment plans.
/// </summary>
public sealed class InstallmentPlanSettings
{
    /// <summary>
    /// Gets or sets how many days after a failed automatic charge each retry is made, in order. Defaults to one,
    /// three and five days. After the last retry fails, the payment is left for the customer to pay.
    /// </summary>
    public int[] RetryDays { get; set; } = [1, 3, 5];

    /// <summary>
    /// Gets or sets the collection method a new plan starts with.
    /// </summary>
    public InstallmentCollectionMethod DefaultCollectionMethod { get; set; } = InstallmentCollectionMethod.AutoCharge;

    /// <summary>
    /// Gets the delay before retry <paramref name="attemptsSoFar"/> (the number of failed charges so far), or
    /// <see langword="null"/> when there are no retries left.
    /// </summary>
    /// <param name="attemptsSoFar">How many charges have failed for the payment.</param>
    public TimeSpan? GetRetryDelay(int attemptsSoFar)
    {
        var retries = RetryDays ?? [];

        if (attemptsSoFar < 1 || attemptsSoFar > retries.Length)
        {
            return null;
        }

        return TimeSpan.FromDays(Math.Max(1, retries[attemptsSoFar - 1]));
    }
}
