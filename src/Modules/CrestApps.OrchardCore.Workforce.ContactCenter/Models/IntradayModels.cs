using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Models;

/// <summary>
/// What an intraday offer gives workers.
/// </summary>
public enum IntradayOfferKind
{
    /// <summary>
    /// Extra paid time right before or after a shift.
    /// </summary>
    VoluntaryOvertime,

    /// <summary>
    /// Unpaid time off at the start or end of a shift.
    /// </summary>
    VoluntaryTimeOff,
}

/// <summary>
/// Where an intraday offer is.
/// </summary>
public enum IntradayOfferStatus
{
    /// <summary>
    /// Workers can take it.
    /// </summary>
    Open,

    /// <summary>
    /// Every place is taken.
    /// </summary>
    Filled,

    /// <summary>
    /// A manager closed it.
    /// </summary>
    Closed,
}

/// <summary>
/// Voluntary overtime or time off offered to a team's workers for a stretch of a day, first come, first served.
/// </summary>
public sealed class IntradayOffer : CatalogItem
{
    /// <summary>
    /// Gets or sets the planning group it helps.
    /// </summary>
    public string PlanningGroupId { get; set; }

    /// <summary>
    /// Gets or sets the team whose workers can take it.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public IntradayOfferKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets how many workers can take it.
    /// </summary>
    public int Places { get; set; } = 1;

    /// <summary>
    /// Gets or sets the note shown to workers.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public IntradayOfferStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the workers who took it.
    /// </summary>
    public List<IntradayOfferClaim> Claims { get; set; } = [];

    /// <summary>
    /// Gets or sets who offered it.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was offered.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// A worker who took an intraday offer.
/// </summary>
public sealed class IntradayOfferClaim
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the shift it changed.
    /// </summary>
    public string ShiftEventId { get; set; }

    /// <summary>
    /// Gets or sets when it was taken.
    /// </summary>
    public DateTime ClaimedUtc { get; set; }
}

/// <summary>
/// One 15-minute interval of a planning group's day as it happens.
/// </summary>
public sealed class IntradayRow
{
    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the interval is over.
    /// </summary>
    public bool Elapsed { get; set; }

    /// <summary>
    /// Gets or sets the work forecast.
    /// </summary>
    public double ForecastVolume { get; set; }

    /// <summary>
    /// Gets or sets the work offered, once the interval is computed.
    /// </summary>
    public double? ActualVolume { get; set; }

    /// <summary>
    /// Gets or sets the handle time forecast, in seconds.
    /// </summary>
    public double ForecastHandleSeconds { get; set; }

    /// <summary>
    /// Gets or sets the handle time, once the interval is computed.
    /// </summary>
    public double? ActualHandleSeconds { get; set; }

    /// <summary>
    /// Gets or sets the agents needed answering.
    /// </summary>
    public int Required { get; set; }

    /// <summary>
    /// Gets or sets the agents to schedule, with shrinkage.
    /// </summary>
    public int RequiredScheduled { get; set; }

    /// <summary>
    /// Gets or sets the agents on published shifts working then.
    /// </summary>
    public int Scheduled { get; set; }

    /// <summary>
    /// Gets or sets the agents who were available or working, on average, once the interval is computed.
    /// </summary>
    public double? Available { get; set; }

    /// <summary>
    /// Gets or sets the service level reached, once the interval is computed.
    /// </summary>
    public double? ServiceLevel { get; set; }

    /// <summary>
    /// Gets the agents short: needed less scheduled, or negative for a surplus.
    /// </summary>
    public int Gap => Required - Scheduled;
}

/// <summary>
/// A time to take some people off the phones, and the shortfall it adds.
/// </summary>
/// <param name="StartUtc">The start.</param>
/// <param name="EndUtc">The end.</param>
/// <param name="AddedShortfall">The agent-intervals short it adds.</param>
public readonly record struct ActivityWindow(DateTime StartUtc, DateTime EndUtc, int AddedShortfall);
