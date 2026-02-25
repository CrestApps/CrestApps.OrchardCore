using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that computes and renders time-of-day usage distribution.
/// </summary>
public sealed class AIChatAnalyticsTimeOfDayDisplayDriver : DisplayDriver<AIChatAnalyticsReport>
{
    internal readonly IStringLocalizer S;

    public AIChatAnalyticsTimeOfDayDisplayDriver(IStringLocalizer<AIChatAnalyticsTimeOfDayDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Display(AIChatAnalyticsReport context, BuildDisplayContext buildContext)
    {
        return Initialize<ChatAnalyticsTimeOfDayViewModel>("ChatAnalyticsTimeOfDay", model =>
        {
            var hourlyGroups = context.Events
                .GroupBy(e => e.SessionStartedUtc.Hour)
                .OrderBy(g => g.Key);

            for (var hour = 0; hour < 24; hour++)
            {
                var group = hourlyGroups.FirstOrDefault(g => g.Key == hour);
                model.HourlyDistribution.Add(new HourlyUsage
                {
                    Hour = hour,
                    Label = FormatHourLabel(hour),
                    SessionCount = group?.Count() ?? 0,
                });
            }
        }).Location("Content:2");
    }

    private string FormatHourLabel(int hour)
    {
        var period = hour < 12 ? S["AM"].Value : S["PM"].Value;
        var displayHour = hour == 0 ? 12 : (hour > 12 ? hour - 12 : hour);

        return $"{displayHour} {period}";
    }
}
