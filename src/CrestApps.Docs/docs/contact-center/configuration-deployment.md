---
sidebar_label: Configuration deployment
sidebar_position: 6
title: Contact Center Configuration Deployment
description: Export a Contact Center tenant's configuration as a deployment plan, review it in source control, and replay it into another environment.
---

# Contact Center Configuration Deployment

A contact centre that can only be configured by hand cannot be promoted. Contact Center therefore exports everything an operator configures as a deployment plan, so a tenant can be built and reviewed in staging, committed to source control as a diff, and replayed into production instead of being rebuilt under a cutover window.

## What travels between environments

The following entities are configuration. Each is carried by its own recipe step, and every step is produced by a single deployment step.

| Entity | Recipe step | Collection | Import order |
| --- | --- | --- | --- |
| Skill | `ContactCenterSkill` | `Skills` | 10 |
| Queue group | `ContactCenterQueueGroup` | `QueueGroups` | 20 |
| Business hours calendar | `ContactCenterBusinessHoursCalendar` | `Calendars` | 30 |
| Queue | `ContactCenterQueue` | `Queues` | 40 |
| Entry point | `ContactCenterEntryPoint` | `EntryPoints` | 50 |
| Dialer profile | `ContactCenterDialerProfile` | `DialerProfiles` | 60 |
| Agent state reason code | `AgentStateReasonCode` | `ReasonCodes` | 70 |

The CRM configuration a contact centre routes and reports on travels the same way, in its own deployment step:

| Entity | Recipe step | Collection | Import order |
| --- | --- | --- | --- |
| Disposition | `OmnichannelDisposition` | `Dispositions` | 10 |
| Channel endpoint | `OmnichannelChannelEndpoint` | `ChannelEndpoints` | 20 |
| Campaign group | `OmnichannelCampaignGroup` | `CampaignGroups` | 30 |
| Campaign | `OmnichannelCampaign` | `Campaigns` | 40 |
| Subject flow settings | `OmnichannelSubjectFlowSettings` | `SubjectFlows` | 50 |
| Subject action | `OmnichannelSubjectAction` | `SubjectActions` | 60 |

The import order is a correctness requirement rather than a preference. A queue references skills, a queue group, and a business hours calendar, so those steps are written to the plan first; an entry point references queues, so it is written after. A recipe runs its steps in file order, and the exported plan is already in that order.

Agent profiles do not travel. A profile names the person who holds it, carries their contact details, and records the state they are in right now; it is a record of who works in an environment rather than of how that environment is configured, so it is treated as runtime state and stays where it is produced. Activities and activity batches are runtime state for the same reason.

Runtime state deliberately does not travel. Interactions, interaction events, call sessions, queue items, reservations, agent sessions, callback requests, provider commands, webhook inbox messages, the outbox, the deduplication ledger, projection checkpoints, work state, and aggregated metrics are produced by traffic in the environment that owns them; copying them would move one tenant's live work into another. Every stored entity is either carried by a step or recorded as runtime state, and a build fails if a new entity is neither.

Provider credentials and connection settings are not part of a plan. They are bound from configuration - `appsettings.json`, environment variables, or a secret store - so that a plan committed to source control never carries a secret and a destination environment keeps its own provider account.

## Exporting a tenant

1. Enable **Deployment** (`OrchardCore.Deployment`) alongside the Contact Center features you use.
2. Go to **Configuration → Import/Export → Deployment Plans** and create a plan.
3. Add the **Contact Center configuration** step.
4. Add the **CRM configuration** step as well if the tenant runs campaigns, dispositions, or subject flows.
5. Leave **Include all Contact Center configuration** selected to export every catalog the tenant runs, or clear it and choose individual catalogs to export a subset.
6. Execute the plan with the **Download** target.

Only the catalogs whose feature is enabled appear in the plan, so a tenant that does not run the dialer produces no dialer profile step. Catalogs with no entries are skipped, so an empty plan is empty rather than full of empty steps.

Entries are exported in a stable order - by name, then by identifier - so that re-exporting an unchanged tenant produces an unchanged file and a plan can be diffed meaningfully in source control.

## Importing into another tenant

Enable **Recipes** (`OrchardCore.Recipes.Core`) in the destination, then import the plan from **Configuration → Import/Export → Package Import**, or include the steps in a setup recipe.

Import is idempotent, which is what makes a plan safe to replay:

- An entry whose identifier already exists is updated in place.
- An entry whose identifier is unknown but whose identity matches an existing entry updates that entry and keeps the destination's identifier. Most entries are identified by their name, campaigns and campaign groups and channel endpoints by their display text, subject flow settings by the subject they configure, and subject actions by the subject, the disposition that triggers them, and their source. This is what stops a replay from duplicating the reason codes and other defaults that the destination's own migrations seeded independently.
- Anything else is created, preserving the identifier from the plan so that later replays match by identifier.
- A setting cleared at the source is cleared at the destination. The plan carries emptied values rather than omitting them, so replaying a plan cannot leave the two environments disagreeing about a setting an operator has just removed.
- An entry the destination's own rules reject is reported and skipped without being stored, and without stopping the entries around it. A plan with one bad entry lands the rest and tells you what it could not land.

Nothing stops you from keeping two entries under one name - two queues called "Support", or several actions on the same disposition - and a plan carrying both lands both. Each entry the destination already owned can be matched once, so the second entry in the plan is created rather than written over the first.

