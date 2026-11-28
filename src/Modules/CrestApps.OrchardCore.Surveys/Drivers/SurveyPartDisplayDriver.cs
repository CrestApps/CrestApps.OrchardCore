using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Shows and edits the survey a content item embeds. Only a user who may manage a survey can choose it (or change the
/// link of a part that embeds it), and the editor lists only those surveys; a content editor without survey rights
/// keeps what the part already embeds.
/// </summary>
internal sealed class SurveyPartDisplayDriver : ContentPartDisplayDriver<SurveyPart>
{
    private readonly SurveyEmbedResolver _resolver;
    private readonly SurveyContentSanitizer _sanitizer;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyStore _surveyStore;
    private readonly ISurveyAuthorizationService _surveyAuthorization;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SurveyAccessScopes _scopes;
    private readonly SurveyIndexReader _reader;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="resolver">The embed resolver.</param>
    /// <param name="sanitizer">The HTML sanitizer of survey texts.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="surveyAuthorization">The survey authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="scopes">Works out the surveys the editor may manage.</param>
    /// <param name="reader">Reads surveys from the survey index.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyPartDisplayDriver(
        SurveyEmbedResolver resolver,
        SurveyContentSanitizer sanitizer,
        SurveyCollectorStore collectorStore,
        SurveyStore surveyStore,
        ISurveyAuthorizationService surveyAuthorization,
        IHttpContextAccessor httpContextAccessor,
        SurveyAccessScopes scopes,
        SurveyIndexReader reader,
        IStringLocalizer<SurveyPartDisplayDriver> stringLocalizer)
    {
        _resolver = resolver;
        _sanitizer = sanitizer;
        _collectorStore = collectorStore;
        _surveyStore = surveyStore;
        _surveyAuthorization = surveyAuthorization;
        _httpContextAccessor = httpContextAccessor;
        _scopes = scopes;
        _reader = reader;
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

            // Only the open surveys the editor may manage, read from the survey index (the scope is worked out at once, and
            // the picker reads only the rows it shows), and the one the part already embeds so the choice stays visible.
            var user = _httpContextAccessor.HttpContext?.User;
            var scope = user is null ? SurveyAccessScope.None : await _scopes.ResolveAsync(user, SurveyOperation.Manage);
            var surveys = scope.IsEmpty
                ? []
                : await _reader.ListByTitleAsync(scope.All ? null : scope.SurveyIds, SurveyStatus.Open, SurveysConstants.PageSize);

            if (!string.IsNullOrEmpty(part.SurveyId) && !surveys.Any(survey => survey.ItemId == part.SurveyId))
            {
                surveys = [.. (await _reader.ListAsync([part.SurveyId], publishedOnly: false)), .. surveys];
            }

            foreach (var survey in surveys)
            {
                model.Surveys.Add(new SelectListItem(survey.Title ?? survey.Name, survey.ItemId, survey.ItemId == part.SurveyId));
            }

            model.Collectors = [new SelectListItem(S["The default link"], string.Empty)];

            foreach (var collector in await _collectorStore.ListBySurveyAsync(part.SurveyId))
            {
                if (SurveyEmbedResolver.CanEmbed(collector))
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

        var surveyId = string.IsNullOrWhiteSpace(model.SurveyId) ? null : model.SurveyId.Trim();
        var collectorId = string.IsNullOrWhiteSpace(model.CollectorId) ? null : model.CollectorId.Trim();

        // Choosing a survey or a link puts that survey's link on the page, which is the survey owner's call: it needs
        // Manage on the survey. Clearing the part, or leaving its choice as it is, needs nothing more.
        if (surveyId is not null && (surveyId != part.SurveyId || collectorId != part.CollectorId))
        {
            var survey = await _surveyStore.FindByIdAsync(surveyId);
            var collector = collectorId is null ? null : await _collectorStore.FindByIdAsync(collectorId);

            if (survey is null || !await CanManageAsync(survey))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.SurveyId), S["You may only embed a survey you can manage."]);

                return Edit(part, context);
            }

            if (collectorId is not null && (collector is null || collector.SurveyId != survey.ItemId || !SurveyEmbedResolver.CanEmbed(collector)))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.CollectorId), S["Choose a link of this survey that can be shown on a page. Invitation and kiosk links cannot."]);

                return Edit(part, context);
            }
        }

        part.SurveyId = surveyId;
        part.CollectorId = collectorId;
        part.Display = model.Display;
        part.Text = string.IsNullOrWhiteSpace(model.Text) ? null : model.Text.Trim();

        return Edit(part, context);
    }

    private async Task<bool> CanManageAsync(Survey survey)
        => _httpContextAccessor.HttpContext?.User is { } user && await _surveyAuthorization.AuthorizeAsync(user, survey, SurveyOperation.Manage);
}
