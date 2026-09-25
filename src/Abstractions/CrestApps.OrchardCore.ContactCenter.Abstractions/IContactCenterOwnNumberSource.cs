namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Names telephone numbers that belong to this contact center: the numbers customers dial in on and the numbers it
/// presents when it calls out.
/// </summary>
/// <remarks>
/// A transfer to one of these is a call the platform places to itself. It rings back into the contact center, is
/// routed like a new customer call, and ties up a line on both ends for a caller who is already here. Each module that
/// owns such a number contributes it, so the check does not need to know which provider or feature configured it.
/// </remarks>
public interface IContactCenterOwnNumberSource
{
    /// <summary>
    /// Gets the numbers this source knows belong to the contact center, in any format; callers compare them by
    /// telephone-number equivalence rather than by spelling.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The numbers, possibly empty.</returns>
    Task<IReadOnlyCollection<string>> GetOwnNumbersAsync(CancellationToken cancellationToken = default);
}
