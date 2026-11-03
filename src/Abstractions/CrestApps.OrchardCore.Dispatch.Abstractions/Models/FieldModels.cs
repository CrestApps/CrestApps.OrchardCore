using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.Dispatch.Models;

/// <summary>
/// What a command from the technician app does.
/// </summary>
public enum FieldCommandKind
{
    /// <summary>
    /// Moves the visit to another status, such as en route.
    /// </summary>
    Status,

    /// <summary>
    /// Answers a checklist question.
    /// </summary>
    Answer,

    /// <summary>
    /// Adds a note to the work order.
    /// </summary>
    Note,

    /// <summary>
    /// Records parts used from a line.
    /// </summary>
    Part,

    /// <summary>
    /// Records the customer's signature.
    /// </summary>
    Signature,

    /// <summary>
    /// Adds a photo to the work order.
    /// </summary>
    Photo,

    /// <summary>
    /// Asks for a follow-up work order, with <see cref="FieldCommand.Text"/> as what it is for.
    /// </summary>
    FollowUp,
}

/// <summary>
/// What happened to a command from the technician app.
/// </summary>
public enum FieldCommandOutcome
{
    /// <summary>
    /// It was applied.
    /// </summary>
    Applied,

    /// <summary>
    /// It came before and was not applied again; the first result is given.
    /// </summary>
    Duplicate,

    /// <summary>
    /// It was refused; the reason says why.
    /// </summary>
    Rejected,
}

/// <summary>
/// The codes of refused commands, so the app can react to each.
/// </summary>
public static class FieldRejections
{
    /// <summary>
    /// The visit was given to someone else.
    /// </summary>
    public const string Moved = "moved";

    /// <summary>
    /// The visit was canceled.
    /// </summary>
    public const string Canceled = "canceled";

    /// <summary>
    /// The visit no longer exists.
    /// </summary>
    public const string Missing = "missing";

    /// <summary>
    /// Someone changed it after the technician did, so theirs is kept.
    /// </summary>
    public const string Stale = "stale";

    /// <summary>
    /// The change itself was refused, such as a number out of range.
    /// </summary>
    public const string Invalid = "invalid";
}

/// <summary>
/// One change a technician made in the app, online or offline, sent once it can be.
/// </summary>
public sealed class FieldCommand
{
    /// <summary>
    /// Gets or sets the key the app made for the command, so sending it again changes nothing.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets what the command does.
    /// </summary>
    public FieldCommandKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string VisitId { get; set; }

    /// <summary>
    /// Gets or sets when the technician made the change, in UTC, from the device's clock.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets the status to move to, for a status command.
    /// </summary>
    public VisitStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the note, or why the visit couldn't be finished.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the checklist, by position, for an answer.
    /// </summary>
    public int ChecklistIndex { get; set; }

    /// <summary>
    /// Gets or sets the question, for an answer.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the answer.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the work order line, for parts used.
    /// </summary>
    public string LineId { get; set; }

    /// <summary>
    /// Gets or sets how many parts were used.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Gets or sets the stock location the parts came from, such as the van's.
    /// </summary>
    public string LocationId { get; set; }

    /// <summary>
    /// Gets or sets who signed, for a signature.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the image of a photo or signature, as a data URL.
    /// </summary>
    public string Data { get; set; }
}

/// <summary>
/// The result of one command.
/// </summary>
public sealed class FieldCommandResult
{
    /// <summary>
    /// Gets or sets the command's key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string VisitId { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public FieldCommandOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the code of a refusal, one of <see cref="FieldRejections"/>.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets why it was refused.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// The record of a command that came in, so a command sent again is known.
/// </summary>
public sealed class FieldCommandReceipt : CatalogItem
{
    /// <summary>
    /// Gets or sets the command's key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the worker who sent it.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string VisitId { get; set; }

    /// <summary>
    /// Gets or sets what it did.
    /// </summary>
    public FieldCommandKind Kind { get; set; }

    /// <summary>
    /// Gets or sets what happened to it.
    /// </summary>
    public FieldCommandOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the code of a refusal.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets why it was refused.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets when the technician made it, in UTC.
    /// </summary>
    public DateTime ClientUtc { get; set; }

    /// <summary>
    /// Gets or sets when it came in, in UTC.
    /// </summary>
    public DateTime ReceivedUtc { get; set; }
}

/// <summary>
/// What the technician app keeps on the device: the technician's visits from today, with what they need offline.
/// </summary>
public sealed class FieldPack
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets when the pack was made, in UTC.
    /// </summary>
    public DateTime GeneratedUtc { get; set; }

