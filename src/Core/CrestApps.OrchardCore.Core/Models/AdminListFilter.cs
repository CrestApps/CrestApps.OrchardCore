using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Core.Models;

/// <summary>
/// One filter of an admin list's toolbar: a select that submits the list's filter form when it changes.
/// </summary>
public sealed class AdminListFilter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdminListFilter"/> class.
    /// </summary>
    /// <param name="name">The name the select posts its value with, such as <c>Options.Status</c>.</param>
    /// <param name="items">The choices, with the current one selected.</param>
    /// <param name="header">The header of the select's menu, such as <c>Filter by status</c>.</param>
    /// <param name="label">The select's accessible label, such as <c>Status</c>.</param>
    public AdminListFilter(string name, IEnumerable<SelectListItem> items, string header, string label)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
        Items = items ?? [];
        Header = header;
        Label = label;
    }

    /// <summary>
    /// Gets the name the select posts its value with.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the choices.
    /// </summary>
    public IEnumerable<SelectListItem> Items { get; }

    /// <summary>
    /// Gets the header of the select's menu.
    /// </summary>
    public string Header { get; }

    /// <summary>
    /// Gets the select's accessible label.
    /// </summary>
    public string Label { get; }
}
