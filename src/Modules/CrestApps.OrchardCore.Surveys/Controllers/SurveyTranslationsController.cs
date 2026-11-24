using System.Globalization;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Infrastructure.Html;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// Translates the texts of a survey's draft into the site's other cultures, and exports or imports a translation as a
/// JSON file. Translations are part of the definition: respondents see them once the draft is published.
/// </summary>
[Admin]
public sealed class SurveyTranslationsController : Controller
{
    private const long MaxFileBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions _fileOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly ISurveyManager _manager;
    private readonly ISurveyAuthorizationService _surveyAuthorization;
    private readonly ILocalizationService _localizationService;
    private readonly IHtmlSanitizerService _htmlSanitizer;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTranslationsController"/> class.
    /// </summary>
    /// <param name="manager">The survey manager.</param>
    /// <param name="surveyAuthorization">The survey authorization service.</param>
    /// <param name="localizationService">The localization service, for the site's cultures.</param>
    /// <param name="htmlSanitizer">The HTML sanitizer.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The localizer.</param>
    public SurveyTranslationsController(
        ISurveyManager manager,
        ISurveyAuthorizationService surveyAuthorization,
        ILocalizationService localizationService,
        IHtmlSanitizerService htmlSanitizer,
        INotifier notifier,
        IHtmlLocalizer<SurveyTranslationsController> htmlLocalizer)
    {
        _manager = manager;
        _surveyAuthorization = surveyAuthorization;
        _localizationService = localizationService;
        _htmlSanitizer = htmlSanitizer;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows the translation of a culture.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="culture">The culture, or <see langword="null"/> for the first one.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/translations", "SurveysTranslations")]
    public async Task<IActionResult> Index(string id, string culture)
    {
        var survey = await AuthorizeAsync(id);

        if (survey is null)
        {
            return Forbid();
        }

        var cultures = await CulturesAsync();
        var chosen = cultures.FirstOrDefault(item => string.Equals(item.Value, culture, StringComparison.OrdinalIgnoreCase))?.Value
            ?? cultures.FirstOrDefault()?.Value;

        return View(Build(survey, chosen, cultures));
    }

    /// <summary>
    /// Saves the translation of a culture into the draft.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="model">The posted translation.</param>
    /// <returns>A redirect to the page.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [Admin("surveys/{id}/translations", "SurveysTranslations")]
    public async Task<IActionResult> IndexPost(string id, SurveyTranslationsViewModel model)
    {
        var survey = await AuthorizeAsync(id);

        if (survey is null)
        {
            return Forbid();
        }

        var cultures = await CulturesAsync();

        if (model is null || !cultures.Any(item => string.Equals(item.Value, model.Culture, StringComparison.OrdinalIgnoreCase)))
        {
            await _notifier.ErrorAsync(H["Choose one of the site's cultures."]);

            return RedirectToAction(nameof(Index), new { id });
        }

        var draft = SurveyDefinitionCloner.Clone(survey.Draft) ?? new SurveyDefinition();
        var html = SurveyTextCatalog.Describe(draft).Where(entry => entry.IsHtml).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var texts = (model.Entries ?? [])
            .Where(entry => !string.IsNullOrEmpty(entry?.Key))
            .Select(entry => new KeyValuePair<string, string>(entry.Key, html.Contains(entry.Key) && !string.IsNullOrWhiteSpace(entry.Translation) ? _htmlSanitizer.Sanitize(entry.Translation) : entry.Translation));

        // Only the texts that changed, and those the translator confirmed as current, stand for the current originals:
        // saving the page leaves every other one out of date or to be checked.
        var confirmed = (model.Entries ?? [])
            .Where(entry => entry?.Confirmed == true && !string.IsNullOrEmpty(entry.Key))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        SurveyTranslations.Merge(draft, model.Culture, texts, overwrite: true, confirmed);

        if (await _manager.SaveDraftAsync(survey, draft, SurveyDraftStamp.ToUtc(model.DraftStamp), HttpContext.RequestAborted) == SurveyErrorCode.Busy)
        {
            await _notifier.ErrorAsync(H["Someone else changed this survey. Reload the page and try again."]);
        }
        else
        {
            await _notifier.SuccessAsync(H["The translation has been saved in the draft. Publish the survey to show it to respondents."]);
        }

        return RedirectToAction(nameof(Index), new { id, culture = model.Culture });
    }

    /// <summary>
    /// Downloads the translation of a culture as a JSON file, with every original text.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The file.</returns>
    [Admin("surveys/{id}/translations/export", "SurveysTranslationsExport")]
    public async Task<IActionResult> Export(string id, string culture)
    {
        var survey = await AuthorizeAsync(id);

        if (survey is null)
        {
            return Forbid();
        }

        var cultures = await CulturesAsync();

        if (!cultures.Any(item => string.Equals(item.Value, culture, StringComparison.OrdinalIgnoreCase)))
        {
            return NotFound();
        }

        var file = SurveyTranslations.Export(survey.Name, survey.Draft, culture);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(file, _fileOptions));

        return File(bytes, "application/json", $"{survey.Name}.{culture}.json");
    }

