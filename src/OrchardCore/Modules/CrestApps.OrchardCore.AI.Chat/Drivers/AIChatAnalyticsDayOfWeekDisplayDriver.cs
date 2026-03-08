using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that computes and renders day-of-week usage distribution.
/// </summary>
public sealed class AIChatAnalyticsDayOfWeekDisplayDriver : DisplayDriver<AIChatAnalyticsReport>
{
    internal readonly IStringLocalizer S;

    public AIChatAnalyticsDayOfWeekDisplayDriver(IStringLocalizer<AIChatAnalyticsDayOfWeekDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Display(AIChatAnalyticsReport context, BuildDisplayContext buildContext)
    {
        return Initialize<ChatAnalyticsDayOfWeekViewModel>("ChatAnalyticsDayOfWeek", model =>
        {
            var dayNames = new[]
            {
                S["Sunday"].Value,
                S["Monday"].Value,
                S["Tuesday"].Value,
                S["Wednesday"].Value,
                S["Thursday"].Value,
                S["Friday"].Value,
                S["Saturday"].Value,
            };

            var dayGroups = context.Events
                .GroupBy(e => (int)e.SessionStartedUtc.DayOfWeek)
                .OrderBy(g => g.Key);

            for (var day = 0; day < 7; day++)
            {
                var group = dayGroups.FirstOrDefault(g => g.Key == day);
                model.DayDistribution.Add(new DayOfWeekUsage
                {
                    DayIndex = day,
                    Label = dayNames[day],
                    SessionCount = group?.Count() ?? 0,
                });
            }
        }).Location("Content:3");
    }
}
