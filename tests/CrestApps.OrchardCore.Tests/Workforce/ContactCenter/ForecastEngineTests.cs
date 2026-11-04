using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// The forecast models and engine: Holt-Winters on daily totals, the weighted history, the split of a day into
/// intervals by each weekday's curve, handle times, events and the backtest that picks a model.
/// </summary>
public sealed class ForecastEngineTests
{
    // A Monday.
    private static readonly DateOnly _start = new(2026, 8, 31);

    [Fact]
    public void HoltWinters_RepeatsAPerfectlyWeeklyPattern()
    {
        double[] week = [120, 110, 105, 100, 90, 40, 20];
        var history = Enumerable.Range(0, 56).Select(day => week[day % 7]).ToArray();

        var forecast = ForecastModels.HoltWinters(history, 7, 14);

        for (var day = 0; day < 14; day++)
        {
            Assert.Equal(week[(56 + day) % 7], forecast[day], 6);
        }
    }

    [Fact]
    public void HoltWinters_FollowsATrend()
    {
        double[] week = [20, 10, 5, 0, -10, -40, -60];
        var history = Enumerable.Range(0, 70).Select(day => 200 + (2d * day) + week[day % 7]).ToArray();

        var forecast = ForecastModels.HoltWinters(history, 7, 7);

        for (var day = 0; day < 7; day++)
        {
            var actual = 200 + (2d * (70 + day)) + week[(70 + day) % 7];

            Assert.InRange(forecast[day], actual * 0.97, actual * 1.03);
        }
    }

    [Fact]
    public void TheEngine_SplitsDaysByEachWeekdaysCurve_AndKeepsClosedHoursEmpty()
    {
        var history = WeekdayHistory(weeks: 8);

        var points = ForecastEngine.Build(history, _start.AddDays(56), _start.AddDays(62), ForecastModelKind.WeightedHistory, []);
        var monday = points.Where(point => point.Date == _start.AddDays(56)).ToDictionary(point => point.Slot);

        Assert.Equal(10, monday[Slot(9, 0)].Volume, 6);
        Assert.Equal(20, monday[Slot(13, 0)].Volume, 6);
        Assert.Equal(0, monday[Slot(20, 0)].Volume, 6);
        Assert.Equal(300, monday[Slot(13, 0)].AverageHandleSeconds, 6);
        Assert.All(points.Where(point => point.Date.DayOfWeek == DayOfWeek.Saturday), point => Assert.Equal(0, point.Volume));
    }

    [Fact]
    public void Events_ScaleTheirDays()
    {
        var history = WeekdayHistory(weeks: 8);
        var wednesday = _start.AddDays(58);
        var thursday = _start.AddDays(59);
        var events = new[]
        {
            new ForecastEvent { Name = "Closed", StartDate = wednesday, EndDate = wednesday, VolumeMultiplier = 0 },
            new ForecastEvent { Name = "Promotion", StartDate = thursday, EndDate = thursday, VolumeMultiplier = 1.5 },
        };

        var points = ForecastEngine.Build(history, _start.AddDays(56), _start.AddDays(62), ForecastModelKind.WeightedHistory, events);

        Assert.All(points.Where(point => point.Date == wednesday), point => Assert.Equal(0, point.Volume));
        Assert.Equal(30, points.Single(point => point.Date == thursday && point.Slot == Slot(13, 0)).Volume, 6);
    }

    [Fact]
    public void TheBacktest_MeasuresEachModel_AndAutoPicksTheBest()
    {
        var history = WeekdayHistory(weeks: 8);

        var weighted = ForecastEngine.Backtest(history, ForecastModelKind.WeightedHistory, weeks: 2);
        var holtWinters = ForecastEngine.Backtest(history, ForecastModelKind.HoltWinters, weeks: 2);

        Assert.Equal(0, weighted.Wape, 6);
        Assert.True(holtWinters.Wape < 0.05);
        Assert.Equal(ForecastModelKind.WeightedHistory, ForecastEngine.ChooseModel(history, weeks: 2));
    }

    private static int Slot(int hour, int minute) => ((hour * 60) + minute) / 15;

    private static List<ForecastHistoryPoint> WeekdayHistory(int weeks)
    {
        var points = new List<ForecastHistoryPoint>();

        for (var day = 0; day < weeks * 7; day++)
        {
            var date = _start.AddDays(day);

            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            for (var slot = Slot(9, 0); slot < Slot(17, 0); slot++)
            {
                var volume = slot < Slot(12, 0) ? 10 : 20;

                points.Add(new ForecastHistoryPoint { Date = date, Slot = slot, Volume = volume, Handled = volume, HandleSeconds = volume * 300 });
            }
        }

        return points;
    }
}
