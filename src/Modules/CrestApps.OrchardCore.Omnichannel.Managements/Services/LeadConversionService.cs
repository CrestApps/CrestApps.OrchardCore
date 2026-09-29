using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Flows.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Modules;
using OrchardCore.Title.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Converts a lead into a contact. Everything is saved through the request's session, so a conversion that fails
/// part way through leaves nothing half done.
/// </summary>
internal sealed class LeadConversionService : ILeadConversionService
{
    // Parts that are never copied from the lead: the lead's own state, the parts the conversion writes itself, and
    // parts whose values must stay unique to one item.
    private static readonly HashSet<string> _partsNotCopied = new(StringComparer.Ordinal)
    {
        OmnichannelConstants.ContentParts.Lead,
        OmnichannelConstants.ContentParts.OmnichannelContact,
        OmnichannelConstants.NamedParts.ContactMethods,
        OmnichannelConstants.ContentParts.Contained,
        OmnichannelConstants.ContentParts.List,
        nameof(TitlePart),
        "AutoroutePart",
        "CommonPart",
        "LocalizationPart",
        "AuditTrailPart",
    };

    private static readonly ActivityStatus[] _liveStatuses =
    [
        ActivityStatus.Reserved,
        ActivityStatus.Dialing,
        ActivityStatus.InProgress,
    ];

    private static readonly ActivityStatus[] _finishedStatuses =
    [
        ActivityStatus.Completed,
        ActivityStatus.Cancelled,
        ActivityStatus.Purged,
    ];

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;
    private readonly INamedCatalog<LeadStatus> _leadStatuses;
    private readonly INamedCatalog<OpportunityStage> _opportunityStages;
    private readonly IEnumerable<ILeadConversionHandler> _handlers;
    private readonly IEnumerable<ILeadConversionRepointer> _repointers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadConversionService"/> class.
    /// </summary>
    public LeadConversionService(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        INamedCatalog<LeadStatus> leadStatuses,
        INamedCatalog<OpportunityStage> opportunityStages,
        IEnumerable<ILeadConversionHandler> handlers,
        IEnumerable<ILeadConversionRepointer> repointers,
        IClock clock,
        ILogger<LeadConversionService> logger,
        IStringLocalizer<LeadConversionService> stringLocalizer)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _leadStatuses = leadStatuses;
        _opportunityStages = opportunityStages;
        _handlers = handlers;
        _repointers = repointers;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<LeadConversionResult> ConvertAsync(LeadConversionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new LeadConversionResult();

        var lead = string.IsNullOrEmpty(request.LeadContentItemId)
            ? null
            : await _contentManager.GetAsync(request.LeadContentItemId, VersionOptions.Latest);

        if (lead is null || !lead.TryGet<LeadPart>(out var leadPart))
        {
            result.Errors.Add(S["The lead could not be found."]);

            return result;
        }

        result.Lead = lead;

        // Converting twice changes nothing, so a retried request or a second click returns what the first did.
        if (leadPart.IsConverted)
        {
            result.AlreadyConverted = true;
            result.Contact = string.IsNullOrEmpty(leadPart.ConvertedContactItemId)
                ? null
                : await _contentManager.GetAsync(leadPart.ConvertedContactItemId, VersionOptions.Latest);

            return result;
        }

        var leadTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(lead.ContentType);
        var leadSettings = GetPartSettings<LeadPartSettings>(leadTypeDefinition, OmnichannelConstants.ContentParts.Lead);

        if (await HasLiveActivityAsync(lead.ContentItemId, request.CompletingActivityId, cancellationToken))
        {
            result.Errors.Add(S["The lead is on a call or reserved for one right now. Convert it once that work is finished."]);

            return result;
        }

        var context = new LeadConversionContext
        {
            Request = request,
            Lead = lead,
        };

        if (!await BuildContactAsync(context, leadTypeDefinition, leadSettings, result))
        {
            return result;
        }

        if (!await BuildAccountAsync(context, leadPart, result))
        {
            return result;
        }

        var contactTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.Contact.ContentType);

        // A contact the lead is merged into keeps the account it already has; conversion never moves a customer.
        if (context.Account is not null &&
            OmnichannelRecordKinds.IsAccountChild(contactTypeDefinition) &&
            (context.ContactCreated || !BelongsToAccount(context.Contact)))
        {
            PlaceInAccount(context.Contact, context.Account);
        }

