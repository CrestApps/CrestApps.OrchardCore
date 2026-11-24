using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Learning.Classes.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Classes.Controllers;

/// <summary>
/// The admin pages of a class item's dated sessions: the sessions, the session editor, cancelling a session, and the
/// roster with its attendance. Every page loads the class item and checks <see cref="ClassesAdminScope"/> on it.
/// </summary>
[Admin]
public sealed class ClassSessionsController : Controller
{
    private static readonly AppointmentStatus[] _onRoster = [AppointmentStatus.Held, AppointmentStatus.Requested, AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.NoShow];
    private static readonly AppointmentStatus[] _markable = [AppointmentStatus.Requested, AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.NoShow];

    private readonly IContentManager _contentManager;
    private readonly ClassesAdminScope _scope;
    private readonly ISchedulingSessionService _sessions;
    private readonly LearnerUserLookup _users;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassSessionsController"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="scope">Decides who manages a class.</param>
    /// <param name="sessions">The Scheduling session service.</param>
    /// <param name="users">Maps user ids to user names.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ClassSessionsController(
        IContentManager contentManager,
        ClassesAdminScope scope,
        ISchedulingSessionService sessions,
        LearnerUserLookup users,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<ClassSessionsController> htmlLocalizer,
        IStringLocalizer<ClassSessionsController> stringLocalizer)
    {
        _contentManager = contentManager;
        _scope = scope;
        _sessions = sessions;
        _users = users;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the sessions of a class item, soonest first.
    /// </summary>
    /// <param name="classItemId">The class course item.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/classes/{classItemId}/sessions", "LearningClassSessions")]
    public async Task<IActionResult> Index(string classItemId)
    {
        var (classItem, refusal) = await LoadClassAsync(classItemId);

        if (refusal is not null)
        {
            return refusal;
        }

        var rows = new List<ClassSessionRow>();

        foreach (var session in (await _sessions.ListBySourceAsync(ClassesConstants.SessionSource, classItemId)).OrderBy(session => session.StartUtc))
        {
            rows.Add(new ClassSessionRow
            {
                Session = session,
                Taken = session.Status == SchedulingSessionStatus.Scheduled ? await _sessions.CountTakenAsync(session.ItemId) : 0,
                Waiting = session.Status == SchedulingSessionStatus.Scheduled
                    ? (await _sessions.ListWaitlistAsync(session.ItemId)).Count(entry => entry.Status == WaitlistStatus.Waiting)
                    : 0,
            });
        }

        return View(new ClassSessionsViewModel
        {
            ClassItem = classItem,
            Part = classItem.Get<ClassPart>(nameof(ClassPart)),
            List = new AdminListContext<ClassSessionRow>
            {
                Source = rows,
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new session, filled from the class's defaults.
    /// </summary>
    /// <param name="classItemId">The class course item.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/classes/{classItemId}/sessions/new", "LearningClassSessionsCreate")]
    public async Task<IActionResult> Create(string classItemId)
    {
        var (classItem, refusal) = await LoadClassAsync(classItemId);

        if (refusal is not null)
        {
            return refusal;
        }

        var part = classItem.Get<ClassPart>(nameof(ClassPart));
        var model = new ClassSessionEditViewModel
        {
            Title = classItem.DisplayText,
            Meetings = [new ClassMeetingRow()],
            Capacity = part.Capacity,
            Location = part.Location,
            MeetingUrl = part.MeetingUrl,
            WaitlistMode = part.WaitlistMode,
        };

        return View("Edit", Prepare(model, classItem, null));
    }

    /// <summary>
    /// Creates a session of the class item, or adds a meeting row to the editor.
    /// </summary>
    /// <param name="classItemId">The class course item.</param>
    /// <param name="model">The editor.</param>
    /// <param name="addMeeting">Whether the Add a meeting button was used.</param>
    /// <returns>The sessions, or the editor with its errors.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Create))]
    [Admin("learning/classes/{classItemId}/sessions/new", "LearningClassSessionsCreate")]
    public async Task<IActionResult> CreatePost(string classItemId, ClassSessionEditViewModel model, bool addMeeting)
    {
        var (classItem, refusal) = await LoadClassAsync(classItemId);

        if (refusal is not null)
        {
            return refusal;
        }

        var part = classItem.Get<ClassPart>(nameof(ClassPart));
        var session = new SchedulingSession
        {
            Source = ClassesConstants.SessionSource,
            SourceId = classItem.ContentItemId,
            AppointmentTypeId = part.AppointmentTypeId,
            ResourceIds = [.. part.ResourceIds ?? []],
        };

        return await SaveAsync(classItem, session, model, addMeeting, created: true);
    }

    /// <summary>
    /// Shows the editor of a session.
    /// </summary>
    /// <param name="id">The session.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/class-sessions/{id}/edit", "LearningClassSessionsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        var (session, classItem, refusal) = await LoadSessionAsync(id);

        if (refusal is not null)
        {
            return refusal;
        }

        var model = new ClassSessionEditViewModel
        {
            Title = session.Title,
            Meetings = await ClassSessionMeetings.ToRowsAsync(_localClock, session.Meetings),
            Capacity = session.Capacity,
            Location = session.Location,
            MeetingUrl = session.MeetingUrl,
            RegistrationOpens = await ToLocalAsync(session.RegistrationOpensUtc),
            RegistrationCloses = await ToLocalAsync(session.RegistrationClosesUtc),
            WaitlistMode = session.WaitlistMode,
            OfferHours = session.OfferHours,
        };

        if (model.Meetings.Count == 0)
        {
            model.Meetings.Add(new ClassMeetingRow());
        }

        return View(Prepare(model, classItem, session));
    }

    /// <summary>
    /// Saves a session, or adds a meeting row to the editor.
    /// </summary>
    /// <param name="id">The session.</param>
    /// <param name="model">The editor.</param>
    /// <param name="addMeeting">Whether the Add a meeting button was used.</param>
    /// <returns>The sessions, or the editor with its errors.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Edit))]
    [Admin("learning/class-sessions/{id}/edit", "LearningClassSessionsEdit")]
    public async Task<IActionResult> EditPost(string id, ClassSessionEditViewModel model, bool addMeeting)
    {
        var (session, classItem, refusal) = await LoadSessionAsync(id);

        if (refusal is not null)
        {
            return refusal;
        }

        if (session.Status == SchedulingSessionStatus.Cancelled)
        {
            await _notifier.WarningAsync(H["A cancelled session cannot be changed."]);

            return RedirectToAction(nameof(Index), new { classItemId = classItem.ContentItemId });
        }

        // A session made before the class had an appointment type takes the class's type now.
        if (string.IsNullOrEmpty(session.AppointmentTypeId))
        {
            session.AppointmentTypeId = classItem.Get<ClassPart>(nameof(ClassPart)).AppointmentTypeId;
        }

        return await SaveAsync(classItem, session, model, addMeeting, created: false);
    }

    /// <summary>
    /// Asks why a session is cancelled.
    /// </summary>
    /// <param name="id">The session.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/class-sessions/{id}/cancel", "LearningClassSessionsCancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        var (session, classItem, refusal) = await LoadSessionAsync(id);

        if (refusal is not null)
        {
            return refusal;
        }

        if (session.Status == SchedulingSessionStatus.Cancelled)
        {
            return RedirectToAction(nameof(Index), new { classItemId = classItem.ContentItemId });
        }

        return View(new ClassSessionCancelViewModel
        {
            Session = session,
            ClassItem = classItem,
            Taken = await _sessions.CountTakenAsync(session.ItemId),
        });
    }

    /// <summary>
    /// Cancels a session with every registration and wait of it.
    /// </summary>
    /// <param name="id">The session.</param>
    /// <param name="model">The reason.</param>
    /// <returns>The sessions.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Cancel))]
    [Admin("learning/class-sessions/{id}/cancel", "LearningClassSessionsCancel")]
    public async Task<IActionResult> CancelPost(string id, ClassSessionCancelViewModel model)
    {
        var (session, classItem, refusal) = await LoadSessionAsync(id);

        if (refusal is not null)
        {
            return refusal;
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), S["Say why the session is cancelled. The learners are told."]);
            model.Session = session;
            model.ClassItem = classItem;
            model.Taken = await _sessions.CountTakenAsync(session.ItemId);

            return View(model);
        }

        var cancelled = await _sessions.CancelAsync(session.ItemId, model.Reason.Trim(), CurrentUserId());

        if (cancelled is { Status: SchedulingSessionStatus.Cancelled })
        {
            await _notifier.SuccessAsync(H["The session was cancelled, with its registrations and waitlist."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The session is busy. Try again in a moment."]);
        }

        return RedirectToAction(nameof(Index), new { classItemId = classItem.ContentItemId });
    }

    /// <summary>
    /// Shows the roster of a session with its attendance, and its waitlist.
    /// </summary>
    /// <param name="id">The session.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/class-sessions/{id}/roster", "LearningClassSessionsRoster")]
    public async Task<IActionResult> Roster(string id)
    {
        var (session, classItem, refusal) = await LoadSessionAsync(id);

        if (refusal is not null)
        {
            return refusal;
        }

        var registrations = (await _sessions.ListRegistrationsAsync(session.ItemId))
            .Where(appointment => _onRoster.Contains(appointment.Status))
            .ToList();
        var waiting = (await _sessions.ListWaitlistAsync(session.ItemId))
            .Where(entry => entry.Status is WaitlistStatus.Waiting or WaitlistStatus.Notified)
            .ToList();
        var names = await _users.GetUserNamesAsync(registrations.Select(appointment => appointment.UserId).Concat(waiting.Select(entry => entry.UserId)));
        var rows = registrations
            .Select(appointment => new ClassRosterRow
            {
                Appointment = appointment,
                Name = Name(names, appointment.UserId, appointment.CustomerName),
                CanMark = session.Status == SchedulingSessionStatus.Scheduled && _markable.Contains(appointment.Status),
                TotalMinutes = session.TotalMinutes,
            })
            .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return View(new ClassRosterViewModel
        {
            Session = session,
            ClassItem = classItem,
            CanMark = rows.Any(row => row.CanMark),
            List = new AdminListContext<ClassRosterRow>
            {
                Source = rows,
            },
            Waitlist = new AdminListContext<ClassWaitlistRow>
            {
                Source = waiting
                    .Select((entry, index) => new ClassWaitlistRow
                    {
                        Entry = entry,
                        Name = Name(names, entry.UserId, entry.CustomerName),
                        Position = index + 1,
                    })
                    .ToList(),
            },
        });
    }

    /// <summary>
    /// Marks the attendance of the roster: attended, partly attended with minutes, absent or not marked.
    /// </summary>
    /// <param name="id">The session.</param>
    /// <param name="attendance">The attendance of each registration, by registration id.</param>
    /// <param name="minutes">The minutes of each partial attendance, by registration id.</param>
    /// <returns>The roster.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Roster))]
    [Admin("learning/class-sessions/{id}/roster", "LearningClassSessionsRoster")]
    public async Task<IActionResult> RosterPost(string id, Dictionary<string, AppointmentAttendance> attendance, Dictionary<string, int?> minutes)
    {
        var (session, _, refusal) = await LoadSessionAsync(id);

        if (refusal is not null)
        {
            return refusal;
        }

        if (session.Status == SchedulingSessionStatus.Cancelled)
        {
            await _notifier.WarningAsync(H["A cancelled session has no attendance to mark."]);

            return RedirectToAction(nameof(Roster), new { id });
        }

        var marks = new List<SessionAttendanceMark>();
        var missingMinutes = 0;

        // Only the registrations of this session are marked; any other id in the form is ignored.
        foreach (var appointment in (await _sessions.ListRegistrationsAsync(session.ItemId)).Where(appointment => _markable.Contains(appointment.Status)))
        {
            if (attendance is null || !attendance.TryGetValue(appointment.ItemId, out var mark))
            {
                continue;
            }

            int? markedMinutes = null;

            if (mark == AppointmentAttendance.Partial)
            {
                if (minutes is null || !minutes.TryGetValue(appointment.ItemId, out markedMinutes) || markedMinutes is not > 0)
                {
                    missingMinutes++;

                    continue;
                }
            }

            marks.Add(new SessionAttendanceMark(appointment.ItemId, mark, markedMinutes));
        }

        var changed = await _sessions.RecordAttendanceAsync(session.ItemId, marks, CurrentUserId());

        if (missingMinutes > 0)
        {
            await _notifier.WarningAsync(H.Plural(missingMinutes, "One partial attendance was not saved: enter the minutes attended.", "{0} partial attendances were not saved: enter the minutes attended."));
        }

        await _notifier.SuccessAsync(changed == 0 ? H["Nothing changed in the attendance."] : H.Plural(changed, "The attendance of 1 learner was saved.", "The attendance of {0} learners was saved."));

        return RedirectToAction(nameof(Roster), new { id });
    }

    private async Task<IActionResult> SaveAsync(ContentItem classItem, SchedulingSession session, ClassSessionEditViewModel model, bool addMeeting, bool created)
    {
        model.Meetings ??= [];

        if (addMeeting)
        {
            // Adding a row is a round trip, so no script is needed; nothing is saved.
            ModelState.Clear();
            model.Meetings.Add(new ClassMeetingRow());

            return View("Edit", Prepare(model, classItem, created ? null : session));
        }

        var conversion = await ClassSessionMeetings.ToUtcAsync(_localClock, model.Meetings);

        foreach (var row in conversion.InvalidRows)
        {
            ModelState.AddModelError($"{nameof(model.Meetings)}[{row}].{nameof(ClassMeetingRow.End)}", S["Give meeting {0} a start and an end after it.", row + 1]);
        }

        if (conversion.Meetings.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Meetings), S["Add at least one meeting with a date, a start and an end."]);
        }

        if (model.Capacity < 1)
        {
            ModelState.AddModelError(nameof(model.Capacity), S["The capacity must be at least one."]);
        }

        var meetingUrl = string.IsNullOrWhiteSpace(model.MeetingUrl) ? null : model.MeetingUrl.Trim();

        if (meetingUrl is not null &&
            (!Uri.TryCreate(meetingUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
        {
            ModelState.AddModelError(nameof(model.MeetingUrl), S["Paste a full meeting link that starts with https://."]);
        }

        var opensUtc = model.RegistrationOpens is { } opens ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(opens, DateTimeKind.Unspecified)) : (DateTime?)null;
        var closesUtc = model.RegistrationCloses is { } closes ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(closes, DateTimeKind.Unspecified)) : (DateTime?)null;

        if (opensUtc is not null && closesUtc is not null && closesUtc <= opensUtc)
        {
            ModelState.AddModelError(nameof(model.RegistrationCloses), S["Registration must close after it opens."]);
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", Prepare(model, classItem, created ? null : session));
        }

        session.Title = string.IsNullOrWhiteSpace(model.Title) ? classItem.DisplayText : model.Title.Trim();
        session.Meetings = [.. conversion.Meetings];
        session.Capacity = model.Capacity;
        session.Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim();
        session.MeetingUrl = meetingUrl;
        session.RegistrationOpensUtc = opensUtc;
        session.RegistrationClosesUtc = closesUtc;
        session.WaitlistMode = model.WaitlistMode;
        session.OfferHours = Math.Max(model.OfferHours, 1);

        var errors = await _sessions.SaveAsync(session, CurrentUserId());

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View("Edit", Prepare(model, classItem, created ? null : session));
        }

        await _notifier.SuccessAsync(created ? H["The session was added."] : H["The session was saved."]);

        return RedirectToAction(nameof(Index), new { classItemId = classItem.ContentItemId });
    }

    private ClassSessionEditViewModel Prepare(ClassSessionEditViewModel model, ContentItem classItem, SchedulingSession session)
    {
        model.SessionId = session?.ItemId;
        model.ClassItem = classItem;
        model.WaitlistModeOptions = ClassesSelectLists.WaitlistModes(S, model.WaitlistMode);

        return model;
    }

    private async Task<(ContentItem ClassItem, IActionResult Refusal)> LoadClassAsync(string classItemId)
    {
        var classItem = string.IsNullOrEmpty(classItemId) ? null : await _contentManager.GetAsync(classItemId, VersionOptions.Latest);

        if (classItem is null || !classItem.Has<ClassPart>())
        {
            return (null, NotFound());
        }

        if (!await _scope.CanManageAsync(User, classItem))
        {
            return (null, Forbid());
        }

        return (classItem, null);
    }

    private async Task<(SchedulingSession Session, ContentItem ClassItem, IActionResult Refusal)> LoadSessionAsync(string id)
    {
        var session = await _sessions.FindByIdAsync(id);

        // Only the sessions of classes are managed here; other Scheduling sessions are not found.
        if (session is null || session.Source != ClassesConstants.SessionSource)
        {
            return (null, null, NotFound());
        }

        var (classItem, refusal) = await LoadClassAsync(session.SourceId);

        return (session, classItem, refusal);
    }

    private async Task<DateTime?> ToLocalAsync(DateTime? utc)
        => utc is { } value
            ? (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))).DateTime
            : null;

    private static string Name(IDictionary<string, string> names, string userId, string fallback)
        => !string.IsNullOrEmpty(userId) && names.TryGetValue(userId, out var name) ? name : fallback ?? userId;

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
