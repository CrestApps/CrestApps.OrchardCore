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

    /// <summary>
    /// Returns the enabled discounts that apply by themselves, without a code.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<Coupon>> ListAutomaticAsync(CancellationToken cancellationToken = default);
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
    /// Gets or sets the automatic discounts taken off this checkout because they ran out, or the buyer had used them as
    /// often as they allow, when payment began. They stay off, so the buyer pays the total they were shown.
    /// </summary>
    public List<string> Declined { get; set; } = [];

    /// <summary>
    /// Gets whether the checkout carries nothing: no code, and no automatic discount taken off.
    /// </summary>
    public bool IsEmpty()
        => GetCodes().Count == 0 && Declined is not { Count: > 0 };

    /// <summary>
    /// Takes an automatic discount off the checkout for good.
    /// </summary>
    public void Decline(string code)
    {
        Declined ??= [];

        if (!string.IsNullOrWhiteSpace(code) && !Declined.Contains(code, StringComparer.OrdinalIgnoreCase))
        {
            Declined.Add(code);
        }
    }

    /// <summary>
    /// Gets whether an automatic discount was taken off the checkout.
    /// </summary>
    public bool IsDeclined(string code)
        => Declined?.Contains(code, StringComparer.OrdinalIgnoreCase) == true;

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
