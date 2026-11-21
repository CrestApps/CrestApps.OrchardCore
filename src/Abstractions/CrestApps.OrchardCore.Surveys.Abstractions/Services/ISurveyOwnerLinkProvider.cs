using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Gives the admin pages of an owned survey a link back to its owner, such as the course item a quiz belongs to.
/// Register an implementation with <c>services.AddScoped&lt;ISurveyOwnerLinkProvider, T&gt;()</c>.
/// </summary>
public interface ISurveyOwnerLinkProvider
{
    /// <summary>
    /// Gets the link to an owner, or <see langword="null"/> when the provider does not know its kind.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The link, or <see langword="null"/>.</returns>
    Task<SurveyOwnerLink> GetLinkAsync(RecordRef owner, CancellationToken cancellationToken = default);
}
