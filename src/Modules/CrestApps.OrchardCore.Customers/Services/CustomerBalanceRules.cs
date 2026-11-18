using CrestApps.OrchardCore.Customers.Core.Models;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// The checks a staff change to a customer's store credit or loyalty points passes before it is made, shared by the
/// customer page's Store credit and Rewards cards and the AI tools. Each caller words the problem itself.
/// </summary>
internal static class CustomerBalanceRules
{
    /// <summary>
    /// Checks a change to a customer's store credit: only a customer with an account holds credit, the amount is not
    /// zero, the currency is a three-letter code and the reason is given, since it is kept with the activity.
    /// </summary>
    /// <param name="customer">The customer.</param>
    /// <param name="amount">The amount to give, or a negative amount to take away.</param>
    /// <param name="currency">The currency code, already trimmed and in upper case.</param>
    /// <param name="reason">Why the credit changes.</param>
    /// <returns>The problem, or <see langword="null"/> when the change can be made.</returns>
    public static CustomerBalanceProblem? CheckStoreCredit(Customer customer, decimal? amount, string currency, string reason)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (string.IsNullOrEmpty(customer.UserId))
        {
            return CustomerBalanceProblem.NoAccount;
        }

        if (amount is null or 0m)
        {
            return CustomerBalanceProblem.NoAmount;
        }

        if (currency?.Length != 3)
        {
            return CustomerBalanceProblem.InvalidCurrency;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return CustomerBalanceProblem.NoReason;
        }

        return null;
    }

    /// <summary>
    /// Checks a change to a customer's loyalty points: only a customer with an account holds points, the points are not
    /// zero and the reason is given, since it is kept with the activity.
    /// </summary>
    /// <param name="customer">The customer.</param>
    /// <param name="points">The points to give, or a negative number to take away.</param>
    /// <param name="reason">Why the points change.</param>
    /// <returns>The problem, or <see langword="null"/> when the change can be made.</returns>
    public static CustomerBalanceProblem? CheckPoints(Customer customer, int? points, string reason)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (string.IsNullOrEmpty(customer.UserId))
        {
            return CustomerBalanceProblem.NoAccount;
        }

        if (points is null or 0)
        {
            return CustomerBalanceProblem.NoAmount;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return CustomerBalanceProblem.NoReason;
        }

        return null;
    }
}

/// <summary>
/// What can stop a staff change to a customer's store credit or loyalty points.
/// </summary>
internal enum CustomerBalanceProblem
{
    NoAccount,
    NoAmount,
    InvalidCurrency,
    NoReason,
}
