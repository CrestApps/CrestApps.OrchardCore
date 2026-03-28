# Navigation Examples

## Example 1: Admin Menu for a Custom Module

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

public sealed class AdminMenu : INavigationProvider
{
    private readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> localizer)
    {
        S = localizer;
    }

    public Task BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        builder
            .Add(S["Content"], content => content
                .Add(S["Testimonials"], S["Testimonials"].PrefixPosition(), testimonials => testimonials
                    .Action("Index", "Admin", new { area = "CrestApps.Testimonials" })
                    .Permission(Permissions.ManageTestimonials)
                    .LocalNav()
                )
            );

        return Task.CompletedTask;
    }
}
```

## Example 2: Main Menu via Recipe

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "main-menu-001",
          "ContentType": "Menu",
          "DisplayText": "Main Menu",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "Main Menu"
          },
          "AliasPart": {
            "Alias": "main-menu"
          },
          "MenuPart": {},
          "MenuItemsListPart": {
            "MenuItems": [
              {
                "ContentType": "LinkMenuItem",
                "ContentItemId": "menu-item-home",
                "LinkMenuItemPart": {
                  "Name": "Home",
                  "Url": "~/"
                }
              },
              {
                "ContentType": "LinkMenuItem",
                "ContentItemId": "menu-item-blog",
                "LinkMenuItemPart": {
                  "Name": "Blog",
                  "Url": "~/blog"
                }
              },
              {
                "ContentType": "LinkMenuItem",
                "ContentItemId": "menu-item-about",
                "LinkMenuItemPart": {
                  "Name": "About Us",
                  "Url": "~/about"
                }
              },
              {
                "ContentType": "LinkMenuItem",
                "ContentItemId": "menu-item-contact",
                "LinkMenuItemPart": {
                  "Name": "Contact",
                  "Url": "~/contact"
                }
              }
            ]
          }
        }
      ]
    }
  ]
}
```
