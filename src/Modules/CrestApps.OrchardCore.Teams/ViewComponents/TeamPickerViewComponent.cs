using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Teams.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Teams.ViewComponents;

/// <summary>
/// Picks teams. Other modules invoke it by name (<c>TeamPicker</c>) from their views; it lists the enabled teams as a
/// tree, and the value is the team id.
/// </summary>
public sealed class TeamPickerViewComponent : ViewComponent
{
    private readonly ITeamManager _teamManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamPickerViewComponent"/> class.
    /// </summary>
    /// <param name="teamManager">The team manager.</param>
    public TeamPickerViewComponent(ITeamManager teamManager)
    {
        _teamManager = teamManager;
    }

    /// <summary>
    /// Renders the picker.
    /// </summary>
    /// <param name="name">The input name.</param>
    /// <param name="selectedValues">The team ids chosen now.</param>
    /// <param name="multiple">Whether several teams can be chosen.</param>
    /// <param name="required">Whether a choice is required.</param>
    /// <param name="buttonText">The text shown while nothing is chosen.</param>
    /// <param name="smallButton">Whether the button is small, for a toolbar.</param>
    /// <param name="excludeId">A team not to offer, such as the team being edited.</param>
    /// <returns>The view.</returns>
    public async Task<IViewComponentResult> InvokeAsync(
        string name,
        IEnumerable<string> selectedValues = null,
        bool multiple = false,
        bool required = false,
        string buttonText = null,
        bool smallButton = false,
        string excludeId = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var selected = new HashSet<string>((selectedValues ?? []).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);

        // A chosen team stays listed after it is disabled, so saving the form doesn't drop it silently.
        var options = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Where(node => node.Team.ItemId != excludeId && (node.Team.Enabled || selected.Contains(node.Team.ItemId)))
            .Select(node => new TeamPickerOption
            {
                Value = node.Team.ItemId,
                Text = node.Team.Name,
                Depth = node.Depth,
                Selected = selected.Contains(node.Team.ItemId),
            })
            .ToList();

        return View(new TeamPickerViewModel
        {
            Id = $"team-picker-{name.Replace('.', '-').Replace('[', '-').Replace(']', '-')}",
            InputName = name,
            Multiple = multiple,
            Required = required,
            ButtonText = buttonText,
            SmallButton = smallButton,
            Options = options,
        });
    }
}
