---
sidebar_label: Child Tenants
title: Child Tenants
description: Create, open and manage the sites your organization runs for others, and decide who on your team may open each one.
technical_manual:
  - modules/tenant-hierarchy
---

Some organizations run a separate site for each business they look after. A bookkeeping firm, for example, may keep one site per client. In this system your organization's site is the **parent** and each site you run for someone else is a **child tenant**. Use this page to add a child tenant, open it without signing in again, change its features, and choose who on your team may open it.

This video is the whole tour: how a parent adds, opens and manages its clients, decides who may open them, and sees what happened. Each section below also has a short screencast of just that task.

<video controls preload="metadata" width="100%" poster="/img/docs/tenant-hierarchy.jpg" aria-label="Video overview of the Tenant Hierarchy: making a parent, adding and opening clients, managing them, access rules, activity and security">
  <source src="/img/docs/tenant-hierarchy.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/tenant-hierarchy.vtt" srcLang="en" label="English" default />
</video>

Your platform administrator may have renamed these words for your organization. A bookkeeping firm might see **Practice**, **Client** and **Clients** instead of *parent*, *child tenant* and *child tenants*. The screens work the same way whatever the words are.

| | |
| --- | --- |
| **Menu** | Child tenants > All child tenants; Child tenants > Open a child tenant; Child tenants > Access; Child tenants > Activity |
| **Permission** | View child tenants; Create child tenants; Edit, suspend, resume and reload child tenants; Enable and disable features in child tenants; Remove child tenants; Manage who may enter child tenants; Enter child tenants that an access grant covers; View Audit Trail (for **Activity**) |
| **Feature** | Parent Tenant (turned on by your platform administrator), which also turns on Audit Trail |

<AskYourAdmin />

## How it works

