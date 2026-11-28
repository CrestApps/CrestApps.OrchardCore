using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Converts a lead through <see cref="ILeadConversionService"/>, the service the Convert Lead page posts to, or previews
/// the contacts and accounts it would match, as that page shows them.
/// </summary>
internal sealed class ConvertLeadTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "convertLead";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "leadId": {
          "type": "string",
          "description": "The content item ID of the lead to convert."
        },
        "preview": {
          "type": "boolean",
          "description": "true returns the contacts and accounts that match the lead instead of converting it."
        },
        "existingContactId": {
          "type": "string",
          "description": "The content item ID of an existing contact to merge the lead into. Leave it out to create a new contact."
        },
        "contactContentType": {
          "type": "string",
          "description": "The contact content type of a new contact. Leave it out to use the lead type's setting."
        },
        "accountMode": {
          "type": "string",
          "enum": ["None", "CreateNew", "UseExisting", "Automatic"],
          "description": "What account the contact joins: none, a new account, an existing account, or the one matching the lead's company. Defaults to None."
        },
        "accountName": {
          "type": "string",
          "description": "With accountMode CreateNew: the new account's name. Leave it out to use the lead's company."
        },
        "existingAccountId": {
          "type": "string",
          "description": "With accountMode UseExisting: the content item ID of the account."
        },
        "createOpportunity": {
          "type": "boolean",
          "description": "Whether to create an opportunity for the contact."
        },
        "opportunityContentType": {
          "type": "string",
          "description": "With createOpportunity: the opportunity content type. Leave it out to use the lead type's setting."
        },
        "opportunityName": {
          "type": "string",
          "description": "With createOpportunity: the opportunity's name."
        },
        "opportunityAmount": {
          "type": "number",
          "minimum": 0,
          "description": "With createOpportunity: the expected amount."
        },
        "opportunityCloseDate": {
          "type": "string",
          "description": "With createOpportunity: the expected close date, as yyyy-MM-dd."
        },
        "openActivities": {
          "type": "string",
          "enum": ["Move", "Cancel"],
          "description": "What happens to the lead's unfinished activities: move them to the contact, or cancel them. Defaults to Move."
        }
      },
      "required": ["leadId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Converts a lead into a contact, optionally attaching it to a new or existing account and creating an opportunity. With preview it returns the matching contacts and accounts instead of converting. A converted lead cannot be converted again.";

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

        if (AIModuleTools.GetCaller(services) is not { } caller)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var leadId = AIModuleTools.ReadString(arguments, "leadId");
        var lead = leadId is null ? null : await services.GetRequiredService<IContentManager>().GetAsync(leadId, VersionOptions.Latest);

        // The permission is checked on the lead, as the Convert Lead page checks it, so a lead the caller may not see is
        // reported the same way as one that does not exist.
        if (lead is null || !lead.TryGet<LeadPart>(out var leadPart))
        {
            return await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ConvertLead)
                ? $"No lead has the ID '{leadId}'."
                : AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ConvertLead, lead))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (leadPart.IsConverted)
        {
            return $"The lead '{lead.DisplayText}' was already converted and cannot be converted again.";
        }

        if (arguments.TryGetFirst<bool>("preview", out var preview) && preview)
        {
            var matchFinder = services.GetRequiredService<LeadMatchFinder>();
            var company = leadPart.Company.GetTrimmedText();

            return AIModuleTools.Json(new
            {
                leadId = lead.ContentItemId,
                lead = lead.DisplayText,
                company,
                matchingContacts = (await matchFinder.FindContactsAsync(lead)).Select(Describe),
                matchingAccounts = (await matchFinder.FindAccountsAsync(company)).Select(Describe),
            });
        }

        if (!OmnichannelToolSupport.TryReadEnum<LeadConversionAccountMode>(arguments, "accountMode", out var accountMode))
        {
            return $"Nothing was converted: accountMode must be one of {OmnichannelToolSupport.Names<LeadConversionAccountMode>()}.";
        }

        if (!OmnichannelToolSupport.TryReadEnum<LeadOpenActivityMode>(arguments, "openActivities", out var openActivities))
        {
            return $"Nothing was converted: openActivities must be one of {OmnichannelToolSupport.Names<LeadOpenActivityMode>()}.";
        }

        DateTime? closeDate = null;
        var closeDateText = AIModuleTools.ReadString(arguments, "opportunityCloseDate");

        // A close date is a calendar date, not a moment, so it is read as given and not converted between time zones.
        if (closeDateText is not null)
        {
            if (!DateTime.TryParseExact(closeDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return "Nothing was converted: opportunityCloseDate must be a date as yyyy-MM-dd.";
            }

            closeDate = parsedDate;
        }

        decimal? amount = arguments.TryGetFirst<decimal>("opportunityAmount", out var parsedAmount) ? parsedAmount : null;

        if (amount < 0)
        {
            return "Nothing was converted: opportunityAmount cannot be negative.";
        }

        var result = await services.GetRequiredService<ILeadConversionService>().ConvertAsync(new LeadConversionRequest
        {
            LeadContentItemId = lead.ContentItemId,
            ExistingContactItemId = AIModuleTools.ReadString(arguments, "existingContactId"),
            ContactContentType = AIModuleTools.ReadString(arguments, "contactContentType"),
            AccountMode = accountMode ?? LeadConversionAccountMode.None,
            AccountName = AIModuleTools.ReadString(arguments, "accountName"),
            ExistingAccountItemId = AIModuleTools.ReadString(arguments, "existingAccountId"),
            CreateOpportunity = arguments.TryGetFirst<bool>("createOpportunity", out var createOpportunity) && createOpportunity,
            OpportunityContentType = AIModuleTools.ReadString(arguments, "opportunityContentType"),
            OpportunityName = AIModuleTools.ReadString(arguments, "opportunityName"),
            OpportunityAmount = amount,
            OpportunityCloseDate = closeDate,
            OpenActivities = openActivities ?? LeadOpenActivityMode.Move,
            UserId = caller.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = caller.Identity?.Name,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return "Nothing was converted: " + string.Join(" ", result.Errors);
        }

        if (result.AlreadyConverted)
        {
            return $"The lead '{lead.DisplayText}' was already converted and cannot be converted again.";
        }

        return AIModuleTools.Json(new
        {
            converted = true,
            leadId = lead.ContentItemId,
            contact = Describe(result.Contact),
            contactCreated = result.ContactCreated,
            account = Describe(result.Account),
            accountCreated = result.AccountCreated,
            opportunity = Describe(result.Opportunity),
            movedActivities = result.MovedActivities,
            cancelledActivities = result.CancelledActivities,
        });
    }

    private static object Describe(ContentItem item)
        => item is null
            ? null
            : new
            {
                id = item.ContentItemId,
                contentType = item.ContentType,
                displayText = item.DisplayText,
            };
}
