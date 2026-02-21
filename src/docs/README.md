# CrestApps.OrchardCore.Docs

Documentation site for the [CrestApps.OrchardCore](https://github.com/CrestApps/CrestApps.OrchardCore) project, built with [Docusaurus 3.9](https://docusaurus.io/).

**Live site:** [orchardcore.crestapps.com](https://orchardcore.crestapps.com)

## Local Development

```bash
npm install
npm start
```

This starts a local development server at `http://localhost:3000`. Most changes are reflected live without restarting.

## Build

```bash
npm run build
```

Generates static content into the `build` directory.

## Versioning

Docs versions are created automatically on tag pushes matching `v*.*.*` via the GitHub Actions workflow. To create a version manually:

```bash
npx docusaurus docs:version 2.0.0
```

## Deployment

The site is deployed automatically to GitHub Pages via the `deploy_docs.yml` workflow on every push to `main` or version tag push.
