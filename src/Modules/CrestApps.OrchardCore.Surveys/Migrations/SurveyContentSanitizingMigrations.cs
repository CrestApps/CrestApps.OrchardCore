using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Core.Services;
using OrchardCore.Data.Migration;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyContentSanitizingMigrations"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="versionStore">The survey version store.</param>
    /// <param name="templateStore">The survey template store.</param>
    /// <param name="bankQuestionStore">The bank question store.</param>
    /// <param name="sanitizer">The survey content sanitizer.</param>
    public SurveyContentSanitizingMigrations(
        SurveyStore surveyStore,
        SurveyVersionStore versionStore,
        SurveyTemplateStore templateStore,
        BankQuestionStore bankQuestionStore,
        SurveyContentSanitizer sanitizer)
    {
        _surveyStore = surveyStore;
        _versionStore = versionStore;
        _templateStore = templateStore;
        _bankQuestionStore = bankQuestionStore;
        _sanitizer = sanitizer;
    }

    /// <summary>
    /// Sanitizes the stored HTML texts.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        foreach (var survey in await _surveyStore.GetAllAsync())
        {
            if (Changes(survey.Draft, _sanitizer.Sanitize))
            {
                await _surveyStore.UpdateAsync(survey);
            }
        }

        foreach (var version in await _versionStore.GetAllAsync())
        {
            if (Changes(version.Definition, _sanitizer.Sanitize))
            {
                await _versionStore.UpdateAsync(version);
            }
        }

        foreach (var template in await _templateStore.GetAllAsync())
        {
            if (Changes(template.Definition, _sanitizer.Sanitize))
            {
                await _templateStore.UpdateAsync(template);
            }
        }

        foreach (var question in await _bankQuestionStore.GetAllAsync())
        {
            if (Changes(question.Question, _sanitizer.Sanitize))
            {
                await _bankQuestionStore.UpdateAsync(question);
            }
        }

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
