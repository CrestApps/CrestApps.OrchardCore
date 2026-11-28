using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Hears what happens to surveys. Every method does nothing by default. Register an implementation with
/// <c>services.AddSurveyObserver&lt;T&gt;()</c>.
/// </summary>
public interface ISurveyObserver
{
    /// <summary>
    /// A version was published.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="version">The new version.</param>
    /// <returns>A task.</returns>
    Task PublishedAsync(Survey survey, SurveyVersion version) => Task.CompletedTask;

    /// <summary>
    /// A survey closed.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns>A task.</returns>
    Task ClosedAsync(Survey survey) => Task.CompletedTask;

    /// <summary>
    /// A survey's status changed.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="previous">The status before.</param>
    /// <returns>A task.</returns>
    Task StatusChangedAsync(Survey survey, SurveyStatus previous) => Task.CompletedTask;

    /// <summary>
    /// A survey was deleted.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns>A task.</returns>
    Task DeletedAsync(Survey survey) => Task.CompletedTask;
}
