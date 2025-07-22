## Enhanced Roles

Extends the Orchard Core Roles module with additional reusable components like `RolePickerPart`.

### RolePickerPart

This add a role-picker to any content type, you can use the Orchard Core content types UI to add it to any content type. Or, you can do it via code using a migration. for example:

```csharp
internal sealed class CustomContentTypeMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CustomContentTypeMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("CustomContentType", type => type
            .WithPart<RolePickerPart>(part => part
                .WithDisplayName("Roles")
                .WithSettings(new RolePickerPartSettings()
                {
                    AllowSelectMultiple = true,
                    Required = true,
                    Hint = "Select one or more roles",
                    ExcludedRoles = ["Authenticated", "Anonymous"],
                })
            )
        );

        return 1;
    }
}
```

Finally, register this migration:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<CustomContentTypeMigrations>();
    }
}
```