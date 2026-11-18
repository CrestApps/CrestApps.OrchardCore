using System.Globalization;
using CrestApps.OrchardCore.BusinessDocuments.ViewModels;
using CrestApps.OrchardCore.Numbering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.BusinessDocuments.Drivers;

/// <summary>
/// The number series settings page: how the numbers of every registered series are written, where they start and when
/// they start again. A series left at the format it was registered with is not stored, so it follows its registration.
/// </summary>
public sealed class NumberSeriesSettingsDisplayDriver : SiteDisplayDriver<NumberSeriesSettings>
{
    /// <summary>
    /// The settings group.
    /// </summary>
    public const string GroupId = BusinessDocumentsConstants.NumberSeriesSettingsGroupId;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILocalClock _localClock;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly NumberSeriesOptions _options;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="NumberSeriesSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="localClock">The clock today's date is read from, in the site's time zone.</param>
    /// <param name="localizerFactory">The factory that localizes the titles of the series.</param>
    /// <param name="options">The registered series.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public NumberSeriesSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ILocalClock localClock,
        IStringLocalizerFactory localizerFactory,
        IOptions<NumberSeriesOptions> options,
        IStringLocalizer<NumberSeriesSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _localClock = localClock;
        _localizerFactory = localizerFactory;
        _options = options.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override string SettingsGroupId => GroupId;

    /// <inheritdoc />
    public override IDisplayResult Edit(ISite site, NumberSeriesSettings settings, BuildEditorContext context)
        => Initialize<NumberSeriesSettingsViewModel>("NumberSeriesSettings_Edit", async model =>
        {
            var today = await GetTodayAsync();

            model.Today = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            model.ResetOptions =
            [
                new SelectListItem(S["Never"], nameof(NumberSeriesReset.Never)),
                new SelectListItem(S["Each year"], nameof(NumberSeriesReset.Yearly)),
                new SelectListItem(S["Each month"], nameof(NumberSeriesReset.Monthly)),
            ];

            foreach (var series in GetRegisteredSeries())
            {
                var registered = series.DefaultFormat ?? new NumberSeriesFormat();
                var chosen = settings.Formats is not null && settings.Formats.TryGetValue(series.Name, out var stored) ? stored : null;
                var customized = chosen is not null;
                var format = chosen ?? registered;

                model.Series.Add(new NumberSeriesViewModel
                {
                    Name = series.Name,
                    Title = series.Title is null ? series.Name : _localizerFactory.Localize(series.Title).Value,
                    Pattern = format.Pattern,
                    FirstNumber = format.FirstNumber,
                    Reset = format.Reset,
                    DefaultPattern = registered.Pattern,
                    Example = format.Format(Math.Max(1, format.FirstNumber), today),
                    IsCustomized = customized,
                });
            }
        })
            .Location("Content:1")
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, BusinessDocumentsPermissions.ManageNumberSeries))
            .OnGroup(SettingsGroupId);

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ISite site, NumberSeriesSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, BusinessDocumentsPermissions.ManageNumberSeries))
        {
            return null;
        }

        var model = new NumberSeriesSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var chosen = new Dictionary<string, NumberSeriesFormat>(StringComparer.Ordinal);
        var removed = new List<string>();
        var valid = true;

        for (var index = 0; index < (model.Series?.Count ?? 0); index++)
        {
            var posted = model.Series[index];

            // Only a registered series can be set here; a series no enabled feature registers keeps what it has.
            if (posted?.Name is null || !_options.Series.TryGetValue(posted.Name, out var series))
            {
                continue;
            }

            var format = new NumberSeriesFormat(posted.Pattern?.Trim(), posted.FirstNumber, posted.Reset);
            var error = Enum.IsDefined(posted.Reset) ? format.Validate() : NumberSeriesFormatError.UnknownToken;

            if (error != NumberSeriesFormatError.None)
            {
                var member = error == NumberSeriesFormatError.FirstNumberTooLow ? nameof(NumberSeriesViewModel.FirstNumber) : nameof(NumberSeriesViewModel.Pattern);

                context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(NumberSeriesSettingsViewModel.Series)}[{index}].{member}", GetMessage(error, series));
                valid = false;

                continue;
            }

            if (IsSameFormat(format, series.DefaultFormat ?? new NumberSeriesFormat()))
            {
                removed.Add(series.Name);
            }
            else
            {
                chosen[series.Name] = format;
            }
        }

        // Nothing is changed unless every series is valid, so a page saved with an error keeps the formats it had.
        if (valid)
        {
            var formats = settings.Formats is null
                ? new Dictionary<string, NumberSeriesFormat>(StringComparer.Ordinal)
                : new Dictionary<string, NumberSeriesFormat>(settings.Formats, StringComparer.Ordinal);

            foreach (var name in removed)
            {
                formats.Remove(name);
            }

            foreach (var (name, format) in chosen)
            {
                formats[name] = format;
            }

            settings.Formats = formats;
        }

        return Edit(site, settings, context);
    }

    private IEnumerable<NumberSeries> GetRegisteredSeries()
        => _options.Series.Values
            .Where(series => !string.IsNullOrEmpty(series?.Name))
            .OrderBy(series => series.Name, StringComparer.Ordinal);

    private async Task<DateOnly> GetTodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

    private string GetMessage(NumberSeriesFormatError error, NumberSeries series)
    {
        var title = series.Title is null ? series.Name : _localizerFactory.Localize(series.Title).Value;

        return error switch
        {
            NumberSeriesFormatError.Empty => S["{0}: enter a pattern.", title],
            NumberSeriesFormatError.TooLong => S["{0}: the pattern can have at most {1} characters.", title, NumberSeriesFormat.MaximumPatternLength],
            NumberSeriesFormatError.MissingSequence => S["{0}: the pattern needs the {1} token, so that each number is different.", title, "{seq}"],
            NumberSeriesFormatError.SeveralSequences => S["{0}: the pattern can have the {1} token only once.", title, "{seq}"],
            NumberSeriesFormatError.UnknownToken => S["{0}: the pattern has a token that is not known or a brace that is not closed. Use only {1}.", title, "{seq}, {seq:0000}, {yyyy}, {yy}, {MM}"],
            NumberSeriesFormatError.ResetNeedsYear => S["{0}: numbers that start again each year or month need {1} or {2} in the pattern, or they would repeat.", title, "{yyyy}", "{yy}"],
            NumberSeriesFormatError.ResetNeedsMonth => S["{0}: numbers that start again each month need {1} in the pattern, or they would repeat.", title, "{MM}"],
            NumberSeriesFormatError.FirstNumberTooLow => S["{0}: the first number must be 1 or more.", title],
            _ => S["{0}: the format cannot be used.", title],
        };
    }

    private static bool IsSameFormat(NumberSeriesFormat format, NumberSeriesFormat registered)
        => string.Equals(format.Pattern, registered.Pattern, StringComparison.Ordinal) &&
            format.FirstNumber == registered.FirstNumber &&
            format.Reset == registered.Reset;
}
