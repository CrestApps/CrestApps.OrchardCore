using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// The search over the call recordings page, as it arrives on the query string.
/// </summary>
public class CallRecordingFilterViewModel
{
    /// <summary>
    /// Gets or sets the inclusive lower bound, in the viewer's time zone, of when the listed calls started.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper bound, in the viewer's time zone and to the minute, of when the listed calls
    /// started.
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Gets or sets the date range preset the picker was left on (for example <c>today</c> or <c>custom</c>), so the
    /// same option is shown again when the page reloads.
    /// </summary>
    public string Range { get; set; }

    /// <summary>
    /// Gets or sets part of the customer's phone number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the call direction.
    /// </summary>
    public InteractionDirection? Direction { get; set; }

    /// <summary>
    /// Gets or sets the kind of call.
    /// </summary>
    public CallRecordingSource? Source { get; set; }

    /// <summary>
    /// Gets or sets the agent whose calls are listed. Ignored for a viewer who may only hear their own calls.
    /// </summary>
    public string AgentUserId { get; set; }
}

/// <summary>
/// The call recordings page.
/// </summary>
public class CallRecordingListViewModel
{
    /// <summary>
    /// Gets or sets the search the list was made with.
    /// </summary>
    public CallRecordingFilterViewModel Filter { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may hear every user's calls, and so filter by agent.
    /// </summary>
    public bool CanListEveryone { get; set; }

    /// <summary>
    /// Gets or sets the directions the list can be filtered by.
    /// </summary>
    public IList<SelectListItem> DirectionOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the kinds of call the list can be filtered by.
    /// </summary>
    public IList<SelectListItem> SourceOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the recordings on this page.
    /// </summary>
    public IList<CallRecordingListItemViewModel> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of recordings the search matched.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the pager shape rendered beneath the list.
    /// </summary>
    public object Pager { get; set; }
}

/// <summary>
/// A recording on the call recordings page.
/// </summary>
public class CallRecordingListItemViewModel
{
    /// <summary>
    /// Gets or sets the recording.
    /// </summary>
    public CallRecording Recording { get; set; }

    /// <summary>
    /// Gets or sets the name of the agent on the call, or <see langword="null"/> when no person took part.
    /// </summary>
    public string AgentName { get; set; }

    /// <summary>
    /// Gets or sets the customer's number formatted for reading, or <see langword="null"/> when it is not known.
    /// </summary>
    public string CustomerNumber { get; set; }
}

/// <summary>
/// One call recording, with its player and transcript.
/// </summary>
public class CallRecordingDisplayViewModel
{
    /// <summary>
    /// Gets or sets the recording.
    /// </summary>
    public CallRecording Recording { get; set; }

    /// <summary>
    /// Gets or sets the name of the agent on the call, or <see langword="null"/> when no person took part.
    /// </summary>
    public string AgentName { get; set; }

    /// <summary>
    /// Gets or sets the customer's number formatted for reading, or <see langword="null"/> when it is not known.
    /// </summary>
    public string CustomerNumber { get; set; }

    /// <summary>
    /// Gets or sets the transcript, or <see langword="null"/> when the call has none.
    /// </summary>
    public CallRecordingTranscript Transcript { get; set; }

    /// <summary>
    /// Gets or sets the content item identifier of the contact the call was with, when it is known.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer may erase the recording.
    /// </summary>
    public bool CanErase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the recording is under legal hold, and so cannot be erased.
    /// </summary>
    public bool IsUnderLegalHold { get; set; }
}
