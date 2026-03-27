# Menu Examples

## Example 1: Complete Site Navigation with Footer Menu via Recipe

Set up two menus — a primary navigation and a footer menu — and place them in zones:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Menu",
        "OrchardCore.Widgets",
        "OrchardCore.Layers"
      ],
      "disable": []
    },
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "primary-nav",
          "ContentType": "Menu",
          "DisplayText": "Primary Navigation",
          "Latest": true,
          "Published": true,
          "MenuPart": {},
          "TitlePart": {
            "Title": "Primary Navigation"
          },
          "MenuItemsListPart": {
            "MenuItems": [
              {
                "ContentItemId": "nav-home",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Home",
                "LinkMenuItemPart": {
                  "Name": "Home",
                  "Url": "~/"
                }
              },
              {
                "ContentItemId": "nav-about",
                "ContentType": "LinkMenuItem",
                "DisplayText": "About Us",
                "LinkMenuItemPart": {
                  "Name": "About Us",
                  "Url": "~/about"
                }
              },
              {
                "ContentItemId": "nav-services",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Services",
                "LinkMenuItemPart": {
                  "Name": "Services",
                  "Url": "~/services"
                },
                "MenuItemsListPart": {
                  "MenuItems": [
                    {
                      "ContentItemId": "nav-consulting",
                      "ContentType": "LinkMenuItem",
                      "DisplayText": "Consulting",
                      "LinkMenuItemPart": {
                        "Name": "Consulting",
                        "Url": "~/services/consulting"
                      }
                    },
                    {
                      "ContentItemId": "nav-development",
                      "ContentType": "LinkMenuItem",
                      "DisplayText": "Development",
                      "LinkMenuItemPart": {
                        "Name": "Development",
                        "Url": "~/services/development"
                      }
                    },
                    {
                      "ContentItemId": "nav-support",
                      "ContentType": "LinkMenuItem",
                      "DisplayText": "Support",
                      "LinkMenuItemPart": {
                        "Name": "Support",
                        "Url": "~/services/support"
                      }
                    }
                  ]
                }
              },
              {
                "ContentItemId": "nav-blog",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Blog",
                "LinkMenuItemPart": {
                  "Name": "Blog",
                  "Url": "~/blog"
                }
              },
              {
                "ContentItemId": "nav-contact",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Contact",
                "LinkMenuItemPart": {
                  "Name": "Contact",
                  "Url": "~/contact"
                }
              }
            ]
          }
        },
        {
          "ContentItemId": "footer-nav",
          "ContentType": "Menu",
          "DisplayText": "Footer Navigation",
          "Latest": true,
          "Published": true,
          "MenuPart": {},
          "TitlePart": {
            "Title": "Footer Navigation"
          },
          "MenuItemsListPart": {
            "MenuItems": [
              {
                "ContentItemId": "footer-privacy",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Privacy Policy",
                "LinkMenuItemPart": {
                  "Name": "Privacy Policy",
                  "Url": "~/privacy"
                }
              },
              {
                "ContentItemId": "footer-terms",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Terms of Service",
                "LinkMenuItemPart": {
                  "Name": "Terms of Service",
                  "Url": "~/terms"
                }
              },
              {
                "ContentItemId": "footer-sitemap",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Sitemap",
                "LinkMenuItemPart": {
                  "Name": "Sitemap",
                  "Url": "~/sitemap.xml"
                }
              }
            ]
          }
        },
        {
          "ContentItemId": "widget-primary-nav",
          "ContentType": "MenuWidget",
          "DisplayText": "Primary Navigation Widget",
          "Latest": true,
          "Published": true,
          "LayerMetadata": {
            "Layer": "Always",
            "Zone": "Navigation",
            "Position": 0
          },
          "MenuWidget": {
            "MenuContentItemId": "primary-nav"
          }
        },
        {
          "ContentItemId": "widget-footer-nav",
          "ContentType": "MenuWidget",
          "DisplayText": "Footer Navigation Widget",
          "Latest": true,
          "Published": true,
          "LayerMetadata": {
            "Layer": "Always",
            "Zone": "Footer",
            "Position": 0
          },
          "MenuWidget": {
            "MenuContentItemId": "footer-nav"
          }
        }
      ]
    }
  ]
}
```

## Example 2: Bootstrap 5 Navbar with Dropdown Support (Razor Alternate)

Create `Views/Menu-PrimaryNavigation.cshtml` to render the primary menu as a responsive Bootstrap 5 navbar:

```cshtml
@{
    var items = (IEnumerable<dynamic>)Model.Items;
}

<nav class="navbar navbar-expand-lg navbar-light bg-light">
    <div class="container">
        <a class="navbar-brand" href="/">My Site</a>
        <button class="navbar-toggler" type="button"
                data-bs-toggle="collapse"
                data-bs-target="#primaryNavbar"
                aria-controls="primaryNavbar"
                aria-expanded="false"
                aria-label="Toggle navigation">
            <span class="navbar-toggler-icon"></span>
        </button>
        <div class="collapse navbar-collapse" id="primaryNavbar">
            <ul class="navbar-nav ms-auto">
                @foreach (var item in items)
                {
                    @await DisplayAsync(item)
                }
            </ul>
        </div>
    </div>
</nav>
```

Create `Views/MenuItem-PrimaryNavigation.cshtml` for each item with dropdown support:

```cshtml
@{
    var items = (IEnumerable<dynamic>)Model.Items;
    var hasChildren = items != null && items.Any();
}

