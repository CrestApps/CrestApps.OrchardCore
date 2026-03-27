# Orchard Core Setup Examples

## Example 1: Blog Site Setup

Setting up a blog site using the Blog recipe:

```bash
# Create the project
dotnet new occms -n MyBlogSite
cd MyBlogSite

# Run the application
dotnet run
```

Navigate to `https://localhost:5001` and complete setup:
- **Site Name**: My Blog
- **Recipe**: Blog
- **Database**: Sqlite
- **Admin User**: admin / Password123!

## Example 2: Custom CMS Project with Modules

Creating a project with custom modules added:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="OrchardCore.Application.Cms.Targets" Version="2.*" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference local custom modules -->
    <ProjectReference Include="../CrestApps.Products/CrestApps.Products.csproj" />
    <ProjectReference Include="../CrestApps.Invoices/CrestApps.Invoices.csproj" />
  </ItemGroup>

</Project>
```

## Example 3: Custom Setup Recipe for an E-Commerce Site

```json
{
  "name": "ECommerce",
  "displayName": "E-Commerce Site",
  "description": "Sets up a basic e-commerce site with products and categories",
  "author": "CrestApps",
  "website": "https://crestapps.com",
  "version": "1.0.0",
  "issetuprecipe": true,
  "categories": ["default"],
  "tags": ["ecommerce"],
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        "OrchardCore.Title",
        "OrchardCore.Alias",
        "OrchardCore.Autoroute",
        "OrchardCore.Html",
        "OrchardCore.Menu",
        "OrchardCore.Navigation",
        "OrchardCore.Themes",
        "OrchardCore.Admin",
        "OrchardCore.Settings",
        "OrchardCore.Media",
        "OrchardCore.Taxonomies",
        "TheTheme",
        "TheAdmin"
      ],
      "disable": []
    },
    {
      "name": "Themes",
      "Site": "TheTheme",
      "Admin": "TheAdmin"
    },
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Product",
          "DisplayName": "Product",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "TitlePart",
              "Name": "TitlePart",
              "Settings": {}
            },
            {
              "PartName": "AutoroutePart",
              "Name": "AutoroutePart",
              "Settings": {
                "AutoroutePartSettings": {
                  "Pattern": "products/{{ ContentItem | display_text | slugify }}"
                }
              }
            },
            {
              "PartName": "HtmlBodyPart",
              "Name": "HtmlBodyPart",
              "Settings": {}
            }
          ]
        }
      ],
      "ContentParts": []
    }
  ]
}
```

## Example 4: Auto-Setup for CI/CD

```json
{
  "OrchardCore": {
    "OrchardCore_AutoSetup": {
      "AutoSetupPath": "",
      "Tenants": [
        {
          "ShellName": "Default",
          "SiteName": "My Test Site",
          "SiteTimeZone": "America/New_York",
          "DatabaseProvider": "Sqlite",
          "RecipeName": "Blog",
          "UserName": "admin",
          "Email": "admin@example.com",
          "Password": "Test123!"
        }
      ]
    }
  }
}
```
