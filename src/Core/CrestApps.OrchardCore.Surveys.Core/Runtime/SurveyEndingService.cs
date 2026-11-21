using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Chooses and renders the ending of a response, and keeps redirects to local or allowed hosts.
/// </summary>
public sealed class SurveyEndingService
{
    private readonly SurveyLogicEngine _logic;
    private readonly SurveyPipingRenderer _piping;
    private readonly IEnumerable<ISurveyEndingRenderer> _renderers;
    private readonly ISiteService _siteService;
    private readonly SurveyDefinitionLocalizer _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyEndingService"/> class.
    /// </summary>
    /// <param name="logic">The logic engine.</param>
    /// <param name="piping">The piping renderer.</param>
    /// <param name="renderers">The ending renderer of the module, if any.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="localizer">The definition localizer, for translated texts.</param>
    public SurveyEndingService(
        SurveyLogicEngine logic,
        SurveyPipingRenderer piping,
        IEnumerable<ISurveyEndingRenderer> renderers,
        ISiteService siteService,
        SurveyDefinitionLocalizer localizer)
    {
        _localizer = localizer;
        _logic = logic;
        _piping = piping;
        _renderers = renderers;
        _siteService = siteService;
    }

    /// <summary>
    /// Chooses the ending after the last page: the first conditional ending whose condition holds, else the default.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="state">The logic state.</param>
    /// <param name="cacheKey">The formula cache key.</param>
    /// <returns>The ending, or <see langword="null"/> when the survey has none.</returns>
    public SurveyEnding Choose(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, string cacheKey)
    {
        var endings = (definition.Endings ?? []).Where(ending => ending is not null).ToList();

        foreach (var ending in endings)
        {
            if (ending.IsDefault || ending.Condition is null || ending.Condition.Mode == VisibilityMode.Always)
            {
                continue;
            }

            var holds = _logic.Holds(definition, response, state, ending.Condition.Rule, ending.Condition.Formula, cacheKey);

            if (ending.Condition.Mode == VisibilityMode.HideWhen ? !holds : holds)
            {
                return ending;
            }
        }

        return endings.FirstOrDefault(ending => ending.IsDefault) ?? endings.FirstOrDefault();
    }

    /// <summary>
    /// Renders the ending of an ended response.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="definition">The definition of its version.</param>
    /// <param name="response">The response.</param>
    /// <param name="requestHost">The host of the request, which local redirects keep.</param>
    /// <returns>The view.</returns>
    public async Task<SurveyEndingView> RenderAsync(Survey survey, SurveyDefinition definition, SurveyResponse response, string requestHost)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);

        definition = _localizer.Localize(definition, response.Culture);

        var ending = definition.Endings?.FirstOrDefault(item => string.Equals(item.Id, response.EndingId, StringComparison.Ordinal))
            ?? definition.Endings?.FirstOrDefault(item => item.IsDefault);

        var view = new SurveyEndingView
        {
            Title = ending?.Title,
            ScreenedOut = response.Status == SurveyResponseStatus.ScreenedOut,
        };

        if (ending is not null)
        {
            var renderer = _renderers.LastOrDefault();

            view.MessageHtml = renderer is not null
                ? await renderer.RenderAsync(survey, definition, response, ending)
                : _piping.Render(ending.Message, definition, response);
        }

        var redirect = !string.IsNullOrWhiteSpace(ending?.RedirectUrl) ? ending.RedirectUrl : definition.Presentation?.CompletedRedirectUrl;

        if (!string.IsNullOrWhiteSpace(redirect))
        {
            var url = _piping.Render(redirect.Trim(), definition, response, encode: false, urlEncode: true);
            (await _siteService.GetSiteSettingsAsync()).TryGet<SurveySettings>(out var settings);

            if (IsAllowedRedirect(url, requestHost, settings?.AllowedRedirectHosts))
            {
                view.RedirectUrl = url;
            }
        }

        return view;
    }

    /// <summary>
    /// Gets whether a redirect is local or goes to an allowed host.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <param name="requestHost">The host of the request.</param>
    /// <param name="allowedHosts">The allowed hosts.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public static bool IsAllowedRedirect(string url, string requestHost, IEnumerable<string> allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(requestHost) && string.Equals(uri.Host, requestHost, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return allowedHosts?.Any(host => !string.IsNullOrWhiteSpace(host) && string.Equals(host.Trim(), uri.Host, StringComparison.OrdinalIgnoreCase)) == true;
    }
}
