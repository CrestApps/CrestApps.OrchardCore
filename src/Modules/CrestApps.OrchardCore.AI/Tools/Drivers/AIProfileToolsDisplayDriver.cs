using CrestApps.OrchardCore.AI.Core;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Display driver that presents the tool selection UI on AI profiles,
/// allowing administrators to choose which AI tools are available for a profile.
/// </summary>
internal sealed class AIProfileToolsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IAIToolAccessEvaluator _toolAccessEvaluator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIProfileToolExposureValidator _exposureValidator;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileToolsDisplayDriver"/> class.
    /// </summary>
    /// <param name="toolDefinitions">The options containing registered AI tool definitions.</param>
    /// <param name="toolAccessEvaluator">The evaluator for checking tool access permissions.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor for retrieving the current user.</param>
    /// <param name="exposureValidator">Finds tools that people who can use the profile may not use.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public AIProfileToolsDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IHttpContextAccessor httpContextAccessor,
        AIProfileToolExposureValidator exposureValidator,
        IStringLocalizer<AIProfileToolsDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        _toolAccessEvaluator = toolAccessEvaluator;
        _httpContextAccessor = httpContextAccessor;
        _exposureValidator = exposureValidator;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfile profile, BuildEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        // Filter tools based on user permissions
        var user = _httpContextAccessor.HttpContext.User;
        var accessibleTools = new Dictionary<string, AIToolDefinitionEntry>();

        foreach (var tool in _toolDefinitions.GetSelectableTools())
        {
            // Check if user has access to this tool
            if (await _toolAccessEvaluator.IsAuthorizedAsync(user, tool.Key))
            {
                accessibleTools[tool.Key] = tool.Value;
            }
        }

        if (accessibleTools.Count == 0)
        {
            return null;
        }

        return Initialize<EditProfileToolsViewModel>("EditProfileTools_Edit", model =>
        {
            var selectedNames = GetSelectedToolNames(profile);

            model.Tools = accessibleTools
            .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
            {
                ItemId = entry.Key,
                DisplayText = entry.Value.Title,
                Description = entry.Value.Description,
                IsSelected = selectedNames?.Contains(entry.Key) ?? false,
            }).OrderBy(entry => entry.DisplayText).ToArray());
        }).Location("Content:7#Capabilities;8");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        if (_toolDefinitions.Tools.Count == 0)
        {
            return null;
        }

        var model = new EditProfileToolsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedToolKeys = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId) ?? [];

        // The editor shows only the tools the user may use, but a request can post any key: the same rule applies on
        // save, and tools already on the profile that the user can't see are kept.
        var metadata = new FunctionInvocationMetadata
        {
            Names = await AIToolSelectionFilter.FilterAsync(
                selectedToolKeys,
                GetSelectedToolNames(profile),
                _toolDefinitions.GetSelectableTools().Keys,
                tool => _toolAccessEvaluator.IsAuthorizedAsync(_httpContextAccessor.HttpContext?.User, tool)),
        };

        // SEC-A3: each tool checks its permission against the caller, so a profile that people without it can use
        // would hand them a tool that only refuses; one reachable by visitors who aren't signed in is the worst case.
        foreach (var issue in await _exposureValidator.ValidateAsync(profile, metadata.Names))
        {
            context.Updater.ModelState.AddModelError(Prefix, string.Equals(issue.Role, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase)
                ? S["Visitors who aren't signed in can use this profile, so it can't have the '{0}' tool, which needs the '{1}' permission.", issue.ToolName, issue.Permission]
                : S["The '{0}' role can use this profile but doesn't have the '{1}' permission the '{2}' tool needs. Remove the tool, grant the role the permission, or stop the role from using the profile.", issue.Role, issue.Permission, issue.ToolName]);
        }

        profile.Put(metadata);

        // Remove the legacy property key if it exists to complete migration.
        profile.Properties.Remove("AIProfileFunctionInvocationMetadata");

        return await EditAsync(profile, context);
    }

    /// <summary>
    /// Reads tool names from the current key, falling back to the legacy key for backward compatibility.
    /// </summary>
    private static string[] GetSelectedToolNames(AIProfile profile)
    {
        var metadata = profile.GetOrCreate<FunctionInvocationMetadata>();

        if (metadata.Names is { Length: > 0 })
        {
            return metadata.Names;
        }

        // Fall back to the legacy property key used in earlier versions.
        var legacyMetadata = profile.Get<FunctionInvocationMetadata>("AIProfileFunctionInvocationMetadata");

        if (legacyMetadata?.Names is { Length: > 0 })
        {
            return legacyMetadata.Names;
        }

        return null;
    }
}
