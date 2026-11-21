using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// The first day of the week of the site's default culture.
/// </summary>
public sealed class SiteCultureWeekStartProvider : ISurveyWeekStartProvider
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteCultureWeekStartProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, to find the localization service.</param>
    public SiteCultureWeekStartProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task<DayOfWeek> GetFirstDayOfWeekAsync()
    {
        var localization = _serviceProvider.GetService<ILocalizationService>();
        var name = localization is null ? null : await localization.GetDefaultCultureAsync();

        try
        {
            return string.IsNullOrEmpty(name)
                ? CultureInfo.InvariantCulture.DateTimeFormat.FirstDayOfWeek
                : CultureInfo.GetCultureInfo(name).DateTimeFormat.FirstDayOfWeek;
        }
        catch (CultureNotFoundException)
        {
            return DayOfWeek.Sunday;
        }
    }
}
