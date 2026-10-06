# CrestApps.OrchardCore Documentation

The documentation site for [CrestApps.OrchardCore](https://github.com/CrestApps/CrestApps.OrchardCore), built with [Docusaurus 3.10](https://docusaurus.io/).

**Live site:** [orchardcore.crestapps.com](https://orchardcore.crestapps.com)

This README is the contributor guide for people **and AI agents**. Read it before you add or change any page. The repository's `AGENTS.md` points here for everything about the docs site.

## Two manuals, one site

The site holds two manuals. Each has its own tab in the navbar, its own sidebar in `sidebars.js`, its own color and its own place in the breadcrumbs.

| | User Manual | Technical Manual |
| --- | --- | --- |
| **Reader** | People who use the app in the browser: agents, supervisors, managers, and the administrators who work in the admin screens. Most are not developers, and most are not administrators. Companies also use it to train their staff. | Developers and IT: the people who install, configure, deploy, operate and extend the modules. |
| **Content** | What a feature is for, when to use it, step-by-step instructions, field-by-field tables, screencasts, use cases, training paths, a glossary and troubleshooting. | Packages and feature IDs, `appsettings.json` and environment variables, recipes, architecture, APIs, extension points, operations and release notes. |
| **Files** | `docs/user-manual/**` | Every other page under `docs/` |
| **Sidebar** | `userManualSidebar` | `technicalSidebar` |
| **Color** | Amber | Steel blue |

### The dividing rule

> If it can be done in the browser, it belongs in the **User Manual**. If it needs code, a configuration file, an environment variable, recipe JSON, a server or a deployment, it belongs in the **Technical Manual**.

| You are documenting... | Manual |
| --- | --- |
| A new screen, button, field, menu item or admin setting | User Manual |
| What a feature is for, and a workflow that uses several screens | User Manual (a feature page, and a use case if it spans features) |
| What someone should do when a menu or button is missing | User Manual (`<AskYourAdmin />`, troubleshooting page) |
| A feature ID, a dependency between features, or a permission key | Technical Manual |
| An `appsettings.json` section or an environment variable | Technical Manual (the feature's page **and** `docs/configuration.md`) |
| A recipe step, a deployment step, a webhook or an API | Technical Manual |
| An interface, a service, a handler, or another extension point | Technical Manual |
| Architecture, scaling, monitoring, runbooks | Technical Manual |

Most features need **both**: a User Manual page that says how to use the screens, and a Technical Manual page that says how it is built and configured. The two pages link to each other.

## When to update the docs

Update the docs in the same change as the code. A code change without its docs is incomplete.

| Your code change... | Update |
| --- | --- |
| Adds or changes a screen, label, menu, button, setting or permission display name | The User Manual page for that screen. Labels in the docs must match the screen exactly. |
| Adds a feature | A User Manual page, a Technical Manual page, both sidebars, the feature in `docs/feature-reference.md`, and the training paths or use cases it belongs in. |
| Adds or changes configuration read from `appsettings.json` | The feature's Technical Manual page and `docs/configuration.md`, with the environment-variable form. |
| Adds or changes an extension point, recipe step, event or API | The Technical Manual page. |
| Renames or removes a feature, page or heading | Every link to it, plus a redirect in `docusaurus.config.js` for a removed page. |
| Changes public behavior in any way | The changelog file under `docs/changelog/` that matches `VersionPrefix` in `Directory.Build.props`. |

## Writing a User Manual page

Write for someone who has never seen the screen and has never written code.

1. **Front matter:** `title`, `sidebar_label`, a one-sentence `description`, and `technical_manual` (the Technical Manual page IDs for the same feature).
2. **Open with the purpose:** one or two sentences on what the feature is for, and when someone would use it.
3. **Access table and admin note**, right after the opening. Most readers can't turn features on or change permissions, so the note tells them to ask their administrator:

   ```markdown
   | | |
   | --- | --- |
   | **Menu** | Interaction Center > Management > Queues |
   | **Permission** | Manage Contact Center queues |
   | **Feature** | Contact Center Work Distribution |

   <AskYourAdmin />
   ```

4. **Screencast** when one exists: `<video controls preload="metadata" width="100%" aria-label="Screencast of ...">` with a `<source src="/img/docs/....mp4" type="video/mp4" />`.
5. **Tasks:** one heading per task, named after what the reader wants to do ("Create a queue"), with numbered steps.
6. **Fields:** a `| Field | What it does |` table per card or tab.
7. **Tips and troubleshooting** where readers commonly get stuck.

Rules:

- Use the labels exactly as the screen shows them, in **bold**, and write menu paths as **Interaction Center > Management > Queues**. Check labels against the code (`S["..."]` strings in views, `AdminMenu*.cs`).
- Use the feature **name** shown in **Tools > Features** (the `Name` in the module's `Manifest.cs`) and the permission name shown in the role editor. Never write a feature ID (`CrestApps.OrchardCore.X`) or a permission key.
- No code blocks, configuration files, JSON, recipes or command lines. Link to the Technical Manual page instead.
- Describe only what the product does today. Never mention test projects, test runs or internal plans. Use made-up names in examples ("John Smith", "Contoso").
- Plain language, short sentences, second person ("you").

## Writing a Technical Manual page

1. **Front matter:** `title`, `sidebar_label`, `description`, and `user_manual` (the User Manual page IDs for the same feature).
2. **Open with what the module adds** and a table of its feature IDs.
3. **Configuration:** show each setting as `appsettings.json` **and** as an environment variable, and add it to `docs/configuration.md`. Remember that a tenant's own `App_Data/Sites/{tenant}/appsettings.json` has no `OrchardCore` wrapper.
4. **Architecture, recipes, events, extension points and operations** as the feature needs.
5. **Do not repeat click-by-click instructions.** Summarize the admin screens in a sentence or two and link to the User Manual page.

## Linking the two manuals

Pages name their counterparts in front matter:

```markdown
---
title: Queues and Queue Groups
technical_manual:
  - contact-center/agents-queues-dialer
---
```

```markdown
---
title: Agents, Queues & Dialer
user_manual:
  - user-manual/queues
  - user-manual/dialer-profiles
---
```

The values are page IDs: the file path under `docs/` without the extension (`docs/user-manual/ai/chat.md` is `user-manual/ai/chat`). The site uses them to show a strip above the title and a card at the end of the page that link to the other manual. Also link to the other manual inside the text wherever a reader would naturally jump, with a normal relative Markdown link such as `[Queues](../user-manual/queues.md)`.

## Adding, moving or removing a page

1. Decide the manual with the dividing rule.
2. Create the file under `docs/user-manual/` or in the matching technical folder, with the front matter above.
3. Add the page ID to `userManualSidebar` or `technicalSidebar` in `sidebars.js`. A new **top-level** entry also gets an icon: `className: icon('name')`, where `name` is a [Lucide](https://lucide.dev/icons) icon. Copy its SVG from the `lucide-static` package into `static/img/icons/` and add a `.sidebar-icon--name` rule in `src/css/custom.css`.
4. When you move or remove a page, add a redirect from the old path to the `@docusaurus/plugin-client-redirects` options in `docusaurus.config.js`, so links from outside the site keep working.
5. Run the checks below.

## Checks before you open a pull request

```bash
cd src/CrestApps.Docs
npm run check:manuals
npm run build
```

`npm run check:manuals` fails when:

- a page is missing from the sidebars, listed twice, or listed in the wrong manual's sidebar;
- a `technical_manual` or `user_manual` entry points to a missing page or to a page in the same manual;
- a User Manual page has a code block, a feature ID, an access table without `<AskYourAdmin />`, or an access table without a `technical_manual` entry.

The build fails on any broken link, broken anchor or MDX error. CI runs both on every pull request.

## For AI agents

When a task touches the docs site, in addition to everything above:

- Read the existing pages around the one you change, and match their structure and tone.
- Verify every menu path, label, default and permission name against the source code before writing it. If you cannot verify something, leave it out and say so in your report.
- Keep the User Manual free of code and IDs; put those in the Technical Manual and link across.
- Keep both sidebars, the front matter links and `docs/feature-reference.md` in sync with the pages.
- Do not hard-wrap Markdown prose; keep each paragraph or list item on one line.
- Do not record or regenerate screencasts unless the user asks for them (see `AGENTS.md`).
- Run `npm run check:manuals` and `npm run build`, and report any failure rather than working around it.

## Markdown is MDX

- Do not use explicit heading IDs (`### Title {#id}`). Link to the generated slug instead (lowercase, spaces become `-`, punctuation is dropped).
- Write titled admonitions as `:::note[Title]`. `:::note Title` renders as plain text.
- Escape `<` and `{` in prose, or put them in inline code.
- Link to other pages with relative `.md` paths, and to images and videos with absolute `/img/...` paths.

## Site features

| Feature | Where |
| --- | --- |
| Audience strip above each title, and the card at the end of each page that links to the other manual | `src/components/Manual/ManualBar.js`, `ManualCrossLinks.js`, placed by `src/theme/DocItem/` |
| `<AskYourAdmin />` note and `<ManualSearch />` box, usable in any page without an import | `src/components/Manual/`, registered in `src/theme/MDXComponents.js` |
| The manual's crumb in the breadcrumbs (Home > User Manual > ...) | `src/theme/DocBreadcrumbs/Items/Home/` |
| Icons (`<Icon name="..." />` in React, `icon('...')` in `sidebars.js`) | `src/components/Icon/`, `static/img/icons/` (Lucide, ISC license) |
| Colors for each manual, the sidebar and the tables | `src/css/custom.css` |
| The manual rules | `scripts/check-manuals.mjs` |

### Search

The search plugin builds a separate index for the User Manual.

- **Navbar:** a **User Manual only** checkbox sits next to the search box. Until someone uses it, it is ticked on User Manual pages and clear elsewhere; after that, the reader's choice is remembered on every page. The plugin is patched (`patches/@easyops-cn+docusaurus-search-local+*.patch`, applied by `patch-package` on install) to accept the scope from the checkbox. When you upgrade the plugin, check that the patch still applies.
- **Search page:** a **Search only the User Manual** checkbox replaces the plugin's drop-down.
- **`<ManualSearch />`** (home page and the User Manual's first page) opens the search page with the checkbox ticked.

Search only works on a production build (`npm run build`, then `npm run serve`), not under `npm start`.

## Project structure

```
src/CrestApps.Docs/
├── docs/
│   ├── user-manual/       # User Manual (userManualSidebar)
│   │   ├── getting-started/
│   │   ├── use-cases/
│   │   ├── ai/
│   │   └── administration/
│   ├── intro.md           # Technical Manual overview (technicalSidebar from here down)
│   ├── getting-started.md
│   ├── configuration.md
│   ├── feature-reference.md
│   ├── ai/
│   ├── omnichannel/
│   ├── telephony/
│   ├── contact-center/
│   ├── modules/
│   ├── samples/
│   └── changelog/
├── patches/               # patch-package patches for npm dependencies
├── scripts/
│   └── check-manuals.mjs
├── sidebars.js
├── docusaurus.config.js   # Site configuration, search and redirects
├── src/components/        # Manual components, Icon
├── src/theme/             # Wrappers around Docusaurus and search plugin components
└── static/                # Images, screencasts, icons
```

## Local development

```bash
cd src/CrestApps.Docs
npm install
npm start
```

This starts a development server at `http://localhost:3000`, which reloads as you edit. To try search, build the site and serve it:

```bash
npm run build
npm run serve
```

## Versioning

The site keeps a version selector so older releases stay available while `main` continues to evolve. The unversioned `docs/` folder is the **Latest** version and tracks `main`. Each released version is frozen under `versioned_docs/` and `versioned_sidebars/`, with the list of published versions in `versions.json`. Versions before 3.0 have no User Manual; their Technical Manual tab shows that version, and their User Manual tab opens the latest User Manual.

Versions are proposed automatically on stable `vX.Y.Z` tag pushes by the `create_docs_version_pr.yml` GitHub Actions workflow, which opens a pull request that snapshots the current docs as `X.Y` (for example, `v2.1.0` produces the `2.1` version, served under `/docs/2.1/`). Patch tags create the `X.Y` docs version only when that version does not already exist; otherwise they are logged and skipped successfully. Prerelease tags are also skipped successfully. The workflow can be run manually with a `vX.Y.Z`, `X.Y.0`, `vX.Y`, or `X.Y` input; two-part inputs snapshot the matching `vX.Y.0` tag. If branch protection requires PR checks, configure a `DOCS_VERSION_PR_TOKEN` repository secret backed by a GitHub App token or fine-grained personal access token with contents and pull-request write access so the generated branch and PR can trigger the normal validation workflows; otherwise the workflow falls back to `GITHUB_TOKEN`.

To cut a version manually:

```bash
npx docusaurus docs:version 2.1
```

Commit the generated `versioned_docs/`, `versioned_sidebars/`, and `versions.json` so the frozen version persists across future deployments.

## Deployment

The site is deployed automatically to GitHub Pages by the `deploy_docs.yml` workflow on every push to `main` and on manual `workflow_dispatch` runs. Release tags create documentation-version pull requests instead of deploying directly, so branch and environment protection rules stay enforced.
