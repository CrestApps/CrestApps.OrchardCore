# Placement Rules

Placement controls where shapes render, whether they render, and which alternates/wrappers apply. Themes and modules can supply `placement.json` at their root.

## File Location and Format

- File name: `placement.json` at the root of a theme or module.
- JSON object: keys are shape types; values are arrays of placement rules.

```json
{
  "TextField": [
    { "place": "Content:1", "displayType": "Detail" }
  ]
}
```

## Filters (Rule Matching)

| Filter | Description |
|---|---|
| `displayType` | `Detail`, `Summary`, `SummaryAdmin`, etc. |
| `differentiator` | Target a specific part/field instance. |
| `contentType` | Single or array, supports `*` wildcard prefixes. |
| `contentPart` | Single or array. |
| `path` | Single or array of request paths. |

## Placement Info

| Key | Description |
|---|---|
| `place` | Target zone/position. Use `-` to hide the shape. Use `/ZoneName` to move to a layout zone. |
| `alternates` | List of alternates to add. |
| `wrappers` | List of wrapper shapes. |
| `shape` | Replace shape type. |

## Hide a Shape

```json
{
  "TextBodyPart": [
    { "place": "-", "contentType": "BlogPost", "displayType": "Summary" }
  ]
}
```

## Move a Shape to a Layout Zone

```json
{
  "HtmlBodyPart": [
    { "place": "/Content:1", "displayType": "Detail" }
  ]
}
```

## Differentiators

Differentiators uniquely identify shapes that share the same type:

| Pattern | Example |
|---|---|
| Part shapes | `[PartName]` or `[PartName]-[ShapeType]` |
| Field shapes | `[PartName]-[FieldName]` or `[PartName]-[FieldName]-[ShapeType]` |

### Field Display Mode Differentiator

If a field uses a display mode, the shape type changes and the differentiator must include it:

```json
{
  "TextField_Display": [
    {
      "place": "Content:1",
      "differentiator": "Blog-MyField-TextField_Display__Header"
    }
  ]
}
```

## Editor Grouping (Tabs/Cards/Columns)

Editor shapes can be grouped using modifiers in `place`:

| Modifier | Purpose |
|---|---|
| `#` | Tabs |
| `%` | Cards |
| `\|` | Columns |
| `;` | Group position |
| `_` | Column width |

Example:

```json
{
  "MediaField_Edit": [
    { "place": "Parts:0#Media;0", "contentType": ["Article"] }
  ],
  "HtmlField_Edit": [
    { "place": "Parts:0#Content;1", "contentType": ["Article"] }
  ]
}
```

## Placement Precedence

1. Startup project (acts like a super-theme)
2. Active theme (front-end or admin depending on request)
3. Modules (dependency order)

## Removing Shapes in Liquid

When you render a local zone like `Model.Content`, you can remove a specific shape before rendering:

```liquid
{% shape_remove_item Model.Content "Blog-Summary" %}
{% shape_remove_item Model.Content "HtmlBodyPart" %}
```

Use the shape differentiator as listed on the shape. Placement rules (`place: "-"`) are still valid when you want to hide shapes globally.

## Dynamic Parts (No Driver)

Dynamic parts render with `ContentPart` shape and use the part name as differentiator:

```json
{
  "ContentPart": [
    { "place": "MyGalleryZone", "differentiator": "GalleryPart" }
  ],
  "ContentPart_Summary": [
    { "place": "MyGalleryZone", "differentiator": "GalleryPart" }
  ]
}
```

## Complete Placement Example

A full `placement.json` for an Article content type:

```json
{
  "TitlePart": [
    { "place": "Header:1", "displayType": "Detail", "contentType": "Article" },
    { "place": "Header:1", "displayType": "Summary", "contentType": "Article" }
  ],
  "HtmlBodyPart": [
    { "place": "Content:5", "displayType": "Detail", "contentType": "Article" },
    { "place": "-", "displayType": "Summary", "contentType": "Article" }
  ],
  "CommonPart": [
    { "place": "Meta:1", "displayType": "Detail" }
  ],
  "MediaField_Edit": [
    { "place": "Parts:0#Media;0", "contentType": ["Article"] }
  ]
}
```
