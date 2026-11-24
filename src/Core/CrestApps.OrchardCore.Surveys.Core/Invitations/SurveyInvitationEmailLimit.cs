using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// The site's daily limit of invitation and reminder e-mails, exact across servers and parallel requests. It is scoped,
/// and a scope is one batch of sends, however many invitations, sends and reminders it runs: the settings are read once,
/// and before its first e-mail of a day the scope takes the tenant's lock for that day of the site (Orchard Core's
/// <see cref="IDistributedLock"/>, which is in-process unless a distributed lock such as Redis is enabled) and keeps it
/// until its changes are saved, so the e-mails another sender counts always include every e-mail sent before. Under the
/// lock the day's e-mails are counted once and the scope's own sends are added as they go out.
/// <para>
/// A scope asks for the lock at most once: when it can't be had within <see cref="LockTimeout"/>, every later e-mail of
/// the scope is held without waiting again. A scope sends only during the <see cref="SendingWindow"/> after it took the
/// lock; its later e-mails are held. The lock lets go on its own after <see cref="LockExpiration"/>, which leaves the
/// scope far more than the rest of its work and its save once the window closes, so the lock doesn't expire while sent
/// e-mails are still unsaved.
/// </para>
/// <para>
/// <see cref="SurveyInvitationService"/> takes the lock before it reads the invitations it is about to e-mail, so it reads
/// them after every other sender of the day saved: two senders of one list never e-mail the same people.
/// </para>
/// </summary>
public sealed class SurveyInvitationEmailLimit : IAsyncDisposable
{
    /// <summary>
    /// How long a scope waits, once, for another sender of the same day to save its e-mails; past it, the scope's
    /// e-mails wait for a later run.
    /// </summary>
    public static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a scope may send e-mails after it took the day's lock; the e-mails it would send later are held for the
    /// hourly reminder task. Well under <see cref="LockExpiration"/>.
    /// </summary>
    public static readonly TimeSpan SendingWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// When a lock that was never released (a server that stopped) lets go on its own. It leaves a scope 25 minutes
    /// after its <see cref="SendingWindow"/> to finish and save, so the lock doesn't expire while its e-mails are unsaved.
    /// </summary>
    public static readonly TimeSpan LockExpiration = TimeSpan.FromMinutes(30);

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
    private int _emailed;
    private DateTime? _lockedUtc;
    private bool _lockUnavailable;
    private bool _holdAll;
    private bool _windowClosed;
    private bool _mailServerFailed;
    private int _sentByScope;

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
    /// Gets whether the scope couldn't get the day's lock in time, so its e-mails are held for a later run.
    /// </summary>
    public bool LockUnavailable
        => _lockUnavailable;

    /// <summary>
    /// Gets whether the mail server failed an e-mail of the scope (<see cref="HoldAfterMailServerFailure"/>), so the
    /// scope's later e-mails are held for a later run.
    /// </summary>
    public bool MailServerFailed
        => _mailServerFailed;

    /// <summary>
    /// Gets whether the scope sent at least one e-mail (see <see cref="AddSent"/>): the mail server was taking its
    /// e-mails.
    /// </summary>
    public bool HasSent
        => _sentByScope > 0;

    /// <summary>
    /// Gets whether no more e-mail of the scope goes out, known without asking the store or the lock: the lock couldn't
    /// be had, the e-mails are held (<see cref="HoldEmails"/>), the mail server failed one of them, the sending window
    /// passed, or today's limit is reached.
    /// </summary>
    public bool IsClosed
    {
        get
        {
            if (_holdAll || _lockUnavailable || _windowClosed || _mailServerFailed)
            {
                return true;
            }

            var utcNow = _clock.UtcNow;

            if (_lockedUtc is { } locked && utcNow - locked >= SendingWindow)
            {
                return true;
            }

            return _countedDayStartUtc is { } counted
                && counted == SurveyInvitationService.DayStartUtc(utcNow, _timeZoneId)
                && _emailed >= _limit;
        }
    }

