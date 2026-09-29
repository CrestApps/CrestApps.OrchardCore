# Contact Center, Omnichannel and phone system import/export inventory

Every admin CRUD surface in the Contact Center, Omnichannel and phone system modules, with how its data leaves one
tenant and arrives in another. "Before" is `origin/main` at `d3c56f29c`; "after" is this branch.

A surface is **covered** when it has a deployment step (export), a recipe step (import) that imports exactly what the
step exports, and an `IRecipeStep` (or, for site settings, an `ISiteSettingsSchemaDefinition`) schema that describes
every exported member. Schema completeness is enforced by `ContactCenterRecipeStepSchemaTests`, which walks each record
type alongside its schema; export-to-import round trips and schema validation of real exports are enforced by
`ContactCenterConfigurationPortabilityTests` in the feature activation suite.

## Covered surfaces

| # | Surface (admin screen) | Model / store | Deployment step | Recipe step | Schema |
| --- | --- | --- | --- | --- | --- |
| 1 | CC Skills | `ContactCenterSkill` / `IContactCenterSkillManager` | Before: yes. After: yes | `ContactCenterSkill` (yes / yes) | Before: partial (no `Properties`, `ItemId`/`Name` refused null). After: full |
| 2 | CC Queue groups | `ActivityQueueGroup` / `IActivityQueueGroupManager` | yes / yes | `ContactCenterQueueGroup` (yes / yes) | partial → full |
| 3 | CC Business hours calendars | `BusinessHoursCalendar` / `IBusinessHoursCalendarManager` | yes / yes | `ContactCenterBusinessHoursCalendar` (yes / yes) | partial, and registered under the Queues feature instead of Business Hours → full, registered under Business Hours |
| 4 | CC Queues | `ActivityQueue` / `IActivityQueueManager` | yes / yes | `ContactCenterQueue` (yes / yes) | partial: missing `FirstResponseTargetSeconds`, `SkillRequirements`, `OverflowTargets`, `MaxWaitSeconds`, `MaxWaitAction`, `MaxQueueSize`, `QueueFullAction`, `Treatment`, `Properties` → full |
| 5 | CC Entry points (incl. IVR menu) | `ContactCenterEntryPoint` / `IContactCenterEntryPointManager` | yes / yes | `ContactCenterEntryPoint` (yes / yes) | partial: missing `TargetType`, `TargetAgentId`, `VoicemailEnabled`, `RingTimeoutSeconds`, `VoicemailGreetingText`, `VoicemailRecipientAgentId`, `VoicemailDestination`, `IvrFlow`, `Properties` → full (IVR nodes, options and actions described) |
| 6 | CC Dialer profiles | `DialerProfile` / `IDialerProfileManager` | yes / yes | `ContactCenterDialerProfile` (yes / yes) | partial: missing `PreviewExtensionSeconds`, `MaxPreviewExtensions`, `Properties` → full |
| 7 | CC Agent state reason codes | `AgentStateReasonCode` / `IAgentStateReasonCodeManager` | yes / yes | `AgentStateReasonCode` (yes / yes) | partial → full |
| 8 | CC Agent entitlements | projection of `AgentProfile` keyed by user name | yes / yes | `ContactCenterAgentEntitlement` (yes / yes) | partial: missing `SkillProficiencies`, `QueueMemberships`; registered under Queues instead of Agent Entitlements → full, own feature |
| 9 | CC Voice media library | `VoiceMediaItem` / `IVoiceMediaItemManager` | **no → yes** (`ContactCenterVoiceMedia`) | **no → yes** | **no → yes** |
| 10 | CC External transfer settings | site settings `ContactCenterExternalTransferSettings` | **no → yes** (site settings step) | built-in `Settings` step | **no → yes** (site settings schema) |
| 11 | CC Recording and monitoring settings | site settings `ContactCenterRecordingSettings` | **no → yes** (site settings step, Recording feature) | built-in `Settings` step | **no → yes** |
| 12 | CC Secure capture settings | site settings `SecureCaptureSettings` | **no → yes** (site settings step, Secure Capture feature) | built-in `Settings` step | **no → yes** |
| 13 | Omnichannel Dispositions | `OmnichannelDisposition` / `INamedCatalogManager<>` | yes / yes | `OmnichannelDisposition` (yes / yes) | partial (no `Properties`) → full |
| 14 | Omnichannel Channel endpoints (incl. messaging routing and SMS provider, stored in the endpoint's `Properties`) | `OmnichannelChannelEndpoint` / `IOmnichannelChannelEndpointManager` | yes / yes | `OmnichannelChannelEndpoint` (yes / yes) | partial: missing `ProviderName`, `Properties` → full |
| 15 | Omnichannel Campaign groups | `OmnichannelCampaignGroup` / `ICatalogManager<>` | yes / yes | `OmnichannelCampaignGroup` (yes / yes) | partial → full |
| 16 | Omnichannel Campaigns | `OmnichannelCampaign` / `ICatalogManager<>` | yes / yes | `OmnichannelCampaign` (yes / yes) | partial → full |
| 17 | Omnichannel Cadences | `Cadence` / `ICatalogManager<>` | yes / yes | `OmnichannelCadence`: **registered but broken** (the handler never bound the JSON, so every import created an empty cadence refused for having no name) → fixed | **no → yes** |
| 18 | Omnichannel Subject flows (actions) | `SubjectAction` / `ISourceCatalogManager<>` | yes / yes | `OmnichannelSubjectAction` (yes / yes) | partial → full |
| 19 | Omnichannel Subject flow settings | `OmnichannelSubjectPart` content-type part settings | Orchard Core **Content Definition** step | `ContentDefinition` | Orchard Core schema |
| 20 | Messaging Templates | `MessageTemplate` / `IMessageTemplateManager` | **no → yes** (`OmnichannelMessageTemplate`) | **no → yes** | **no → yes** |
| 21 | Telephony Extensions | `TelephonyExtension` / `ITelephonyExtensionManager` | **no → yes** (`TelephonyExtension`) | **no → yes** (user resolved by user name) | **no → yes** |
| 22 | Telephony settings | site settings `TelephonySettings` | **no → yes** (site settings step) | built-in `Settings` step | **no → yes** |
| 23 | Soft phone widget settings | site settings `SoftPhoneWidgetSettings` | **no → yes** (site settings step, Soft Phone feature) | built-in `Settings` step | **no → yes** |
| 24 | Telnyx voice settings | site settings `TelnyxSettings` | **no → yes** (`TelnyxSettings`, secrets stripped) | **no → yes** (`TelnyxSettings`, merges, protects supplied secrets) | **no → yes** (step schema and `Settings`-step schema) |
| 25 | Telnyx SMS settings | site settings `TelnyxSmsSettings` | **no → yes** (`TelnyxSmsSettings`, secrets stripped) | **no → yes** | **no → yes** |
| 26 | Asterisk settings | site settings `AsteriskSettings` | **no → yes** (`AsteriskSettings`, secrets stripped) | **no → yes** (same live-binding guard as the settings screen) | **no → yes** |

## Deliberately excluded

| Surface or data | Module | Why it does not travel |
| --- | --- | --- |
| Activities, activity batches | Omnichannel.Managements | Operational record. A batch is a run: loading it creates activities, so replaying one would create live work in the destination. |
| Contacts and subjects | Omnichannel.Managements | Content items; they travel through Orchard Core's content steps, not a Contact Center step. |
| DNC import options, bulk-manage filters, report filters | Omnichannel.Managements | Transient form state of an operation, not stored configuration. |
| Conversations, broadcasts | Omnichannel.Messaging | The record of what was said and sent; replaying a broadcast would message its recipients again. |
| Shared voicemail messages | ContactCenter | Messages callers left, with recordings on interactions that exist only in the source. |
| Agent profiles (beyond entitlements), personal voicemail greetings | ContactCenter | Bound to users and live presence; the manager-owned part travels as entitlements. |
| Interactions, interaction events, call sessions, call quality, queue items, reservations, agent sessions, callbacks, provider commands, webhook inbox, outbox, dedupe ledger, projection checkpoints, work state, metrics, secure capture sessions | ContactCenter | Runtime state produced by traffic (enumerated by `ContactCenterConfigurationCoverageTests`). |
| Telephony call logs (`TelephonyInteraction`), per-user provider OAuth tokens, SIP/PJSIP credentials, ARI channel bindings | Telephony, Telnyx, Asterisk | Runtime state or per-user secrets. |
| Telnyx Connect (provisioning) | Telnyx | An action against the provider account, not stored configuration; its results (connection ids) travel in `TelnyxSettings`. |
| Default Asterisk, configuration-driven Telnyx/Twilio defaults, workspace tunables | Asterisk, Telnyx, Messaging | Bound from shell configuration, not the admin UI. |
| Twilio and SMS settings | Omnichannel.Sms (settings owned by `OrchardCore.Sms`) | Orchard Core owns these settings and their schemas (`TwilioSettingsSchema`, `SmsSettingsSchema` already exist). |
| Omnichannel.Voice, Omnichannel.EventGrid, WebSockets, Telephony.Azure | — | No admin CRUD surface. |

## Secrets

Protected values (Telnyx API keys, webhook public keys and TURN credential; Asterisk ARI password, TURN shared secret
and PJSIP Realtime connection string) are data-protected with the source tenant's keys, so a copy is unreadable
elsewhere and a plaintext copy would put a credential in a file. The provider steps therefore export every other member
and leave the protected ones out (`ProtectedSettingsDeploymentSerializer`). On import a secret that is absent or empty
keeps the destination's stored value; a secret supplied in clear text in a hand-written recipe is protected with the
same purpose the settings screen uses before it is stored. The generic `Settings` step is still available for those
objects and is described by its own schema, whose secret descriptions say the value is stored as given and must already
be protected by the destination.

## Leftovers noticed, not changed

- `OmnichannelDeploymentSteps.SubjectFlowSettings` and `Views/Items/OmnichannelSubjectFlowSettingsDeploymentStep.Thumbnail.cshtml`
  in Omnichannel.Managements belong to no registered step; subject flow settings travel with the content definition.
- Settings imported through Orchard Core's built-in `Settings` step are not followed by an `IOptionsUpdateNotifier`
  signal from CrestApps code. The telephony and soft phone settings are read live through `ISiteService`, and the
  provider steps signal their own options; whether any other imported settings object is read through a cached
  options projection that needs a tenant reload has not been verified against a running tenant.
