using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Issues one single-use code of a template coupon to one person: the code works like the template, may be bound to
/// the person's e-mail address and may end sooner than the template. Issuing is idempotent per
/// <see cref="CouponIssueRequest.IdempotencyKey"/>: a repeated request returns the code issued the first time.
/// </summary>
public interface ICouponIssuer
{
    /// <summary>
    /// Issues a code, or returns the one already issued for the request's idempotency key.
    /// </summary>
    /// <remarks>
    /// Issuing runs under a lock on the idempotency key and commits the tenant session before the lock is let go, so a
    /// second request with the same key, even one running at the same time, finds the code instead of issuing another.
    /// The commit also saves the work the caller has pending in the session. A repeated key returns the code issued the
    /// first time, whatever the template coupon's state and the request's other values are now. An expected refusal
    /// (a missing, disabled, automatic or ended template, or a request without a key) comes back as a failed result,
    /// never as an exception.
    /// </remarks>
    /// <param name="request">The template coupon, the recipient and the key.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The issued code, or why none could be issued.</returns>
    Task<CouponIssueResult> IssueAsync(CouponIssueRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A request to issue a code of a template coupon.
/// </summary>
public sealed class CouponIssueRequest
{
    /// <summary>
    /// Gets the id of the template coupon. It must be a code coupon (not automatic) that can be redeemed now.
    /// </summary>
    public string CouponId { get; init; }

    /// <summary>
    /// Gets the text the code starts with, such as <c>WELCOME-</c>: up to
    /// <see cref="CheckoutConstants.MaxCouponCodePrefixLength"/> letters, digits or dashes, kept in upper case.
    /// </summary>
    public string Prefix { get; init; }

    /// <summary>
    /// Gets the e-mail address the code is bound to, or <see langword="null"/> for a code anyone holding it may use.
    /// </summary>
    public string CustomerEmail { get; init; }

    /// <summary>
    /// Gets how long the code works after it is issued, or <see langword="null"/> to end with the template. The code's
    /// <see cref="CouponCode.ExpiresUtc"/> is the earlier of the issue time plus this and the template's end date as it
    /// is when the code is issued, and stays empty only when there is neither. The code never works past the template's
    /// end date.
    /// </summary>
    public TimeSpan? ValidFor { get; init; }

    /// <summary>
    /// Gets the stable key that makes issuing idempotent, built from business ids (for example the mailing list and a
    /// keyed hash of the subscriber's address), never from an id made on each attempt. It is required, and at most
    /// <see cref="CheckoutConstants.MaxCouponIdempotencyKeyLength"/> characters.
    /// </summary>
    public string IdempotencyKey { get; init; }

    /// <summary>
    /// Gets the record the code was issued for, such as a mailing list member, shown with the code in the admin.
    /// </summary>
    public RecordRef Source { get; init; }
}

/// <summary>
/// The outcome of <see cref="ICouponIssuer.IssueAsync"/>.
/// </summary>
public sealed class CouponIssueResult
{
    /// <summary>
    /// Gets whether a code is available: issued now or earlier for the same key.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets whether the code was issued earlier for the same idempotency key.
    /// </summary>
    public bool AlreadyIssued { get; init; }

    /// <summary>
    /// Gets the issued code.
    /// </summary>
    public CouponCode Code { get; init; }

    /// <summary>
    /// Gets the template coupon the code belongs to.
    /// </summary>
    public Coupon Coupon { get; init; }

    /// <summary>
    /// Gets why no code was issued (the template is missing, disabled, automatic or ended).
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Returns a successful result.
    /// </summary>
    /// <param name="coupon">The template coupon.</param>
    /// <param name="code">The issued code.</param>
    /// <param name="alreadyIssued">Whether the code was issued earlier for the same key.</param>
    public static CouponIssueResult Success(Coupon coupon, CouponCode code, bool alreadyIssued)
        => new() { Succeeded = true, Coupon = coupon, Code = code, AlreadyIssued = alreadyIssued };

    /// <summary>
    /// Returns a failed result.
    /// </summary>
    /// <param name="error">Why no code was issued.</param>
    public static CouponIssueResult Failure(string error)
        => new() { Error = error };
}
