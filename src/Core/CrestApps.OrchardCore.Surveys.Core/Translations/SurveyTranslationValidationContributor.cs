using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Translations;

/// <summary>
/// Warns before publishing about translations of items that no longer exist, so authors can tidy them, and about
/// translations made from an original text that has changed since.
/// </summary>
public sealed class SurveyTranslationValidationContributor : ISurveyDefinitionValidationContributor
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTranslationValidationContributor"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyTranslationValidationContributor(IStringLocalizer<SurveyTranslationValidationContributor> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public Task ValidateAsync(SurveyDefinitionValidationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var translations = context.Definition?.Translations;

        if (translations is null || translations.Count == 0)
        {
            return Task.CompletedTask;
        }

        var known = SurveyTextCatalog.KnownKeys(context.Definition);

        foreach (var (culture, texts) in translations)
        {
            var stale = (texts ?? []).Keys.Count(key => !known.Contains(key));

            if (stale > 0)
            {
                context.AddWarning("stale-translation", S["The {0} translation has {1} texts of items that no longer exist. They are ignored.", culture, stale]);
            }

            var outdated = SurveyTranslations.OutdatedKeys(context.Definition, culture).Count;

            if (outdated > 0)
            {
                context.AddWarning("outdated-translation", S["The {0} translation has {1} texts whose original text changed after they were translated. Respondents still see the old translation.", culture, outdated]);
            }
        }

        return Task.CompletedTask;
    }
}
