---
sidebar_label: Building Front-End Assets
sidebar_position: 3
title: Building Front-End Assets
description: How the modules' JavaScript and CSS are built from source, the commands to run, the Assets.json format, and how to add a script, a stylesheet or a third-party library to a module.
---

# Building Front-End Assets

:::note[Technical Manual]
This is the Technical Manual, for developers and IT staff who install, configure, deploy and extend the modules. If you use the app in the browser, go to the [User Manual](user-manual/index.md) instead.
:::

The modules' scripts and stylesheets are written under each module's `Assets/` folder and built into its `wwwroot/` folder, which is what Orchard Core serves. The built files are committed, so you only need this page when you change a module's front end, add a library to one, or work on this repository.

:::tip[Using the NuGet packages?]
If you install the published packages in your own Orchard Core solution, the built scripts and stylesheets are already inside them. There is nothing to build.
:::

## Requirements

- **Node.js 24.** The version is pinned in `.node-version` at the repository root, which version managers such as fnm read. The build warns when another major version runs it, and fails in CI.
- **npm 11**, which ships with Node.js 24. Use npm: the lockfile is npm's, and other package managers would not honor it.

## Install

Run this once after cloning, and again whenever a `package.json` changes:

```bash
npm install
```

The repository is a single npm workspace. Every module that ships a third-party library declares it in its own `package.json`, and one install at the root installs them all into one lockfile, `package-lock.json`.

## Build

| Command | What it does |
| --- | --- |
| `npm run build` | Builds the asset groups whose sources changed since their last build. |
| `npm run rebuild` | Builds every asset group. |
| `npm run watch` | Builds, then rebuilds a group whenever one of its sources changes. Copies are not watched. |
| `npm run dry-run` | Lists the selected groups and the files each one reads and writes, without building. |
| `npm run clean` | Deletes the selected groups' outputs and the build cache. |
| `npm run lint` | Lints every module's scripts with ESLint. |
| `npm run check` | Type-checks the scripts that opt in with `// @ts-check`. |

Every command accepts filters after `--`. A group is selected when it matches any of them:

```bash
# One or more groups, by name
npm run build -- -n soft-phone.js
npm run build -- -n soft-phone.js,telephony-client.js

# By tag. Every group is tagged with its module's short name, and vendored libraries with "vendor"
npm run build -- -t Telephony
npm run watch -- -t ContactCenter
npm run dry-run -- -t vendor
```

`npm run build` decides what to build from the contents of the files, not their timestamps: a group is rebuilt when its definition, one of its sources, or one of its outputs changed. Switching branches therefore rebuilds only what differs.

:::warning[Commit the built files]
Commit the `wwwroot` files a build changes together with the sources that changed them. The build only writes a file when its contents change, so an unchanged output never shows up as modified. A pull request runs `npm run rebuild` and fails when the committed `wwwroot` does not match what the sources build.
:::

## How a module declares its assets

Each module lists its assets in an `Assets.json` file next to its `.csproj`. The file holds an array of asset groups:

```json
[
  {
    "action": "vite",
    "name": "telephony-client.js",
    "source": [
      "Assets/js/shared/call-timer.js",
      "Assets/js/soft-phone/format.js",
      "Assets/js/telephony-client.js"
    ],
    "dest": "wwwroot/scripts/telephony-client.js"
  },
  {
    "action": "vite",
    "name": "soft-phone.css",
    "source": "Assets/scss/soft-phone.scss",
    "dest": "wwwroot/styles/soft-phone.css"
  }
]
```

| Key | Meaning |
| --- | --- |
| `action` | `vite` builds the sources into a script or a stylesheet. `copy` copies files unchanged. |
| `name` | The group's name. It must be unique across the repository; `-n` selects groups by it. |
| `source` | A path, or an ordered list of paths, relative to the module folder. |
| `dest` | For `vite`, the readable output file. For `copy`, a folder ending in `/`, or a single file path to copy one source to under another name. |
| `tags` | Optional. Extra tags for `-t`, in addition to the module's short name. |
| `global` | Optional, for a `vite` script built from one package. Exposes the package's exports as `window[global]`. |

A `vite` group writes two files: the readable `dest` file and a minified sibling, for example `telephony-client.js` and `telephony-client.min.js`. Register both with the resource manager. By default, Orchard Core serves the minified file in production and the readable one in development:

