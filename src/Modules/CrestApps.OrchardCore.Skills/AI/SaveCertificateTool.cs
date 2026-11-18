using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Media;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Records a certificate for a person, or changes one, through <see cref="ICertificationRecordService.SaveAsync"/>, as
/// the certificate editor and the staff profile's "add certificate" do; the service checks the certification type, the
/// dates, works out the expiry from the type's validity, and refuses a valid certificate without the copy its type
/// requires. A new certificate is pending until it is verified with setCertificateStatus. The copy of the certificate is
/// a path in the media library, never the file itself, and must exist there. The holder of a certificate never changes,
/// and a staff member must be among the people the caller manages. It needs <see cref="SkillsPermissions.AssignSkills"/>.
/// </summary>
internal sealed class SaveCertificateTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveCertificate";

    private static readonly string[] _fieldNames = ["certificationId", "number", "issuedOn", "expiresOn", "documentPath", "notes"];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "certificateId": {
          "type": "string",
          "description": "The id of the certificate to change, from listCertificates. Leave it out to record a new certificate, and give subjectKind and subjectId."
        },
    """ + SubjectProperties + """
        ,"certificationId": {
          "type": "string",
          "description": "The id of the certification type, from listCertificationTypes. Required for a new certificate."
        },
        "number": {
          "type": "string",
          "description": "The certificate's number. An empty text removes it."
        },
        "issuedOn": {
          "type": "string",
          "description": "The day it was issued, as yyyy-MM-dd. An empty text removes it."
        },
        "expiresOn": {
          "type": "string",
          "description": "The last day it is valid, as yyyy-MM-dd. Leave it out of a new certificate to work it out from the issue date and the type's validity."
        },
        "documentPath": {
          "type": "string",
          "description": "The path of the copy of the certificate in the media library, such as certificates/luis-forklift.pdf. Upload the file to the media library first. An empty text removes it."
        },
        "notes": {
          "type": "string",
          "description": "Notes about the certificate. An empty text removes them."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Records a certificate for a user or staff member, or updates one: certification type, number, issued and expiry dates, document path (a file already in the media library) and notes. A new certificate is pending until verified with setCertificateStatus.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.AssignSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!TryReadDate(arguments, "issuedOn", out var issuedOn) || !TryReadDate(arguments, "expiresOn", out var expiresOn))
        {
            return "Nothing was saved: issuedOn and expiresOn must be dates written as yyyy-MM-dd.";
        }

        var recordService = services.GetRequiredService<ICertificationRecordService>();
        var certificateId = ReadString(arguments, "certificateId");
        CertificationRecord record;
        string subjectName;

        if (certificateId is null)
        {
            var lookup = await FindSubjectAsync(arguments, services, caller, forChange: true, cancellationToken);

            if (lookup.Error is not null)
            {
                return $"Nothing was saved: {lookup.Error}";
            }

            if (ReadString(arguments, "certificationId") is null)
            {
                return "Nothing was saved: give the certificationId of a new certificate, from listCertificationTypes.";
            }

            record = new CertificationRecord
            {
                SubjectKind = lookup.Subject.Kind,
                SubjectId = lookup.Subject.Id,
                SubjectName = lookup.Name,
                Status = CertificationStatus.Pending,
            };
            subjectName = lookup.Name;
        }
        else
        {
            record = await recordService.FindByIdAsync(certificateId, cancellationToken);

            if (record is null)
            {
                return $"Nothing was saved: there is no certificate with the id '{certificateId}'. Use listCertificates to find it.";
            }

            if (Has(arguments, "subjectKind") || Has(arguments, "subjectId"))
            {
                return "Nothing was saved: the holder of a certificate can't change. Leave subjectKind and subjectId out, or record a new certificate for the other person.";
            }

            if (!_fieldNames.Any(name => Has(arguments, name)))
            {
                return "Nothing was changed: give at least one field to change.";
            }

            if (!await CanChangeAsync(services, caller, record, cancellationToken))
            {
                return $"Nothing was saved: you are not allowed to change the certificates of {record.SubjectName ?? record.SubjectId}.";
            }

            subjectName = record.SubjectName;
        }

        if (Has(arguments, "certificationId") && ReadString(arguments, "certificationId") is { } certificationId && certificationId != record.CertificationId)
        {
            var certification = await services.GetRequiredService<ICertificationManager>().FindByIdAsync(certificationId, cancellationToken);

            if (certification is null || !certification.Enabled)
            {
                return $"Nothing was saved: there is no enabled certification type with the id '{certificationId}'. Use listCertificationTypes to find it.";
            }

            record.CertificationId = certificationId;
        }

        if (Has(arguments, "documentPath"))
        {
            var documentPath = ReadString(arguments, "documentPath");

            if (documentPath is not null)
            {
                var checkedPath = await CheckMediaPathAsync(services, documentPath);

                if (checkedPath.Error is not null)
                {
                    return $"Nothing was saved: {checkedPath.Error}";
                }

                documentPath = checkedPath.Path;
            }

            record.DocumentPath = documentPath;
        }

        if (Has(arguments, "number"))
        {
            record.Number = ReadString(arguments, "number");
        }

        if (Has(arguments, "issuedOn"))
        {
            record.IssuedOn = issuedOn;
        }

        if (Has(arguments, "expiresOn"))
        {
            record.ExpiresOn = expiresOn;
        }

        if (Has(arguments, "notes"))
        {
            record.Notes = ReadString(arguments, "notes");
        }

        var result = await recordService.SaveAsync(record, caller.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", result.Errors)}";
        }

        var today = await GetTodayAsync(services);
        var certifications = await SkillsToolResults.GetCertificationsAsync(services, cancellationToken);

        return Json(new
        {
            certificate = await SkillsToolResults.CertificateAsync(services, record, certifications, new Dictionary<string, string> { [record.Subject.Key] = subjectName }, today),
            created = certificateId is null,
            message = certificateId is null
                ? $"The certificate has been recorded for {subjectName}. It is {record.Status.ToString().ToLowerInvariant()}; verify it with setCertificateStatus once it is checked."
                : "The certificate has been saved.",
        });
    }

    private static async Task<(string Path, string Error)> CheckMediaPathAsync(IServiceProvider services, string value)
    {
        var path = value.Replace('\\', '/').Trim().TrimStart('/');

        if (path.Contains("://", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return (null, $"'{value}' is an address, not a media library path. Upload the file to the media library and give its path, such as certificates/name.pdf.");
        }

        if (services.GetService<IMediaFileStore>() is not { } mediaFileStore)
        {
            return (null, "the media library is not enabled, so a copy of the certificate can't be attached.");
        }

        var publicPrefix = mediaFileStore.MapPathToPublicUrl(string.Empty).TrimEnd('/').TrimStart('/') + "/";

        if (publicPrefix.Length > 1 && path.StartsWith(publicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = path[publicPrefix.Length..];
        }

        if (path.Length == 0 || path.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            return (null, $"'{value}' is not a media library path. Use a path such as certificates/name.pdf.");
        }

        var file = await mediaFileStore.GetFileInfoAsync(path);

        if (file is null || file.IsDirectory)
        {
            return (null, $"there is no file '{path}' in the media library. Upload it first, or give the path of an existing file.");
        }

        return (file.Path, null);
    }
}
