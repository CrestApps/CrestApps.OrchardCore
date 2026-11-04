using CrestApps.OrchardCore.Workforce.ContactCenter.Models;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;

/// <summary>
/// One 15-minute interval of history in the planning group's time zone.
/// </summary>
public sealed class ForecastHistoryPoint
{
    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the interval of the day: 0 for 00:00.
    /// </summary>
    public int Slot { get; set; }

    /// <summary>
    /// Gets or sets the work offered.
    /// </summary>
    public double Volume { get; set; }

    /// <summary>
    /// Gets or sets the work handled.
    /// </summary>
    public double Handled { get; set; }

    /// <summary>
    /// Gets or sets the handle seconds of the work handled.
    /// </summary>
    public double HandleSeconds { get; set; }
}

/// <summary>
/// One 15-minute interval of a forecast, before staffing.
/// </summary>
public sealed class ForecastPoint
{
    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the interval of the day.
    /// </summary>
    public int Slot { get; set; }

    /// <summary>
    /// Gets or sets the work forecast.
    /// </summary>
    public double Volume { get; set; }

    /// <summary>
    /// Gets or sets the average handle time in seconds.
    /// </summary>
    public double AverageHandleSeconds { get; set; }
}

/// <summary>
/// A model's error on held-out weeks.
/// </summary>
/// <param name="Model">The model.</param>
/// <param name="Mape">The mean absolute percentage error of the daily totals.</param>
/// <param name="Wape">The weighted absolute percentage error of the daily totals.</param>
public readonly record struct BacktestResult(ForecastModelKind Model, double Mape, double Wape);

/// <summary>
/// The daily-volume models.
/// </summary>
public static class ForecastModels
{
    /// <summary>
    /// Forecasts with additive Holt-Winters triple exponential smoothing.
    /// </summary>
    /// <param name="history">The daily totals, oldest first, at least two seasons.</param>
    /// <param name="season">The season length in days.</param>
    /// <param name="horizon">How many days to forecast.</param>
    /// <param name="alpha">The level smoothing.</param>
    /// <param name="beta">The trend smoothing.</param>
    /// <param name="gamma">The season smoothing.</param>
    /// <returns>The forecast, one value per day, never negative.</returns>
    public static double[] HoltWinters(IReadOnlyList<double> history, int season, int horizon, double alpha = 0.3, double beta = 0.05, double gamma = 0.2)
    {
        ArgumentNullException.ThrowIfNull(history);

        if (history.Count < season * 2)
        {
            throw new ArgumentException($"Holt-Winters needs at least {season * 2} days of history.", nameof(history));
        }

        var first = history.Take(season).Average();
        var second = history.Skip(season).Take(season).Average();
        var level = first;
        var trend = (second - first) / season;
        var seasonal = history.Take(season).Select(value => value - first).ToArray();

        for (var t = 0; t < history.Count; t++)
        {
            var index = t % season;
            var value = history[t];
            var previousLevel = level;

            level = (alpha * (value - seasonal[index])) + ((1 - alpha) * (level + trend));
            trend = (beta * (level - previousLevel)) + ((1 - beta) * trend);
            seasonal[index] = (gamma * (value - level)) + ((1 - gamma) * seasonal[index]);
        }

        var forecast = new double[horizon];

        for (var h = 1; h <= horizon; h++)
        {
            forecast[h - 1] = Math.Max(0, level + (h * trend) + seasonal[(history.Count + h - 1) % season]);
        }

        return forecast;
    }

    /// <summary>
    /// Forecasts a day as the weighted average of the same weekday in the last weeks, the latest weighing most.
    /// </summary>
    /// <param name="daily">The daily totals by day.</param>
    /// <param name="date">The day to forecast.</param>
    /// <param name="lastHistoryDay">The last day of history.</param>
    /// <param name="weeks">How many weeks to look back.</param>
    /// <returns>The forecast.</returns>
    public static double WeightedHistory(IReadOnlyDictionary<DateOnly, double> daily, DateOnly date, DateOnly lastHistoryDay, int weeks)
    {
        ArgumentNullException.ThrowIfNull(daily);

        var total = 0d;
        var weights = 0d;
        var sameDay = date;

        while (sameDay > lastHistoryDay)
        {
            sameDay = sameDay.AddDays(-7);
        }

        for (var week = 0; week < weeks; week++, sameDay = sameDay.AddDays(-7))
        {
            if (!daily.TryGetValue(sameDay, out var value))
            {
                continue;
            }

            var weight = weeks - week;

            total += weight * value;
            weights += weight;
        }

        return weights > 0 ? total / weights : 0;
    }
}

