using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Deletes one cadence or subject action, as the Delete action of its admin page does, after checking the permission
/// of that kind of record. These are the records the generic <c>deleteRecord</c> tool cannot delete: cadences are a
/// read-only record kind, because their editor posts the steps as a list of objects the editor replay cannot post, and
/// subject actions are created for a subject and an action type the record tools cannot pass. Dispositions, lead
/// statuses and opportunity stages are deleted with <c>deleteRecord</c>.
/// </summary>
public sealed class DeleteOmnichannelRecordTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteOmnichannelRecord";

    private const string _cadence = "cadence";
    private const string _subjectAction = "subjectAction";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "kind": {
          "type": "string",
          "enum": ["cadence", "subjectAction"],
          "description": "The kind of record to delete. Dispositions, lead statuses and opportunity stages are deleted with deleteRecord."
        },
        "id": {
          "type": "string",
          "description": "The ID of the record: a cadence from listRecords (kind omnichannelCadence), a subject action from listSubjectFlows."
        }
      },
      "required": ["kind", "id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes one omnichannel cadence or subject flow action by kind (cadence, subjectAction) and id. Dispositions, lead statuses and opportunity stages are deleted with deleteRecord; campaigns, campaign groups and addresses cannot be deleted. This cannot be undone.";

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
        var kind = Normalize(AIModuleTools.ReadString(arguments, "kind"));
        var permission = GetPermission(kind);

        if (permission is null)
        {
            // Nothing is read before the caller's permission is known, so an unknown kind is refused the same way.
            return AIModuleTools.GetCaller(services) is null
                ? AIToolCallerAuthorization.NotAllowedMessage
                : "Nothing was deleted: kind must be cadence or subjectAction. Dispositions, lead statuses and opportunity stages are deleted with deleteRecord; campaigns, campaign groups and addresses cannot be deleted.";
        }

        if (!await AIModuleTools.IsAuthorizedAsync(services, permission))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was deleted: give the ID of the record in id.";
        }

        return kind == _cadence
            ? await DeleteCadenceAsync(services.GetRequiredService<ICatalogManager<Cadence>>(), id, cancellationToken)
            : await DeleteSubjectActionAsync(services.GetRequiredService<ISourceCatalogManager<SubjectAction>>(), id, cancellationToken);
    }

    private static async Task<string> DeleteCadenceAsync(ICatalogManager<Cadence> manager, string id, CancellationToken cancellationToken)
    {
        var cadence = await manager.FindByIdAsync(id, cancellationToken);

        if (cadence is null)
        {
            return $"Nothing was deleted: no cadence has the ID '{id}'.";
        }

        if (!await manager.DeleteAsync(cadence, cancellationToken))
        {
            return $"The cadence '{cadence.DisplayText}' could not be deleted.";
        }

        return AIModuleTools.Json(new
        {
            deleted = new
            {
                kind = "cadence",
                id,
                name = cadence.DisplayText,
            },
        });
    }

    private static async Task<string> DeleteSubjectActionAsync(ISourceCatalogManager<SubjectAction> manager, string id, CancellationToken cancellationToken)
    {
        var action = await manager.FindByIdAsync(id, cancellationToken);

        if (action is null)
        {
            return $"Nothing was deleted: no subject action has the ID '{id}'.";
        }

        if (!await manager.DeleteAsync(action, cancellationToken))
        {
            return $"Unable to remove the subject action '{action.DisplayText}'.";
        }

        return AIModuleTools.Json(new
        {
            deleted = new
            {
                kind = "subject action",
                id,
                name = action.DisplayText,
                subjectContentType = action.SubjectContentType,
            },
        });
    }

    private static Permission GetPermission(string kind)
        => kind switch
        {
            _cadence => OmnichannelConstants.Permissions.ManageCadences,
            _subjectAction => OmnichannelConstants.Permissions.ManageSubjectFlows,
            _ => null,
        };

    private static string Normalize(string kind)
        => kind is null
            ? null
            : new[] { _cadence, _subjectAction }
                .FirstOrDefault(known => string.Equals(known, kind, StringComparison.OrdinalIgnoreCase));
}