        if (request.CreateOpportunity && !await BuildOpportunityAsync(context, leadPart, leadSettings, result))
        {
            return result;
        }

        foreach (var handler in _handlers)
        {
            await handler.ConvertingAsync(context);
        }

        await SaveAsync(context.Account, isNew: result.AccountCreated);
        await SaveAsync(context.Contact, isNew: context.ContactCreated);

        if (context.Opportunity is not null)
        {
            context.Opportunity.Alter<OpportunityPart>(part => part.PrimaryContactItemId ??= context.Contact.ContentItemId);
            await SaveAsync(context.Opportunity, isNew: true);
        }

        await MoveActivitiesAsync(context, contactTypeDefinition, result, cancellationToken);

        foreach (var repointer in _repointers)
        {
            await repointer.RepointAsync(context, cancellationToken);
        }

        await CloseLeadAsync(context);

        foreach (var handler in _handlers)
        {
            await handler.ConvertedAsync(context);
        }

        result.Contact = context.Contact;
        result.ContactCreated = context.ContactCreated;
        result.Account = context.Account;
        result.Opportunity = context.Opportunity;

        // The lead and contact ids trace a conversion; the account and opportunity are logged as present or not, and
        // who converted stays on the lead's own audit fields rather than in the log.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Converted lead {LeadId} into {ContactMode} contact {ContactId} (account: {HasAccount}, opportunity: {HasOpportunity}); moved {MovedActivities} activities and cancelled {CancelledActivities}.",
                lead.ContentItemId.SanitizeLogValue(),
                context.ContactCreated ? "new" : "existing",
                context.Contact.ContentItemId.SanitizeLogValue(),
                context.Account is not null,
                context.Opportunity is not null,
                result.MovedActivities,
                result.CancelledActivities);
        }

        return result;
    }

    private async Task<bool> BuildContactAsync(
        LeadConversionContext context,
        ContentTypeDefinition leadTypeDefinition,
        LeadPartSettings leadSettings,
        LeadConversionResult result)
    {
        var request = context.Request;
        var lead = context.Lead;
        ContentItem contact;

        if (!string.IsNullOrEmpty(request.ExistingContactItemId))
        {
            contact = await _contentManager.GetAsync(request.ExistingContactItemId, VersionOptions.Latest);

            if (contact is null || !OmnichannelRecordKinds.IsContact(await _contentDefinitionManager.GetTypeDefinitionAsync(contact.ContentType)))
            {
                result.Errors.Add(S["The contact to merge the lead into could not be found."]);

                return false;
            }

            context.ContactCreated = false;
        }
        else
        {
            var contactType = request.ContactContentType ?? leadSettings.TargetContactContentType;
            var contactTypeDefinition = string.IsNullOrEmpty(contactType)
                ? null
                : await _contentDefinitionManager.GetTypeDefinitionAsync(contactType);

            if (!OmnichannelRecordKinds.IsContact(contactTypeDefinition))
            {
                result.Errors.Add(S["Choose the contact type the lead becomes. Set it on the lead type to skip this choice."]);

                return false;
            }

            contact = await _contentManager.NewAsync(contactType);
            context.ContactCreated = true;
        }

        var contactDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contact.ContentType);

        if (context.ContactCreated)
        {
            contact.DisplayText = lead.DisplayText;

            if (contactDefinition.Parts.Any(part => part.PartDefinition?.Name == nameof(TitlePart)))
            {
                contact.Alter<TitlePart>(part => part.Title = lead.TryGet<TitlePart>(out var leadTitle) && !string.IsNullOrEmpty(leadTitle.Title) ? leadTitle.Title : lead.DisplayText);
            }
        }

        CopyMatchingParts(lead, leadTypeDefinition, contact, contactDefinition, overwrite: context.ContactCreated);
        MergeContactMethods(lead, contact);
        MergeCommunicationPreferences(lead, contact);

        context.Contact = contact;

        return true;
    }

    private async Task<bool> BuildAccountAsync(LeadConversionContext context, LeadPart leadPart, LeadConversionResult result)
    {
        var request = context.Request;
        var accountTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(OmnichannelRecordKinds.IsAccount)
            .ToArray();

        switch (request.AccountMode)
        {
            case LeadConversionAccountMode.None:
                return true;

            case LeadConversionAccountMode.UseExisting:
                var existing = string.IsNullOrEmpty(request.ExistingAccountItemId)
                    ? null
                    : await _contentManager.GetAsync(request.ExistingAccountItemId, VersionOptions.Latest);

                if (existing is null || !accountTypes.Any(type => type.Name == existing.ContentType))
                {
                    result.Errors.Add(S["The account could not be found."]);

                    return false;
                }

                context.Account = existing;

                return true;

            case LeadConversionAccountMode.Automatic:
                // A merged contact that already belongs to an account keeps it, and the opportunity joins that account.
                if (!context.ContactCreated && context.Contact.TryGet<ContainedPart>(out var contained) && !string.IsNullOrEmpty(contained.ListContentItemId))
                {
                    var contactAccount = await _contentManager.GetAsync(contained.ListContentItemId, VersionOptions.Latest);

                    if (contactAccount is not null && accountTypes.Any(type => type.Name == contactAccount.ContentType))
                    {
                        context.Account = contactAccount;
                    }

                    return true;
                }

                if (string.IsNullOrWhiteSpace(leadPart.Company) || accountTypes.Length == 0)
                {
                    return true;
                }

                var company = leadPart.Company.Trim();
                var accountTypeNames = accountTypes.Select(type => type.Name).ToArray();
                var matches = await _session.Query<ContentItem, ContentItemIndex>(index =>
                        index.Latest &&
                        index.ContentType.IsIn(accountTypeNames) &&
                        index.DisplayText == company)
                    .Take(2)
                    .ListAsync();

                var matchList = matches.ToList();

                if (matchList.Count == 1)
                {
                    context.Account = matchList[0];

                    return true;
                }

                if (matchList.Count > 1)
                {
                    // Two accounts with the same name cannot be told apart automatically; leave the choice to a person.
                    return true;
                }

                return await CreateAccountAsync(context, company, accountTypes, result);

            case LeadConversionAccountMode.CreateNew:
                var name = string.IsNullOrWhiteSpace(request.AccountName) ? leadPart.Company?.Trim() : request.AccountName.Trim();

                if (string.IsNullOrEmpty(name))
                {
                    result.Errors.Add(S["Enter the name of the new account. The lead has no company to name it after."]);

                    return false;
                }

                return await CreateAccountAsync(context, name, accountTypes, result);
        }

        return true;
    }

    private async Task<bool> CreateAccountAsync(LeadConversionContext context, string name, ContentTypeDefinition[] accountTypes, LeadConversionResult result)
    {
        var accountType = accountTypes.FirstOrDefault(type => type.Name == OmnichannelConstants.ContentTypes.Account) ?? accountTypes.FirstOrDefault();

        if (accountType is null)
        {
            result.Errors.Add(S["There is no account type to create the account with."]);

            return false;
        }

        var account = await _contentManager.NewAsync(accountType.Name);

        account.DisplayText = name;

        if (accountType.Parts.Any(part => part.PartDefinition?.Name == nameof(TitlePart)))
        {
            account.Alter<TitlePart>(part => part.Title = name);
        }

        context.Account = account;
        result.AccountCreated = true;

        return true;
    }

    private async Task<bool> BuildOpportunityAsync(
        LeadConversionContext context,
        LeadPart leadPart,
        LeadPartSettings leadSettings,
        LeadConversionResult result)
    {
        var request = context.Request;
        var opportunityType = request.OpportunityContentType ?? leadSettings.DefaultOpportunityContentType;
        var opportunityTypeDefinition = string.IsNullOrEmpty(opportunityType)
            ? null
            : await _contentDefinitionManager.GetTypeDefinitionAsync(opportunityType);

        if (!OmnichannelRecordKinds.IsOpportunity(opportunityTypeDefinition))
        {
            result.Errors.Add(S["Choose the opportunity type to create."]);

            return false;
        }

        var stages = OpportunityStages.ForType(await _opportunityStages.GetAllAsync(), opportunityTypeDefinition);
        var stage = stages.FirstOrDefault(entry => entry.ItemId == request.OpportunityStageId)
            ?? stages.FirstOrDefault(entry => !entry.IsClosed);

        var opportunity = await _contentManager.NewAsync(opportunityType);
        var name = !string.IsNullOrWhiteSpace(request.OpportunityName)
            ? request.OpportunityName.Trim()
            : $"{(string.IsNullOrWhiteSpace(leadPart.Company) ? context.Lead.DisplayText : leadPart.Company.Trim())} - {_clock.UtcNow:yyyy-MM-dd}";

        opportunity.DisplayText = name;

        if (opportunityTypeDefinition.Parts.Any(part => part.PartDefinition?.Name == nameof(TitlePart)))
        {
            opportunity.Alter<TitlePart>(part => part.Title = name);
        }

        opportunity.Alter<OpportunityPart>(part =>
        {
            part.StageId = stage?.ItemId;
            part.Probability = stage?.Probability;
            part.Amount = request.OpportunityAmount;
            part.CloseDate = request.OpportunityCloseDate?.Date;
            part.OwnerId = leadPart.OwnerId ?? request.UserId;
            part.Source = leadPart.Source;
            part.CampaignId = request.CampaignId;
            part.PrimaryContactItemId = context.ContactCreated ? null : context.Contact.ContentItemId;
            part.ConvertedFromLeadItemId = context.Lead.ContentItemId;
        });

        if (context.Account is not null)
        {
            PlaceInAccount(opportunity, context.Account);
        }

        context.Opportunity = opportunity;

        return true;
    }

    private async Task MoveActivitiesAsync(
        LeadConversionContext context,
        ContentTypeDefinition contactTypeDefinition,
        LeadConversionResult result,
        CancellationToken cancellationToken)
    {
        var leadId = context.Lead.ContentItemId;
        var activities = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.ContactContentItemId == leadId,
                collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var activity in activities)
        {
            activity.ConvertedFromLeadItemId = leadId;
            activity.ContactContentItemId = context.Contact.ContentItemId;
            activity.ContactContentType = contactTypeDefinition.Name;

            var isOpen = !_finishedStatuses.Contains(activity.Status);

            if (isOpen &&
                context.Request.OpenActivities == LeadOpenActivityMode.Cancel &&
                !string.Equals(activity.ItemId, context.Request.CompletingActivityId, StringComparison.Ordinal))
            {
                activity.Status = ActivityStatus.Cancelled;
                activity.TerminalReasonCode = OmnichannelConstants.TerminalReasons.LeadConverted;
                result.CancelledActivities++;
            }
            else
            {
                result.MovedActivities++;
            }

            await _session.SaveAsync(activity, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
        }
    }

    private async Task CloseLeadAsync(LeadConversionContext context)
    {
        var convertedStatus = (await _leadStatuses.GetAllAsync()).FirstOrDefault(status => status.IsConverted);
        var lead = context.Lead;

        lead.Alter<LeadPart>(part =>
        {
            part.IsConverted = true;
            part.IsClosed = true;
            part.StatusId = convertedStatus?.ItemId ?? part.StatusId;
            part.ConvertedUtc = _clock.UtcNow;
            part.ConvertedById = context.Request.UserId;
            part.ConvertedByUsername = context.Request.UserName;
            part.ConvertedContactItemId = context.Contact.ContentItemId;
            part.ConvertedContactType = context.Contact.ContentType;
            part.ConvertedAccountItemId = context.Account?.ContentItemId;
            part.ConvertedOpportunityItemId = context.Opportunity?.ContentItemId;
        });

        await _contentManager.UpdateAsync(lead);

        if (lead.Published || !(await _contentManager.HasPublishedVersionAsync(lead)))
        {
            await _contentManager.PublishAsync(lead);
        }
    }

    private async Task<bool> HasLiveActivityAsync(string leadId, string completingActivityId, CancellationToken cancellationToken)
    {
        var live = await _session.QueryIndex<OmnichannelActivityIndex>(
                index => index.ContactContentItemId == leadId && index.Status.IsIn(_liveStatuses),
                collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        return live.Any(index => !string.Equals(index.ItemId, completingActivityId, StringComparison.Ordinal));
    }

    private async Task SaveAsync(ContentItem contentItem, bool isNew)
    {
        if (contentItem is null)
        {
            return;
        }

        if (isNew)
        {
            await _contentManager.CreateAsync(contentItem, VersionOptions.Published);

            return;
        }

        await _contentManager.UpdateAsync(contentItem);

        if (contentItem.Published || !(await _contentManager.HasPublishedVersionAsync(contentItem)))
        {
            await _contentManager.PublishAsync(contentItem);
        }
    }

    private static bool BelongsToAccount(ContentItem contentItem)
        => contentItem.TryGet<ContainedPart>(out var contained) && !string.IsNullOrEmpty(contained.ListContentItemId);

    private static void PlaceInAccount(ContentItem contentItem, ContentItem account)
    {
        contentItem.Alter<ContainedPart>(part =>
        {
            part.ListContentItemId = account.ContentItemId;
            part.ListContentType = account.ContentType;
        });
    }

    /// <summary>
    /// Copies the lead's parts and fields onto the contact where the contact's type has the same part or a field of
    /// the same name and kind. When the lead is merged into an existing contact, only what the contact lacks is
    /// copied, so a lead's rough data never overwrites a clean contact's values.
    /// </summary>
    internal static void CopyMatchingParts(
        ContentItem lead,
        ContentTypeDefinition leadDefinition,
        ContentItem contact,
        ContentTypeDefinition contactDefinition,
        bool overwrite)
    {
        if (leadDefinition is null || contactDefinition is null)
        {
            return;
        }

        var leadJson = (JsonObject)lead.Content;
        var contactJson = (JsonObject)contact.Content;

        foreach (var leadPart in leadDefinition.Parts)
        {
            if (_partsNotCopied.Contains(leadPart.Name) || _partsNotCopied.Contains(leadPart.PartDefinition?.Name ?? string.Empty))
            {
                continue;
            }

            if (leadJson[leadPart.Name] is not JsonObject leadPartJson)
            {
                continue;
            }

            // Each type keeps its own fields in a part named after the type, so a lead type's fields line up with the
            // contact type's fields by name and kind rather than by part.
            if (leadPart.PartDefinition?.Name == leadDefinition.Name)
            {
                var contactOwnPart = contactDefinition.Parts.FirstOrDefault(part => part.PartDefinition?.Name == contactDefinition.Name);

                if (contactOwnPart is not null)
                {
                    CopyMatchingFields(leadPart, leadPartJson, contactOwnPart, contactJson, overwrite);
                }

                continue;
            }

            var contactPart = contactDefinition.Parts.FirstOrDefault(part =>
                part.Name == leadPart.Name &&
                part.PartDefinition?.Name == leadPart.PartDefinition?.Name);

            if (contactPart is null)
            {
                continue;
            }

            if (!overwrite && contactJson[contactPart.Name] is JsonObject existing && existing.Count > 0)
            {
                continue;
            }

            var copy = leadPartJson.DeepClone();
            RegenerateNestedIds(copy);
            contactJson[contactPart.Name] = copy;
        }
    }

    private static void CopyMatchingFields(
        ContentTypePartDefinition leadPart,
        JsonObject leadPartJson,
        ContentTypePartDefinition contactPart,
        JsonObject contactJson,
        bool overwrite)
    {
        if (contactJson[contactPart.Name] is not JsonObject contactPartJson)
        {
            contactPartJson = [];
            contactJson[contactPart.Name] = contactPartJson;
        }

        foreach (var leadField in leadPart.PartDefinition.Fields)
        {
            var contactField = contactPart.PartDefinition.Fields.FirstOrDefault(field =>
                field.Name == leadField.Name &&
                field.FieldDefinition?.Name == leadField.FieldDefinition?.Name);

            if (contactField is null || leadPartJson[leadField.Name] is not JsonNode value)
            {
                continue;
            }

            if (!overwrite && contactPartJson[contactField.Name] is JsonObject existing && HasValue(existing))
            {
                continue;
            }

            contactPartJson[contactField.Name] = value.DeepClone();
        }
    }

    private static bool HasValue(JsonObject field)
        => field.Any(property => property.Value switch
        {
            null => false,
            JsonValue scalar => !string.IsNullOrWhiteSpace(scalar.ToString()),
            JsonArray array => array.Count > 0,
            JsonObject obj => obj.Count > 0,
            _ => true,
        });

    /// <summary>
    /// Adds the lead's phone numbers and email addresses to the contact. A new contact takes all of them; a contact
    /// the lead is merged into keeps its own and gains only the ones it does not already have. Every copied method
    /// gets new identifiers, because two methods sharing one make the contact editor drop its changes.
    /// </summary>
    internal static void MergeContactMethods(ContentItem lead, ContentItem contact)
    {
        if (!lead.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var leadMethods) ||
            leadMethods.ContentItems is not { Count: > 0 })
        {
            return;
        }

        var contactMethods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        contactMethods.ContentItems ??= [];

        var known = contactMethods.ContentItems
            .Select(GetContactMethodKey)
            .Where(key => key is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var method in leadMethods.ContentItems)
        {
            var key = GetContactMethodKey(method);

            if (key is not null && !known.Add(key))
            {
                continue;
            }

            contactMethods.ContentItems.Add(CloneContactMethod(method));
        }

        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, contactMethods);
    }

    private static ContentItem CloneContactMethod(ContentItem method)
    {
        var clone = new ContentItem
        {
            ContentType = method.ContentType,
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            DisplayText = method.DisplayText,
        };

        var cloneJson = (JsonObject)clone.Content;

        foreach (var (name, value) in (JsonObject)method.Content)
        {
            if (value is null || cloneJson.ContainsKey(name))
            {
                continue;
            }

            cloneJson[name] = value.DeepClone();
        }

        return clone;
    }

    private static string GetContactMethodKey(ContentItem method)
    {
        if (method.TryGet<PhoneNumberInfoPart>(out var phone) && !string.IsNullOrWhiteSpace(phone.Number?.PhoneNumber))
        {
            return "phone:" + PhoneNumberSearchTerm.NormalizeDigits(phone.Number.PhoneNumber);
        }

        if (method.TryGet<EmailInfoPart>(out var email) && !string.IsNullOrWhiteSpace(email.Email?.Text))
        {
            return "email:" + email.Email.Text.Trim();
        }

        return null;
    }

    /// <summary>
    /// Carries the lead's opt-outs to the contact. A flag set on either record stays set, with the earliest time it
    /// was set, so converting a lead can never undo a request not to be called, texted or emailed.
    /// </summary>
    internal static void MergeCommunicationPreferences(ContentItem lead, ContentItem contact)
    {
        if (!lead.TryGet<OmnichannelContactPart>(out var leadPart))
        {
            return;
        }

        contact.Alter<OmnichannelContactPart>(contactPart =>
        {
            if (string.IsNullOrEmpty(contactPart.TimeZoneId))
            {
                contactPart.TimeZoneId = leadPart.TimeZoneId;
            }

            (contactPart.DoNotCall, contactPart.DoNotCallUtc) = Merge(contactPart.DoNotCall, contactPart.DoNotCallUtc, leadPart.DoNotCall, leadPart.DoNotCallUtc);
            (contactPart.DoNotSms, contactPart.DoNotSmsUtc) = Merge(contactPart.DoNotSms, contactPart.DoNotSmsUtc, leadPart.DoNotSms, leadPart.DoNotSmsUtc);
            (contactPart.DoNotEmail, contactPart.DoNotEmailUtc) = Merge(contactPart.DoNotEmail, contactPart.DoNotEmailUtc, leadPart.DoNotEmail, leadPart.DoNotEmailUtc);
        });
    }

    private static (bool Value, DateTime? Utc) Merge(bool contactValue, DateTime? contactUtc, bool leadValue, DateTime? leadUtc)
    {
        if (!contactValue && !leadValue)
        {
            return (false, contactUtc);
        }

        if (contactValue && leadValue)
        {
            return (true, Earliest(contactUtc, leadUtc));
        }

        return contactValue ? (true, contactUtc) : (true, leadUtc);
    }

    private static DateTime? Earliest(DateTime? first, DateTime? second)
    {
        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        return first < second ? first : second;
    }

    private static void RegenerateNestedIds(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj["ContentItems"] is JsonArray items)
            {
                foreach (var item in items.OfType<JsonObject>())
                {
                    item["ContentItemId"] = IdGenerator.GenerateId();
                    item["ContentItemVersionId"] = IdGenerator.GenerateId();
                }
            }

            foreach (var (_, value) in obj)
            {
                RegenerateNestedIds(value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var value in array)
            {
                RegenerateNestedIds(value);
            }
        }
    }

    private static T GetPartSettings<T>(ContentTypeDefinition definition, string partName)
        where T : class, new()
    {
        var part = definition?.Parts.FirstOrDefault(entry => entry.PartDefinition?.Name == partName);

        return part?.GetSettings<T>() ?? new T();
    }
}
