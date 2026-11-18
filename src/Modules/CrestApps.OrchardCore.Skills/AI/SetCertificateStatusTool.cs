using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Verifies or revokes a certificate through <see cref="ICertificationRecordService.VerifyAsync"/> and
/// <see cref="ICertificationRecordService.RevokeAsync"/>, as the Certificates page's Verify and Revoke do; the reason of a
/// revoke is added to the notes. Verifying is refused when the certificate's type requires a copy and none is attached.
/// The caller is recorded as the person who verified it. A staff member must be among the people the caller manages. It
/// needs <see cref="SkillsPermissions.AssignSkills"/>.
/// </summary>
internal sealed class SetCertificateStatusTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setCertificateStatus";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "certificateId": {
          "type": "string",
          "description": "The id of the certificate, from listCertificates."
        },
        "action": {
          "type": "string",
          "enum": ["verify", "revoke"],
          "description": "verify marks the certificate as checked and valid; revoke withdraws it."
        },
        "reason": {
          "type": "string",
          "description": "Why the certificate is revoked; it is added to the certificate's notes."
        }
      },
      "required": ["certificateId", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Verifies a pending certificate or revokes one with a reason. A revoked certificate no longer satisfies the skills that require it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.AssignSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var certificateId = ReadString(arguments, "certificateId");
        var action = ReadString(arguments, "action")?.ToLowerInvariant();

        if (certificateId is null || action is not ("verify" or "revoke"))
        {
            return "Nothing was changed: give the certificateId and an action, verify or revoke.";
        }

        var recordService = services.GetRequiredService<ICertificationRecordService>();
        var record = await recordService.FindByIdAsync(certificateId, cancellationToken);

        if (record is null)
        {
            return $"Nothing was changed: there is no certificate with the id '{certificateId}'. Use listCertificates to find it.";
        }

        if (!await CanChangeAsync(services, caller, record, cancellationToken))
        {
            return $"Nothing was changed: you are not allowed to change the certificates of {record.SubjectName ?? record.SubjectId}.";
        }

        var result = action == "verify"
            ? await recordService.VerifyAsync(certificateId, caller.Identity?.Name, cancellationToken)
            : await recordService.RevokeAsync(certificateId, caller.Identity?.Name, ReadString(arguments, "reason"), cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was changed: {string.Join(" ", result.Errors)}";
        }

        var today = await GetTodayAsync(services);
        var certifications = await SkillsToolResults.GetCertificationsAsync(services, cancellationToken);
        var stored = await recordService.FindByIdAsync(certificateId, cancellationToken) ?? record;

        return Json(new
        {
            certificate = await SkillsToolResults.CertificateAsync(services, stored, certifications, null, today),
            message = action == "verify" ? "The certificate is marked as checked." : "The certificate has been withdrawn.",
        });
    }
}
