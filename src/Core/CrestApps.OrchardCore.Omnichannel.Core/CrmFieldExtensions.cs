using OrchardCore.ContentFields.Fields;

namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// Reads the single value the content fields of the lead and opportunity parts hold. Every method accepts a
/// <see langword="null"/> field, which a part saved before the field was set carries.
/// </summary>
public static class CrmFieldExtensions
{
    /// <summary>
    /// Returns the trimmed text of the field, or <see langword="null"/> when the field is empty.
    /// </summary>
    /// <param name="field">The text field.</param>
    public static string GetTrimmedText(this TextField field)
        => string.IsNullOrWhiteSpace(field?.Text) ? null : field.Text.Trim();

    /// <summary>
    /// Returns the first content item identifier the field picks, or <see langword="null"/> when it picks none.
    /// </summary>
    /// <param name="field">The content picker field.</param>
    public static string GetFirstContentItemId(this ContentPickerField field)
        => field?.ContentItemIds?.FirstOrDefault(id => !string.IsNullOrEmpty(id));

    /// <summary>
    /// Returns the first user identifier the field picks, or <see langword="null"/> when it picks none.
    /// </summary>
    /// <param name="field">The user picker field.</param>
    public static string GetFirstUserId(this UserPickerField field)
        => field?.UserIds?.FirstOrDefault(id => !string.IsNullOrEmpty(id));
}