    /// <summary>
    /// Imports a translation file into the draft. Only keys of existing items are kept.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="culture">The culture the file is imported as.</param>
    /// <param name="file">The file.</param>
    /// <returns>A redirect to the page.</returns>
    [HttpPost]
    [Admin("surveys/{id}/translations/import", "SurveysTranslationsImport")]
    public async Task<IActionResult> Import(string id, string culture, IFormFile file)
    {
        var survey = await AuthorizeAsync(id);

        if (survey is null)
        {
            return Forbid();
        }

        var cultures = await CulturesAsync();

        if (!cultures.Any(item => string.Equals(item.Value, culture, StringComparison.OrdinalIgnoreCase)))
        {
            await _notifier.ErrorAsync(H["Choose one of the site's cultures."]);

            return RedirectToAction(nameof(Index), new { id });
        }

        if (file is null || file.Length == 0 || file.Length > MaxFileBytes)
        {
            await _notifier.ErrorAsync(H["Choose a translation file of at most 2 MB."]);

            return RedirectToAction(nameof(Index), new { id, culture });
        }

        SurveyTranslationFile content;

        try
        {
            await using var stream = file.OpenReadStream();
            content = await JsonSerializer.DeserializeAsync<SurveyTranslationFile>(stream, _fileOptions, HttpContext.RequestAborted);
        }
        catch (JsonException)
        {
            content = null;
        }

        if (content?.Texts is null)
        {
            await _notifier.ErrorAsync(H["The file is not a survey translation file."]);

            return RedirectToAction(nameof(Index), new { id, culture });
        }

        var draft = SurveyDefinitionCloner.Clone(survey.Draft) ?? new SurveyDefinition();
        var html = SurveyTextCatalog.Describe(draft).Where(entry => entry.IsHtml).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);

        // A text whose original in the file differs from the survey's now was translated before an author reworded it:
        // writing it would give respondents the old meaning, so it is left out and counted.
        var (current, changed) = SurveyTranslations.SplitByOriginal(draft, content.Texts);

        // A text the file gives with the survey's current original was translated from it, so it is current.
        var confirmed = current
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Original))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        var written = SurveyTranslations.Merge(draft, culture, current
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Translation))
            .Select(entry => new KeyValuePair<string, string>(entry.Key, html.Contains(entry.Key) ? _htmlSanitizer.Sanitize(entry.Translation) : entry.Translation)), overwrite: true, confirmed);

        await _manager.SaveDraftAsync(survey, draft, cancellationToken: HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["{0} texts were imported into the draft.", written]);

        if (changed > 0)
        {
            await _notifier.WarningAsync(H["{0} texts were left out because their original text has changed since the file was exported. Export the file again and translate them.", changed]);
        }

        return RedirectToAction(nameof(Index), new { id, culture });
    }

    private static SurveyTranslationsViewModel Build(Survey survey, string culture, List<SelectListItem> cultures)
    {
        var texts = SurveyTranslations.Texts(survey.Draft, culture);
        var outdated = SurveyTranslations.OutdatedKeys(survey.Draft, culture);
        var toCheck = SurveyTranslations.UncheckedKeys(survey.Draft, culture);
        var model = new SurveyTranslationsViewModel
        {
            Survey = survey,
            Culture = culture,
            Cultures = cultures,
            DraftStamp = SurveyDraftStamp.From(survey.DraftModifiedUtc),
        };

        foreach (var entry in SurveyTextCatalog.Describe(survey.Draft))
        {
            var translation = texts.TryGetValue(entry.Key, out var text) ? text : null;

            model.Entries.Add(new SurveyTranslationEntryViewModel
            {
                Key = entry.Key,
                Group = entry.Group,
                Label = entry.Label,
                Original = entry.Text,
                Translation = translation,
                IsHtml = entry.IsHtml,
                Outdated = outdated.Contains(entry.Key),
                Unchecked = toCheck.Contains(entry.Key),
            });

            if (outdated.Contains(entry.Key))
            {
                model.Outdated++;
            }
            else if (!string.IsNullOrWhiteSpace(translation))
            {
                model.Translated++;

                // Translated, but nothing tells whether the original changed since: shown to be checked once.
                if (toCheck.Contains(entry.Key))
                {
                    model.Unchecked++;
                }
            }
        }

        foreach (var item in cultures)
        {
            item.Selected = string.Equals(item.Value, culture, StringComparison.OrdinalIgnoreCase);
        }

        return model;
    }

    private async Task<List<SelectListItem>> CulturesAsync()
    {
        var supported = await _localizationService.GetSupportedCulturesAsync();
        var defaultCulture = await _localizationService.GetDefaultCultureAsync();

        return (supported ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name) && !string.Equals(name, defaultCulture, StringComparison.OrdinalIgnoreCase))
            .Select(name => new SelectListItem(CultureLabel(name), name))
            .ToList();
    }

    private static string CultureLabel(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return name;
        }
    }

    private async Task<Survey> AuthorizeAsync(string id)
    {
        var survey = string.IsNullOrEmpty(id) ? null : await _manager.FindByIdAsync(id);

        return survey is not null && await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.Manage, HttpContext.RequestAborted) ? survey : null;
    }
}
