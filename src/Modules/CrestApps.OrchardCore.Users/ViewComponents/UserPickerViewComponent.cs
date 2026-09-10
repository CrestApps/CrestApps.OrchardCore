using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.ViewComponents;

/// <summary>
/// A reusable user picker: a searchable selector, backed by the shared user-search endpoint, that any screen can
/// drop in to let an operator search for and select one or more users. It resolves the currently-selected users
/// so their display names show when the picker first renders, and delegates the search-and-select UI to the
/// shared <c>ItemSelector</c> component (top matches with live search).
/// </summary>
public sealed class UserPickerViewComponent : ViewComponent
{
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;

    public UserPickerViewComponent(UserManager<IUser> userManager, IDisplayNameProvider displayNameProvider)
    {
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
    }

    /// <summary>
    /// Renders the user picker.
    /// </summary>
    /// <param name="name">The form field name the selected value(s) post under.</param>
    /// <param name="selectedValues">The currently-selected values (of the kind named by <paramref name="valueType"/>).</param>
    /// <param name="valueType">What the picker stores and posts: <c>userId</c> (default), <c>userName</c>, or <c>normalizedUserName</c>.</param>
    /// <param name="multiple">Whether more than one user can be selected.</param>
    /// <param name="roles">When set, restricts the searchable users to these roles.</param>
    /// <param name="label">Optional label rendered above the picker.</param>
    /// <param name="buttonText">Optional toggle-button text.</param>
    /// <param name="searchPlaceholder">Optional search-box placeholder.</param>
    public async Task<IViewComponentResult> InvokeAsync(
        string name,
        IEnumerable<string> selectedValues = null,
        string valueType = "userId",
        bool multiple = false,
        string[] roles = null,
        string label = null,
        string buttonText = null,
        string searchPlaceholder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var values = (selectedValues ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var initialItems = new List<UserPickerItem>(values.Length);

        foreach (var value in values)
        {
            var user = await ResolveUserAsync(value, valueType);

            initialItems.Add(new UserPickerItem
            {
                Value = value,
                Text = user is null ? value : await _displayNameProvider.GetAsync(user, HttpContext.RequestAborted),
            });
        }

        var model = new UserPickerViewModel
        {
            Id = $"user-picker-{name.Replace('.', '-').Replace('[', '-').Replace(']', '-')}",
            InputName = name,
            ValueType = string.IsNullOrWhiteSpace(valueType) ? "userId" : valueType,
            Roles = roles ?? [],
            Multiple = multiple,
            Label = label,
            ButtonText = buttonText,
            SearchPlaceholder = searchPlaceholder,
            InitialItemsJson = JsonSerializer.Serialize(initialItems),
        };

        return View(model);
    }

    private async Task<IUser> ResolveUserAsync(string value, string valueType)
    {
        return (valueType?.Trim().ToLowerInvariant()) switch
        {
            "username" or "normalizedusername" => await _userManager.FindByNameAsync(value),
            _ => await _userManager.FindByIdAsync(value),
        };
    }
}
