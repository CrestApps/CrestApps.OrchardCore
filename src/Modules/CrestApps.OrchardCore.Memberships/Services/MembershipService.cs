using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Memberships.Services;

/// <summary>
/// Memberships bought with orders: a paid order makes or extends one and puts the buyer in its roles; a refunded or
/// canceled order, the end date, or the store ends it and takes them out of the roles no other membership gives them.
/// </summary>
public sealed class MembershipService
{
    private readonly IMembershipStore _memberships;
    private readonly IContentManager _contentManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IUserService _userService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public MembershipService(
        IMembershipStore memberships,
        IContentManager contentManager,
        UserManager<IUser> userManager,
        IUserService userService,
        IClock clock,
        ILogger<MembershipService> logger)
    {
        _memberships = memberships;
        _contentManager = contentManager;
        _userManager = userManager;
        _userService = userService;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Makes or extends a membership for each of a paid order's membership lines. Returns how many it did; a guest's
    /// order makes none, since there's no account to put in a role.
    /// </summary>
    public async Task<int> GrantAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (string.IsNullOrEmpty(order.UserId) || await _userService.GetUserByUniqueIdAsync(order.UserId) is not { } user)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var memberships = await _memberships.ListByUserAsync(order.UserId, cancellationToken);
        var granted = 0;

        foreach (var line in order.Lines ?? [])
        {
            if ((await _contentManager.GetAsync(line.ContentItemId, VersionOptions.Published))?.TryGet<MembershipPart>(out var part) != true ||
                part.RoleNames is not { Length: > 0 })
            {
                continue;
            }

            // The same order twice, such as a payment confirmed again, extends nothing.
            if (memberships.Any(membership => membership.OrderIds.Contains(order.ItemId) && membership.ContentItemId == line.ContentItemId))
            {
                continue;
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

            foreach (var role in await AddRolesAsync(user, current.RoleNames))
            {
                if (!current.AddedRoleNames.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    current.AddedRoleNames.Add(role);
                }
            }

            await _memberships.UpdateAsync(current, cancellationToken);
            granted++;
        }

        return granted;
    }

    /// <summary>
    /// Ends the memberships an order bought, when it's refunded in full or canceled.
    /// </summary>
    public async Task RevokeOrderAsync(Order order, string reason, CancellationToken cancellationToken = default)
    {
        foreach (var membership in await _memberships.ListByOrderAsync(order.UserId, order.ItemId, cancellationToken))
        {
            if (membership.Status == MembershipStatus.Active)
            {
                await EndAsync(membership, MembershipStatus.Revoked, reason, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Ends a membership, and takes its member out of the roles no other active membership of theirs gives them.
    /// </summary>
    public async Task EndAsync(Membership membership, MembershipStatus status, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        var now = _clock.UtcNow;

        membership.Status = status;
        membership.EndedReason = reason;
        membership.ModifiedUtc = now;

        if (status == MembershipStatus.Revoked && (membership.EndsUtc is null || membership.EndsUtc > now))
        {
            membership.EndsUtc = now;
        }

        await _memberships.UpdateAsync(membership, cancellationToken);

        if (await _userService.GetUserByUniqueIdAsync(membership.UserId) is not { } user)
        {
            return;
        }

        var others = (await _memberships.ListByUserAsync(membership.UserId, cancellationToken))
            .Where(other => other.ItemId != membership.ItemId && other.Status == MembershipStatus.Active)
            .SelectMany(other => other.RoleNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removed = false;

        // Only the roles it gave them: one they were in before keeps them in it.
        foreach (var role in membership.AddedRoleNames.Where(role => !others.Contains(role)))
        {
            if (!await _userManager.IsInRoleAsync(user, role))
            {
                continue;
            }

            var result = await _userManager.RemoveFromRoleAsync(user, role);

            if (result.Succeeded)
            {
                removed = true;
            }
            else
            {
                _logger.LogError("Could not take user '{UserId}' out of the '{RoleName}' role: {Errors}", membership.UserId, role, string.Join("; ", result.Errors.Select(error => error.Description)));
            }
        }

        // Roles are read from the sign-in: a new security stamp makes it be checked again, so what the membership
        // opened closes now rather than when the member next signs in.
        if (removed)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }
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

    // Puts the user in the roles they aren't in, and returns those.
    private async Task<IReadOnlyList<string>> AddRolesAsync(IUser user, IEnumerable<string> roles)
    {
        var added = new List<string>();

        foreach (var role in roles)
        {
            if (await _userManager.IsInRoleAsync(user, role))
            {
                continue;
            }

            var result = await _userManager.AddToRoleAsync(user, role);

            if (result.Succeeded)
            {
                added.Add(role);
            }
            else
            {
                _logger.LogError("Could not put user '{UserName}' in the '{RoleName}' role: {Errors}", user.UserName, role, string.Join("; ", result.Errors.Select(error => error.Description)));
            }
        }

        return added;
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