- Each child tenant is a complete site of its own, with its own address, users, content and data. Nothing in one child tenant is visible from another.
- Its address is made from a short name you choose and your own address, for example *northwind-traders.your-firm.example.com*.
- People on your team **open** a child tenant from your site. They arrive signed in, with the roles that an access rule gives them there. They never need a password for the child tenant.
- The child tenant's own staff, if it has any, sign in there directly with their own accounts. They cannot see your site or any other child tenant.
- Everything your team does in a child tenant is recorded under their own name, so you can always tell who did what.
- Other organizations on the same platform never see your site or your child tenants, and you never see theirs. See [Kept apart from other organizations](#kept-apart-from-other-organizations).

## See your child tenants

Open **Child tenants > All child tenants**. Each row shows the name, the address, the description and the state:

| State | Meaning |
| --- | --- |
| **Setting up** | The site is being created. The list refreshes on its own and shows **Running** after a few seconds. |
| **Running** | The site is open for business. |
| **Suspended** | Nobody can use the site, including its own staff, until you resume it. |
| **Removal scheduled** | The site is suspended and is removed when its waiting period ends. You can still restore it. |
| **Removing** | The site and its data are being removed. |
| **Setup failed** | The site could not be created. Click **Retry**, or **Remove from list** to take it off the list. |

Your plan sets how many child tenants you may have. When you reach it, **Add child tenant** is greyed out, and pointing at it tells you the limit. Use the search box to find one by name or address, and the **State** and **Sort** menus to narrow and order the list.

## Add a child tenant

<video controls preload="metadata" width="100%" aria-label="Screencast of adding a child tenant: typing its name, checking its address, picking what it starts from, and watching it go from Setting up to Running">
  <source src="/img/docs/tenant-hierarchy-create.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/tenant-hierarchy-create.vtt" srcLang="en" label="English" default />
</video>

1. Open **Child tenants > All child tenants** and click **Add child tenant**.
2. Type the **Name** everybody sees, for example *Northwind Traders*.
3. Check the **Address**. It is filled in from the name, and you can change it. Use lowercase letters, digits and hyphens, 3 to 40 characters. The screen tells you at once whether the address is available.
4. Optionally add a **Description**. Only your team sees it, in the list.
5. Under **Start from**, pick what the new site starts with, for example **Blank site**.
6. Click **Create client** (or **Create** with your organization's word).

The list shows the new child tenant as **Setting up**, then **Running**. When it is running, an **Open** button appears.

## Open a child tenant

<video controls preload="metadata" width="100%" aria-label="Screencast of opening a child tenant without a password, the building icon that shows where you are, switching to another child tenant and going back">
  <source src="/img/docs/tenant-hierarchy-open.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/tenant-hierarchy-open.vtt" srcLang="en" label="English" default />
</video>

- Click **Open** next to the child tenant in the list, or
- open **Child tenants > Open a child tenant** and pick it from the list.

You arrive in the child tenant's admin, already signed in. Click the building icon in the top bar to see where you are, for example *You are working in Northwind Traders, through Your Firm*, and these choices:

| Choice | What it does |
| --- | --- |
| **Switch child tenant** | Shows your other child tenants so you can jump to another one. |
| **Back to** *your organization* | Returns to your own site. You stay signed in to the child tenant. |
| **Leave this child tenant** | Signs you out of the child tenant and returns to your own site. |

On the **Open a child tenant** page, click the star next to a child tenant to mark it as a favorite. When you have more than a handful of child tenants, favorites and the ones you opened recently are listed first, and a search box appears. **Sign out of every child tenant** ends every session you have open in your child tenants, on every device.

If a child tenant says **You cannot open this**, you do not have an access rule for it, or it is suspended. Ask the person who manages access in your organization.

:::note[Your session follows your own account]
When you sign out of your own site, are disabled, change your password, or lose your access rule, your open child tenant sessions end too, within a few minutes at most.
:::

## Change a child tenant

<video controls preload="metadata" width="100%" aria-label="Screencast of managing a child tenant: its features, suspending and resuming it, removing it, and restoring it while its removal is scheduled">
  <source src="/img/docs/tenant-hierarchy-manage.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/tenant-hierarchy-manage.vtt" srcLang="en" label="English" default />
</video>

Open **Child tenants > All child tenants**, click **Manage** next to the child tenant, and pick a choice:

| Choice | What it does |
| --- | --- |
| **Edit** | Change the name, the address or the description. If you change the address, the old address stops working at once, so tell the people who use it. |
| **Features** | Turn features of the child tenant on or off. Changing a feature restarts the child tenant, which takes a few seconds. Features that could reach other sites are hidden, and some features are always on. |
| **Access** | Rules for this child tenant only. See [Decide who may open child tenants](#decide-who-may-open-child-tenants). |
| **Activity** | Everything that happened to this child tenant, in the Audit Trail. |
| **Reload** | Restarts the child tenant, for example after a change that has not shown up yet. |
| **Suspend** | Closes the child tenant to everybody until you **Resume** it. Its address then shows that the site is not available. |
| **Remove** | Only for a suspended child tenant. See below. |

To change several child tenants at once, tick them in the list and use the bulk action menu.

### Remove a child tenant

1. **Suspend** the child tenant first.
2. Click **Manage > Remove**.
3. Type the child tenant's name exactly as it is shown, and click **Remove child tenant**.

If your plan keeps removed child tenants for a number of days, the child tenant shows **Removal scheduled** and you can **Restore** it until the waiting period ends. Otherwise it is removed at once with all its users, content and data, and this cannot be undone.

## Decide who may open child tenants

<video controls preload="metadata" width="100%" aria-label="Screencast of the access rules: letting everyone with a role open every child tenant, and adding a rule for one person">
  <source src="/img/docs/tenant-hierarchy-access.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/tenant-hierarchy-access.vtt" srcLang="en" label="English" default />
</video>

People on your team never get a password for a child tenant. They open it from your site and are signed in to it automatically. **Access rules** decide who may open which child tenants, and which roles they have once they are inside. Someone who matches no rule cannot open a child tenant.

Open **Child tenants > Access** for rules that cover every child tenant, or **Manage > Access** next to one child tenant for rules that cover only that one. The **Rules** table reads like a sentence: *Who in your organization*, *Can open*, *Roles inside the child tenant*. A child tenant's page also lists the rules for every child tenant, because they apply there too.

To add a rule:

1. Under **Who in** *your organization*, choose **Everyone with a role**, then pick the role from your own site, for example *Bookkeeper*. Or choose **One person**, click **Select a person** and search for them.
2. Under **Their roles inside the child tenant**, tick the roles they get there, for example *Editor*. These decide what they can see and do once they open it. A role that does not exist in a child tenant is ignored there.
3. Click **Add rule**.

When someone matches several rules, they get every role of every rule. To take access away, click **Remove** next to a rule. Open sessions that depend on it end at their next check, within a few minutes.

:::tip
Give access through roles rather than one user at a time. When someone joins or leaves the team, changing their role on your own site updates their access to every child tenant at once.
:::

## See what happened

<video controls preload="metadata" width="100%" aria-label="Screencast of the activity in the Audit Trail: the tenant hierarchy events, who did each one, and the details of an event">
  <source src="/img/docs/tenant-hierarchy-activity.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/tenant-hierarchy-activity.vtt" srcLang="en" label="English" default />
</video>

Everything that happens to your child tenants is recorded in the **Audit Trail**, under the **Tenant Hierarchy** category. Open **Child tenants > Activity** for the whole history, or **Manage > Activity** for one child tenant. Each line shows what happened, when, who did it, and which child tenant, with details such as the roles someone received, how they signed in, why a session ended and the network address it came from. Click **Details** for the full event.

The Audit Trail keeps events for as long as its settings say, under **Settings > Audit Trail**. There you can also turn single events off.

## Kept apart from other organizations

Your platform may host many organizations, each with its own child tenants. They are kept apart from yours in every direction:

- Your lists, searches, tenant switcher and Audit Trail show only your own child tenants. Another organization's child tenants never appear, even when you search for their name or address.
- Nobody in your organization can open, change, suspend or remove another organization's child tenant, and nobody there can do it to yours. A link to another organization's child tenant gives the same **You cannot open this** page as one that does not exist.
- Being signed in to your own site does not sign you in anywhere else. Another organization's site, and its child tenants, ask for their own sign-in, and your account does not exist there.
- Access rules only ever cover your own child tenants, and only your own team.
- A child tenant cannot see or reach your site, the other child tenants, or any other organization.

## Troubleshooting

| What you see | Why, and what to do |
| --- | --- |
| **Add child tenant** is greyed out | You reached the number of child tenants your plan allows. Point at the button to see the limit, and ask your platform administrator to raise it, or remove a child tenant you no longer need. |
| **This address is already in use** or **This address is reserved** | Another of your child tenants uses the address, or it is kept for the system. Pick another one. |
| **Setup failed** | The site could not be created. Click **Retry**. If it fails again, click **Remove from list** and tell your platform administrator. |
| **You cannot open this** | You have no access rule for the child tenant, it is suspended, or it is not one of yours. Ask the person who manages access in your organization. |
| **Two-factor sign-in required** | Your organization requires it before you open a child tenant. Sign out of your own site, and sign in again with two-factor authentication. |
| You are signed out of a child tenant | Your session ended: you were idle too long, it reached its maximum length, you signed out of your own site, or your access rule changed. Open the child tenant again from your site. |
| A child tenant's address shows **This site is not available** | The child tenant is suspended or removed. Resume or restore it, or check the address. |
| **Changed by platform** | Your platform administrator moved the child tenant to another organization, or made it an ordinary tenant. Click **Remove from list** to clear it. |
