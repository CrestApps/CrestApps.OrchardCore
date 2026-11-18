using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Creates or changes a certification type through the certification manager, whose catalog handler holds the editor's
/// rules (a unique name, a validity of at least one month, reminders of one day or more). A new type gets the default
/// reminder 30 days before expiry unless reminder days are given. Only the fields given change. It needs
/// <see cref="SkillsPermissions.ManageSkills"/>.
/// </summary>
internal sealed class SaveCertificationTypeTool : SkillsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveCertificationType";

    private static readonly string[] _fieldNames = ["name", "issuer", "description", "validityMonths", "reminderDays", "requiresDocument", "enabled"];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "certificationId": {
          "type": "string",
          "description": "The id of the certification type to change, from listCertificationTypes. Leave it out to create one."
        },
        "name": {
          "type": "string",
          "description": "The certification type's name, unique. Required for a new type."
        },
        "issuer": {
          "type": "string",
          "description": "Who issues the certificates, such as OSHA. An empty text removes it."
        },
        "description": {
          "type": "string",
          "description": "What the certification covers. An empty text removes it."
        },
        "validityMonths": {
          "type": "integer",
          "description": "How many months a certificate is valid from its issue date. 0 means certificates never expire."
        },
        "reminderDays": {
          "type": "array",
          "items": { "type": "integer" },
          "description": "How many days before expiry reminders are raised, such as [60, 30]. The list replaces the stored one; an empty list means no reminders."
        },
        "requiresDocument": {
          "type": "boolean",
          "description": "Whether a copy of the certificate must be attached before it can be marked valid."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the type is offered for new certificates and skills. A new type is enabled when left out."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a certification type: name, issuer, validity in months, reminder days before expiry, document required and enabled. Only the fields given change.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => SkillsPermissions.ManageSkills;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<ICertificationManager>();
        var certificationId = ReadString(arguments, "certificationId");
        Certification certification;

        if (certificationId is null)
        {
            certification = await manager.NewAsync(cancellationToken: cancellationToken);
        }
        else
        {
            certification = await manager.FindByIdAsync(certificationId, cancellationToken);

            if (certification is null)
            {
                return $"Nothing was saved: there is no certification type with the id '{certificationId}'. Use listCertificationTypes to find it, or leave certificationId out to create one.";
            }

            if (!_fieldNames.Any(name => Has(arguments, name)))
            {
                return "Nothing was changed: give at least one field to change.";
            }
        }

        if (Has(arguments, "validityMonths"))
        {
            if (ReadString(arguments, "validityMonths") is null)
            {
                certification.ValidityMonths = null;
            }
            else if (ReadInt(arguments, "validityMonths") is not { } months)
            {
                return "Nothing was saved: validityMonths must be a whole number of months, or 0 when certificates never expire.";
            }
            else
            {
                certification.ValidityMonths = months == 0 ? null : months;
            }
        }

        if (Has(arguments, "reminderDays"))
        {
            if (!TryReadIntList(arguments, "reminderDays", out var days))
            {
                return "Nothing was saved: reminderDays must be whole numbers of days, such as [60, 30].";
            }

            certification.ReminderDays = days;
        }

        if (Has(arguments, "name"))
        {
            certification.Name = ReadString(arguments, "name");
        }

        if (Has(arguments, "issuer"))
        {
            certification.Issuer = ReadString(arguments, "issuer");
        }

        if (Has(arguments, "description"))
        {
            certification.Description = ReadString(arguments, "description");
        }

        if (Has(arguments, "requiresDocument"))
        {
            certification.RequiresDocument = ReadBool(arguments, "requiresDocument") ?? certification.RequiresDocument;
        }

        if (Has(arguments, "enabled"))
        {
            certification.Enabled = ReadBool(arguments, "enabled") ?? certification.Enabled;
        }

        var validation = await manager.ValidateAsync(certification, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Where(message => !string.IsNullOrEmpty(message)))}";
        }

        if (certificationId is null)
        {
            await manager.CreateAsync(certification, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(certification, cancellationToken: cancellationToken);
        }

        return Json(new
        {
            certificationType = SkillsToolResults.Certification(certification),
            created = certificationId is null,
            message = certificationId is null ? "The certification type has been created." : "The certification type has been saved.",
        });
    }
}
