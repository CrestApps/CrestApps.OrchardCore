# Localization Examples

## Example 1: PO Translation File (French)

### Localization/fr.po

```po
# French translations for CrestApps.MyModule

msgid "Welcome to our site"
msgstr "Bienvenue sur notre site"

msgid "Blog Post"
msgstr "Article de blog"

msgid "Categories"
msgstr "Catégories"

msgid "Read more"
msgstr "Lire la suite"

msgid "Published on {0}"
msgstr "Publié le {0}"

msgid "Written by {0}"
msgstr "Écrit par {0}"

msgid "No results found"
msgstr "Aucun résultat trouvé"

# Plural forms
msgid "One comment"
msgid_plural "{0} comments"
msgstr[0] "Un commentaire"
msgstr[1] "{0} commentaires"
```

## Example 2: Localization Settings Recipe

```json
{
  "steps": [
    {
      "name": "Settings",
      "LocalizationSettings": {
        "DefaultCulture": "en-US",
        "SupportedCultures": [
          "en-US",
          "fr-FR",
          "es-ES",
          "de-DE",
          "ja-JP"
        ]
      }
    }
  ]
}
```

## Example 3: Using Localization in a Controller

```csharp
using Microsoft.Extensions.Localization;

public sealed class BlogController : Controller
{
    private readonly IStringLocalizer S;

    public BlogController(IStringLocalizer<BlogController> localizer)
    {
        S = localizer;
    }

    public IActionResult Index()
    {
        ViewData["Title"] = S["Blog Posts"];
        return View();
    }

    public IActionResult Details(string id)
    {
        var post = GetPost(id);
        if (post == null)
        {
            TempData["Error"] = S["The requested blog post was not found."];
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = post.Title;
        ViewData["Published"] = S["Published on {0}", post.PublishedDate.ToString("D")];
        return View(post);
    }
}
```
