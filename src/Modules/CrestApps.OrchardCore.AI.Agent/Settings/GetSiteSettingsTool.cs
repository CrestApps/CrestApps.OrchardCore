using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.Settings;

/// <summary>
/// Reads one site settings page the way the Settings admin page opens it: the same permission check with the group
/// ID, and the editor built by the site display drivers, so each driver applies its own permission and shows the
/// values it shows on the page. Secrets are never returned.
/// </summary>
public sealed class GetSiteSettingsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getSiteSettings";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "groupId": {
          "type": "string",
          "description": "The ID of the settings group, from listSiteSettingsGroups, such as 'general', 'admin' or 'email'."
        }
      },
      "required": [
        "groupId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the current values of one settings group and a JSON schema of its editable fields, by section. Secrets are never returned, only whether a value is set and the secret name.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("groupId", out var groupId) || string.IsNullOrWhiteSpace(groupId))
        {
            return "Nothing was read: give the groupId of a settings page. Use listSiteSettingsGroups to find it.";
        }

        groupId = groupId.Trim();

        // As the settings page does: the permission to manage this group, with the group ID as the resource.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, SettingsPermissions.ManageGroupSettings, groupId))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (await SiteSettingsEditor.IsCustomSettingsGroupAsync(services, groupId))
        {
            return $"The settings group '{groupId}' is a custom settings content type. These tools do not read or change custom settings; a person edits them on the settings page.";
        }

        var httpContext = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var site = await services.GetRequiredService<ISiteService>().GetSiteSettingsAsync();
        var editor = await SiteSettingsEditor.BuildAsync(services, httpContext, site, groupId);

        if (editor is null || editor.Sections.Count == 0)
        {
            return $"No settings page '{groupId}' was found that you can manage. Use listSiteSettingsGroups to see the settings pages you can open.";
        }

        return AgentToolResults.Json(new
        {
            groupId,
            sections = editor.Sections
                .Select(section => new
                {
                    name = section.Name,
                    values = SiteSettingsEditor.DescribeValues(section),
                    schema = SiteSettingsEditor.BuildSchema(section),
                })
                .ToList(),
            canUpdate = editor.Unsupported.Count == 0,
            cannotUpdateBecause = editor.Unsupported.Count == 0
                ? null
                : editor.Unsupported,
            savingReloadsSite = editor.ReloadsTenant ? true : (bool?)null,
            link = AgentToolLinks.GetAdminPath(services, "OrchardCore.Settings", "Admin", "Index", new() { ["groupId"] = groupId }),
            note = "To change fields, call updateSiteSettings with changes keyed by section name and then field name, in the shape of values. Secrets are never shown or accepted; a person enters them on the settings page.",
        });
    }
}
