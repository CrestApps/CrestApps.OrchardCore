using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Grants operations on surveys beyond global permissions, such as course staff managing their course's surveys.
/// Register an implementation with <c>services.AddSurveyAccessProvider&lt;T&gt;()</c>.
/// </summary>
public interface ISurveyAccessProvider
{
    /// <summary>
    /// Decides whether the provider grants an operation.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="survey">The survey.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> to grant, <see langword="false"/> to leave the decision to others.</returns>
    Task<bool> GrantsAsync(ClaimsPrincipal user, Survey survey, SurveyOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists, at once, the surveys on which the provider grants an operation to a user, so pages that list the surveys a
    /// user may read (reports, data sets) don't ask <see cref="GrantsAsync"/> about every survey. Return the owners whose
    /// surveys are granted and the ids of other granted surveys, an empty list when the provider grants nothing, or
    /// <see langword="null"/> (the default) when the provider can't list them; every survey is then asked.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The grants, or <see langword="null"/>.</returns>
    Task<SurveyAccessGrants> ListGrantsAsync(ClaimsPrincipal user, SurveyOperation operation, CancellationToken cancellationToken = default)
        => Task.FromResult<SurveyAccessGrants>(null);
}
