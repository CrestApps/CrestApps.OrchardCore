---
sidebar_label: Tenant Hierarchy
title: Tenant Hierarchy
description: Let an organization run its own sites for others, set what it may do, and keep the whole hierarchy in view from the platform.
technical_manual:
  - modules/tenant-hierarchy
---

The tenant hierarchy lets one tenant, a **parent**, create and manage its own **child tenants**. A bookkeeping firm, for example, can run one site per client business without asking you for each one. This page is for platform administrators, who work in the main site (the platform). The parent's own team uses [Child Tenants](child-tenants.md).

| | |
| --- | --- |
| **Menu** | Multi-Tenancy > Tenant Hierarchy |
| **Permission** | Manage the tenant hierarchy |
| **Feature** | Tenant Hierarchy Platform (main site only) |

<AskYourAdmin />

## How it works

- You make an existing tenant a parent and give it a **policy**: how many child tenants it may have, what they may start from, where their data lives, and how its team opens them.
- The parent's team then adds, opens, suspends and removes its child tenants on its own, within that policy. Each child tenant is a separate site with its own address, users and data.
- A parent sees only its own child tenants. A child tenant sees nothing outside itself. Neither can reach the platform or another hierarchy.
- When a child tenant is suspended or removed, its address shows that the site is not available. Visitors never land on the main site instead.
- You keep full control: the platform's **Tenants** screen still lists every tenant, and the **Tenant Hierarchy** screen shows who belongs to whom.

If the screen shows **The host guards are not installed**, the application was started without the protections the hierarchy needs, and no tenant can be made a parent. Ask the people who host the application to follow the Technical Manual.

## See the hierarchy

Open **Multi-Tenancy > Tenant Hierarchy**. Each parent is listed with its state, its address, a count such as **3 of 25 child tenants**, and its child tenants underneath. Click a parent's name to see all its child tenants and the actions for the parent.

**Orphaned child tenants** are child tenants whose parent no longer exists. Move each one to a parent, or make it an ordinary tenant.

## Make a tenant a parent

Any tenant that is set up and not yet part of the hierarchy can become a parent. If there is none, click **Create a tenant**, create and set up the tenant on the **Tenants** screen, then come back.

1. Click **Make a parent tenant**.
2. Pick the **Tenant**. The **Display name** and **Address** fill in from it. When the tenant already has an address under the platform's address, it keeps that address.
3. Check the **Display name**, which the parent's team sees, and the **Address**.
4. Set the policy below, and click **Make parent tenant**.

The tenant keeps its users, content and data. Its address changes only if you change the **Address**.

## Set a parent's policy

Click **Policy** next to a parent to change it later.

| Setting | What it does |
| --- | --- |
| **Limit** | The highest number of child tenants the parent may have. |
| **Address pattern** | How child tenant addresses are made. Leave it empty to put each child tenant's short name in front of the parent's address. |
| **Setup recipes** | What a new child tenant can start from. Select none to allow every one. |
| **Database** | Where each child tenant's data lives: its own file, its own database, its own schema, or tables with their own prefix in a shared database. Its own database keeps child tenants apart best. |
| **Database pool** | The database server used for the child tenants, as configured by the people who host the application. Its passwords are never shown to a tenant. |
| **Blocked features** | Features hidden from child tenants, on top of the ones that are always hidden because they could reach other sites. |
| **Denied to local users** | Permissions that a child tenant's own users never get, even its administrators. The parent's team is not affected. |
| **Keep removed child tenants** | How many days a removed child tenant stays suspended and can be restored. 0 removes it at once. |
| **Require two-factor authentication** | The parent's team must have signed in with two-factor authentication before they can open a child tenant. |
| **Check sessions every** | How soon a removed access rule or a disabled user takes effect in an open child tenant. |
| **End idle sessions after** / **End every session after** | Limits on how long a session in a child tenant may last. |
| **Tenant switcher** | Whether **Switch** opens the parent's picker page or shows it in a panel over the child tenant. |
| **Words on screen** | The words the parent's team sees instead of *parent*, *child tenant* and *child tenants*, for example *Practice*, *Client* and *Clients*. |

Click **Save**. Changes apply to the parent and its child tenants right away.

## Suspend, move or remove

On a parent's page:

| Action | What it does |
| --- | --- |
| **Suspend with children** | Suspends the parent and every running child tenant. Nobody can use them until you click **Resume with children**. |
| **Move** | Next to a child tenant, pick the new parent and click **Move**. Its address changes, and every open session from the old parent ends. |
| **Make ordinary** | Turns a parent with no child tenants back into an ordinary tenant. It keeps its data and address. |
| **Remove everything** | Only for a suspended parent. Removes each child tenant, then the parent, with all their data. Type the tenant name to confirm. This cannot be undone. |

A parent cannot be removed on the **Tenants** screen while it still has child tenants, so that no child tenant is left without its parent by accident.
