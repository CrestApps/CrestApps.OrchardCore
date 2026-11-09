using System.Globalization;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Outreach;

internal sealed class OutreachTestClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 4, 15, 0, 0, DateTimeKind.Utc);

    public ITimeZone[] GetTimeZones() => [];

    public ITimeZone GetTimeZone(string timeZoneId) => null;

    public ITimeZone GetSystemTimeZone() => null;

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;
}

/// <summary>
/// Pages a fixed list of recipients with an offset cursor, and counts the pages it was asked for.
/// </summary>
internal sealed class ListAudienceSource : IOutreachAudienceSource
{
    private readonly OutreachRecipient[] _recipients;

    public ListAudienceSource(params OutreachRecipient[] recipients)
    {
        _recipients = recipients;
    }

    public string Name => "List";

    public List<string> Cursors { get; } = [];

    public static OutreachRecipient Recipient(string address, string skipReason = null)
        => new() { Key = address, Address = address, SkipReason = skipReason };

    public Task<OutreachAudiencePage> GetPageAsync(OutreachRun run, string cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        Cursors.Add(cursor);

        var offset = string.IsNullOrEmpty(cursor) ? 0 : int.Parse(cursor, CultureInfo.InvariantCulture);
        var page = _recipients.Skip(offset).Take(pageSize).ToArray();
        var next = offset + page.Length;

        return Task.FromResult(new OutreachAudiencePage
        {
            Recipients = page,
            NextCursor = next < _recipients.Length ? next.ToString(CultureInfo.InvariantCulture) : null,
        });
    }
}

/// <summary>
/// Records what it sends; can throw or refuse for chosen addresses.
/// </summary>
internal sealed class RecordingSender : IOutreachChannelSender
{
    public string Name => "Recording";

    public bool QuietHours { get; set; }

    public List<(string Address, string Body)> Sent { get; } = [];

    /// <summary>
    /// Gets the addresses whose send throws, with how many more times it throws (-1 for always).
    /// </summary>
    public Dictionary<string, int> Throws { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Refuses { get; } = new(StringComparer.Ordinal);

    public bool ObservesQuietHours(OutreachRun run) => QuietHours;

    public Task<OutreachSendResult> SendAsync(OutreachRun run, OutreachRecipient recipient, OutreachContent content, CancellationToken cancellationToken = default)
    {
        if (Throws.TryGetValue(recipient.Address, out var remaining) && remaining != 0)
        {
            Throws[recipient.Address] = remaining - 1;

            throw new InvalidOperationException("The provider is down.");
        }

        if (Refuses.Contains(recipient.Address))
        {
            return Task.FromResult(OutreachSendResult.Failure("Blocked by the carrier."));
        }

        Sent.Add((recipient.Address, content.Body));

        return Task.FromResult(OutreachSendResult.Success());
    }
}

internal sealed class DenyListPolicy : IOutreachRecipientPolicy
{
    private readonly HashSet<string> _optedOut;

    public DenyListPolicy(params string[] optedOut)
    {
        _optedOut = new HashSet<string>(optedOut, StringComparer.OrdinalIgnoreCase);
    }

    public Task<string> GetSkipReasonAsync(OutreachRun run, OutreachRecipient recipient, CancellationToken cancellationToken = default)
        => Task.FromResult(_optedOut.Contains(recipient.Address) ? OutreachConstants.SkipReasons.OptedOut : null);
}

internal sealed class RecordingObserver : IOutreachRunObserver
{
    public List<(OutreachRunStatus Status, int Sent, int Skipped, int Failed)> Updates { get; } = [];

    public Task RunUpdatedAsync(OutreachRun run, CancellationToken cancellationToken = default)
    {
        Updates.Add((run.Status, run.Sent, run.Skipped, run.Failed));

        return Task.CompletedTask;
    }
}

/// <summary>
/// One schedule that is open from <see cref="OpensUtc"/> on.
/// </summary>
internal sealed class OpensLaterHoursService : IOpenHoursService
{
    public DateTime OpensUtc { get; set; }

    public Task<bool?> IsOpenAsync(string scheduleId, DateTime utcInstant, string timeZoneId = null, CancellationToken cancellationToken = default)
        => Task.FromResult<bool?>(utcInstant >= OpensUtc);

    public Task<IReadOnlyList<TimeWindow>> GetOpenWindowsAsync(string scheduleId, DateTime fromUtc, DateTime toUtc, string timeZoneId = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TimeWindow>>(OpensUtc >= toUtc ? [] : [new TimeWindow(OpensUtc > fromUtc ? OpensUtc : fromUtc, toUtc)]);

    public Task<IReadOnlyList<HoursScheduleOption>> ListSchedulesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<HoursScheduleOption>>([]);
}
