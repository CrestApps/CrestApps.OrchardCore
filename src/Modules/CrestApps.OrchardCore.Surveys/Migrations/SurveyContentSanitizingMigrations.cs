using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Core.Services;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Re-saves, once, every survey draft, published version, template and bank question whose HTML texts the sanitizer
/// changes, so author HTML stored before the stores sanitized on save (or stored in a translation whose source text was
/// empty) is cleaned at rest. Records the sanitizer leaves as they are are not saved again, so their modified time is
/// kept. It is registered after <see cref="SurveysMigrations"/>, whose tables it reads.
/// </summary>
internal sealed class SurveyContentSanitizingMigrations : DataMigration
{
    private readonly SurveyStore _surveyStore;
    private readonly SurveyVersionStore _versionStore;
    private readonly SurveyTemplateStore _templateStore;
    private readonly BankQuestionStore _bankQuestionStore;
    private readonly SurveyContentSanitizer _sanitizer;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyContentSanitizingMigrations"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="versionStore">The survey version store.</param>
    /// <param name="templateStore">The survey template store.</param>
    /// <param name="bankQuestionStore">The bank question store.</param>
    /// <param name="sanitizer">The survey content sanitizer.</param>
    /// <param name="session">The YesSql session.</param>
    public SurveyContentSanitizingMigrations(
        SurveyStore surveyStore,
        SurveyVersionStore versionStore,
        SurveyTemplateStore templateStore,
        BankQuestionStore bankQuestionStore,
        SurveyContentSanitizer sanitizer,
        ISession session)
    {
        _surveyStore = surveyStore;
        _versionStore = versionStore;
        _templateStore = templateStore;
        _bankQuestionStore = bankQuestionStore;
        _sanitizer = sanitizer;
        _session = session;
    }

    /// <summary>
    /// Sanitizes the stored HTML texts.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        // A page at a time: the step runs on the host's one migration transaction, so it can't commit as it goes.
        var collection = SurveysConstants.CollectionName;

        await SurveyMigrationPages.ForEachAsync(_session, _surveyStore, collection, survey => Changes(survey.Draft, _sanitizer.Sanitize));
        await SurveyMigrationPages.ForEachAsync(_session, _versionStore, collection, version => Changes(version.Definition, _sanitizer.Sanitize));
        await SurveyMigrationPages.ForEachAsync(_session, _templateStore, collection, template => Changes(template.Definition, _sanitizer.Sanitize));
        await SurveyMigrationPages.ForEachAsync(_session, _bankQuestionStore, collection, question => Changes(question.Question, _sanitizer.Sanitize));

        return 1;
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
