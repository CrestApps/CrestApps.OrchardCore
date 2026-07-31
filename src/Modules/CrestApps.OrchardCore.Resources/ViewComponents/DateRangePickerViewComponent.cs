using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Resources.ViewComponents;

/// <summary>
/// Renders a reusable date-range picker that enhances two machine-formatted date/time inputs
/// with preset ranges, a custom range, and single-bound "on or before" / "on or after" selections.
/// </summary>
public sealed class DateRangePickerViewComponent : ViewComponent
{
    private const string MachineFormat = "yyyy-MM-ddTHH:mm";

    /// <summary>
    /// Renders the date-range picker for the provided from/to inputs.
    /// </summary>
    /// <param name="fromName">The form field name used for the inclusive lower bound.</param>
    /// <param name="toName">The form field name used for the inclusive upper bound.</param>
    /// <param name="fromId">The HTML identifier for the lower-bound input. Defaults to a value derived from <paramref name="fromName"/>.</param>
    /// <param name="toId">The HTML identifier for the upper-bound input. Defaults to a value derived from <paramref name="toName"/>.</param>
    /// <param name="from">The initial lower-bound value, if any.</param>
    /// <param name="to">The initial upper-bound value, if any.</param>
    /// <param name="label">The optional label rendered above the picker.</param>
    /// <param name="placeholder">The placeholder shown on the toggle when no range is selected.</param>
    /// <param name="wrapperCssClass">The CSS classes applied to the picker root element.</param>
    /// <returns>The rendered picker view.</returns>
    public IViewComponentResult Invoke(
        string fromName,
        string toName,
        string fromId = null,
        string toId = null,
        DateTime? from = null,
        DateTime? to = null,
        string label = null,
        string placeholder = null,
        string wrapperCssClass = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(fromName);
        ArgumentException.ThrowIfNullOrEmpty(toName);

        var culture = CultureInfo.CurrentCulture;
        var pickerId = "drp-" + Guid.NewGuid().ToString("N")[..8];

        var model = new DateRangePickerViewModel
        {
            PickerId = pickerId,
            GroupName = pickerId + "-range",
            Label = label,
            Placeholder = placeholder,
            WrapperCssClass = string.IsNullOrEmpty(wrapperCssClass) ? "col p-1" : wrapperCssClass,
            FromName = fromName,
            ToName = toName,
            FromId = string.IsNullOrEmpty(fromId) ? DeriveId(fromName) : fromId,
            ToId = string.IsNullOrEmpty(toId) ? DeriveId(toName) : toId,
            FromValue = from?.ToString(MachineFormat, CultureInfo.InvariantCulture),
            ToValue = to?.ToString(MachineFormat, CultureInfo.InvariantCulture),
            DatePattern = culture.DateTimeFormat.ShortDatePattern,
            TimePattern = culture.DateTimeFormat.ShortTimePattern,
            WeekStart = (int)culture.DateTimeFormat.FirstDayOfWeek,
            HasInitialValue = from.HasValue || to.HasValue,
        };

        return View(model);
    }

    private static string DeriveId(string name)
        => name.Replace('.', '_').Replace('[', '_').Replace(']', '_');
}
