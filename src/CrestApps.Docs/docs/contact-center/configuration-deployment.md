---
sidebar_label: Configuration deployment
sidebar_position: 6
title: Contact Center Configuration Deployment
description: Export a Contact Center tenant's configuration as a deployment plan, review it in source control, and replay it into another environment.
---

# Contact Center Configuration Deployment

A contact centre that can only be configured by hand cannot be promoted. Contact Center therefore exports everything an operator configures through Orchard Core's standard deployment and recipe pipelines, so a tenant can be built and reviewed in staging, committed to source control as a diff, and replayed into production instead of being rebuilt under a cutover window.

## One-step feature enablement recipes

A supported Contact Center tenant enables roughly a dozen features across the base module, the voice stack, and a provider adapter, and getting that set — and its order — right by hand is the bulk of first-run toil. The base module ships two harvestable recipes, one per certified provider profile, that enable exactly the feature set of the matching support-matrix tenant profile in a single step:

| Recipe | Display name | Enables the feature set of tenant profile |
| --- | --- | --- |
| `contact-center-asterisk-ga-core` | **Contact Center — Asterisk (GA-Core)** | `ga-core-asterisk` |
| `contact-center-dialpad-ga-core` | **Contact Center — DialPad (GA-Core)** | `ga-core-dialpad` |

Enable **Recipes** (`OrchardCore.Recipes`), then run the recipe for your provider from **Configuration → Recipes**. Orchard Core resolves feature dependencies, so the listed features and anything they depend on are enabled together. Each recipe enables the base orchestration, availability, queues, routing, voice with the browser soft phone, the agent desktop, real-time updates, preview/manual dialing, and the provider's Contact Center voice adapter.

The feature list in each recipe is the coherent, certified combination for its tenant profile — a build test asserts each recipe enables exactly its profile's features from `support-matrix.v1.json`, so a recipe cannot drift from the support matrix and cannot ship an unlisted (unsupported) combination. There is deliberately no "inbound-only" or "outbound-only" recipe: the certified profiles bundle inbound voice with preview and manual dialing, and a partial split would be an unlisted feature combination. The recipes enable features only; they carry no queues, skills, entry points, or dialer profiles, because that configuration references environment-specific resources (a configured provider, its channel endpoints, and campaigns). Configure your provider and then define or import that configuration with the steps below.

## What travels between environments

Each configurable entity has its own deployment step and a matching recipe step, in the same way every other Orchard Core module exposes its configuration.

| Entity | Deployment step (category **Contact Center**) | Recipe step name | Collection |
| --- | --- | --- | --- |
| Skill | Contact Center Skills | `ContactCenterSkill` | `Skills` |
| Queue group | Contact Center Queue Groups | `ContactCenterQueueGroup` | `QueueGroups` |
| Business hours calendar | Contact Center Business Hours Calendars | `ContactCenterBusinessHoursCalendar` | `Calendars` |
| Queue | Contact Center Queues | `ContactCenterQueue` | `Queues` |
| Entry point | Contact Center Entry Points | `ContactCenterEntryPoint` | `EntryPoints` |
| Dialer profile | Contact Center Dialer Profiles | `ContactCenterDialerProfile` | `DialerProfiles` |
| Agent state reason code | Contact Center Agent State Reason Codes | `AgentStateReasonCode` | `ReasonCodes` |
| Agent entitlements | Contact Center Agent Entitlements | `ContactCenterAgentEntitlement` | `Agents` |

