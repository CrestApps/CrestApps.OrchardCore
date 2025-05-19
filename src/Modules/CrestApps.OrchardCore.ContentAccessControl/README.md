## Content Access Control Feature

This feature enables you to restrict access to content items based on user roles. Once enabled, you can add the `RolePickerPart` to any content type. This part allows you to specify one or more roles required to access the content item. You can attach the part using the content definitions user interface or by adding a migration as shown below:

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
                .WithDisplayName("Limit access to selected roles")
                .WithSettings(new RolePickerPartSettings()
                {
                    AllowSelectMultiple = true,
                    Required = true,
                    Hint = "Select one or more roles",
                    ExcludedRoles = ["Administrator", "Authenticated", "Anonymous"],
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