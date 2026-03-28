# Shape Build/Override Workflow

Use this checklist to safely build or override a shape (e.g., a content item template, widget, or section).

## Step 1: Identify Content Type and Parts

- Locate the content type and note:
  - **Stereotype** (Widget, Section, Block, or none) — this drives the base shape name.
  - Parts attached and fields on those parts.
  - Field types and settings.
- If `ContentDefinition.json` exists, extract the relevant type/part slice rather than reading the full JSON.

## Step 2: Determine Template Name

Choose the file name based on the stereotype:

| Stereotype | Template Pattern | File Example |
|---|---|---|
| (none/default) | `Content-<ContentType>` | `Content-Article.cshtml` |
| Widget | `Widget-<ContentType>` | `Widget-Image.liquid` |
| Section | `Section-<ContentType>` | `Section-Hero.liquid` |
| Block | `Block-<ContentType>` | `Block-TextAndImage.cshtml` |

Add display type if needed: `Content-Article.Summary.cshtml` (or `.liquid`).

See `shape-alternates.md` for the full alternates reference.

## Step 3: Map Fields to Properties

Use the field type to determine the value property:

| Field Type | Property | Access Pattern (Razor) |
|---|---|---|
| `TextField` | `.Text` | `Model.ContentItem.Content.<Part>.<Field>.Text` |
| `HtmlField` | `.Html` | `Model.ContentItem.Content.<Part>.<Field>.Html` |
| `MediaField` | `.Paths[]`, `.MediaTexts[]` | `Model.ContentItem.Content.<Part>.<Field>.Paths[0]` |
| `NumericField` | `.Value` | `Model.ContentItem.Content.<Part>.<Field>.Value` |
| `BooleanField` | `.Value` | `Model.ContentItem.Content.<Part>.<Field>.Value` |
| `LinkField` | `.Url`, `.Text`, `.Target` | `Model.ContentItem.Content.<Part>.<Field>.Url` |
| `ContentPickerField` | `.ContentItemIds[]` | `Model.ContentItem.Content.<Part>.<Field>.ContentItemIds` |
| `TaxonomyField` | `.TermContentItemIds[]` | `Model.ContentItem.Content.<Part>.<Field>.TermContentItemIds` |

For fields on the type itself, the part name equals the type name.

## Step 4: Render in Templates

### Razor — Media Field

```cshtml
@{
    var imgPath = Model.ContentItem.Content.PartName.Image.Paths?[0]?.ToString();
}
@if (!string.IsNullOrEmpty(imgPath))
{
    <img asset-src="@imgPath" asp-append-version="true" alt="">
}
```

### Liquid — Media Field

```liquid
{% assign img = Model.ContentItem.Content.PartName.Image.Paths[0] %}
<img src="{{ img | asset_url | resize_url: width: 1200 }}" alt="">
```

### Razor — Text Field

```cshtml
@Model.ContentItem.Content.PartName.MyTextField.Text
```

### Liquid — Text Field

```liquid
{{ Model.ContentItem.Content.PartName.MyTextField.Text }}
```

### Rendering Child Content

When overriding a content item template and you just want a wrapper around child shapes:

```cshtml
@await DisplayAsync(Model.Content)
```

This lets parts (including BagPart) render with their own templates.

## Step 5: Apply Placement (if needed)

- If scoping to a specific instance/part, use placement with `differentiator` (see `placement-rules.md`).
- For Section/Widget stereotypes, alternates often suffice without placement changes.

## Step 6: Validate

Optional debugging when values look wrong or are missing:

- **Liquid**: `{{ Model.Metadata.Alternates | json | console_log }}` and `{{ Model.ContentItem | json | console_log }}`
- **Razor**: temporarily render a `<pre>` with `Model.Metadata.Type` or `Model.Metadata.Alternates`

Remove debug snippets after verifying. Verify the target display types you care about (`Detail`, `Summary`, etc.).
