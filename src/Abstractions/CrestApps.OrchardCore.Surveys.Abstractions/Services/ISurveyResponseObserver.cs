using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Hears what happens to responses. Every method does nothing by default. Workflows, notifications, webhooks, the audit
/// trail, telemetry and consumers such as Learning are observers. Register an implementation with
/// <c>services.AddSurveyResponseObserver&lt;T&gt;()</c>. Methods ending in <c>-ed</c> run after the change is saved.
/// </summary>
public interface ISurveyResponseObserver
{
    /// <summary>
    /// A response started.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task StartedAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// A page was completed (Next with valid answers).
    /// </summary>
    /// <param name="context">The event; <see cref="SurveyResponseEventContext.PageId"/> is set.</param>
    /// <returns>A task.</returns>
    Task PageCompletedAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// A response is about to complete; observers may add properties before it is saved.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task CompletingAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// A response was completed.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task CompletedAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// A respondent was screened out.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task ScreenedOutAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// A response was abandoned.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task AbandonedAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// A completed response needs a grader.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task NeedsGradingAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// A response was fully graded (automatically or by a grader).
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task GradedAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// The score of a graded response changed.
    /// </summary>
    /// <param name="context">The event; <see cref="SurveyResponseEventContext.ScoreChange"/> is set.</param>
    /// <returns>A task.</returns>
    Task ScoreChangedAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// A response was voided.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task VoidedAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// A response was deleted.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task DeletedAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// A completed response was reopened.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <returns>A task.</returns>
    Task ReopenedAsync(SurveyResponseEventContext context) => Task.CompletedTask;

    /// <summary>
    /// An accommodation was granted.
    /// </summary>
    /// <param name="surveyOverride">The accommodation.</param>
    /// <returns>A task.</returns>
    Task OverrideGrantedAsync(SurveyOverride surveyOverride) => Task.CompletedTask;
}
