using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using CrestApps.OrchardCore.AI.Tools.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Builds the tool pickers of the WebMCP settings and the WebMCP part from the selectable tools and tool instances
/// the current user may assign, and merges what the user picked into a stored selection.
/// </summary>
internal sealed class WebMcpToolSelectionService
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IAIToolInstanceAccessor _instanceAccessor;
    private readonly IAIToolAccessEvaluator _toolAccessEvaluator;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpToolSelectionService"/> class.
    /// </summary>
    /// <param name="toolDefinitions">The registered AI tool definitions.</param>
    /// <param name="instanceAccessor">The accessor of the tool instances the current user may assign.</param>
    /// <param name="toolAccessEvaluator">The evaluator of the per-tool permission.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WebMcpToolSelectionService(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAIToolInstanceAccessor instanceAccessor,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IStringLocalizer<WebMcpToolSelectionService> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        _instanceAccessor = instanceAccessor;
        _toolAccessEvaluator = toolAccessEvaluator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the selectable tools the user may use, by tool key.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The tools.</returns>
    public async Task<Dictionary<string, AIToolDefinitionEntry>> GetAccessibleToolsAsync(ClaimsPrincipal user)
    {
        var accessibleTools = new Dictionary<string, AIToolDefinitionEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in _toolDefinitions.GetSelectableTools())
        {
            if (await _toolAccessEvaluator.IsAuthorizedAsync(user, tool.Key))
            {
                accessibleTools[tool.Key] = tool.Value;
            }
        }

        return accessibleTools;
    }

    /// <summary>
    /// Gets the tool instances the current user may assign.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool instances.</returns>
    public Task<IList<AIToolInstance>> GetAccessibleInstancesAsync(CancellationToken cancellationToken = default)
        => _instanceAccessor.GetAccessibleInstancesAsync(cancellationToken);

    /// <summary>
    /// Fills the tool picker with the accessible tools, grouped by category, and marks the selected ones.
    /// </summary>
    /// <param name="model">The tool picker.</param>
    /// <param name="accessibleTools">The tools the user may use.</param>
    /// <param name="selected">The selected tool names.</param>
    public void PopulateTools(EditProfileToolsViewModel model, Dictionary<string, AIToolDefinitionEntry> accessibleTools, IEnumerable<string> selected)
    {
        var selectedNames = new HashSet<string>(selected ?? [], StringComparer.OrdinalIgnoreCase);

        model.Tools = accessibleTools
            .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
            {
                ItemId = entry.Key,
                DisplayText = entry.Value.Title,
                Description = entry.Value.Description,
                IsSelected = selectedNames.Contains(entry.Key),
            }).OrderBy(entry => entry.DisplayText).ToArray());
    }

    /// <summary>
    /// Fills the tool instance picker with the accessible instances and marks the selected ones.
    /// </summary>
    /// <param name="model">The tool instance picker.</param>
    /// <param name="accessibleInstances">The instances the user may assign.</param>
    /// <param name="selected">The selected tool names.</param>
    public static void PopulateInstances(EditToolInstancesViewModel model, IEnumerable<AIToolInstance> accessibleInstances, IEnumerable<string> selected)
    {
        var selectedNames = new HashSet<string>(selected ?? [], StringComparer.OrdinalIgnoreCase);

        model.Instances = accessibleInstances
            .Select(instance => new ToolInstanceEntry
            {
                Name = instance.Name,
                Description = instance.Description,
                IsSelected = selectedNames.Contains(instance.Name),
            })
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Merges what the user picked into the stored selection. A user only changes the tools they may assign; the
    /// tools they may not see stay as they were.
    /// </summary>
    /// <param name="current">The stored selection.</param>
    /// <param name="accessibleToolKeys">The keys of the tools the user may use.</param>
    /// <param name="accessibleInstanceNames">The names of the tool instances the user may assign.</param>
    /// <param name="toolsModel">The posted tool picker.</param>
    /// <param name="instancesModel">The posted tool instance picker.</param>
    /// <returns>The new selection.</returns>
    public static List<string> Merge(
        IEnumerable<string> current,
        IEnumerable<string> accessibleToolKeys,
        IEnumerable<string> accessibleInstanceNames,
        EditProfileToolsViewModel toolsModel,
        EditToolInstancesViewModel instancesModel)
    {
        var accessibleTools = new HashSet<string>(accessibleToolKeys ?? [], StringComparer.OrdinalIgnoreCase);
        var accessibleInstances = new HashSet<string>(accessibleInstanceNames ?? [], StringComparer.OrdinalIgnoreCase);

        var selectedToolKeys = toolsModel?.Tools?.Values?
            .SelectMany(entries => entries ?? [])
            .Where(entry => entry.IsSelected)
            .Select(entry => entry.ItemId) ?? [];

        var selectedInstanceNames = instancesModel?.Instances?
            .Where(entry => entry.IsSelected)
            .Select(entry => entry.Name) ?? [];

        var tools = (current ?? [])
            .Where(name => !accessibleTools.Contains(name) && !accessibleInstances.Contains(name))
            .ToList();

        tools.AddRange(accessibleTools.Intersect(selectedToolKeys, StringComparer.OrdinalIgnoreCase));
        tools.AddRange(accessibleInstances.Intersect(selectedInstanceNames, StringComparer.OrdinalIgnoreCase));

        return tools
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
