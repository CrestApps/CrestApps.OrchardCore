namespace CrestApps.OrchardCore.Resources.ViewComponents;

/// <summary>
/// The model of the searchable selector the <see cref="ItemSelectorViewComponent"/> renders on the CrestApps
/// bootstrap-select fork.
/// </summary>
public sealed class ItemSelectorViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the select element.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name the selected values are posted under.
    /// </summary>
    public string InputName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether several items can be selected.
    /// </summary>
    public bool Multiple { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the select-all and deselect-all buttons are shown.
    /// </summary>
    public bool ActionsBox { get; set; }

    /// <summary>
    /// Gets or sets the configuration the script reads: the search endpoint and the search delay.
    /// </summary>
    public string ConfigurationJson { get; set; }

    /// <summary>
    /// Gets or sets the saved items, rendered as the select's options.
    /// </summary>
    public IReadOnlyList<ItemSelectorOption> InitialItems { get; set; } = [];

    /// <summary>
    /// Gets or sets the text shown while nothing is selected.
    /// </summary>
    public string ButtonText { get; set; }

    /// <summary>
    /// Gets or sets the placeholder of the search box.
    /// </summary>
    public string SearchPlaceholder { get; set; }

    /// <summary>
    /// Gets or sets the text shown when the search finds nothing.
    /// </summary>
    public string EmptyResultsText { get; set; }

    /// <summary>
    /// Gets or sets the header of the menu.
    /// </summary>
    public string MenuHeader { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the selector is drawn small, for use in toolbars.
    /// </summary>
    public bool SmallButton { get; set; } = true;
}

/// <summary>
/// An item a selector starts with.
/// </summary>
public sealed class ItemSelectorOption
{
    /// <summary>
    /// Gets or sets the value posted when the item is selected.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the text shown for the item.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the secondary text shown beside the item.
    /// </summary>
    public string SecondaryText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item is selected.
    /// </summary>
    public bool Selected { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item can be selected.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