@if (hasChildren)
{
    <li class="nav-item dropdown">
        <a class="nav-link dropdown-toggle"
           href="#"
           role="button"
           data-bs-toggle="dropdown"
           aria-expanded="false">
            @if (!string.IsNullOrEmpty((string)Model.IconClass))
            {
                <i class="@Model.IconClass me-1"></i>
            }
            @Model.Text
        </a>
        <ul class="dropdown-menu">
            @foreach (var child in items)
            {
                <li>
                    <a class="dropdown-item" href="@child.Href">
                        @if (!string.IsNullOrEmpty((string)child.IconClass))
                        {
                            <i class="@child.IconClass me-1"></i>
                        }
                        @child.Text
                    </a>
                </li>
            }
        </ul>
    </li>
}
else
{
    <li class="nav-item">
        <a class="nav-link" href="@Model.Href">
            @if (!string.IsNullOrEmpty((string)Model.IconClass))
            {
                <i class="@Model.IconClass me-1"></i>
            }
            @Model.Text
        </a>
    </li>
}
```

## Example 3: Custom Admin Navigation Provider

Register custom admin menu entries for a module using `INavigationProvider`:

```csharp
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

public sealed class AdminMenuProvider : INavigationProvider
{
    private readonly IStringLocalizer _localizer;

    public AdminMenuProvider(IStringLocalizer<AdminMenuProvider> localizer)
    {
        _localizer = localizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.CompletedTask;
        }

        builder
            .Add(_localizer["Store Management"], "50", store => store
                .AddClass("icon-class-store")
                .Id("storeManagement")
                .Add(_localizer["Products"], "0", products => products
                    .Action("Index", "Product", new { area = "MyModule" })
                    .Permission(Permissions.ManageProducts)
                    .LocalNav())
                .Add(_localizer["Orders"], "1", orders => orders
                    .Action("Index", "Order", new { area = "MyModule" })
                    .Permission(Permissions.ManageOrders)
                    .LocalNav())
                .Add(_localizer["Settings"], "2", settings => settings
                    .Action("Index", "StoreSettings", new { area = "MyModule" })
                    .Permission(Permissions.ManageStoreSettings)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
```

Register the provider in your module's `Startup.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Navigation;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INavigationProvider, AdminMenuProvider>();
    }
}
```

## Example 4: Paginated Blog List with PagerSlim (Liquid)

Render a blog list with slim pagination:

```liquid
<div class="blog-list">
    {% for item in Model.ContentItems %}
        <article class="blog-entry mb-4">
            <h2><a href="{{ item | display_url }}">{{ item.Content.TitlePart.Title }}</a></h2>
            <time datetime="{{ item.PublishedUtc | date: '%Y-%m-%d' }}">
                {{ item.PublishedUtc | date: '%B %d, %Y' }}
            </time>
            <div class="blog-summary">
                {{ item.Content.BlogPost.Subtitle.Text }}
            </div>
        </article>
    {% endfor %}
</div>

{% assign pager = Model.Pager %}
{% if pager %}
    <nav aria-label="Blog pagination" class="mt-4">
        <ul class="pagination justify-content-between">
            {% if pager.PreviousPage %}
                <li class="page-item">
                    <a class="page-link" href="{{ pager.PreviousPage }}">
                        &larr; Newer Posts
                    </a>
                </li>
            {% else %}
                <li class="page-item disabled">
                    <span class="page-link">&larr; Newer Posts</span>
                </li>
            {% endif %}

            {% if pager.NextPage %}
                <li class="page-item">
                    <a class="page-link" href="{{ pager.NextPage }}">
                        Older Posts &rarr;
                    </a>
                </li>
            {% else %}
                <li class="page-item disabled">
                    <span class="page-link">Older Posts &rarr;</span>
                </li>
            {% endif %}
        </ul>
    </nav>
{% endif %}
```

## Example 5: Menu with Icons and Dynamic URLs via Recipe

Create a menu that combines static links with icons and a dynamic `UrlMenuItem`:

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "user-menu",
          "ContentType": "Menu",
          "DisplayText": "User Menu",
          "Latest": true,
          "Published": true,
          "MenuPart": {},
          "TitlePart": {
            "Title": "User Menu"
          },
          "MenuItemsListPart": {
            "MenuItems": [
              {
                "ContentItemId": "user-menu-dashboard",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Dashboard",
                "LinkMenuItemPart": {
                  "Name": "Dashboard",
                  "Url": "~/dashboard"
                },
                "HtmlMenuItemPart": {
                  "IconClass": "fa fa-tachometer-alt"
                }
              },
              {
                "ContentItemId": "user-menu-profile",
                "ContentType": "UrlMenuItem",
                "DisplayText": "My Profile",
                "UrlMenuItemPart": {
                  "Name": "My Profile",
                  "Url": "{{ '~/profile/' | append: User.Identity.Name }}"
                },
                "HtmlMenuItemPart": {
                  "IconClass": "fa fa-user"
                }
              },
              {
                "ContentItemId": "user-menu-notifications",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Notifications",
                "LinkMenuItemPart": {
                  "Name": "Notifications",
                  "Url": "~/notifications"
                },
                "HtmlMenuItemPart": {
                  "IconClass": "fa fa-bell"
                }
              },
              {
                "ContentItemId": "user-menu-settings",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Settings",
                "LinkMenuItemPart": {
                  "Name": "Settings",
                  "Url": "~/settings"
                },
                "HtmlMenuItemPart": {
                  "IconClass": "fa fa-cog"
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
