using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Creates or updates one action of a subject flow with the fields the action editors set, and saves it through the
/// subject action manager, whose handler validates it as it validates the editors' posts.
/// </summary>
internal sealed class SaveSubjectActionTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveSubjectAction";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the action to update. Leave it out to create an action."
        },
        "subjectContentType": {
          "type": "string",
          "description": "The technical name of the subject the action belongs to. Required when creating."
        },
        "actionType": {
          "type": "string",
          "enum": ["Finish", "TryAgain", "NewActivity", "ConvertLead"],
          "description": "What the action does. Required when creating; ConvertLead exists only with the CRM feature. It cannot be changed afterwards."
        },
        "dispositionId": {
          "type": "string",
          "description": "The ID of the disposition that triggers the action. Required when creating. Use listRecords with the kind omnichannelDisposition to find it."
        },
        "dispositionGuidance": {
          "type": "string",
          "description": "Guidance shown to the agent for this disposition. An empty text clears it."
        },
        "doNotCall": {
          "type": "string",
          "enum": ["set", "clear", "leave"],
          "description": "Whether the action marks the contact as do not call (set), removes that mark (clear) or leaves it as it is."
        },
        "doNotSms": {
          "type": "string",
          "enum": ["set", "clear", "leave"],
          "description": "Whether the action marks the contact as do not text (set), removes that mark (clear) or leaves it as it is."
        },
        "doNotEmail": {
          "type": "string",
          "enum": ["set", "clear", "leave"],
          "description": "Whether the action marks the contact as do not email (set), removes that mark (clear) or leaves it as it is."
        },
        "urgencyLevel": {
          "type": "string",
          "enum": ["Normal", "VeryLow", "Low", "Medium", "High", "VeryHigh"],
          "description": "TryAgain and NewActivity: the urgency of the activity the action schedules."
        },
        "assignmentType": {
          "type": "string",
          "enum": ["SameOwner", "SpecificOwner"],
          "description": "TryAgain and NewActivity: who the scheduled activity is assigned to."
        },
        "ownerUserName": {
          "type": "string",
          "description": "TryAgain and NewActivity with assignmentType SpecificOwner: the user name of the owner."
        },
        "defaultScheduleHours": {
          "type": "integer",
          "minimum": 0,
          "description": "TryAgain and NewActivity: how many hours from now the scheduled activity defaults to."
        },
        "maxAttempts": {
          "type": "integer",
          "minimum": 1,
          "description": "TryAgain: the most attempts before the activity is not tried again."
        },
        "newSubjectContentType": {
          "type": "string",
          "description": "NewActivity: the subject of the new activity. Leave it out to keep the subject of the activity that raised the action."
        },
        "setLeadStatusId": {
          "type": "string",
          "description": "CRM only: the lead status the lead moves to when this action runs. Must not be the converted status. An empty text clears it."
        },
        "accountMode": {
          "type": "string",
          "enum": ["None", "CreateNew", "UseExisting", "Automatic"],
          "description": "ConvertLead: what account the converted contact joins."
        },
        "createOpportunity": {
          "type": "boolean",
          "description": "ConvertLead: whether an opportunity is created."
        },
        "opportunityContentType": {
          "type": "string",
          "description": "ConvertLead with createOpportunity: the opportunity content type."
        },
        "openActivities": {
          "type": "string",
          "enum": ["Move", "Cancel"],
          "description": "ConvertLead: what happens to the lead's unfinished activities."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates one action of a subject flow: the subject type, the disposition that triggers it, the action type (Finish, TryAgain, NewActivity, ConvertLead) and its type-specific settings. The action type cannot be changed after creation.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<SubjectAction>>();
        var options = services.GetRequiredService<IOptions<SubjectActionOptions>>().Value;
        var crmEnabled = options.ActionTypes.ContainsKey(OmnichannelConstants.ActionTypes.ConvertLead);
        var id = AIModuleTools.ReadString(arguments, "id");
        SubjectAction existing = null;
        SubjectAction working;

        if (id is not null)
        {
            existing = await manager.FindByIdAsync(id, cancellationToken);

            if (existing is null)
            {
                return $"Nothing was saved: no subject action has the ID '{id}'. Call listSubjectFlows with the subject to find it.";
            }

            var actionType = AIModuleTools.ReadString(arguments, "actionType");

            if (actionType is not null && !string.Equals(actionType, existing.Source, StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: the action is a {existing.Source} action and its type cannot be changed. Delete it with deleteOmnichannelRecord and create a new one.";
            }

            var subject = AIModuleTools.ReadString(arguments, "subjectContentType");

            if (subject is not null && !string.Equals(subject, existing.SubjectContentType, StringComparison.OrdinalIgnoreCase))
            {
                return "Nothing was saved: an action cannot be moved to another subject. Create a new action on that subject instead.";
            }

            working = existing.Clone();
        }
        else
        {
            var subject = AIModuleTools.ReadString(arguments, "subjectContentType");
            var actionType = AIModuleTools.ReadString(arguments, "actionType");

            if (subject is null || actionType is null || AIModuleTools.ReadString(arguments, "dispositionId") is null)
            {
                return "Nothing was saved: subjectContentType, actionType and dispositionId are required to create an action.";
            }

            var contentType = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(subject);

            if (!OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(contentType))
            {
                return $"Nothing was saved: no subject content type is named '{subject}'. Call listSubjectFlows to see the subjects.";
            }

            if (!options.ActionTypes.TryGetValue(actionType, out var entry))
            {
                return $"Nothing was saved: '{actionType}' is not an action type here. Use one of {string.Join(", ", options.ActionTypes.Keys)}.";
            }

            working = await manager.NewAsync(entry.Type, cancellationToken: cancellationToken);
            working.SubjectContentType = contentType.Name;
        }

        var refusal = await ApplyAsync(arguments, services, working, crmEnabled, cancellationToken);

        if (refusal is not null)
        {
            return "Nothing was saved: " + refusal;
        }

        var validation = await manager.ValidateAsync(working, cancellationToken);

        if (!validation.Succeeded)
        {
            return "Nothing was saved: " + OmnichannelToolSupport.Reasons(validation);
        }

        if (existing is null)
        {
            await manager.CreateAsync(working, cancellationToken);
        }
        else
        {
            existing.DispositionId = working.DispositionId;
            existing.DispositionGuidance = working.DispositionGuidance;
            existing.SetDoNotCall = working.SetDoNotCall;
            existing.SetDoNotSms = working.SetDoNotSms;
            existing.SetDoNotEmail = working.SetDoNotEmail;
            existing.Properties = working.Properties;

            await manager.UpdateAsync(existing, cancellationToken: cancellationToken);
        }

        var saved = existing ?? working;
        var disposition = await services.GetRequiredService<INamedCatalog<OmnichannelDisposition>>().FindByIdAsync(saved.DispositionId, cancellationToken);

        return AIModuleTools.Json(new
        {
            saved = existing is null ? "created" : "updated",
            action = SubjectFlowToolSupport.DescribeAction(
                saved,
                disposition?.Name,
                options.ActionTypes.TryGetValue(saved.Source ?? string.Empty, out var type) ? type.DisplayName?.Value : null),
        });
    }

    private static async Task<string> ApplyAsync(AIFunctionArguments arguments, IServiceProvider services, SubjectAction action, bool crmEnabled, CancellationToken cancellationToken)
    {
        var dispositionId = AIModuleTools.ReadString(arguments, "dispositionId");

        if (dispositionId is not null)
        {
            if (await services.GetRequiredService<INamedCatalog<OmnichannelDisposition>>().FindByIdAsync(dispositionId, cancellationToken) is null)
            {
                return $"no disposition has the ID '{dispositionId}'. Use listRecords with the kind omnichannelDisposition to find it.";
            }

            action.DispositionId = dispositionId;
        }

        if (arguments.TryGetFirstString("dispositionGuidance", true, out var guidance))
        {
            action.DispositionGuidance = string.IsNullOrWhiteSpace(guidance) ? null : guidance.Trim();
        }

        foreach (var (key, apply) in new (string, Action<bool?>)[]
        {
            ("doNotCall", value => action.SetDoNotCall = value),
            ("doNotSms", value => action.SetDoNotSms = value),
            ("doNotEmail", value => action.SetDoNotEmail = value),
        })
        {
            if (!SubjectFlowToolSupport.TryReadFlag(AIModuleTools.ReadString(arguments, key), out var flag, out var given))
            {
                return $"{key} must be set, clear or leave.";
            }

            if (given)
            {
                apply(flag);
            }
        }

        var refusal = ApplyScheduling(arguments, services, action);

        if (refusal is not null)
        {
            return refusal;
        }

        return await ApplyLeadAsync(arguments, services, action, crmEnabled, cancellationToken);
    }

    private static string ApplyScheduling(AIFunctionArguments arguments, IServiceProvider services, SubjectAction action)
    {
        var isTryAgain = string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase);
        var isNewActivity = string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase);

        if (!OmnichannelToolSupport.TryReadEnum<ActivityUrgencyLevel>(arguments, "urgencyLevel", out var urgency))
        {
            return $"urgencyLevel must be one of {OmnichannelToolSupport.Names<ActivityUrgencyLevel>()}.";
        }

        if (!OmnichannelToolSupport.TryReadEnum<SubjectActionOwnerAssignmentType>(arguments, "assignmentType", out var assignment))
        {
            return $"assignmentType must be one of {OmnichannelToolSupport.Names<SubjectActionOwnerAssignmentType>()}.";
        }

        var hasMaxAttempts = arguments.TryGetFirst<int>("maxAttempts", out var maxAttempts);
        var hasHours = arguments.TryGetFirst<int>("defaultScheduleHours", out var hours);
        var owner = AIModuleTools.ReadString(arguments, "ownerUserName");
        var newSubject = arguments.TryGetFirstString("newSubjectContentType", true, out var subjectText);

        if (!isTryAgain && !isNewActivity)
        {
            return urgency.HasValue || assignment.HasValue || hasMaxAttempts || hasHours || owner is not null || newSubject
                ? $"a {action.Source} action schedules nothing, so it takes no urgency, owner, schedule, attempts or new subject."
                : null;
        }

        if (hasMaxAttempts && (!isTryAgain || maxAttempts < 1))
        {
            return isTryAgain ? "maxAttempts must be 1 or more." : "maxAttempts applies to a TryAgain action only.";
        }

        if (newSubject && !isNewActivity)
        {
            return "newSubjectContentType applies to a NewActivity action only.";
        }

        if (hasHours && hours < 0)
        {
            return "defaultScheduleHours cannot be negative.";
        }

        // The editors store the owner's normalized user name, which is what the handler looks the user up by.
        var normalizedOwner = owner is null
            ? null
            : services.GetService<ILookupNormalizer>()?.NormalizeName(owner) ?? owner.ToUpperInvariant();

        if (isTryAgain)
        {
            var metadata = action.GetOrCreate<TryAgainActionMetadata>();
            metadata.UrgencyLevel = urgency ?? metadata.UrgencyLevel;
            metadata.AssignmentType = assignment ?? metadata.AssignmentType;
            metadata.NormalizedUserName = normalizedOwner ?? metadata.NormalizedUserName;
            metadata.DefaultScheduleHours = hasHours ? hours : metadata.DefaultScheduleHours;
            metadata.MaxAttempt = hasMaxAttempts ? maxAttempts : metadata.MaxAttempt;

            // As in the editor, an owner is kept only for a specific-owner assignment.
            if (metadata.AssignmentType != SubjectActionOwnerAssignmentType.SpecificOwner)
            {
                metadata.NormalizedUserName = null;
            }

            action.Put(metadata);
        }
        else
        {
            var metadata = action.GetOrCreate<NewActivityActionMetadata>();
            metadata.UrgencyLevel = urgency ?? metadata.UrgencyLevel;
            metadata.AssignmentType = assignment ?? metadata.AssignmentType;
            metadata.NormalizedUserName = normalizedOwner ?? metadata.NormalizedUserName;
            metadata.DefaultScheduleHours = hasHours ? hours : metadata.DefaultScheduleHours;

            if (newSubject)
            {
                metadata.SubjectContentType = string.IsNullOrWhiteSpace(subjectText) ? null : subjectText.Trim();
            }

            if (metadata.AssignmentType != SubjectActionOwnerAssignmentType.SpecificOwner)
            {
                metadata.NormalizedUserName = null;
            }

            action.Put(metadata);
        }

        return null;
    }

    private static async Task<string> ApplyLeadAsync(AIFunctionArguments arguments, IServiceProvider services, SubjectAction action, bool crmEnabled, CancellationToken cancellationToken)
    {
        var isConvertLead = string.Equals(action.Source, OmnichannelConstants.ActionTypes.ConvertLead, StringComparison.OrdinalIgnoreCase);
        var hasStatus = arguments.TryGetFirstString("setLeadStatusId", true, out var statusText);

        if (!OmnichannelToolSupport.TryReadEnum<LeadConversionAccountMode>(arguments, "accountMode", out var accountMode))
        {
            return $"accountMode must be one of {OmnichannelToolSupport.Names<LeadConversionAccountMode>()}.";
        }

        if (!OmnichannelToolSupport.TryReadEnum<LeadOpenActivityMode>(arguments, "openActivities", out var openActivities))
        {
            return $"openActivities must be one of {OmnichannelToolSupport.Names<LeadOpenActivityMode>()}.";
        }

        var hasCreateOpportunity = arguments.TryGetFirst<bool>("createOpportunity", out var createOpportunity);
        var hasOpportunityType = arguments.TryGetFirstString("opportunityContentType", true, out var opportunityType);

        if (!isConvertLead && (accountMode.HasValue || openActivities.HasValue || hasCreateOpportunity || hasOpportunityType))
        {
            return "accountMode, createOpportunity, opportunityContentType and openActivities apply to a ConvertLead action only.";
        }

        if (hasStatus)
        {
            if (!crmEnabled)
            {
                return "lead statuses exist only with the Omnichannel CRM feature.";
            }

            var statusId = string.IsNullOrWhiteSpace(statusText) ? null : statusText.Trim();

            if (statusId is not null)
            {
                // The editor offers only statuses that are not the converted one; a model is told instead of the value being dropped.
                var status = await services.GetRequiredService<INamedCatalog<LeadStatus>>().FindByIdAsync(statusId, cancellationToken);

                if (status is null)
                {
                    return $"no lead status has the ID '{statusId}'. Use listRecords with the kind leadStatus to find it.";
                }

                if (status.IsConverted)
                {
                    return $"the lead status '{status.Name}' is the converted status, which only a lead conversion sets.";
                }
            }

            action.Put(new SetLeadStatusActionMetadata { StatusId = statusId });
        }

        if (isConvertLead)
        {
            var metadata = action.GetOrCreate<ConvertLeadActionMetadata>();
            metadata.AccountMode = accountMode ?? metadata.AccountMode;
            metadata.OpenActivities = openActivities ?? metadata.OpenActivities;
            metadata.CreateOpportunity = hasCreateOpportunity ? createOpportunity : metadata.CreateOpportunity;

            if (hasOpportunityType)
            {
                metadata.OpportunityContentType = string.IsNullOrWhiteSpace(opportunityType) ? null : opportunityType.Trim();
            }

            action.Put(metadata);
        }

        return null;
    }
}
