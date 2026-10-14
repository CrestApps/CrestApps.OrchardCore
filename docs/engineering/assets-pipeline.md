# Front-end asset pipeline

Every module that ships JavaScript or CSS declares it in an `Assets.json` next to its `.csproj`. A small CLI in
`.scripts/assets/` reads those files and builds each asset group with **Vite**, the only bundler, into the module's
`wwwroot/`. The built files are committed: `dotnet build`, the NuGet packages and the Playwright harness all serve
them as they are.

```bash
npm install          # once, and whenever a package.json changes
npm run build        # build the groups whose sources changed
npm run rebuild      # build every group
npm run watch        # rebuild a group when one of its sources changes
npm run dry-run      # list groups and the files each one writes
npm run clean        # delete outputs and the build cache
```

Filters work with every command: `npm run build -- -n soft-phone.js` (group names, comma-separated) and
`npm run build -- -t Telephony` (tags; every group is tagged with its module's short name, and vendored libraries
with `vendor`).

## Requirements

- **Node.js 24**, pinned in `.node-version`. The Node major decides the npm major, and npm majors disagree about the
  lockfile, so CI fails on a mismatch and a local build warns.
- **npm workspaces.** The root `package.json` lists `src/Modules/*` and `src/Startup/*` as workspaces, so one
  `npm install` at the root installs every module's packages into one lockfile. There are no per-module lockfiles.

## Where dependencies go

| File | Holds |
| --- | --- |
| Root `package.json` | Build, lint and test tooling only: Vite, Sass, ESLint, TypeScript (type checks only) and Vitest. |
| A module's `package.json` | The libraries that module ships in its `wwwroot` (for example Resources declares Chart.js, SIP.js and the Telnyx SDK). |

Add a library to the module that copies or bundles it (`npm install <pkg> -w <workspace name>`). A version that
should not float, such as a vendored SDK the soft phone was tested against, is pinned exactly.

## Assets.json

`Assets.json` is an array of asset groups. The shape follows Orchard Core's assets manager, with two actions.