/// <summary>
/// Builds a forecast from history: the daily volume from a model, scaled by events, split into intervals by each
/// weekday's curve, with each interval's handle time from the same weekday and time.
/// </summary>
public static class ForecastEngine
{
    /// <summary>
    /// The number of 15-minute intervals in a day.
    /// </summary>
    public const int SlotsPerDay = 96;

    /// <summary>
    /// Builds the forecast of a range of days.
    /// </summary>
    /// <param name="history">The history, before the first day.</param>
    /// <param name="fromDate">The first day.</param>
    /// <param name="toDate">The last day.</param>
    /// <param name="model">The model; <see cref="ForecastModelKind.Auto"/> picks the one with the lowest error.</param>
    /// <param name="events">The events.</param>
    /// <param name="historyWeeks">How many weeks the weighted history and the curves read.</param>
    /// <returns>Every interval of every day, in order.</returns>
    public static List<ForecastPoint> Build(IReadOnlyList<ForecastHistoryPoint> history, DateOnly fromDate, DateOnly toDate, ForecastModelKind model, IEnumerable<ForecastEvent> events, int historyWeeks = 8)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);

        var before = history.Where(point => point.Date < fromDate).ToArray();
        var eventList = events.ToArray();
        var points = new List<ForecastPoint>();

        if (model == ForecastModelKind.Auto)
        {
            model = ChooseModel(before, 2, historyWeeks);
        }

        var dailyTotals = DailyForecast(before, fromDate, toDate, model, historyWeeks);
        var curves = Curves(before, historyWeeks);
        var handleTimes = HandleTimes(before);

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            var total = dailyTotals.GetValueOrDefault(date);

            foreach (var forecastEvent in eventList.Where(item => item.Covers(date)))
            {
                total *= Math.Max(0, forecastEvent.VolumeMultiplier);
            }

            var curve = curves.GetValueOrDefault(date.DayOfWeek);

            for (var slot = 0; slot < SlotsPerDay; slot++)
            {
                var share = curve is null ? 0 : curve[slot];

                points.Add(new ForecastPoint
                {
                    Date = date,
                    Slot = slot,
                    Volume = total * share,
                    AverageHandleSeconds = handleTimes.TryGetValue((date.DayOfWeek, slot), out var handle) ? handle : handleTimes.GetValueOrDefault((default, -1)),
                });
            }
        }

        return points;
    }

    /// <summary>
    /// Measures a model by forecasting the last weeks of history from the weeks before them.
    /// </summary>
    /// <param name="history">The history.</param>
    /// <param name="model">The model.</param>
    /// <param name="weeks">How many weeks to hold out.</param>
    /// <param name="historyWeeks">How many weeks the weighted history reads.</param>
    /// <returns>The error of the daily totals.</returns>
    public static BacktestResult Backtest(IReadOnlyList<ForecastHistoryPoint> history, ForecastModelKind model, int weeks = 2, int historyWeeks = 8)
    {
        ArgumentNullException.ThrowIfNull(history);

        if (history.Count == 0)
        {
            return new BacktestResult(model, double.PositiveInfinity, double.PositiveInfinity);
        }

        var lastDay = history.Max(point => point.Date);
        var holdOutFrom = lastDay.AddDays(1 - (weeks * 7));
        var training = history.Where(point => point.Date < holdOutFrom).ToArray();

        if (training.Length == 0 || (model == ForecastModelKind.HoltWinters && DaysOf(training) < 14))
        {
            return new BacktestResult(model, double.PositiveInfinity, double.PositiveInfinity);
        }

        var forecast = DailyForecast(training, holdOutFrom, lastDay, model, historyWeeks);
        var actual = history.Where(point => point.Date >= holdOutFrom).GroupBy(point => point.Date).ToDictionary(group => group.Key, group => group.Sum(point => point.Volume));
        var absoluteError = 0d;
        var actualTotal = 0d;
        var percentages = new List<double>();

        for (var date = holdOutFrom; date <= lastDay; date = date.AddDays(1))
        {
            var real = actual.GetValueOrDefault(date);
            var error = Math.Abs(forecast.GetValueOrDefault(date) - real);

            absoluteError += error;
            actualTotal += real;

            if (real > 0)
            {
                percentages.Add(error / real);
            }
        }

        return new BacktestResult(
            model,
            percentages.Count > 0 ? percentages.Average() : 0,
            actualTotal > 0 ? absoluteError / actualTotal : (absoluteError > 0 ? double.PositiveInfinity : 0));
    }

    /// <summary>
    /// Picks the model with the lowest weighted error on the last weeks of history; the weighted history on a tie.
    /// </summary>
    /// <param name="history">The history.</param>
    /// <param name="weeks">How many weeks to hold out.</param>
    /// <param name="historyWeeks">How many weeks the weighted history reads.</param>
    /// <returns>The model.</returns>
    public static ForecastModelKind ChooseModel(IReadOnlyList<ForecastHistoryPoint> history, int weeks = 2, int historyWeeks = 8)
    {
        var weighted = Backtest(history, ForecastModelKind.WeightedHistory, weeks, historyWeeks);
        var holtWinters = Backtest(history, ForecastModelKind.HoltWinters, weeks, historyWeeks);

        return holtWinters.Wape < weighted.Wape - 1e-9 ? ForecastModelKind.HoltWinters : ForecastModelKind.WeightedHistory;
    }

    private static Dictionary<DateOnly, double> DailyForecast(ForecastHistoryPoint[] history, DateOnly fromDate, DateOnly toDate, ForecastModelKind model, int historyWeeks)
    {
        var result = new Dictionary<DateOnly, double>();

        if (history.Length == 0)
        {
            return result;
        }

        var firstDay = history.Min(point => point.Date);
        var lastDay = history.Max(point => point.Date);
        var totals = history.GroupBy(point => point.Date).ToDictionary(group => group.Key, group => group.Sum(point => point.Volume));

        // Days without history count as no work, so a closed day keeps its place in the weekly season.
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            totals.TryAdd(date, 0);
        }

        if (model == ForecastModelKind.HoltWinters && totals.Count >= 14)
        {
            // Holt-Winters forecasts from the day after the history, so the days between are forecast and skipped.
            var series = totals.OrderBy(entry => entry.Key).Select(entry => entry.Value).ToArray();
            var horizon = toDate.DayNumber - lastDay.DayNumber;
            var forecast = ForecastModels.HoltWinters(series, 7, Math.Max(horizon, 0));

            for (var date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                var index = date.DayNumber - lastDay.DayNumber - 1;
                result[date] = index >= 0 && index < forecast.Length ? forecast[index] : 0;
            }

            return result;
        }

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            result[date] = ForecastModels.WeightedHistory(totals, date, lastDay, historyWeeks);
        }

        return result;
    }

    private static Dictionary<DayOfWeek, double[]> Curves(ForecastHistoryPoint[] history, int historyWeeks)
    {
        var curves = new Dictionary<DayOfWeek, double[]>();

        if (history.Length == 0)
        {
            return curves;
        }

        var since = history.Max(point => point.Date).AddDays(-7 * historyWeeks);

        foreach (var weekday in history.Where(point => point.Date > since).GroupBy(point => point.Date.DayOfWeek))
        {
            var sums = new double[SlotsPerDay];

            foreach (var point in weekday)
            {
                if (point.Slot >= 0 && point.Slot < SlotsPerDay)
                {
                    sums[point.Slot] += point.Volume;
                }
            }

            var total = sums.Sum();

            if (total > 0)
            {
                curves[weekday.Key] = [.. sums.Select(value => value / total)];
            }
        }

        return curves;
    }

    private static Dictionary<(DayOfWeek Day, int Slot), double> HandleTimes(IReadOnlyList<ForecastHistoryPoint> history)
    {
        var result = new Dictionary<(DayOfWeek, int), double>();
        var handled = history.Where(point => point.Handled > 0).ToArray();

        if (handled.Length == 0)
        {
            return result;
        }

        // The overall average is kept under slot -1 for intervals without their own history.
        result[(default, -1)] = handled.Sum(point => point.HandleSeconds) / handled.Sum(point => point.Handled);

        foreach (var group in handled.GroupBy(point => (point.Date.DayOfWeek, point.Slot)))
        {
            result[group.Key] = TrimmedMean([.. group.Select(point => point.HandleSeconds / point.Handled)]);
        }

        return result;
    }

    private static double TrimmedMean(double[] values)
    {
        Array.Sort(values);

        var trim = values.Length >= 10 ? values.Length / 10 : 0;

        return values.Skip(trim).Take(values.Length - (2 * trim)).Average();
    }

    private static int DaysOf(IReadOnlyList<ForecastHistoryPoint> history)
        => history.Max(point => point.Date).DayNumber - history.Min(point => point.Date).DayNumber + 1;
}
