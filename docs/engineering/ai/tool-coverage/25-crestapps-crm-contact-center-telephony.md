# CrestApps: CRM, contact center and telephony

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet. The design rules, the permission and approval model and the test plan that apply to every tool below are in [01-foundation.md](01-foundation.md) and [03-testing.md](03-testing.md); the final agent roster is in [02-agents.md](02-agents.md) and wins over the agent notes in this file.

Scope: 17 module folders under `CrestApps.OrchardCore/src/Modules` (Omnichannel, Omnichannel.Managements, Omnichannel.EventGrid, Omnichannel.Messaging, Omnichannel.Messaging.Azure, Omnichannel.Messaging.Sms, Omnichannel.Sms, Omnichannel.Voice, ContactCenter, Telephony, Telephony.Azure, Telnyx, Asterisk, DialPad, DncRegistry, DncRegistry.Azure, Outreach) plus their `src/Core/*` and `src/Abstractions/*` projects.

Conventions used in every section below:

- Almost every configuration record in this group is a catalog record (`CatalogItem`) managed by an `ICatalogManager<T>` (`CrestApps.Core.Services`), edited in the UI through `CatalogAdminController<T>` and a display driver, validated in a `CatalogEntryHandlerBase<T>.ValidatingAsync` handler, and importable through a `CatalogRecipeStep<T>` recipe step. A `saveX` tool for such a record should follow the pattern already used by `ChannelEndpointsApiEndpoints` (`src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Endpoints/Api/ChannelEndpointsApiEndpoints.cs`): `manager.NewAsync(JsonObject)` → `manager.ValidateAsync` → `manager.CreateAsync`, and for an update validate a merged copy first, then `manager.UpdateAsync(entry, JsonObject)`. The display drivers contain no validation (`AddModelError` count is 0 in the queue, entry point, dialer profile, business hours and reason code drivers), so going through the manager loses nothing.
- Recipe steps exist for nearly all of these records, so `importOrchardCoreRecipe` can already create or update them. It cannot list, read back, or delete them, and it gives the model no per-record schema, which is why dedicated `list`/`get`/`save` tools are still proposed.
- All tools for a CrestApps module go in that module's own `AIToolsStartup` gated by `[RequireFeatures("CrestApps.OrchardCore.AI")]`, plus the `[Feature(...)]` of the feature that owns the record.
- Existing tools in this group (all registered `.Hidden()`, given to an automated conversation by the conversation itself, never selectable): `transferToLiveAgent` (Omnichannel.Managements, `Startup.cs`), `endCall`, `collectSecureDigits`, `pressKeys` (Omnichannel.Voice, `Startup.cs`). They are in-call tools for the AI voice/SMS agent and are not admin tools; nothing below replaces them.

## Omnichannel activities — `CrestApps.OrchardCore.Omnichannel.Activities` (headless services), `CrestApps.OrchardCore.Omnichannel.Managements` (screens)

**What an admin can do in the UI:**

