namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// A change about to be saved on a check, as the <see cref="Services.ICheckChangeHandler"/>s see it: each one may
/// refuse it, such as the module the check pays when the date falls in a closed accounting period.
/// </summary>
public sealed class CheckChangingContext
{
    private readonly List<string> _errors = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckChangingContext"/> class.
    /// </summary>
    /// <param name="check">The check, as it is before the change.</param>
    /// <param name="kind">The change.</param>
    /// <param name="newStatus">The status the check moves to.</param>
    public CheckChangingContext(CheckRecord check, CheckChangeKind kind, CheckStatus newStatus)
    {
        ArgumentNullException.ThrowIfNull(check);

        Check = check;
        Kind = kind;
        NewStatus = newStatus;
    }

    /// <summary>
    /// Gets the check, as it is before the change. Don't change it here.
    /// </summary>
    public CheckRecord Check { get; }

    /// <summary>
    /// Gets the change: <see cref="CheckChangeKind.Voided"/>, <see cref="CheckChangeKind.Reissued"/>,
    /// <see cref="CheckChangeKind.Cleared"/> or <see cref="CheckChangeKind.Uncleared"/>.
    /// </summary>
    public CheckChangeKind Kind { get; }

    /// <summary>
    /// Gets the status the check moves to.
    /// </summary>
    public CheckStatus NewStatus { get; }

    /// <summary>
    /// Gets the date the check is voided on, for <see cref="CheckChangeKind.Voided"/>.
    /// </summary>
    public DateOnly? VoidedOn { get; init; }

    /// <summary>
    /// Gets the date the bank paid the check, for <see cref="CheckChangeKind.Cleared"/>, or the date it was recorded as
    /// paid, for <see cref="CheckChangeKind.Uncleared"/>.
    /// </summary>
    public DateOnly? ClearedOn { get; init; }

    /// <summary>
    /// Gets why the change is made.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets why the change was refused; empty while no handler refused it.
    /// </summary>
    public IReadOnlyList<string> Errors
        => _errors;

    /// <summary>
    /// Gets whether a handler refused the change.
    /// </summary>
    public bool IsRefused
        => _errors.Count > 0;

    /// <summary>
    /// Refuses the change: it is not saved, and the reason is shown to the person who asked for it.
    /// </summary>
    /// <param name="error">Why, for the person who asked for the change.</param>
    public void Refuse(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        _errors.Add(error);
    }
}
