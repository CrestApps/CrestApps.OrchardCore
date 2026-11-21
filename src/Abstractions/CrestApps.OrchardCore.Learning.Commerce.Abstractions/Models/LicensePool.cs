using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Commerce.Models;

/// <summary>
/// Seats in a course an account bought; the account's admins give them to members or invite people to them. The number
/// of seats left is the balance of the seat ledger.
/// </summary>
public sealed class LicensePool : CatalogItem
{
    /// <summary>
    /// Gets or sets the account that owns the seats.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the course the seats are in.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the title shown, such as the course name.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets how many seats were bought.
    /// </summary>
    public int Seats { get; set; }

    /// <summary>
    /// Gets or sets how many days a seat's access lasts once given; empty for good.
    /// </summary>
    public int? AccessDays { get; set; }

    /// <summary>
    /// Gets or sets the order that bought the seats.
    /// </summary>
    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets the order line.
    /// </summary>
    public string LineId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public LicensePoolState State { get; set; }

    /// <summary>
    /// Gets or sets when the pool was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// The state of a licence pool.
/// </summary>
public enum LicensePoolState
{
    /// <summary>
    /// Seats can be given.
    /// </summary>
    Active,

    /// <summary>
    /// The order was refunded or canceled; every seat was taken back.
    /// </summary>
    Revoked,
}

/// <summary>
/// A seat of a licence pool: given to a member, or waiting for an invited person.
/// </summary>
public sealed class LicenseSeat : CatalogItem
{
    public string PoolId { get; set; }

    public string AccountId { get; set; }

    public string UserId { get; set; }

    public string Email { get; set; }

    public string InvitationId { get; set; }

    public LicenseSeatState State { get; set; }

    public string GivenBy { get; set; }

    public DateTime GivenUtc { get; set; }

    public DateTime? AssignedUtc { get; set; }

    public DateTime? RemovedUtc { get; set; }
}

/// <summary>
/// The state of a seat.
/// </summary>
public enum LicenseSeatState
{
    /// <summary>
    /// Waiting for an invited person to accept.
    /// </summary>
    Invited,

    /// <summary>
    /// Given to a person, who is enrolled.
    /// </summary>
    Assigned,

    /// <summary>
    /// Taken back; the seat returned to the pool.
    /// </summary>
    Removed,
}