- Interaction Center → Activities (`ActivitiesController.Activities`): the signed-in user's scheduled manual activities, filtered by urgency, subject type, channel, attempts, contact time zone and scheduled range (`ListOmnichannelActivityFilter`).
- A contact's (or account's) activity page (`List`, `Account`): scheduled and completed activities of that contact; Create a scheduled activity (`Create`), log an inbound activity (`CreateInbound`), Edit, Complete (disposition + notes + the subject's form), Purge.
- Interaction Center → Management → Manage Activities (`ManageActivities`): filter (`BulkManageActivityFilter`: subject, channel, source, interaction type, status, assignment status, campaign, dialer profile, assigned users, scheduled/created ranges, urgency, phone number, time zones, do-not-call dates, contact published, attempts, limit) and run a bulk action on the selected rows or on everything that matches: Assign, Reschedule, Purge, SetInstructions, SetUrgencyLevel, ChangeSubject, ClearAssignment, ChangeSource, ChangeDialerProfile, ChangeCampaign (`BulkActivityAction`).

**Already covered:** nothing for activities (they are not content items). Contacts, leads, accounts and opportunities are content items, so the content tools cover reading and editing them. Workflow activities `CreateAIActivityTask`, `UpdateContactTask` are reachable through `createOrUpdateWorkflow`. Reports (Activity summary, Campaign performance, Disposition breakdown, Dialer call detail, Handoff containment, Enterprise activity) run through the Reports framework (`AddReport<...>` in `Startup.cs`), not through this group's tools.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listOmnichannelActivities` | Read | no | `ListActivities` (scope `mine`), `ListContactActivities` on the contact (scope `contact`), `ManageActivities` (scope `all`) | Lists CRM activities with paging. Scope `mine` returns the caller's scheduled manual activities, `contact` returns one contact's scheduled or completed activities, `all` applies the Manage Activities filters (status, campaign, dialer profile, assignee, dates, phone number). Use it to find activity ids before any change. | CrmAgent |
| `getOmnichannelActivity` | Read | no | `ListActivities` | Returns one activity: contact, subject type, channel, schedule, assignment, status, attempts, disposition, notes, hand-off summary and whether an AI session ran it. | CrmAgent |
| `saveOmnichannelActivity` | Write | no | `EditActivity` | Creates a scheduled outbound activity for a contact, logs an inbound one, or edits a pending activity (subject type, schedule, assignee, instructions, urgency, campaign). Fails when no outbound subject is configured or the lead was converted. Does not dial or send anything by itself. | CrmAgent |
| `completeOmnichannelActivity` | Write | no | `CompleteActivity` (or `CompleteOwnActivity` for the caller's own) | Completes an activity with a disposition and notes. The disposition's subject actions then run (follow-up activity, retry, do-not-contact flags, lead conversion), so read the subject flow first when unsure. | CrmAgent |
| `bulkUpdateOmnichannelActivities` | Write | no | `ManageActivities` | Applies one bulk action (assign, clearAssignment, reschedule, setInstructions, setUrgency, changeSubject, changeSource, changeDialerProfile, changeCampaign) to a list of activity ids or to every activity matching a filter. Activities the dialer is dialing or an agent is on are skipped and reported. | CrmAgent |
| `purgeOmnichannelActivities` | Destructive | yes | `PurgeActivity` | Purges activities by id or by filter so they are never worked. Cannot be undone; activities on a live call are skipped. | CrmAgent |

**Implementation notes:** Reads: `IOmnichannelActivityManager.PageManualScheduledAsync`, `PageContactManualScheduledAsync`, `PageContactManualCompletedAsync`, `PageBulkManageableAsync`, `GetBulkManageableAsync`, `FindByIdAsync` (`src/Core/CrestApps.OrchardCore.Omnichannel.Core/Services`). Complete: `IActivityDispositionService.ApplyAsync(ActivityDispositionRequest)`; the UI also binds the subject content item's editor, which a tool must replace with a JSON subject payload. Gaps: (1) the ten bulk actions are private methods of `ActivitiesController` (`BulkAssignAsync`, `BulkRescheduleAsync`, `BulkPurgeAsync`, …) together with the in-flight guard `RemoveDialerWorkInFlightAsync` and the `TryGetManuallyAssignableSource` rule — they must move to a service before a tool can call them; (2) create/edit binds through `OmnichannelActivityDisplayDriver` and `OmnichannelActivityEditRules`, so the field rules must be reused, not re-implemented; (3) purge is `ActivityPurgeHelper.Purge` + `UpdateAsync` in the controller. Authorization is resource-based (`AuthorizeAsync(User, permission, activity)`), keep that.

**Test prompts:**

- "What activities do I have scheduled for today?"
- "Schedule a follow-up call for the contact Jane Doe tomorrow at 10 and assign it to me."
- "Move every pending activity of the Spring campaign that is assigned to Mark over to Lisa."
- "Purge all pending activities that were created from the March import."

## Omnichannel activity loads (batches) — `CrestApps.OrchardCore.Omnichannel.Managements`

**What an admin can do in the UI:**

- Interaction Center → Management → Load Activities (`ActivityBatchesController`): list and search loads; create one per source (`ActivityBatchSourceOptions`), edit, Save, Save & Load, clone, delete, bulk remove.
- Count matches before loading (`CountMatches` → `IActivityBatchLoadCoordinator.PreviewAsync`).
- Load (`Load` → background `IActivityBatchLoadCoordinator.LoadAsync`): creates one activity per matching contact or lead; the list then shows loaded, matched and skipped counts (duplicate, opted out, shared number opted out, no destination, not in service, limit, converted, existing contact).
- Pick a lead import to load from (`LeadImports`).

**Already covered:** nothing. No recipe step exists for a batch.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listActivityBatches` | Read | no | `ManageActivityBatches` | Lists activity loads with their source, campaign, channel, status (new, started, loading, loaded), totals loaded/matched/skipped and any load error. Pass an id to get the full definition of one load. | CrmAgent |
| `saveActivityBatch` | Write | no | `ManageActivityBatches` | Creates or updates an activity load definition: source, campaign, channel, sending address, subject and contact types, AI profile, cadence, dialer profile, assignees, schedule, urgency, filters and limit. `cloneFromId` copies an existing load. Saving does not load it. | CrmAgent |
| `previewActivityBatch` | Read | no | `ManageActivityBatches` | Counts how many records a saved or draft load would match and how many would be skipped and why, without creating anything. Use it before `loadActivityBatch`. | CrmAgent |
| `loadActivityBatch` | External | yes | `ManageActivityBatches` | Starts loading a saved batch in the background, creating one activity per match. For an automated or dialer batch this leads to real calls or text messages to customers. A batch can be loaded only once. | CrmAgent |
| `deleteActivityBatch` | Destructive | yes | `ManageActivityBatches` (a loaded batch also needs `DeleteLoadedActivityBatches`) | Deletes an activity load record. Refused while it is loading. The activities it already created are kept. | CrmAgent |

**Implementation notes:** `ICatalogManager<OmnichannelActivityBatch>`, `IActivityBatchLoadCoordinator.PreviewAsync/LoadAsync` (`Services/IActivityBatchLoadCoordinator.cs`). Gaps: the start-load sequence (`StartLoadAsync`: reset progress, set `Started`, stamp the loader, then `HttpBackgroundJob.ExecuteAfterEndOfRequestAsync`) and the delete rules (`GetDeleteRefusalAsync`) are private to the controller and must be extracted; the editor binds through `OmnichannelActivityBatchDisplayDriver` plus source-specific drivers (`LeadBatchFilterDisplayDriver`, `LeadAIConversionBatchDisplayDriver`), so the tool schema must be built per source. Risk: a load of an automated batch is the trigger for outbound contact at scale; keep approval and show the preview counts in the approval text.

**Test prompts:**

- "How many leads would the 'June leads' load create activities for?"
- "Copy the load 'Renewals May' as 'Renewals June' for the same campaign."
- "Load the 'Renewals June' batch now."

## Omnichannel campaigns, campaign groups, dispositions and cadences — `CrestApps.OrchardCore.Omnichannel.Managements`

**What an admin can do in the UI:**

- Management → Campaigns (`CampaignsController`): list, create, edit. No delete route is mapped.
- Management → Campaign Groups (`CampaignGroupsController`): list, create, edit. No delete route is mapped.
- Management → Dispositions (`DispositionsController`): list, create, edit, delete. A disposition has `Name`, `Description`, `CaptureDate`, `Outcome`.
- Management → Cadences (`CadencesController`): list, create, edit, delete. A cadence has ordered steps (`DelayMinutes`, `IsAiGenerated`, `Message`) and `Enabled`.

**Already covered:** recipe steps `OmnichannelCampaign`, `OmnichannelCampaignGroup`, `OmnichannelDisposition`, `OmnichannelCadence` (usable through `importOrchardCoreRecipe`; create/update only).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listOmnichannelCampaigns` | Read | no | `ManageCampaigns` | Lists campaigns with group, interaction type (manual or automated), channel and AI settings summary. Pass an id for the full record. | OmnichannelSetupAgent |
| `saveOmnichannelCampaign` | Write | no | `ManageCampaigns` | Creates or updates a campaign: name, description, group, interaction type, channel, goal, opening prompt pattern, AI connection/deployment, system message, model parameters, tool names and whether the AI may update the contact or subject. | OmnichannelSetupAgent |
| `listOmnichannelCampaignGroups` | Read | no | `ManageCampaignGroups` | Lists campaign groups. | OmnichannelSetupAgent |
| `saveOmnichannelCampaignGroup` | Write | no | `ManageCampaignGroups` | Creates or renames a campaign group. | OmnichannelSetupAgent |
| `listOmnichannelDispositions` | Read | no | `ManageDispositions` | Lists dispositions with their outcome and whether they capture a date. Use it to find the disposition id for `completeOmnichannelActivity` or a subject action. | OmnichannelSetupAgent |
| `saveOmnichannelDisposition` | Write | no | `ManageDispositions` | Creates or updates a disposition (name, description, outcome, capture date). | OmnichannelSetupAgent |
| `listOmnichannelCadences` | Read | no | `ManageCadences` | Lists follow-up cadences with their steps. | OmnichannelSetupAgent |
| `saveOmnichannelCadence` | Write | no | `ManageCadences` | Creates or updates a cadence and its ordered steps (delay in minutes, fixed message or AI-generated). A cadence drives automated follow-up messages for the activities that use it. | OmnichannelSetupAgent |
| `deleteOmnichannelRecord` | Destructive | yes | Per kind: `ManageDispositions`, `ManageCadences`, `ManageSubjectFlows`, `ManageLeadStatuses`, `ManageOpportunityStages` | Deletes one omnichannel configuration record by kind (disposition, cadence, subjectAction, leadStatus, opportunityStage) and id. Campaigns, campaign groups and addresses cannot be deleted. | OmnichannelSetupAgent |

**Implementation notes:** `ICatalogManager<OmnichannelCampaign>`, `ICatalogManager<OmnichannelCampaignGroup>`, `ICatalogManager<OmnichannelDisposition>`, `ICatalogManager<Cadence>`. The single `deleteOmnichannelRecord` tool folds five deletes that share the same arguments and risk; it must check the permission of the kind it is given. Campaign `ToolNames` grants tools to an automated conversation, so `saveOmnichannelCampaign` changes what an AI agent can do on live customer conversations — consider treating a change to `ToolNames`, `SystemMessage` or `AllowAIToUpdate*` as `Security` (open question).

**Test prompts:**

- "Create a campaign called 'Winter renewals' in the Retention group, manual phone calls."
- "Which dispositions do we have and which ones count as a successful outcome?"
- "Add a third step to the 'Lead nurture' cadence that sends an AI-written message two days later."

## Omnichannel subject flows — `CrestApps.OrchardCore.Omnichannel.Managements`

**What an admin can do in the UI:**

- Management → Subject Flows (`SubjectFlowsController.Index`): the content types that carry `OmnichannelSubjectPart`, with direction, interaction type, channel and whether they have actions.
- Manage a subject's actions (`ManageActions`): list; create an action by type (`Finish`, `TryAgain`, `NewActivity`, and `ConvertLead` when the CRM feature is on); edit; delete. Each action is keyed by disposition and can set do-not-call / do-not-SMS / do-not-email.
- The flow settings themselves (direction, channel, sending address, AI profile, require disposition, hand-off rules, SMS opt-out keywords…; `SubjectFlowSettings`) are part settings of `OmnichannelSubjectPart` on the subject content type.

**Already covered:** recipe step `OmnichannelSubjectAction` (via `importOrchardCoreRecipe`). Subject flow settings are content-type part settings, so `getContentTypeDefinition` / `applyContentTypeDefinitionFromRecipe` cover them.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listSubjectFlows` | Read | no | `ManageSubjectFlows` | Lists the subject content types with their flow settings and, for one subject, every action per disposition (type, what it does, do-not-contact flags). Use it to explain or audit what happens after a disposition. | OmnichannelSetupAgent |
| `saveSubjectAction` | Write | no | `ManageSubjectFlows` | Creates or updates one action of a subject flow: the subject type, the disposition that triggers it, the action type (Finish, TryAgain, NewActivity, ConvertLead) and its type-specific settings. | OmnichannelSetupAgent |

**Implementation notes:** `ISubjectFlowSettingsService` (`GetConfiguredSubjectTypesAsync`, `FindConfiguredFlowSettingsAsync`), the `SubjectAction` catalog (`_actionCatalog`), `SubjectActionOptions.ActionTypes`. Type-specific metadata is bound by `TryAgainSubjectActionDisplayDriver`, `NewActivitySubjectActionDisplayDriver`, `LeadSubjectActionDisplayDriver` (`TryAgainActionMetadata`, `NewActivityActionMetadata`, `LeadSubjectActionMetadata`); the tool needs a schema per action type. Delete goes through `deleteOmnichannelRecord` (kind `subjectAction`).

**Test prompts:**

- "What happens when an agent picks 'No answer' on a Sales Call?"
- "For the Sales Call subject, make 'Not interested' also mark the contact as do not call."

## Omnichannel numbers not in service — `CrestApps.OrchardCore.Omnichannel.Managements`

**What an admin can do in the UI:** Management → Numbers Not In Service (`NotInServiceNumbersController`): search the numbers detected or marked as not in service (source, reason, first/last detected, count); mark a number by hand; allow dialing again (single or bulk).

**Already covered:** nothing.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listNotInServiceNumbers` | Read | no | `ManageActivities` | Lists or searches phone numbers marked as not in service, with who or what marked them and when. | CrmAgent |
| `setNumberNotInService` | Write | no | `ManageActivities` | Marks one or more phone numbers as not in service so campaigns skip them, or clears the mark so they can be loaded and dialed again. Numbers must include the country code. | CrmAgent |

**Implementation notes:** `INotInServiceNumberService.PageAsync`, `MarkAsync(NotInServiceMark)` with source `OmnichannelConstants.NotInServiceSources.Manual`, `ClearAsync`.

**Test prompts:**

- "Is +1 702 555 0123 marked as not in service?"
- "Allow dialing again for these three numbers: …"

## Automated AI calls and texts — `CrestApps.OrchardCore.Omnichannel.Activities` with `CrestApps.OrchardCore.Omnichannel.Voice` / `CrestApps.OrchardCore.Omnichannel.Sms`

**What an admin can do in the UI:** there is no dedicated screen; an automated activity is created by an automated activity load, a workflow (`CreateAIActivityTask`) or the public API (`AIActivitiesApiEndpoints`: start a call, start a text, start a batch of up to `ApiAIActivityBatchRequest.MaxItems`, read an activity or batch, cancel a pending one).

**Already covered:** workflow task `CreateAIActivityTask` through `createOrUpdateWorkflow`. `sendSmsMessage` (AI.Agent) sends a plain SMS through Orchard's SMS service without an AI conversation or an activity.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `startAIActivity` | External | yes | `EditActivity` | Starts an automated AI phone call or AI text conversation to one phone number (E.164) with a given AI profile, optionally from a chosen address, for a contact, campaign and subject, now or at a scheduled time. Contacts a real customer and uses paid telephony and model usage. | CrmAgent |
| `cancelAIActivity` | Write | no | `EditActivity` | Cancels an automated call or text that has not started yet (status not started, pending or scheduled). Returns a conflict when it is already in progress or finished. | CrmAgent |

**Implementation notes:** `AIActivityStarter.StartAsync(AIActivityStartRequest)` (`src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Services/AIActivityStarter.cs`, registered with `TryAddScoped`), with `Source = ActivitySources.Api` replaced by a source for the chat widget (a new registered source may be needed, see `ActivitySourceOptions`). Cancel mirrors `AIActivitiesApiEndpoints.CancelAsync` (`CanCancel`). Status is read with `getOmnichannelActivity`. A batch start is deliberately not proposed: use an activity load for volume.

**Test prompts:**

- "Have the appointment-confirmation AI call +1 555 010 2030 now."
- "Cancel the AI call that is scheduled for 3 pm to the Smith contact."

## Omnichannel addresses (channel endpoints) — `CrestApps.OrchardCore.Omnichannel.ChannelEndpoints`

**What an admin can do in the UI:** Management → Omnichannel Addresses (`ChannelEndpointsController`): list and search; create by address type (`OmnichannelAddressOptions.AddressTypes`), clone, edit. The editor is extended by other features: outbound line agents (`OutboundLineEndpointDisplayDriver`, Contact Center Voice), messaging line settings (`MessagingLineEndpointDisplayDriver`), per-number SMS provider (`SmsEndpointProviderDisplayDriver`), provider number details (`TelnyxNumberDisplayDrivers`). The admin UI has no delete; the public API (`ChannelEndpointsApiEndpoints`) does.

**Already covered:** recipe step `OmnichannelChannelEndpoint` (via `importOrchardCoreRecipe`).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listOmnichannelAddresses` | Read | no | `ManageChannelEndpoints` | Lists the business's own numbers and addresses with channel, capabilities, provider and value; pass an id for the full record including outbound-line agents and messaging settings. Use it to find the address id that entry points, loads and messages send from. | OmnichannelSetupAgent |
| `saveOmnichannelAddress` | Write | no | `ManageChannelEndpoints` | Creates or updates an omnichannel address: display name, address type, value, capabilities (phone, SMS, …), provider and the feature-specific settings. An agent can hold only one number per channel; validation refuses a conflict. | OmnichannelSetupAgent |

**Implementation notes:** `ICatalogManager<OmnichannelChannelEndpoint>` / `IOmnichannelChannelEndpointManager`; copy the create/update code of `ChannelEndpointsApiEndpoints`. Rules: `IChannelEndpointRule`, `IChannelEndpointAddressPolicy`. Changing an address re-routes live inbound calls and messages for that number at once.

**Test prompts:**

- "Which phone numbers do we own and which ones can send text messages?"
- "Add +1 555 010 9000 as a phone and SMS address on the Telnyx provider and call it 'Support line'."

## Omnichannel CRM — `CrestApps.OrchardCore.Omnichannel.Crm`

**What an admin can do in the UI:**

- Interaction Center → Leads / Accounts / Opportunities, and Management → Lead Sources (`CrmAdminMenu`): Orchard content lists filtered by type.
- Convert a lead (`LeadsController.Convert`): into a new or existing contact, optionally into a new or existing account, optionally creating an opportunity (name, amount, close date), and choosing what happens to its open activities.
- Management → Lead Statuses (`LeadStatusesController`) and Opportunity Stages (`OpportunityStagesController`): list, create, edit, delete (ordered; flags `IsDefault`, `IsContacted`, `IsClosed`, `IsConverted` / `Probability`, `IsClosed`, `IsWon`).

**Already covered:** content tools cover leads, accounts, opportunities, contacts and lead sources (they are content items with `LeadPart`, `OpportunityPart`, `OmnichannelContactPart`). Recipe steps `OmnichannelLeadStatus`, `OmnichannelOpportunityStage`. Workflow `ConvertLeadTask`. Reports Lead funnel, Lead conversion, Opportunity pipeline run through the Reports framework.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `convertLead` | Write | no | `ConvertLead` (on the lead) | Converts a lead into a contact, optionally attaching it to a new or existing account and creating an opportunity. With `preview` it returns the matching contacts and accounts instead of converting. A converted lead cannot be converted again. | CrmAgent |
| `listLeadStatuses` | Read | no | `ManageLeadStatuses` | Lists lead statuses in order with their default/contacted/closed/converted flags. | OmnichannelSetupAgent |
| `saveLeadStatus` | Write | no | `ManageLeadStatuses` | Creates or updates a lead status (name, order, flags). | OmnichannelSetupAgent |
| `listOpportunityStages` | Read | no | `ManageOpportunityStages` | Lists opportunity pipeline stages in order with probability and closed/won flags. | OmnichannelSetupAgent |
| `saveOpportunityStage` | Write | no | `ManageOpportunityStages` | Creates or updates an opportunity stage (name, order, probability, closed, won). | OmnichannelSetupAgent |

**Implementation notes:** `ILeadConversionService.ConvertAsync(LeadConversionRequest)`; match preview uses `LeadMatchFinder.FindContactsAsync/FindAccountsAsync` (module service). `ICatalogManager<LeadStatus>`, `ICatalogManager<OpportunityStage>`. Editing a converted lead needs `EditConvertedLead`; the content tools must keep honouring that.

**Test prompts:**

- "Convert the lead Acme / John Park into a contact under a new account 'Acme Inc.' and open a 12,000 opportunity closing next month."
- "Add a lead status 'Nurturing' after 'Contacted'."

## Omnichannel Messaging workspace — `CrestApps.OrchardCore.Omnichannel.Messaging`, `CrestApps.OrchardCore.Omnichannel.Messaging.RoutedDistribution`, `CrestApps.OrchardCore.Omnichannel.Messaging.Sms`

**What an admin can do in the UI:**

- Messaging → Inbox (`AdminController`): customer-centric inbox with filters (mine, queue, all, unread, channel, agent, queue, campaign); open a conversation and its timeline; mark read; claim; send a reply with optional subject and attachments; set status (open, snoozed, closed); transfer to an agent or queue with a note; favorite; set own messaging availability; compose a new message to one recipient, or to several (queued as a broadcast; needs `SendGroupMessages`); search contacts.
- Messaging → Broadcasts (`BroadcastsController`): list broadcasts with sent/failed/skipped counts; create one (name, sending address, recipients typed or picked from contacts, body). Sending is done by the Outreach engine in the background.
- Messaging → Templates (`TemplatesController`): list, create, edit, delete, bulk remove (`MessageTemplate`: `Name`, `Body`).
- Entry points and addresses get messaging settings from `MessagingEntryPointDisplayDriver` and `MessagingLineEndpointDisplayDriver` (covered by the entry point and address tools).

**Already covered:** `sendSmsMessage` sends an SMS outside any conversation (no thread, no opt-out or quiet-hours handling by the workspace). Recipe steps `OmnichannelMessageTemplate` and `MessagingBroadcasts`. Workflow task `SendTextMessageTask`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listMessagingConversations` | Read | no | `UseMessagingWorkspace` (visibility then follows `ViewOwnMessagingConversations` / `ViewQueueMessagingConversations` / `ViewAllMessagingConversations`) | Lists messaging conversations the caller may see, filtered by scope (mine, queue, all), channel, unread, holder, queue, campaign and open/closed, with last message preview and SLA state. | MessagingAgent |
| `getMessagingConversation` | Read | no | `UseMessagingWorkspace` + view access to the conversation | Returns one conversation with its recent messages, contact, assignment, status, summary and history of transfers. | MessagingAgent |
| `sendMessagingMessage` | External | yes | `UseMessagingWorkspace` + send access to the conversation (`SendMessagesDuringQuietHours` to send outside business hours) | Sends a message to a customer, either as a reply in an existing conversation or as a new conversation from a chosen address to one recipient. Refused when the recipient opted out, the address is invalid, or quiet hours apply. | MessagingAgent |
| `updateMessagingConversation` | Write | no | `UseMessagingWorkspace` + the matching conversation operation (claim, close, snooze) | Changes a conversation's handling state: claim it, set status open/snoozed/closed, mark it read, or set/clear it as a favorite. | MessagingAgent |
| `transferMessagingConversation` | Write | no | `UseMessagingWorkspace` + transfer access to the conversation | Hands a conversation to another agent or to a queue with an optional note. Use the listed transfer targets; an agent who cannot take it is refused. | MessagingAgent |
| `setMessagingAvailability` | Write | no | `UseMessagingWorkspace` | Sets whether the calling user is available to be assigned new messaging conversations. | MessagingAgent |
| `listMessageTemplates` | Read | no | `ManageMessaging` | Lists reusable message templates with their body. | MessagingAgent |
| `saveMessageTemplate` | Write | no | `ManageMessaging` | Creates or updates a message template (name, body). | MessagingAgent |
| `deleteMessageTemplate` | Destructive | yes | `ManageMessaging` | Deletes a message template. | MessagingAgent |
| `listMessagingBroadcasts` | Read | no | `SendGroupMessages` | Lists broadcasts with status and sent, failed and skipped counts. | MessagingAgent |
| `sendMessagingBroadcast` | External | yes | `SendGroupMessages` | Queues one message to many recipients from a chosen address; each recipient gets an individual 1:1 thread. Opted-out or non-consenting recipients are skipped and text messages wait for quiet hours to end. Costs carrier fees per message. | MessagingAgent |

**Implementation notes:** `IMessagingConversationService` (`SendAsync`, `SendDirectAsync`, `ClaimAsync`, `SetStatusAsync`), `IMessagingConversationTransferService.TransferAsync`, `IMessagingAvailabilityService.SetAvailableAsync`, `IMessagingConversationStore`, `IMessageTemplateManager`, `IMessagingBroadcastManager` (creating a broadcast is what queues it; `MessagingBroadcastBackgroundTask` and the Outreach engine send it). Gaps: per-conversation authorization (`MessagingWorkspaceBuilder.AuthorizeAsync(User, conversation, ConversationOperation.X)`), inbox scoping (`MessagingInboxScope`), recipient validation (`MessagingContactSearch.FindInvalidNewAddressesAsync`, `MessagingWorkspaceBuilder.FindUnreachableRecipientsAsync`) and transfer targets (`MessagingTransferTargets`) live in the module's `Services` folder, not in Core; the tools live in the same module so they can use them, but the compose and broadcast validation inside `AdminController.NewPost` and `BroadcastsController.CreatePost` must be extracted. Attachments are not proposed (binary upload). Risk: the `MessagingBroadcasts` recipe step can also queue a broadcast, so `importOrchardCoreRecipe` is an unapproved path to mass sending unless that tool is itself approval-gated.

**Test prompts:**

- "Show me my unread text conversations."
- "Reply to the conversation with +1 555 010 7788: 'Your order ships tomorrow.'"
- "Transfer that conversation to the Billing queue with a note that the customer wants a refund."
- "Text these 40 numbers from the Support line that the office is closed on Friday."

## Contact Center queues and queue groups — `CrestApps.OrchardCore.ContactCenter.Queues`

**What an admin can do in the UI:**

- Interaction Center → Management → Queues (`QueuesController`): list, search, create (also as a clone), edit, delete. A queue (`ActivityQueue`) has group, default priority, routing strategy (`LongestIdle`, `RoundRobin`, `LeastBusy`), sticky agent, SLA threshold and aging, first-response target, reservation timeout, unanswered-offer action, business hours calendar and after-hours action, overflow targets, skill requirements, max wait and queue-full actions, max size, queued-callback settings and in-queue treatment (`QueueTreatmentSettings`).
- Management → Queue groups (`QueueGroupsController`): list, create, edit, delete.

**Already covered:** recipe steps `ContactCenterQueue`, `ContactCenterQueueGroup` (via `importOrchardCoreRecipe`). Workflow tasks `EnqueueActivityTask`, `TransferToAgentTask`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listContactCenterQueues` | Read | no | `ManageContactCenterQueues` | Lists queues with group, routing strategy, SLA threshold, business hours calendar and enabled state; supports a name filter and paging. | ContactCenterSetupAgent |
| `getContactCenterQueue` | Read | no | `ManageContactCenterQueues` | Returns one queue in full, including skill requirements, overflow targets, wait limits, callback settings and in-queue treatment steps. Read it before saving a change. | ContactCenterSetupAgent |
| `saveContactCenterQueue` | Write | no | `ManageContactCenterQueues` | Creates or updates a queue. Only the fields given are changed on an update. Changes apply to live routing immediately, so disable a queue rather than emptying its settings. | ContactCenterSetupAgent |
| `listContactCenterQueueGroups` | Read | no | `ManageContactCenterQueueGroups` | Lists queue groups. | ContactCenterSetupAgent |
| `saveContactCenterQueueGroup` | Write | no | `ManageContactCenterQueueGroups` | Creates or renames a queue group. | ContactCenterSetupAgent |
| `deleteContactCenterRecord` | Destructive | yes | Per kind: `ManageContactCenterQueues` (queue, entryPoint), `ManageContactCenterQueueGroups`, `ManageContactCenterBusinessHours`, `ManageContactCenterAgents` (agentStateReasonCode), `ManageContactCenterDialer`, `ManageContactCenterVoiceMedia` | Deletes one Contact Center configuration record by kind (queue, queueGroup, entryPoint, businessHoursCalendar, agentStateReasonCode, dialerProfile, voiceMedia) and id. Records still referenced by live routing can leave calls without a destination; check references first. | ContactCenterSetupAgent |

**Implementation notes:** `IActivityQueueManager` (`FindByNameAsync`, `GetEnabledAsync`, catalog CRUD), `IActivityQueueGroupManager`. Validation: `ActivityQueueHandler.ValidatingAsync`, `ActivityQueueGroupHandler.ValidatingAsync`. `deleteContactCenterRecord` folds seven deletes with identical arguments and risk; it must authorize per kind and, for voice media, delete provider copies (`IVoiceMediaProvisioningService.DeleteEverywhereAsync` — verify what `VoiceMediaItemHandler` already does).

**Test prompts:**

- "Create a Billing queue in the Support group with a 30 second SLA that overflows to General after 2 minutes."
- "Which queues require the Spanish skill?"
- "Delete the old 'Test' queue."

## Contact Center inbound entry points and inbound voice — `CrestApps.OrchardCore.ContactCenter.EntryPoints`, `CrestApps.OrchardCore.ContactCenter.InboundVoice`

**What an admin can do in the UI:**

- Management → Inbound entry points (`EntryPointsController`): list, create, clone, edit, delete. An entry point (`ContactCenterEntryPoint`) picks a channel and its numbers from the omnichannel addresses, routes to a queue, an agent or an AI agent, sets priority, business hours and closed action (`HoldInQueue`, `Voicemail`, `Overflow`, `Reject`), welcome and closed messages, ring timeout and voicemail (destination, greeting, recipient).
- With Inbound Voice: a phone menu (`IvrFlow`: root node, retries, fallback action, nodes with prompt text or voice media and digit options `RouteToQueue`, `RouteToAgent`, `Voicemail`, `ExternalTransfer`, `Repeat`, `SubMenu`) through `ContactCenterEntryPointVoiceDisplayDriver`.
- Other features add settings to the same editor: messaging (`MessagingEntryPointDisplayDriver`), AI agent for SMS (`SmsEntryPointAIAgentDisplayDriver`) and for voice (`TelnyxEntryPointAIAgentDisplayDriver`).

**Already covered:** recipe step `ContactCenterEntryPoint` (via `importOrchardCoreRecipe`).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listContactCenterEntryPoints` | Read | no | `ManageContactCenterQueues` | Lists inbound entry points with channel, numbers, target (queue, agent or AI agent), business hours and enabled state. Use it to answer "where does this number go?". | ContactCenterSetupAgent |
| `getContactCenterEntryPoint` | Read | no | `ManageContactCenterQueues` | Returns one entry point in full, including its phone menu tree, voicemail and channel-specific settings. Read it before changing a menu. | ContactCenterSetupAgent |
| `saveContactCenterEntryPoint` | Write | no | `ManageContactCenterQueues` | Creates or updates an entry point, including its phone menu. The menu is validated (root node, duplicate node ids, missing prompt or options, invalid or duplicate digits, missing action target, unknown sub-menu). A saved change takes effect on the next inbound call or message to those numbers. | ContactCenterSetupAgent |

**Implementation notes:** `IContactCenterEntryPointManager`, validation in `ContactCenterEntryPointHandler.ValidatingAsync` and `IvrFlowValidator` (`IvrFlowValidationErrorKind`: `RootNodeMissing`, `RootNodeNotFound`, `NodeIdMissing`, `NodeIdDuplicate`, `NodePromptMissing`, `NodeHasNoOptions`, `OptionDigitInvalid`, `OptionDigitDuplicate`, `OptionActionMissing`, `ActionTargetMissing`, `SubMenuNotFound`, `MaxRetriesInvalid`). The phone menu is a nested structure that is easy to break, so the tool should accept the whole `IvrFlow` and return validation errors per node; a partial node patch is not worth the risk. Extension settings live in the entity's `Properties` (e.g. `MessagingEntryPointSettings`, `EntryPointAIAgentOptions`) — the tool must round-trip `Properties` it does not understand. Delete goes through `deleteContactCenterRecord`.

**Test prompts:**

- "Where do calls to +1 555 010 9000 go after hours?"
- "Add option 3 'Billing' to the main phone menu and send it to the Billing queue."
- "Create an entry point for the new sales number that rings the Sales queue and sends closed-hours calls to voicemail."

## Contact Center business hours — `CrestApps.OrchardCore.ContactCenter.BusinessHours`

**What an admin can do in the UI:** Management → Business hours (`BusinessHoursCalendarsController`): list, create, edit, delete calendars (`BusinessHoursCalendar`: time zone, weekly schedule, holidays, enabled).

**Already covered:** recipe step `ContactCenterBusinessHoursCalendar`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listBusinessHoursCalendars` | Read | no | `ManageContactCenterBusinessHours` | Lists business-hours calendars with time zone, weekly schedule and holidays. | ContactCenterSetupAgent |
| `saveBusinessHoursCalendar` | Write | no | `ManageContactCenterBusinessHours` | Creates or updates a business-hours calendar (time zone, opening hours per weekday, holiday dates). Queues, entry points and automated sends that use it follow the change at once. | ContactCenterSetupAgent |
| `checkBusinessHours` | Read | no | `ManageContactCenterBusinessHours` | Tells whether a calendar is open now or at a given date and time. Use it to explain why calls are treated as after hours. | ContactCenterSetupAgent |

**Implementation notes:** `IBusinessHoursCalendarManager`, `IBusinessHoursService.IsOpenAsync(calendarId[, utcInstant])`. Convert the instant from the user's local time with `ILocalClock` (repository rule: never show or accept UTC). Use `listTimeZones` for the time zone id.

**Test prompts:**

- "Close the Support calendar on December 25 and 26."
- "Is the Sales calendar open right now?"

## Contact Center agents, entitlements, agent states and skills — `CrestApps.OrchardCore.ContactCenter.Agents`, `CrestApps.OrchardCore.ContactCenter.AgentEntitlements`, `CrestApps.OrchardCore.ContactCenter.AgentServices`

**What an admin can do in the UI:**

- Management → Agent entitlements (`AgentEntitlementsController`, only with the Agent Entitlements feature): list agents; create entitlements for an Orchard user; edit allowed queues, allowed campaigns, skill proficiencies and queue memberships. No delete.
- Management → Agent states (`AgentStateReasonCodesController`): list, create, edit, delete reason codes (`AgentStateReasonCode`: name, the presence status it applies to, sort order, enabled).
- Skills: `SkillsController` only redirects to the shared Skills module (`SkillsConstants.Features.Skills`); the skill catalog is not owned here.
- A staff member's page shows the agent summary (`AgentStaffDisplayDriver`, read-only).

**Already covered:** recipe steps `ContactCenterAgentEntitlement`, `AgentStateReasonCode`, `ContactCenterSkill`. Workflow task `SetAgentPresenceTask`. Skills belong to the Skills module's section of the plan.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listContactCenterAgents` | Read | no | `ManageContactCenterAgents` | Lists agent profiles with user, presence, signed-in queues and campaigns, allowed queues and campaigns, skills with proficiency, queue memberships and concurrency limit. Filter by user, queue or skill. | ContactCenterSetupAgent |
| `saveContactCenterAgent` | Security | yes | `ManageContactCenterAgents` | Creates the agent profile of an Orchard user or updates which queues and campaigns the agent may sign in to, their skills and proficiency, queue memberships and maximum concurrent interactions. It changes who can receive which customers' calls. | ContactCenterSetupAgent |
| `listAgentStateReasonCodes` | Read | no | `ManageContactCenterAgents` | Lists the reason codes agents pick when changing state (break, away, meeting…). | ContactCenterSetupAgent |
| `saveAgentStateReasonCode` | Write | no | `ManageContactCenterAgents` | Creates or updates an agent state reason code (name, the state it applies to, order, enabled). | ContactCenterSetupAgent |

**Implementation notes:** `IAgentProfileManager` (`FindByUserIdAsync`, `GetMembersForQueueAsync`, catalog list), `IAgentPresenceManager.UpdateEntitlementsAsync(agentId, AgentEntitlements)` and `ApplyManagedConfigurationAsync(agentId, AgentManagedConfiguration)` (the recipe step `ContactCenterAgentEntitlementStep` already shows create-or-update by user name), helpers `AgentEntitlementUtilities.ApplySkills/NormalizeQueueMemberships`, `ContactCenterAdminFormOptionsProvider.FilterExisting*IdsAsync`, `IAgentStateReasonCodeManager`. When the Agent Entitlements feature is off any agent may sign in to any queue (manifest text), so `saveContactCenterAgent` should say so instead of saving allow-lists that are not enforced. Skills are synchronized with the shared Skills module (`SharedSkills/*`); do not write `AgentProfile.Skills` directly.

**Test prompts:**

- "Which agents can take calls from the Billing queue?"
- "Let Maria sign in to the Sales and Renewals queues and give her Spanish at level 4."
- "Add a 'Coaching' reason for the Away state."

## Contact Center outbound dialer — `CrestApps.OrchardCore.ContactCenter.Dialer`, `CrestApps.OrchardCore.ContactCenter.Dialer.Paced`

**What an admin can do in the UI:**

- Management → Dialer Profiles (`DialerProfilesController`): list, create, edit, clone, delete. A profile (`DialerProfile`) sets the mode (`Manual`, `Preview`, `Power`, `Progressive`, `Predictive`), provider, calls per agent, attempts and retry delay, answering-machine detection, ring and preview timeouts, caller id, default region, compliance switches (respect do-not-call, calling window, abandonment cap and rate, safe-harbor message), predictive pacing settings and calling calendars.
- Callbacks have no screen: a `CallbackRequest` is created by a queued-callback offer, the `ScheduleCallbackTask` workflow or `ICallbackService`, and is promoted by `CallbackDispatchBackgroundTask`. They are visible only through the report builder (`CallbackRequestReportDataSet`) and, when reserved for an agent, on the calendar.
- Live dialer state is on the Live dashboard (see Supervision).

**Already covered:** recipe step `ContactCenterDialerProfile`. Workflow tasks `ScheduleCallbackTask`, `ScheduleDialerRetryTask`. Reports Dialer queue inventory, Dialer decision log, Dialer activity (Paced) through the Reports framework.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listDialerProfiles` | Read | no | `ManageContactCenterDialer` | Lists dialer profiles with mode, provider, attempts, caller id and compliance switches. | ContactCenterSetupAgent |
| `getDialerProfile` | Read | no | `ManageContactCenterDialer` | Returns one dialer profile in full, including predictive pacing and calling-calendar settings. | ContactCenterSetupAgent |
| `saveDialerProfile` | Security | yes | `ManageContactCenterDialer` | Creates, clones or updates a dialer profile. It controls how fast real customers are dialed and which compliance rules (do-not-call, calling window, abandonment cap) are enforced, so a change can have legal consequences. Paced modes need the Paced Dialing feature. | ContactCenterSetupAgent |

**Implementation notes:** `IDialerProfileManager`, validation in `DialerProfileHandler.ValidatingAsync`, `DialerCampaignProfileGuard` (Omnichannel.Managements) for profile/campaign consistency. Kind is set to `Security` because the tool can switch off `RespectDoNotCall`, `EnforceCallingWindow` and `EnforceAbandonmentCap`; if that is judged too strict, split it: `Write` for everything else and approval only when a compliance switch is turned off (open question). No callback tools are proposed because no UI manages callbacks; see open questions.

**Test prompts:**

- "Show me the settings of the 'Evening predictive' dialer profile."
- "Clone 'Evening predictive' as 'Weekend progressive' with mode Progressive and 3 attempts."

## Contact Center voice media — `CrestApps.OrchardCore.ContactCenter` (base feature; menu `ContactCenterVoiceMediaAdminMenu`)

**What an admin can do in the UI:** Management → Voice Media (`VoiceMediaController`): list, create with an audio upload (20 MB limit, allowed content types), edit name/description or replace the audio, delete, and Re-sync media (push the master copy to every telephony provider that can host media).

**Already covered:** recipe step `ContactCenterVoiceMedia` (definition only; audio travels with the stored references).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listVoiceMedia` | Read | no | `ManageContactCenterVoiceMedia` | Lists voice media clips (greetings, prompts, hold audio) with format, whether a master copy exists and which providers hold a copy. Use it to find a clip id for a phone menu or queue treatment. | ContactCenterSetupAgent |
| `saveVoiceMedia` | Write | no | `ManageContactCenterVoiceMedia` | Creates or updates a voice media clip from an audio file that is already in the site's media library (path), or renames one. Audio is copied to the telephony provider, so it needs a provider or media storage that can host it. | ContactCenterSetupAgent |
| `resyncVoiceMedia` | External | yes | `ManageContactCenterVoiceMedia` | Re-sends a clip's master copy to every enabled telephony provider and reports per provider whether it was verified, uploaded or refused. Use it after changing provider or when a prompt does not play. | ContactCenterSetupAgent |

**Implementation notes:** `IVoiceMediaItemManager`, `IVoiceMediaProvisioningService.StoreAsync(item, Stream, contentType, format, namePrefix)`, `ResyncAsync(item)`, `CanUpload`. Gap: the upload path (`VoiceMediaItemDisplayDriver.UploadAudioAsync`: size and content-type checks, `VoiceMediaUploadFormat.Resolve(IFormFile)`, deferred `ReconcileReplacedAudioAsync`) only accepts an `IFormFile`; it must be extracted to accept a stream read from `IMediaFileStore`. `resyncVoiceMedia` is `External` because it uploads to the provider account.

**Test prompts:**

- "Which voice prompts are missing a copy on the telephony provider?"
- "Create a voice clip 'Holiday greeting' from /media/audio/holiday.wav."
- "Re-sync the 'Main welcome' clip."

## Contact Center call recording — `CrestApps.OrchardCore.ContactCenter.Recording`, `CrestApps.OrchardCore.ContactCenter.Recording.Core`

**What an admin can do in the UI:**

- Interaction Center → Call recordings (`CallRecordingsController`): list with filters (date range, customer number, direction, source: Contact Center call / AI voice agent / soft phone keypad, agent); someone with only `ListenToOwnCallRecordings` sees only their own.
- Open a recording: details, transcript, contact link, legal-hold state; play the audio (audited).
- Share: create a time-limited public playback link (needs `ListenToAllCallRecordings`).
- Erase with a mandatory reason (needs `DeleteCallRecordings`); refused under legal hold.
- Recordings also show on the activity page (`OmnichannelActivityCallRecordingsDisplayDriver`).

**Already covered:** workflow tasks `StartCallRecordingTask`, `StopCallRecordingTask`. Report builder data set `CallRecordingReportDataSet`.

**Settings groups:** `ContactCenterRecordingSettings` / group `contactcenter` / permission `ManageContactCenter` / secrets: none. Fields: `RecordAllCalls`, `ConsentModel`, `RequireExplicitConsent`, `EnableRecordingDisclosure`, `RecordingDisclosureText`, `DiscloseOnInboundCalls`, `DiscloseOnAIVoiceCalls`, `PromptAgentsToDisclose`, `RetentionDays`, `LegalHoldByDefault`, `AllowAgentSecurePause`, `MaxSecurePauseSeconds`, `RequirePauseReason`. These are compliance settings; `updateSiteSettings` for this group should require approval.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listCallRecordings` | Read | no | `ListenToOwnCallRecordings` (own only) or `ListenToAllCallRecordings` | Lists call recordings filtered by date range, customer number, direction, source and agent, with duration and agent. Callers without the all-recordings permission only get their own calls. | ContactCenterSupervisorAgent |
| `getCallRecording` | Read | no | `ListenToOwnCallRecordings` / `ListenToAllCallRecordings` | Returns one recording's details and transcript, the related contact and whether it is under legal hold. Reading it is written to the access audit like opening the page. It does not return audio. | ContactCenterSupervisorAgent |
| `shareCallRecording` | Security | yes | `ListenToAllCallRecordings` | Creates a link that lets anyone who has it play the recording until it expires, and returns the link and expiry. | ContactCenterSupervisorAgent |
| `eraseCallRecording` | Destructive | yes | `DeleteCallRecordings` | Permanently erases a recording and its media with a stated reason. Refused when the interaction is under legal hold. Cannot be undone. | ContactCenterSupervisorAgent |

**Implementation notes:** `ICallRecordingStore.QueryAsync(CallRecordingQuery)`, `ICallRecordingTranscriptProvider`, `CallRecordingLinks.Create(recordingId)` + `CallRecordingLinkEndpoints.BuildUrl`, `IRecordingAccessGovernanceService.RecordAccessAsync` / `EraseAsync(interactionId, ContactCenterActor.Supervisor(userId), reason)`. Gaps: the access rules (`GetAccessAsync`, `FindPlayableAsync`, own-versus-everyone narrowing) and the audit call (`AuditAccessAsync`) are private to `CallRecordingsController`; extract them so a tool cannot bypass the audit or the own-calls restriction. Transcripts contain customer personal data and possibly payment data; the tool result goes to the model provider (open question).

**Test prompts:**

- "Find yesterday's recorded calls with +1 555 010 4455."
- "What was said on Mark's last recorded call?"
- "Erase the recording of interaction X, the customer asked for it under their privacy rights."

## Contact Center shared voicemail — `CrestApps.OrchardCore.ContactCenter.InboundVoice`

**What an admin can do in the UI:** Interaction Center → Shared voicemail (`SharedVoicemailController`): list queue voicemails for the queues the user is entitled to, filtered by queue and status (`New`, `Claimed`, `Resolved`); play the recording; Claim; Release back to the queue; Resolve with a note; Call back (asks the user's soft phone to dial the caller); Delete (needs manage rights; refused under legal hold).

**Already covered:** report builder data set `SharedVoicemailReportDataSet`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listSharedVoicemails` | Read | no | `AccessContactCenterSharedVoicemail` (limited to entitled queues) | Lists shared queue voicemails by queue and status with caller, time, duration and who claimed them. | ContactCenterSupervisorAgent |
| `updateSharedVoicemail` | Write | no | `AccessContactCenterSharedVoicemail` (taking over another user's claim needs `ManageContactCenterSharedVoicemail`) | Claims a voicemail for the caller, releases it back to the queue, or marks it done with a note. | ContactCenterSupervisorAgent |
| `callBackSharedVoicemail` | External | yes | `AccessContactCenterSharedVoicemail` | Asks the caller's own soft phone to dial the person who left the voicemail. Needs the user's soft phone to be open; the voicemail stays open until it is resolved. | ContactCenterSupervisorAgent |
| `deleteSharedVoicemail` | Destructive | yes | `ManageContactCenterSharedVoicemail` | Deletes a shared voicemail and its recording. Refused under legal hold. Cannot be undone. | ContactCenterSupervisorAgent |

**Implementation notes:** `ISharedVoicemailService` (`ListAsync`, `ClaimAsync`, `ReleaseAsync`, `ResolveAsync`, `RequestCallbackAsync`, `DeleteAsync`) — every method takes the `ClaimsPrincipal` and authorizes itself through `ISharedVoicemailAuthorizationService`. The call-back also needs `ISoftPhoneClientNotifier.DialRequestedAsync(userId, TelephonyDialRequest)`, which the controller calls after `RequestCallbackAsync`.

**Test prompts:**

- "Are there new voicemails in the Support queue?"
- "Claim the oldest one and call the person back."
- "Mark that voicemail as done: spoke to the customer, issue solved."

## Contact Center supervision and live dashboard — `CrestApps.OrchardCore.ContactCenter.Supervision`, `CrestApps.OrchardCore.ContactCenter.RealTime`

**What an admin can do in the UI:**

- Interaction Center → Live dashboard (`SupervisorDashboardController`, state from `SupervisorDashboardEndpoints`): per queue waiting count, longest wait, SLA breaches and signed-in agents; per agent presence, current work and stale work; limited to the queues the supervisor oversees (`ISupervisorQueueAuthorizationService`).
- "Why work is not being dispatched" diagnostics per queue and campaign (`HandleDispatchAsync`) and the dialer banner for paced campaigns (`HandleDialerAsync`).
- On a live call (`SupervisorInterventionEndpoints`): Monitor / Whisper / Barge (rings the supervisor's soft phone), switch mode, stop; Take over; End call; Transfer to an agent, queue, entry point or external number; start or stop recording.
- On an agent: set state (any `AgentPresenceStatus`, or sign out) with a reason; Free agent (release work that is already over); send a message to the agent's desktop.
- AI calls (`SupervisorAICallsEndpoints`): list live AI voice calls, read the live transcript, send a note to the AI, take over, end, listen.
- Public API equivalents exist for AI live calls (`AILiveCallsApiEndpoints`: list, notes, transfer, switch profile, end).

**Already covered:** nothing. Neighbouring existing tools: `workforceForecastSummary`, `workforceOutOfAdherence` (Workforce.ContactCenter).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getContactCenterLiveState` | Read | no | `MonitorContactCenter` | Returns the live picture for the queues the caller supervises: per queue waiting count, longest wait, SLA breaches and signed-in agents; per agent presence, reason, time in state and current call. Use it for "how are we doing right now?". | ContactCenterSupervisorAgent |
| `getDialerDispatchDiagnostics` | Read | no | `MonitorContactCenter` | Explains, per queue or campaign, why waiting work is or is not being dispatched or dialed: agents signed in, available, held back and why, and the dialer's pacing state. Use it when a campaign is not dialing. | ContactCenterSupervisorAgent |
| `listLiveAIVoiceCalls` | Read | no | `MonitorContactCenter` | Lists the automated AI voice calls in progress with contact, number, AI profile, direction and duration. | ContactCenterSupervisorAgent |
| `getLiveAIVoiceCallTranscript` | Read | no | `MonitorContactCenter` | Returns the transcript so far of one live AI voice call, optionally only the turns after a given index. | ContactCenterSupervisorAgent |
| `sendNoteToLiveAIVoiceCall` | External | yes | `ContactCenterInterveneInCalls` | Sends a private instruction to the AI that is on a live call (the customer does not hear it), which changes how the AI continues the conversation. | ContactCenterSupervisorAgent |
| `monitorLiveCall` | External | yes | `MonitorContactCenter` | Joins the caller's own soft phone to a live call in monitor (listen), whisper or barge mode, switches the mode, or stops the engagement. For an AI call only listening is available. The supervisor's phone rings. | ContactCenterSupervisorAgent |
| `interveneInLiveCall` | External | yes | `ContactCenterInterveneInCalls` | Acts on a live call in a supervised queue: take it over (it rings the supervisor), end it, or start or stop its recording. Works for agent calls and AI calls (recording control is for agent calls only). | ContactCenterSupervisorAgent |
| `transferLiveCall` | External | yes | `ContactCenterInterveneInCalls` (an outside number also needs `ContactCenterTransferExternally`) | Transfers a live call to an agent, a queue, an entry point or an external number. For an AI call the transfer can be cold, warm or screened with a briefing. | ContactCenterSupervisorAgent |
| `setContactCenterAgentState` | Write | no | `ContactCenterInterveneInCalls` | Sets another agent's state (available, break, away, meeting, training…) with a reason, or signs the agent out. When the agent is on a call the state applies once that work ends. | ContactCenterSupervisorAgent |
| `freeContactCenterAgent` | Write | no | `ContactCenterInterveneInCalls` | Releases an agent who is stuck on work that has already ended so they can receive work again. Use it only when the live state shows stale work. | ContactCenterSupervisorAgent |
| `sendMessageToContactCenterAgent` | Write | no | `MonitorContactCenter` | Shows a short message from the supervisor on an agent's desktop. | ContactCenterSupervisorAgent |

**Implementation notes:** Services: `IContactCenterSupervisorInterventionService` (`TakeOverAsync`, `EndCallAsync`, `TransferAsync`, `SetRecordingAsync`, `SetAgentStateAsync`, `SignOutAgentAsync`, `FreeAgentAsync`, `SendMessageAsync`), `IContactCenterMonitoringService` (`EngageAsync`, `SwitchModeAsync`, `StopEngagementAsync`), `IContactCenterPhoneCallSupervisionService` (agents' own phone calls, keyed by `PhoneCallKey`; `ListenToAICallAsync`), `IAIVoiceCallSupervision` (`SendNoteAsync`, `TakeOverAsync`, `EndAsync`, `SwitchProfileAsync`), `IAIVoiceCallTransfers.TransferAsync(activityId, AITakeOverRequest)`, `IDialerDispatchDiagnosticsService.DiagnoseAsync`, `IDialerPacingBannerService`, `IStaleAgentWorkService`. The intervention service re-authorizes (`InterveneInCalls`) and audits each action; keep calling it rather than the providers. Gaps: (1) the whole live-state read model is built inside the static endpoint `SupervisorDashboardEndpoints.HandleStateAsync` (and `HandleDispatchAsync`, `BuildDialerBannerAsync`), and the per-call queue scoping is the nested `CallScope` class of `SupervisorInterventionEndpoints` — both must be extracted to services before tools can reuse them without duplicating authorization; (2) the AI live-call list is an inline YesSql query in `SupervisorAICallsEndpoints.HandleListAsync`. A call id is an interaction id, a phone-call key, or (AI calls) an activity id — the tools should take a `callType` to avoid guessing. Monitor, take over and call-back need the supervisor's soft phone to be connected; the tool can only report the provider's result. Optional extra (API only, no dashboard button): switch the AI profile of a live AI call (`SwitchProfileAsync`).

**Test prompts:**

- "How many calls are waiting in Support and what is the longest wait?"
- "Why is the Renewals campaign not dialing?"
- "Put Tom on break, reason coaching."
- "Tell the AI on the call with +1 555 010 2030 to offer the 10% discount."
- "End the call agent Sara is on."

## Contact Center agent workspace (self-service) — `CrestApps.OrchardCore.ContactCenter.Agents` with `CrestApps.OrchardCore.ContactCenter.Queues`/`Voice` (`AgentDesktopStartup`)

**What an admin can do in the UI:**

- Interaction Center → My workspace (`AgentWorkspaceController`, `AgentWorkspaceEndpoints`, `AgentSoftPhoneController`): sign in to queues and campaigns, sign out, set presence with a reason, see current work and recent interactions, complete wrap-up, pause/resume recording, confirm the recording disclosure, accept/decline an offer or send it to voicemail (`VoiceOfferEndpoints`), transfer and consult (`AgentSoftPhoneTransferEndpoints`), play and delete own voicemails, start a secure data capture (`SecureCaptureEndpoints`).
- My voicemail greeting (`MyVoicemailGreetingController`): record or upload a greeting, clear it.

**Already covered:** nothing.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getMyAgentWorkState` | Read | no | `ContactCenterSignIntoQueues` | Returns the calling user's own agent state: presence, signed-in queues and campaigns, current interaction and pending wrap-ups. | ContactCenterSupervisorAgent |
| `setMyAgentPresence` | Write | no | `ContactCenterSignIntoQueues` | Signs the calling user in to queues and campaigns, changes their own presence (available, break, away…) with a reason, or signs them out. Signing in means calls will be offered to their soft phone; entitlement rules can refuse a queue. | ContactCenterSupervisorAgent |

**Implementation notes:** `IAgentSessionService.BuildSnapshotAsync(userId)` (`AgentDesktopSnapshot`), `IAgentPresenceManager.SignInAsync/SignOutAsync/SetPresenceAsync` (throws `AgentEntitlementDeniedException`). Deliberately not proposed: answering, declining, transferring, holding, pausing recording, wrap-up completion and secure data capture — these are real-time actions on the agent's own live call that belong to the soft phone and, for secure capture, must never pass through a model. The voicemail greeting is an audio recording/upload.

**Test prompts:**

- "Sign me in to the Support queue."
- "Set me to break, lunch."
- "Am I still signed in to any queue?"

## Contact Center interval statistics, reports and health — `CrestApps.OrchardCore.ContactCenter.IntervalStatistics`, `CrestApps.OrchardCore.ContactCenter` (reports with `CrestApps.OrchardCore.Reports`)

**What an admin can do in the UI:**

- Management → Interval statistics (`IntervalStatisticsController`): see how many days are computed and final, today's totals, the rebuild state (and whether it stalled); request a rebuild of a date range (needs `ManageContactCenter`).
- Reports menu (Reports framework): Call insights, Call quality, Call handling, Agent activity timeline, Agent availability, Reconciled payroll timecard, Agent productivity, Queue usage, Campaign summary, Dialer queue inventory, Subject inventory, Dialer decision log (`AnalyticsStartup`), Dialer activity (`DialerPacedReportsStartup`); report builder data sets under `Reports/DataSources/DataSets`.
- Health: `api/contact-center/health/dependencies` (needs `MonitorContactCenter`) reports each dependency check.

**Already covered:** reports and report-builder data sets are expected to be reached through the Reports module's tools (not in this group; unverified what that section proposes). `IContactCenterReportingService` (`GetCallInsightsAsync`, `GetAgentProductivityAsync`, `GetQueueUsageAsync`, `GetCampaignSummaryAsync`, `GetSubjectInventoryAsync`) is the service behind them.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getIntervalStatisticsStatus` | Read | no | `ViewContactCenterReports` | Returns the state of the 15-minute statistics: days computed and final, today's totals, and whether a rebuild is running or stalled. | ContactCenterSetupAgent |
| `rebuildIntervalStatistics` | Write | no | `ManageContactCenter` | Requests that the interval statistics of a date range be computed again in the background (a few days every five minutes). Use it after correcting data or when numbers look wrong. | ContactCenterSetupAgent |
| `getContactCenterHealth` | Read | no | `MonitorContactCenter` | Runs the Contact Center dependency health checks (storage, outbox, active calls, queue backlog, dialer pacing, provider ingress, distributed lock, backplane) and returns each status. Use it first when calls or routing misbehave. | ContactCenterSupervisorAgent |

**Implementation notes:** Interval statistics: the controller's `_roller.GetStateAsync` / `RequestRebuildAsync(fromUtc, toUtc, userName)` and `_reader.GetDaysAsync` / `GetQueueIntervalsAsync` (types under `IntervalStatistics/`); `RequestRebuildAsync` can throw `ConcurrencyException`. Health: `HealthCheckService.CheckHealthAsync` with the dependency-tag predicate from `ContactCenterHealthEndpoints` (only registered with `OrchardCoreFeatureIds.HealthChecks`). If the Reports section does not offer a generic `runReport`, add one read tool here over `IContactCenterReportingService` (open question).

**Test prompts:**

- "Are the contact center statistics up to date?"
- "Rebuild the interval statistics for last week."
- "Is anything unhealthy in the contact center?"

## Contact Center settings — `CrestApps.OrchardCore.ContactCenter` and its features

**What an admin can do in the UI:** Settings → Contact Center (`ContactCenterSettingsAdminMenu`, group `contactcenter`): recording governance, default numbers, external transfer destinations, secure data capture.

**Already covered:** the generic site-settings tools. Deployment steps exist for the recording and secure-capture settings (`AddSiteSettingsPropertyDeploymentStep<ContactCenterRecordingSettings, …>` and `<SecureCaptureSettings, …>` in `ConfigurationDeploymentStartup.cs`) and a recipe schema for the default numbers (`ContactCenterDefaultAddressSettingsSchema`), so `applySiteSettings` / `importOrchardCoreRecipe` can already set them.

**Settings groups:**

- `ContactCenterRecordingSettings` / group `contactcenter` / permission `ManageContactCenter` / secrets: none (Recording feature; see Call recording).
- `ContactCenterExternalTransferSettings` / group `contactcenter` / permission `ManageContactCenter` / secrets: none. Fields: `Destinations` (list of `ContactCenterExternalDestination`), `AllowUnlistedNumbers` (Voice feature).
- `SecureCaptureSettings` / group `contactcenter` / permission `ManageContactCenter` / secrets: none. Fields: `Enabled`, `LinkTimeToLiveSeconds`, `PauseRecordingDuringCapture` (Secure Data Capture feature).
- `ContactCenterDefaultAddressSettings` / group `contactcenter` / permission `ManageContactCenter` / secrets: none. Fields: `DefaultPhoneAddressId`, `DefaultSmsAddressId` (when Omnichannel Channel Endpoints is on).

No dedicated tool. **Implementation notes:** four settings classes share one group id, so `getSiteSettings("contactcenter")` must return all of them, each only when its feature is enabled. The drivers clamp and validate (`Math.Clamp` on retention and pause seconds; address ids validated against the address catalog), so `updateSiteSettings` must run the same validation, ideally by reusing the drivers' logic.

**Test prompts:**

- "Turn on recording disclosure for inbound calls and keep recordings for 90 days."
- "Add our pharmacy partner's number as an allowed external transfer destination."

## Telephony — `CrestApps.OrchardCore.Telephony`, `CrestApps.OrchardCore.Telephony.SoftPhone`, `CrestApps.OrchardCore.Telephony.SoftPhone.Core`, `CrestApps.OrchardCore.Telephony.SoftPhone.Extension`

**What an admin can do in the UI:**

- Interaction Center → Management → Extensions, or Settings → Communication → Extensions (`ExtensionsController`): list, search, create, edit, delete, bulk remove (`TelephonyExtension`: `Name`, `Number`, user).
- Settings → Communication → Telephony (group `telephony`): default provider and allowed short codes, call cost, soft phone widget, plus each provider's settings.
- Soft phone (widget, `/softphone` page): place and control calls in the browser (`TelephonyHub`); click-to-dial beside phone fields posts to `softphone/dial`, which asks the user's own soft phone to dial.
- Connect / disconnect the user's own provider account (`TelephonyOAuthController`, interactive OAuth).

**Already covered:** recipe step `TelephonyExtension` (via `importOrchardCoreRecipe`).

**Settings groups:**

- `TelephonySettings` / group `telephony` / permission `ManageTelephonySettings` / secrets: none (`DefaultProviderName`, `AllowedShortCodes`, …).
- `TelephonyCostSettings` / group `telephony` / permission `ManageTelephonySettings` / secrets: none (`Currency`, `PerMinute`).
- `SoftPhoneWidgetSettings` / group `telephony` / permission `ManageTelephonySettings` / secrets: none (`Enabled`, `DisplayOnAdmin`, `EnableDiagnostics`, `AccentColor`, `RecentCallsCount`, `DefaultCountryCode`).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listTelephonyExtensions` | Read | no | `ManageTelephonyExtensions` | Lists internal phone extensions with their number and the user they ring. | TelephonyAgent |
| `saveTelephonyExtension` | Write | no | `ManageTelephonyExtensions` | Creates or updates an extension (name, number, user). The number must be unique. | TelephonyAgent |
| `deleteTelephonyExtension` | Destructive | yes | `ManageTelephonyExtensions` | Deletes one or more extensions. | TelephonyAgent |
| `dialFromSoftPhone` | External | yes | `UseTelephonySoftPhone` | Asks the calling user's own soft phone to dial a number. Nothing happens when their soft phone is not open; outbound screening (do-not-call, short codes) still applies in the phone. | TelephonyAgent |

**Implementation notes:** Extensions are a catalog with `TelephonyExtensionHandler` validation and `TelephonyExtensionDirectory`. `dialFromSoftPhone` mirrors `SoftPhoneDialerEndpoints.HandleDialAsync`: `ISoftPhoneClientNotifier.DialRequestedAsync(userId, new TelephonyDialRequest { Number })`. No server-side originate tool is proposed: `ITelephonyService.DialAsync` acts for the current user's browser session and the audio lives in the browser. OAuth connect is interactive and stays in the UI.

**Test prompts:**

- "What is Maria's extension?"
- "Give John extension 204."
- "Call +1 555 010 3344 from my phone."

## Telnyx — `CrestApps.OrchardCore.Telnyx`, `CrestApps.OrchardCore.Telnyx.Sms`, `CrestApps.OrchardCore.Telnyx.AiVoice`

**What an admin can do in the UI:**

- Settings → Communication → Telephony: Telnyx settings, and a Connect button (`TelnyxConnectController.Connect`) that provisions the call-control and SIP connections and webhook at the provider with the saved API key and stores the returned ids; connection status; Disconnect; inspect a number.
- Settings → Communication → Telnyx numbers (`TelnyxNumbersController`): list owned numbers (with reputation), search available numbers (country, area code, city, region, contains, SMS-capable), buy a number (it is also added as a phone address), set a number's caller name, release a number.
- SMS settings screen: Telnyx SMS settings.
- Public API equivalents: `TelnyxNumbersApiEndpoints` (search, order, list).

**Already covered:** recipe steps `TelnyxSettings`, `TelnyxSmsSettings` (via `importOrchardCoreRecipe`).

**Settings groups:**

- `TelnyxSettings` / group `telephony` / permission `ManageTelephonySettings` / secrets: `ApiKey`, `WebhookPublicKey`, `TurnCredential` (data-protected; never re-rendered).
- `TelnyxSmsSettings` / group `sms` (Orchard `SmsSettings.GroupId`) / permission `SmsPermissions.ManageSmsSettings` / secrets: `ApiKey`, `WebhookPublicKey`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getTelnyxConnectionStatus` | Read | no | `ManageTelephonySettings` | Tells whether the Telnyx provider is enabled and connected (connection ids present) and returns the default caller id. Never returns the API key. | TelephonyAgent |
| `setTelnyxConnection` | Security | yes | `ManageTelephonySettings` | Connects the site to the Telnyx account (creates the voice application, SIP connection and webhook with the saved API key and stores their ids) or disconnects it. Requires the provider to be enabled with an API key saved in the settings first. | TelephonyAgent |
| `listTelnyxNumbers` | Read | no | `ManageTelephonySettings` | Lists the phone numbers the Telnyx account owns, with caller name and reputation. | TelephonyAgent |
| `searchTelnyxAvailableNumbers` | Read | no | `ManageTelephonySettings` | Searches phone numbers that can be bought, by country, area code, city, region, digits contained and whether SMS is needed. Returns up to 30. | TelephonyAgent |
| `buyTelnyxNumber` | External | yes | `ManageTelephonySettings` | Orders a phone number from the provider (it is billed to the account) and adds it as a phone address of the site. | TelephonyAgent |
| `setTelnyxNumberCallerName` | External | yes | `ManageTelephonySettings` | Sets the caller name shown for an owned number. Carriers can take days to show it. | TelephonyAgent |
| `releaseTelnyxNumber` | Destructive | yes | `ManageTelephonySettings` | Releases an owned number back to the provider. The number is lost and calls or texts to it stop; it cannot be undone. | TelephonyAgent |

**Implementation notes:** `TelnyxApiClient` (Core, `TelnyxApiClient.Numbers.cs`): `ListOwnedNumbersAsync`, `SearchAvailableNumbersAsync(TelnyxNumberSearch)`, `OrderNumbersAsync`, `SetCallerNameAsync`, `ReleaseNumberAsync`; `TelnyxCallerNumberReputation.ApplyToAsync`. Connect: `ITelnyxProvisioningApiService.ConnectAsync/DisconnectAsync`. Gaps: the connect sequence (unprotect the key, build the webhook URL and resource name, store `ConnectionId`/`SipConnectionId`/`OutboundVoiceProfileId`, `RequestUpdate<TelnyxOptions>()`) and the buy-then-create-address step are in the controllers and must be extracted. `releaseTelnyxNumber` does not remove the matching omnichannel address (the UI does not either) — say so in the result. `InspectNumber` is a diagnostic that builds a raw `HttpClient` in the controller; not proposed. Secrets must be written only through `updateSiteSettings` with masking; no tool here accepts a key.

**Test prompts:**

- "Which phone numbers do we have at Telnyx?"
- "Find a 702 number that can send texts."
- "Buy +1 702 555 0199."
- "Release the number ending in 0144."

## DNC Registry — `CrestApps.OrchardCore.DncRegistry`, `CrestApps.OrchardCore.DncRegistry.UsaFtc`, `CrestApps.OrchardCore.DncRegistry.CanadaDncl`, `CrestApps.OrchardCore.DncRegistry.Local`

**What an admin can do in the UI:**

- Interaction Center → Local DNC Registry (`LocalDncRegistryAdminController`): list uploaded lists by country with status, counts and errors; upload a CSV (name, country); Process now; Pause import; Delete (in the background); download the import errors.
- Settings → Content Import (`importContentSettings`): enforce do-not-call registries globally and choose which.
- Settings → DNC Registries → USA FTC Registry / Canada LNNTE-DNCL Registry: account and API key.

**Already covered:** recipe step `LocalDncLists` (name, country code and inline `PhoneNumbers`; it skips a list whose name already exists for the country). Report builder data sets `DncListsReportDataSet`, `DncEntriesReportDataSet`.

**Settings groups:**

- `DncRegistrySettings` / group `importContentSettings` / permission `ManageDncRegistrySettings` / secrets: none (`EnforceGlobally`, `EnforcedRegistryKeys`).
- `UsaFtcDncRegistrySettings` / group `usaFtcRegistry` / permission `ManageDncRegistrySettings` / secrets: `ProtectedApiKey` (fields `OrganizationId`, `BaseUrl`).
- `CanadaDnclRegistrySettings` / group `canadaDnclRegistry` / permission `ManageDncRegistrySettings` / secrets: `ProtectedApiKey` (fields `AccountNumber`, `BaseUrl`).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listLocalDncLists` | Read | no | `ManageDncRegistrySettings` | Lists the local do-not-call lists with country, status, number count, processed and imported totals and any import error. | TelephonyAgent |
| `importLocalDncList` | Write | no | `ManageDncRegistrySettings` | Creates a local do-not-call list for a country from phone numbers given inline or from a CSV file in the media library, and queues it for background import. | TelephonyAgent |
| `setLocalDncListImportState` | Write | no | `ManageDncRegistrySettings` | Starts or resumes processing of a queued or failed list now, or pauses a running import. | TelephonyAgent |
| `deleteLocalDncList` | Security | yes | `ManageDncRegistrySettings` | Deletes a local do-not-call list and all its numbers in the background. Those numbers can be dialed again afterwards, so this weakens compliance screening and cannot be undone. | TelephonyAgent |
| `checkDoNotCallNumbers` | Read | no | `ManageDncRegistrySettings` | Checks phone numbers against the enabled do-not-call registries (local lists and, when configured, the national ones) and returns which are registered. A national registry lookup may call the registry's paid service. No UI equivalent; optional. | TelephonyAgent |

**Implementation notes:** `ILocalDncListManager` (`GetListsAsync`, `GetCountAsync`, `QueueImportAsync(name, countryCode, fileName, stream)`, `ResumeImportAsync`, `PauseImportAsync`, `MarkAsDeletingAsync` + background `DeleteAsync`, `FindByIdAsync`), `INationalDoNotCallRegistry.GetRegisteredNumbersAsync` (all registered registries). Gap: `TriggerImportProcessing` and the delete background job are private to the controller. `checkDoNotCallNumbers` should default to the local registry only and treat a national lookup as `External` (open question). `deleteLocalDncList` is marked `Security` rather than only `Destructive` because it removes a compliance control.

**Test prompts:**

- "Add these five numbers to a US do-not-call list called 'Complaints June'."
- "Did the 'State list 2026' import finish, and were there errors?"
- "Is +1 702 555 0123 on any do-not-call list?"

## No tools needed

| Module | Reason | Settings group (if any) |
| --- | --- | --- |
| Omnichannel — `CrestApps.OrchardCore.Omnichannel` | Shared message, preference and index infrastructure only (`Startup.cs` registers the store collection and indexes); no menu, controller or settings. | — |
| Omnichannel - Azure Communication Services — `CrestApps.OrchardCore.Omnichannel.AzureCommunicationServices` | Feature that only pulls in Orchard's Azure email and SMS providers; no code of its own in the module. | Orchard's own email/SMS provider groups (other sections) |
| Omnichannel - Azure Event Grid — `CrestApps.OrchardCore.Omnichannel.EventGrid` | Webhook receiver (`api/azure/webhook/eventgrid`) configured only from appsettings (`CrestApps:Omnichannel:EventGrid`); no admin UI. `EventGridSettings` is declared but not referenced by a driver. | — |
| Omnichannel Messaging - Azure Blob Storage — `CrestApps.OrchardCore.Omnichannel.Messaging.Azure` | Storage provider swap for message attachments; two files, no UI. | — |
| SMS Messaging Channel — `CrestApps.OrchardCore.Omnichannel.Messaging.Sms` | Adds SMS as a channel of the Messaging workspace (per-number provider on the address editor, keyword replies from appsettings `CrestApps:Omnichannel:Messaging:Sms:KeywordReplies`, `SendTextMessageTask` workflow). Its user actions are the Messaging tools and `saveOmnichannelAddress`. | — |
| SMS Omnichannel Automation — `CrestApps.OrchardCore.Omnichannel.Sms` | Runs automated AI SMS conversations (processor, handlers, background tasks, AI profile templates). Its only UI is the AI-agent section of an SMS entry point, covered by `saveContactCenterEntryPoint`. | — |
| Automated Voice — `CrestApps.OrchardCore.Omnichannel.Voice` | Runs AI voice conversations; registers the hidden in-call tools `endCall`, `collectSecureDigits`, `pressKeys`. No admin UI. Starting a call is `startAIActivity`; supervising one is in the supervisor tools. | — |
| Outreach — `CrestApps.OrchardCore.Outreach` | Dependency-only bulk-send engine (`IOutreachEngine`, `OutreachBackgroundTask`); no UI. Reached through `sendMessagingBroadcast` and the Marketing module. | — |
| Telephony - Azure Blob Storage — `CrestApps.OrchardCore.Telephony.Azure` | Storage provider swap for call recordings; no UI. | — |
| Asterisk — `CrestApps.OrchardCore.Asterisk` | Telephony provider with a settings page only (no controllers or endpoints). Recipe step `AsteriskSettings`. | `AsteriskSettings` / group `telephony` / `ManageTelephonySettings` / secrets: `Password`, `TurnSharedSecret`, `PjsipRealtimeConnectionString` |
| DialPad — `CrestApps.OrchardCore.DialPad` | The folder holds only `bin/` and `obj/`; there is no source in this checkout. | — |
| DNC Registry - Azure Blob Storage — `CrestApps.OrchardCore.DncRegistry.Azure` | Storage provider swap for uploaded DNC files; no UI. | — |
| Contact Center infrastructure features — `...ContactCenter.AgentServices`, `...RealTime`, `...Recording.Core`, `...Voice`, `...Voice.Media`, `...ProviderInbox` | Dependency-only or plumbing features (agent directory services, SignalR hub, recording governance services, call router, media sessions, webhook inbox). Their admin-visible parts are covered by the sections above; `Voice.Media` is switched on with `enableSiteFeature`. | `contactcenter` (see Contact Center settings) |
| Contact Center Secure Data Capture — `...ContactCenter.SecureCapture` | Agent-initiated capture of card data on a live call through a tokenized public page; must not pass through a model. Only its settings are admin-facing. | `SecureCaptureSettings` / group `contactcenter` |
| Contact Center skills screen — `SkillsController` | Redirects to the shared Skills module; the skill catalog and its tools belong to that module's section. | — |

## Proposed agents for this group

| Agent | Purpose (one sentence) | Tools | Could merge with |
| --- | --- | --- | --- |
| CrmAgent | Works the CRM day to day: finds, schedules, completes and bulk-manages activities, prepares and loads activity batches, converts leads, manages not-in-service numbers and starts AI calls or texts. | `listOmnichannelActivities`, `getOmnichannelActivity`, `saveOmnichannelActivity`, `completeOmnichannelActivity`, `bulkUpdateOmnichannelActivities`, `purgeOmnichannelActivities`, `listActivityBatches`, `saveActivityBatch`, `previewActivityBatch`, `loadActivityBatch`, `deleteActivityBatch`, `listNotInServiceNumbers`, `setNumberNotInService`, `convertLead`, `startAIActivity`, `cancelAIActivity` (16) plus the existing content tools for contacts, leads, accounts and opportunities | MessagingAgent (one "customer engagement" agent, 27 tools — over the limit, so only if broadcasts/templates move out); a Customers/Accounts agent from the commerce group |
| OmnichannelSetupAgent | Configures the CRM for administrators: campaigns and groups, dispositions, cadences, subject flows, the business's own numbers and addresses, lead statuses and opportunity stages. | `listOmnichannelCampaigns`, `saveOmnichannelCampaign`, `listOmnichannelCampaignGroups`, `saveOmnichannelCampaignGroup`, `listOmnichannelDispositions`, `saveOmnichannelDisposition`, `listOmnichannelCadences`, `saveOmnichannelCadence`, `listSubjectFlows`, `saveSubjectAction`, `listOmnichannelAddresses`, `saveOmnichannelAddress`, `listLeadStatuses`, `saveLeadStatus`, `listOpportunityStages`, `saveOpportunityStage`, `deleteOmnichannelRecord` (17) | TelephonyAgent (numbers and addresses belong together); not ContactCenterSetupAgent (would be 41 tools) |
| MessagingAgent | Reads and answers customer conversations in the messaging workspace and manages templates and broadcasts. | `listMessagingConversations`, `getMessagingConversation`, `sendMessagingMessage`, `updateMessagingConversation`, `transferMessagingConversation`, `setMessagingAvailability`, `listMessageTemplates`, `saveMessageTemplate`, `deleteMessageTemplate`, `listMessagingBroadcasts`, `sendMessagingBroadcast` (11) | A Communications agent holding `sendEmail`, `sendSmsMessage`, `sendNotification`; a Marketing agent (broadcasts share the Outreach engine) |
| ContactCenterSetupAgent | Configures the Contact Center for administrators: queues, queue groups, entry points and phone menus, business hours, agents and their entitlements, agent states, dialer profiles, voice media and interval statistics. | `listContactCenterQueues`, `getContactCenterQueue`, `saveContactCenterQueue`, `listContactCenterQueueGroups`, `saveContactCenterQueueGroup`, `listContactCenterEntryPoints`, `getContactCenterEntryPoint`, `saveContactCenterEntryPoint`, `listBusinessHoursCalendars`, `saveBusinessHoursCalendar`, `checkBusinessHours`, `listContactCenterAgents`, `saveContactCenterAgent`, `listAgentStateReasonCodes`, `saveAgentStateReasonCode`, `listDialerProfiles`, `getDialerProfile`, `saveDialerProfile`, `listVoiceMedia`, `saveVoiceMedia`, `resyncVoiceMedia`, `deleteContactCenterRecord`, `getIntervalStatisticsStatus`, `rebuildIntervalStatistics` (24) | Nothing (already at the limit); if it must shrink, move dialer profiles and interval statistics to a small OutboundDialerAgent together with `getDialerDispatchDiagnostics` |
| ContactCenterSupervisorAgent | Runs live operations for supervisors: live queue and agent state, dialer diagnostics, interventions on live calls and AI calls, agent state changes, shared voicemail, call recordings and health. | `getContactCenterLiveState`, `getDialerDispatchDiagnostics`, `listLiveAIVoiceCalls`, `getLiveAIVoiceCallTranscript`, `sendNoteToLiveAIVoiceCall`, `monitorLiveCall`, `interveneInLiveCall`, `transferLiveCall`, `setContactCenterAgentState`, `freeContactCenterAgent`, `sendMessageToContactCenterAgent`, `listSharedVoicemails`, `updateSharedVoicemail`, `callBackSharedVoicemail`, `deleteSharedVoicemail`, `listCallRecordings`, `getCallRecording`, `shareCallRecording`, `eraseCallRecording`, `getContactCenterHealth`, `getMyAgentWorkState`, `setMyAgentPresence` (22) | The Workforce agent that owns `workforceOutOfAdherence` and `workforceForecastSummary` (adherence and forecast are supervisor questions); a Reports agent for the contact center reports |
| TelephonyAgent | Manages phone infrastructure and calling compliance: extensions, provider connection and phone numbers, local do-not-call lists, and dialing from the user's soft phone. | `listTelephonyExtensions`, `saveTelephonyExtension`, `deleteTelephonyExtension`, `dialFromSoftPhone`, `getTelnyxConnectionStatus`, `setTelnyxConnection`, `listTelnyxNumbers`, `searchTelnyxAvailableNumbers`, `buyTelnyxNumber`, `setTelnyxNumberCallerName`, `releaseTelnyxNumber`, `listLocalDncLists`, `importLocalDncList`, `setLocalDncListImportState`, `deleteLocalDncList`, `checkDoNotCallNumbers` (16) | OmnichannelSetupAgent (33 tools together — too many unless DNC moves to a Compliance agent with consent/phone-verification tools from the neighbouring group) |

Total new tools proposed in this group: 106 (CrmAgent 16, OmnichannelSetupAgent 17, MessagingAgent 11, ContactCenterSetupAgent 24, ContactCenterSupervisorAgent 22, TelephonyAgent 16). None is proposed as `direct`; `dialFromSoftPhone`, `getMyAgentWorkState` and `setMyAgentPresence` are the candidates to tick on the main assistant if agents and supervisors use the chat widget.

## Open questions

- Extraction work is the real cost: the live dashboard read model (`SupervisorDashboardEndpoints`), the per-call supervisor scoping (`SupervisorInterventionEndpoints.CallScope`), the ten activity bulk actions and the in-flight guard (`ActivitiesController`), the batch start-load and delete rules (`ActivityBatchesController`), recording access and audit (`CallRecordingsController`), messaging compose/broadcast validation (`AdminController.NewPost`, `BroadcastsController.CreatePost`), Telnyx connect and buy (`TelnyxConnectController`, `TelnyxNumbersController`) and the voice-media upload (`VoiceMediaItemDisplayDriver`) all live in controllers, endpoints or drivers. Should these be extracted to services first (recommended), or should the tools duplicate them?
- Live-call tools act on real customers in real time. Is an approval prompt fast enough for `interveneInLiveCall`/`transferLiveCall`, or should live-call control stay out of the chat widget in the first release and ship read-only (`getContactCenterLiveState`, diagnostics, transcripts) first?
- Approval for agent-affecting actions: `setContactCenterAgentState` (including sign-out) and `freeContactCenterAgent` are `Write` under the brief's rules, but they change a colleague's working state. Should they require approval?
- `saveDialerProfile` and `deleteLocalDncList` are marked `Security` because they can switch off compliance screening. Is that the intended use of `Security`, or should there be a separate compliance flag? The same question applies to `updateSiteSettings` on the `contactcenter` group (recording consent and disclosure) and the DNC groups.
- `saveOmnichannelCampaign` can change the system message and `ToolNames` of an automated customer conversation. Should that be `Security` with approval?
- Recipes are a side door: `importOrchardCoreRecipe` with `MessagingBroadcasts` queues a mass send, and with `ContactCenterDialerProfile`, `LocalDncLists` or the provider settings steps changes compliance or secrets. Does `importOrchardCoreRecipe` require approval for these step names?
- Personal data to the model: recording transcripts, AI call transcripts, message threads and voicemail details contain customer data and sometimes payment data. Is returning them to the model acceptable for every deployment, or should these read tools be opt-in per site?
- Callbacks have no admin screen (`CallbackRequest` is only visible in the report builder and, when reserved, on the calendar). Should the plan add `listCallbackRequests` / `scheduleCallback` (over `ICallbackRequestManager` and `ICallbackService.ScheduleAsync`) even though there is no UI to mirror?
- Interactions have permissions (`ViewInteractions`, `ManageInteractions`) but no admin screen; history is only in reports. Is a read tool such as `getInteractionTimeline` (over `IInteractionManager` and `IInteractionEventStore`) wanted?
- Reports: this section assumes the Reports module's section offers a generic way to run a registered report with a date range. If it does not, a `getContactCenterReport` read tool over `IContactCenterReportingService` and an Omnichannel equivalent are needed here.
- The admin UI cannot delete campaigns, campaign groups or omnichannel addresses, but the public API can delete an address (`DeleteChannelEndpoint`). Should `deleteOmnichannelRecord` accept `address`, and are campaign deletes intentionally absent?
- Agent Entitlements is optional. With it off, there is no screen that lists agents; `listContactCenterAgents` still works, but should `saveContactCenterAgent` be registered only with that feature?
- Skills: the Contact Center skill screens redirect to the shared Skills module while a `ContactCenterSkill` recipe step and manager still exist here. Which module owns the skill tools?
- `checkDoNotCallNumbers` and a possible "switch AI profile on a live call" tool have no admin-UI equivalent (the second exists only in the public API). Keep or drop?
- `DialPad` has no source in this checkout, and the caller's list of Contact Center features (QA, wallboards) does not match the manifest: there is no QA-scorecard or wallboard feature in `CrestApps.OrchardCore.ContactCenter/Manifest.cs`; the nearest are the Call quality report, call recordings with transcripts, and the Live dashboard. Is there another module that should have been in this group?
