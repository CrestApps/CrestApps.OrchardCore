using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Deletes a certificate through <see cref="ICertificationRecordService.DeleteAsync"/>, as the Certificates page's Delete
/// does. The copy in the media library is not deleted. A staff member must be among the people the caller manages. It
/// needs <see cref="SkillsPermissions.AssignSkills"/> and asks for approval.
/// </summary>
internal sealed class DeleteCertificateTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteCertificate";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "certificateId": {
          "type": "string",
          "description": "The id of the certificate to delete, from listCertificates."
        }
      },
      "required": ["certificateId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a certificate record. Use revoke instead to keep the history.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.AssignSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var certificateId = ReadString(arguments, "certificateId");

        if (certificateId is null)
        {
            return "Nothing was deleted: give the certificateId, from listCertificates.";
        }

        var recordService = services.GetRequiredService<ICertificationRecordService>();
        var record = await recordService.FindByIdAsync(certificateId, cancellationToken);

        if (record is null)
        {
            return $"Nothing was deleted: there is no certificate with the id '{certificateId}'. Use listCertificates to find it.";
        }

        if (!await CanChangeAsync(services, caller, record, cancellationToken))
        {
            return $"Nothing was deleted: you are not allowed to change the certificates of {record.SubjectName ?? record.SubjectId}.";
        }

        var deleted = await recordService.DeleteAsync(certificateId, cancellationToken);

        return Json(new
        {
            id = certificateId,
            subjectName = record.SubjectName,
            deleted,
            message = deleted ? "The certificate has been deleted." : "The certificate could not be deleted.",
        });
    }
}
