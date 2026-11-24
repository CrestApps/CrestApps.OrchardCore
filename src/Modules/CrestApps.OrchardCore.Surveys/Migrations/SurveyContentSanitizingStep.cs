using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Saves again, a page per run, every survey draft, published version, template and bank question whose HTML texts the
/// sanitizer changes, so author HTML stored before the stores sanitized on save (or stored in a translation whose source
/// text was empty) is cleaned at rest. Records the sanitizer leaves as they are are not saved again, so their modified
/// time is kept. Every walk saves with YesSql's concurrency check, so an edit saved while a run holds its page is never
/// overwritten with the copy the run loaded: the run's commit fails, and the next run reads the page again.
/// <see cref="SurveyContentSanitizingMigrations"/> asks for it.
/// </summary>
internal sealed class SurveyContentSanitizingStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Surveys.SanitizeContent";

    private readonly ISession _session;
    private readonly SurveyStore _surveyStore;
    private readonly SurveyVersionStore _versionStore;
    private readonly SurveyTemplateStore _templateStore;
    private readonly BankQuestionStore _bankQuestionStore;
    private readonly SurveyContentSanitizer _sanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyContentSanitizingStep"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="versionStore">The survey version store.</param>
    /// <param name="templateStore">The survey template store.</param>
    /// <param name="bankQuestionStore">The bank question store.</param>
    /// <param name="sanitizer">The survey content sanitizer.</param>
    public SurveyContentSanitizingStep(
        ISession session,
        SurveyStore surveyStore,
        SurveyVersionStore versionStore,
        SurveyTemplateStore templateStore,
        BankQuestionStore bankQuestionStore,
        SurveyContentSanitizer sanitizer)
    {
        _session = session;
        _surveyStore = surveyStore;
        _versionStore = versionStore;
        _templateStore = templateStore;
        _bankQuestionStore = bankQuestionStore;
        _sanitizer = sanitizer;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        const string collection = SurveysConstants.CollectionName;

        return SurveyUpgradePages.RunAsync(
            context,
            cursor => SurveyUpgradePages.VisitPageAsync(_session, _surveyStore, collection, cursor, survey => Changes(survey.Draft, _sanitizer.Sanitize), save: _surveyStore.SaveUnchangedAsync, cancellationToken: cancellationToken),
            cursor => SurveyUpgradePages.VisitPageAsync(_session, _versionStore, collection, cursor, version => Changes(version.Definition, _sanitizer.Sanitize), save: _versionStore.SaveUnchangedAsync, cancellationToken: cancellationToken),
            cursor => SurveyUpgradePages.VisitPageAsync(_session, _templateStore, collection, cursor, template => Changes(template.Definition, _sanitizer.Sanitize), save: _templateStore.SaveUnchangedAsync, cancellationToken: cancellationToken),
            cursor => SurveyUpgradePages.VisitPageAsync(_session, _bankQuestionStore, collection, cursor, question => Changes(question.Question, _sanitizer.Sanitize), save: _bankQuestionStore.SaveUnchangedAsync, cancellationToken: cancellationToken));
    }

    private static bool Changes<T>(T value, Action<T> sanitize)
        where T : class
    {
        if (value is null)
        {
            return false;
        }

        var before = JsonSerializer.Serialize(value);

        sanitize(value);

        return !string.Equals(before, JsonSerializer.Serialize(value), StringComparison.Ordinal);
    }
}
