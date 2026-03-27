# Taxonomy Examples

## Example 1: Blog Categories Taxonomy

### Recipe to Create a Categories Taxonomy

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "taxonomy-categories",
          "ContentType": "Taxonomy",
          "DisplayText": "Categories",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Categories"
          },
          "AliasPart": {
            "Alias": "categories"
          },
          "TaxonomyPart": {
            "Terms": [
              {
                "ContentItemId": "category-technology",
                "ContentType": "Category",
                "DisplayText": "Technology",
                "TitlePart": { "Title": "Technology" },
                "Terms": [
                  {
                    "ContentItemId": "category-web-dev",
                    "ContentType": "Category",
                    "DisplayText": "Web Development",
                    "TitlePart": { "Title": "Web Development" }
                  },
                  {
                    "ContentItemId": "category-mobile",
                    "ContentType": "Category",
                    "DisplayText": "Mobile Development",
                    "TitlePart": { "Title": "Mobile Development" }
                  }
                ]
              },
              {
                "ContentItemId": "category-design",
                "ContentType": "Category",
                "DisplayText": "Design",
                "TitlePart": { "Title": "Design" }
              },
              {
                "ContentItemId": "category-business",
                "ContentType": "Category",
                "DisplayText": "Business",
                "TitlePart": { "Title": "Business" }
              }
            ],
            "TermContentType": "Category"
          }
        }
      ]
    }
  ]
}
```

## Example 2: Tags Taxonomy (Flat)

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "taxonomy-tags",
          "ContentType": "Taxonomy",
          "DisplayText": "Tags",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Tags"
          },
          "AliasPart": {
            "Alias": "tags"
          },
          "TaxonomyPart": {
            "Terms": [
              {
                "ContentItemId": "tag-csharp",
                "ContentType": "Tag",
                "DisplayText": "C#",
                "TitlePart": { "Title": "C#" }
              },
              {
                "ContentItemId": "tag-aspnet",
                "ContentType": "Tag",
                "DisplayText": "ASP.NET",
                "TitlePart": { "Title": "ASP.NET" }
              },
              {
                "ContentItemId": "tag-orchardcore",
                "ContentType": "Tag",
                "DisplayText": "Orchard Core",
                "TitlePart": { "Title": "Orchard Core" }
              }
            ],
            "TermContentType": "Tag"
          }
        }
      ]
    }
  ]
}
```

## Example 3: Attaching Taxonomy Fields to Blog Post

```csharp
_contentDefinitionManager.AlterPartDefinition("BlogPost", part => part
    .WithField("Categories", field => field
        .OfType("TaxonomyField")
        .WithDisplayName("Categories")
        .WithSettings(new TaxonomyFieldSettings
        {
            TaxonomyContentItemId = "taxonomy-categories",
            Unique = false,
            LeavesOnly = false
        })
        .WithPosition("0")
    )
    .WithField("Tags", field => field
        .OfType("TaxonomyField")
        .WithDisplayName("Tags")
        .WithEditor("Tags")
        .WithSettings(new TaxonomyFieldSettings
        {
            TaxonomyContentItemId = "taxonomy-tags"
        })
        .WithPosition("1")
    )
);
```