The CRM configuration a contact centre routes and reports on travels the same way. Those steps are described in [Omnichannel management](../omnichannel/management.md#exporting-and-importing-configuration).

A step only appears when the feature that owns its entity is enabled, so a tenant that does not run the dialer is not offered a dialer profile step.

Full agent profiles do not travel. A profile names the person who holds it, carries their contact details, and records the state they are in right now; it is a record of who works in an environment rather than of how that environment is configured, so it is treated as runtime state and stays where it is produced. The manager-owned entitlement configuration an operator does grant — display name, maximum concurrent interactions, allowed queues, allowed campaigns and skills — travels through the **Contact Center Agent Entitlements** step. That step is a deliberate projection: it is keyed by the Orchard user name (the internal user identifier differs between environments) and it never carries live presence, the internal user identifier, or the profile item identifier. The user name is resolved from the live user record at export time, so a plan stays valid even after an administrator renames the user. On import each entry is matched to a user by name — an entry whose user is absent from the destination is reported and skipped — dangling queue and campaign references are dropped, and an existing profile has only its configuration promoted, so a signed-in agent's presence and active reservation are left untouched.

Runtime state deliberately does not travel. Activities, activity batches, interactions, interaction events, call sessions, queue items, reservations, agent sessions, callback requests, provider commands, webhook inbox messages, the outbox, the deduplication ledger, projection checkpoints, work state, and aggregated metrics are produced by traffic in the environment that owns them; copying them would move one tenant's live work into another. Every stored entity is either carried by a step or recorded as runtime state, and a build fails if a new entity is neither.

Provider credentials and connection settings are not part of a plan. They are bound from configuration - `appsettings.json`, environment variables, or a secret store - so that a plan committed to source control never carries a secret and a destination environment keeps its own provider account.

## Exporting a tenant

1. Enable **Deployment** (`OrchardCore.Deployment`) alongside the Contact Center features you use.
2. Go to **Configuration → Import/Export → Deployment Plans** and create a plan.
3. Add the Contact Center steps you need. Each step exports every entry of its type.
4. Add the Omnichannel steps as well if the tenant runs campaigns, dispositions, or subject flows.
5. Execute the plan with the **Download** target.

## Importing into another tenant

Enable **Recipes** (`OrchardCore.Recipes.Core`) in the destination, then import the plan from **Configuration → Import/Export → Package Import**, or include the steps in a setup recipe.

Import is idempotent, which is what makes a plan safe to replay:

- An entry whose identifier already exists is updated in place.
- An entry whose identifier is unknown is created, preserving the identifier from the plan so that later replays match it.
- An entry the destination's own rules reject is reported and skipped without being stored, and without stopping the entries around it. A plan with one bad entry lands the rest and tells you what it could not land.

Because identifiers are preserved, cross-references keep working after the import: a queue that points at a queue group, an entry point that points at a queue, and a dialer profile that points at a campaign all still resolve.

### Order the steps so that referenced entities import first

A recipe runs its steps in file order, and a reference is checked when the entry that carries it is stored. Order a plan's Contact Center steps as follows:

1. Skills, queue groups, and business hours calendars.
2. Queues.
3. Entry points.
4. Dialer profiles.
5. Agent entitlements.

Agent state reason codes reference nothing and can be placed anywhere. Agent entitlements reference queues and campaigns, so place the **Contact Center Agent Entitlements** step after the queue step and after the Omnichannel campaign step. Where Contact Center configuration references CRM configuration - a dialer profile that names a campaign, or a queue that overflows to a channel endpoint - place the Omnichannel steps before the Contact Center steps that need them.

The standard agent state reason codes seeded by the Contact Center migrations use fixed identifiers, so every tenant agrees on them and a plan that references a standard reason code still resolves after it is replayed.

Members owned by the environment - creation and modification stamps, and ownership - are never carried; the destination writes its own. Every other member of every entry travels, including members added to an entity after the step was written, because the step serialises the entity itself rather than a hand-written property list.

## Writing steps by hand

The steps are plain recipe steps, so a tenant can also be scripted from scratch. Each step is an object with a `name` and a collection whose entries are the entities themselves.

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
    },
    {
      "name": "ContactCenterAgentEntitlement",
      "Agents": [
        {
          "UserName": "agent.smith",
          "DisplayName": "Agent Smith",
          "MaxConcurrentInteractions": 1,
          "AllowedQueueIds": [],
          "AllowedCampaignIds": [],
          "Skills": ["Spanish"]
        }
      ]
    }
  ]
}
```

Omitting `ItemId` creates a new entry under an identifier the store issues. Including an `ItemId` that the destination already holds updates that entry; including one it does not hold creates the entry under that identifier, which is what lets a hand-written plan be replayed without duplicating what it created the first time.

The agent entitlement step is the exception to the `ItemId` rule: it identifies an entry by `UserName` rather than by `ItemId`, because an agent is a person who already exists in the destination's user store. An entry whose `UserName` does not resolve to a user in the destination is reported and skipped, and an existing agent has only the manager-owned configuration in the step promoted — live presence and any active reservation are left untouched.

## Rules applied on import

An imported entry is judged by the same rules as one typed into the admin editor, because the step writes through the entity's own manager. The rules belong to the entry itself rather than to the editor's screen, so a recipe, a deployment plan, the admin editor and any service that writes through the entry's manager all enforce the same set.

This matters because the two paths used to disagree. A rule expressed only in an editor screen never runs when a recipe writes the same entry, so a plan could store a queue pointing at a queue group that does not exist, or a dialer profile in a mode the product does not support. The import reported success, the entry read as configured, and the problem appeared later as traffic that did not route.

Practically, this means a plan you wrote by hand can be refused:

- An entry the rules reject is reported and skipped. The entries around it are still imported, so one bad entry does not abandon the plan.
- References are checked. A queue naming a queue group the destination does not hold is refused, which is why the steps are ordered so that each entity is imported after the entities it references.
- Values are normalised on the way in. A phone number on a channel endpoint is stored in its canonical form whether it was typed into the editor or written into a recipe, so a recipe-written endpoint matches inbound traffic.

Values outside a permitted range are refused rather than corrected. A dialer profile asking for more concurrent calls per agent than the product allows fails with a message naming the limit, instead of being quietly adjusted to a number nobody asked for.

## Extending the set

A new Contact Center configuration entity becomes portable by adding the same four pieces every Orchard Core module adds: a deployment step, a deployment source, a display driver for the step, and a recipe step. Register them in the feature that owns the entity's manager:

```csharp
services
    .AddDeployment<ContactCenterSkillDeploymentSource, ContactCenterSkillDeploymentStep>()
    .AddRecipeExecutionStep<ContactCenterSkillStep>();
```

The build fails until the new entity is also declared as configuration or as runtime state, so the decision cannot be skipped.

Registering the recipe step also brings the entity under the rule-ownership checks: it must have a handler registered by the same feature that registers the step, its editor screens must not carry rules of their own, and every admin action that saves it must run the handlers first. Registering the handler in a different feature is checked because a tenant can enable the feature that carries the recipe step without enabling the one that carries the admin screens, and a handler that is not registered does not run. Runtime state - activities, activity batches, interactions, call sessions, queue items and agent sessions - is outside this, because no plan authors it.
