using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Emails;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICouponIssuer"/>: one single-use <see cref="CouponCode"/> of a template coupon per idempotency
/// key, kept in the coupon collection with the codes generated in bulk.
/// </summary>
/// <remarks>
/// The key is locked from the lookup to the commit, so two requests with the same key, even at the same time, never
/// both issue a code: the second one waits, then finds the first one's code.
/// </remarks>
public sealed class DefaultCouponIssuer : ICouponIssuer
{
    private const string Collection = CheckoutConstants.CouponCollectionName;
    private const string LockPrefix = "COUPON_ISSUE:";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly ICouponStore _couponStore;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCouponIssuer"/> class.
    /// </summary>
    /// <param name="couponStore">The coupon catalog the template coupon is read from.</param>
    /// <param name="session">The tenant session the code is saved and committed in.</param>
    /// <param name="distributedLock">The lock taken on the idempotency key.</param>
    /// <param name="clock">The clock the issue time and expiry are read from.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The localizer of the refusal reasons.</param>
    public DefaultCouponIssuer(
        ICouponStore couponStore,
        ISession session,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<DefaultCouponIssuer> logger,
        IStringLocalizer<DefaultCouponIssuer> stringLocalizer)
    {
        _couponStore = couponStore;
        _session = session;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<CouponIssueResult> IssueAsync(CouponIssueRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Validate(request) is { } error)
        {
            return CouponIssueResult.Failure(error);
        }

        var key = request.IdempotencyKey.Trim();
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + key, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            _logger.LogWarning("Could not lock the coupon idempotency key to issue a code of coupon '{CouponId}'.", request.CouponId);

            return CouponIssueResult.Failure(S["Another request is issuing this code. Try again in a moment."]);
        }

        await using (locker)
        {
            // The same key always gets the code it got the first time, so a person can't get a second one by asking
            // again, even after the template changed or when the request carries other values.
            var existing = await _session
                .Query<CouponCode, CouponCodeIndex>(index => index.IdempotencyKey == key, collection: Collection)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                return CouponIssueResult.Success(await _couponStore.FindByIdAsync(existing.CouponId, cancellationToken), existing, alreadyIssued: true);
            }

            var template = await _couponStore.FindByIdAsync(request.CouponId.Trim(), cancellationToken);
            var now = _clock.UtcNow;

            if (Refuse(template, now) is { } refusal)
            {
                return CouponIssueResult.Failure(refusal);
            }

            var codes = await CouponCodeGenerator.DrawUniqueAsync(_session, request.Prefix, 1, cancellationToken);

            if (codes.Count == 0)
            {
                return CouponIssueResult.Failure(S["No unique code could be made. Try again."]);
            }

            var code = new CouponCode
            {
                ItemId = IdGenerator.GenerateId(),
                CouponId = template.ItemId,
                Code = codes[0],
                MaxRedemptions = 1,
                CreatedUtc = now,
                CustomerEmail = EmailAddresses.Normalize(request.CustomerEmail),
                ExpiresUtc = GetExpiry(now, request.ValidFor, template.EndsUtc),
                Source = request.Source.IsEmpty ? null : request.Source.ToString(),
                IdempotencyKey = key,
            };

            await _session.SaveAsync(code, collection: Collection, cancellationToken: cancellationToken);

            // Committed while the key is locked, so the next request with the key finds this code.
            await _session.SaveChangesAsync(cancellationToken);

            return CouponIssueResult.Success(template, code, alreadyIssued: false);
        }
    }

    // The earlier of the code's own time and the template's end, whichever there is. A time too far away to add counts
    // as none.
    private static DateTime? GetExpiry(DateTime now, TimeSpan? validFor, DateTime? templateEndsUtc)
    {
        DateTime? own = validFor is { } duration && duration < DateTime.MaxValue - now
            ? now + duration
            : null;

        if (own is null)
        {
            return templateEndsUtc;
        }

        return templateEndsUtc is { } ends && ends < own.Value
            ? ends
            : own;
    }

    private string Validate(CouponIssueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CouponId))
        {
            return S["Choose the coupon the code is issued from."];
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return S["A code is issued under a key that identifies who it is for."];
        }

        if (request.IdempotencyKey.Trim().Length > CheckoutConstants.MaxCouponIdempotencyKeyLength)
        {
            return S["The key a code is issued under is at most {0} characters.", CheckoutConstants.MaxCouponIdempotencyKeyLength];
        }

        if (!CouponCodeGenerator.IsValidPrefix(request.Prefix))
        {
            return S["A prefix is up to {0} letters, digits or dashes.", CheckoutConstants.MaxCouponCodePrefixLength];
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail) && !EmailAddresses.IsValid(request.CustomerEmail))
        {
            return S["The e-mail address the code is for is not valid."];
        }

        if (request.ValidFor is { } validFor && validFor <= TimeSpan.Zero)
        {
            return S["A code works for a time greater than zero."];
        }

        return null;
    }

    private string Refuse(Coupon template, DateTime now)
    {
        if (template is null)
        {
            return S["The coupon to issue the code from was not found."];
        }

        if (template.IsAutomatic)
        {
            return S["An automatic discount has no codes to issue."];
        }

        if (!template.IsEnabled)
        {
            return S["The coupon to issue the code from is turned off."];
        }

        if (!template.IsRedeemable(now))
        {
            return S["The coupon to issue the code from can't be used now: it hasn't started, has ended or is used up."];
        }

        return null;
    }
}
