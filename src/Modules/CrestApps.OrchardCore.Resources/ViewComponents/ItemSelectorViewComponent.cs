using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Resources.ViewComponents;

/// <summary>
/// Renders a reusable searchable item selector.
/// </summary>
public sealed class ItemSelectorViewComponent : ViewComponent
{
    /// <summary>
    /// Renders the item selector with the provided configuration.
    /// </summary>
    /// <param name="id">The unique HTML identifier for the selector root.</param>
    /// <param name="inputName">The form field name used for the selected value or values.</param>
    /// <param name="endpoint">The search endpoint used to fetch items.</param>
    /// <param name="initialItemsJson">The JSON payload representing the initially selected items.</param>
    /// <param name="multiple">Whether multiple selection is enabled.</param>
    /// <param name="enableSearch">Whether the search box is shown.</param>
    /// <param name="enableSelectAll">Whether the select-all action is shown.</param>
    /// <param name="enableDeselectAll">Whether the deselect-all action is shown.</param>
    /// <param name="buttonText">The text shown on the toggle button.</param>
    /// <param name="searchPlaceholder">The search box placeholder text.</param>
    /// <param name="emptyResultsText">The text displayed when no items are available.</param>
    /// <param name="noSelectionText">The text displayed when no items are selected.</param>
    /// <param name="loadingText">The text displayed while loading items.</param>
    /// <param name="loadErrorText">The text displayed when items cannot be loaded.</param>
    /// <param name="resultsTextFormat">The status text format shown after a search, where {0} is replaced with the result count.</param>
    /// <param name="selectedItemsLabel">The label shown above the selected items list.</param>
    /// <param name="availableItemsLabel">The label shown above the available items list.</param>
    /// <param name="selectAllText">The label shown for the select-all action.</param>
    /// <param name="deselectAllText">The label shown for the deselect-all action.</param>
    /// <param name="searchButtonText">The label shown for the search button.</param>
    /// <param name="enableSearchButton">Whether a dedicated search button is shown next to the search input.</param>
    /// <param name="showSelectedItems">Whether the selected items list is shown below the toggle button.</param>
    /// <param name="closeOnSelect">Whether the dropdown closes after selecting an item.</param>
    /// <param name="searchDelay">The debounce delay, in milliseconds, used for search-as-you-type.</param>
    /// <returns>The rendered selector view.</returns>
    public IViewComponentResult Invoke(
        string id,
        string inputName,
        string endpoint,
        string initialItemsJson = "[]",
        bool multiple = false,
        bool enableSearch = true,
        bool enableSelectAll = false,
        bool enableDeselectAll = true,
        string buttonText = null,
        string searchPlaceholder = null,
        string emptyResultsText = null,
        string noSelectionText = null,
        string loadingText = null,
        string loadErrorText = null,
        string resultsTextFormat = null,
        string selectedItemsLabel = null,
        string availableItemsLabel = null,
        string selectAllText = null,
        string deselectAllText = null,
        string searchButtonText = null,
        bool enableSearchButton = true,
        bool showSelectedItems = true,
        bool closeOnSelect = false,
        int searchDelay = 300)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(inputName);
        ArgumentException.ThrowIfNullOrEmpty(endpoint);

        if (string.IsNullOrWhiteSpace(initialItemsJson))
        {
            initialItemsJson = "[]";
        }

        var configurationJson = JsonSerializer.Serialize(new
        {
            id,
            inputName,
            endpoint,
            multiple,
            enableSearch,
            enableSelectAll,
            enableDeselectAll,
            buttonText = buttonText ?? (multiple ? "Select items" : "Select item"),
            searchPlaceholder = searchPlaceholder ?? "Search items",
            emptyResultsText = emptyResultsText ?? "No items found.",
            noSelectionText = noSelectionText ?? "No items selected.",
            loadingText = loadingText ?? "Loading items...",
            loadErrorText = loadErrorText ?? "Unable to load items.",
            resultsTextFormat = resultsTextFormat ?? "Found {0} item(s).",
            selectedItemsLabel = selectedItemsLabel ?? "Selected items",
            availableItemsLabel = availableItemsLabel ?? "Available items",
            selectAllText = selectAllText ?? "Select all",
            deselectAllText = deselectAllText ?? "Deselect all",
            searchButtonText = searchButtonText ?? "Search",
            enableSearchButton,
            showSelectedItems,
            closeOnSelect = closeOnSelect || !multiple,
            searchDelay = searchDelay < 0 ? 0 : searchDelay,
            initialItems = JsonSerializer.Deserialize<JsonElement>(initialItemsJson),
        });

        return View(new ItemSelectorViewModel
        {
            Id = id,
            ConfigurationJson = configurationJson,
        });
    }
}
