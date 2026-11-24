using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Says how long a learner spent, as people read it: "Less than a minute", "12 min", "1 h 05 min".
/// </summary>
public sealed class LearningTimeText
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningTimeText"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningTimeText(IStringLocalizer<LearningTimeText> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the text of a time spent.
    /// </summary>
    /// <param name="seconds">The seconds spent.</param>
    /// <returns>The text.</returns>
    public LocalizedString Format(long seconds)
    {
        if (seconds <= 0)
        {
            return S["None recorded"];
        }

        if (seconds < 60)
        {
            return S["Less than a minute"];
        }

        var minutes = seconds / 60;

        if (minutes < 60)
        {
            return S["{0} min", minutes];
        }

        return S["{0} h {1} min", minutes / 60, (minutes % 60).ToString("00", System.Globalization.CultureInfo.CurrentCulture)];
    }
}
