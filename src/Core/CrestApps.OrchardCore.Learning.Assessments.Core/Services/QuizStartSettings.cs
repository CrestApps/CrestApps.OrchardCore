using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// The access code and the honor statement of a quiz, kept where Surveys enforces them: the access code is the password
/// of the survey's default collector, the one a learner's start runs on (Surveys checks it with its wrong-password
/// lockout, after the CAPTCHA), and the honor statement is the survey's statement, which the learner accepts before an
/// attempt starts.
/// </summary>
public sealed class QuizStartSettings
{
    /// <summary>
    /// The collector kind of the collector that holds a quiz's access code. A course item's survey has no public link, so
    /// this collector is only the default one a learner's start runs on; a start on its link is refused, because a quiz
    /// is answered only through an enrollment.
    /// </summary>
    public const string CollectorKind = "LearningCourseItem";

    private readonly ISurveyManager _surveyManager;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyCollectorManager _collectorManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizStartSettings"/> class.
    /// </summary>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="collectorManager">The collector manager, which hashes the access code.</param>
    public QuizStartSettings(
        ISurveyManager surveyManager,
        SurveyCollectorStore collectorStore,
        SurveyCollectorManager collectorManager)
    {
        _surveyManager = surveyManager;
        _collectorStore = collectorStore;
        _collectorManager = collectorManager;
    }

    /// <summary>
    /// Gets whether a quiz's survey has an access code.
    /// </summary>
    /// <param name="survey">The quiz's survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a learner must enter an access code to start an attempt.</returns>
    public async Task<bool> HasAccessCodeAsync(Survey survey, CancellationToken cancellationToken = default)
        => survey is not null && !string.IsNullOrEmpty((await _collectorStore.FindDefaultAsync(survey.ItemId, cancellationToken))?.PasswordHash);

    /// <summary>
    /// Saves a quiz's access code and honor statement on its survey.
    /// </summary>
    /// <param name="survey">The quiz's survey.</param>
    /// <param name="accessCode">A new access code, or empty to keep the current one.</param>
    /// <param name="removeAccessCode">Whether to remove the access code.</param>
    /// <param name="statement">The honor statement, or empty for none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveAsync(Survey survey, string accessCode, bool removeAccessCode, string statement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        var trimmed = string.IsNullOrWhiteSpace(statement) ? null : statement.Trim();

        survey.Access ??= new SurveyAccessSettings();

        if (!string.Equals(survey.Access.Statement, trimmed, StringComparison.Ordinal))
        {
            survey.Access.Statement = trimmed;
            await _surveyManager.UpdateAsync(survey, data: null, cancellationToken);
        }

        var code = accessCode?.Trim();

        if (!removeAccessCode && string.IsNullOrEmpty(code))
        {
            return;
        }

        var collector = await _collectorStore.FindDefaultAsync(survey.ItemId, cancellationToken);

        if (collector is null && removeAccessCode)
        {
            return;
        }

        var isNew = collector is null;

        if (isNew)
        {
            collector = _collectorManager.New(survey.ItemId, CollectorKind);
            collector.Name = "Course access";
            collector.IsDefault = true;
        }

        SurveyCollectorManager.SetPassword(collector, removeAccessCode ? null : code);
        await _collectorManager.SaveAsync(collector, isNew, cancellationToken);
    }
}
