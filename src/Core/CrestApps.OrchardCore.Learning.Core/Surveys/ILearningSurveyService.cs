using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// The surveys of course items: each course survey, choice and quiz owns a survey that the item creates, and learners
/// answer it through their enrollment.
/// </summary>
public interface ILearningSurveyService
{
    /// <summary>
    /// Creates the survey a course item owns, from a template. Learners answer it through their enrollment, so it needs a
    /// signed-in respondent and gets no public web link.
    /// </summary>
    /// <param name="item">The course item.</param>
    /// <param name="kind">The kind of survey, such as Quiz.</param>
    /// <param name="template">The template, or <see langword="null"/> for a blank survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new survey.</returns>
    Task<Survey> CreateOwnedSurveyAsync(ContentItem item, string kind, string template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a copied course item its own survey: a copy of the source item's survey (published when the source is),
    /// replacing any survey made for the item while it was created. Falls back to a new survey from the template when
    /// the source survey is gone.
    /// </summary>
    /// <param name="item">The copied course item.</param>
    /// <param name="sourceSurveyId">The survey of the item it was copied from.</param>
    /// <param name="kind">The kind of survey, used when the source survey is gone.</param>
    /// <param name="template">The template, used when the source survey is gone.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The copied item's survey.</returns>
    Task<Survey> CopyOwnedSurveyAsync(ContentItem item, string sourceSurveyId, string kind, string template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts or resumes the learner's response to the survey of a course item.
    /// </summary>
    /// <param name="user">The learner.</param>
    /// <param name="item">The course item.</param>
    /// <param name="surveyId">The survey of the item.</param>
    /// <param name="returnUrl">The local URL the ending links back to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result: the response and, for an anonymous survey, the resume token.</returns>
    Task<SurveyStartResult> StartAsync(ClaimsPrincipal user, ContentItem item, string surveyId, string returnUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts or resumes the learner's response to the survey of a course item with what the learner typed on the start
    /// form: the access code (the password of the survey's link, which Surveys checks with its limit on wrong tries) and
    /// whether they accepted the survey's statement, such as an honor statement. A completed response of a course survey
    /// whose version allows changing answers is reopened instead of starting another.
    /// </summary>
    /// <param name="user">The learner.</param>
    /// <param name="item">The course item.</param>
    /// <param name="surveyId">The survey of the item.</param>
    /// <param name="returnUrl">The local URL the ending links back to.</param>
    /// <param name="password">The access code entered, or <see langword="null"/>.</param>
    /// <param name="statementAccepted">Whether the learner accepted the survey's statement.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result: the response and, for an anonymous survey, the resume token.</returns>
    Task<SurveyStartResult> StartAsync(ClaimsPrincipal user, ContentItem item, string surveyId, string returnUrl, string password, bool statementAccepted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells whether the learner may start a new response to the survey of a course item now, and how many attempts are
    /// left, with the access rules Surveys runs on a start (limits with their counting mode and accommodations
    /// included) and the same request <see cref="StartAsync"/> makes. Nothing is started.
    /// </summary>
    /// <param name="user">The learner.</param>
    /// <param name="item">The course item.</param>
    /// <param name="surveyId">The survey of the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SurveyStartCheckResult> CheckStartAsync(ClaimsPrincipal user, ContentItem item, string surveyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the responses given through an enrollment to a survey, newest first, test responses left out.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The responses.</returns>
    Task<IReadOnlyList<SurveyResponse>> ListEnrollmentResponsesAsync(string surveyId, string enrollmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the published course item that owns a survey.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns>The course item, or <see langword="null"/> when the survey is not owned by one.</returns>
    Task<ContentItem> FindOwnerItemAsync(Survey survey);

    /// <summary>
    /// Finds the latest version of the course item that owns a survey, published or not, while it is in a course. The
    /// grading scope reads items the same way, so staff who may grade an unpublished quiz get every grading tool for it.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns>The course item, or <see langword="null"/> when the survey is not owned by an item in a course.</returns>
    Task<ContentItem> FindLatestOwnerItemAsync(Survey survey);

    /// <summary>
    /// Gets whether a survey belongs to a course item, whatever the item's publish state: a draft, an unpublished item,
    /// an item moved out of its course and a removed one all still own their survey.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns><see langword="true"/> when Learning owns the survey.</returns>
    Task<bool> IsOwnedByCourseItemAsync(Survey survey);

    /// <summary>
    /// Lists, from the content index and without loading any item, the owners of the surveys course items own: the latest
    /// version of every content item whose type has the course item part. Access providers that grant every course
    /// item's survey list their grants with it (<see cref="ISurveyAccessProvider.ListGrantsAsync"/>). A survey whose item
    /// was removed is not listed, though <see cref="IsOwnedByCourseItemAsync"/> still counts it as Learning's.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The owners.</returns>
    Task<IReadOnlyCollection<RecordRef>> ListCourseItemOwnersAsync(CancellationToken cancellationToken = default);
}
