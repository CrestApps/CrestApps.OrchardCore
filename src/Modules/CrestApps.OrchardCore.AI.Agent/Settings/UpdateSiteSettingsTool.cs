using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.Settings;

/// <summary>
/// Changes fields of one site settings page the way the Settings admin page saves it: the same permission check with
/// the group ID, then the site display drivers bind and validate the posted values, apply their own permission,
/// protect credentials, run their side effects and ask for the tenant reload. Nothing is saved when a driver reports
/// an error, and the errors are returned as the drivers wrote them.
/// </summary>
public sealed class UpdateSiteSettingsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "updateSiteSettings";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "groupId": {
          "type": "string",
          "description": "The ID of the settings group, from listSiteSettingsGroups, such as 'general', 'admin' or 'email'."
        },
        "changes": {
          "type": "object",
          "description": "The fields to change, keyed by section name and then by field name, in the shape getSiteSettings returns, for example { \"ISite.AdminSettings\": { \"DisplayMenuFilter\": true } }. Send only the fields to change; the other fields keep their current values. A secret value is never accepted."
        }
      },
      "required": [
        "groupId",
        "changes"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Changes fields of one settings group; send only the fields to change, in the shape returned by getSiteSettings. Returns validation errors, or the saved values and whether the site is reloading. Secrets are never accepted.";

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
            return "Nothing was saved: give the groupId of a settings page. Use listSiteSettingsGroups to find it.";
        }

        groupId = groupId.Trim();

        // As the settings page does: the permission to manage this group, with the group ID as the resource.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, SettingsPermissions.ManageGroupSettings, groupId))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadJsonObject(arguments, "changes", out var changes) || changes is null || changes.Count == 0)
        {
            return "Nothing was saved: changes must be an object keyed by section name and then by field name, as getSiteSettings returns them.";
        }

        if (await SiteSettingsEditor.IsCustomSettingsGroupAsync(services, groupId))
        {
            return $"Nothing was saved: the settings group '{groupId}' is a custom settings content type. These tools do not change custom settings; a person edits them on the settings page.";
        }

        var httpContext = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var siteService = services.GetRequiredService<ISiteService>();

        // The editor is built from the current settings, as the page shows it before a person changes it.
        var before = await SiteSettingsEditor.BuildAsync(services, httpContext, await siteService.GetSiteSettingsAsync(), groupId);

        if (before is null || before.Sections.Count == 0)
        {
            return $"Nothing was saved: no settings page '{groupId}' was found that you can manage. Use listSiteSettingsGroups to see the settings pages you can open.";
        }

        if (before.Unsupported.Count > 0)
        {
            return $"Nothing was saved: this settings page cannot be changed from the chat because {string.Join("; ", before.Unsupported)}. A person can change it on the settings page.";
        }

        var problems = SiteSettingsEditor.ValidateChanges(before, changes, out var normalized);

        if (problems.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", problems)}";
        }

        var formValues = SiteSettingsEditor.BuildFormValues(before, normalized);
        var site = await siteService.LoadSiteSettingsAsync();
        var protectedValues = ReadProtectedValues(site);
        var updater = new FormValuesModelUpdater(services, httpContext, formValues);
        var shellReleaseManager = services.GetRequiredService<IShellReleaseManager>();

        var shape = await services.GetRequiredService<IDisplayManager<ISite>>().UpdateEditorAsync(site, updater, false, groupId);
        var after = SiteSettingsEditor.Capture(services, shape);

        if (after is null)
        {
            shellReleaseManager.SuspendReleaseRequest();

            return $"Nothing was saved: no settings page '{groupId}' was found that you can manage.";
        }

        if (!updater.ModelState.IsValid)
        {
            // As the settings page does: an invalid post keeps the tenant from reloading.
            shellReleaseManager.SuspendReleaseRequest();

            return AgentToolResults.Json(new
            {
                saved = false,
                groupId,
                errors = updater.ModelState
                    .Where(entry => entry.Value.Errors.Count > 0)
                    .SelectMany(entry => entry.Value.Errors.Select(error => new
                    {
                        field = string.IsNullOrEmpty(entry.Key) ? null : entry.Key,
                        message = string.IsNullOrEmpty(error.ErrorMessage)
                            ? "The value is not valid."
                            : error.ErrorMessage,
                    }))
                    .ToList(),
                note = "Nothing was saved. Fix the fields named in the errors and try again.",
            });
        }

        if (ReadProtectedValues(site) != protectedValues)
        {
            shellReleaseManager.SuspendReleaseRequest();

            return "Nothing was saved: saving this page would change the super user, the site salt or the home route, which these tools never change.";
        }

        await siteService.UpdateSiteSettingsAsync(site);

        return AgentToolResults.Json(new
        {
            saved = true,
            groupId,
            changed = SiteSettingsEditor.GetChangedFields(before, after),
            sections = after.Sections
                .Select(section => new
                {
                    name = section.Name,
                    values = SiteSettingsEditor.DescribeValues(section),
                })
                .ToList(),
            reloadRequested = after.ReloadsTenant ? true : (bool?)null,
            note = after.ReloadsTenant
                ? "The settings are saved and the site reloads when this request ends; the chat may reconnect."
                : "The settings are saved. Some settings pages (external sign-in providers, for example) also reload the site after a save without saying so; the chat may then reconnect.",
        });
    }

    // The values a settings page never changes; a save that changes one of them is refused.
    private static string ReadProtectedValues(ISite site)
        => JsonSerializer.Serialize(new
        {
            site.SuperUser,
            site.SiteSalt,
            HomeRoute = site.HomeRoute?.OrderBy(value => value.Key, StringComparer.Ordinal).Select(value => $"{value.Key}={value.Value}").ToArray(),
        });
}
