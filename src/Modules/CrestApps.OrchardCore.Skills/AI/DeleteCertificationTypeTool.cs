using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Deletes a certification type through the certification manager, as the Certification Types page's Delete does. Its
/// certificates are kept: the service does not remove them. It needs
/// <see cref="SkillsPermissions.ManageSkills"/> and asks for approval.
/// </summary>
internal sealed class DeleteCertificationTypeTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteCertificationType";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "certificationId": {
          "type": "string",
          "description": "The id of the certification type to delete, from listCertificationTypes."
        }
      },
      "required": ["certificationId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a certification type. The certificates of this type are kept. Disable it instead when certificates of this type exist.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.ManageSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var certificationId = ReadString(arguments, "certificationId");

        if (certificationId is null)
        {
            return "Nothing was deleted: give the certificationId, from listCertificationTypes.";
        }

        var manager = services.GetRequiredService<ICertificationManager>();
        var certification = await manager.FindByIdAsync(certificationId, cancellationToken);

        if (certification is null)
        {
            return $"Nothing was deleted: there is no certification type with the id '{certificationId}'. Use listCertificationTypes to find it.";
        }

        var deleted = await manager.DeleteAsync(certification, cancellationToken);

        return Json(new
        {
            id = certification.ItemId,
            name = certification.Name,
            deleted,
            message = deleted
                ? $"The certification type '{certification.Name}' has been deleted. The certificates of this type are kept."
                : $"The certification type '{certification.Name}' could not be deleted.",
        });
    }
}