```csharp
_manifest
    .DefineScript("telephony-client")
    .SetUrl(
        "~/CrestApps.OrchardCore.Telephony/scripts/telephony-client.min.js",
        "~/CrestApps.OrchardCore.Telephony/scripts/telephony-client.js")
    .SetVersion("1.0.0");
```

### Scripts

Scripts are built with [Vite](https://vite.dev/) into a classic script that a page loads with a plain `<script>` tag. It is not an ES module. The output targets current browsers and is minified for the `.min.js` file.

A group with several sources runs them in the order they are listed, so **list a helper before the script that uses it**. Files in a bundle share state through a namespace on `window` rather than through `import` statements:

```js
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    softPhone.formatPhoneNumber = function (number) {
        // ...
    };
}(typeof globalThis !== 'undefined' ? globalThis : window));
```

Each source is built as its own strict-mode module. Two habits that work in a plain script do not work in a bundle:

- A top-level `var` stays inside the bundle. To publish an API to the page, assign it to `window` explicitly, for example `window.checkoutPayment = (function () { ... })();`.
- Assigning a name that was never declared throws a `ReferenceError` instead of creating a global.

### Stylesheets

Groups whose `dest` ends in `.css` compile their `.scss` and `.css` sources with Dart Sass and write a readable `.css` file and a `.min.css` file minified with Lightning CSS.

### Third-party libraries

Add the package to the `package.json` of the module that ships it, from the repository root:

```bash
npm install some-library@1.2.3 -w crestapps.orchardcore.resources
```

Then choose how the module ships it.

**Copy the files the package already builds.** Use this when the package's `dist` folder has browser-ready files. A `source` path that starts with `node_modules/` is found whether npm installed the package in the module or at the repository root:

```json
{
  "action": "copy",
  "name": "flatpickr-js",
  "source": [
    "node_modules/flatpickr/dist/flatpickr.js",
    "node_modules/flatpickr/dist/flatpickr.min.js"
  ],
  "dest": "wwwroot/vendors/flatpickr/js/",
  "tags": ["vendor"]
}
```

**Bundle a package that ships only ES modules or CommonJS.** A `vite` group with a `global` builds the package into a classic script that sets one global, with a minified copy:

```json
{
  "action": "vite",
  "name": "sip.js",
  "source": "node_modules/sip.js/lib/index.js",
  "dest": "wwwroot/vendors/sip.js/sip.js",
  "global": "SIP",
  "tags": ["vendor"]
}
```

Pin an exact version for a library whose behavior you have tested against, such as a telephony SDK, so an install never upgrades it unnoticed.

## Add an asset to a module

1. Write the source under the module's `Assets/` folder, for example `Assets/js/my-widget.js`.
2. Add a group to the module's `Assets.json`. Create the file next to the `.csproj` if the module has none yet; the build finds every `Assets.json` under `src/Modules`, `src/Startup` and `src/Themes`.
3. Build it: `npm run build -- -n my-widget.js`.
4. Register the `.min.js` and `.js` pair in the module's resource manifest, and require it from a view or a display driver.
5. Commit the source, the `Assets.json` change and the built `wwwroot` files.

## Lint and type-check

`npm run lint` runs ESLint over every module's `Assets` scripts with its recommended rules. It catches mistakes such as a name that is used but never declared, which in a browser only fails when that line runs. When a script reads a global that another script on the page defines, such as `signalR` or `Stripe`, add it to the page globals in `eslint.config.mjs`.

`npm run check` type-checks, with TypeScript, the scripts whose first line is `// @ts-check`. Nothing is compiled: TypeScript only reports errors, and Vite still builds the scripts. Add the comment to a script once it checks clean, and keep it clean from then on.

A pull request runs both, and fails on any error.

## Troubleshooting

| Problem | What to do |
| --- | --- |
| `'node_modules/...' was not found in any node_modules folder` | Run `npm install` at the repository root. |
| The build warns that another Node.js version is running | Switch to Node.js 24, the version in `.node-version`. |
| A script works in the readable file but a global is missing on the page | The script published its API through a top-level `var` or an undeclared assignment. Assign it to `window` instead. |
| The pull request fails "Test building assets" | Run `npm run lint`, `npm run check` and `npm run rebuild`, fix what they report, and commit the `wwwroot` changes the rebuild makes. |
| `npm run build` skips a group you expected to build | Run `npm run rebuild -- -n <group name>`, or `npm run clean` to clear the build cache. |
