namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Checks whether a worker may be given a published shift, for open-shift claims and trades. The built-in one checks
/// the team, skills, overlapping shifts, rest between shifts and hours; labor rule sets add their own.
/// </summary>
public interface IShiftAssignmentValidator
{
    /// <summary>
    /// Adds to <see cref="ShiftAssignmentContext.Problems"/> any reason the worker can't take the shift.
    /// </summary>
    /// <param name="context">The worker and the shift.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ValidateAsync(ShiftAssignmentContext context, CancellationToken cancellationToken = default);
}