| Key | Meaning |
| --- | --- |
| `action` | `vite` builds; `copy` copies files unchanged. |
| `name` | Unique across the repository; used by `-n`. |
| `source` | A path, or an ordered array of paths, relative to the module. A path that starts with `node_modules/` is found the way Node finds packages, walking up from the module, so it works whether npm hoisted the package to the root or kept it in the module. |
| `dest` | `vite`: the readable output file; the build also writes its `.min` sibling. `copy`: a folder ending in `/` (each file keeps its name), or one file path for a single source (which renames it). |
| `tags` | Optional; used by `-t`. |
| `global` | `vite` script groups with one source only: bundle that package and expose its exports as `window[global]`. |

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
    "name": "sip.js",
    "source": "node_modules/sip.js/lib/index.js",
    "dest": "wwwroot/vendors/sip.js/sip.js",
    "global": "SIP",
    "tags": ["vendor"]
  },
  {
    "action": "copy",
    "name": "flatpickr-css",
    "source": ["node_modules/flatpickr/dist/flatpickr.css", "node_modules/flatpickr/dist/flatpickr.min.css"],
    "dest": "wwwroot/vendors/flatpickr/css/",
    "tags": ["vendor"]
  }
]
```

### What a `vite` group produces

- **Scripts** are bundled into a classic IIFE script, not an ES module, because Razor views load them through the
  resource manager with a plain `<script>` tag. The readable file carries a "generated" banner; the `.min.js` is
  minified with Oxc. The target is Vite's modern-browser baseline.
- **Several sources** become one entry that imports them in the listed order, so they run in that order, as the
  concatenated files used to. The sources do not import one another; they share state through namespaces on
  `window`/`globalThis` (for example `root.CrestAppsSoftPhone`), so **a helper must be listed before the script that
  uses it**. Tests in `tests/js` and the Playwright `SoftPhoneAssets` helper read these lists to enforce that.
- **Each source is its own module**, so a top-level `var` no longer leaks onto `window`, and modules are strict, so
  assigning an undeclared name throws. A script that publishes an API assigns it explicitly:
  `window.checkoutPayment = (function () { ... })();`.
- **Stylesheets** (`.css`/`.scss`) are compiled with Dart Sass and minified with Lightning CSS.
- Outputs are written with LF line endings, and only when their contents change.

### Incremental builds

`npm run build` keeps a record per group in `node_modules/.cache/crestapps-assets/`: the hash of the group's
definition and the build tooling, of every module that went into its last bundle, and of every file it wrote. A
group is rebuilt when any of those change, or when an output was edited or deleted. Content hashes, not timestamps,
so switching branches or cloning fresh rebuilds what actually differs. `npm run rebuild` ignores the cache.

## CI

`assets_validation.yml` runs `npm ci` and `npm run rebuild`, renormalizes line endings, and fails if git reports
any change: the committed `wwwroot` must be what the sources build. Fix a failure by running `npm run rebuild`
locally and committing the result. `pr_ci.yml` runs the Vitest suite.

## Compared with Orchard Core's assets manager

Orchard Core moved from the same Gulp pipeline this repository used to `.scripts/assets-manager`, a Concurrently-
based CLI over Yarn 4 workspaces that supports Parcel, Vite, Webpack, Sass, `min`, `concat`, `copy` and `run`. This
pipeline takes its shape (one `Assets.json` array of `action`/`name`/`source`/`dest`/`tags` groups, `-n`/`-t`
filters, a pinned `.node-version`, committed output checked in CI) and deliberately narrows it:

- **One bundler.** Vite covers bundling, Sass, minification and vendor IIFE builds, and it is already the engine
  under Vitest, so building and testing share one toolchain. `copy` is the only other action.
- **npm, not Yarn.** npm workspaces give one lockfile and one install without adding corepack and a second package
  manager.
- **No Concurrently.** Groups build in-process through Vite's API, a few at a time.

## Lint and type checks

- `npm run lint` runs ESLint (`eslint.config.mjs`) over every module's `Assets` scripts and the build scripts, with the
  recommended rules. The sources are linted as modules, as the build bundles them, and `no-undef` catches a name that is
  used but never declared. Page globals the scripts read as bare names (`signalR`, `flatpickr`, `Stripe`, ...) are
  listed in the config. A deliberately ignored `catch (error)` is not reported.
- `npm run check` runs TypeScript (`tsconfig.json`) as a type checker only, over the scripts that opt in with
  `// @ts-check` on their first line. Nothing is emitted. Opt a file in once it checks clean; converted ES modules are
  the natural candidates.

`assets_validation.yml` runs both before it rebuilds the assets.

## Phase 2

Decided on 2026-10-14:

- **Sources become ES modules; the output stays classic scripts.** Helpers `export` functions and the main scripts
  `import` them, so the module graph orders a bundle instead of `Assets.json`, and tests import named exports
  instead of reading `globalThis`. Vite still emits IIFE scripts, so the resource manifests, the inline scripts
  that call `flatpickrCulture`, `checkoutPayment` or `stripeCheckoutPayment` straight away, and the `window` APIs
  other bundles use (`telephonyClient`, `telephonySoftPhone`, `contactCenterRealTime`) stay as they are.
  `type="module"` output was not chosen: module scripts are deferred, which would change that timing.
- **Shared helpers stay duplicated across bundles for now** (a few KB per page, up to about 21 KB minified on the
  supervisor dashboard). A shared classic "core" bundle, or module output with shared chunks, can remove it later.
- **ESLint now, type checks opt-in** (above).
- **One pull request per area:** lint and type-check tooling, then Messaging, then Contact Center, then the
  Telephony soft phone.
- **No dev server.** The scripts are classic scripts served by Orchard Core, so there is nothing for Vite's dev server
  or hot module replacement to attach to; `npm run watch` rebuilds on save.
