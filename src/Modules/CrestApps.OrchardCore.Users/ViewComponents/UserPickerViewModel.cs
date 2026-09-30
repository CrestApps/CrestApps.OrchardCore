using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Users.ViewComponents;

/// <summary>
/// View model for the reusable user picker.
/// </summary>
public sealed class UserPickerViewModel
{
    public string Id { get; set; }

    public string InputName { get; set; }

    public string ValueType { get; set; }

    public string[] Roles { get; set; }

    public bool Multiple { get; set; }

    public string Label { get; set; }

    public string ButtonText { get; set; }

    public string SearchPlaceholder { get; set; }

    public string InitialItemsJson { get; set; }

    public bool SmallButton { get; set; } = true;
}

/// <summary>
/// A single pre-selected user shown when the picker first renders.
/// </summary>
public sealed class UserPickerItem
{
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    // The item selector keeps an initial item only when it is marked selected. Without this a picker opened on a
    // saved value showed nothing, and saving the form again cleared the value.
    [JsonPropertyName("selected")]
    public bool Selected { get; set; } = true;
}
