# CrestApps Orchard Core Teams

One team hierarchy for the whole site: the organization, regions, sites, departments, teams and crews. Workforce, Contact Center and any other module that organizes people build on the same teams.

## Features

| Feature | Id | What it adds |
| --- | --- | --- |
| Teams | `CrestApps.OrchardCore.Teams` | Teams with a kind, a parent, a description, a linked role, a time zone and whether they are in use; the **Teams** admin list and editor; the **Manage teams** permission; and a `Teams` recipe and deployment step. |

## Admin

- **Teams**: the team tree. Each team is followed by the teams under it. Modules add their own cards to the editor and their own actions to each row.

## Extending

Inject `ITeamManager` to read and change teams (it keeps the hierarchy a tree), or `TeamStore` to read them without running the catalog handlers. Keep a module's own data on a team with `team.Put(settings)` and `team.TryGet<TSettings>(out var settings)`, add a `DisplayDriver<Team>` for its card in the editor, an `ICatalogEntryHandler<Team>` to react to changes, and an `ITeamDeletionGuard` (`services.AddTeamDeletionGuard<T>()`) to refuse to delete a team the module still uses. Call `services.AddTeamsCore()` from a module's own registration; it is safe to call more than once. `TeamRecipeImport` imports teams from a module's own recipe step.

## Documentation

- User Manual: [Teams](../../CrestApps.Docs/docs/user-manual/workforce/teams.md)
- Technical Manual: [Teams](../../CrestApps.Docs/docs/modules/teams.md)
