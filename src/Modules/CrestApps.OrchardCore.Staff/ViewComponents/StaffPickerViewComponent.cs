using System.Text.Json;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Staff.ViewComponents;

/// <summary>
/// Picks staff members. Other modules invoke it by name (<c>StaffPicker</c>) from their views; it searches active staff
/// through the <c>Admin/api/staff/search</c> endpoint, optionally only those with a capability. The value is the staff
/// member id, or the user id with <c>valueType = "userId"</c> for fields that store users.
/// </summary>
public sealed class StaffPickerViewComponent : ViewComponent
{
    private readonly IStaffDirectory _directory;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffPickerViewComponent"/> class.
    /// </summary>
    /// <param name="directory">The staff directory.</param>
    public StaffPickerViewComponent(IStaffDirectory directory)
    {
        _directory = directory;
    }

    /// <summary>
    /// Renders the picker.
    /// </summary>
    /// <param name="name">The input name.</param>
    /// <param name="selectedValues">The values chosen now.</param>
    /// <param name="capability">A capability the people offered must have.</param>
    /// <param name="valueType"><c>staffId</c> (the default) or <c>userId</c>.</param>
    /// <param name="multiple">Whether several people can be chosen.</param>
    /// <param name="excludeId">A staff member not to offer, such as the person being edited.</param>
    /// <param name="buttonText">The button text.</param>
    /// <param name="smallButton">Whether the button is small.</param>
    /// <param name="required">Whether a choice is required.</param>
    /// <returns>The view.</returns>
    public async Task<IViewComponentResult> InvokeAsync(
        string name,
        IEnumerable<string> selectedValues = null,
        string capability = null,
        string valueType = "staffId",
        bool multiple = false,
        string excludeId = null,
        string buttonText = null,
        bool smallButton = false,
        bool required = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var values = (selectedValues ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
        var byUser = string.Equals(valueType, "userId", StringComparison.OrdinalIgnoreCase);
        var names = byUser
            ? await _directory.GetNamesByUserIdAsync(values, HttpContext.RequestAborted)
            : await _directory.GetNamesAsync(values, HttpContext.RequestAborted);

        var initial = values.Select(value => new { value, text = names.GetValueOrDefault(value, value) }).ToArray();

        return View(new StaffPickerViewModel
        {
            Id = $"staff-picker-{name.Replace('.', '-').Replace('[', '-').Replace(']', '-')}",
            InputName = name,
            Capability = capability,
            ValueType = byUser ? "userId" : "staffId",
            Multiple = multiple,
            ExcludeId = excludeId,
            ButtonText = buttonText,
            SmallButton = smallButton,
            Required = required,
            InitialItemsJson = JsonSerializer.Serialize(initial),
        });
    }
}