    /// <summary>
    /// Gets or sets the time zone the visits are shown in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the visits, in the order of the day.
    /// </summary>
    public List<FieldVisit> Visits { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses a technician may move a visit to from each status.
    /// </summary>
    public Dictionary<string, List<string>> NextStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the stock locations parts can be used from, the technician's own first.
    /// </summary>
    public List<FieldStockLocation> StockLocations { get; set; } = [];

    /// <summary>
    /// Gets or sets the location sharing settings and the technician's consent.
    /// </summary>
    public FieldLocationSettings Location { get; set; } = new();
}

/// <summary>
/// A visit as the technician app shows it.
/// </summary>
public sealed class FieldVisit
{
    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the visit's number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public VisitStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the start, in UTC.
    /// </summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end, in UTC.
    /// </summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the start of the arrival window, in UTC.
    /// </summary>
    public DateTime? ArrivalStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the arrival window, in UTC.
    /// </summary>
    public DateTime? ArrivalEndUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the technician leads the visit.
    /// </summary>
    public bool IsLead { get; set; }

    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public FieldWorkOrder WorkOrder { get; set; }
}

/// <summary>
/// A work order as the technician app shows it.
/// </summary>
public sealed class FieldWorkOrder
{
    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public WorkOrderPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public WorkOrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the type's name.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the address, one line.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the address's latitude.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the address's longitude.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Gets or sets how to get in.
    /// </summary>
    public string AccessNotes { get; set; }

    /// <summary>
    /// Gets or sets the person to talk to.
    /// </summary>
    public WorkOrderContact Contact { get; set; } = new();

    /// <summary>
    /// Gets or sets the parts, services and labor.
    /// </summary>
    public List<FieldLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the checklists, with their answers.
    /// </summary>
    public List<WorkOrderChecklist> Checklists { get; set; } = [];

    /// <summary>
    /// Gets or sets the notes, oldest first.
    /// </summary>
    public List<WorkOrderNote> Notes { get; set; } = [];

    /// <summary>
    /// Gets or sets who signed already.
    /// </summary>
    public List<string> SignedBy { get; set; } = [];

    /// <summary>
    /// Gets or sets how many photos are on it.
    /// </summary>
    public int Photos { get; set; }

    /// <summary>
    /// Gets or sets whether the customer must sign before it is completed.
    /// </summary>
    public bool RequiresSignature { get; set; }

    /// <summary>
    /// Gets or sets how many photos it needs before it is completed.
    /// </summary>
    public int RequiredPhotos { get; set; }

    /// <summary>
    /// Gets or sets the ids of the assets the work is on.
    /// </summary>
    public List<string> AssetIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the assets the work is on.
    /// </summary>
    public List<FieldAsset> Assets { get; set; } = [];

    /// <summary>
    /// Gets or sets the latest other work orders on the same assets, newest first.
    /// </summary>
    public List<FieldHistoryItem> History { get; set; } = [];
}

/// <summary>
/// An asset a work order is on, as the technician app shows it.
/// </summary>
public sealed class FieldAsset
{
    /// <summary>
    /// Gets or sets the asset.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets its number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets its name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets its serial number.
    /// </summary>
    public string SerialNumber { get; set; }

    /// <summary>
    /// Gets or sets its make and model.
    /// </summary>
    public string Model { get; set; }
}

/// <summary>
/// An earlier work order on the same equipment, as the technician app shows it.
/// </summary>
public sealed class FieldHistoryItem
{
    /// <summary>
    /// Gets or sets the work order's number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets its summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets its status.
    /// </summary>
    public WorkOrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was completed, if it was.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}

/// <summary>
/// A line of a work order as the technician app shows it.
/// </summary>
public sealed class FieldLine
{
    /// <summary>
    /// Gets or sets the line.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what it is.
    /// </summary>
    public WorkOrderLineKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how many were expected.
    /// </summary>
    public decimal QuantityEstimated { get; set; }

    /// <summary>
    /// Gets or sets how many were used.
    /// </summary>
    public decimal QuantityUsed { get; set; }

    /// <summary>
    /// Gets or sets whether the parts are counted in stock, so a location is needed.
    /// </summary>
    public bool IsStocked { get; set; }

