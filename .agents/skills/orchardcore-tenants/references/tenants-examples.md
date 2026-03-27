# Orchard Core Tenants Examples

## Example 1: Multi-Tenant Blog Setup

Setting up a SaaS application with a Blog tenant:

```bash
# Create the project
dotnet new occms -n MyMultiSite
cd MyMultiSite
dotnet run

# Set up the Default tenant with the SaaS recipe
# Navigate to https://localhost:5001 and complete setup:
# - Recipe: SaaS
# - Database: Sqlite
# - Admin: admin / Password123!
```

After setup, create a Blog tenant:
1. Go to **Configuration > Tenants**.
2. Click **Create Tenant**.
3. Fill in:
   - **Name**: Blog
   - **URL Prefix**: blog
   - **Database Provider**: Sqlite
4. Click **Create**, then **Setup** on the new tenant.
5. During tenant setup:
   - **Recipe**: Blog
   - **Site Name**: Company Blog
   - **Admin**: admin / Password123!
6. Access the blog at `https://localhost:5001/blog`.

## Example 2: Creating Multiple Tenants via Recipe

```json
{
  "steps": [
    {
      "name": "Tenants",
      "Tenants": [
        {
          "Name": "Marketing",
          "RequestUrlPrefix": "marketing",
          "RequestUrlHost": "",
          "State": "Uninitialized",
          "FeatureProfiles": [],
          "Description": "Marketing department site"
        },
        {
          "Name": "Engineering",
          "RequestUrlPrefix": "engineering",
          "RequestUrlHost": "",
          "State": "Uninitialized",
          "FeatureProfiles": [],
          "Description": "Engineering department site"
        },
        {
          "Name": "Sales",
          "RequestUrlPrefix": "sales",
          "RequestUrlHost": "",
          "State": "Uninitialized",
          "FeatureProfiles": [],
          "Description": "Sales department site"
        }
      ]
    }
  ]
}
```

## Example 3: Tenant-Aware Service

```csharp
using OrchardCore.Environment.Shell;

public sealed class TenantBrandingService
{
    private readonly ShellSettings _shellSettings;

    public TenantBrandingService(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public string GetTenantLogo()
    {
        return _shellSettings.Name switch
        {
            "Marketing" => "/media/logos/marketing.png",
            "Engineering" => "/media/logos/engineering.png",
            "Sales" => "/media/logos/sales.png",
            _ => "/media/logos/default.png"
        };
    }
}
```
