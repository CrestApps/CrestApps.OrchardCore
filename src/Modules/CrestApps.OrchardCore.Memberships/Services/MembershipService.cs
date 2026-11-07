using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Users;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Memberships.Services;

/// <summary>
/// Memberships bought with orders: a paid order makes or extends one and puts the buyer in its roles; a refunded or
/// canceled order, the end date, or the store ends it and takes them out of the roles nothing else, such as another
/// membership or a subscription, still gives them. Roles go through <see cref="IRoleGrantService"/>, keyed by the
/// membership.
/// </summary>
public sealed class MembershipService
{
    private readonly IMembershipStore _memberships;
    private readonly IUserService _userService;
    private readonly IRoleGrantService _roleGrantService;
    private readonly IClock _clock;

    public MembershipService(
        IMembershipStore memberships,
        IUserService userService,
        IRoleGrantService roleGrantService,
        IClock clock)
    {
        _memberships = memberships;
        _userService = userService;
        _roleGrantService = roleGrantService;
        _clock = clock;
    }

    /// <summary>
    /// Returns the IDs of the order's lines that already made or extended a membership.
    /// </summary>
    /// <param name="order">The paid order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<ISet<string>> GetGrantedLineIdsAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (string.IsNullOrEmpty(order.UserId))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var granted = (await _memberships.ListByUserAsync(order.UserId, cancellationToken))
            .Where(membership => membership.OrderIds.Contains(order.ItemId))
            .Select(membership => membership.ContentItemId)
            .ToHashSet(StringComparer.Ordinal);

        return (order.Lines ?? [])
            .Where(line => line.LineId is not null && line.ContentItemId is not null && granted.Contains(line.ContentItemId))
            .Select(line => line.LineId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Makes or extends a membership for one line of a paid order, from <paramref name="part"/> of the product version
    /// it bought. A guest's order makes none, since there's no account to put in a role.
    /// </summary>
    /// <param name="order">The paid order.</param>
    /// <param name="line">The membership line.</param>
    /// <param name="part">The membership part of the purchased version of the line's product.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a membership was made or extended.</returns>
    public async Task<bool> GrantLineAsync(Order order, OrderLine line, MembershipPart part, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(line);

        if (part?.RoleNames is not { Length: > 0 } ||
            string.IsNullOrEmpty(order.UserId) ||
            await _userService.GetUserByUniqueIdAsync(order.UserId) is not { } user)
        {
            return false;
        }

        var now = _clock.UtcNow;
        var memberships = await _memberships.ListByUserAsync(order.UserId, cancellationToken);

        // The same order twice, such as a payment confirmed again, extends nothing.
        if (memberships.Any(membership => membership.OrderIds.Contains(order.ItemId) && membership.ContentItemId == line.ContentItemId))
        {
            return false;
        }

        var current = memberships.FirstOrDefault(membership => membership.ContentItemId == line.ContentItemId && membership.Status == MembershipStatus.Active);

        if (current is null)
        {
            current = new Membership
            {
                ItemId = global::OrchardCore.IdGenerator.GenerateId(),
                UserId = order.UserId,
                UserName = user.UserName,
                ContentItemId = line.ContentItemId,
                Title = line.Title,
                StartsUtc = now,
                EndsUtc = MembershipPeriods.Extend(now, null, part.DurationDays, line.Quantity),
                Status = MembershipStatus.Active,
                CreatedUtc = now,
            };

            current.OrderIds.Add(order.ItemId);
            current.RoleNames = [.. part.RoleNames];
            current.ModifiedUtc = now;

            await _memberships.CreateAsync(current, cancellationToken);
        }
        else
        {
            // One that doesn't end stays so.
            current.EndsUtc = current.EndsUtc is null ? null : MembershipPeriods.Extend(now, current.EndsUtc, part.DurationDays, line.Quantity);
            current.RoleNames = [.. current.RoleNames.Union(part.RoleNames, StringComparer.OrdinalIgnoreCase)];
            current.OrderIds.Add(order.ItemId);
            current.ModifiedUtc = now;

            await _memberships.UpdateAsync(current, cancellationToken);
        }

        foreach (var role in current.RoleNames)
        {
            await _roleGrantService.GrantAsync(order.UserId, role, MembershipRoleGrantSource.SourceName, current.ItemId);
        }

        return true;
    }

    /// <summary>
    /// Ends the membership one line of an order bought or extended, when the order is refunded in full or canceled.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="line">The line.</param>
    /// <param name="reason">Why it ends.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task RevokeLineAsync(Order order, OrderLine line, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(line);

        foreach (var membership in await _memberships.ListByOrderAsync(order.UserId, order.ItemId, cancellationToken))
        {
            if (membership.Status == MembershipStatus.Active && membership.ContentItemId == line.ContentItemId)
            {
                await EndAsync(membership, MembershipStatus.Revoked, reason, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Ends a membership, and takes its member out of the roles it put them in that nothing else still gives them.
    /// </summary>
    public async Task EndAsync(Membership membership, MembershipStatus status, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        // Revoked while it's still active, so a membership from before grants were recorded is reported with the roles
        // it put its member in, and only those are taken away.
        foreach (var role in membership.RoleNames ?? [])
        {
            await _roleGrantService.RevokeAsync(membership.UserId, role, MembershipRoleGrantSource.SourceName, membership.ItemId);
        }

        var now = _clock.UtcNow;

        membership.Status = status;
        membership.EndedReason = reason;
        membership.ModifiedUtc = now;

        if (status == MembershipStatus.Revoked && (membership.EndsUtc is null || membership.EndsUtc > now))
        {
            membership.EndsUtc = now;
        }

        await _memberships.UpdateAsync(membership, cancellationToken);
    }

    /// <summary>
    /// Ends the memberships whose end date passed.
    /// </summary>
    public async Task<int> ExpireAsync(int take, CancellationToken cancellationToken = default)
    {
        var ended = await _memberships.ListEndedAsync(_clock.UtcNow, take, cancellationToken);

        foreach (var membership in ended)
        {
            await EndAsync(membership, MembershipStatus.Expired, reason: null, cancellationToken);
        }

        return ended.Count;
    }
}

/// <summary>
/// How long a membership lasts.
/// </summary>
public static class MembershipPeriods
{
    /// <summary>
    /// Returns when a membership ends once <paramref name="units"/> more of <paramref name="days"/> are added: from its
    /// current end when it hasn't passed, else from now. Zero days never ends.
    /// </summary>
    public static DateTime? Extend(DateTime utcNow, DateTime? currentEndUtc, int days, int units)
    {
        if (days <= 0)
        {
            return null;
        }

        var from = currentEndUtc is { } end && end > utcNow ? end : utcNow;

        return from.AddDays((double)days * Math.Max(1, units));
    }
}