    /// <summary>
    /// Holds every later e-mail of the scope without asking for the lock, when the caller already knows the lock is busy
    /// (an earlier invitation list of the same run couldn't get it).
    /// </summary>
    public void HoldEmails()
        => _holdAll = true;

    /// <summary>
    /// Holds every later e-mail of the scope after the mail server failed one (<see cref="SurveyInvitationDelivery.RetryLater"/>):
    /// while it is down, a batch tries one e-mail, not every one, and no invitation uses up its attempts on an outage.
    /// </summary>
    public void HoldAfterMailServerFailure()
        => _mailServerFailed = true;

    /// <summary>
    /// Gets the start of today in the site's time zone, as UTC: the day the limit counts.
    /// </summary>
    /// <returns>The start of the site's day, in UTC.</returns>
    public async Task<DateTime> TodayStartUtcAsync()
    {
        if (_timeZoneId is null)
        {
            await ReadSettingsAsync();
        }

        return SurveyInvitationService.DayStartUtc(_clock.UtcNow, _timeZoneId);
    }

    /// <summary>
    /// Gets whether one more e-mail may go out now. Takes the lock of the site's day first when the scope doesn't hold
    /// it yet; when the lock can't be taken in time, no later e-mail of the scope goes out, without waiting again.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the e-mail may be sent; call <see cref="AddSent"/> once it went out.</returns>
    public async Task<bool> CanSendAsync(CancellationToken cancellationToken = default)
    {
        if (_holdAll || _lockUnavailable || _windowClosed || _mailServerFailed)
        {
            return false;
        }

        if (_timeZoneId is null)
        {
            await ReadSettingsAsync();
        }

        var utcNow = _clock.UtcNow;

        if (_lockedUtc is { } lockedUtc && utcNow - lockedUtc >= SendingWindow)
        {
            _windowClosed = true;
            _logger.LogWarning("This send stopped sending invitation e-mails {Window} after it took the daily e-mail lock; the rest are held for the hourly reminder task.", SendingWindow);

            return false;
        }

        var dayStartUtc = SurveyInvitationService.DayStartUtc(utcNow, _timeZoneId);

        if (_countedDayStartUtc == dayStartUtc)
        {
            return _emailed < _limit;
        }

        var key = LockKeyPrefix + CalendarTime.ToLocal(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), _timeZoneId).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (!_lockers.ContainsKey(key))
        {
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key, LockTimeout, LockExpiration);

            if (!locked)
            {
                _lockUnavailable = true;
                _logger.LogWarning("The daily invitation e-mail lock '{LockKey}' could not be acquired in time; the invitations wait for a later run.", key);

                return false;
            }

            _lockers[key] = locker;

            // The window counts from the first lock the scope holds, which expires first.
            _lockedUtc ??= _clock.UtcNow;
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
    {
        _emailed++;
        _sentByScope++;
    }

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

        if (_lockedUtc is { } lockedUtc && _clock.UtcNow - lockedUtc >= LockExpiration)
        {
            _logger.LogError("The daily invitation e-mail lock was held longer than its expiration of {Expiration} before the send was saved; another sender may have counted the day's e-mails without this send's.", LockExpiration);
        }

        var lockers = _lockers.Values.ToArray();

        _lockers.Clear();
        _countedDayStartUtc = null;
        _lockedUtc = null;

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

    // One read of the site settings gives both the limit and the time zone.
    private async Task ReadSettingsAsync()
    {
        string timeZoneId = null;
        SurveySettings settings = null;

        if (_siteService is not null && await _siteService.GetSiteSettingsAsync() is { } site)
        {
            settings = site.TryGet<SurveySettings>(out var stored) ? stored : null;
            timeZoneId = site.TimeZoneId;
        }

        if (string.IsNullOrEmpty(timeZoneId))
        {
            timeZoneId = _clock.GetSystemTimeZone()?.TimeZoneId ?? string.Empty;
        }

        _timeZoneId = timeZoneId;
        _limit = Math.Max(1, settings?.InvitationEmailsPerDay ?? SurveySettings.DefaultInvitationEmailsPerDay);
    }
}
