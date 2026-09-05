using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for omnichannel activity batch.
/// </summary>
public class OmnichannelActivityBatchViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the activity source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the activity source display name.
    /// </summary>
    [BindNever]
    public string SourceDisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the selected source requires user assignment while loading.
    /// </summary>
    [BindNever]
    public bool RequiresUserAssignment { get; set; }

    /// <summary>
    /// Gets or sets the schedule at.
    /// </summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? ScheduleAt { get; set; }

    /// <summary>
    /// Gets or sets the subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the contact content type.
    /// </summary>
    public string ContactContentType { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier used for outbound activities loaded from this batch.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the communication channel used for outbound activities loaded from this batch.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint used for outbound activities loaded from this batch.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the AI profile identifier used by automated activities loaded from this batch.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile identifier used by dialer activities.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the instructions.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the user ids.
    /// </summary>
    public string[] UserIds { get; set; }

    /// <summary>
    /// Gets or sets the include do no calls.
    /// </summary>
    public bool IncludeDoNoCalls { get; set; }

    /// <summary>
    /// Gets or sets the include do no sms.
    /// </summary>
    public bool IncludeDoNoSms { get; set; }

    /// <summary>
    /// Gets or sets the include do no email.
    /// </summary>
    public bool IncludeDoNoEmail { get; set; }

    /// <summary>
    /// Gets or sets the prevent duplicates.
    /// </summary>
    public bool PreventDuplicates { get; set; }

    /// <summary>
    /// Gets or sets the urgency level.
    /// </summary>
    public ActivityUrgencyLevel UrgencyLevel { get; set; }

    /// <summary>
    /// Gets or sets the lead created from.
    /// </summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? LeadCreatedFrom { get; set; }

    /// <summary>
    /// Gets or sets the lead created to.
    /// </summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
    public DateTime? LeadCreatedTo { get; set; }

    /// <summary>
    /// Gets or sets the only published leads.
    /// </summary>
    public bool OnlyPublishedLeads { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of leads to load.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the phone number to filter leads by.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the phone number match type.
    /// </summary>
    public PhoneNumberMatchType PhoneNumberMatchType { get; set; } = PhoneNumberMatchType.Contains;

    /// <summary>
    /// Gets or sets the time zone identifiers to filter leads by.
    /// </summary>
    public string[] TimeZoneIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the subject content type of the last completed activity to filter leads by.
    /// </summary>
    public string LastActivitySubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the disposition identifier of the last completed activity to filter leads by.
    /// </summary>
    public string LastActivityDispositionId { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    [BindNever]
    public OmnichannelActivityBatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the subject content types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the contact content types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ContactContentTypes { get; set; }

    /// <summary>
    /// Gets or sets the available dialer profiles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> DialerProfiles { get; set; }

    /// <summary>
    /// Gets or sets the selected users.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SelectedUsers { get; set; }

    /// <summary>
    /// Gets or sets the urgency levels.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> UrgencyLevels { get; set; }

    /// <summary>
    /// Gets or sets the available phone number match types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> PhoneNumberMatchTypes { get; set; }

    /// <summary>
    /// Gets or sets the available time zones.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> TimeZones { get; set; }

    /// <summary>
    /// Gets or sets the available dispositions for last activity filter.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Dispositions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the selected source loads through a dialer profile, which
    /// forces the phone channel and hides the outbound channel selection.
    /// </summary>
    [BindNever]
    public bool IsDialerSource { get; set; }

    /// <summary>
    /// Gets or sets the available campaigns for outbound activities.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Campaigns { get; set; }

    /// <summary>
    /// Gets or sets the available channels for outbound activities.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; }

    /// <summary>
    /// Gets or sets the available channel endpoints for outbound activities.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ChannelEndpoints { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI profile selector is shown for this batch. It is shown only
    /// for the automatic source and only when the AI feature is enabled.
    /// </summary>
    [BindNever]
    public bool ShowAIProfile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI may update the contact during automated conversations.
    /// </summary>
    public bool AllowAIToUpdateContact { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI may update the subject during automated conversations.
    /// </summary>
    public bool AllowAIToUpdateSubject { get; set; } = true;

    /// <summary>
    /// Gets or sets the available AI profiles for automated activities.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AIProfiles { get; set; }

    /// <summary>
    /// Gets or sets how long automated conversations wait before sending each AI reply.
    /// </summary>
    public OmnichannelResponseDelayMode ResponseDelayMode { get; set; }

    /// <summary>
    /// Gets or sets the reply delay in seconds (the exact wait when fixed, or the base when random).
    /// </summary>
    public int ResponseDelaySeconds { get; set; }

    /// <summary>
    /// Gets or sets the +/- jitter, in seconds, applied to the base delay when the mode is random.
    /// </summary>
    public int ResponseDelayJitterSeconds { get; set; }

    /// <summary>
    /// Gets or sets the available response-delay modes.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ResponseDelayModes { get; set; }

    /// <summary>
    /// Gets or sets the selected reusable cadence id. Empty means never nudge.
    /// </summary>
    public string CadenceId { get; set; }

    /// <summary>
    /// Gets or sets the available cadences.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Cadences { get; set; }

    /// <summary>
    /// Gets or sets the id of the business-hours calendar that gates background-initiated sends.
    /// </summary>
    public string BusinessHoursCalendarId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the business-hours calendar picker should be shown (a calendar
    /// provider such as ContactCenter is available).
    /// </summary>
    [BindNever]
    public bool ShowBusinessHoursCalendar { get; set; }

    /// <summary>
    /// Gets or sets the available business-hours calendars.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> BusinessHoursCalendars { get; set; }
}
