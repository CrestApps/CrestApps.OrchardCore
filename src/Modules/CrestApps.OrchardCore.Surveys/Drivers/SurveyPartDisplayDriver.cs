using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Shows and edits the survey a content item embeds.
/// </summary>
internal sealed class SurveyPartDisplayDriver : ContentPartDisplayDriver<SurveyPart>
{
    private readonly SurveyEmbedResolver _resolver;
    private readonly SurveyContentSanitizer _sanitizer;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="resolver">The embed resolver.</param>
    /// <param name="sanitizer">The HTML sanitizer of survey texts.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyPartDisplayDriver(
        SurveyEmbedResolver resolver,
        SurveyContentSanitizer sanitizer,
        SurveyCollectorStore collectorStore,
        ISession session,
        IStringLocalizer<SurveyPartDisplayDriver> stringLocalizer)
    {
        _resolver = resolver;
        _sanitizer = sanitizer;
        _collectorStore = collectorStore;
        _session = session;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Display(SurveyPart part, BuildPartDisplayContext context)
        => Initialize<SurveyPartViewModel>(GetDisplayShapeType(context), async model =>
        {
            var (survey, version, collector) = await _resolver.ResolveAsync(part.SurveyId, part.CollectorId);
            var title = string.IsNullOrWhiteSpace(version?.Definition?.Title) ? survey?.Title : version.Definition.Title;

            model.Title = title;
            model.Introduction = new HtmlString(_sanitizer.Sanitize(version?.Definition?.Introduction));
            model.Token = version is null ? null : collector?.Token;
            model.Display = part.Display;
            model.Text = string.IsNullOrWhiteSpace(part.Text) ? title : part.Text;
        }).Location("Detail", "Content:10").Location("Summary", "Content:10");

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyPart part, BuildPartEditorContext context)
        => Initialize<SurveyPartEditViewModel>(GetEditorShapeType(context), async model =>
        {
            model.SurveyId = part.SurveyId;
            model.CollectorId = part.CollectorId;
            model.Display = part.Display;
            model.Text = part.Text;
            model.Surveys = [new SelectListItem(S["Choose a survey"], string.Empty)];

            var surveys = await _session.QueryIndex<SurveyIndex>(index => index.Status == nameof(SurveyStatus.Open), collection: SurveysConstants.CollectionName)
                .OrderBy(index => index.Title)
                .Take(SurveysConstants.PageSize)
                .ListAsync();

            foreach (var survey in surveys)
            {
                model.Surveys.Add(new SelectListItem(survey.Title ?? survey.Name, survey.ItemId, survey.ItemId == part.SurveyId));
            }

            model.Collectors = [new SelectListItem(S["The default link"], string.Empty)];

            foreach (var collector in await _collectorStore.ListBySurveyAsync(part.SurveyId))
            {
                if (collector.Kind != SurveyCollectorKinds.Invitation)
                {
                    model.Collectors.Add(new SelectListItem(collector.Name, collector.ItemId, collector.ItemId == part.CollectorId));
                }
            }
        });

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyPart part, UpdatePartEditorContext context)
    {
        var model = new SurveyPartEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.SurveyId = string.IsNullOrWhiteSpace(model.SurveyId) ? null : model.SurveyId.Trim();
        part.CollectorId = string.IsNullOrWhiteSpace(model.CollectorId) ? null : model.CollectorId.Trim();
        part.Display = model.Display;
        part.Text = string.IsNullOrWhiteSpace(model.Text) ? null : model.Text.Trim();

        return Edit(part, context);
    }
}
