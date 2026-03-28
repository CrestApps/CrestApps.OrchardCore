# Shape Alternates

Shape alternates are template name candidates that Orchard Core tries in order. They let you target a specific content type, display type, part, field, or zone without changing drivers.

## Naming Rules

- `__` separates alternate segments; filenames use `-` in place of `__` (both work, but `-` is standard).
- A single `_` in shape type maps to `.` in file names.
- Display types are inserted with `_DisplayType` between the base shape and the alternate segments.

Shape type → file name examples:

| Shape Type | File Name |
|---|---|
| `Content__Article` | `Content-Article.cshtml` or `.liquid` |
| `Content_Summary__Article` | `Content-Article.Summary.cshtml` or `.liquid` |
| `Component__Header` | `Component-Header.cshtml` |

## Content Item Alternates

- `Content__[ContentType]` — content item shape for a specific content type.
- `Content_[DisplayType]__[ContentType]` — display-type-specific override (e.g., Summary).
- `Content__Alias__[Alias]`
- `Content__Slug__[Slug]`

## Stereotype Alternates

Content types with a stereotype use that stereotype as the base shape name instead of `Content`:

| Stereotype | Shape Pattern | File Example |
|---|---|---|
| `Widget` | `Widget__[ContentType]` | `Widget-Image.cshtml` |
| `Section` | `Section__[ContentType]` | `Section-Hero.cshtml` |
| `Block` | `Block__[ContentType]` | `Block-TextAndImage.cshtml` |

Same alternate patterns apply; replace `Content` with the stereotype value.

## Part Alternates

- `[ShapeType]` (often the part type name)
- `[ShapeType]_[DisplayType]`
- `[ContentType]_[DisplayType]__[PartType]`
- `[ContentType]_[DisplayType]__[PartName]`
- `[ContentType]_[DisplayType]__[PartType]__[ShapeType]`
- `[ContentType]_[DisplayType]__[PartName]__[ShapeType]`

Display mode variants (for parts with display modes):

- `[ShapeType]_[DisplayType]__[DisplayMode]_Display`
- `[ContentType]_[DisplayType]__[PartType]__[DisplayMode]_Display`
- `[ContentType]_[DisplayType]__[PartName]__[DisplayMode]_Display`

## Field Alternates

- `[ShapeType]` (often the field type name)
- `[ShapeType]_[DisplayType]` (field type with display type)
- `[PartType]__[FieldName]`
- `[ContentType]__[PartName]__[FieldName]`
- `[ContentType]__[FieldType]`
- `[FieldType]__[ShapeType]`
- `[PartType]__[FieldName]__[ShapeType]`
- `[ContentType]__[PartName]__[FieldName]__[ShapeType]`

## Zone Alternates

- `Zone__ZoneName` → `Zone-Footer.cshtml` (or `.liquid`)

## Taxonomy Term Alternates

- Term landing pages render the `TermPart` shape for the term content type.
- Alternate: `<TermContentType>__TermPart` → `Tag-TermPart.liquid`
- For the term header/body, override with `Content__<TermContentType>` → `Content-Tag.liquid`

## Debugging Alternates

- **Liquid**: `{{ Model.Metadata.Alternates | json | console_log }}` to the browser console.
- **Razor**: temporarily render `Model.Metadata.Alternates` in a `<pre>` tag.
- Remove debug snippets after verifying.

## Practical Tips

- Most-specific alternates win; keep templates targeted to avoid surprising overrides.
- When overriding a content item template and you just want a wrapper, prefer `@await DisplayAsync(Model.Content)` and let parts render with their own templates.
- Override `BagPart` only when you need custom item-level markup (e.g., FAQ accordion).
