using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Persists the coupon codes a site offers.
/// </summary>
public interface ICouponStore : ICatalog<Coupon>
{
    /// <summary>
    /// Returns the coupon with the given code, matched without case sensitivity.
    /// </summary>
    /// <param name="code">The coupon code.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Coupon> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// The codes a customer entered on a checkout, stored on the session so they survive the customer navigating back
/// and forth through the flow.
/// </summary>
public sealed class CheckoutCoupon
{
    /// <summary>
    /// The most codes one checkout carries.
    /// </summary>
    public const int MaxCodes = 5;

    /// <summary>
    /// Gets or sets the code entered by a checkout made before it could carry several. New checkouts keep their codes
    /// in <see cref="Codes"/>.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the codes the customer entered, in the order they entered them.
    /// </summary>
    public List<string> Codes { get; set; } = [];

    /// <summary>
    /// Gets every code the checkout carries.
    /// </summary>
    public IReadOnlyList<string> GetCodes()
        => [.. (string.IsNullOrWhiteSpace(Code) ? Codes ?? [] : (Codes ?? []).Prepend(Code))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Adds a code, unless the checkout already carries it. Returns whether it's there afterwards.
    /// </summary>
    public bool Add(string code)
    {
        var codes = GetCodes().ToList();

        if (codes.Contains(code, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (codes.Count >= MaxCodes)
        {
            return false;
        }

        codes.Add(code);
        Code = null;
        Codes = codes;

        return true;
    }

    /// <summary>
    /// Takes a code off. Returns whether it was there.
    /// </summary>
    public bool Remove(string code)
    {
        var codes = GetCodes().ToList();
        var removed = codes.RemoveAll(entry => string.Equals(entry, code, StringComparison.OrdinalIgnoreCase)) > 0;

        Code = null;
        Codes = codes;

        return removed;
    }
}
