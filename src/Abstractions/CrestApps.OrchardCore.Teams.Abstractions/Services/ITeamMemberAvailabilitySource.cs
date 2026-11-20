using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Teams.Services;

/// <summary>
/// Says which users can't take new work now: off shift, on leave, out of office, signed out. A module that knows
/// registers one with <see cref="TeamsServiceCollectionExtensions.AddTeamMemberAvailabilitySource{TSource}"/>. With no
/// source registered, everybody is available. Sources combine: a user any honored source reports is unavailable.
/// </summary>
public interface ITeamMemberAvailabilitySource
{
    /// <summary>
    /// Gets the source's name, which a <see cref="Models.TeamAssignmentRequest.AvailabilitySources"/> list names to honor
    /// it, and which a caller keeps in its settings.
    /// </summary>
    string Name => GetType().Name;

    /// <summary>
    /// Gets what the source is called where a person chooses the sources to honor.
    /// </summary>
    LocalizationSource DisplayName => null;

    /// <summary>
    /// Gets whether a caller that lets people choose honors the source until they choose otherwise. A source about one
    /// kind of work (such as being signed in to a phone) says no, so it counts only where someone turns it on.
    /// </summary>
    bool HonoredByDefault => true;

    /// <summary>
    /// Gets the users, among those asked about, who are not available now.
    /// </summary>
    /// <param name="userIds">The users asked about.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The ids of the users who are not available.</returns>
    Task<IReadOnlyCollection<string>> GetUnavailableAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);
}
