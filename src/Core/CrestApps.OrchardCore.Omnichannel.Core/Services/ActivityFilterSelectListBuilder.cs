using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Builds the source and channel option lists of the activity filters from the registered
/// <see cref="ActivitySourceOptions"/> and <see cref="ActivityChannelOptions"/>, so every screen offers the same
/// values. A selected value that is not registered, such as one in a saved filter or an old link, is kept as an
/// option with its raw value as the label, so the filter shows what it is applying instead of silently resetting.
/// </summary>
public static class ActivityFilterSelectListBuilder
{
    /// <summary>
    /// Builds the source options, ordered by display name.
    /// </summary>
    /// <param name="options">The registered activity sources.</param>
    /// <param name="selectedSource">The currently selected source.</param>
    /// <returns>The source options, without an empty "any" option.</returns>
    public static List<SelectListItem> BuildSourceItems(ActivitySourceOptions options, string selectedSource)
    {
        ArgumentNullException.ThrowIfNull(options);

        return BuildItems(
            options.Sources.Values.Select(entry => (entry.Source, entry.DisplayName?.Value ?? entry.Source)),
            selectedSource);
    }

    /// <summary>
    /// Builds the options of the sources that may be set on activities by hand, ordered by display name.
    /// </summary>
    /// <param name="options">The registered activity sources.</param>
    /// <returns>The manually assignable source options, without an empty option.</returns>
    public static List<SelectListItem> BuildManuallyAssignableSourceItems(ActivitySourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return BuildItems(
            options.Sources.Values
                .Where(entry => entry.CanBeSetManually)
                .Select(entry => (entry.Source, entry.DisplayName?.Value ?? entry.Source)),
            selectedValue: null);
    }

    /// <summary>
    /// Builds the channel options, ordered by display name.
    /// </summary>
    /// <param name="options">The registered activity channels.</param>
    /// <param name="selectedChannel">The currently selected channel.</param>
    /// <returns>The channel options, without an empty "any" option.</returns>
    public static List<SelectListItem> BuildChannelItems(ActivityChannelOptions options, string selectedChannel)
    {
        ArgumentNullException.ThrowIfNull(options);

        return BuildItems(
            options.Channels.Values.Select(entry => (entry.Channel, entry.DisplayName?.Value ?? entry.Channel)),
            selectedChannel);
    }

    /// <summary>
    /// Appends the selected value as an option when no option carries it, and marks the matching option selected.
    /// </summary>
    /// <param name="items">The options to update.</param>
    /// <param name="selectedValue">The currently selected value.</param>
    public static void KeepSelectedValue(IList<SelectListItem> items, string selectedValue)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (string.IsNullOrWhiteSpace(selectedValue))
        {
            return;
        }

        var match = items.FirstOrDefault(item => string.Equals(item.Value, selectedValue, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            items.Add(new SelectListItem(selectedValue, selectedValue, selected: true));

            return;
        }

        match.Selected = true;
    }

    private static List<SelectListItem> BuildItems(IEnumerable<(string Value, string Text)> values, string selectedValue)
    {
        var items = values
            .OrderBy(value => value.Text, StringComparer.CurrentCultureIgnoreCase)
            .Select(value => new SelectListItem(value.Text, value.Value))
            .ToList();

        KeepSelectedValue(items, selectedValue);

        return items;
    }
}
