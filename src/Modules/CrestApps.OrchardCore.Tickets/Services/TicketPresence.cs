using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Who has a ticket open right now, and who is writing on it, so agents don't answer the same ticket twice. Each open
/// page checks in every few seconds; a viewer who stopped checking in for <see cref="Timeout"/> is gone. It is kept in the
/// distributed cache, so every node sees the same viewers.
/// </summary>
public sealed class TicketPresence
{
    /// <summary>
    /// How long a viewer counts after their last check-in.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    private readonly IDistributedCache _cache;
    private readonly IClock _clock;

    public TicketPresence(IDistributedCache cache, IClock clock)
    {
        _cache = cache;
        _clock = clock;
    }

    /// <summary>
    /// Records that a user has a ticket open (and whether they are writing on it), and returns the other people who
    /// have it open.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="userId">The user.</param>
    /// <param name="name">Their name, as others see it.</param>
    /// <param name="writing">Whether they are writing a reply or a note.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The others who have the ticket open.</returns>
    public async Task<IReadOnlyList<TicketViewer>> CheckInAsync(string ticketId, string userId, string name, bool writing, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ticketId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var key = Key(ticketId);
        var now = _clock.UtcNow;
        var viewers = await ReadAsync(key, cancellationToken);

        viewers.RemoveAll(viewer => now - viewer.SeenUtc > Timeout || viewer.UserId == userId);
        viewers.Add(new TicketViewer(userId, name, writing, now));

        await _cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(viewers), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Timeout * 2 }, cancellationToken);

        return [.. viewers.Where(viewer => viewer.UserId != userId)];
    }

    /// <summary>
    /// Records that a user closed a ticket.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task LeaveAsync(string ticketId, string userId, CancellationToken cancellationToken = default)
    {
        var key = Key(ticketId);
        var viewers = await ReadAsync(key, cancellationToken);

        if (viewers.RemoveAll(viewer => viewer.UserId == userId) > 0)
        {
            await _cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(viewers), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Timeout * 2 }, cancellationToken);
        }
    }

    private async Task<List<TicketViewer>> ReadAsync(string key, CancellationToken cancellationToken)
    {
        var bytes = await _cache.GetAsync(key, cancellationToken);

        if (bytes is not { Length: > 0 })
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<TicketViewer>>(bytes) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Key(string ticketId)
        => "CrestApps.Tickets.Presence:" + ticketId;
}

/// <summary>
/// Someone who has a ticket open.
/// </summary>
/// <param name="UserId">The user.</param>
/// <param name="Name">Their name.</param>
/// <param name="Writing">Whether they are writing on it.</param>
/// <param name="SeenUtc">When they last checked in.</param>
public sealed record TicketViewer(string UserId, string Name, bool Writing, DateTime SeenUtc);
