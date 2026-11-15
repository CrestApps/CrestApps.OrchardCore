using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Classes.ViewModels;

/// <summary>
/// One meeting row of the session editor, in local time.
/// </summary>
public class ClassMeetingRow
{
    public DateTime? Date { get; set; }

    public TimeSpan? Start { get; set; }

    public TimeSpan? End { get; set; }
}

/// <summary>
/// The editor of the class part.
/// </summary>
public class ClassPartViewModel
{
    public string AppointmentTypeId { get; set; }

    public string[] ResourceIds { get; set; } = [];

    public int Capacity { get; set; }

    public string Location { get; set; }

    public string MeetingUrl { get; set; }

    public SchedulingWaitlistMode WaitlistMode { get; set; }

    public int? MinimumMinutes { get; set; }

    [BindNever]
    public string ClassItemId { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public IList<SelectListItem> AppointmentTypeOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> ResourceOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> WaitlistModeOptions { get; set; } = [];
}

/// <summary>
/// The class panel a learner sees on a class item.
/// </summary>
public class ClassPanelViewModel
{
    public string ClassItemId { get; set; }

    public string CourseId { get; set; }

    public ClassPart Part { get; set; }

    public bool SignedIn { get; set; }

    public bool Enrolled { get; set; }

    public bool EnrollmentActive { get; set; }

    public ClassRegistration Registration { get; set; }

    public IList<ClassPanelSession> Sessions { get; set; } = [];
}

/// <summary>
/// A session in the class panel.
/// </summary>
public class ClassPanelSession
{
    public SchedulingSession Session { get; set; }

    public int PlacesLeft { get; set; }

    public ClassRegistrationWindow Window { get; set; }

    public bool CanRegister { get; set; }
}

/// <summary>
/// Whether a session takes registrations now.
/// </summary>
public enum ClassRegistrationWindow
{
    Open,
    NotYetOpen,
    Closed,
}

/// <summary>
/// The sessions of a class item.
/// </summary>
public class ClassSessionsViewModel
{
    public ContentItem ClassItem { get; set; }

    public ClassPart Part { get; set; }

    public AdminListContext<ClassSessionRow> List { get; set; }
}

/// <summary>
/// A session on the sessions page.
/// </summary>
public class ClassSessionRow
{
    public SchedulingSession Session { get; set; }

    public int Taken { get; set; }

    public int Waiting { get; set; }
}

/// <summary>
/// The session editor.
/// </summary>
public class ClassSessionEditViewModel
{
    public string Title { get; set; }

    public List<ClassMeetingRow> Meetings { get; set; } = [];

    public int Capacity { get; set; }

    public string Location { get; set; }

    public string MeetingUrl { get; set; }

    public DateTime? RegistrationOpens { get; set; }

    public DateTime? RegistrationCloses { get; set; }

    public SchedulingWaitlistMode WaitlistMode { get; set; }

    public int OfferHours { get; set; } = 24;

    [BindNever]
    public string SessionId { get; set; }

    [BindNever]
    public ContentItem ClassItem { get; set; }

    [BindNever]
    public IList<SelectListItem> WaitlistModeOptions { get; set; } = [];
}

/// <summary>
/// The cancel page of a session.
/// </summary>
public class ClassSessionCancelViewModel
{
    public string Reason { get; set; }

    [BindNever]
    public SchedulingSession Session { get; set; }

    [BindNever]
    public ContentItem ClassItem { get; set; }

    [BindNever]
    public int Taken { get; set; }
}

/// <summary>
/// The roster of a session.
/// </summary>
public class ClassRosterViewModel
{
    public SchedulingSession Session { get; set; }

    public ContentItem ClassItem { get; set; }

    public bool CanMark { get; set; }

    public AdminListContext<ClassRosterRow> List { get; set; }

    public AdminListContext<ClassWaitlistRow> Waitlist { get; set; }
}

/// <summary>
/// A registration on the roster of a session.
/// </summary>
public class ClassRosterRow
{
    public Appointment Appointment { get; set; }

    public string Name { get; set; }

    public bool CanMark { get; set; }

    public int TotalMinutes { get; set; }
}

/// <summary>
/// A person waiting for a place in a session.
/// </summary>
public class ClassWaitlistRow
{
    public WaitlistEntry Entry { get; set; }

    public string Name { get; set; }

    public int Position { get; set; }
}
