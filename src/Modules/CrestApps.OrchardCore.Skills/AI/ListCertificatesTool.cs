using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Lists the certificates people hold through <see cref="ICertificationRecordService.PageAsync"/>, with the filters of
/// the Certificates page: text (the holder's name or the number), certification type, status and expiring within a number
/// of days (the page's "Expiring soon" is 60 days, and like it, only valid certificates unless a status is given), plus
/// one person. It needs <see cref="SkillsPermissions.ViewSkills"/>.
/// </summary>
internal sealed class ListCertificatesTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listCertificates";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
    """ + SubjectProperties + """
        ,"search": {
          "type": "string",
          "description": "Part of the holder's name or of the certificate number."
        },
        "certificationId": {
          "type": "string",
          "description": "Only certificates of this certification type, from listSkills with kind certificationType."
        },
        "status": {
          "type": "string",
          "enum": ["Pending", "Valid", "Expired", "Revoked"],
          "description": "Only certificates in this status. Pending is waiting to be checked; Revoked was withdrawn."
        },
        "expiringWithinDays": {
          "type": "integer",
          "minimum": 0,
          "maximum": 3650,
          "description": "Only certificates that expire within this many days from today (or already have). 60 matches the Certificates page's Expiring soon filter. Unless a status is given, only valid certificates are listed."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1. Defaults to 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many certificates a page holds, from 1 to 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the certificates people hold, filtered by person, certification type, status or expiring soon, with paging. Give subjectKind and subjectId together to filter by person.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.ViewSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var today = await GetTodayAsync(services);
        var query = new CertificationRecordQuery
        {
            Search = ReadString(arguments, "search"),
            CertificationId = ReadString(arguments, "certificationId"),
        };

        if (ReadString(arguments, "status") is { } statusText)
        {
            if (!Enum.TryParse<CertificationStatus>(statusText, ignoreCase: true, out var status) || !Enum.IsDefined(status))
            {
                return "status must be Pending, Valid, Expired or Revoked.";
            }

            query.Status = status;
        }

        if (Has(arguments, "expiringWithinDays") && ReadString(arguments, "expiringWithinDays") is not null)
        {
            if (ReadInt(arguments, "expiringWithinDays") is not { } days || days is < 0 or > 3650)
            {
                return "expiringWithinDays must be a whole number of days from 0 to 3650.";
            }

            query.ExpiresOnOrBefore = today.AddDays(days);
            query.Status ??= CertificationStatus.Valid;
        }

        SubjectLookup lookup = null;

        if (ReadString(arguments, "subjectKind") is not null || ReadString(arguments, "subjectId") is not null)
        {
            lookup = await FindSubjectAsync(arguments, services, caller, forChange: false, cancellationToken);

            if (lookup.Error is not null)
            {
                return lookup.Error;
            }

            query.Subject = lookup.Subject;
        }

        var (page, pageSize) = ReadPage(arguments);
        var (count, records) = await services.GetRequiredService<ICertificationRecordService>().PageAsync(query, (page - 1) * pageSize, pageSize, cancellationToken);
        var certifications = await SkillsToolResults.GetCertificationsAsync(services, cancellationToken);
        var names = await GetSubjectNamesAsync(services, records.Select(record => record.Subject), cancellationToken);
        var items = new List<object>();

        foreach (var record in records)
        {
            items.Add(await SkillsToolResults.CertificateAsync(services, record, certifications, names, today));
        }

        return Json(new
        {
            items,
            total = count,
            page,
            pageSize,
            truncated = count > page * pageSize,
            today = SkillsToolResults.Date(today),
        });
    }
}
