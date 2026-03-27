# Users & Roles Examples

## Example 1: Custom Permission Provider

```csharp
using OrchardCore.Security.Permissions;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageProducts =
        new("ManageProducts", "Manage product catalog");

    public static readonly Permission ViewProducts =
        new("ViewProducts", "View product catalog", new[] { ManageProducts });

    public static readonly Permission ManageOrders =
        new("ManageOrders", "Manage customer orders");

    public static readonly Permission ViewOrders =
        new("ViewOrders", "View customer orders", new[] { ManageOrders });

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        return Task.FromResult<IEnumerable<Permission>>(new[]
        {
            ManageProducts,
            ViewProducts,
            ManageOrders,
            ViewOrders
        });
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return new[]
        {
            new PermissionStereotype
            {
                Name = "Administrator",
                Permissions = new[] { ManageProducts, ManageOrders }
            },
            new PermissionStereotype
            {
                Name = "Editor",
                Permissions = new[] { ViewProducts, ViewOrders }
            },
            new PermissionStereotype
            {
                Name = "Contributor",
                Permissions = new[] { ViewProducts }
            }
        };
    }
}
```

## Example 2: Roles Recipe

```json
{
  "steps": [
    {
      "name": "Roles",
      "Roles": [
        {
          "Name": "ProductManager",
          "Description": "Can manage products and view orders",
          "Permissions": [
            "ManageProducts",
            "ViewProducts",
            "ViewOrders",
            "AccessAdminPanel"
          ]
        },
        {
          "Name": "OrderProcessor",
          "Description": "Can manage orders",
          "Permissions": [
            "ManageOrders",
            "ViewOrders",
            "ViewProducts",
            "AccessAdminPanel"
          ]
        },
        {
          "Name": "Customer",
          "Description": "Registered customer with basic access",
          "Permissions": [
            "ViewProducts",
            "ViewOwnOrders"
          ]
        }
      ]
    }
  ]
}
```

## Example 3: Custom User Settings

```csharp
// Migration to create a custom user profile
public int Create()
{
    _contentDefinitionManager.AlterTypeDefinition("UserProfile", type => type
        .DisplayedAs("User Profile")
        .Stereotype("CustomUserSettings")
        .WithPart("UserProfile", part => part
            .WithPosition("0")
        )
    );

    _contentDefinitionManager.AlterPartDefinition("UserProfile", part => part
        .WithField("FirstName", field => field
            .OfType("TextField")
            .WithDisplayName("First Name")
            .WithPosition("0")
        )
        .WithField("LastName", field => field
            .OfType("TextField")
            .WithDisplayName("Last Name")
            .WithPosition("1")
        )
        .WithField("ProfilePicture", field => field
            .OfType("MediaField")
            .WithDisplayName("Profile Picture")
            .WithPosition("2")
        )
        .WithField("Bio", field => field
            .OfType("TextField")
            .WithDisplayName("Bio")
            .WithEditor("TextArea")
            .WithPosition("3")
        )
    );

    return 1;
}
```
