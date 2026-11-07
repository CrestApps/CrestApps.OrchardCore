---
sidebar_label: Teams
title: Teams
description: "The shared team hierarchy: the organization, regions, sites, departments, teams and crews, the team manager and store, the editor other modules add sections to, deletion guards, the Teams recipe and deployment step, and how Workforce keeps its own team settings."
user_manual:
  - user-manual/workforce/teams
---

The **Teams** module keeps one team hierarchy for the site, so Workforce, Contact Center and any other module that organizes people use the same teams. A team has a name, a kind (`Organization`, `Region`, `Site`, `Department`, `Team` or `Crew`), an optional parent, a description, a linked role, a time zone and whether it is in use. A module that needs more about a team keeps it on the team itself and adds its own section to the team editor.

The admin screens are described in the User Manual: [Teams](../user-manual/workforce/teams.md).

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Teams` | Teams | |

Projects: `CrestApps.OrchardCore.Teams.Abstractions` (the `Team` model and the contracts), `CrestApps.OrchardCore.Teams.Core` (the store, the index and the services), `CrestApps.OrchardCore.Teams` (the module).

## Storage

Each team is a YesSql document in the `Teams` collection (`TeamsConstants.CollectionName`), with a `TeamIndex` of `ItemId`, `Name`, `Kind`, `ParentId` and `Enabled`. A site has tens or hundreds of teams, so the hierarchy is read whole and walked in memory (`TeamTree`).

## Services

| Contract | What it does |
| --- | --- |
| `ITeamManager` | `ICatalogManager<Team>` plus `ListTreeAsync` (each team followed by its sub-teams by name, with its depth), `GetSubtreeIdsAsync`, `GetAncestorsAsync` and `GetDeletionBlockersAsync`. Validation refuses a blank name, a parent that doesn't exist, a parent under the team itself, and a time zone that isn't a known IANA or Windows id (a Windows id is stored as its IANA id). |
| `TeamStore` | The `DocumentCatalog<Team, TeamIndex>` behind the manager, public so that services of other modules can read teams without running the catalog handlers. Write through `ITeamManager` when the handlers must run. |
| `TeamTree` | `GetSubtreeIds` and `Order` over a list of teams. |
| `ITeamMembershipService` | Who is in a team and which teams a person is in: `GetMembersAsync(teamId, includeSubTeams, on)`, `GetMembershipsAsync(userId, on)` and `IsMemberAsync(teamId, userId, on)`. A team's members include the members of its sub-teams. The answers come from the registered `ITeamMemberProvider`s; with none, teams have no members. |
| `TeamRecipeImport` | Imports teams from recipe JSON: created, or updated by id or name, with the parent by `parentName` in any order. A module's own recipe step passes a callback to copy its settings onto each team. |

Register the services with `services.AddTeamsCore()`. The call is safe to repeat, so a module that builds on teams calls it from its own registration, as `AddWorkforceCore()` does.

## Extension points

| Contract | When it runs | Use it for |
| --- | --- | --- |
| `ICatalogEntryHandler<Team>` | The team manager's lifecycle. | Keeping a module's records in step with a team, as Workforce does for each team's calendar. |
| `ITeamMemberProvider` | `ITeamMembershipService`. | Giving the members of teams a module keeps, as `TeamMember { TeamId, UserId, Role, EffectiveFrom, EffectiveTo, Source }` (`Role` is an open string; `TeamMemberRoles` names the usual ones). Register it with `services.AddTeamMemberProvider<TProvider>()`. Workforce gives each worker with a user account from its team memberships, and shows a team's workers on the team editor with its own driver. |
| `ITeamDeletionGuard` | `ITeamManager.GetDeletionBlockersAsync`, which the **Delete** action calls first. | Refusing to delete a team the module still uses. Return the reason, shown to the person deleting it, or `null`. Register it with `services.AddTeamDeletionGuard<TGuard>()`. |
| `DisplayDriver<Team>` | The team editor, and the rows of the team list. | Adding a section to the editor (a shape in the `Content` zone, after the Teams card at `Content:1`) or actions to a row's menu (between **Add a team under it** at `ActionsMenu:5` and **Delete** at `ActionsMenu:100`). |

A module keeps its own data on the team with `team.Put(settings)` and `team.TryGet<TSettings>(out var settings)`, as on any catalog item. Workforce adds `WorkforceTeamSettings` (the site address and coordinates, the calendar, the crew lead, and the open shift, trade, time off and time clock policies) with the helpers `team.GetWorkforceSettings()` and `team.AlterWorkforceSettings(settings => ...)`.

```csharp
public sealed class RosterTeamDisplayDriver : DisplayDriver<Team>
{
    public override IDisplayResult Edit(Team team, BuildEditorContext context)
    {
        return Initialize<RosterTeamViewModel>("RosterTeamFields_Edit", model =>
        {
            model.Color = team.TryGet<RosterTeamSettings>(out var settings) ? settings.Color : null;
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(Team team, UpdateEditorContext context)
    {
        var model = new RosterTeamViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        team.Alter<RosterTeamSettings>(settings => settings.Color = model.Color);

        return Edit(team, context);
    }
}
```

## Recipes and deployment

The `Teams` step creates teams, or updates them by id or name, with the parent by `parentName` in any order. Every team goes through the team manager, so it is validated and the handlers of the modules that use teams run (Workforce gives each new team its calendar). What other modules keep on a team stays as it is.

```json
{
  "steps": [
    {
      "name": "Teams",
      "Teams": [
        { "name": "East", "kind": "Region" },
        { "name": "Boston", "kind": "Site", "parentName": "East", "timeZoneId": "America/New_York" },
        { "name": "Billing", "kind": "Team", "parentName": "Boston", "linkedRoleName": "Billing" }
      ]
    }
  ]
}
```

The **Teams** deployment step exports every team in tree order, with its parent by name. It leaves out what other modules keep on a team, because that can name records of this site such as a calendar; their own steps carry it. The Workforce step, for example, still accepts and exports teams with their Workforce settings. With CrestApps Recipes on, both steps describe their schema.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageTeams` (Manage teams) | The team list, the editor and deleting teams. Administrators have it. |

When Workforce is on, **Manage workforce teams** grants **Manage teams** too, so roles given it before teams had their own module keep their access.

## Upgrading from Workforce teams

Before this module, Workforce kept its teams as `WorkforceTeam` documents in the `Workforce` collection. On an existing site, the Workforce feature's migration (`WorkforceMigrations.UpdateFrom1Async`) runs after the Teams migration and moves them: each old document is read as JSON, saved as a `Team` in the `Teams` collection with the **same id** (worker memberships and every other record that names a team keep working), and its Workforce fields go into the team's `WorkforceTeamSettings`. A team already in the `Teams` collection is left as it is. The old documents and the old `Workforce_TeamIndex` table are then removed. The move runs on the migration's own connection and transaction.
