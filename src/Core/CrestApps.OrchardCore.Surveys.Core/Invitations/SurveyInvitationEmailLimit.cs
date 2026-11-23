using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// The site's daily limit of invitation and reminder e-mails, exact across servers and parallel requests. Before its
/// first e-mail of a day, a scope takes the tenant's lock for that day of the site (Orchard Core's
/// <see cref="IDistributedLock"/>, which is in-process unless a distributed lock such as Redis is enabled) and keeps it
/// until its changes are saved, so the e-mails another sender counts always include every e-mail sent before. Under the
/// lock the day's e-mails are counted once per batch and the batch's own sends are added as they go out.
/// </summary>
public sealed class SurveyInvitationEmailLimit : IAsyncDisposable
{
    /// <summary>
    /// How long a sender waits for another sender of the same day to save its e-mails; past it, its invitations wait
    /// for a later run.
    /// </summary>
    public static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// When a lock that was never released (a server that stopped) lets go on its own; longer than a send of a full
    /// page of invitations.
    /// </summary>
    public static readonly TimeSpan LockExpiration = TimeSpan.FromMinutes(15);

    private const string LockKeyPrefix = "Surveys:InvitationEmails:";

    private readonly SurveyInvitationStore _invitationStore;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;
    private readonly Dictionary<string, ILocker> _lockers = new(StringComparer.Ordinal);

    private bool _releaseRegistered;
    private string _timeZoneId;
    private int _limit;
    private DateTime? _countedDayStartUtc;
    private DateTime? _busyDayStartUtc;
    private int _emailed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationEmailLimit"/> class.
    /// </summary>
    /// <param name="invitationStore">The invitation store, which counts the day's e-mails.</param>
    /// <param name="distributedLock">The tenant's lock service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="siteService">The site service, for the limit and the site's time zone; without it the default limit applies.</param>
    public SurveyInvitationEmailLimit(
        SurveyInvitationStore invitationStore,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<SurveyInvitationEmailLimit> logger,
        ISiteService siteService = null)
    {
        _invitationStore = invitationStore;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
        _siteService = siteService;
    }

    /// <summary>
    /// Starts a batch of sends: the settings are read again and the day's e-mails counted again on its first e-mail.
    /// The locks the scope already holds are kept.
    /// </summary>
    public void BeginBatch()
    {
        _timeZoneId = null;
        _countedDayStartUtc = null;
        _busyDayStartUtc = null;
    }

    /// <summary>
    /// Gets whether one more e-mail may go out now. Takes the lock of the site's day first when the scope doesn't hold
    /// it yet; when the lock can't be taken in time, no e-mail of the batch goes out for that day, without waiting
    /// again for each one.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the e-mail may be sent; call <see cref="AddSent"/> once it went out.</returns>
    public async Task<bool> CanSendAsync(CancellationToken cancellationToken = default)
    {
        if (_timeZoneId is null)
        {
            await ReadSettingsAsync();
        }

        var utcNow = _clock.UtcNow;
        var dayStartUtc = SurveyInvitationService.DayStartUtc(utcNow, _timeZoneId);

        if (_countedDayStartUtc == dayStartUtc)
        {
            return _emailed < _limit;
        }

        if (_busyDayStartUtc == dayStartUtc)
        {
            return false;
        }

        var key = LockKeyPrefix + CalendarTime.ToLocal(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), _timeZoneId).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (!_lockers.ContainsKey(key))
        {
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key, LockTimeout, LockExpiration);

            if (!locked)
            {
                _busyDayStartUtc = dayStartUtc;
                _logger.LogWarning("The daily invitation e-mail lock '{LockKey}' could not be acquired in time; the invitations wait for a later run.", key);

                return false;
            }

            _lockers[key] = locker;
            RegisterRelease();
        }

        // Counted under the lock: every other sender of the day saved its e-mails before it let go of the lock, and this
        // scope's own earlier e-mails are flushed into the count.
        _emailed = await _invitationStore.CountEmailedSinceAsync(dayStartUtc, cancellationToken);
        _countedDayStartUtc = dayStartUtc;

        return _emailed < _limit;
    }

    /// <summary>
    /// Adds an e-mail that went out to the batch's count.
    /// </summary>
    public void AddSent()
        => _emailed++;

    /// <summary>
    /// Releases the locks the scope holds. Called once the scope's changes are saved.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task ReleaseAsync()
    {
        if (_lockers.Count == 0)
        {
            return;
        }

        var lockers = _lockers.Values.ToArray();

        _lockers.Clear();
        _countedDayStartUtc = null;

        foreach (var locker in lockers)
        {
            await locker.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
        => await ReleaseAsync();

    // In a shell scope the scope's session is saved before its last before-dispose callback, and deferred tasks run
    // after it, so the lock is let go of right after the save and never makes a deferred send wait. Outside a shell
    // scope the lock is let go of when the service scope is disposed.
    private void RegisterRelease()
    {
        if (_releaseRegistered || ShellScope.Current is null)
        {
            return;
        }

        ShellScope.RegisterBeforeDispose(_ => ReleaseAsync(), last: true);
        _releaseRegistered = true;
    }

    private async Task ReadSettingsAsync()
    {
        string timeZoneId = null;
        SurveySettings settings = null;

        if (_siteService is not null)
        {
            settings = await _siteService.GetSettingsAsync<SurveySettings>();
            timeZoneId = (await _siteService.GetSiteSettingsAsync())?.TimeZoneId;
        }

        if (string.IsNullOrEmpty(timeZoneId))
        {
            timeZoneId = _clock.GetSystemTimeZone()?.TimeZoneId ?? string.Empty;
        }

        _timeZoneId = timeZoneId;
        _limit = Math.Max(1, settings?.InvitationEmailsPerDay ?? SurveySettings.DefaultInvitationEmailsPerDay);
    }
}
