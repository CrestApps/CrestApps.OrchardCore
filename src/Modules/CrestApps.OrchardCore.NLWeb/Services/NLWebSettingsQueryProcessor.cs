using CrestApps.OrchardCore.NLWeb.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Applies the site's NLWeb settings to a question before anything else runs: the number of results, the response
/// modes the site allows, and the search index the query's <c>site</c> picks.
/// </summary>
internal sealed class NLWebSettingsQueryProcessor : INLWebQueryProcessor
{
    /// <summary>
    /// The key of the <see cref="NLWebSettings"/> in <see cref="NLWebAskContext.Properties"/>.
    /// </summary>
    internal const string SettingsKey = "NLWebSettings";

    /// <summary>
    /// The key of the name of the search index to use in <see cref="NLWebAskContext.Properties"/>.
    /// </summary>
    internal const string IndexProfileNameKey = "NLWebIndexProfileName";

    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NLWebSettingsQueryProcessor"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public NLWebSettingsQueryProcessor(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public int Order => -1000;

    /// <inheritdoc/>
    public async Task ProcessAsync(NLWebAskContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = await _siteService.GetSettingsAsync<NLWebSettings>();

        Apply(context, settings);
    }

    /// <summary>
    /// Applies the settings to the context.
    /// </summary>
    /// <param name="context">The ask context.</param>
    /// <param name="settings">The NLWeb settings.</param>
    internal static void Apply(NLWebAskContext context, NLWebSettings settings)
    {
        context.Properties[SettingsKey] = settings;

        var maxResults = context.Request.Prefer.MaxResults ?? settings.MaxResults;
        context.MaxResults = Math.Clamp(maxResults, 1, NLWebConstants.MaxResultsLimit);

        var mode = string.IsNullOrEmpty(context.Request.Prefer.Mode) ? settings.DefaultMode : context.Request.Prefer.Mode;
        context.Mode = NormalizeMode(mode, settings);

        var indexNames = settings.IndexProfileNames ?? [];
        var indexName = indexNames.FirstOrDefault(name => string.Equals(name, context.Site, StringComparison.OrdinalIgnoreCase))
            ?? indexNames.FirstOrDefault();

        if (!string.IsNullOrEmpty(indexName))
        {
            context.Properties[IndexProfileNameKey] = indexName;
        }
    }

    /// <summary>
    /// Gets the settings applied to a context.
    /// </summary>
    /// <param name="context">The ask context.</param>
    /// <returns>The settings, or the defaults when none were applied.</returns>
    internal static NLWebSettings GetSettings(NLWebAskContext context)
        => context.Properties.TryGetValue(SettingsKey, out var settings) && settings is NLWebSettings nlwebSettings
            ? nlwebSettings
            : new NLWebSettings();

    private static string NormalizeMode(string mode, NLWebSettings settings)
    {
        return mode?.Trim().ToLowerInvariant() switch
        {
            NLWebProtocol.Modes.Summarize when settings.AllowSummarize => NLWebProtocol.Modes.Summarize,
            NLWebProtocol.Modes.Generate when settings.AllowGenerate => NLWebProtocol.Modes.Generate,
            _ => NLWebProtocol.Modes.List,
        };
    }
}
