using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// Creates a custom capability, or changes one, through <see cref="IStaffCapabilityService.SaveAsync"/>, as Staff &gt;
/// Capabilities does: the service checks the key and the name, and a capability's key never changes. A change starts from
/// the capability's current values, so only the fields given change; a new one is enabled at position 100, as the Create
/// page fills it. It needs <see cref="StaffPermissions.ManageStaff"/>.
/// </summary>
internal sealed class SaveStaffCapabilityTool : StaffManagementAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveStaffCapability";

    private const int DefaultPosition = 100;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The capability's key, such as Dispatcher: letters, digits, dots, dashes and underscores, starting with a letter. It is stored on staff members and never changes once saved."
        },
        "isNew": {
          "type": "boolean",
          "description": "true to add a new custom capability; false or left out to change an existing one."
        },
        "displayName": {
          "type": "string",
          "description": "The label shown in the editor, filters and pickers. Required for a new capability."
        },
        "description": {
          "type": "string",
          "description": "What the capability means. An empty text removes it."
        },
        "position": {
          "type": "integer",
          "description": "The order in lists; lower comes first."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the capability is offered. Staff members who have it keep it while it is off."
        }
      },
      "required": ["name"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a custom capability or changes the display name, description, position and enabled flag of any capability. Only the fields given change.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
        => HasAsync(services, caller, StaffPermissions.ManageStaff);

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var name = ReadString(arguments, "name");

        if (name is null)
        {
            return "Nothing was saved: give the capability's name (its key).";
        }

        var capabilities = services.GetRequiredService<IStaffCapabilityService>();
        var scope = services.GetService<StaffCapabilityToolScope>();
        var isNew = ReadBool(arguments, "isNew") ?? false;

        if (scope?.Changed.Contains(name) == true)
        {
            return $"Nothing was saved: the capability '{name}' was changed earlier in this message. Change it again in the next reply.";
        }

        StaffCapabilityDefinition definition;

        if (isNew)
        {
            definition = new StaffCapabilityDefinition
            {
                Name = name,
                DisplayName = ReadString(arguments, "displayName"),
                Description = ReadString(arguments, "description"),
                Position = ReadInt(arguments, "position") ?? DefaultPosition,
                Enabled = ReadBool(arguments, "enabled") ?? true,
            };
        }
        else
        {
            var existing = await capabilities.FindAsync(name, cancellationToken);

            if (existing is null)
            {
                return $"Nothing was saved: there is no capability '{name}'. Use listStaffCapabilities to find it, or set isNew to true to add it.";
            }

            definition = new StaffCapabilityDefinition
            {
                Name = existing.Name,
                DisplayName = Has(arguments, "displayName") ? ReadString(arguments, "displayName") : existing.DisplayName,
                Description = Has(arguments, "description") ? ReadString(arguments, "description") : existing.Description,
                Position = ReadInt(arguments, "position") ?? existing.Position,
                Enabled = ReadBool(arguments, "enabled") ?? existing.Enabled,
            };
        }

        var errors = await capabilities.SaveAsync(definition, isNew, cancellationToken);

        if (errors.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", errors)}";
        }

        scope?.Record(definition.Name);

        return Json(new
        {
            name = definition.Name.Trim(),
            displayName = definition.DisplayName?.Trim(),
            description = definition.Description,
            position = definition.Position,
            enabled = definition.Enabled,
            created = isNew,
            message = isNew ? $"The capability '{definition.DisplayName?.Trim()}' has been added." : $"The capability '{definition.DisplayName?.Trim()}' has been saved.",
            note = "The change can be seen, and the capability given to staff, from the next reply.",
        });
    }
}