### Identifiers must survive the trip

Configuration refers to configuration by identifier: a queue names its queue group, an entry point names its queue, and a subject action names the disposition that triggers it. An import that minted a fresh identifier would leave every one of those references pointing at nothing, so a created entry keeps the identifier the plan exported.

That is not enough on its own. Two environments that were configured independently hold the same queue group under two different identifiers, and matching them by name means the destination keeps the identifier it already published to its own data - so every reference the rest of the plan carries would point at an entry the destination does not have. The import therefore records each substitution it makes and applies it to the entries it has already stored as well as to the ones still to come. That matters because no ordering of a plan makes every reference resolvable when it is written: a queue overflows into another queue in its own step, and it references a channel endpoint that a different deployment step carries, so an import that only looked forward would leave those references pointing at entries the destination does not hold. The order of the steps in a plan is therefore a convenience rather than a correctness requirement, and adding the CRM step before or after the Contact Center step gives the same result. The substitutions are held for the duration of one import and are not carried into the next.

For the same reason, the standard agent state reason codes seeded by the Contact Center migrations use fixed identifiers, so that every tenant agrees on them and a plan that references a standard reason code still resolves after it is replayed. Tenants seeded before those identifiers were fixed keep the random identifiers they were given; a plan replayed into such a tenant still lands correctly, because the reason codes are matched by name and the references that pointed at them are repointed at the identifiers that tenant holds.

Members owned by the environment - creation and modification stamps, and ownership - are never carried; the destination writes its own. Every other member of every entry travels, including members added to an entity after the step was written, because the step is driven by the shape of the entity rather than by a hand-written property list.

## Writing steps by hand

The steps are plain recipe steps, so a tenant can also be scripted from scratch. Each step is an object with a `name` and a collection whose entries are the entities themselves. A hand-written plan can give its entries any `ItemId` it likes - `"support-queue"` reads better than a generated identifier - and reference them by it: the store issues the real identifier, and the import translates whatever the plan invented wherever the plan used it.

```json
{
  "steps": [
    {
      "name": "ContactCenterSkill",
      "Skills": [
        {
          "Name": "Spanish",
          "Description": "Native or fluent Spanish.",
          "Enabled": true
        }
      ]
    },
    {
      "name": "ContactCenterQueue",
      "Queues": [
        {
          "Name": "Support",
          "Description": "General support queue.",
          "Enabled": true
        }
      ]
    }
  ]
}
```

The collection is matched without regard to case, so `"Queues"` and `"queues"` both work. A step whose collection is named something the destination does not recognise imports nothing, and it is reported as an error rather than passing as a successful step, so a mistyped plan fails the import instead of leaving you with a tenant that looks configured.

Omitting a member leaves the existing value untouched on an update, so a step can carry only the members it intends to change; a member present with a `null` value is cleared. Including a store-issued `ItemId` pins the identifier, which is what an exported plan does. Any other `ItemId` is treated as a name the plan invented for its own use: the entry is stored under an identifier the store issues, and every reference the plan makes to the invented name is translated to it. Omitting `ItemId` altogether lets the entry be matched by its identity.

## Rules applied on import

An imported entry is judged by the same rules as one typed into the admin editor. The rules belong to the entry itself rather than to the editor's screen, so a recipe, a deployment plan, the admin editor and any service that writes through the entry's manager all enforce the same set.

This matters because the two paths used to disagree. A rule expressed only in an editor screen never runs when a recipe writes the same entry, so a plan could store a queue pointing at a queue group that does not exist, or a dialer profile in a mode the product does not support. The import reported success, the entry read as configured, and the problem appeared later as traffic that did not route.

Practically, this means a plan you wrote by hand can be refused:

- An entry the rules reject is reported and skipped. The entries around it are still imported, so one bad entry does not abandon the plan.
- References are checked. A queue naming a queue group the destination does not hold is refused, which is why the plan orders each catalog after the catalogs it references.
- Values are normalised on the way in. A phone number on a channel endpoint is stored in its canonical form whether it was typed into the editor or written into a recipe, so a recipe-written endpoint matches inbound traffic.

Values outside a permitted range are refused rather than corrected. A dialer profile asking for more concurrent calls per agent than the product allows fails with a message naming the limit, instead of being quietly adjusted to a number nobody asked for.

## Extending the set

A new Contact Center configuration entity becomes portable by registering its catalog in the module's startup:

```csharp
services.AddConfigurationCatalog<ContactCenterSkill, IContactCenterSkillManager>(
    ContactCenterConfigurationCatalogs.Group,
    ContactCenterConfigurationCatalogs.Skill,
    "Skills",
    order: 10);
```

The registration is all that is required: the recipe step, the deployment step's contribution, the ordering, and the export and import of every member follow from it. The build fails until the new entity is also declared as configuration or as runtime state, so the decision cannot be skipped.

Registering the catalog also brings the entity under the rule-ownership checks: it must have a handler registered by the same feature that registers the catalog, its editor screens must not carry rules of their own, and every admin action that saves it must run the handlers first. Registering the handler in a different feature is checked because a tenant can enable the feature that carries the recipe step without enabling the one that carries the admin screens, and a handler that is not registered does not run. Runtime state - activities, activity batches, interactions, call sessions, queue items and agent sessions - is outside this, because no plan authors it.