    /// <summary>
    /// Gets or sets the stock item the parts are counted as, when they are.
    /// </summary>
    public string StockItemId { get; set; }
}

/// <summary>
/// A stock location the technician can use parts from.
/// </summary>
public sealed class FieldStockLocation
{
    /// <summary>
    /// Gets or sets the location.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets its name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether it is the technician's own, such as the van they drive.
    /// </summary>
    public bool IsOwn { get; set; }

    /// <summary>
    /// Gets or sets the units it has free of the stock items the pack's lines use, by stock item.
    /// </summary>
    public Dictionary<string, int> Available { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// The location sharing settings the app follows.
/// </summary>
public sealed class FieldLocationSettings
{
    /// <summary>
    /// Gets or sets whether the technician agreed to the current notice.
    /// </summary>
    public bool Consented { get; set; }

    /// <summary>
    /// Gets or sets the version of the notice to agree to.
    /// </summary>
    public string NoticeVersion { get; set; }

    /// <summary>
    /// Gets or sets how often to send the position, in seconds.
    /// </summary>
    public int IntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets how long positions are kept, in days.
    /// </summary>
    public int RetentionDays { get; set; }
}

/// <summary>
/// A worker's agreement to share their location while working, and its history.
/// </summary>
public sealed class WorkerLocationConsent : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the version of the notice agreed to, or <see langword="null"/> while not agreed.
    /// </summary>
    public string NoticeVersion { get; set; }

    /// <summary>
    /// Gets or sets when it was last given or withdrawn, in UTC.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets every change, oldest first.
    /// </summary>
    public List<WorkerLocationConsentChange> History { get; set; } = [];
}

/// <summary>
/// One time a worker agreed to share their location, or withdrew.
/// </summary>
public sealed class WorkerLocationConsentChange
{
    /// <summary>
    /// Gets or sets when, in UTC.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets whether they agreed.
    /// </summary>
    public bool Given { get; set; }

    /// <summary>
    /// Gets or sets the version of the notice.
    /// </summary>
    public string NoticeVersion { get; set; }
}

/// <summary>
/// A position a technician's device sent while they were working.
/// </summary>
public sealed class WorkerLocationPing : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets when the device took the position, in UTC.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets the latitude.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Gets or sets how accurate the position is, in meters.
    /// </summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>
    /// Gets or sets the speed, in meters a second.
    /// </summary>
    public double? Speed { get; set; }

    /// <summary>
    /// Gets or sets the heading, in degrees from north.
    /// </summary>
    public double? Heading { get; set; }

    /// <summary>
    /// Gets or sets the visit the technician was on or going to.
    /// </summary>
    public string VisitId { get; set; }
}

/// <summary>
/// A position sent by the app.
/// </summary>
public sealed class LocationPingInput
{
    /// <summary>
    /// Gets or sets when the device took it, in UTC.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets the latitude.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Gets or sets how accurate it is, in meters.
    /// </summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>
    /// Gets or sets the speed, in meters a second.
    /// </summary>
    public double? Speed { get; set; }

    /// <summary>
    /// Gets or sets the heading, in degrees from north.
    /// </summary>
    public double? Heading { get; set; }
}

/// <summary>
/// What happened to positions sent by the app.
/// </summary>
public sealed class LocationPingResult
{
    /// <summary>
    /// Gets or sets how many were kept.
    /// </summary>
    public int Kept { get; set; }

    /// <summary>
    /// Gets or sets why the rest weren't, if none were kept.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// The settings of the technician app, from the <c>CrestApps:Dispatch:Mobile</c> configuration section.
/// </summary>
public sealed class DispatchMobileOptions
{
    /// <summary>
    /// Gets or sets how many days after today the app keeps visits for, offline.
    /// </summary>
    public int OfflineDays { get; set; } = 2;

    /// <summary>
    /// Gets or sets how often the app sends the technician's position while they work, in seconds.
    /// </summary>
    public int PingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets how long positions are kept, in days.
    /// </summary>
    public int LocationRetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets how long the records of commands are kept, in days; a command sent again after that is applied again.
    /// </summary>
    public int ReceiptRetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the version of the location notice; change it when the notice changes, so technicians agree again.
    /// </summary>
    public string ConsentNoticeVersion { get; set; } = "1";

    /// <summary>
    /// Gets or sets the largest photo or signature accepted, in kilobytes.
    /// </summary>
    public int MaxFileKilobytes { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the most commands taken in one sync.
    /// </summary>
    public int MaxCommandsPerSync { get; set; } = 200;
}
