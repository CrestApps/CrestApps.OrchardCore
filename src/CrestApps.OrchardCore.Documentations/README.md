# CrestApps.OrchardCore Documentation

Documentation site for the [CrestApps.OrchardCore](https://github.com/CrestApps/CrestApps.OrchardCore) project, built with [Docusaurus 3.9](https://docusaurus.io/).

**Live site:** [orchardcore.crestapps.com](https://orchardcore.crestapps.com)

## Local Development

```bash
cd src/CrestApps.OrchardCore.Documentations
npm install
npm start
```

This starts a local development server at `http://localhost:3000`. Most changes are reflected live without restarting.

## Build

```bash
npm run build
```

Generates static content into the `build` directory.

## Contributing to the Documentation

### Project Structure

```
src/CrestApps.OrchardCore.Documentations/
├── docs/                  # All documentation pages (Markdown)
│   ├── intro.md           # Homepage / Introduction
│   ├── getting-started.md # Getting started guide
│   ├── ai/                # AI module docs
│   │   ├── ai-services.md
│   │   ├── ai-chat.md
│   │   ├── data-sources/  # Nested category
│   │   ├── documents/
│   │   └── mcp/
│   ├── providers/         # AI provider docs
│   ├── omnichannel/       # Omnichannel module docs
│   ├── modules/           # Standard module docs
│   └── samples/           # Sample project docs
├── sidebars.js            # Sidebar navigation configuration
├── docusaurus.config.js   # Site configuration
├── src/css/custom.css     # Custom styles
└── static/                # Static assets (images, favicon)
```

### Adding a New Documentation Page

1. **Create the Markdown file** in the appropriate folder under `docs/`. For example, to add a page for a new AI feature:

   ```
   docs/ai/my-new-feature.md
   ```

2. **Add frontmatter** at the top of the file with at least a `sidebar_label` and `title`:

   ```markdown
   ---
   title: My New Feature
   sidebar_label: My New Feature
   ---

   # My New Feature

   Description of the feature...
   ```

3. **Register the page in `sidebars.js`** so it appears in the navigation. Open `sidebars.js` and add your page ID (the file path relative to `docs/` without the `.md` extension) to the appropriate category:

   ```javascript
   // sidebars.js
   const sidebars = {
     docsSidebar: [
       // ...
       {
         type: 'category',
         label: 'AI Suite',
         items: [
           'ai/ai-services',
           'ai/ai-chat',
           'ai/my-new-feature',  // <-- Add your page here
           // ...
         ],
       },
     ],
   };
   ```

### Adding a New Sidebar Category

To add an entirely new section to the navigation, add a new category object to the `docsSidebar` array in `sidebars.js`:

```javascript
{
  type: 'category',
  label: 'My New Section',
  collapsed: true,        // true = collapsed by default, false = expanded
  items: [
    'my-section/overview',   // maps to docs/my-section/overview.md
    'my-section/setup',      // maps to docs/my-section/setup.md
  ],
},
```

### Adding a Nested Sub-Category

For deeper navigation hierarchies, nest categories inside items:

```javascript
{
  type: 'category',
  label: 'AI Suite',
  items: [
    'ai/ai-services',
    {
      type: 'category',
      label: 'Data Sources',        // Sub-category
      items: [
        'ai/data-sources/index',    // maps to docs/ai/data-sources/index.md
        'ai/data-sources/elasticsearch',
        'ai/data-sources/azure-ai',
      ],
    },
  ],
},
```

### Page ID Convention

The page ID used in `sidebars.js` is the **file path relative to `docs/`** without the `.md` extension:

| File path | Page ID |
|---|---|
| `docs/getting-started.md` | `getting-started` |
| `docs/ai/ai-chat.md` | `ai/ai-chat` |
| `docs/ai/mcp/ftp.md` | `ai/mcp/ftp` |
| `docs/modules/users.md` | `modules/users` |

### Adding Images or Static Assets

Place images and other static files in the `static/` directory. Reference them in Markdown using an absolute path from the site root:

```markdown
![My diagram](/img/my-diagram.png)
```

### Linking Between Pages

Use relative Markdown links to reference other documentation pages:

```markdown
See the [AI Services](ai/ai-services.md) page for more details.
```

Or from a nested page back to a parent:

```markdown
See the [Getting Started](../getting-started.md) guide.
```

## Versioning

Docs versions are created automatically on tag pushes matching `v*.*.*` via the GitHub Actions workflow. To create a version manually:

```bash
npx docusaurus docs:version 2.0.0
```

## Deployment

The site is deployed automatically to GitHub Pages via the `deploy_docs.yml` workflow on every push to `main` or version tag push.
