---
sidebar_label: Widgets and Recipe
title: Knowledge Base Widgets and Recipe
description: "The help center's widgets (search, categories, related articles), the zones the help center and article pages render on any theme, and the Knowledge Base recipe that places them."
user_manual: user-manual/knowledge-base/help-center
---

The help center's page (`/help`) and an article's page share three widgets, so they look alike. The **Knowledge Base** recipe places them.

## Widgets

Each widget is a content type with the `Widget` stereotype and one part, defined by the Knowledge Base migration (step 7 to 8).

| Widget type | Part | Shape | Shows |
| --- | --- | --- | --- |
| `HelpCenterSearchWidget` (**Help center search**) | `HelpCenterSearchPart` | `HelpCenterSearchPart` | A search box posting to `/help` (`?q=`). On a category's page (`?category=`) it searches within the category. |
| `KnowledgeBaseCategoriesWidget` (**Knowledge base categories**) | `KnowledgeBaseCategoriesPart` | `KnowledgeBaseCategoriesPart` | The categories with how many articles the reader may open in each (`KnowledgeBaseHelpCenter.GetCategoriesAsync`), the page's or the article's category picked |
| `RelatedArticlesWidget` (**Related articles**) | `RelatedArticlesPart` (`Count`, 1 to 20, 5 by default) | `RelatedArticlesPart` | On an article's page only: `KnowledgeBaseHelpCenter.GetRelatedAsync`, the articles of its category first, most helpful first, then those whose title or summary share the words of its title and summary (`KnowledgeArticleSearch.SuggestAsync`), never the article itself |

Every query keeps to what the reader may open, through Content Access Control's `ContentAccessRoleIndex`. `KnowledgeBasePageContext` tells a widget the page it is on: the published article of an `OrchardCore.Contents` display route, and the `q` and `category` of the help center's route. The shapes render the partials `_KnowledgeBaseSearch`, `_KnowledgeBaseCategories` and `_KnowledgeBaseArticles` (in the module's `Views/Shared`), which the help center's own page renders too.

## Zones on any theme

The Knowledge Base renders three layout zones itself (`KnowledgeBaseConstants.Zones`), so widgets placed in them show whatever the theme's layout renders:

| Zone | Rendered |
| --- | --- |
| `KnowledgeBaseTop` | Above the help center's page, and above an article |
| `KnowledgeBaseSidebar` | Beside the help center's articles, and beside an article (a `col-md-4` column) |
| `KnowledgeBaseAfterContent` | After the help center's articles, and after an article |

The help center's view (`HelpCenter/Index`) reads them with `KnowledgeBaseZones.Find(ThemeLayout, zone)`, which returns a zone only when widgets were put in it; with no widgets in the top or sidebar zone it renders its own search box or categories instead. An article's page is the `Content-KnowledgeArticle.Detail` template, Orchard Core's `Content` template with the three zones around it; a theme can override it.

To use a theme's own zones instead, such as a sidebar, move the widgets there under **Design > Widgets**: they then show where that theme renders the zone, on the pages of their layer.

## Recipe

`knowledge-base.recipe.json` (**Knowledge Base**, in the module's `Recipes` folder) runs these steps:

1. `feature`: enables `CrestApps.OrchardCore.KnowledgeBase`, `OrchardCore.Widgets` and `OrchardCore.Layers`.
2. `KnowledgeBaseZones`: adds the three zones to `LayerSettings.Zones` (**Design > Settings > Zones**), keeping the site's own zones and adding none twice. Use this step instead of the `Settings` step, which replaces the list.
3. `Layers`: a **Help center** layer whose rule is any of `UrlCondition` equals `/help` or starts with `/help/` (both case-insensitive), so it covers the help center and the articles under `help/{slug}`.
4. `Content`: the three widgets, on the **Help center** layer, in their zones, with fixed content item ids, so running the recipe again updates them rather than adding more.

```json
{
  "steps": [
    {
      "name": "KnowledgeBaseZones"
    }
  ]
}
```

The step takes no properties. Its schema is `KnowledgeBaseZonesRecipeStepSchema`, registered when CrestApps Recipes is on.
